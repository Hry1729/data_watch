from concurrent.futures import ThreadPoolExecutor

from monitor.performance import PerformanceMetrics, TimingHistogram


def test_timing_histogram_reports_bounded_quantiles():
    histogram = TimingHistogram()

    for value in (0.5, 1.0, 2.0, 10.0, 100.0):
        histogram.record(value)

    snapshot = histogram.snapshot()

    assert snapshot["count"] == 5
    assert snapshot["average_ms"] == 22.7
    assert snapshot["max_ms"] == 100.0
    assert snapshot["p99_ms"] == 100.0
    assert snapshot["p999_ms"] == 100.0


def test_performance_metrics_are_thread_safe_and_include_runtime_snapshot():
    metrics = PerformanceMetrics()

    def record_batch() -> None:
        for _ in range(1000):
            metrics.record_timing("read_ms", 2.0)
            metrics.increment("reads")

    with ThreadPoolExecutor(max_workers=4) as pool:
        futures = [pool.submit(record_batch) for _ in range(4)]
        for future in futures:
            future.result()

    metrics.set_gauge("queue_depth", 7)
    snapshot = metrics.snapshot()

    assert snapshot["timings"]["read_ms"]["count"] == 4000
    assert snapshot["counters"]["reads"] == 4000
    assert snapshot["gauges"]["queue_depth"] == 7
    assert len(snapshot["runtime"]["gc_counts"]) == 3
    assert snapshot["runtime"]["rss_bytes"] is None or snapshot["runtime"]["rss_bytes"] > 0
