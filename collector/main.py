"""
自动化码头设备数据采集平台 - 程序入口

启动流程：
1. 加载配置
2. 初始化各组件（设备、驱动、队列、存储）
3. 连接设备
4. 启动调度器 + 聚合器 + 写入器
5. 等待信号退出
"""

import asyncio
import os
import signal

from core.config import (
    CollectorConfig,
    DeviceConfig,
    InterfaceIngestConfig,
    TDengineConfig,
    VariableConfig,
)
from core.config_loader import load_collector_config_from_env
from core.exceptions import ConfigError
from core.config_validator import validate_config
from core.logger import setup_logger
from core.logger import get_logger
from core.opcda_mapper import OpcDaTagMapper
from core.system_log import SystemLogWriter
from device.manager import DeviceManager
from collector.scheduler import TaskScheduler
from collector.executor import TaskExecutor
from collector.task import CollectTask
from data_queue.buffer import DataBuffer, DataPoint
from data_queue.aggregator import DataAggregator
from storage.tdengine import TDengineConnection
from storage.batch_writer import BatchWriter
from monitor.health import HealthChecker
from monitor.ingest_server import InterfaceIngestServer
from monitor.metrics import MetricsCollector

logger = get_logger("main")


class CollectorApp:
    """采集应用主类"""

    def __init__(self, config: CollectorConfig, config_db_manager=None):
        self.config = config
        self.config_db_manager = config_db_manager
        self._shutdown = asyncio.Event()
        self._aggregator_task = None
        self._health_task = None
        self._closed = False

        # 初始化组件
        self.system_logger = (
            SystemLogWriter(config_db_manager)
            if config_db_manager is not None
            else None
        )
        status_reporter = (
            self.config_db_manager.update_device_status
            if self.config_db_manager is not None
            else None
        )
        self.device_manager = DeviceManager(config, status_reporter=status_reporter)
        self.data_buffer = DataBuffer(max_size=config.queue_max_size)
        self.aggregator = DataAggregator(config, self.data_buffer)
        self.tdengine = TDengineConnection(config.tdengine)
        self.batch_writer = BatchWriter(
            self.tdengine,
            fake_mode=config.fake_mode_active,
            system_logger=self.system_logger,
        )
        self.scheduler = TaskScheduler()
        self.executor = TaskExecutor(
            self.data_buffer,
            device_manager=self.device_manager,
            system_logger=self.system_logger,
        )
        self.health_checker = HealthChecker(
            self.device_manager, self.tdengine, interval=30
        )
        self.metrics = MetricsCollector()

        # OPC DA tag 映射器
        self.opcda_mapper = OpcDaTagMapper(config.variables)

        self.ingest_server = None
        if self.config.interface_ingest.enabled:
            ingest_cfg = self.config.interface_ingest
            self.ingest_server = InterfaceIngestServer(
                host=ingest_cfg.host,
                port=ingest_cfg.port,
                ingest_path=ingest_cfg.path,
                api_key=ingest_cfg.api_key,
                max_batch_size=ingest_cfg.max_batch_size,
                max_body_bytes=ingest_cfg.max_body_bytes,
            )

        # 设置聚合器输出回调，将flush的数据传递给写入器
        self.aggregator.set_output_callback(self.batch_writer.write_sync)

    async def start(self) -> None:
        """启动采集服务"""
        logger.info("Collector starting...")
        if self.system_logger is not None:
            await self.system_logger.start()
            self.system_logger.emit(
                level="INFO",
                log_type="system",
                source="collector.main",
                content="Collector startup requested",
                detail_json={
                    "devices_configured": len(self.config.devices),
                    "variables_configured": len(self.config.variables),
                    "fake_mode": self.config.fake_mode_active,
                    "interface_ingest_enabled": self.config.interface_ingest.enabled,
                },
            )

        # 1. 注册设备
        self.device_manager.register_devices()

        # 2. 连接TDengine
        await self.tdengine.connect()
        if self.system_logger is not None:
            self.system_logger.emit(
                level="INFO",
                log_type="storage",
                source="collector.tdengine",
                content="TDengine connection established",
                detail_json={
                    "host": self.config.tdengine.host,
                    "port": self.config.tdengine.port,
                    "database": self.config.tdengine.database,
                },
            )
        # 初始化存储拓扑（设备类型数据库 -> 设备超级表 -> 变量子表）
        await self.tdengine.init_storage_topology(
            self.device_manager.get_all_devices(),
            self.config.variables,
        )
        if self.config.fake_mode_active:
            await self._write_fake_bootstrap_points()

        # 3. 连接所有设备
        await self.device_manager.connect_all()

        # 4. 构建采集任务
        tasks = []
        for device in self.device_manager.get_all_devices():
            for scan_task in device.scan_tasks:
                collect_task = CollectTask(device=device, scan_task=scan_task)
                tasks.append(collect_task)
        self.scheduler.add_tasks(tasks)

        # 5. 启动调度器
        await self.scheduler.start(self.executor.execute)

        # 6. 启动外部接口采集协议（HTTP Push）
        if self.ingest_server is not None:
            await self.ingest_server.start(
                data_buffer=self.data_buffer,
                event_loop=asyncio.get_running_loop(),
                opcda_mapper=self.opcda_mapper,
            )

        # 7. 启动聚合器（内部通过回调将数据传递给写入器）
        self._aggregator_task = asyncio.create_task(self.aggregator.run())

        # 8. 启动健康检查
        self._health_task = asyncio.create_task(self.health_checker.run())

        # 打印启动信息
        logger.info("Collector started successfully")
        self._log_status()
        if self.system_logger is not None:
            self.system_logger.emit(
                level="INFO",
                log_type="system",
                source="collector.main",
                content="Collector started successfully",
                detail_json={
                    "devices": len(self.device_manager.devices),
                    "tasks": len(self.scheduler.tasks),
                    "variables": len(self.config.variables),
                    "queue_max_size": self.config.queue_max_size,
                    "fake_mode": self.config.fake_mode_active,
                },
            )

        # 等待退出信号
        await self._shutdown.wait()
        await self.shutdown()

    async def shutdown(self) -> None:
        """优雅停机；允许启动失败后的半初始化清理。"""
        if self._closed:
            return
        self._closed = True

        # 清理：先停采集，再 flush 聚合缓存和等待写入，最后断开外部资源。
        logger.info("Shutting down...")
        if self.system_logger is not None:
            self.system_logger.emit(
                level="INFO",
                log_type="system",
                source="collector.main",
                content="Collector shutdown started",
                detail_json={
                    "queue_size": self.data_buffer.qsize(),
                    "aggregator_cache": self.aggregator.cache_size,
                },
            )
        if self._health_task is not None:
            self._health_task.cancel()

        await self.scheduler.stop()

        # 停止聚合器轮询任务
        self.aggregator.stop()
        if self._aggregator_task is not None:
            try:
                await self._aggregator_task
            except asyncio.CancelledError:
                pass
            finally:
                self._aggregator_task = None

        # Drain DataBuffer 队列剩余点到 aggregator
        remaining = self.data_buffer.drain()
        if remaining:
            logger.info("Draining %d remaining points from buffer to aggregator", len(remaining))
            for point in remaining:
                self.aggregator.add(point)

        # Flush aggregator cache
        flushed = await self.aggregator.flush_to_output()
        if flushed:
            logger.info("Flushed aggregator cache during shutdown: points=%s", flushed)
        await self.batch_writer.drain(timeout=10)

        for t in [self._health_task]:
            if t is None:
                continue
            try:
                await t
            except asyncio.CancelledError:
                pass
        self._health_task = None

        if self.ingest_server is not None:
            await self.ingest_server.stop()
        await self.device_manager.disconnect_all()
        await self.tdengine.close()

        self._log_status()
        logger.info("Collector stopped")
        if self.system_logger is not None:
            self.system_logger.emit(
                level="INFO",
                log_type="system",
                source="collector.main",
                content="Collector stopped",
                detail_json={
                    "buffer": self.data_buffer.stats,
                    "aggregator": self.aggregator.stats,
                    "executor": self.executor.stats,
                    "batch_writer": self.batch_writer.stats,
                },
            )
            await self.system_logger.stop()
        if self.config_db_manager is not None:
            await self.config_db_manager.close()
            self.config_db_manager = None

    def _log_status(self) -> None:
        """打印当前状态"""
        logger.info(
            f"Status: devices={len(self.device_manager.devices)}, "
            f"tasks={len(self.scheduler.tasks)}, "
            f"queue={self.data_buffer.qsize()}, "
            f"aggregator_cache={self.aggregator.cache_size}"
        )

    def stop(self) -> None:
        """触发停止"""
        logger.info("Stop requested")
        self._shutdown.set()

    async def _write_fake_bootstrap_points(self) -> None:
        """在假数据模式下，写入一批可识别的联调数据。"""
        points = [
            DataPoint("QC_FAKE100", "900001", 23.4),
            DataPoint("QC_FAKE100", "900002", 67.8),
            DataPoint("ASC_FAKE200", "900101", 10.5),
            DataPoint("ASC_FAKE200", "900102", 11.6),
        ]
        logger.info("[FAKE-DATA] bootstrap writing demo points=%s", len(points))
        await self.batch_writer.write(points)


def _build_fake_topology(config: CollectorConfig, reason: str) -> CollectorConfig:
    """构造仅用于联调的假拓扑，避免 MySQL 未就绪时阻断 TDengine 验证。"""
    logger.warning("[FAKE-DATA] fallback enabled, reason: %s", reason)
    config.devices = [
        DeviceConfig(
            device_id="QC_FAKE100",
            device_type="QC",
            protocol="s7",
            ip="127.0.0.1",
            endpoint="127.0.0.1:102",
            port=102,
            rack=0,
            slot=1,
            scan_rate=1,
            device_pk=900001,
        ),
        DeviceConfig(
            device_id="ASC_FAKE200",
            device_type="ASC",
            protocol="ge_srtp",
            ip="127.0.0.1",
            endpoint="127.0.0.1:18245",
            port=18245,
            rack=0,
            slot=1,
            scan_rate=1,
            device_pk=900002,
        ),
    ]
    config.variables = [
        VariableConfig("900001", "QC_FAKE100", "DB398.DBD0", "REAL", 1, True, 900001),
        VariableConfig("900002", "QC_FAKE100", "DB399.DBD0", "REAL", 1, True, 900002),
        VariableConfig("900101", "ASC_FAKE200", "%R100", "INT", 1, True, 900101),
        VariableConfig("900102", "ASC_FAKE200", "%R101", "INT", 1, True, 900102),
    ]
    config.fake_mode_active = True
    return config


def create_sample_config() -> CollectorConfig:
    """创建示例运行配置（设备/点位实际由 MySQL 动态加载）"""
    return CollectorConfig(
        tdengine=TDengineConfig(
            host="localhost",
            port=6041,
            user="root",
            password="taosdata",
            database="device_data",
        ),
        devices=[],
        variables=[],
        batch_size=1000,
        flush_interval=1,
        max_window_seconds=600,
        log_level="INFO",
        interface_ingest=InterfaceIngestConfig(
            enabled=True,
            host="0.0.0.0",
            port=8090,
            path="/ingest",
            api_key=None,
            max_batch_size=1000,
        ),
    )


async def main():
    """程序入口"""
    # 先用默认级别初始化日志，读取配置后再调整
    setup_logger(level="INFO")

    # 从配置文件加载
    try:
        config = load_collector_config_from_env()
    except ConfigError as e:
        logger.error(f"Config load failed: {e}")
        logger.error(
            "Please provide config file (default: config/settings.toml) "
            "or set COLLECTOR_CONFIG=<path>"
        )
        raise

    # 用配置文件中的日志级别重新初始化
    setup_logger(level=config.log_level)
    logger.info("Using config file: %s", os.getenv("COLLECTOR_CONFIG", "config/settings.toml"))

    runtime_db_manager = None
    try:
        from core.config_db import ConfigDBManager, DatabaseConfig
    except ModuleNotFoundError as e:
        if config.enable_fake_bootstrap:
            config = _build_fake_topology(config, f"MySQL loader import failed: {e}")
        else:
            raise ConfigError(
                "MySQL config loader dependency is missing. "
                "Please install requirements: pip install -r requirements.txt "
                "or set runtime.enable_fake_bootstrap=true for temporary fake validation."
            ) from e
    else:
        # 设备与点位配置以 MySQL 为准（覆盖 TOML 中 devices/variables）
        db_manager = ConfigDBManager(DatabaseConfig.from_mysql_config(config.mysql))
        try:
            config = await db_manager.load_config(base_config=config)
            runtime_db_manager = db_manager
            logger.info(
                "Loaded topology from MySQL: devices=%s, variables=%s",
                len(config.devices),
                len(config.variables),
            )
        except Exception as e:
            if config.enable_fake_bootstrap:
                await db_manager.close()
                config = _build_fake_topology(config, f"MySQL topology load failed: {e}")
            else:
                await db_manager.close()
                logger.error(f"Failed to load devices/variables from MySQL: {e}")
                raise ConfigError(
                    "Device/variable config must be loaded from MySQL. "
                    "Please check [mysql] in config/settings.toml and table data "
                    "(device rows exist, device_variable is_storage=1). "
                    "For temporary fake validation set runtime.enable_fake_bootstrap=true."
                ) from e

    validation = validate_config(config)
    for warning in validation.warnings:
        logger.warning("Config validation warning: %s", warning)
    if not validation.valid:
        for error in validation.errors:
            logger.error("Config validation error: %s", error)
        if runtime_db_manager is not None:
            await runtime_db_manager.close()
        raise ConfigError("Collector config validation failed; see logs for details.")

    # 创建并启动应用
    app = CollectorApp(config, config_db_manager=runtime_db_manager)

    # 注册信号处理
    loop = asyncio.get_event_loop()
    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, app.stop)
        except NotImplementedError:
            # Windows 的 ProactorEventLoop 不支持 add_signal_handler
            signal.signal(sig, lambda *_: app.stop())

    try:
        await app.start()
    except KeyboardInterrupt:
        app.stop()
    except Exception as e:
        logger.error(f"Fatal error: {e}")
        raise
    finally:
        await app.shutdown()


if __name__ == "__main__":
    asyncio.run(main())
