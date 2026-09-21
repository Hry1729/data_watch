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
import json
import os
import signal
import sys
import time

_REPO_ROOT = Path(__file__).resolve().parent.parent
if (_REPO_ROOT / "data_watch_shared").is_dir():
    sys.path.insert(0, str(_REPO_ROOT))

if "--driver-preflight" in sys.argv:
    from driver_preflight import main as _preflight_main
    raise SystemExit(_preflight_main())

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
from device.manager import DeviceManager, is_center_collection_protocol
from device.device import Device, DeviceStatus
from collector.bounded_executor import BoundedExecutor, BoundedThreadPoolExecutor
from collector.scheduler import TaskScheduler
from collector.executor import TaskExecutor
from collector.task import CollectTask
from storage.redis_client import get_redis
from storage.durable_redis_writer import DurableRedisWriter
from monitor.health import HealthChecker
from monitor.ingest_server import InterfaceIngestServer
from monitor.driver_status_server import DriverStatusServer
from monitor.performance import PerformanceMetrics

logger = get_logger("main")

# 停机等待尚未产出采集结果的 PLC 读取池的最长秒数。
SHUTDOWN_DRAIN_TIMEOUT = 10.0


def _resolve_runtime_dir(arg_value: str | None) -> Path:
    if arg_value:
        return Path(arg_value)
    home = os.environ.get("DATAWATCH_HOME")
    if home:
        return Path(home) / "runtime"
    return Path(r"C:\ProgramData\DataWatch\runtime")


def _clear_stale_shutdown_flag(flag_path: Path) -> None:
    """启动时清掉上一轮残留的 shutdown_flag（异常退出或未被消费的）。

    否则本进程启动后 watchdog 立刻读到旧 flag → 刚启动就被停。flag 是一次性
    停机信号，每次启动都从干净状态开始（C10）。
    """
    try:
        flag_path.unlink(missing_ok=True)
    except OSError as exc:
        logger.warning("无法清理残留 shutdown_flag %s: %s", flag_path, exc)


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

    def __init__(
        self,
        config: CollectorConfig,
        config_db_manager=None,
        port_mapping_file=None,
        writer_factory=None,
    ):
        self.config = config
        self.config_db_manager = config_db_manager
        self._shutdown = asyncio.Event()
        self._health_task = None
        self._reload_task = None
        self._performance_task = None
        self._closed = False

        # 初始化组件
        self.performance_metrics = PerformanceMetrics()
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
        # 专用 PLC 读线程池（§7.3）：snap7/GE 阻塞读不再与 Redis 写抢默认池。
        # 容量按设备数派生（单设备单 in-flight，正常不该拒绝），后续按 §8 压测再调。
        read_devices = len(config.devices)
        self.plc_read_executor = BoundedThreadPoolExecutor(
            name="plc_read",
            max_workers=min(64, max(16, read_devices)),
            max_pending=read_devices + 64,
            metrics=self.performance_metrics,
        )
        self.device_manager = DeviceManager(
            config,
            status_reporter=status_reporter,
            port_mapping_file=port_mapping_file,
            metrics=self.performance_metrics,
            read_executor=self.plc_read_executor,
        )
        self.scheduler = TaskScheduler(metrics=self.performance_metrics)
        if writer_factory is None:
            redis_client = get_redis(config)
            self.redis_writer = DurableRedisWriter(
                redis_client,
                config,
                outbox_path=Path(os.environ.get("DATAWATCH_DATA_ROOT") or os.environ.get("DATAWATCH_HOME") or r"C:\ProgramData\DataWatch") / "runtime" / "collector-retry.db",
                metrics=self.performance_metrics,
            )
        else:
            self.redis_writer = writer_factory(config, self.performance_metrics)
        parse_workers = max(2, min(32, os.cpu_count() or 4))
        self.parse_executor = BoundedExecutor(
            name="parse",
            max_workers=parse_workers,
            max_pending=parse_workers * 2,
            metrics=self.performance_metrics,
        )
        self.redis_executor = BoundedExecutor(
            name="redis",
            max_workers=16,
            max_pending=64,
            metrics=self.performance_metrics,
        )
        self.executor = TaskExecutor(
            device_manager=self.device_manager,
            system_logger=self.system_logger,
            redis_writer=self.redis_writer,
            metrics=self.performance_metrics,
            parse_executor=self.parse_executor,
            redis_executor=self.redis_executor,
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
        self.driver_status_server = None
    async def start(self) -> None:
        """启动采集服务"""
        self.performance_metrics.enable_gc_pause_tracking()
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

        # 0. 检查当前安装包固定的数据写出路径
        try:
            self.redis_writer.ping()
            logger.info("Storage sink ready: %s", self.redis_writer.storage_path)
        except Exception as exc:
            logger.warning("Storage sink check failed: %s", exc)

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
                device_manager=self.device_manager,
            )

        # 5b. 驱动加载状态探针（供 Agent 确认真实 loaded）
        # P0 修复：提前获取注册表快照，避免每次回调都触发 _resolve_driver_registry()
        registry_snapshot = self.device_manager._resolve_driver_registry()

        def _driver_snapshot():
            return {
                "generation": int(getattr(registry_snapshot, "generation", 0) or 0),
                "drivers": registry_snapshot.list_status() if registry_snapshot is not None else [],
            }

        self.driver_status_server = DriverStatusServer(
            get_snapshot=_driver_snapshot,
        )
        try:
            self.driver_status_server.start()
        except OSError as exc:
            logger.warning("Driver status server failed to bind: %s", exc)
            self.driver_status_server = None

        # 6. 启动健康检查
        self._health_task = asyncio.create_task(self.health_checker.run())

        # 7. 启动配置热更新轮询（仅在 DB 模式下）
        if self.config_db_manager is not None:
            self._reload_task = asyncio.create_task(self._config_reload_loop())

        self._performance_task = asyncio.create_task(self._performance_log_loop())

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
                    "storage_path": self.redis_writer.storage_path,
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

        if self._performance_task is not None:
            self._performance_task.cancel()
            try:
                await self._performance_task
            except asyncio.CancelledError:
                pass
            self._performance_task = None

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
        if self.driver_status_server is not None:
            self.driver_status_server.stop()
            self.driver_status_server = None
        # Parse/Redis jobs may already contain successfully read samples. Drain
        # them fully before closing devices or Redis instead of cancelling data.
        await self.executor.shutdown()
        await self.device_manager.disconnect_all()
        # 读池在驱动 disconnect 之后再关：disconnect 自身也走该池。
        try:
            await asyncio.wait_for(
                asyncio.to_thread(self.plc_read_executor.shutdown, wait=True),
                timeout=SHUTDOWN_DRAIN_TIMEOUT,
            )
        except asyncio.TimeoutError:
            unfinished_jobs = self.plc_read_executor.pending
            self.plc_read_executor.shutdown(wait=False, cancel_futures=True)
        else:
            unfinished_jobs = 0
        if unfinished_jobs:
            logger.warning(
                "Shutdown drain timed out with %d unfinished job(s)", unfinished_jobs
            )
        self.redis_writer.close()

        self._log_status()
        logger.info(
            "Performance snapshot: %s",
            json.dumps(self.performance_metrics.snapshot(), separators=(",", ":")),
        )
        self.performance_metrics.close()
        logger.info("Collector stopped")
        if self.system_logger is not None:
            self.system_logger.emit(
                level="INFO",
                log_type="system",
                source="collector.main",
                content="Collector stopped",
                detail_json={
                    "executor": self.executor.stats,
                    "storage_path": self.redis_writer.storage_path,
                },
            )
            await self.system_logger.stop()
        if self.config_db_manager is not None:
            await self.config_db_manager.close()
            self.config_db_manager = None

    async def _performance_log_loop(self, interval_seconds: float = 60.0) -> None:
        last_stream_points = 0
        while not self._shutdown.is_set():
            try:
                await asyncio.wait_for(
                    self._shutdown.wait(),
                    timeout=interval_seconds,
                )
            except asyncio.TimeoutError:
                snap = self.performance_metrics.snapshot()
                logger.info(
                    "Performance snapshot: %s",
                    json.dumps(snap, separators=(",", ":")),
                )
                # 上报本分钟 Redis 有效点数增量到 redis metrics counter（60s 一次，零性能影响）。
                # stream_points_written 是本地累计（write_stream 时 increment），口径=已过滤的有效点数。
                current = int(snap.get("counters", {}).get("stream_points_written", 0))
                delta = current - last_stream_points
                if delta > 0:
                    try:
                        self.redis_writer.publish_stream_points(delta)
                        last_stream_points = current  # 只在上报成功后推进；失败保留，下分钟补 delta
                    except Exception:
                        logger.debug("Failed to publish stream_points metrics", exc_info=True)

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
        tracked = await self.config_db_manager.get_device_config_versions()
        while not self._shutdown.is_set():
            try:
                await asyncio.sleep(10)
                current = await self.config_db_manager.get_device_config_versions()
                current_ids = set(current.keys())
                tracked_ids = set(tracked.keys())
                runtime_ids = set(self.device_manager.devices.keys())
                # 启动连接设备期间配置源可能已更新。除签名差异外，同时对齐
                # 真实运行集合，避免热加载基线已是新值、内存仍是旧值时漏删设备。
                added = (current_ids - tracked_ids) | (current_ids - runtime_ids)
                removed = (tracked_ids - current_ids) | (runtime_ids - current_ids)
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
                        tracked[device_id] = current[device_id]
                    except Exception as exc:
                        logger.error("Failed to add device %s: %s", device_id, exc)

                # 版本变更
                for device_id in changed:
                    dev_cfg = dev_by_id.get(device_id)
                    if dev_cfg is None:
                        continue
                    try:
                        old_sig = tracked[device_id]
                        new_sig = current[device_id]
                        # 任意协议：先在候选上完成校验；失败保留旧代际，禁止先删后建。
                        config_error = self.device_manager.validate_device_config(
                            dev_cfg, vars_by_dev.get(device_id, [])
                        )
                        # 仅真实错误字符串才拒绝（避免测试桩 MagicMock 被当成失败）
                        if isinstance(config_error, str) and config_error.strip():
                            old_device = self.device_manager.get_device(device_id)
                            if old_device is not None:
                                old_device.config_error = config_error
                            logger.error(
                                "Rejected invalid reload device=%s; keeping previous generation: %s",
                                device_id,
                                config_error,
                            )
                            continue
                        if old_sig.device_fields_changed(new_sig):
                            if is_center_collection_protocol(dev_cfg.protocol):
                                logger.warning(
                                    "OPC DA device %s config changed; Agent must have reconciled proxy and port mapping before collector rebuild",
                                    device_id,
                                )
                            await self._replace_device_atomic(
                                device_id,
                                dev_cfg,
                                vars_by_dev.get(device_id, []),
                            )
                        else:
                            await self._apply_device_variables(
                                device_id, dev_cfg, vars_by_dev.get(device_id, [])
                            )
                        old_device = self.device_manager.get_device(device_id)
                        if old_device is not None:
                            old_device.config_error = None
                        tracked[device_id] = new_sig
                    except Exception as exc:
                        old_device = self.device_manager.get_device(device_id)
                        if old_device is not None:
                            old_device.config_error = str(exc)
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

    async def _replace_device_atomic(self, device_id: str, dev_cfg, var_cfgs) -> None:
        """字段变更：候选不入表，就绪后原子交换；失败保留旧代际。"""
        from collector.task import CollectTask

        old = self.device_manager.get_device(device_id)
        old_tasks = []
        if isinstance(old, Device):
            old_tasks = [
                CollectTask(device=old, scan_task=st) for st in old.scan_tasks
            ]

        candidate = self.device_manager.prepare_candidate_device(dev_cfg, var_cfgs)
        if candidate.status == DeviceStatus.CONFIG_ERROR:
            if isinstance(old, Device):
                old.config_error = candidate.config_error
            logger.error(
                "Device %s candidate config error; kept previous generation: %s",
                device_id,
                candidate.config_error,
            )
            raise RuntimeError(candidate.config_error or "candidate CONFIG_ERROR")

        try:
            ok = await self.device_manager.connect_device(candidate)
            if not ok:
                await self.device_manager.discard_candidate(candidate)
                logger.error(
                    "Device %s candidate connect failed; kept previous generation",
                    device_id,
                )
                raise RuntimeError("candidate connect failed")

            new_tasks = [
                CollectTask(device=candidate, scan_task=st)
                for st in candidate.scan_tasks
            ]

            # 原子交换后再装调度；失败则换回旧对象并重装旧任务
            await self.device_manager.swap_device(device_id, candidate)
            try:
                await self.scheduler.reload_device_tasks(device_id, new_tasks)
            except Exception:
                if isinstance(old, Device):
                    await self.device_manager.swap_device(device_id, old)
                    # 先恢复连接，再装调度，避免任务在断连态抢跑
                    try:
                        ok = await self.device_manager.connect_device(old)
                        if not ok:
                            await self.device_manager.schedule_reconnect(old)
                            logger.error(
                                "Device %s reconnect previous returned False; "
                                "scheduled reconnect",
                                device_id,
                            )
                    except Exception as reconnect_exc:
                        logger.error(
                            "Device %s reconnect previous after rollback failed: %s",
                            device_id,
                            reconnect_exc,
                        )
                        try:
                            await self.device_manager.schedule_reconnect(old)
                        except Exception as schedule_exc:
                            logger.error(
                                "Device %s schedule_reconnect after rollback also failed: %s",
                                device_id,
                                schedule_exc,
                            )
                    try:
                        await self.scheduler.reload_device_tasks(device_id, old_tasks)
                    except Exception as restore_exc:
                        logger.error(
                            "Device %s reinstall previous tasks failed: %s",
                            device_id,
                            restore_exc,
                        )
                    logger.error(
                        "Device %s scheduler install failed; restored previous generation",
                        device_id,
                    )
                await self.device_manager.discard_candidate(candidate)
                raise

            logger.info(
                "Device %s atomically replaced: version=%d tasks=%d",
                device_id,
                candidate.config_version,
                len(new_tasks),
            )
        except Exception:
            # 尚未 swap 成功时的清理（connect 失败已 discard）
            if (
                self.device_manager.get_device(device_id) is not candidate
                and candidate.driver is not None
            ):
                await self.device_manager.discard_candidate(candidate)
            raise

    async def _add_device(self, dev_cfg, var_cfgs) -> None:
        """运行期注册并连接一台新设备，注册其采集任务。"""
        device = self.device_manager.add_device(dev_cfg, var_cfgs)
        if device is None:
            return
        new_tasks = [CollectTask(device=device, scan_task=st) for st in device.scan_tasks]
        if new_tasks:
            await self.scheduler.reload_device_tasks(dev_cfg.device_id, new_tasks)
        # 与启动行为一致：新增设备自动连接 PLC（external-push 仅置 online）
        ok = await self.device_manager.connect_device(device)
        if not ok and device.status.value != "config_error":
            await self.device_manager.schedule_reconnect(device)
        logger.info(
            "Runtime added device %s: connected=%s tasks=%d",
            dev_cfg.device_id, ok, len(new_tasks),
        )

    async def _remove_device(self, device_id: str) -> None:
        """运行期下线一台设备。约束2：先取消调度循环，再交由 device_manager 处理。"""
        self.device_manager.invalidate_device(device_id)
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
                {var_id: 0 for var_id in values},
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


async def main(writer_factory=None, config_manager_factory=None):
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
    if config_manager_factory is not None:
        db_manager = config_manager_factory(config)
        try:
            config = await db_manager.load_config(base_config=config)
            runtime_db_manager = db_manager
            logger.info(
                "Loaded topology from injected config source: devices=%s, variables=%s",
                len(config.devices),
                len(config.variables),
            )
        except Exception as e:
            await db_manager.close()
            raise ConfigError(f"Failed to load injected topology: {e}") from e
    else:
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
        writer_factory=writer_factory,
    )
    runtime_dir = _resolve_runtime_dir(args.runtime_dir)
    flag_path = runtime_dir / "collector.shutdown_flag"
    _clear_stale_shutdown_flag(flag_path)
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
