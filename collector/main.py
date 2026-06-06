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
from pathlib import Path
import asyncio
import os
import signal
import time

from core.config import (
    CollectorConfig,
    DeviceConfig,
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
from collector.scheduler import TaskScheduler
from collector.executor import TaskExecutor
from collector.task import CollectTask
from storage.redis_client import get_redis, close_pool
from storage.redis_writer import RedisWriter
from monitor.health import HealthChecker
from monitor.ingest_server import InterfaceIngestServer

logger = get_logger("main")


def _resolve_runtime_dir(arg_value: str | None) -> Path:
    if arg_value:
        return Path(arg_value)
    home = os.environ.get("DATAWATCH_HOME")
    if home:
        return Path(home) / "runtime"
    return Path(r"C:\ProgramData\DataWatch\runtime")


async def _watch_shutdown_flag(app: "CollectorApp", flag_path: Path, interval: float = 1.0) -> None:
    while not app._shutdown.is_set():
        try:
            if flag_path.exists():
                logger.info("shutdown_flag detected at %s, requesting stop", flag_path)
                app.stop()
                return
        except OSError:
            pass  # 文件系统瞬时异常不阻断 watchdog
        await asyncio.sleep(interval)


class CollectorApp:
    """采集应用主类"""

    def __init__(self, config: CollectorConfig, config_db_manager=None, port_mapping_file=None):
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
        self.device_manager = DeviceManager(
            config, status_reporter=status_reporter, port_mapping_file=port_mapping_file
        )
        self.scheduler = TaskScheduler()
        redis_client = get_redis(config)
        self.redis_writer = RedisWriter(redis_client, config)
        self.executor = TaskExecutor(
            device_manager=self.device_manager,
            system_logger=self.system_logger,
            redis_writer=self.redis_writer,
        )
        self.health_checker = HealthChecker(self.device_manager, interval=30)

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

        # 清理：先停采集与配置热更新，再停接口服务，最后断开设备连接。
        logger.info("Shutting down...")
        if self.system_logger is not None:
            self.system_logger.emit(
                level="INFO",
                log_type="system",
                source="collector.main",
                content="Collector shutdown started",
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
        close_pool()

        self._log_status()
        logger.info("Collector stopped")
        if self.system_logger is not None:
            self.system_logger.emit(
                level="INFO",
                log_type="system",
                source="collector.main",
                content="Collector stopped",
                detail_json={
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
            f"tasks={len(self.scheduler.tasks)}"
        )

    def stop(self) -> None:
        """触发停止"""
        logger.info("Stop requested")
        self._shutdown.set()

    async def _config_reload_loop(self) -> None:
        """每 10 秒轮询 MySQL，处理设备新增 / 删除（禁用） / 版本变更。

        get_device_config_versions() 已过滤 enable==1，因此从当前集合消失即按
        下线处理（删除与禁用同一路径）。每轮只在检测到变更时拉取一次完整快照。
        """
        tracked: dict[str, int] = {d.device_id: d.config_version for d in self.config.devices}
        while not self._shutdown.is_set():
            try:
                await asyncio.sleep(10)
                current = await self.config_db_manager.get_device_config_versions()
                current_ids = set(current.keys())
                tracked_ids = set(tracked.keys())
                added = current_ids - tracked_ids
                removed = tracked_ids - current_ids
                changed = {
                    d for d in (current_ids & tracked_ids) if current[d] != tracked[d]
                }

                if not (added or removed or changed):
                    continue

                logger.info(
                    "Topology change detected: added=%s removed=%s changed=%s",
                    sorted(added), sorted(removed), sorted(changed),
                )

                # 约束1：本轮只拉取一次完整配置快照
                devices, variables = await self.config_db_manager.load_devices_and_variables()
                dev_by_id = {d.device_id: d for d in devices}
                vars_by_dev: dict[str, list] = {}
                for var_cfg in variables:
                    vars_by_dev.setdefault(var_cfg.device_id, []).append(var_cfg)

                # 单台设备失败不应中断整轮，否则收尾的权威快照同步会被跳过，
                # 导致 tracked / self.config / mapper / ingest 元数据长期不一致。
                # 删除 / 禁用
                for device_id in removed:
                    try:
                        await self._remove_device(device_id)
                        tracked.pop(device_id, None)
                    except Exception as exc:
                        logger.error("Failed to remove device %s: %s", device_id, exc)

                # 新增
                for device_id in added:
                    dev_cfg = dev_by_id.get(device_id)
                    if dev_cfg is None:
                        continue
                    try:
                        await self._add_device(dev_cfg, vars_by_dev.get(device_id, []))
                        tracked[device_id] = dev_cfg.config_version
                    except Exception as exc:
                        logger.error("Failed to add device %s: %s", device_id, exc)

                # 版本变更
                for device_id in changed:
                    dev_cfg = dev_by_id.get(device_id)
                    if dev_cfg is None:
                        continue
                    try:
                        await self._apply_device_variables(
                            device_id, dev_cfg, vars_by_dev.get(device_id, [])
                        )
                        tracked[device_id] = dev_cfg.config_version
                    except Exception as exc:
                        logger.error("Failed to reload device %s: %s", device_id, exc)

                # 约束4：无论个别设备是否失败，都以本轮完整快照刷新
                # 权威内存配置，再刷新 mapper 与 ingest 元数据
                self.config.devices = devices
                self.config.variables = variables
                self.opcda_mapper.rebuild(self.config.variables)
                if self.ingest_server is not None:
                    self.ingest_server.update_metadata(self.config)
            except asyncio.CancelledError:
                break
            except Exception as exc:
                logger.error("Config reload loop error: %s", exc)

    async def _add_device(self, dev_cfg, var_cfgs) -> None:
        """运行期注册并连接一台新设备，注册其采集任务。"""
        device = self.device_manager.add_device(dev_cfg, var_cfgs)
        if device is None:
            return
        # 与启动行为一致：新增设备自动连接 PLC（external-push 仅置 online）
        ok = await self.device_manager.connect_device(device)
        if not ok:
            await self.device_manager.schedule_reconnect(device)
        new_tasks = [CollectTask(device=device, scan_task=st) for st in device.scan_tasks]
        if new_tasks:
            await self.scheduler.reload_device_tasks(dev_cfg.device_id, new_tasks)
        logger.info(
            "Runtime added device %s: connected=%s tasks=%d",
            dev_cfg.device_id, ok, len(new_tasks),
        )

    async def _remove_device(self, device_id: str) -> None:
        """运行期下线一台设备。约束2：先取消调度循环，再交由 device_manager 处理。"""
        await self.scheduler.reload_device_tasks(device_id, [])
        await self.device_manager.remove_device(device_id)
        logger.info("Runtime removed device %s", device_id)

    async def _apply_device_variables(self, device_id: str, dev_cfg, var_cfgs) -> None:
        """版本变更：重建单台设备的变量与采集任务，不影响其他设备。"""
        device = self.device_manager.rebuild_device_variables(device_id, var_cfgs)
        if device is None:
            logger.warning("Cannot reload device %s: not found", device_id)
            return
        device.config_version = dev_cfg.config_version
        new_tasks = [CollectTask(device=device, scan_task=st) for st in device.scan_tasks]
        await self.scheduler.reload_device_tasks(device_id, new_tasks)
        logger.info(
            "Device %s reloaded: version=%d, variables=%d, tasks=%d",
            device_id, dev_cfg.config_version, len(device.variables), len(new_tasks),
        )

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
    parser.add_argument("--runtime-dir", type=str, default=None)
    parser.add_argument(
        "--port-mapping",
        type=str,
        default=None,
        help="权威端口映射文件路径（Agent 生成）。指定后 center 采集端口只读该文件、"
             "不再动态探测分配。未指定时回退环境变量 COLLECTOR_PORT_MAPPING。",
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
    port_mapping_file = args.port_mapping or os.environ.get("COLLECTOR_PORT_MAPPING")
    app = CollectorApp(
        config,
        config_db_manager=runtime_db_manager,
        port_mapping_file=port_mapping_file,
    )
    runtime_dir = _resolve_runtime_dir(args.runtime_dir)
    flag_path = runtime_dir / "collector.shutdown_flag"
    watchdog_task = asyncio.create_task(_watch_shutdown_flag(app, flag_path))

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
        watchdog_task.cancel()
        try:
            await watchdog_task
        except asyncio.CancelledError:
            pass
        await app.shutdown()

    return 0


if __name__ == "__main__":
    exit_code = asyncio.run(main())
    if exit_code is not None and exit_code != 0:
        raise SystemExit(exit_code)
