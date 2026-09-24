"""由性能基准使用的有界写入器指标。"""

from __future__ import annotations

import math
import threading


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
        }
        self._gauges: dict[str, int | None] = {
            "buffer_rows": 0,
            "peak_buffer_rows": 0,
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

    def set_buffer_rows(self, rows: int) -> None:
        with self._lock:
            self._gauges["buffer_rows"] = rows
            self._gauges["peak_buffer_rows"] = max(
                self._gauges["peak_buffer_rows"],
                rows,
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
            },
            "distributions": {
                "rows_per_flush": self._rows_per_flush.snapshot(),
                "sql_statement_bytes": self._sql_statement_bytes.snapshot(),
            },
            "counters": counters,
            "gauges": gauges,
        }
