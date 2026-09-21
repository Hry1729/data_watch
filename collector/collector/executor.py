"""
任务执行器模块

本模块提供采集任务的执行功能：
- TaskExecutor: 执行具体的采集操作
- 调用驱动读取 PLC 数据
- 解析结果并写入 Redis

典型用法:
    executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)
    await executor.execute(task)
"""

import asyncio
import time
import traceback
from datetime import datetime
from typing import TYPE_CHECKING, Any, Dict, List, Optional

from core.exceptions import ReadError
from core.logger import get_logger
from core.quality_codes import QUALITY_BAD, QUALITY_CONFIG_DECODE_ERROR, QUALITY_GOOD, is_valid_quality
from collector.bounded_executor import BoundedExecutor, ExecutorSaturatedError
from collector.task import CollectTask
from data_queue.types import DataPoint
from device.manager import DeviceManager
from drivers.base import DriverSample
from monitor.performance import PerformanceMetrics

if TYPE_CHECKING:
    from storage.redis_writer import RedisWriter

logger = get_logger("executor")

# 相同日志抑制间隔（秒）
LOG_SUPPRESS_INTERVAL = 30


def _normalize_driver_sample(
    raw: Any,
    default_timestamp: datetime,
    native_quality: bool = False,
) -> tuple[Any, int, datetime, int | None]:
    """将驱动返回的单点标量归一化为 (value, quality, timestamp, protocol_quality)。

    Args:
        raw: 单个标量值，可能是裸值或 DriverSample。
        default_timestamp: 协议未提供 source_timestamp 时的回落值。

    Returns:
        (value, quality, timestamp, protocol_quality) 四元组。
        value 为 None 时调用方应跳过不构造 DataPoint，因为 writer v2 契约
        无法表达"只有质量没有数值"的点。

    Notes:
        - 裸值走快路径，不构造 DriverSample，避免给普通驱动加分配开销。
        - 非法平台质量码会被转为 CONFIG_DECODE_ERROR 并记录 warning。
    """
    if isinstance(raw, DriverSample):
        quality = raw.quality
        if not is_valid_quality(quality):
            logger.warning(
                "Driver returned invalid quality code %r, converting to CONFIG_DECODE_ERROR",
                quality,
            )
            quality = QUALITY_CONFIG_DECODE_ERROR
        timestamp = raw.source_timestamp if raw.source_timestamp is not None else default_timestamp
        return (raw.value, quality, timestamp, raw.protocol_quality)
    if native_quality:
        logger.warning(
            "Driver declaring native_quality returned a raw value; "
            "converting quality to CONFIG_DECODE_ERROR"
        )
        return (raw, QUALITY_CONFIG_DECODE_ERROR, default_timestamp, None)
    return (raw, QUALITY_GOOD, default_timestamp, None)


class TaskExecutor:
    """
    任务执行器
    执行具体的采集操作：调用驱动读取，解析后写入 Redis
    """

    def __init__(
        self,
        device_manager: Optional[DeviceManager] = None,
        system_logger: Optional[Any] = None,
        summary_interval_seconds: float = 60.0,
        redis_writer: Optional["RedisWriter"] = None,
        metrics: Optional[PerformanceMetrics] = None,
        parse_executor: Optional[BoundedExecutor] = None,
        redis_executor: Optional[BoundedExecutor] = None,
    ):
        # collector 主链路只写 Redis；缺少 redis_writer 会静默丢数据，故快速失败。
        if redis_writer is None:
            raise ValueError("TaskExecutor requires redis_writer")
        self.device_manager = device_manager
        self._system_logger = system_logger
        self._redis_writer = redis_writer
        self._metrics = metrics or PerformanceMetrics()
        self._parse_executor = parse_executor
        self._redis_executor = redis_executor
        self._metrics.increment("stale_generation_drop_count", 0)
        self._clock = time.perf_counter
        self._metrics.increment("coalesced_read_group_count", 0)
        self._metrics.increment("coalesced_read_task_count", 0)
        self._metrics.increment("coalesced_read_saved_block_count", 0)
        self._summary_interval_seconds = summary_interval_seconds
        self._summary_started_at = time.time()
        self._summary_tasks = 0
        self._summary_points = 0
        self._summary_errors = 0
        self._summary_elapsed_ms = 0.0
        self._total_reads = 0
        self._total_errors = 0
        # 日志抑制：记录每个设备上次 "not connected" 警告的时间
        self._device_warn_last_time: Dict[str, float] = {}
        self._executor_warn_last_time: Dict[str, float] = {}

    async def _read_blocks(self, device, blocks) -> Dict[str, Any]:
        """Apply a logical deadline without releasing the device before I/O exits."""
        timeout_ms = device.connection_config.get("read_timeout_ms")
        if timeout_ms is None:
            return await device.driver.read(blocks)

        timeout_ms = int(timeout_ms)
        read_task = asyncio.create_task(device.driver.read(blocks))
        try:
            done, _ = await asyncio.wait({read_task}, timeout=timeout_ms / 1000.0)
        except asyncio.CancelledError:
            try:
                await asyncio.shield(read_task)
            except Exception:
                pass
            raise
        if read_task in done:
            return read_task.result()

        self._metrics.increment("read_timeout_count")
        logger.warning(
            "Device %s read exceeded configured timeout %sms; draining underlying I/O",
            device.device_id,
            timeout_ms,
        )
        try:
            await asyncio.shield(read_task)
        except Exception:
            pass
        raise ReadError(
            f"Read exceeded configured timeout {timeout_ms}ms "
            f"for device {device.device_id}"
        )

    async def _read_device(self, task: CollectTask) -> Dict[str, Any]:
        return await self._read_blocks(task.device, task.scan_task.read_blocks)

    async def execute(
        self,
        task: CollectTask,
        *,
        _prefetched_result: Optional[Dict[str, Any]] = None,
        _prefetched_elapsed: Optional[float] = None,
    ) -> None:
        """
        执行单个采集任务
        1. 调用驱动批量读取
        2. 解析结果
        3. 写入 Redis
        """
        device = task.device
        scan_task = task.scan_task

        if not self._task_is_current(task):
            self._record_stale_generation_drop()
            return

        if not device.driver or not device.driver.is_connected:
            # 日志抑制：同一设备在 LOG_SUPPRESS_INTERVAL 秒内只输出一次警告
            now = time.time()
            last_warn = self._device_warn_last_time.get(device.device_id, 0)
            if now - last_warn >= LOG_SUPPRESS_INTERVAL:
                logger.warning(
                    "Device %s not connected, skipping task %s",
                    device.device_id,
                    task.task_name,
                )
                self._device_warn_last_time[device.device_id] = now
            if self.device_manager:
                await self.device_manager.schedule_reconnect(device)
            return

        # 批量读取（读失败：标记设备异常并向调度器抛出）
        start = self._clock()
        try:
            result = (
                _prefetched_result
                if _prefetched_result is not None
                else await self._read_device(task)
            )
            if scan_task.read_blocks and all(result.get(block.address) is None for block in scan_task.read_blocks):
                raise ReadError(f"All read blocks failed for task {task.task_name}")
        except ExecutorSaturatedError as exc:
            # 读池满是背压、不是设备故障：计 reject 跳过本周期，不标设备错误、
            # 不触发重连（§7.9-5 避免重连风暴），也不向调度器抛出。read_ms 不记录，
            # 以免近零的拒绝样本污染真实读耗时分位；reject 数由 plc_read_executor_reject_count 反映。
            self._total_errors += 1
            self._warn_executor_saturated("plc_read", task, exc)
            self._record_read_summary(task, 0, (self._clock() - start) * 1000, error=True)
            return
        except Exception as e:
            if not self._task_is_current(task):
                self._metrics.record_timing("read_ms", (self._clock() - start) * 1000)
                self._record_stale_generation_drop()
                return
            self._total_errors += 1
            logger.error("Task %s read failed: %s", task.task_name, e)
            elapsed_ms = (self._clock() - start) * 1000
            self._metrics.record_timing("read_ms", elapsed_ms)
            self._metrics.increment("read_error_count")
            self._emit_read_failure(task, e, elapsed_ms)
            self._record_read_summary(task, 0, elapsed_ms, error=True)
            if self.device_manager:
                await self.device_manager.mark_device_error(device, f"read failed: {e}", "timeout")
            raise

        elapsed = (
            _prefetched_elapsed
            if _prefetched_elapsed is not None
            else self._clock() - start
        )
        self._metrics.record_timing("read_ms", elapsed * 1000)
        self._metrics.increment("read_success_count")
        self._total_reads += 1

        if not self._task_is_current(task):
            self._record_stale_generation_drop()
            return

        # 解析结果
        parse_start = self._clock()
        native_quality = bool(getattr(device.driver, "native_quality", False))
        try:
            if self._parse_executor is None:
                points = self._parse_result(
                    device.device_id, scan_task, result, native_quality
                )
            else:
                points = await self._parse_executor.run(
                    self._parse_result,
                    device.device_id,
                    scan_task,
                    result,
                    native_quality,
                )
        finally:
            self._metrics.record_timing(
                "parse_ms", (self._clock() - parse_start) * 1000
            )
        self._metrics.increment("points_parsed", len(points))

        # 写入 Redis（写失败：读已成功，设备保持 ONLINE，但如实计为错误，不谎报成功）
        # 注意：await 会让本任务等待 Redis 写完，写入耗时会计入该任务的采集周期；
        # 写操作仍在线程池执行，不阻塞事件循环线程。
        if points:
            redis_start = self._clock()
            try:
                if self._redis_executor is None:
                    loop = asyncio.get_running_loop()
                    written = await loop.run_in_executor(
                        None, self._write_if_current, task, points
                    )
                else:
                    written = await self._redis_executor.run(
                        self._write_if_current, task, points
                    )
                if not written:
                    self._record_stale_generation_drop()
                    return
            except Exception as exc:
                self._total_errors += 1
                self._metrics.increment("redis_write_error_count")
                logger.error(
                    "Redis write failed after successful read for task %s device %s: %s",
                    task.task_name,
                    device.device_id,
                    exc,
                )
                self._record_read_summary(task, len(points), elapsed * 1000, error=True)
                return
            finally:
                self._metrics.record_timing(
                    "redis_ms", (self._clock() - redis_start) * 1000
                )

        logger.debug(
            "Task %s: read %s points in %.1fms",
            task.task_name,
            len(points),
            elapsed * 1000,
        )
        self._record_read_summary(task, len(points), elapsed * 1000)


    async def execute_many(self, tasks: List[CollectTask]) -> None:
        """Read coincident tasks for one device once, then preserve per-task outputs."""
        active_tasks = []
        for task in tasks:
            if self._task_is_current(task):
                active_tasks.append(task)
            else:
                self._record_stale_generation_drop()
        if not active_tasks:
            return
        if len(active_tasks) == 1:
            await self.execute(active_tasks[0])
            return

        device = active_tasks[0].device
        if any(task.device.device_id != device.device_id for task in active_tasks):
            raise ValueError("execute_many requires tasks from one device")
        if not device.driver or not device.driver.is_connected:
            outcomes = await asyncio.gather(
                *(self.execute(task) for task in active_tasks),
                return_exceptions=True,
            )
            for task, outcome in zip(active_tasks, outcomes):
                if isinstance(outcome, BaseException):
                    task.error_count += 1
                    logger.error(
                        "Task %s failed (errors=%s): %s",
                        task.task_name,
                        task.error_count,
                        outcome,
                    )
                else:
                    task.error_count = 0
            return

        blocks_by_address = {}
        requested_blocks = 0
        for task in active_tasks:
            for block in task.scan_task.read_blocks:
                requested_blocks += 1
                existing = blocks_by_address.get(block.address)
                if existing is None or block.count > existing.count:
                    blocks_by_address[block.address] = block
        merged_blocks = list(blocks_by_address.values())

        started = self._clock()
        try:
            result = await self._read_blocks(device, merged_blocks)
        except ExecutorSaturatedError as exc:
            self._total_errors += 1
            self._warn_executor_saturated("plc_read", active_tasks[0], exc)
            self._metrics.increment("coalesced_read_reject_count")
            return
        except Exception as exc:
            elapsed_ms = (self._clock() - started) * 1000
            self._metrics.record_timing("read_ms", elapsed_ms)
            self._total_errors += 1
            self._metrics.increment("read_error_count")
            self._metrics.increment("coalesced_read_error_count")
            for task in active_tasks:
                task.error_count += 1
            self._emit_read_failure(active_tasks[0], exc, elapsed_ms)
            if self.device_manager:
                await self.device_manager.mark_device_error(
                    device, f"read failed: {exc}", "timeout"
                )
            raise

        elapsed = self._clock() - started
        self._metrics.increment("coalesced_read_group_count")
        self._metrics.increment("coalesced_read_task_count", len(active_tasks))
        self._metrics.increment(
            "coalesced_read_saved_block_count",
            max(0, requested_blocks - len(merged_blocks)),
        )
        outcomes = await asyncio.gather(
            *(
                self.execute(
                    task,
                    _prefetched_result=result,
                    _prefetched_elapsed=elapsed,
                )
                for task in active_tasks
            ),
            return_exceptions=True,
        )

        for task, outcome in zip(active_tasks, outcomes):
            if isinstance(outcome, BaseException):
                task.error_count += 1
                logger.error(
                    "Task %s failed after coalesced read (errors=%s): %s",
                    task.task_name,
                    task.error_count,
                    outcome,
                )
            else:
                task.error_count = 0
    async def shutdown(self, timeout: Optional[float] = None) -> int:
        """关闭 parse/redis 有界池，返回两池未完成 job 数之和（§7.9-5）。"""
        executors = []
        for executor in (self._parse_executor, self._redis_executor):
            if executor is not None and executor not in executors:
                executors.append(executor)
        if not executors:
            return 0
        counts = await asyncio.gather(
            *(executor.shutdown(timeout) for executor in executors)
        )
        return sum(counts)

    def _warn_executor_saturated(
        self,
        stage: str,
        task: CollectTask,
        error: ExecutorSaturatedError,
    ) -> None:
        now = time.time()
        if now - self._executor_warn_last_time.get(stage, 0) < LOG_SUPPRESS_INTERVAL:
            return
        self._executor_warn_last_time[stage] = now
        logger.warning(
            "%s executor saturated; dropping cycle task=%s: %s",
            stage,
            task.task_name,
            error,
        )

    def _task_is_current(self, task: CollectTask) -> bool:
        if self.device_manager is None:
            return True
        return self.device_manager.is_current_generation(
            task.device.device_id, task.generation
        )

    def _record_stale_generation_drop(self) -> None:
        self._metrics.increment("stale_generation_drop_count")

    def _write_if_current(self, task: CollectTask, points: List[DataPoint]) -> bool:
        if self.device_manager is None:
            self._write_to_redis(task.device, task.scan_task, points)
            return True
        return self.device_manager.run_if_current_generation(
            task.device.device_id,
            task.generation,
            lambda: self._write_to_redis(task.device, task.scan_task, points),
        )

    @staticmethod
    def _format_point_value(value: Any) -> str:
        """点值转字符串供 Redis/写库使用。

        bool 必须先于数值判断（Python 中 bool 是 int 子类）：str(True)="True"
        会让 writer 的 float() 解析失败、兜底成 0（bool 点全部入库为 0）；统一转成
        "1"/"0"，与其它数值串一致、可被 float() 正常解析。
        """
        if value is None:
            return ""
        if isinstance(value, bool):
            return "1" if value else "0"
        return str(value)

    def _write_to_redis(self, device, scan_task, points: List[DataPoint]) -> None:
        """将采集结果写入 Redis（latest / window / stream）。在线程池中调用，不阻塞事件循环。"""
        try:
            # var_id -> last point（多值时取最后一个）
            var_points: Dict[str, DataPoint] = {}
            for p in points:
                var_points[p.variable_id] = p

            scan_rate_ms = int(scan_task.scan_rate * 1000)
            device_code = device.device_id
            device_type = device.device_type.lower()

            # variable_id → Variable 元数据
            var_meta: Dict[str, Any] = {v.variable_id: v for v in scan_task.variables}

            # 按 group_code 分组
            by_group: Dict[str, Dict[str, tuple]] = {}
            group_versions: Dict[str, int] = {}
            for var_id, point in var_points.items():
                var = var_meta.get(var_id)
                if var is None:
                    continue
                gc = var.group_code
                if gc not in by_group:
                    by_group[gc] = {}
                group_versions[gc] = getattr(var, "group_config_version", 1)
                by_group[gc][var_id] = (point, var.data_type, var.is_storage)

            for group_code, var_data in by_group.items():
                # latest Hash：所有点位
                fields = {
                    var_id: {
                        "v": self._format_point_value(point.value),
                        "t": self._timestamp_to_millis(point.timestamp),
                        "q": int(getattr(point, "quality", 0)),
                    }
                    for var_id, (point, _, _) in var_data.items()
                }
                ts_ms = max(field["t"] for field in fields.values())

                # 写顺序：window+stream（合并一个 pipeline）-> latest。latest 必须最后写，
                # 否则 window/stream 失败时 latest 已被刷新，客户端据此误判批次已落库。
                # window frame：所有点位
                frame = {
                    "device_code": device_code,
                    "group_code": group_code,
                    "ts": ts_ms,
                    "scan_rate": scan_rate_ms,
                    "values": {
                        var_id: self._format_point_value(point.value)
                        for var_id, (point, _, _) in var_data.items()
                    },
                }
                # stream：过滤 STRING 类型
                stream_values = {
                    var_id: self._format_point_value(point.value)
                    for var_id, (point, dt, is_storage) in var_data.items()
                    if is_storage and dt != "STRING" and point.value is not None
                }
                # stream_qualities：键集合与 stream_values 一致
                stream_qualities = {
                    var_id: int(getattr(point, "quality", 0))
                    for var_id, (point, dt, is_storage) in var_data.items()
                    if is_storage and dt != "STRING" and point.value is not None
                }
                # window+stream 合并一次往返（§7.5）；任一失败抛出 → 跳过 latest。
                self._redis_writer.write_window_stream(
                    device_code, device_type, group_code, scan_rate_ms,
                    ts_ms, group_versions[group_code], frame, stream_values,
                    stream_qualities,
                )

                self._redis_writer.write_latest(device_code, group_code, fields)

        except Exception as exc:
            # 不在此吞掉异常：交由 execute() 的写入分支统一计错并记录 summary
            logger.error("Redis write failed for device %s: %s", device.device_id, exc)
            raise

    @staticmethod
    def _timestamp_to_millis(timestamp: Any) -> int:
        if isinstance(timestamp, datetime):
            return int(timestamp.timestamp() * 1000)
        if isinstance(timestamp, (int, float)):
            ts = float(timestamp)
            return int(ts if ts > 1_000_000_000_000 else ts * 1000)
        return int(time.time() * 1000)

    def _parse_result(
        self,
        device_id: str,
        scan_task,
        result: dict,
        native_quality: bool = False,
    ) -> List[DataPoint]:
        """解析读取结果为DataPoint列表"""
        points = []
        now = datetime.now()
        result_map = getattr(scan_task, "result_map", {}) or {}
        external_decoded: Dict[str, Any] = {}
        # 优先使用 ScanTask 上挂载的 decode（外置驱动规划时写入）
        decode_hook = getattr(scan_task, "decode_read_results", None)
        device_manager = getattr(self, "device_manager", None)
        if result_map:
            # ScanTask 未挂 decoder 时，可经 registry + binding.protocol 回退查找
            if decode_hook is None and device_manager is not None:
                sample = next(iter(result_map.values()))
                external_protocol = sample.get("protocol")
                resolve = getattr(device_manager, "_resolve_driver_registry", None)
                registry = resolve() if callable(resolve) else None
                if registry is not None and external_protocol:
                    registration = registry.get(str(external_protocol))
                    if registration is not None and registration.decode_read_results:
                        decode_hook = registration.decode_read_results
            # 只要有 decoder + result_map：整表交给 decoder，不依赖 protocol 筛选
            if decode_hook is not None:
                external_decoded = decode_hook(result, result_map)

        for variable in scan_task.variables:
            mapped = result_map.get(variable.variable_id)
            if mapped:
                if decode_hook is not None:
                    if variable.variable_id not in external_decoded:
                        continue
                    raw = external_decoded[variable.variable_id]
                    from drivers.base import MISSING_RESULT
                    if raw is MISSING_RESULT:
                        points.append(DataPoint(device_id=device_id, variable_id=variable.variable_id, value=None, timestamp=now, quality=QUALITY_BAD))
                        continue
                    value, quality, timestamp, protocol_quality = _normalize_driver_sample(
                        raw, now, native_quality
                    )
                    if value is None:
                        continue
                    points.append(DataPoint(
                        device_id=device_id,
                        variable_id=variable.variable_id,
                        value=value,
                        timestamp=timestamp,
                        quality=quality,
                    ))
                    continue

                raise ValueError("Mapped read plan requires a package decoder")

            block_addr = variable.address
            raw_values = result.get(block_addr)

            if raw_values is None:
                points.append(DataPoint(
                    device_id=device_id,
                    variable_id=variable.variable_id,
                    value=None,
                    timestamp=now,
                    quality=QUALITY_BAD,
                ))
                continue

            # 单值或多值
            values = raw_values if isinstance(raw_values, list) else [raw_values]

            for val in values:
                value, quality, timestamp, protocol_quality = _normalize_driver_sample(
                    val, now, native_quality
                )
                if value is None:
                    continue
                point = DataPoint(
                    device_id=device_id,
                    variable_id=variable.variable_id,
                    value=value,
                    timestamp=timestamp,
                    quality=quality,
                )
                points.append(point)

        return points

    def _emit_read_failure(self, task: CollectTask, error: Exception, elapsed_ms: float) -> None:
        """将读取失败记录为系统日志，不阻塞采集。"""
        if self._system_logger is None:
            return

        device = task.device
        self._system_logger.emit(
            level="ERROR",
            log_type="collect",
            source="collector.executor",
            device_id=getattr(device, "device_pk", None),
            device_code=device.device_id,
            content=f"Collect task {task.task_name} read failed",
            detail_json={
                "task_name": task.task_name,
                "scan_rate": getattr(task.scan_task, "scan_rate", None),
                "read_blocks": [
                    getattr(block, "address", str(block))
                    for block in getattr(task.scan_task, "read_blocks", [])
                ],
                "error": str(error),
            },
            exception_stack="".join(
                traceback.format_exception(type(error), error, error.__traceback__)
            )[:4000],
            elapsed_ms=int(elapsed_ms),
        )

    def _record_read_summary(
        self,
        task: CollectTask,
        points_count: int,
        elapsed_ms: float,
        error: bool = False,
    ) -> None:
        """每个间隔发出一条紧凑的采集摘要，而非每个测点发一条。"""
        if self._system_logger is None:
            return

        self._summary_tasks += 1
        self._summary_points += points_count
        self._summary_elapsed_ms += elapsed_ms
        if error:
            self._summary_errors += 1

        now = time.time()
        if now - self._summary_started_at < self._summary_interval_seconds:
            return

        interval_seconds = max(0.001, now - self._summary_started_at)
        average_elapsed_ms = (
            self._summary_elapsed_ms / self._summary_tasks
            if self._summary_tasks
            else 0
        )
        level = "WARN" if self._summary_errors else "INFO"
        self._system_logger.emit(
            level=level,
            log_type="collect",
            source="collector.executor",
            content="Collector read summary",
            detail_json={
                "interval_seconds": round(interval_seconds, 3),
                "tasks": self._summary_tasks,
                "points": self._summary_points,
                "errors": self._summary_errors,
                "average_elapsed_ms": round(average_elapsed_ms, 3),
                "last_task": task.task_name,
                "last_device_code": task.device.device_id,
                "total_reads": self._total_reads,
                "total_errors": self._total_errors,
            },
        )

        self._summary_started_at = now
        self._summary_tasks = 0
        self._summary_points = 0
        self._summary_errors = 0
        self._summary_elapsed_ms = 0.0

    @property
    def stats(self):
        return {
            "total_reads": self._total_reads,
            "total_errors": self._total_errors,
            "performance": self._metrics.snapshot(),
        }
