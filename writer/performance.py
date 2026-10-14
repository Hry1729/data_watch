"""由性能基准使用的有界写入器指标。"""

from __future__ import annotations

import math
import threading
import time


# 阶段层级：**只加同层**。父阶段已经包含它的子阶段，跨层相加会重复计数。
# 特别地，容量满时 drain 会触发 flush，那次 flush 计入 flush_outer_ms，
# drain 侧只留排除掉它的 drain_own_ms。
STAGE_CONTAINMENT: dict[str, tuple[str, ...]] = {
    "loop_iteration_ms": (
        "read_block_ms",
        "drain_own_ms",
        "overdelivered_own_ms",
        "ack_retry_outer_ms",
        "delete_retry_outer_ms",
        "flush_outer_ms",
        "idle_ms",
        "loop_other_ms",
    ),
    "read_block_ms": ("read_claim_ms", "prepare_ms", "handle_entry_ms", "read_other_ms"),
    "flush_outer_ms": ("build_ms", "build_wait_ms", "td_execute_ms", "ack_ms", "flush_other_ms"),
    "drain_own_ms": ("drain_claim_ms", "drain_other_ms"),
    "overdelivered_own_ms": ("overdelivered_claim_ms", "overdelivered_other_ms"),
}


class ExecutorTiming:
    """一次 run_in_executor 的等待拆段，附带调用期间的线程 CPU 时间。

    只算"外层减内层"会把线程池排队和事件循环恢复混成一段，所以在四个点位取时钟：

        queue_ms    线程开始   - 提交前     （线程池排队）
        call_ms     调用结束   - 线程开始   （同步调用，含网络与驱动等待）
        call_cpu_ms 调用期间的线程 CPU 时间 （区分计算与等待）
        resume_ms   await 返回 - 调用结束   （事件循环恢复）

    call_ms 是"这一跳等了多久"，**不是**数据库的执行时间：里面含网络往返、
    驱动内部等待与结果解码。call_cpu_ms 才是这段时间里真正在算的部分。
    """

    __slots__ = ("queued_at", "started_at", "finished_at", "call_cpu_ms")

    def __init__(self) -> None:
        self.queued_at: float = 0.0
        self.started_at: float = 0.0
        self.finished_at: float = 0.0
        self.call_cpu_ms: float = 0.0

    def wrap(self, call):
        """包住要在执行器线程里跑的那个同步调用。"""

        def run():
            self.started_at = time.perf_counter()
            cpu_started = time.thread_time()
            try:
                return call()
            finally:
                self.call_cpu_ms = (time.thread_time() - cpu_started) * 1000
                self.finished_at = time.perf_counter()

        return run

    def record(self, metrics, name: str, resumed_at: float) -> None:
        """await 返回后调用；三段只在这一次累加，避免上下游各记一遍。

        超时或取消时执行器里的调用可能**还没返回**（`finished_at` 仍为 0）：这类
        标为"未完成"，只记已经等到的部分，且不进入完成样本的段长。否则
        `resumed_at - 0` 会被当成一个巨大段长，半截时间也会污染均值。
        每次调用都新建一个 ExecutorTiming，所以后台线程之后才结束只是写在一个
        已经记完账的对象上，不会串到下一批。
        """
        if self.finished_at <= 0.0 or self.finished_at > resumed_at:
            metrics.record_executor(
                name,
                completed=False,
                # 连线程都没排上时 started_at 仍为 0，排队段就是已经等过的全部
                queue_ms=max(0.0, (min(self.started_at or resumed_at, resumed_at) - self.queued_at) * 1000),
                call_ms=(max(0.0, (resumed_at - self.started_at) * 1000)
                         if self.started_at > 0.0 else 0.0),
                call_cpu_ms=self.call_cpu_ms,
                resume_ms=0.0,
            )
            return
        metrics.record_executor(
            name,
            completed=True,
            queue_ms=max(0.0, (self.started_at - self.queued_at) * 1000),
            call_ms=max(0.0, (self.finished_at - self.started_at) * 1000),
            call_cpu_ms=self.call_cpu_ms,
            resume_ms=max(0.0, (resumed_at - self.finished_at) * 1000),
        )


async def run_instrumented(loop, executor, metrics, name: str, call):
    """在默认/指定执行器里跑 `call`，并把 `name` 的三段等待累加进指标。

    只在**唯一一处**调用，避免同一段等待被上下游各记一遍。
    """
    timing = ExecutorTiming()
    timing.queued_at = time.perf_counter()
    try:
        return await loop.run_in_executor(executor, timing.wrap(call))
    finally:
        timing.record(metrics, name, time.perf_counter())


_TIMING_BUCKETS = (
    0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 50.0,
    100.0, 200.0, 500.0, 1000.0, 2000.0, 5000.0, math.inf,
)
_SQL_BYTE_BUCKETS = (
    1024.0, 4096.0, 16384.0, 65536.0, 262144.0,
    524288.0, 1048576.0, 2097152.0, 3145728.0, math.inf,
)
_ROW_BUCKETS = (
    1.0, 10.0, 50.0, 100.0, 250.0, 500.0, 1000.0,
    2500.0, 5000.0, 10000.0, 50000.0, math.inf,
)


class FixedHistogram:
    def __init__(self, buckets: tuple[float, ...]) -> None:
        self._buckets = buckets
        self._counts = [0] * len(buckets)
        self._count = 0
        self._sum = 0.0
        self._max = 0.0
        self._lock = threading.Lock()

    def record(self, value: float) -> None:
        value = max(0.0, float(value))
        with self._lock:
            self._count += 1
            self._sum += value
            self._max = max(self._max, value)
            for index, upper_bound in enumerate(self._buckets):
                if value <= upper_bound:
                    self._counts[index] += 1
                    break

    def snapshot(self) -> dict[str, float | int]:
        with self._lock:
            count = self._count
            counts = tuple(self._counts)
            total = self._sum
            maximum = self._max
        return {
            "count": count,
            "average": round(total / count, 3) if count else 0.0,
            "max": round(maximum, 3),
            "p95": self._quantile(counts, count, 0.95, maximum),
            "p99": self._quantile(counts, count, 0.99, maximum),
            "p999": self._quantile(counts, count, 0.999, maximum),
        }

    def _quantile(
        self,
        counts: tuple[int, ...],
        count: int,
        quantile: float,
        maximum: float,
    ) -> float:
        if count == 0:
            return 0.0
        target = max(1, math.ceil(count * quantile))
        cumulative = 0
        for upper_bound, bucket_count in zip(self._buckets, counts):
            cumulative += bucket_count
            if cumulative >= target:
                value = maximum if math.isinf(upper_bound) else upper_bound
                return round(value, 3)
        return round(maximum, 3)


class WriterPerformanceMetrics:
    def __init__(self) -> None:
        self._write_batch_ms = FixedHistogram(_TIMING_BUCKETS)
        self._flush_ms = FixedHistogram(_TIMING_BUCKETS)
        self._rows_per_flush = FixedHistogram(_ROW_BUCKETS)
        self._sql_statement_bytes = FixedHistogram(_SQL_BYTE_BUCKETS)
        self._build_ms = FixedHistogram(_TIMING_BUCKETS)
        self._build_wait_ms = FixedHistogram(_TIMING_BUCKETS)
        self._execute_ms = FixedHistogram(_TIMING_BUCKETS)
        self._ack_ms = FixedHistogram(_TIMING_BUCKETS)
        self._parse_ms = FixedHistogram(_TIMING_BUCKETS)
        self._event_loop_lag_ms = FixedHistogram(_TIMING_BUCKETS)
        # 阶段累计（毫秒）与执行器调用的三段汇总。**按批/按次累加**，不逐点逐条
        # 记直方图：逐条既放大观测开销，也让分布被长尾淹没。
        self._stages: dict[str, float] = {}
        self._executor: dict[str, dict[str, float]] = {}
        self._counters: dict[str, int] = {
            "write_batch_success_count": 0,
            "write_batch_failure_count": 0,
            "rows_written": 0,
            "rows_failed": 0,
            "flush_count": 0,
            "committed_value_rows": 0,
            "committed_quality_rows": 0,
            "sql_bytes": 0,
            "sql_value_rows_attempted": 0,
            "sql_quality_rows_attempted": 0,
            "sql_value_rows_succeeded": 0,
            "sql_quality_rows_succeeded": 0,
            "payload_bytes_admitted": 0,
            "bisect_attempts": 0,
            "retry_connection_rows": 0,
            "retry_schema_rows": 0,
            "retry_size_rows": 0,
            "retry_unknown_rows": 0,
            "retry_bisect_depth_rows": 0,
            # B31：终态 XDEL 的拓扑守卫必须**按原因分别可见**（否则会与其它未知重试混在一起，
            # 无法回答"这台实例到底有没有因为多消费组而拒绝删除"）。
            "retry_terminal_delete_multi_group_skipped_rows": 0,
            "retry_terminal_delete_topology_unknown_rows": 0,
        }
        self._gauges: dict[str, int | None] = {
            "buffer_rows": 0,
            "peak_buffer_rows": 0,
            "isolated_rows": 0,
            "peak_isolated_rows": 0,
            "buffer_bytes": 0,
            "peak_buffer_bytes": 0,
            "oldest_buffered_source_age_ms": None,
            "redis_lag_entries": None,
            "redis_lag_known_entries": 0,
            "redis_lag_unknown_streams": 0,
            "redis_pending_entries": None,
            "redis_pending_known_entries": 0,
            "redis_backlog_unknown_streams": 0,
            "oldest_pending_stream_age_ms": None,
            "oldest_pending_source_age_ms": None,
            "pending_source_age_unknown_streams": 0,
        }
        self._lock = threading.Lock()

    def record_write_batch(self, elapsed_ms: float, rows: int, success: bool) -> None:
        self._write_batch_ms.record(elapsed_ms)
        with self._lock:
            if success:
                self._counters["write_batch_success_count"] += 1
                self._counters["rows_written"] += rows
            else:
                self._counters["write_batch_failure_count"] += 1
                self._counters["rows_failed"] += rows

    def record_flush(self, elapsed_ms: float, rows: int) -> None:
        self._flush_ms.record(elapsed_ms)
        self._rows_per_flush.record(rows)
        with self._lock:
            self._counters["flush_count"] += 1

    def record_retry(self, reason: str) -> None:
        name = f"retry_{reason}_rows"
        if name not in self._counters:
            name = "retry_unknown_rows"
        with self._lock:
            self._counters[name] += 1

    def record_parse(self, elapsed_ms: float) -> None:
        self._parse_ms.record(elapsed_ms)

    def record_stage(self, name: str, elapsed_ms: float) -> None:
        """累加一次阶段耗时。同一段等待只允许在唯一一处累加，见 STAGE_CONTAINMENT。"""
        with self._lock:
            self._stages[name] = self._stages.get(name, 0.0) + max(0.0, elapsed_ms)

    def record_executor(
        self, name: str, *, completed: bool, queue_ms: float, call_ms: float,
        call_cpu_ms: float, resume_ms: float,
    ) -> None:
        """累加一次执行器调用的三段等待与线程 CPU 时间（见 ExecutorTiming）。

        `completed=False` 是"超时/取消，调用尚未返回"：只计未完成计数与已等到的
        部分，**不进**完成样本的段长与均值。
        """
        with self._lock:
            bucket = self._executor.get(name)
            if bucket is None:
                bucket = self._executor[name] = {
                    "calls": 0.0, "queue_ms": 0.0, "call_ms": 0.0,
                    "call_cpu_ms": 0.0, "resume_ms": 0.0, "total_ms": 0.0,
                    "incomplete_calls": 0.0, "incomplete_queue_ms": 0.0,
                    "incomplete_call_ms": 0.0,
                }
            if not completed:
                bucket["incomplete_calls"] += 1
                bucket["incomplete_queue_ms"] += queue_ms
                bucket["incomplete_call_ms"] += call_ms
                return
            bucket["calls"] += 1
            bucket["queue_ms"] += queue_ms
            bucket["call_ms"] += call_ms
            bucket["call_cpu_ms"] += call_cpu_ms
            bucket["resume_ms"] += resume_ms
            bucket["total_ms"] += queue_ms + call_ms + resume_ms

    def stage_totals(self) -> dict[str, float]:
        """取阶段累计快照；调用方用它做嵌套相减（例如排除 drain 内触发的 flush）。"""
        with self._lock:
            return dict(self._stages)

    def record_event_loop_lag(self, elapsed_ms: float) -> None:
        self._event_loop_lag_ms.record(elapsed_ms)

    def record_pipeline(
        self, *, build_ms: float, build_wait_ms: float,
        execute_ms: float, ack_ms: float, sql_bytes: int,
        bisect_attempts: int, value_rows: int, quality_rows: int,
        sql_value_rows_attempted: int = 0, sql_quality_rows_attempted: int = 0,
        sql_value_rows_succeeded: int = 0, sql_quality_rows_succeeded: int = 0,
        sql_statement_bytes: list[int] | tuple[int, ...] = (),
    ) -> None:
        for statement_bytes in sql_statement_bytes:
            self._sql_statement_bytes.record(statement_bytes)
        self._build_ms.record(build_ms)
        self._build_wait_ms.record(build_wait_ms)
        self._execute_ms.record(execute_ms)
        self._ack_ms.record(ack_ms)
        with self._lock:
            for name, value in (
                ("build_ms", build_ms), ("build_wait_ms", build_wait_ms),
                ("td_execute_ms", execute_ms), ("ack_ms", ack_ms),
            ):
                self._stages[name] = self._stages.get(name, 0.0) + max(0.0, value)
            self._counters["sql_bytes"] += sql_bytes
            self._counters["sql_value_rows_attempted"] += sql_value_rows_attempted
            self._counters["sql_quality_rows_attempted"] += sql_quality_rows_attempted
            self._counters["sql_value_rows_succeeded"] += sql_value_rows_succeeded
            self._counters["sql_quality_rows_succeeded"] += sql_quality_rows_succeeded
            self._counters["bisect_attempts"] += bisect_attempts
            self._counters["committed_value_rows"] += value_rows
            self._counters["committed_quality_rows"] += quality_rows

    def record_payload_bytes(self, size: int) -> None:
        with self._lock:
            self._counters["payload_bytes_admitted"] += size

    def set_buffer_bytes(self, size: int) -> None:
        with self._lock:
            self._gauges["buffer_bytes"] = size
            self._gauges["peak_buffer_bytes"] = max(
                self._gauges["peak_buffer_bytes"], size
            )

    def set_buffer_rows(self, rows: int, isolated_rows: int = 0) -> None:
        with self._lock:
            self._gauges["buffer_rows"] = rows
            self._gauges["peak_buffer_rows"] = max(
                self._gauges["peak_buffer_rows"],
                rows,
            )
            # 持续 RETRY 被隔离出主缓冲的行数。它们仍在 Redis PEL 中，
            # 按回收周期重试，不计入 buffer_rows，但必须可见。
            self._gauges["isolated_rows"] = isolated_rows
            self._gauges["peak_isolated_rows"] = max(
                self._gauges["peak_isolated_rows"], isolated_rows
            )

    def set_backlog(
        self, *, lag_entries: int | None, lag_known_entries: int,
        lag_unknown_streams: int, pending_entries: int | None,
        pending_known_entries: int, backlog_unknown_streams: int,
        oldest_pending_stream_age_ms: int | None,
        oldest_pending_source_age_ms: int | None,
        pending_source_age_unknown_streams: int,
        oldest_buffered_source_age_ms: int | None,
    ) -> None:
        with self._lock:
            self._gauges.update({
                "redis_lag_entries": lag_entries,
                "redis_lag_known_entries": lag_known_entries,
                "redis_lag_unknown_streams": lag_unknown_streams,
                "redis_pending_entries": pending_entries,
                "redis_pending_known_entries": pending_known_entries,
                "redis_backlog_unknown_streams": backlog_unknown_streams,
                "oldest_pending_stream_age_ms": oldest_pending_stream_age_ms,
                "oldest_pending_source_age_ms": oldest_pending_source_age_ms,
                "pending_source_age_unknown_streams": pending_source_age_unknown_streams,
                "oldest_buffered_source_age_ms": oldest_buffered_source_age_ms,
            })

    def snapshot(self) -> dict:
        with self._lock:
            counters = dict(self._counters)
            gauges = dict(self._gauges)
            stages = dict(self._stages)
            executor = {name: dict(bucket) for name, bucket in self._executor.items()}
        return {
            "timings": {
                "write_batch_ms": self._write_batch_ms.snapshot(),
                "flush_ms": self._flush_ms.snapshot(),
                "parse_ms": self._parse_ms.snapshot(),
                "event_loop_lag_ms": self._event_loop_lag_ms.snapshot(),
                "build_ms": self._build_ms.snapshot(),
                "build_wait_ms": self._build_wait_ms.snapshot(),
                "td_execute_ms": self._execute_ms.snapshot(),
                "ack_ms": self._ack_ms.snapshot(),
                # 阶段层级：stages_ms 里父子并存，**只能加同层**；
                # stage_containment 给出父子关系，供报告与核对脚本使用。
                "stages_ms": {name: round(value, 3) for name, value in stages.items()},
                "executor_ms": {
                    name: {field: round(value, 3) for field, value in bucket.items()}
                    for name, bucket in executor.items()
                },
                "stage_containment": {
                    parent: list(children) for parent, children in STAGE_CONTAINMENT.items()
                },
            },
            "distributions": {
                "rows_per_flush": self._rows_per_flush.snapshot(),
                "sql_statement_bytes": self._sql_statement_bytes.snapshot(),
            },
            "counters": counters,
            "gauges": gauges,
        }
