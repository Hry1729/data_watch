from __future__ import annotations

import asyncio
import pytest

import main
from main import (
    _connect_mapper_connections,
    _cancel_consumer_tasks,
    _connect_writers,
    _partition_stream_keys,
    _refresh_stream_scopes_once,
    _stream_keys_for_scopes,
)


def test_partition_stream_keys_assigns_each_shard_once() -> None:
    keys = [f"stream-{index}" for index in range(4)]

    partitions = _partition_stream_keys(keys, 4)

    assert partitions == [["stream-0"], ["stream-1"], ["stream-2"], ["stream-3"]]
    assert sorted(key for partition in partitions for key in partition) == keys


def test_partition_stream_keys_caps_workers_to_stream_count() -> None:
    assert _partition_stream_keys(["stream-0", "stream-1"], 8) == [
        ["stream-0"],
        ["stream-1"],
    ]


def test_partition_stream_keys_rejects_non_positive_workers() -> None:
    with pytest.raises(ValueError, match="consumer_workers"):
        _partition_stream_keys(["stream-0"], 0)


def test_stream_keys_for_scopes_expands_every_shard() -> None:
    assert _stream_keys_for_scopes([("qc", "g001"), ("agv", "g002")], 2) == [
        "rt:stream:qc:g001:0",
        "rt:stream:qc:g001:1",
        "rt:stream:agv:g002:0",
        "rt:stream:agv:g002:1",
    ]


@pytest.mark.asyncio
async def test_refresh_stream_scopes_adds_new_keys_and_drains_before_live_read(monkeypatch) -> None:
    class FakeConnection:
        def __init__(self) -> None:
            self.closed = False

        def close(self) -> None:
            self.closed = True

    class FakeConsumer:
        def __init__(self, name: str) -> None:
            self._consumer_name = name
            self.queued = []

        def queue_stream_key(self, key: str) -> None:
            self.queued.append(key)

    connection = FakeConnection()
    monkeypatch.setattr(main, "_open_mysql_connection", lambda config: connection)
    monkeypatch.setattr(
        main,
        "_discover_stream_scopes",
        lambda conn: [("qc", "g001"), ("agv", "g002")],
    )
    consumers = [FakeConsumer("writer-1"), FakeConsumer("writer-2")]
    partitions = [["rt:stream:old:g000:0"], ["rt:stream:old:g000:1"]]
    known = set(partitions[0] + partitions[1])

    new_keys = await _refresh_stream_scopes_once({}, 1, consumers, partitions, known)

    assert new_keys == {"rt:stream:agv:g002:0", "rt:stream:qc:g001:0"}
    assert connection.closed is True
    assert sorted(key for part in partitions for key in part) == [
        "rt:stream:old:g000:0", "rt:stream:old:g000:1"
    ]
    assert sorted(key for consumer in consumers for key in consumer.queued) == sorted(new_keys)
    assert all(len(consumer.queued) == 1 for consumer in consumers)

    await _refresh_stream_scopes_once({}, 1, consumers, partitions, known)
    assert all(len(consumer.queued) == 1 for consumer in consumers)


def test_connect_mapper_connections_creates_one_connection_per_worker(monkeypatch) -> None:
    first = object()
    created = []

    def open_connection(config: dict):
        connection = object()
        created.append(connection)
        return connection

    monkeypatch.setattr(main, "_open_mysql_connection", open_connection)

    connections = _connect_mapper_connections({}, first, 4)

    assert connections == [first, *created]
    assert len(connections) == 4


def test_connect_mapper_connections_closes_open_connections_on_failure(monkeypatch) -> None:
    class FakeConnection:
        def __init__(self) -> None:
            self.closed = False

        def close(self) -> None:
            self.closed = True

    first = FakeConnection()
    second = FakeConnection()
    attempts = iter([second, RuntimeError("connect failed")])

    def open_connection(config: dict):
        result = next(attempts)
        if isinstance(result, Exception):
            raise result
        return result

    monkeypatch.setattr(main, "_open_mysql_connection", open_connection)

    with pytest.raises(RuntimeError, match="connect failed"):
        _connect_mapper_connections({}, first, 4)

    assert first.closed is True
    assert second.closed is True

@pytest.mark.asyncio
async def test_connect_writers_creates_one_connection_per_worker(monkeypatch) -> None:
    created = []

    class FakeWriter:
        def __init__(self, syslog=None) -> None:
            self.syslog = syslog
            self.connected = False
            created.append(self)

        async def connect(self, config: dict) -> None:
            self.connected = True

        async def close(self) -> None:
            pass

    monkeypatch.setattr(main, "TDengineWriter", FakeWriter)

    writers = await _connect_writers({"tdengine": {}}, 4, "syslog")

    assert writers == created
    assert len(writers) == 4
    assert all(writer.connected and writer.syslog == "syslog" for writer in writers)


@pytest.mark.asyncio
async def test_connect_writers_closes_existing_connections_on_failure(monkeypatch) -> None:
    created = []

    class FakeWriter:
        def __init__(self, syslog=None) -> None:
            self.closed = False
            created.append(self)

        async def connect(self, config: dict) -> None:
            if len(created) == 3:
                raise RuntimeError("connect failed")

        async def close(self) -> None:
            self.closed = True

    monkeypatch.setattr(main, "TDengineWriter", FakeWriter)

    with pytest.raises(RuntimeError, match="connect failed"):
        await _connect_writers({"tdengine": {}}, 4, None)

    assert created[0].closed is True
    assert created[1].closed is True


@pytest.mark.asyncio
async def test_connect_writers_binds_each_workers_metrics_before_connect(monkeypatch):
    metrics = [object(), object()]
    connected = []

    class FakeWriter:
        def __init__(self, syslog=None):
            self.metrics = None

        def bind_metrics(self, value):
            self.metrics = value

        async def connect(self, config):
            connected.append(self.metrics)

        async def close(self):
            pass

    monkeypatch.setattr(main, "TDengineWriter", FakeWriter)
    await _connect_writers({}, 2, None, metrics)
    assert connected == metrics



@pytest.mark.asyncio
async def test_failed_consumer_shutdown_cancels_other_running_workers():
    entered = asyncio.Event()

    async def keeps_running():
        entered.set()
        await asyncio.Event().wait()

    survivor = asyncio.create_task(keeps_running())
    await entered.wait()
    async def fails():
        raise RuntimeError("worker failed")

    failed = asyncio.create_task(fails())
    with pytest.raises(RuntimeError, match="worker failed"):
        await failed
    await asyncio.wait_for(_cancel_consumer_tasks([failed, survivor]), timeout=1)
    assert survivor.cancelled()


def test_read_worker_backlogs_uses_source_ts_and_preserves_unknown_totals():
    from unittest.mock import MagicMock

    redis_client = MagicMock()
    redis_client.xinfo_groups.side_effect = [
        [{"name": "writer", "lag": 5}],
        [{"name": "writer", "lag": None}],
    ]
    redis_client.xpending.side_effect = [
        {"pending": 2, "min": "1000-0"},
        {"pending": 0, "min": None},
    ]
    redis_client.xrange.return_value = [("1000-0", {"ts": "100"})]
    statuses = main._read_worker_backlogs(
        redis_client, "writer", [["s1", "s2"]], 5000
    )
    assert statuses[0] == {
        "lag_entries": None,
        "lag_known_entries": 5,
        "lag_unknown_streams": 1,
        "pending_entries": 2,
        "pending_known_entries": 2,
        "backlog_unknown_streams": 0,
        "oldest_pending_stream_age_ms": 4000,
        "oldest_pending_source_age_ms": 4900,
        "pending_source_age_unknown_streams": 0,
    }


def test_read_worker_backlogs_marks_pending_total_unknown_on_failed_stream():
    from unittest.mock import MagicMock
    import redis

    redis_client = MagicMock()
    redis_client.xinfo_groups.side_effect = [
        [{"name": "writer", "lag": 5}], redis.RedisError("offline")
    ]
    redis_client.xpending.return_value = {"pending": 2, "min": "1000-0"}
    redis_client.xrange.return_value = [("1000-0", {"ts": "100"})]
    result = main._read_worker_backlogs(
        redis_client, "writer", [["s1", "s2"]], 5000
    )[0]
    assert result["lag_entries"] is None
    assert result["lag_known_entries"] == 5
    assert result["pending_entries"] is None
    assert result["pending_known_entries"] == 2
    assert result["backlog_unknown_streams"] == 1


@pytest.mark.asyncio
async def test_performance_report_has_worker_identity_and_live_aggregate(monkeypatch):
    from unittest.mock import MagicMock
    from performance import WriterPerformanceMetrics

    statuses = [
        {"lag_entries": 4, "lag_known_entries": 4, "lag_unknown_streams": 0,
         "pending_entries": 2, "pending_known_entries": 2,
         "backlog_unknown_streams": 0, "oldest_pending_stream_age_ms": 1000,
         "oldest_pending_source_age_ms": 3000,
         "pending_source_age_unknown_streams": 0},
        {"lag_entries": None, "lag_known_entries": 0, "lag_unknown_streams": 1,
         "pending_entries": 3, "pending_known_entries": 3,
         "backlog_unknown_streams": 0, "oldest_pending_stream_age_ms": 2000,
         "oldest_pending_source_age_ms": 5000,
         "pending_source_age_unknown_streams": 0},
    ]
    monkeypatch.setattr(main, "_read_worker_backlogs", lambda *_args: statuses)
    consumers = [MagicMock(_consumer_name="writer-1"), MagicMock(_consumer_name="writer-2")]
    consumers[0].oldest_buffered_source_age_ms.return_value = 1000
    consumers[1].oldest_buffered_source_age_ms.return_value = 2000
    metrics = [WriterPerformanceMetrics(), WriterPerformanceMetrics()]
    metrics[0].record_payload_bytes(10)
    metrics[1].record_payload_bytes(20)
    report = await main._report_performance(
        MagicMock(), "writer", consumers, [["s1"], ["s2"]], metrics
    )
    assert [worker["consumer_name"] for worker in report["workers"]] == [
        "writer-1", "writer-2"
    ]
    assert report["aggregate"]["counters"]["payload_bytes_admitted"] == 30
    assert report["aggregate"]["gauges"]["redis_pending_entries"] == 5
    assert report["aggregate"]["gauges"]["redis_lag_entries"] is None
    assert report["aggregate"]["gauges"]["redis_lag_known_entries"] == 4
    assert report["aggregate"]["gauges"]["redis_lag_unknown_streams"] == 1
    assert report["aggregate"]["gauges"]["oldest_buffered_source_age_ms"] == 2000
    assert report["aggregate"]["rates"] is None
    metrics[0].record_payload_bytes(20)
    metrics[0].record_pipeline(
        build_ms=0, build_wait_ms=0, execute_ms=0, ack_ms=0,
        sql_bytes=100, bisect_attempts=0, value_rows=2, quality_rows=4,
        sql_value_rows_succeeded=2, sql_quality_rows_succeeded=4,
    )
    later = await main._report_performance(
        MagicMock(), "writer", consumers, [["s1"], ["s2"]], metrics,
        previous=report, elapsed_s=2.0,
    )
    assert later["aggregate"]["rates"]["admitted_payload_bytes_per_s"] == 10.0
    assert later["aggregate"]["rates"]["sql_bytes_per_s"] == 50.0
    assert later["aggregate"]["rates"]["committed_frames_per_s"] == 1.0
    assert later["workers"][1]["rates"]["sql_bytes_per_s"] == 0.0


@pytest.mark.asyncio
async def test_performance_watcher_samples_event_loop_lag():
    from performance import WriterPerformanceMetrics

    metrics = [WriterPerformanceMetrics()]
    task = asyncio.create_task(
        main._watch_performance(None, "writer", [], [], metrics, interval_s=60)
    )
    try:
        await asyncio.sleep(0.28)
    finally:
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task
    assert metrics[0].snapshot()["timings"]["event_loop_lag_ms"]["count"] >= 1
