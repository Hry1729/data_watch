from performance import WriterPerformanceMetrics


def test_writer_metrics_are_bounded_and_track_rows():
    metrics = WriterPerformanceMetrics()

    metrics.set_buffer_rows(8)
    metrics.record_write_batch(12.5, 5, True)
    metrics.record_write_batch(20.0, 3, False)
    metrics.record_flush(35.0, 8)
    metrics.set_buffer_rows(3)

    snapshot = metrics.snapshot()
    assert snapshot["timings"]["write_batch_ms"]["count"] == 2
    assert snapshot["distributions"]["rows_per_flush"]["count"] == 1
    assert snapshot["counters"]["rows_written"] == 5
    assert snapshot["counters"]["rows_failed"] == 3
    assert snapshot["gauges"]["buffer_rows"] == 3
    assert snapshot["gauges"]["peak_buffer_rows"] == 8
