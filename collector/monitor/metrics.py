"""性能指标收集"""

import asyncio
import time
from dataclasses import dataclass, field
from datetime import datetime
from typing import Any, Dict, List, Optional
from collections import defaultdict, deque

try:
    from prometheus_client import Counter, Gauge, Histogram, CollectorRegistry, generate_latest
    from prometheus_client.core import GaugeMetricFamily
    PROMETHEUS_AVAILABLE = True
except ImportError:
    Counter = None  # type: ignore
    Gauge = None  # type: ignore
    Histogram = None  # type: ignore
    CollectorRegistry = None  # type: ignore
    generate_latest = None  # type: ignore
    PROMETHEUS_AVAILABLE = False

from core.logger import get_logger

logger = get_logger("metrics")


@dataclass
class MetricData:
    """指标数据"""
    name: str
    value: float
    timestamp: float
    labels: Dict[str, str] = field(default_factory=dict)


class MetricsCollector:
    """性能指标收集器"""

    def __init__(self):
        # 原生指标存储
        self._metrics: Dict[str, deque] = defaultdict(deque)
        self._counters: Dict[str, int] = defaultdict(int)
        self._gauges: Dict[str, float] = defaultdict(float)
        self._histograms: Dict[str, List[float]] = defaultdict(list)

        # 指标配置
        self._max_history_size = 1000
        self._auto_flush_interval = 60.0  # 60秒自动刷新
        self._timer_timeout = 300.0  # 计时器超时时间（5分钟）
        self._timers: Dict[str, MetricData] = {}
        self._compat_timers: Dict[str, str] = {}

        # Prometheus指标（如果可用）
        self._registry = CollectorRegistry() if PROMETHEUS_AVAILABLE else None
        self._setup_prometheus_metrics()

        # 启动自动刷新任务
        self._flush_task = None
        self._running = False

    def _setup_prometheus_metrics(self):
        """设置Prometheus指标"""
        if not PROMETHEUS_AVAILABLE:
            return

        # 计数器
        self.collector_errors = Counter(
            'collector_errors_total',
            'Total number of collector errors',
            ['device', 'error_type'],
            registry=self._registry
        )

        self.data_points_processed = Counter(
            'data_points_processed_total',
            'Total number of data points processed',
            ['device', 'status'],  # status: success, failed, dropped
            registry=self._registry
        )

        self.devices_connected = Gauge(
            'devices_connected',
            'Number of currently connected devices',
            registry=self._registry
        )

        self.queue_size = Gauge(
            'data_queue_size',
            'Current size of data queue',
            registry=self._registry
        )

        self.database_write_duration = Histogram(
            'database_write_duration_seconds',
            'Time spent writing to database',
            ['device'],
            buckets=[0.1, 0.5, 1.0, 2.0, 5.0, 10.0],
            registry=self._registry
        )

        self.collection_latency = Histogram(
            'collection_latency_seconds',
            'Time from data collection to storage',
            ['device'],
            buckets=[0.001, 0.005, 0.01, 0.05, 0.1, 0.5, 1.0],
            registry=self._registry
        )

    # 基本指标方法
    def increment(self, name: str, value: int = 1, labels: Optional[Dict[str, str]] = None):
        """增加计数器"""
        self._counters[name] += value
        metric = MetricData(
            name=name,
            value=float(value),
            timestamp=time.time(),
            labels=labels or {}
        )
        self._metrics[f"counter_{name}"].append(metric)

        # 更新Prometheus计数器
        if PROMETHEUS_AVAILABLE and name in ['collector_errors_total', 'data_points_processed_total']:
            self._update_prometheus_counter(name, value, labels)

    def gauge(self, name: str, value: float, labels: Optional[Dict[str, str]] = None):
        """设置仪表盘"""
        self._gauges[name] = value
        metric = MetricData(
            name=name,
            value=value,
            timestamp=time.time(),
            labels=labels or {}
        )
        self._metrics[f"gauge_{name}"].append(metric)

        # 更新Prometheus仪表盘
        if PROMETHEUS_AVAILABLE and name == 'devices_connected':
            self.devices_connected.set(value)
        elif PROMETHEUS_AVAILABLE and name == 'data_queue_size':
            self.queue_size.set(value)

    def histogram(self, name: str, value: float, labels: Optional[Dict[str, str]] = None):
        """记录直方图"""
        self._histograms[name].append(value)
        metric = MetricData(
            name=name,
            value=value,
            timestamp=time.time(),
            labels=labels or {}
        )
        self._metrics[f"histogram_{name}"].append(metric)

        # 限制历史记录大小
        if len(self._histograms[name]) > self._max_history_size:
            self._histograms[name] = self._histograms[name][-self._max_history_size:]

    def timer_start(self, name: str, labels: Optional[Dict[str, str]] = None) -> str:
        """开始计时，返回计时器ID"""
        timer_id = f"{name}_{time.time()}_{len(self._metrics)}"
        self._timers[timer_id] = MetricData(
            name=name,
            value=time.time(),
            timestamp=time.time(),
            labels=labels or {}
        )
        return timer_id

    def timer_stop(self, timer_id: str):
        """停止计时，记录耗时"""
        try:
            timer_data = self._timers.pop(timer_id)
            duration = time.time() - timer_data.value
            name = timer_data.name
            labels = timer_data.labels

            # 记录直方图
            self.histogram(name, duration, labels)

            # 更新Prometheus直方图
            if PROMETHEUS_AVAILABLE:
                if name == 'database_write_duration_seconds':
                    self.database_write_duration.labels(**labels).observe(duration)
                elif name == 'collection_latency_seconds':
                    self.collection_latency.labels(**labels).observe(duration)

        except KeyError:
            logger.warning("Timer %s not found", timer_id)

    # 业务指标
    def record_data_point(self, device_id: str, variable_id: str, value: Any, success: bool = True):
        """记录数据点处理"""
        status = "success" if success else "failed"
        labels = {"device": device_id}

        self.increment("data_points_processed", 1, {**labels, "status": status})

        # 记录数据质量指标
        if not success:
            self.increment("data_points_failed", 1, {"device": device_id})
            self.increment("collector_errors", 1, {"device": device_id, "error_type": "data_processing"})

    def record_device_status(self, device_id: str, status: str):
        """记录设备状态"""
        self.gauge(f"device_status_{device_id}", 1.0 if status == "online" else 0.0, {"device": device_id, "status": status})

        # 更新在线设备数
        online_devices = sum(
            1
            for key, value in self._gauges.items()
            if key.startswith("device_status_") and value == 1.0
        )
        self.gauge("devices_connected", online_devices)

    def record_database_write(self, device_id: str, duration: float, success: bool = True):
        """记录数据库写入"""
        labels = {"device": device_id}

        if success:
            self.histogram("database_write_duration", duration, labels)
        else:
            self.increment("collector_errors", 1, {**labels, "error_type": "database_write"})

    def record_collection_latency(self, device_id: str, latency: float):
        """记录采集延迟"""
        labels = {"device": device_id}
        self.histogram("collection_latency", latency, labels)

    def get_metrics_summary(self) -> Dict[str, Any]:
        """获取指标摘要"""
        summary = {
            "counters": dict(self._counters),
            "gauges": dict(self._gauges),
            "histograms": {
                name: {
                    "count": len(values),
                    "min": min(values),
                    "max": max(values),
                    "avg": sum(values) / len(values),
                    "p95": sorted(values)[int(len(values) * 0.95)] if values else 0,
                }
                for name, values in self._histograms.items()
            },
            "timestamp": time.time()
        }
        return summary

    def get_prometheus_metrics(self) -> str:
        """获取Prometheus格式的指标"""
        if not PROMETHEUS_AVAILABLE:
            return ""
        return generate_latest(self._registry).decode('utf-8')

    async def start_auto_flush(self):
        """启动自动刷新"""
        if self._running:
            return

        self._running = True
        self._flush_task = asyncio.create_task(self._auto_flush_loop())
        logger.info("Metrics auto flush started")

    async def stop_auto_flush(self):
        """停止自动刷新"""
        self._running = False
        if self._flush_task and not self._flush_task.done():
            self._flush_task.cancel()
            try:
                await self._flush_task
            except asyncio.CancelledError:
                pass
        logger.info("Metrics auto flush stopped")

    async def _auto_flush_loop(self):
        """自动刷新循环"""
        while self._running:
            try:
                # 清理旧指标
                await self._cleanup_old_metrics()
                await asyncio.sleep(self._auto_flush_interval)
            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error("Metrics auto flush error: %s", e)
                await asyncio.sleep(5)

    async def _cleanup_old_metrics(self):
        """清理旧指标和过期计时器"""
        current_time = time.time()
        max_age = 3600  # 1小时

        # 清理队列中的旧指标
        for name, metrics in self._metrics.items():
            if isinstance(metrics, deque):
                while metrics and current_time - metrics[0].timestamp > max_age:
                    metrics.popleft()

        # 清理过期计时器（防止内存泄漏）
        stale_timer_ids = [
            timer_id
            for timer_id, timer_data in self._timers.items()
            if current_time - timer_data.timestamp > self._timer_timeout
        ]
        if stale_timer_ids:
            for timer_id in stale_timer_ids:
                self._timers.pop(timer_id, None)
            logger.warning("Cleaned up %s stale timers (not stopped properly)", len(stale_timer_ids))

        # 清理兼容计时器中的过期项
        stale_compat_timers = [
            name for name, timer_id in self._compat_timers.items()
            if timer_id not in self._timers
        ]
        for name in stale_compat_timers:
            self._compat_timers.pop(name, None)

        logger.debug("Metrics cleanup completed")

    def export_to_dict(self) -> Dict[str, Any]:
        """导出指标为字典"""
        return {
            "counters": dict(self._counters),
            "gauges": dict(self._gauges),
            "histograms": {
                name: {
                    "count": len(values),
                    "min": min(values),
                    "max": max(values),
                    "avg": sum(values) / len(values),
                    "p95": sorted(values)[int(len(values) * 0.95)] if values else 0,
                }
                for name, values in self._histograms.items()
            },
            "prometheus_enabled": PROMETHEUS_AVAILABLE
        }

    def _update_prometheus_counter(self, name: str, value: int, labels: Optional[Dict[str, str]] = None):
        """更新Prometheus计数器"""
        if not PROMETHEUS_AVAILABLE:
            return

        if name == 'collector_errors_total':
            self.collector_errors.labels(**labels or {}).inc(value)
        elif name == 'data_points_processed_total':
            self.data_points_processed.labels(**labels or {}).inc(value)

    # 兼容旧API
    def start_timer(self, name: str) -> None:
        """开始计时（兼容旧API）"""
        self._compat_timers[name] = self.timer_start(name)

    def stop_timer(self, name: str) -> float:
        """停止计时（兼容旧API）"""
        timer_id = self._compat_timers.pop(name, None)
        if timer_id is not None:
            self.timer_stop(timer_id)
        return 0.0

    def get_metrics(self) -> Dict:
        """获取指标（兼容旧API）"""
        return self.get_metrics_summary()


# 全局指标收集器实例
_metrics_collector = None


def get_metrics_collector() -> MetricsCollector:
    """获取指标收集器实例"""
    global _metrics_collector
    if _metrics_collector is None:
        _metrics_collector = MetricsCollector()
    return _metrics_collector


def init_metrics_collector() -> MetricsCollector:
    """初始化指标收集器"""
    global _metrics_collector
    _metrics_collector = MetricsCollector()
    return _metrics_collector
