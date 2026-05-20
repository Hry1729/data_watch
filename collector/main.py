"""
自动化码头设备数据采集平台 - 程序入口

启动流程：
1. 加载配置
2. 初始化各组件（设备、驱动、队列、存储）
3. 连接设备
4. 启动调度器 + 聚合器 + 写入器
5. 等待信号退出

支持 --dry-run 参数只做配置预检不启动采集。
"""

import argparse
import asyncio
import os
import signal
import time

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
from core.dry_run import dry_run_main
from device.manager import DeviceManager
from device.device import Variable
from collector.scheduler import TaskScheduler
from collector.executor import TaskExecutor
from collector.task import CollectTask
from data_queue.buffer import DataBuffer
from storage.redis_client import get_redis
from storage.redis_writer import RedisWriter
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
        self._health_task = None
        self._reload_task = None
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
        self.scheduler = TaskScheduler()
        redis_client = get_redis(config)
        self.redis_writer = RedisWriter(redis_client, config)
        self.executor = TaskExecutor(
            self.data_buffer,
            device_manager=self.device_manager,
            system_logger=self.system_logger,
            redis_writer=self.redis_writer,
        )
        self.health_checker = HealthChecker(self.device_manager, interval=30)
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

        # 0. 检查 Redis 连通性
        try:
            self.redis_writer.r.ping()
            logger.info(
                "Redis connected: %s:%s db=%s",
                self.config.redis.host, self.config.redis.port, self.config.redis.db,
            )
        except Exception as exc:
            logger.warning("Redis ping failed: %s — realtime push will be unavailable", exc)

        # 1. 注册设备
        self.device_manager.register_devices()
        if self.config.fake_mode_active:
            self._write_fake_bootstrap_points()

        # 2. 连接所有设备
        await self.device_manager.connect_all()

        # 3. 构建采集任务
        tasks = []
        for device in self.device_manager.get_all_devices():
            for scan_task in device.scan_tasks:
                collect_task = CollectTask(device=device, scan_task=scan_task)
                tasks.append(collect_task)
        self.scheduler.add_tasks(tasks)

        # 4. 启动调度器
        await self.scheduler.start(self.executor.execute)

        # 5. 启动外部接口采集协议（HTTP Push）
        if self.ingest_server is not None:
            await self.ingest_server.start(
                data_buffer=self.data_buffer,
                event_loop=asyncio.get_running_loop(),
                opcda_mapper=self.opcda_mapper,
                redis_writer=self.redis_writer,
                realtime_config=self.config,
            )

        # 6. 启动健康检查
        self._health_task = asyncio.create_task(self.health_checker.run())

        # 7. 启动配置热更新轮询（仅在 DB 模式下）
        if self.config_db_manager is not None:
            self._reload_task = asyncio.create_task(self._config_reload_loop())

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
                    "storage_path": "redis_only",
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
                },
            )
        if self._reload_task is not None:
            self._reload_task.cancel()
            try:
                await self._reload_task
            except asyncio.CancelledError:
                pass
            self._reload_task = None

        if self._health_task is not None:
            self._health_task.cancel()

        await self.scheduler.stop()

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
                    "executor": self.executor.stats,
                    "storage_path": "redis_only",
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
            f"queue={self.data_buffer.qsize()}"
        )

    def stop(self) -> None:
        """触发停止"""
        logger.info("Stop requested")
        self._shutdown.set()

    async def _config_reload_loop(self) -> None:
        """每 10 秒轮询 MySQL，检测设备 variable_config_version 变化并热重载。"""
        tracked: dict[str, int] = {d.device_id: d.config_version for d in self.config.devices}
        while not self._shutdown.is_set():
            try:
                await asyncio.sleep(10)
                current = await self.config_db_manager.get_device_config_versions()
                for device_id, new_ver in current.items():
                    old_ver = tracked.get(device_id)
                    if old_ver is not None and new_ver != old_ver:
                        logger.info(
                            "Device %s config version changed %d -> %d, reloading",
                            device_id, old_ver, new_ver,
                        )
                        await self._reload_device(device_id, new_ver)
                        tracked[device_id] = new_ver
            except asyncio.CancelledError:
                break
            except Exception as exc:
                logger.error("Config reload loop error: %s", exc)

    async def _reload_device(self, device_id: str, new_version: int) -> None:
        """重建单台设备的变量和采集任务，不影响其他设备。"""
        device = self.device_manager.get_device(device_id)
        if device is None or device.device_pk is None:
            logger.warning("Cannot reload device %s: not found or missing device_pk", device_id)
            return
        device_cfg = next((d for d in self.config.devices if d.device_id == device_id), None)
        if device_cfg is None:
            return
        try:
            new_var_cfgs = await self.config_db_manager.load_variables_for_device(
                device.device_pk, device_cfg
            )
            device.variables = []
            for var_cfg in new_var_cfgs:
                device.add_variable(Variable(
                    variable_id=var_cfg.variable_id,
                    address=var_cfg.address,
                    data_type=var_cfg.data_type,
                    scan_rate=var_cfg.scan_rate,
                    is_storage=var_cfg.is_storage,
                    group_code=var_cfg.group_code,
                ))
            device.build_scan_tasks()
            device.config_version = new_version
            new_tasks = [CollectTask(device=device, scan_task=st) for st in device.scan_tasks]
            await self.scheduler.reload_device_tasks(device_id, new_tasks)
            logger.info(
                "Device %s reloaded: version=%d, variables=%d, tasks=%d",
                device_id, new_version, len(device.variables), len(new_tasks),
            )
        except Exception as exc:
            logger.error("Failed to reload device %s: %s", device_id, exc)

    def _write_fake_bootstrap_points(self) -> None:
        """在假数据模式下，写入一批可识别的 Redis 联调数据。"""
        ts_ms = int(time.time() * 1000)
        frames = [
            ("QC_FAKE100", "qc", {"900001": "23.4", "900002": "67.8"}),
            ("ASC_FAKE200", "asc", {"900101": "10.5", "900102": "11.6"}),
        ]
        logger.info("[FAKE-DATA] bootstrap writing Redis demo frames=%s", len(frames))
        for device_code, device_type, values in frames:
            fields = {
                var_id: {"v": value, "t": ts_ms, "q": 0}
                for var_id, value in values.items()
            }
            frame = {
                "device_code": device_code,
                "group_code": "g001",
                "ts": ts_ms,
                "scan_rate": 1000,
                "values": values,
            }
            self.redis_writer.write_latest(device_code, "g001", fields)
            self.redis_writer.write_window(device_code, "g001", 1000, frame)
            self.redis_writer.write_stream(
                device_code,
                device_type,
                "g001",
                1000,
                ts_ms,
                1,
                values,
            )


def _build_fake_topology(config: CollectorConfig, reason: str) -> CollectorConfig:
    """构造仅用于联调的假拓扑，避免 MySQL 未就绪时阻断 Redis 链路验证。"""
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
    # 解析命令行参数
    parser = argparse.ArgumentParser(
        description="设备数据采集平台",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="只做配置预检，不启动采集。输出设备数、点位数、合并块数量等统计信息。",
    )
    parser.add_argument(
        "--config",
        type=str,
        default=None,
        help="配置文件路径 (默认: config/settings.toml 或环境变量 COLLECTOR_CONFIG)",
    )
    args = parser.parse_args()

    # 先用默认级别初始化日志，读取配置后再调整
    setup_logger(level="INFO")

    # 设置配置文件路径
    if args.config:
        os.environ["COLLECTOR_CONFIG"] = args.config

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

    # 用配置文件中的日志参数重新初始化
    setup_logger(
        level=config.log_level,
        log_file=config.log_file,
        log_max_bytes=config.log_max_bytes,
        log_backup_count=config.log_backup_count,
    )
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

    # Dry-run 模式：只做配置预检，不启动采集
    if args.dry_run:
        exit_code = await dry_run_main(config)
        if runtime_db_manager is not None:
            await runtime_db_manager.close()
        return exit_code

    # 创建并启动应用
    app = CollectorApp(config, config_db_manager=runtime_db_manager)

    # 注册信号处理
    loop = asyncio.get_running_loop()
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

    return 0


if __name__ == "__main__":
    exit_code = asyncio.run(main())
    if exit_code is not None and exit_code != 0:
        raise SystemExit(exit_code)
