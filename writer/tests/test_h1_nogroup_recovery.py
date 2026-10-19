"""H1：XREADGROUP 报 NOGROUP 时必须重建消费组（id=0 MKSTREAM）并立即重试。"""
import asyncio
import time
from unittest.mock import AsyncMock, MagicMock

import pytest
import redis

from consumer import StreamConsumer
from performance import WriterPerformanceMetrics


def _consumer(redis_client) -> StreamConsumer:
    config = {
        "writer": {
            "flush_interval_ms": 500, "batch_count": 5, "max_buffer_rows": 10,
            "pending_min_idle_ms": 0, "max_row_retry": 3,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }
    consumer = StreamConsumer(
        config, MagicMock(), MagicMock(), redis_client,
        metrics=WriterPerformanceMetrics(),
    )
    consumer.drain_pending = AsyncMock()
    return consumer


@pytest.mark.asyncio
async def test_xreadgroup_nogroup_recreates_group_from_zero_and_retries():
    redis_client = MagicMock()
    redis_client.xinfo_groups.return_value = [{"name": "writer"}]
    redis_client.xreadgroup.side_effect = [
        redis.exceptions.ResponseError(
            "NOGROUP No such key 's1' or consumer group 'writer' in XREADGROUP with GROUP option"
        ),
        asyncio.CancelledError(),
    ]
    consumer = _consumer(redis_client)

    started = time.monotonic()
    with pytest.raises(asyncio.CancelledError):
        await consumer.run_loop(["s1"])

    # 启动一次 + NOGROUP 后重建一次；ID 必须是 0，保留缺组期间写入的积压
    assert redis_client.xgroup_create.call_count == 2
    for call in redis_client.xgroup_create.call_args_list:
        assert call.args == ("s1", "writer")
        assert call.kwargs == {"id": "0", "mkstream": True}
    # 重建后立即重试，不走通用错误分支的 sleep(1)
    assert redis_client.xreadgroup.call_count == 2
    assert time.monotonic() - started < 0.9


@pytest.mark.asyncio
async def test_xreadgroup_other_error_does_not_recreate_group():
    redis_client = MagicMock()
    redis_client.xinfo_groups.return_value = [{"name": "writer"}]
    redis_client.xreadgroup.side_effect = [
        redis.exceptions.ConnectionError("down"),
        asyncio.CancelledError(),
    ]
    consumer = _consumer(redis_client)
    with pytest.raises(asyncio.CancelledError):
        await consumer.run_loop(["s1"])
    assert redis_client.xgroup_create.call_count == 1
