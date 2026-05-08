"""性能监控测试"""

import pytest
import asyncio
import time
from unittest.mock import patch

from core.performance_monitor import (
    PerformanceCollector,
    PerformanceTimer,
    PerformanceProfiler,
    measure_performance,
    get_performance_collector,
    init_performance,
)


class TestPerformanceCollector:
    """性能收集器测试"""

    @pytest.mark.asyncio
    async def test_record_metric(self):
        """测试记录指标"""
        collector = PerformanceCollector(max_metrics=100)

        # 记录指标
        await collector.record_metric("test_metric", 42.5, {"tag": "value"})

        # 获取指标
        metrics = collector.get_recent_metrics("test_metric")
        assert len(metrics) == 1
        assert metrics[0].value == 42.5
        assert metrics[0].tags["tag"] == "value"

    @pytest.mark.asyncio
    async def test_increment_counter(self):
        """测试计数器"""
        collector = PerformanceCollector()

        # 增加计数器
        await collector.increment("counter1", 5)
        await collector.increment("counter1", 3)

        # 获取计数器值
        assert collector.get_counter("counter1") == 8

    @pytest.mark.asyncio
    async def test_set_gauge(self):
        """测试设置仪表盘"""
        collector = PerformanceCollector()

        # 设置仪表盘
        await collector.set_gauge("gauge1", 100.0)
        await collector.set_gauge("gauge1", 200.0, {"tag": "value"})

        # 获取仪表盘值
        assert collector.get_gauge("gauge1") == 100.0
        assert collector.get_gauge("gauge1", {"tag": "value"}) == 200.0

    @pytest.mark.asyncio
    async def test_histogram_stats(self):
        """测试直方图统计"""
        collector = PerformanceCollector()

        # 记录直方图数据
        for i in range(10):
            await collector.record_histogram("hist1", i)

        # 获取统计
        stats = collector.get_histogram_stats("hist1")
        assert stats["count"] == 10
        assert stats["min"] == 0
        assert stats["max"] == 9
        assert stats["mean"] == 4.5


class TestPerformanceTimer:
    """性能计时器测试"""

    @pytest.mark.asyncio
    async def test_timer(self):
        """测试计时器"""
        collector = PerformanceCollector()
        timer = PerformanceTimer(collector, "test_timer")

        with timer:
            await asyncio.sleep(0.1)
        await asyncio.sleep(0)

        # 验证持续时间被记录
        stats = collector.get_histogram_stats("duration_test_timer")
        assert stats["count"] == 1
        assert 90 < stats["mean"] < 150  # 大约100ms

    @pytest.mark.asyncio
    async def test_measure_performance(self):
        """测试性能测量上下文管理器"""
        collector = PerformanceCollector()

        async def test_func():
            await asyncio.sleep(0.05)

        # 使用上下文管理器
        async with measure_performance(collector, "test_func"):
            await test_func()

        # 验证持续时间被记录
        stats = collector.get_histogram_stats("duration_test_func")
        assert stats["count"] == 1
        assert 40 < stats["mean"] < 70  # 大约50ms


class TestPerformanceProfiler:
    """性能分析器测试"""

    @pytest.mark.asyncio
    async def test_profile_function(self):
        """测试函数性能分析"""
        collector = PerformanceCollector()
        profiler = PerformanceProfiler(collector)

        @profiler.profile_function("test_func")
        async def test_func():
            await asyncio.sleep(0.01)
            return "result"

        # 调用函数多次
        for _ in range(5):
            await test_func()

        # 验证统计
        stats = profiler.get_function_stats("test_func")
        assert stats["count"] == 5
        assert stats["mean"] > 0.01  # 至少10ms

    @pytest.mark.asyncio
    async def test_profile_function_sync(self):
        """测试同步函数性能分析"""
        collector = PerformanceCollector()
        profiler = PerformanceProfiler(collector)

        @profiler.profile_function("sync_func")
        def sync_func():
            time.sleep(0.01)
            return "result"

        # 调用函数多次
        for _ in range(5):
            sync_func()

        # 验证统计
        stats = profiler.get_function_stats("sync_func")
        assert stats["count"] == 5
        assert stats["mean"] > 0.01  # 至少10ms

    def test_report(self):
        """测试性能报告"""
        collector = PerformanceCollector()
        profiler = PerformanceProfiler(collector)

        # 设置一些仪表盘值
        collector.gauges["metric1"] = 100.0
        collector.gauges["metric2"] = 200.0

        # 生成报告
        report = profiler.report()
        assert "Performance Report:" in report
        assert "metric1: 100.00" in report
        assert "metric2: 200.00" in report

    def test_enable_disable(self):
        """测试启用/禁用"""
        collector = PerformanceCollector()
        profiler = PerformanceProfiler(collector)

        # 禁用
        profiler.disable()
        assert not profiler._enabled

        # 启用
        profiler.enable()
        assert profiler._enabled


class TestGlobalPerformanceMonitor:
    """全局性能监控测试"""

    @pytest.mark.asyncio
    async def test_get_performance_collector(self):
        """测试获取全局性能收集器"""
        # 获取默认的
        collector1 = get_performance_collector()
        assert isinstance(collector1, PerformanceCollector)

        # 获取两次应该是同一个
        collector2 = get_performance_collector()
        assert collector1 is collector2

    @pytest.mark.asyncio
    async def test_init_performance(self):
        """测试初始化性能监控"""
        # 创建新的性能收集器
        new_collector = init_performance(max_metrics=1000)

        assert new_collector.max_metrics == 1000

        # 验证全局收集器被更新
        global_collector = get_performance_collector()
        assert global_collector is new_collector
