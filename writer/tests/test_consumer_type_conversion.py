"""writer 类型转换失败整 entry 隔离测试。"""

import json
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
    mapper = mapper or MagicMock()
    mapper.get_or_load.return_value = _mapping()
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
