"""
任务执行器模块

本模块提供采集任务的执行功能：
- TaskExecutor: 执行具体的采集操作
- 调用驱动读取 PLC 数据
- 解析结果并生成 DataPoint 入队

典型用法:
    executor = TaskExecutor(buffer, device_manager)
    await executor.execute(task)
"""

import time
import traceback
from datetime import datetime
from typing import Any, Dict, List, Optional

from core.exceptions import ReadError
from core.logger import get_logger
from collector.task import CollectTask
from data_queue.buffer import DataBuffer, DataPoint
from device.manager import DeviceManager

logger = get_logger("executor")

# 相同日志抑制间隔（秒）
LOG_SUPPRESS_INTERVAL = 30


class TaskExecutor:
    """
    任务执行器
    执行具体的采集操作：调用驱动读取，将结果放入队列
    """

    def __init__(
        self,
        buffer: DataBuffer,
        device_manager: Optional[DeviceManager] = None,
        system_logger: Optional[Any] = None,
        summary_interval_seconds: float = 60.0,
    ):
        self.buffer = buffer
        self.device_manager = device_manager
        self._system_logger = system_logger
        self._summary_interval_seconds = summary_interval_seconds
        self._summary_started_at = time.time()
        self._summary_tasks = 0
        self._summary_points = 0
        self._summary_errors = 0
        self._summary_elapsed_ms = 0.0
        self._total_reads = 0
        self._total_errors = 0
        self._total_dropped = 0  # 队列满时丢弃的点数
        # 日志抑制：记录每个设备上次 "not connected" 警告的时间
        self._device_warn_last_time: Dict[str, float] = {}

    async def execute(self, task: CollectTask) -> None:
        """
        执行单个采集任务
        1. 调用驱动批量读取
        2. 解析结果
        3. 生成DataPoint入队
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

        # 批量读取
        start = time.time()
        try:
            result = await device.driver.read(scan_task.read_blocks)
            if scan_task.read_blocks and all(result.get(block.address) is None for block in scan_task.read_blocks):
                raise ReadError(f"All read blocks failed for task {task.task_name}")
            elapsed = time.time() - start
            self._total_reads += 1

            # 解析结果并入队
            points = self._parse_result(device.device_id, scan_task, result)
            if points:
                accepted = await self.buffer.put_batch(points)
                dropped = len(points) - accepted
                if dropped > 0:
                    logger.warning(
                        "Task %s: queue full, dropped %d/%d points",
                        task.task_name,
                        dropped,
                        len(points),
                    )
                    self._total_dropped += dropped

            logger.debug(
                "Task %s: read %s points in %.1fms",
                task.task_name,
                len(points),
                elapsed * 1000,
            )
            self._record_read_summary(task, len(points), elapsed * 1000)

        except Exception as e:
            self._total_errors += 1
            logger.error("Task %s read failed: %s", task.task_name, e)
            elapsed_ms = (time.time() - start) * 1000
            self._emit_read_failure(task, e, elapsed_ms)
            self._record_read_summary(task, 0, elapsed_ms, error=True)
            if self.device_manager:
                await self.device_manager.mark_device_error(device, f"read failed: {e}", "timeout")
            raise

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
            # 仅将需要入库的变量送入队列，减轻聚合与存储压力
            if not variable.is_storage:
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
            "total_dropped": self._total_dropped,
        }
