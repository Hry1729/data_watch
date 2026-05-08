"""批量写入器"""

import asyncio
import time
import traceback
from typing import Any, Dict, List, Optional, Set, Tuple

from core.exceptions import StorageError
from core.logger import get_logger
from data_queue.buffer import DataPoint
from storage.tdengine import TDengineConnection, VariableStorageTarget

logger = get_logger("batch_writer")


class BatchWriter:
    """
    批量写入TDengine
    将多个DataPoint合并为一条INSERT语句
    """

    def __init__(
        self,
        tdengine: TDengineConnection,
        max_retries: int = 3,
        retry_delay: float = 1.0,
        fake_mode: bool = False,
        system_logger: Optional[Any] = None,
        summary_interval_seconds: float = 60.0,
    ):
        self.tdengine = tdengine
        self.max_retries = max_retries
        self.retry_delay = retry_delay
        self.fake_mode = fake_mode
        self._system_logger = system_logger
        self._summary_interval_seconds = summary_interval_seconds
        self._summary_started_at = time.time()
        self._summary_batches = 0
        self._summary_points = 0
        self._summary_affected = 0
        self._summary_failed = 0
        self._summary_elapsed_ms = 0.0
        self._total_written = 0
        self._total_failed = 0
        self._running = False
        self._pending_tasks: Set[asyncio.Task] = set()

    async def write(self, points: List[DataPoint]) -> int:
        """
        批量写入数据点到TDengine
        每个变量写入对应子表，行结构：(ts, value)
        Returns:
            成功写入的条数
        """
        if not points:
            return 0

        if self.fake_mode:
            logger.info("[FAKE-DATA] writing fake points count=%s", len(points))

        start = time.time()
        try:
            affected = await self._write_by_subtable(points)
            elapsed_ms = (time.time() - start) * 1000
            failed_count = max(0, len(points) - affected)
            self._record_storage_summary(len(points), affected, failed_count, elapsed_ms)
            return affected
        except Exception as e:
            elapsed_ms = (time.time() - start) * 1000
            logger.error("Batch write failed: %s", e)
            self._emit_storage_failure(points, e, elapsed_ms)
            self._record_storage_summary(len(points), 0, len(points), elapsed_ms, error=True)
            raise StorageError(f"Batch write failed: {e}") from e

    async def _write_by_subtable(self, points: List[DataPoint]) -> int:
        """按变量子表分组写入。"""
        table_groups: Dict[str, Tuple[VariableStorageTarget, List[DataPoint]]] = {}
        failed_count = 0
        failures: List[str] = []

        for point in points:
            try:
                target = self.tdengine.get_storage_target(point.variable_id)
            except StorageError as exc:
                failed_count += 1
                failures.append(str(exc))
                logger.error(
                    "TDengine target lookup failed device=%s variable_id=%s: %s",
                    point.device_id,
                    point.variable_id,
                    exc,
                )
                continue
            group_key = f"{target.database_name}.{target.table_name}"
            if group_key not in table_groups:
                table_groups[group_key] = (target, [])
            table_groups[group_key][1].append(point)

        tasks = []
        task_meta = []
        for group_key, (target, table_points) in table_groups.items():
            task = asyncio.create_task(self._write_subtable_with_retry(target, table_points))
            tasks.append(task)
            task_meta.append((group_key, len(table_points)))

        results = await asyncio.gather(*tasks, return_exceptions=True)

        total_affected = 0
        for (group_key, group_size), result in zip(task_meta, results):
            if isinstance(result, int):
                total_affected += result
            else:
                failed_count += group_size
                failures.append(f"{group_key}: {result}")
                logger.error("Subtable write failed group=%s points=%s error=%s", group_key, group_size, result)

        self._total_written += total_affected
        self._total_failed += failed_count

        if failures:
            raise StorageError(
                f"TDengine batch partially failed failed_points={failed_count} "
                f"success_points={total_affected}; {'; '.join(failures[:5])}"
            )

        return total_affected

    async def _write_subtable_with_retry(
        self,
        target: VariableStorageTarget,
        points: List[DataPoint],
    ) -> int:
        """对单个变量子表进行有限重试，避免一个子表失败拖垮全部分组。"""
        last_error: Exception | None = None
        for attempt in range(1, self.max_retries + 2):
            try:
                return await self._write_subtable_points(target, points)
            except Exception as exc:
                last_error = exc
                if attempt > self.max_retries:
                    break
                delay = self.retry_delay * attempt
                logger.warning(
                    "Subtable write retry table=%s variable_id=%s attempt=%s/%s delay=%.1fs error=%s",
                    target.table_name,
                    target.variable_id,
                    attempt,
                    self.max_retries,
                    delay,
                    exc,
                )
                await asyncio.sleep(delay)
        raise StorageError(
            f"Subtable write exhausted retries table={target.table_name} "
            f"variable_id={target.variable_id}: {last_error}"
        )

    async def _write_subtable_points(
        self,
        target: VariableStorageTarget,
        points: List[DataPoint],
    ) -> int:
        """写入单个变量子表。"""
        rows_by_ts: Dict[str, float] = {}
        duplicate_ts_count = 0
        invalid_value_count = 0
        for point in points:
            try:
                val = float(point.value)
                ts = point.timestamp.strftime("%Y-%m-%d %H:%M:%S.%f")[:-3]
                if ts in rows_by_ts:
                    duplicate_ts_count += 1
                # 同一变量同一时间戳只保留最后一次值，不再篡改采样时间。
                rows_by_ts[ts] = val
            except (ValueError, TypeError):
                invalid_value_count += 1
                logger.warning(
                    "Skip non-numeric point device=%s variable_id=%s table=%s value=%r",
                    point.device_id,
                    point.variable_id,
                    target.table_name,
                    point.value,
                )
                continue

        if invalid_value_count:
            self._total_failed += invalid_value_count

        if not rows_by_ts:
            return 0

        if duplicate_ts_count:
            self._total_failed += duplicate_ts_count
            logger.warning(
                "TDengine duplicate timestamp detected for table=%s variable_id=%s duplicates=%s",
                target.table_name,
                target.variable_id,
                duplicate_ts_count,
            )

        values_sql = " ".join(
            f"('{ts}', {val})" for ts, val in rows_by_ts.items()
        )
        sql = (
            f"INSERT INTO `{target.database_name}`.`{target.table_name}` (`ts`, `value`) "
            f"VALUES {values_sql}"
        )
        return await self.tdengine.execute(sql)

    def _emit_storage_failure(self, points: List[DataPoint], error: Exception, elapsed_ms: float) -> None:
        """Record TDengine write errors as system logs without blocking the writer."""
        if self._system_logger is None:
            return

        device_codes = sorted({point.device_id for point in points if point.device_id})
        variable_ids = sorted({point.variable_id for point in points if point.variable_id})
        self._system_logger.emit(
            level="ERROR",
            log_type="storage",
            source="collector.batch_writer",
            content="TDengine batch write failed",
            detail_json={
                "points": len(points),
                "devices": device_codes[:20],
                "devices_count": len(device_codes),
                "variables_count": len(variable_ids),
                "error": str(error),
                "fake_mode": self.fake_mode,
            },
            exception_stack="".join(
                traceback.format_exception(type(error), error, error.__traceback__)
            )[:4000],
            elapsed_ms=int(elapsed_ms),
        )

    def _record_storage_summary(
        self,
        points_count: int,
        affected_count: int,
        failed_count: int,
        elapsed_ms: float,
        error: bool = False,
    ) -> None:
        """Emit compact TDengine storage summaries at a fixed interval."""
        if self._system_logger is None:
            return

        self._summary_batches += 1
        self._summary_points += points_count
        self._summary_affected += affected_count
        self._summary_failed += failed_count
        self._summary_elapsed_ms += elapsed_ms

        now = time.time()
        if now - self._summary_started_at < self._summary_interval_seconds:
            return

        interval_seconds = max(0.001, now - self._summary_started_at)
        average_elapsed_ms = (
            self._summary_elapsed_ms / self._summary_batches
            if self._summary_batches
            else 0
        )
        level = "WARN" if error or self._summary_failed else "INFO"
        self._system_logger.emit(
            level=level,
            log_type="storage",
            source="collector.batch_writer",
            content="TDengine write summary",
            detail_json={
                "interval_seconds": round(interval_seconds, 3),
                "batches": self._summary_batches,
                "points": self._summary_points,
                "affected": self._summary_affected,
                "failed": self._summary_failed,
                "average_elapsed_ms": round(average_elapsed_ms, 3),
                "total_written": self._total_written,
                "total_failed": self._total_failed,
                "fake_mode": self.fake_mode,
            },
        )

        self._summary_started_at = now
        self._summary_batches = 0
        self._summary_points = 0
        self._summary_affected = 0
        self._summary_failed = 0
        self._summary_elapsed_ms = 0.0

    def write_sync(self, points: List[DataPoint]) -> asyncio.Task:
        """
        写入回调（用于聚合器输出）

        返回一个 Task，调用方应该 await 这个 Task 以确保写入完成。
        写入失败时 Task 会抛出异常，调用方需要处理。

        示例:
            task = batch_writer.write_sync(points)
            await task  # 等待完成，失败时抛出异常
        """
        task = asyncio.create_task(self.write(points))
        self._pending_tasks.add(task)
        task.add_done_callback(self._on_write_done)
        return task

    def _on_write_done(self, task: asyncio.Task) -> None:
        """
        任务完成回调，用于清理待处理任务集合

        注意：异常处理由调用方（聚合器）负责，此处只记录日志用于调试。
        """
        self._pending_tasks.discard(task)
        if task.cancelled():
            logger.warning("Managed write task cancelled")
        elif task.exception():
            # 调用方应该 await task 来处理异常，这里只做兜底日志
            logger.debug("Managed write task failed: %s", task.exception())

    async def drain(self, timeout: float | None = None) -> None:
        """等待已提交的写入任务完成，用于优雅停机。"""
        if not self._pending_tasks:
            return

        pending = list(self._pending_tasks)
        done, still_pending = await asyncio.wait(pending, timeout=timeout)
        if still_pending:
            logger.warning("Write drain timeout, pending_tasks=%s", len(still_pending))
        for task in done:
            if task.cancelled():
                continue
            try:
                task.result()
            except Exception:
                # _on_write_done 已经记录具体错误，避免重复刷屏。
                pass

    async def run_writer_loop(
        self,
        flush_func,
        flush_interval: float = 1.0,
    ) -> None:
        """
        写入循环：定期从聚合器取数据并写入
        Args:
            flush_func: 协程函数，返回待写入的DataPoint列表
            flush_interval: 刷新间隔（秒）
        """
        self._running = True
        logger.info("BatchWriter started")

        while self._running:
            try:
                points = await flush_func()
                if points:
                    await self.write(points)
                await asyncio.sleep(flush_interval)
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"BatchWriter error: {e}")
                await asyncio.sleep(1)

        self._running = False
        logger.info("BatchWriter stopped")

    def stop(self) -> None:
        self._running = False

    @property
    def stats(self):
        return {
            "total_written": self._total_written,
            "total_failed": self._total_failed,
        }
