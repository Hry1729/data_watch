"""Low-overhead runtime metrics for collector performance baselines."""

from __future__ import annotations

import ctypes
import gc
import math
import os
import threading
from typing import Any


_DEFAULT_BUCKETS_MS = (
    0.1,
    0.25,
    0.5,
    1.0,
    2.0,
    5.0,
    10.0,
    20.0,
    30.0,
    40.0,
    50.0,
    75.0,
    100.0,
    125.0,
    150.0,
    200.0,
    300.0,
    500.0,
    1000.0,
    2000.0,
    math.inf,
)


class TimingHistogram:
    """Thread-safe fixed-bucket timing histogram with bounded memory."""

    def __init__(self, buckets_ms: tuple[float, ...] = _DEFAULT_BUCKETS_MS) -> None:
        self._buckets = buckets_ms
        self._counts = [0] * len(buckets_ms)
        self._count = 0
        self._sum_ms = 0.0
        self._max_ms = 0.0
        self._lock = threading.Lock()

    def record(self, value_ms: float) -> None:
        value = max(0.0, float(value_ms))
        with self._lock:
            self._count += 1
            self._sum_ms += value
            self._max_ms = max(self._max_ms, value)
            for index, upper_bound in enumerate(self._buckets):
                if value <= upper_bound:
                    self._counts[index] += 1
                    break

    def snapshot(self) -> dict[str, float | int]:
        with self._lock:
            count = self._count
            counts = tuple(self._counts)
            sum_ms = self._sum_ms
            max_ms = self._max_ms

        return {
            "count": count,
            "average_ms": round(sum_ms / count, 3) if count else 0.0,
            "max_ms": round(max_ms, 3),
            "p50_ms": self._quantile(counts, count, 0.50, max_ms),
            "p95_ms": self._quantile(counts, count, 0.95, max_ms),
            "p99_ms": self._quantile(counts, count, 0.99, max_ms),
            "p999_ms": self._quantile(counts, count, 0.999, max_ms),
        }

    def _quantile(
        self,
        counts: tuple[int, ...],
        count: int,
        quantile: float,
        max_ms: float,
    ) -> float:
        if count == 0:
            return 0.0

        target = max(1, math.ceil(count * quantile))
        cumulative = 0
        for upper_bound, bucket_count in zip(self._buckets, counts):
            cumulative += bucket_count
            if cumulative >= target:
                value = max_ms if math.isinf(upper_bound) else upper_bound
                return round(value, 3)
        return round(max_ms, 3)


class PerformanceMetrics:
    """Shared collector timing, counter, gauge, and runtime metrics."""

    def __init__(self) -> None:
        self._timings: dict[str, TimingHistogram] = {}
        self._counters: dict[str, int] = {}
        self._gauges: dict[str, int | float] = {}
        self._lock = threading.Lock()

    def record_timing(self, name: str, value_ms: float) -> None:
        with self._lock:
            histogram = self._timings.get(name)
            if histogram is None:
                histogram = TimingHistogram()
                self._timings[name] = histogram
        histogram.record(value_ms)

    def increment(self, name: str, amount: int = 1) -> None:
        with self._lock:
            self._counters[name] = self._counters.get(name, 0) + amount

    def set_gauge(self, name: str, value: int | float) -> None:
        with self._lock:
            self._gauges[name] = value

    def snapshot(self) -> dict[str, Any]:
        with self._lock:
            timings = dict(self._timings)
            counters = dict(self._counters)
            gauges = dict(self._gauges)

        return {
            "timings": {name: metric.snapshot() for name, metric in timings.items()},
            "counters": counters,
            "gauges": gauges,
            "runtime": {
                "rss_bytes": _get_rss_bytes(),
                "gc_counts": list(gc.get_count()),
                "gc_collections": [entry["collections"] for entry in gc.get_stats()],
            },
        }


def _get_rss_bytes() -> int | None:
    if os.name == "nt":
        return _get_windows_rss_bytes()

    try:
        with open("/proc/self/statm", encoding="ascii") as statm:
            rss_pages = int(statm.read().split()[1])
        return rss_pages * int(os.sysconf("SC_PAGE_SIZE"))
    except (OSError, ValueError, IndexError, AttributeError):
        return None


def _get_windows_rss_bytes() -> int | None:
    class ProcessMemoryCounters(ctypes.Structure):
        _fields_ = [
            ("cb", ctypes.c_ulong),
            ("PageFaultCount", ctypes.c_ulong),
            ("PeakWorkingSetSize", ctypes.c_size_t),
            ("WorkingSetSize", ctypes.c_size_t),
            ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
            ("QuotaPagedPoolUsage", ctypes.c_size_t),
            ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
            ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
            ("PagefileUsage", ctypes.c_size_t),
            ("PeakPagefileUsage", ctypes.c_size_t),
        ]

    counters = ProcessMemoryCounters()
    counters.cb = ctypes.sizeof(counters)
    try:
        process = ctypes.windll.kernel32.GetCurrentProcess()
        ok = ctypes.windll.psapi.GetProcessMemoryInfo(
            process,
            ctypes.byref(counters),
            counters.cb,
        )
    except (AttributeError, OSError):
        return None
    return int(counters.WorkingSetSize) if ok else None
