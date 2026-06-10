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
import struct
from typing import TYPE_CHECKING, Any, Dict, List, Optional

from core.exceptions import ReadError
from core.logger import get_logger
from collector.task import CollectTask
from data_queue.types import DataPoint
from device.manager import DeviceManager

if TYPE_CHECKING:
    from storage.redis_writer import RedisWriter

logger = get_logger("executor")

# 相同日志抑制间隔（秒）
LOG_SUPPRESS_INTERVAL = 30


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
    ):
        # collector 主链路只写 Redis；缺少 redis_writer 会静默丢数据，故快速失败。
        if redis_writer is None:
            raise ValueError("TaskExecutor requires redis_writer")
        self.device_manager = device_manager
        self._system_logger = system_logger
        self._redis_writer = redis_writer
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

    async def execute(self, task: CollectTask) -> None:
        """
        执行单个采集任务
        1. 调用驱动批量读取
        2. 解析结果
        3. 写入 Redis
        """
        device = task.device
        scan_task = task.scan_task

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
        start = time.time()
        try:
            result = await device.driver.read(scan_task.read_blocks)
            if scan_task.read_blocks and all(result.get(block.address) is None for block in scan_task.read_blocks):
                raise ReadError(f"All read blocks failed for task {task.task_name}")
        except Exception as e:
            self._total_errors += 1
            logger.error("Task %s read failed: %s", task.task_name, e)
            elapsed_ms = (time.time() - start) * 1000
            self._emit_read_failure(task, e, elapsed_ms)
            self._record_read_summary(task, 0, elapsed_ms, error=True)
            if self.device_manager:
                await self.device_manager.mark_device_error(device, f"read failed: {e}", "timeout")
            raise

        elapsed = time.time() - start
        self._total_reads += 1

        # 解析结果
        points = self._parse_result(device.device_id, scan_task, result)

        # 写入 Redis（写失败：读已成功，设备保持 ONLINE，但如实计为错误，不谎报成功）
        # 注意：await 会让本任务等待 Redis 写完，写入耗时会计入该任务的采集周期；
        # 写操作仍在线程池执行，不阻塞事件循环线程。
        if points:
            loop = asyncio.get_running_loop()
            try:
                await loop.run_in_executor(
                    None, self._write_to_redis, device, scan_task, points
                )
            except Exception as exc:
                self._total_errors += 1
                logger.error(
                    "Redis write failed after successful read for task %s device %s: %s",
                    task.task_name,
                    device.device_id,
                    exc,
                )
                self._record_read_summary(task, len(points), elapsed * 1000, error=True)
                return

        logger.debug(
            "Task %s: read %s points in %.1fms",
            task.task_name,
            len(points),
            elapsed * 1000,
        )
        self._record_read_summary(task, len(points), elapsed * 1000)

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
                        "v": str(point.value),
                        "t": self._timestamp_to_millis(point.timestamp),
                        "q": int(getattr(point, "quality", 0)),
                    }
                    for var_id, (point, _, _) in var_data.items()
                }
                ts_ms = max(field["t"] for field in fields.values())

                # 写顺序：window -> stream -> latest。latest 必须最后写，
                # 否则 stream/window 失败时 latest 已被刷新，客户端据此误判批次已落库。
                # window List：所有点位
                frame = {
                    "device_code": device_code,
                    "group_code": group_code,
                    "ts": ts_ms,
                    "scan_rate": scan_rate_ms,
                    "values": {
                        var_id: str(point.value)
                        for var_id, (point, _, _) in var_data.items()
                    },
                }
                self._redis_writer.write_window(device_code, group_code, scan_rate_ms, frame)

                # stream：过滤 STRING 类型
                stream_values = {
                    var_id: str(point.value)
                    for var_id, (point, dt, is_storage) in var_data.items()
                    if is_storage and dt != "STRING"
                }
                if stream_values:
                    self._redis_writer.write_stream(
                        device_code, device_type, group_code, scan_rate_ms,
                        ts_ms, group_versions[group_code], stream_values,
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
    ) -> List[DataPoint]:
        """解析读取结果为DataPoint列表"""
        points = []
        now = datetime.now()

        for variable in scan_task.variables:
            result_map = getattr(scan_task, "result_map", {})
            mapped = result_map.get(variable.variable_id)
            if mapped:
                block_addr = mapped["block_address"]
                raw_values = result.get(block_addr)
                if raw_values is None:
                    continue

                # 根据 protocol 选择还原方法
                protocol = mapped.get("protocol", "")
                if protocol == "ge_srtp":
                    value = self._extract_mapped_ge_value(raw_values, mapped)
                else:
                    value = self._extract_mapped_s7_value(raw_values, mapped)

                points.append(DataPoint(
                    device_id=device_id,
                    variable_id=variable.variable_id,
                    value=value,
                    timestamp=now,
                ))
                continue

            block_addr = variable.address
            raw_values = result.get(block_addr)

            if raw_values is None:
                continue

            # 单值或多值
            values = raw_values if isinstance(raw_values, list) else [raw_values]

            for val in values:
                point = DataPoint(
                    device_id=device_id,
                    variable_id=variable.variable_id,
                    value=val,
                    timestamp=now,
                )
                points.append(point)

        return points

    @staticmethod
    def _extract_mapped_s7_value(raw_values: Any, mapping: Dict[str, Any]) -> Any:
        """从合并后的 S7 I/Q/M BYTE 块或 Timer/Counter 块中还原单个变量值。"""
        values = raw_values if isinstance(raw_values, list) else [raw_values]
        if "value_offset" in mapping:
            return values[int(mapping.get("value_offset", 0))]

        raw = bytearray(int(value) & 0xFF for value in values)
        offset = int(mapping.get("byte_offset", 0))
        bit = mapping.get("bit")
        data_type = str(mapping.get("data_type", "BYTE")).upper()

        if bit is not None:
            return bool((raw[offset] >> int(bit)) & 1)
        if data_type == "BYTE":
            return raw[offset]
        if data_type == "INT":
            return struct.unpack(">h", raw[offset:offset + 2])[0]
        if data_type == "WORD":
            return struct.unpack(">H", raw[offset:offset + 2])[0]
        if data_type in {"REAL", "FLOAT"}:
            return struct.unpack(">f", raw[offset:offset + 4])[0]
        if data_type == "DINT":
            return struct.unpack(">i", raw[offset:offset + 4])[0]
        if data_type == "DWORD":
            return struct.unpack(">I", raw[offset:offset + 4])[0]
        return raw[offset]

    @staticmethod
    def _extract_mapped_ge_value(raw_values: Any, mapping: Dict[str, Any]) -> Any:
        """
        从合并后的 GE 块中还原单个变量值。

        寄存器区域 (%R/%AI/%AQ):
            word_offset 定位起始 word
            INT: 有符号 16 位
            WORD/UINT: 无符号 16 位
            DINT: 2 word 组合为有符号 32 位
            DWORD/UDINT: 2 word 组合为无符号 32 位
            REAL/FLOAT: 2 word 组合为 32 位浮点

        离散量区域 (%I/%Q/%M):
            第一版只支持 BOOL
            bit_offset 直接从 bit list 中取值

        REAL/DINT/DWORD 字序：
            当前只支持 word_order="low_high"（低地址 word 在前，高地址 word 在后）
            与 GE PACSystems 文档和 gesrtp-py 实现一致，但仍需现场已知值校验后固化
        """
        values = raw_values if isinstance(raw_values, list) else [raw_values]
        area = str(mapping.get("area", ""))
        data_type = str(mapping.get("data_type", "INT")).upper()

        # 寄存器区域：word 偏移
        if area in ("R", "AI", "AQ"):
            word_offset = int(mapping.get("word_offset", 0))
            word_length = int(mapping.get("word_length", 1))
            # 获取 word_order 配置（当前只支持 low_high）
            word_order = str(mapping.get("word_order", "low_high"))

            # 单 word 类型
            if data_type == "INT":
                # 有符号 16 位：gesrtp-py 已用 <h 返回 signed，只需处理 unsigned 输入
                val = int(values[word_offset])
                if val > 0x7FFF:
                    return val - 0x10000  # unsigned 转 signed
                return val
            if data_type in ("WORD", "UINT"):
                return values[word_offset] & 0xFFFF
            if data_type == "USINT":
                return values[word_offset] & 0xFF

            # 2 word 类型：组合为 32 位值
            if word_length >= 2 and word_offset + 1 < len(values):
                if word_order != "low_high":
                    raise ValueError(f"Unsupported GE word_order: {word_order}. Current only supports low_high.")

                low = values[word_offset] & 0xFFFF
                high = values[word_offset + 1] & 0xFFFF

                if data_type in ("DINT",):
                    # 有符号 32 位（low_high word 序：low + high << 16）
                    val = low | (high << 16)
                    if val >= 0x80000000:
                        val -= 0x100000000
                    return val
                if data_type in ("DWORD", "UDINT"):
                    return low | (high << 16)
                if data_type in ("REAL", "FLOAT"):
                    # IEEE 754 浮点（low_high word 序）
                    raw_bytes = struct.pack('<HH', low, high)
                    return struct.unpack('<f', raw_bytes)[0]

            return values[word_offset]

        # 离散量区域：BOOL bit 偏移
        if area in ("I", "Q", "M"):
            if data_type == "BOOL":
                bit_offset = int(mapping.get("bit_offset", 0))
                return bool(values[bit_offset])
            # 第一版不支持离散区的多字节类型
            logger.warning(
                "GE discrete area %s does not support %s in this version",
                area, data_type
            )
            return None

        return values[0]

    def _emit_read_failure(self, task: CollectTask, error: Exception, elapsed_ms: float) -> None:
        """Record a failed read as a system log without blocking collection."""
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
        """Emit one compact collection summary per interval instead of per point."""
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
        }
