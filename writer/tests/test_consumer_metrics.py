from unittest.mock import AsyncMock, MagicMock

import pytest

from consumer import StreamConsumer, _BufferedRow
from performance import WriterPerformanceMetrics
from tdengine_writer import LANDED


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
    # 两行同子表 → 一个 chunk：返回 (覆盖的行下标, 状态, 耗时ms)
    writer.write_multi_table = AsyncMock(return_value=[([0, 1], LANDED, 1.5)])
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



@pytest.mark.asyncio
async def test_metrics_count_only_complete_frames_as_committed_rows():
    from tdengine_writer import RETRY

    metrics = WriterPerformanceMetrics()
    writer = MagicMock()
    writer._last_sql_bytes = 1234
    writer._last_build_ms = 2.0
    writer._last_build_wait_ms = 0.5
    writer._last_execute_ms = 3.0
    writer._last_bisect_attempts = 1
    writer._last_local_retry_indices = {0}
    writer.write_multi_table = AsyncMock(
        return_value=[([0], RETRY, 1.0), ([1], LANDED, 1.0)]
    )
    redis_client = MagicMock()
    consumer = StreamConsumer(_config(), MagicMock(), writer, redis_client, metrics=metrics)
    for index in range(2):
        consumer._buffer.append(
            _BufferedRow(
                entry_id=f"{index + 1}-0",
                stream_key="rt:stream:qc:g001:0",
                device_code="qc100",
                device_type="qc",
                group_code="g001",
                child_table="qc100_1",
                stable="sdqc",
                ts=1000 + index,
                columns={"qc1": 1.0},
                qualities={"101": 0, "102": 0},
            )
        )
    await consumer._flush()
    counters = metrics.snapshot()["counters"]
    assert counters["committed_value_rows"] == 1
    assert counters["committed_quality_rows"] == 2
    assert counters["sql_bytes"] == 1234
    assert counters["bisect_attempts"] == 1


def test_oldest_buffered_age_includes_inflight_source_entry():
    consumer = StreamConsumer(_config(), MagicMock(), MagicMock(), MagicMock())
    row = _BufferedRow(
        entry_id="4000-0", stream_key="s1", device_code="d", device_type="qc",
        group_code="g", child_table="c", stable="sdqc", ts=1000,
        columns={"qc1": 1.0},
    )
    consumer._buffer = [row]
    assert consumer.oldest_buffered_source_age_ms(5000) == 4000
    consumer._inflight_source_ms = 500
    assert consumer.oldest_buffered_source_age_ms(5000) == 4500
    consumer._buffer.clear()
    assert consumer.oldest_buffered_source_age_ms(5000) == 4500
