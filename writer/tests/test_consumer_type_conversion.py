"""writer 类型转换失败整 entry 隔离测试。"""

import json
import asyncio
import threading
from unittest.mock import MagicMock, patch

import pytest

import consumer as consumer_mod
from consumer import StreamConsumer
from mysql_mapper import GroupMapping
from performance import WriterPerformanceMetrics


def _config() -> dict:
    return {
        "writer": {
            "flush_interval_ms": 500,
            "batch_count": 100,
            "max_buffer_rows": 1000,
            "pending_min_idle_ms": 60000,
            "pending_max_retry": 3,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }


def _mapping() -> GroupMapping:
    return GroupMapping(
        config_version=0,
        child_table="qc100",
        stable="sdqc",
        var_map={"v1": "qc1", "v2": "qc2"},
    )


def _consumer(mapper=None):
    writer = MagicMock()
    redis_client = MagicMock()
    if mapper is None:
        mapper = MagicMock()
        mapping = _mapping()
        mapper.get_cached.return_value = mapping
        mapper.get_or_load.return_value = mapping
    return (
        StreamConsumer(
            _config(), mapper, writer, redis_client, metrics=WriterPerformanceMetrics()
        ),
        redis_client,
    )


def _entry_data(values: dict) -> dict:
    return {
        "device_code": "qc100",
        "device_type": "qc",
        "group_code": "g001",
        "ts": "1000",
        "config_version": "0",
        "values": json.dumps(values),
    }


def _malformed_entry(**updates) -> dict:
    entry = _entry_data({"v1": "1.0"})
    entry.update(updates)
    return entry


@pytest.mark.asyncio
async def test_invalid_string_pushes_dlq_not_buffer():
    consumer, redis_client = _consumer()
    entry_data = _entry_data({"v1": "abc"})

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry("rt:stream:qc:g001:0", "1-0", entry_data, 1)

    assert consumer._buffer == []
    dlq.assert_called_once()
    assert "type_conversion" in dlq.call_args.kwargs["last_error"]
    assert "v1" in dlq.call_args.kwargs["last_error"]
    redis_client.xack.assert_called_once()


@pytest.mark.asyncio
async def test_none_value_pushes_dlq_not_buffer():
    consumer, redis_client = _consumer()
    entry_data = _entry_data({"v1": None})

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry("rt:stream:qc:g001:0", "1-0", entry_data, 1)

    assert consumer._buffer == []
    dlq.assert_called_once()
    assert "type_conversion" in dlq.call_args.kwargs["last_error"]
    assert "v1" in dlq.call_args.kwargs["last_error"]
    redis_client.xack.assert_called_once()


@pytest.mark.asyncio
async def test_nan_value_pushes_dlq_not_buffer():
    consumer, redis_client = _consumer()
    entry_data = _entry_data({"v1": "nan"})

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry("rt:stream:qc:g001:0", "1-0", entry_data, 1)

    assert consumer._buffer == []
    dlq.assert_called_once()
    assert "type_conversion" in dlq.call_args.kwargs["last_error"]
    assert "v1" in dlq.call_args.kwargs["last_error"]


@pytest.mark.asyncio
async def test_mixed_good_and_bad_isolates_whole_entry():
    """任一 var 失败则整 entry 不入 buffer。"""
    consumer, redis_client = _consumer()
    entry_data = _entry_data({"v1": "1.5", "v2": "bad"})

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry("rt:stream:qc:g001:0", "1-0", entry_data, 1)

    assert consumer._buffer == []
    dlq.assert_called_once()
    last_error = dlq.call_args.kwargs["last_error"]
    assert "type_conversion" in last_error
    assert "v2" in last_error
    redis_client.xack.assert_called_once()


@pytest.mark.asyncio
async def test_dlq_failure_keeps_no_buffer_and_no_ack():
    consumer, redis_client = _consumer()
    entry_data = _entry_data({"v1": "abc"})

    with patch.object(consumer_mod, "push_dlq", return_value=False):
        await consumer._handle_entry("rt:stream:qc:g001:0", "1-0", entry_data, 1)

    assert consumer._buffer == []
    redis_client.xack.assert_not_called()


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("field", "value", "error_fragment"),
    [
        ("ts", "not-a-timestamp", "timestamp_invalid"),
        ("values", "{not-json", "values_json_invalid"),
        ("values", '["not-an-object"]', "values is not a JSON object"),
    ],
)
async def test_malformed_entry_goes_to_dlq_before_ack(field, value, error_fragment):
    consumer, redis_client = _consumer()

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "1-1",
            _malformed_entry(**{field: value}),
            1,
        )

    assert consumer._buffer == []
    dlq.assert_called_once()
    assert error_fragment in dlq.call_args.kwargs["last_error"]
    assert redis_client.xack.call_count == 1


@pytest.mark.asyncio
@pytest.mark.parametrize("field", ["device_code", "group_code"])
async def test_missing_required_identity_goes_to_dlq_before_ack(field):
    consumer, redis_client = _consumer()

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "1-2",
            _malformed_entry(**{field: ""}),
            1,
        )

    assert consumer._buffer == []
    dlq.assert_called_once()
    assert "device_code/group_code" in dlq.call_args.kwargs["last_error"]
    redis_client.xack.assert_called_once()


@pytest.mark.asyncio
async def test_malformed_entry_dlq_failure_stays_pending():
    consumer, redis_client = _consumer()

    with patch.object(consumer_mod, "push_dlq", return_value=False) as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "1-3",
            _malformed_entry(ts="not-a-timestamp"),
            1,
        )

    assert consumer._buffer == []
    dlq.assert_called_once()
    redis_client.xack.assert_not_called()


@pytest.mark.asyncio
async def test_slow_dlq_redis_call_does_not_block_consumer_event_loop():
    consumer, _ = _consumer()
    started = threading.Event()
    release = threading.Event()

    def slow_dlq(*args, **kwargs):
        started.set()
        release.wait(2)
        return False

    with patch.object(consumer_mod, "push_dlq", side_effect=slow_dlq):
        task = asyncio.create_task(
            consumer._handle_entry(
                "rt:stream:qc:g001:0",
                "1-slow",
                _malformed_entry(ts="not-a-timestamp"),
                1,
            )
        )
        await asyncio.sleep(0)
        assert await asyncio.to_thread(started.wait, 1)

        ticks = 0

        async def ticker():
            nonlocal ticks
            for _ in range(3):
                ticks += 1
                await asyncio.sleep(0)

        await asyncio.wait_for(ticker(), 0.2)
        assert ticks == 3
        release.set()
        await task


@pytest.mark.asyncio
@pytest.mark.parametrize("raw", ["0", "0.0", 0])
async def test_valid_zero_still_buffered(raw):
    consumer, redis_client = _consumer()
    entry_data = _entry_data({"v1": raw})

    with patch.object(consumer_mod, "push_dlq") as dlq:
        await consumer._handle_entry("rt:stream:qc:g001:0", "1-0", entry_data, 1)

    dlq.assert_not_called()
    assert len(consumer._buffer) == 1
    assert consumer._buffer[0].columns == {"qc1": 0.0}
    assert consumer._buffer[0].qualities == {}
    redis_client.xack.assert_not_called()


@pytest.mark.asyncio
async def test_valid_entry_buffered_unchanged():
    consumer, redis_client = _consumer()
    entry_data = _entry_data({"v1": "12.5", "v2": "3"})

    with patch.object(consumer_mod, "push_dlq") as dlq:
        await consumer._handle_entry("rt:stream:qc:g001:0", "1-0", entry_data, 1)

    dlq.assert_not_called()
    assert len(consumer._buffer) == 1
    assert consumer._buffer[0].columns == {"qc1": 12.5, "qc2": 3.0}
    assert consumer._buffer[0].qualities == {}

@pytest.mark.asyncio
async def test_mapping_cache_hit_avoids_executor_load():
    mapper = MagicMock()
    mapper.get_cached.return_value = _mapping()
    consumer, _ = _consumer(mapper)

    await consumer._handle_entry(
        "rt:stream:qc:g001:0",
        "1-0",
        _entry_data({"v1": "12.5"}),
        1,
    )

    mapper.get_or_load.assert_not_called()
    assert len(consumer._buffer) == 1


@pytest.mark.asyncio
async def test_mapping_cache_miss_loads_mapping_in_executor():
    mapper = MagicMock()
    mapper.get_cached.return_value = None
    mapper.get_or_load.return_value = _mapping()
    consumer, _ = _consumer(mapper)

    await consumer._handle_entry(
        "rt:stream:qc:g001:0",
        "1-0",
        _entry_data({"v1": "12.5"}),
        1,
    )

    mapper.get_or_load.assert_called_once_with("qc100", "g001", 0)
    assert len(consumer._buffer) == 1


@pytest.mark.asyncio
async def test_initial_mapping_load_missing_var_stays_pending():
    mapper = MagicMock()
    mapper.get_cached.return_value = None
    mapper.get_or_load.return_value = GroupMapping(
        config_version=0,
        child_table="qc100",
        stable="sdqc",
        var_map={"v1": "qc1"},
    )
    consumer, redis_client = _consumer(mapper)

    await consumer._handle_entry(
        "rt:stream:qc:g001:0",
        "1-0",
        _entry_data({"new": "2.5"}),
        1,
    )

    mapper.reload.assert_not_called()
    redis_client.xack.assert_not_called()
    assert consumer._buffer == []


@pytest.mark.asyncio
async def test_cached_mapping_reload_finds_new_var_without_silent_ack():
    mapper = MagicMock()
    cached = GroupMapping(config_version=0, child_table="qc100", stable="sdqc", var_map={"v1": "qc1"})
    refreshed = GroupMapping(
        config_version=0,
        child_table="qc100",
        stable="sdqc",
        var_map={"v1": "qc1", "new": "qc2"},
    )
    mapper.get_cached.return_value = cached
    mapper.reload.return_value = refreshed
    consumer, redis_client = _consumer(mapper)

    await consumer._handle_entry(
        "rt:stream:qc:g001:0",
        "1-0",
        _entry_data({"v1": "1.5", "new": "2.5"}),
        1,
    )

    mapper.reload.assert_called_once_with("qc100", "g001", 0, True)
    redis_client.xack.assert_not_called()
    assert len(consumer._buffer) == 1
    assert consumer._buffer[0].columns == {"qc1": 1.5, "qc2": 2.5}


@pytest.mark.asyncio
async def test_unknown_var_after_cached_mapping_reload_stays_pending():
    mapper = MagicMock()
    cached = GroupMapping(config_version=0, child_table="qc100", stable="sdqc", var_map={"v1": "qc1"})
    mapper.get_cached.return_value = cached
    mapper.reload.return_value = cached
    consumer, redis_client = _consumer(mapper)

    await consumer._handle_entry(
        "rt:stream:qc:g001:0",
        "1-0",
        _entry_data({"v1": "1.5", "new": "2.5"}),
        1,
    )

    mapper.reload.assert_called_once_with("qc100", "g001", 0, True)
    redis_client.xack.assert_not_called()
    assert consumer._buffer == []
