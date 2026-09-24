"""消费者端测试，验证刷新时的二分搜索降级功能。Consumer-side tests for §7.6 binary-search degrade wiring in _flush.

Verifies the per-row status contract from write_multi_table is honored:
LANDED → XACK + XDEL; BAD_DATA → DLQ then XACK + XDEL only on success;
RETRY (and DLQ-push failure) → kept buffered, never acked or deleted
(hard constraints ② and ④).
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
    redis_client.xdel.assert_called_once()
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
    redis_client.xdel.assert_not_called()
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
    redis_client.xdel.assert_not_called()
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
    deleted = {eid for call in redis_client.xdel.call_args_list for eid in call.args[1:]}
    assert deleted == {"1-0", "2-0"}
    assert consumer._buffer == [rows[2]]


def test_xack_failure_never_deletes_and_retries_ack_first():
    writer = MagicMock()
    redis_client = MagicMock()
    redis_client.xack.side_effect = [RuntimeError("redis unavailable"), 1]
    consumer = _consumer(writer, redis_client)

    consumer._ack_ids("rt:stream:qc:g001:0", ["1-0"])

    redis_client.xdel.assert_not_called()
    assert consumer._ack_retry == {"rt:stream:qc:g001:0": {"1-0"}}

    consumer._retry_pending_acks()

    redis_client.xdel.assert_called_once_with("rt:stream:qc:g001:0", "1-0")
    assert consumer._ack_retry == {}


def test_xdel_failure_is_retried_without_repeating_xack():
    writer = MagicMock()
    redis_client = MagicMock()
    redis_client.xdel.side_effect = [RuntimeError("redis unavailable"), 1]
    consumer = _consumer(writer, redis_client)

    consumer._ack_ids("rt:stream:qc:g001:0", ["1-0"])

    redis_client.xack.assert_called_once()
    assert consumer._delete_retry == {"rt:stream:qc:g001:0": {"1-0"}}

    consumer._retry_pending_deletes()

    redis_client.xack.assert_called_once()
    assert redis_client.xdel.call_count == 2
    assert consumer._delete_retry == {}


# ----------------------------------------------------------------------
# 持续 RETRY 的隔离：一条长期不恢复的行不得拖住同 worker 其它 stream
# ----------------------------------------------------------------------

@pytest.mark.asyncio
async def test_persistently_retrying_row_leaves_main_buffer_after_max_retries():
    """连续 RETRY 达上限后移出主缓冲，但绝不 ACK、绝不进 DLQ。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")
    consumer._buffer = [row]

    with patch.object(consumer_mod, "push_dlq") as dlq:
        for _ in range(consumer._max_row_retry):
            consumer._buffer = [row]
            await consumer._flush()

    dlq.assert_not_called()                       # RETRY 绝不进 DLQ
    redis_client.xack.assert_not_called()         # 也不能 ACK
    redis_client.xdel.assert_not_called()
    assert consumer._buffer == []                 # 不再占用主缓冲容量
    assert [r.entry_id for r in consumer._isolated] == ["1-0"]  # 但仍在本地跟踪
    assert row.retry_attempts >= consumer._max_row_retry


@pytest.mark.asyncio
async def test_isolated_row_does_not_block_healthy_rows_of_same_worker():
    """隔离发生后，同 worker 其它行仍能正常落库并 ACK，且读取容量恢复。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(
        return_value=[([0], RETRY, 1.0), ([1], LANDED, 1.0)]
    )
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    bad, healthy = _row("1-0"), _row("2-0")
    consumer._buffer = [bad, healthy]

    for _ in range(consumer._max_row_retry):
        consumer._buffer = [bad, healthy]
        await consumer._flush()

    acked = {eid for call in redis_client.xack.call_args_list for eid in call.args[2:]}
    assert acked == {"2-0"}, "健康行必须照常 ACK，不被坏行拖住"
    assert [r.entry_id for r in consumer._isolated] == ["1-0"]
    # 主缓冲不再被坏行占住：剩余容量回到满额（隔离区另计，仍受上限约束）。
    assert consumer._remaining_capacity() == consumer._max_buffer_rows - 1


@pytest.mark.asyncio
async def test_isolated_rows_are_readmitted_for_retry_not_abandoned():
    """隔离不是放弃：按回收周期放回主缓冲重试一次。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")

    for _ in range(consumer._max_row_retry):
        consumer._buffer = [row]
        await consumer._flush()
    assert [r.entry_id for r in consumer._isolated] == ["1-0"]

    consumer._readmit_isolated()
    assert consumer._isolated == []
    assert [r.entry_id for r in consumer._buffer] == ["1-0"]
    assert consumer._isolated_keys == set()


@pytest.mark.asyncio
async def test_isolated_set_is_bounded_and_restores_backpressure():
    """隔离区有上限：超出后新行留在主缓冲形成背压，而不是无界增长。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    consumer._max_isolated_rows = 2
    rows = [_row(f"{i}-0") for i in range(1, 5)]

    for _ in range(consumer._max_row_retry):
        consumer._buffer = list(rows)
        # 每行单独成为一块，确保都走 RETRY 分支
        writer.write_multi_table = AsyncMock(
            side_effect=lambda batch: [([i], RETRY, 1.0) for i in range(len(batch))]
        )
        await consumer._flush()

    assert len(consumer._isolated) <= 2, "隔离区不得超过上限"
    assert consumer._buffer, "超限后必须有行留在主缓冲形成背压"
    assert len(consumer._isolated) + len(consumer._buffer) == 4, "不能丢行"
