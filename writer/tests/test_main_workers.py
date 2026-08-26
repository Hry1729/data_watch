from __future__ import annotations

import pytest

import main
from main import (
    _connect_mapper_connections,
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
            self.group_keys = []
            self.drained = []

        def _ensure_consumer_groups(self, keys: list[str]) -> None:
            self.group_keys.extend(keys)

        async def drain_pending(self, key: str, consumer_name: str) -> None:
            self.drained.append((key, consumer_name))

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
    assert sorted(key for part in partitions for key in part) == sorted(known)
    assert all(len(consumer.group_keys) == 1 for consumer in consumers)
    assert all(len(consumer.drained) == 1 for consumer in consumers)
    assert all(
        consumer.drained[0][0] in consumer.group_keys for consumer in consumers
    )

    await _refresh_stream_scopes_once({}, 1, consumers, partitions, known)
    assert all(len(consumer.group_keys) == 1 for consumer in consumers)


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
