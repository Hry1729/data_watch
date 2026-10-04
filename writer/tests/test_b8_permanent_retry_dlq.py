"""B8：按错误类别判定的永久性 RETRY（size）推 DLQ 后 ACK；其余 RETRY 类别保持重试。

验收场景：
- 单帧 SQL 超出语句上限（reason=size）的行：DLQ 写入被接受后 XACK，不进隔离区、不留主缓冲，
  同批好行照常 ACK —— 不再无限重试占满隔离区形成背压。
- DLQ 写入失败：不 ACK，留主缓冲与 PEL（DLQ 成功才 ACK）。
- connection / unknown / schema / bisect_depth：不进 DLQ、不 ACK。
"""
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import consumer as consumer_mod
import tdengine_writer
from consumer import StreamConsumer
from performance import WriterPerformanceMetrics
from tdengine_writer import LANDED, RETRY, TDengineWriter

STREAM = "rt:stream:qc:g001:0"


def _config() -> dict:
    return {
        "writer": {
            "flush_interval_ms": 500,
            "batch_count": 5,
            "max_buffer_rows": 10,
            "pending_min_idle_ms": 0,
            "max_row_retry": 3,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }


def _entry_data() -> dict:
    return {
        "device_code": "qc100",
        "device_type": "qc",
        "group_code": "g001",
        "ts": "1000",
        "config_version": "0",
        "values": '{"1": "1.5"}',
    }


def _consumer(writer, redis_client) -> StreamConsumer:
    consumer = StreamConsumer(
        _config(), MagicMock(), writer, redis_client, metrics=WriterPerformanceMetrics(),
    )
    mapping = MagicMock()
    mapping.var_map = {"1": "c1"}
    mapping.child_table = "qc100_1"
    mapping.stable = "sdqc"
    consumer._mapper.get_cached = MagicMock(return_value=mapping)
    return consumer


def _writer(reason: str, *, local_retry: bool) -> MagicMock:
    writer = MagicMock()
    writer._last_retry_reasons = {0: reason}
    writer._last_local_retry_indices = {0} if local_retry else set()
    writer.write_multi_table = AsyncMock(
        return_value=[([0], RETRY, 1.0), ([1], LANDED, 1.0)]
    )
    return writer


def _acked(redis_client) -> set[str]:
    return {eid for call in redis_client.xack.call_args_list for eid in call.args[2:]}


@pytest.mark.asyncio
async def test_permanent_size_row_goes_to_dlq_then_acks():
    redis_client = MagicMock()
    consumer = _consumer(_writer("size", local_retry=True), redis_client)
    await consumer._handle_entry(STREAM, "1-0", _entry_data(), 1)
    await consumer._handle_entry(STREAM, "2-0", _entry_data(), 1)

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._flush()

    dlq.assert_called_once()
    kwargs = dlq.call_args.kwargs
    assert kwargs["entry_id"] == "1-0"
    assert kwargs["data"] == _entry_data()
    assert "permanent size" in kwargs["last_error"]
    assert kwargs["segment_evidence"]["overall_status"] == "permanent_size"
    assert _acked(redis_client) == {"1-0", "2-0"}
    assert consumer._buffer == []
    assert consumer._isolated == []


@pytest.mark.asyncio
async def test_permanent_size_row_stays_pending_when_dlq_push_fails():
    redis_client = MagicMock()
    consumer = _consumer(_writer("size", local_retry=True), redis_client)
    await consumer._handle_entry(STREAM, "1-0", _entry_data(), 1)
    await consumer._handle_entry(STREAM, "2-0", _entry_data(), 1)

    with patch.object(consumer_mod, "push_dlq", return_value=False):
        await consumer._flush()

    assert _acked(redis_client) == {"2-0"}
    assert [row.entry_id for row in consumer._buffer] == ["1-0"]
    assert consumer._isolated == []


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("reason", "local_retry"),
    [("connection", False), ("unknown", False), ("schema", True), ("bisect_depth", False)],
)
async def test_recoverable_retry_reasons_never_reach_dlq(reason, local_retry):
    redis_client = MagicMock()
    consumer = _consumer(_writer(reason, local_retry=local_retry), redis_client)
    await consumer._handle_entry(STREAM, "1-0", _entry_data(), 1)
    await consumer._handle_entry(STREAM, "2-0", _entry_data(), 1)

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._flush()

    dlq.assert_not_called()
    assert _acked(redis_client) == {"2-0"}
    held = [row.entry_id for row in consumer._buffer + consumer._isolated]
    assert held == ["1-0"]


@pytest.mark.asyncio
async def test_writer_tags_oversized_single_row_with_size_reason(monkeypatch):
    monkeypatch.setattr(tdengine_writer, "MAX_SQL_BYTES", 256)
    wide = {f"c{i}": 1.0 for i in range(64)}
    writer = TDengineWriter()
    writer._database = "device_data"
    writer._col_cache = {"sdqc": set(wide)}
    executed: list[str] = []
    writer._conn = MagicMock()
    writer._conn.execute = MagicMock(side_effect=lambda sql: executed.append(sql) or 1)

    results = await writer.write_multi_table([
        {"child_table": "qc100_1", "stable": "sdqc", "device_code": "qc100",
         "ts": 1000, "columns": wide},
    ])

    assert results == [([0], RETRY, 0.0)]
    assert writer._last_retry_reasons == {0: "size"}
    assert executed == []
