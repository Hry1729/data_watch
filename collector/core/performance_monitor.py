"""性能监控"""

import asyncio
import functools
import inspect
import time
from collections import defaultdict, deque
from dataclasses import dataclass, field
from datetime import datetime, timedelta
from typing import Any, Dict, List, Optional, Callable
from contextlib import asynccontextmanager

from core.logger import get_logger

logger = get_logger("performance")


@dataclass
class MetricData:
    """指标数据"""
    name: str
    value: float
    timestamp: float
    tags: Dict[str, str] = field(default_factory=dict)
    metadata: Dict[str, Any] = field(default_factory=dict)


class PerformanceCollector:
    """性能数据收集器"""

    def __init__(self, max_metrics: int = 10000):
        self.max_metrics = max_metrics
        self.metrics: deque = deque(maxlen=max_metrics)
        self.counters: Dict[str, int] = defaultdict(int)
        self.gauges: Dict[str, float] = {}
        self.histograms: Dict[str, List[float]] = defaultdict(list)
        self._lock = asyncio.Lock()

    async def record_metric(
        self,
        name: str,
        value: float,
        tags: Optional[Dict[str, str]] = None,
        metadata: Optional[Dict[str, Any]] = None
    ):
        """记录指标"""
        async with self._lock:
            metric = MetricData(
                name=name,
                value=value,
                timestamp=time.time(),
                tags=tags or {},
                metadata=metadata or {}
            )
            self.metrics.append(metric)

    async def increment(self, name: str, value: int = 1, tags: Optional[Dict[str, str]] = None):
        """增加计数器"""
        async with self._lock:
            self.counters[name] = self.counters.get(name, 0) + value
            if tags:
                tag_key = f"{name}_{hash(str(tags))}"
                self.counters[tag_key] = self.counters.get(tag_key, 0) + value

    async def set_gauge(self, name: str, value: float, tags: Optional[Dict[str, str]] = None):
        """设置仪表盘值"""
        async with self._lock:
            if tags:
                # 带标签的仪表盘
                tag_key = f"{name}_{hash(str(tags))}"
                self.gauges[tag_key] = value
            else:
                # 简单仪表盘
                self.gauges[name] = value

    async def record_histogram(self, name: str, value: float, tags: Optional[Dict[str, str]] = None):
        """记录直方图"""
        async with self._lock:
            self.histograms[name].append(value)
            # 限制直方图大小
            if len(self.histograms[name]) > 1000:
                self.histograms[name] = self.histograms[name][-1000:]

    def get_counter(self, name: str) -> int:
        """获取计数器值"""
        return self.counters.get(name, 0)

    def get_gauge(self, name: str, tags: Optional[Dict[str, str]] = None) -> float:
        """获取仪表盘值"""
        if tags:
            return self.gauges.get(f"{name}_{hash(str(tags))}", 0.0)
        return self.gauges.get(name, 0.0)

    def get_histogram_stats(self, name: str) -> Dict[str, float]:
        """获取直方图统计"""
        values = self.histograms.get(name, [])
        if not values:
            return {}

        sorted_values = sorted(values)
        n = len(sorted_values)
        return {
            "count": n,
            "min": min(values),
            "max": max(values),
            "mean": sum(values) / n,
            "median": sorted_values[n // 2],
            "p95": sorted_values[int(n * 0.95)] if n > 0 else 0,
            "p99": sorted_values[int(n * 0.99)] if n > 0 else 0,
        }

    def get_recent_metrics(self, name: str, minutes: int = 60) -> List[MetricData]:
        """获取最近的指标"""
        cutoff = time.time() - minutes * 60
        return [m for m in self.metrics if m.name == name and m.timestamp > cutoff]

    def get_all_metrics(self) -> List[MetricData]:
        """获取所有指标"""
        return list(self.metrics)


class PerformanceTimer:
    """性能计时器"""

    def __init__(self, collector: PerformanceCollector, name: str, tags: Optional[Dict[str, str]] = None):
        self.collector = collector
        self.name = name
        self.tags = tags or {}
        self.start_time = None
        self.end_time = None

    def __enter__(self):
        self.start_time = time.time()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.end_time = time.time()
        duration = self.end_time - self.start_time
        asyncio.create_task(
            self.collector.record_histogram(
                f"duration_{self.name}",
                duration * 1000,  # 毫秒
                self.tags
            )
        )


@asynccontextmanager
async def measure_performance(collector: PerformanceCollector, name: str, tags: Optional[Dict[str, str]] = None):
    """异步性能测量上下文管理器"""
    start_time = time.time()
    try:
        yield
    finally:
        duration = time.time() - start_time
        await collector.record_histogram(
            f"duration_{name}",
            duration * 1000,  # 毫秒
            tags
        )


class PerformanceProfiler:
    """性能分析器"""

    def __init__(self, collector: PerformanceCollector):
        self.collector = collector
        self.function_stats: Dict[str, List[float]] = defaultdict(list)
        self._enabled = True

    def enable(self):
        """启用性能分析"""
        self._enabled = True

    def disable(self):
        """禁用性能分析"""
        self._enabled = False

    def profile_function(self, func_name: str):
        """函数性能分析装饰器"""
        def decorator(func):
            @functools.wraps(func)
            async def async_wrapper(*args, **kwargs):
                if not self._enabled:
                    return await func(*args, **kwargs)

                start = time.time()
                try:
                    result = await func(*args, **kwargs)
                    return result
                finally:
                    duration = time.time() - start
                    self.function_stats[func_name].append(duration)
                    await self.collector.record_histogram(
                        f"function_duration_{func_name}",
                        duration * 1000
                    )

            @functools.wraps(func)
            def sync_wrapper(*args, **kwargs):
                if not self._enabled:
                    return func(*args, **kwargs)

                start = time.time()
                try:
                    result = func(*args, **kwargs)
                    return result
                finally:
                    duration = time.time() - start
                    self.function_stats[func_name].append(duration)

            return async_wrapper if inspect.iscoroutinefunction(func) else sync_wrapper
        return decorator

    def get_function_stats(self, func_name: str) -> Dict[str, float]:
        """获取函数统计"""
        durations = self.function_stats.get(func_name, [])
        if not durations:
            return {}

        return {
            "count": len(durations),
            "min": min(durations),
            "max": max(durations),
            "mean": sum(durations) / len(durations),
            "p95": sorted(durations)[int(len(durations) * 0.95)] if durations else 0,
            "total": sum(durations),
        }

    def report(self) -> str:
        """生成性能报告"""
        report = ["Performance Report:"]
        report.append("=" * 50)

        # 系统级指标
        report.append("\nSystem Metrics:")
        for name, value in self.collector.gauges.items():
            report.append(f"  {name}: {value:.2f}")

        # 函数性能
        report.append("\nFunction Performance:")
        for func_name in self.function_stats:
            stats = self.get_function_stats(func_name)
            if stats.get("count", 0) > 0:
                report.append(f"  {func_name}:")
                report.append(f"    Calls: {stats['count']}")
                report.append(f"    Avg: {stats['mean']*1000:.2f}ms")
                report.append(f"    Min: {stats['min']*1000:.2f}ms")
                report.append(f"    Max: {stats['max']*1000:.2f}ms")

        return "\n".join(report)


# 全局性能收集器
_performance_collector = PerformanceCollector()


def get_performance_collector() -> PerformanceCollector:
    """获取全局性能收集器"""
    return _performance_collector


def init_performance(max_metrics: int = 10000) -> PerformanceCollector:
    """初始化性能监控"""
    global _performance_collector
    _performance_collector = PerformanceCollector(max_metrics)
    return _performance_collector
