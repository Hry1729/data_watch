"""B30 回归：XAUTOCLAIM 的两/三元素响应都必须可用；形态异常必须**回退**而非静默停止回收。

Redis 6.2/6.x 返回两元素 ``[next_cursor, entries]``；7.0+ 才增加第三元素（被删除条目）。
修复前 `_drain_with_xautoclaim` 硬性三元素解包 ⇒ 6.x 上抛 ``ValueError``，而该异常不匹配
`drain_pending` 的 "unknown command"/"xautoclaim" 回退条件 ⇒ 每轮回收只留一条 warning、
**PEL 回收静默停止**。
"""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock

import pytest

WRITER_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = WRITER_ROOT.parent
for _candidate in (WRITER_ROOT, REPO_ROOT):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from consumer import StreamConsumer, XAutoclaimUnsupported  # noqa: E402
from performance import WriterPerformanceMetrics  # noqa: E402

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


def _consumer(redis_client: MagicMock) -> StreamConsumer:
    return StreamConsumer(
        _config(),
        MagicMock(),
        MagicMock(),
        redis_client,
        metrics=WriterPerformanceMetrics(),
    )


@pytest.mark.asyncio
async def test_two_element_reply_is_supported() -> None:
    """Redis 6.2/6.x：两元素响应不得抛错。"""
    client = MagicMock()
    client.xautoclaim.return_value = ["0-0", []]
    consumer = _consumer(client)

    await consumer._drain_with_xautoclaim(STREAM, "writer-1")

    assert client.xautoclaim.called


@pytest.mark.asyncio
async def test_three_element_reply_is_still_supported() -> None:
    """Redis 7.0+：三元素响应保持可用（回归护栏）。"""
    client = MagicMock()
    client.xautoclaim.return_value = ["0-0", [], []]
    consumer = _consumer(client)

    await consumer._drain_with_xautoclaim(STREAM, "writer-1")

    assert client.xautoclaim.called


@pytest.mark.asyncio
async def test_malformed_reply_raises_dedicated_error() -> None:
    client = MagicMock()
    client.xautoclaim.return_value = "garbage"
    consumer = _consumer(client)

    with pytest.raises(XAutoclaimUnsupported):
        await consumer._drain_with_xautoclaim(STREAM, "writer-1")


@pytest.mark.asyncio
async def test_malformed_reply_falls_back_instead_of_stopping_recovery() -> None:
    """关键：形态异常必须走 XPENDING+XCLAIM 回退，不能静默停止 PEL 回收。"""
    client = MagicMock()
    client.xautoclaim.return_value = "garbage"
    consumer = _consumer(client)
    fallback = AsyncMock()
    consumer._drain_with_xpending_xclaim = fallback

    await consumer.drain_pending(STREAM, "writer-1")

    fallback.assert_awaited()


@pytest.mark.asyncio
async def test_unknown_command_still_falls_back() -> None:
    """既有行为保持：服务端不支持 XAUTOCLAIM 时仍回退。"""
    client = MagicMock()
    client.xautoclaim.side_effect = Exception("ERR unknown command 'XAUTOCLAIM'")
    consumer = _consumer(client)
    fallback = AsyncMock()
    consumer._drain_with_xpending_xclaim = fallback

    await consumer.drain_pending(STREAM, "writer-1")

    fallback.assert_awaited()
