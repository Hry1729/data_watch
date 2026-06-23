"""Consumer-side tests for §7.6 binary-search degrade wiring in _flush.

Verifies the per-row status contract from write_multi_table is honored:
LANDED → XACK; BAD_DATA → DLQ then XACK only on success; RETRY (and DLQ-push
failure) → kept buffered, never acked (hard constraints ② and ④).
"""
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import consumer as consumer_mod
from consumer import StreamConsumer, _BufferedRow
from performance import WriterPerformanceMetrics
from tdengine_writer import BAD_DATA, LANDED, RETRY


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


def _row(entry_id: str) -> _BufferedRow:
    return _BufferedRow(
        entry_id=entry_id,
        stream_key="rt:stream:qc:g001:0",
        device_code="qc100",
        device_type="qc",
        group_code="g001",
        child_table="qc100_1",
        stable="sdqc",
        ts=1000,
        columns={"qc1": 1.0},
        entry_data={"device_code": "qc100", "values": "{}"},
    )


def _consumer(writer, redis_client):
    return StreamConsumer(
        _config(), MagicMock(), writer, redis_client, metrics=WriterPerformanceMetrics()
    )


@pytest.mark.asyncio
async def test_bad_data_pushed_to_dlq_then_acked():
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], BAD_DATA, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    consumer._buffer = [_row("1-0")]

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._flush()

    dlq.assert_called_once()
    redis_client.xack.assert_called_once()
    assert consumer._buffer == []  # 坏行已隔离离开 buffer


@pytest.mark.asyncio
async def test_bad_data_dlq_failure_keeps_buffer_and_does_not_ack():
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], BAD_DATA, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")
    consumer._buffer = [row]

    with patch.object(consumer_mod, "push_dlq", return_value=False):
        await consumer._flush()

    redis_client.xack.assert_not_called()  # DLQ 失败 → 绝不 ACK
    assert consumer._buffer == [row]  # 留 buffer，不丢


@pytest.mark.asyncio
async def test_retry_status_keeps_buffer_and_does_not_ack():
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")
    consumer._buffer = [row]

    with patch.object(consumer_mod, "push_dlq") as dlq:
        await consumer._flush()

    dlq.assert_not_called()  # RETRY 绝不进 DLQ
    redis_client.xack.assert_not_called()
    assert consumer._buffer == [row]


@pytest.mark.asyncio
async def test_mixed_landed_bad_retry_split_correctly():
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(
        return_value=[([0], LANDED, 1.0), ([1], BAD_DATA, 1.0), ([2], RETRY, 1.0)]
    )
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    rows = [_row("1-0"), _row("2-0"), _row("3-0")]
    consumer._buffer = list(rows)

    with patch.object(consumer_mod, "push_dlq", return_value=True):
        await consumer._flush()

    # LANDED + 成功 DLQ 的坏行 → ACK；RETRY 行留 buffer
    acked = {eid for call in redis_client.xack.call_args_list for eid in call.args[2:]}
    assert acked == {"1-0", "2-0"}
    assert consumer._buffer == [rows[2]]
