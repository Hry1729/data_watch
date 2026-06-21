from unittest.mock import AsyncMock, MagicMock

import pytest

from consumer import StreamConsumer, _BufferedRow
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
        "redis": {
            "group_name": "writer",
            "consumer_name": "writer-1",
        },
    }


@pytest.mark.asyncio
async def test_flush_records_batch_latency_rows_and_buffer_depth():
    metrics = WriterPerformanceMetrics()
    redis_client = MagicMock()
    writer = MagicMock()
    # 两行同子表 → 一个 chunk：返回 (覆盖的行下标, 成功, 耗时ms)
    writer.write_multi_table = AsyncMock(return_value=[([0, 1], True, 1.5)])
    consumer = StreamConsumer(
        _config(),
        MagicMock(),
        writer,
        redis_client,
        metrics=metrics,
    )
    consumer._buffer = [
        _BufferedRow(
            entry_id="1-0",
            stream_key="rt:stream:qc:g001:0",
            device_code="qc100",
            device_type="qc",
            group_code="g001",
            child_table="qc100_1",
            stable="sdqc",
            ts=1000,
            columns={"qc1": 1.0},
        ),
        _BufferedRow(
            entry_id="2-0",
            stream_key="rt:stream:qc:g001:0",
            device_code="qc100",
            device_type="qc",
            group_code="g001",
            child_table="qc100_1",
            stable="sdqc",
            ts=1100,
            columns={"qc1": 2.0},
        ),
    ]
    metrics.set_buffer_rows(2)

    await consumer._flush()

    snapshot = metrics.snapshot()
    assert snapshot["timings"]["write_batch_ms"]["count"] == 1
    assert snapshot["counters"]["rows_written"] == 2
    assert snapshot["distributions"]["rows_per_flush"]["count"] == 1
    assert snapshot["gauges"]["buffer_rows"] == 0
    redis_client.xack.assert_called_once()
