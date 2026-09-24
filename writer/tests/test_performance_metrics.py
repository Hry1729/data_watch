from performance import WriterPerformanceMetrics


def test_writer_metrics_are_bounded_and_track_rows():
    metrics = WriterPerformanceMetrics()

    metrics.set_buffer_rows(8)
    metrics.set_buffer_bytes(1024)
    metrics.record_payload_bytes(1024)
    metrics.record_write_batch(12.5, 5, True)
    metrics.record_write_batch(20.0, 3, False)
    metrics.record_flush(35.0, 8)
    metrics.set_buffer_rows(3)
    metrics.set_buffer_bytes(256)

    snapshot = metrics.snapshot()
    assert snapshot["timings"]["write_batch_ms"]["count"] == 2
    assert snapshot["distributions"]["rows_per_flush"]["count"] == 1
    assert snapshot["counters"]["rows_written"] == 5
    assert snapshot["counters"]["rows_failed"] == 3
    assert snapshot["gauges"]["buffer_rows"] == 3
    assert snapshot["gauges"]["peak_buffer_rows"] == 8
    assert snapshot["gauges"]["buffer_bytes"] == 256
    assert snapshot["gauges"]["peak_buffer_bytes"] == 1024
    assert snapshot["counters"]["payload_bytes_admitted"] == 1024


def test_writer_metrics_separate_event_loop_lag_and_unknown_redis_lag():
    metrics = WriterPerformanceMetrics()
    metrics.record_event_loop_lag(12.5)
    metrics.set_backlog(
        lag_entries=None, lag_known_entries=4, lag_unknown_streams=2,
        pending_entries=None, pending_known_entries=3,
        backlog_unknown_streams=1, oldest_pending_stream_age_ms=2000,
        oldest_pending_source_age_ms=4000,
        pending_source_age_unknown_streams=1,
        oldest_buffered_source_age_ms=5000,
    )
    snapshot = metrics.snapshot()
    assert snapshot["timings"]["event_loop_lag_ms"]["count"] == 1
    assert snapshot["gauges"]["redis_lag_entries"] is None
    assert snapshot["gauges"]["redis_lag_unknown_streams"] == 2
    assert snapshot["gauges"]["oldest_buffered_source_age_ms"] == 5000


def test_sql_statement_bytes_histogram_tracks_each_attempt():
    metrics = WriterPerformanceMetrics()
    metrics.record_pipeline(
        build_ms=0, build_wait_ms=0, execute_ms=0, ack_ms=0,
        sql_bytes=3072, bisect_attempts=0, value_rows=1, quality_rows=0,
        sql_statement_bytes=[1024, 2048],
    )
    snapshot = metrics.snapshot()
    assert snapshot["counters"]["sql_bytes"] == 3072
    assert snapshot["distributions"]["sql_statement_bytes"]["count"] == 2
    assert snapshot["distributions"]["sql_statement_bytes"]["max"] == 2048
