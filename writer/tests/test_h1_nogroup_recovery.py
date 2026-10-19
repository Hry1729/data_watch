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


_NOGROUP = (
    "NOGROUP No such key 's1' or consumer group 'writer' in XREADGROUP with GROUP option"
)


class _SleepLog:
    def __init__(self) -> None:
        self.delays: list[float] = []

    async def __call__(self, delay, result=None):
        self.delays.append(delay)
        return result


def _patch_sleep(monkeypatch) -> _SleepLog:
    log = _SleepLog()
    monkeypatch.setattr(asyncio, "sleep", log)
    return log


def _redis_for_recreate(xgroup_create) -> MagicMock:
    redis_client = MagicMock()
    redis_client.xinfo_groups.return_value = [{"name": "writer"}]
    redis_client.xgroup_create.side_effect = xgroup_create
    return redis_client


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "recreate_error",
    [
        None,
        redis.exceptions.ResponseError("BUSYGROUP Consumer Group name already exists"),
    ],
    ids=["created", "busygroup"],
)
async def test_nogroup_recreate_success_retries_immediately(recreate_error, monkeypatch):
    """创建成功或 BUSYGROUP（组已存在）时立即重试，不走 1 秒退避。"""
    sleeps = _patch_sleep(monkeypatch)
    creates = {"n": 0}

    def xgroup_create(*args, **kwargs):
        creates["n"] += 1
        if creates["n"] == 1:
            return None  # 启动时建组
        if creates["n"] == 2 and recreate_error is not None:
            raise recreate_error
        if creates["n"] > 2:
            raise AssertionError("成功重建后不应再次 xgroup_create")
        return None

    redis_client = _redis_for_recreate(xgroup_create)
    reads = {"n": 0}

    def xreadgroup(**kwargs):
        reads["n"] += 1
        if reads["n"] == 1:
            raise redis.exceptions.ResponseError(_NOGROUP)
        raise asyncio.CancelledError()

    redis_client.xreadgroup.side_effect = xreadgroup
    consumer = _consumer(redis_client)
    with pytest.raises(asyncio.CancelledError):
        await consumer.run_loop(["s1"])

    assert reads["n"] == 2
    assert creates["n"] == 2
    assert not any(delay >= 1 for delay in sleeps.delays)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "recreate_error",
    [
        redis.exceptions.ConnectionError("Connection closed by server"),
        redis.exceptions.TimeoutError("Timeout reading from socket"),
    ],
    ids=["connection", "timeout"],
)
async def test_nogroup_recreate_connection_error_backs_off(recreate_error, monkeypatch):
    """重建消费组时的连接/超时错误不得逃出 run_loop，并且要退避。"""
    sleeps = _patch_sleep(monkeypatch)
    creates = {"n": 0}

    def xgroup_create(*args, **kwargs):
        creates["n"] += 1
        if creates["n"] == 1:
            return None
        raise recreate_error

    redis_client = _redis_for_recreate(xgroup_create)
    reads = {"n": 0}

    def xreadgroup(**kwargs):
        reads["n"] += 1
        if reads["n"] > 3:
            raise AssertionError(
                f"xreadgroup 热循环（{reads['n']} 次，sleeps={sleeps.delays}）"
            )
        if any(delay >= 1 for delay in sleeps.delays):
            raise asyncio.CancelledError()
        raise redis.exceptions.ResponseError(_NOGROUP)

    redis_client.xreadgroup.side_effect = xreadgroup
    consumer = _consumer(redis_client)
    with pytest.raises(asyncio.CancelledError):
        await consumer.run_loop(["s1"])

    assert reads["n"] == 2
    assert 1 in sleeps.delays


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "message",
    [
        "READONLY You can't write against a read only replica.",
        "NOPERM this user has no permissions to run the 'xgroup' command",
        "ERR unknown",
    ],
    ids=["readonly", "noperm", "other"],
)
async def test_nogroup_recreate_response_error_backs_off(message, monkeypatch):
    """READONLY / 权限错误不能被吞掉后立即重试。"""
    sleeps = _patch_sleep(monkeypatch)
    creates = {"n": 0}

    def xgroup_create(*args, **kwargs):
        creates["n"] += 1
        if creates["n"] == 1:
            return None
        raise redis.exceptions.ResponseError(message)

    redis_client = _redis_for_recreate(xgroup_create)
    reads = {"n": 0}

    def xreadgroup(**kwargs):
        reads["n"] += 1
        if reads["n"] > 3:
            raise AssertionError(
                f"xreadgroup 热循环（{reads['n']} 次，sleeps={sleeps.delays}）"
            )
        if any(delay >= 1 for delay in sleeps.delays):
            raise asyncio.CancelledError()
        raise redis.exceptions.ResponseError(_NOGROUP)

    redis_client.xreadgroup.side_effect = xreadgroup
    consumer = _consumer(redis_client)
    with pytest.raises(asyncio.CancelledError):
        await consumer.run_loop(["s1"])

    assert reads["n"] == 2
    assert 1 in sleeps.delays
