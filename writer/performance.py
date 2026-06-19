"""Bounded writer metrics used by performance baselines."""

from __future__ import annotations

import math
import threading


_TIMING_BUCKETS = (
    0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 50.0,
    100.0, 200.0, 500.0, 1000.0, 2000.0, 5000.0, math.inf,
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
        self._counters: dict[str, int] = {
            "write_batch_success_count": 0,
            "write_batch_failure_count": 0,
            "rows_written": 0,
            "rows_failed": 0,
            "flush_count": 0,
        }
        self._gauges: dict[str, int] = {
            "buffer_rows": 0,
            "peak_buffer_rows": 0,
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

    def set_buffer_rows(self, rows: int) -> None:
        with self._lock:
            self._gauges["buffer_rows"] = rows
            self._gauges["peak_buffer_rows"] = max(
                self._gauges["peak_buffer_rows"],
                rows,
            )

    def snapshot(self) -> dict:
        with self._lock:
            counters = dict(self._counters)
            gauges = dict(self._gauges)
        return {
            "timings": {
                "write_batch_ms": self._write_batch_ms.snapshot(),
                "flush_ms": self._flush_ms.snapshot(),
            },
            "distributions": {
                "rows_per_flush": self._rows_per_flush.snapshot(),
            },
            "counters": counters,
            "gauges": gauges,
        }
