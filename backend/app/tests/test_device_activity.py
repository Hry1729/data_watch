from __future__ import annotations

import asyncio
from datetime import datetime
import json
from zoneinfo import ZoneInfo

import pytest
import pytest_asyncio

from app.core import request_metrics
from app.services import device_activity


@pytest_asyncio.fixture(autouse=True)
async def reset_activity_cache():
    device_activity._cache.clear()
    device_activity._inflight.clear()
    device_activity._last_failed.clear()
    device_activity._retry_after.clear()
    device_activity._key_cache = None
    device_activity._key_inflight = None
    yield
    tasks = list(device_activity._inflight.values())
    if device_activity._key_inflight is not None:
        tasks.append(device_activity._key_inflight)
    for task in tasks:
        task.cancel()
    await asyncio.gather(*tasks, return_exceptions=True)


class FakePipeline:
    def __init__(self, values):
        self.values = values
        self.keys = []

    def hgetall(self, key):
        self.keys.append(key)
        return self

    async def execute(self):
        return [self.values[key] if key in self.values else self.values[key.encode()] for key in self.keys]


class FakeRedis:
    def __init__(self, values):
        self.values = values
        self.scan_calls = 0

    async def scan_iter(self, **_kwargs):
        self.scan_calls += 1
        for key in self.values:
            yield key

    def pipeline(self, *, transaction):
        assert transaction is False
        return FakePipeline(self.values)


@pytest.mark.asyncio
async def test_fetch_latest_collect_times_uses_all_point_groups(monkeypatch):
    values = {
        "rt:latest:QC01:g001": {
            "1": json.dumps({"v": 1, "t": 1_721_800_000_000, "q": 0}),
        },
        "rt:latest:QC01:g002": {
            "2": json.dumps({"v": 2, "t": 1_721_800_001_000, "q": 0}),
        },
        "rt:latest:QC02:g001": {
            "3": json.dumps({"v": 3, "t": 1_721_800_002_000, "q": 0}),
        },
    }
    monkeypatch.setattr(
        device_activity,
        "get_redis_client",
        lambda: _async_value(FakeRedis(values)),
    )

    result, connected = await device_activity.fetch_latest_collect_times(["QC01"])

    assert connected is True
    assert list(result) == ["QC01"]
    assert isinstance(result["QC01"], datetime)
    # parse_local_datetime 返回 Asia/Shanghai 墙钟的 naive datetime。
    # 直接 .timestamp() 会按机器本地时区折算，UTC 环境下会偏 8 小时。
    expected = datetime.fromtimestamp(
        1_721_800_001,
        tz=ZoneInfo("Asia/Shanghai"),
    ).replace(tzinfo=None)
    assert result["QC01"] == expected


@pytest.mark.asyncio
async def test_fetch_latest_collect_times_matches_device_case_and_string_timestamp(monkeypatch):
    values = {
        "rt:latest:qc100:g001": {
            "1": json.dumps({"v": 1, "t": "1785981600000", "q": 0}),
        },
    }
    monkeypatch.setattr(
        device_activity,
        "get_redis_client",
        lambda: _async_value(FakeRedis(values)),
    )

    result, connected = await device_activity.fetch_latest_collect_times(["QC100"])

    assert connected is True
    assert list(result) == ["QC100"]
    assert result["QC100"] == datetime(2026, 8, 6, 10, 0, 0)


@pytest.mark.asyncio
async def test_concurrent_calls_share_one_scan_and_reuse_fresh_result(monkeypatch):
    redis = FakeRedis({
        "rt:latest:qc200:g001": {
            "1": json.dumps({"t": 1_721_800_000_000}),
        },
    })
    monkeypatch.setattr(device_activity, "get_redis_client", lambda: _async_value(redis))

    first, second = await asyncio.gather(
        device_activity.fetch_latest_collect_times(["QC200"]),
        device_activity.fetch_latest_collect_times(["qc200"]),
    )
    await asyncio.sleep(0)
    third = await device_activity.fetch_latest_collect_times(["QC200"])

    assert redis.scan_calls == 1
    assert first[0]["QC200"] == second[0]["qc200"] == third[0]["QC200"]
    assert first[1] is second[1] is third[1] is True


@pytest.mark.asyncio
async def test_expired_result_is_refetched(monkeypatch):
    redis = FakeRedis({})
    clock = [100.0]
    monkeypatch.setattr(device_activity, "get_redis_client", lambda: _async_value(redis))
    monkeypatch.setattr(device_activity, "monotonic", lambda: clock[0])

    assert await device_activity.fetch_latest_collect_times(["QC201"]) == ({}, True)
    await asyncio.sleep(0)
    clock[0] += 3.0
    assert await device_activity.fetch_latest_collect_times(["QC201"]) == ({}, True)
    await asyncio.sleep(0.02)

    assert redis.scan_calls == 2


@pytest.mark.asyncio
async def test_redis_failure_is_not_cached(monkeypatch):
    class RecoveringRedis(FakeRedis):
        async def scan_iter(self, **kwargs):
            self.scan_calls += 1
            if self.scan_calls == 1:
                raise ConnectionError("unavailable")
            async for key in super().scan_iter(**kwargs):
                yield key

    redis = RecoveringRedis({})
    monkeypatch.setattr(device_activity, "get_redis_client", lambda: _async_value(redis))

    assert await device_activity.fetch_latest_collect_times(["QC202"]) == ({}, False)
    await asyncio.sleep(0)
    assert await device_activity.fetch_latest_collect_times(["QC202"]) == ({}, True)


@pytest.mark.asyncio
async def test_redis_read_timeout_reports_disconnected(monkeypatch):
    class StalledRedis(FakeRedis):
        async def scan_iter(self, **_kwargs):
            await asyncio.Event().wait()
            yield "never"

    monkeypatch.setattr(device_activity, "get_redis_client", lambda: _async_value(StalledRedis({})))
    monkeypatch.setattr(device_activity, "_REDIS_READ_TIMEOUT_SECONDS", 0.01)

    assert await device_activity.fetch_latest_collect_times(["QC203"]) == ({}, False)


@pytest.mark.asyncio
async def test_stale_result_does_not_extend_after_refresh_failure(monkeypatch):
    class FailAfterFirstRedis(FakeRedis):
        async def scan_iter(self, **_kwargs):
            self.scan_calls += 1
            if self.scan_calls > 1:
                raise ConnectionError("unavailable")
            for key in self.values:
                yield key

    redis = FailAfterFirstRedis({
        "rt:latest:QC204:g001": {"1": json.dumps({"t": 1_721_800_000_000})},
    })
    clock = [100.0]
    monkeypatch.setattr(device_activity, "get_redis_client", lambda: _async_value(redis))
    monkeypatch.setattr(device_activity, "monotonic", lambda: clock[0])

    first, connected = await device_activity.fetch_latest_collect_times(["QC204"])
    assert connected is True
    await asyncio.sleep(0)
    clock[0] = 103.0
    stale, connected = await device_activity.fetch_latest_collect_times(["QC204"])
    assert stale == first and connected is True
    await asyncio.sleep(0.02)
    stale, connected = await device_activity.fetch_latest_collect_times(["QC204"])
    assert stale == first and connected is False

    clock[0] = 131.0
    assert await device_activity.fetch_latest_collect_times(["QC204"]) == ({}, False)


@pytest.mark.asyncio
async def test_background_refresh_does_not_inherit_request_metrics(monkeypatch):
    redis = FakeRedis({})
    clock = [100.0]
    seen_contexts = []

    async def get_client():
        seen_contexts.append(request_metrics._current.get())
        return redis

    monkeypatch.setattr(device_activity, "get_redis_client", get_client)
    monkeypatch.setattr(device_activity, "monotonic", lambda: clock[0])
    metrics = request_metrics.RequestMetrics()
    token = request_metrics._current.set(metrics)
    try:
        await device_activity.fetch_latest_collect_times(["QC205"])
        await asyncio.sleep(0)
        clock[0] = 103.0
        await device_activity.fetch_latest_collect_times(["QC205"])
        await asyncio.sleep(0.02)
    finally:
        request_metrics._current.reset(token)

    assert seen_contexts == [metrics, None]


@pytest.mark.asyncio
async def test_different_device_requests_share_key_discovery(monkeypatch):
    redis = FakeRedis({
        "rt:latest:QC300:g001": {"1": json.dumps({"t": 1_721_800_000_000})},
        "rt:latest:qc301:g002": {"2": json.dumps({"t": 1_721_800_001_000})},
    })
    monkeypatch.setattr(device_activity, "get_redis_client", lambda: _async_value(redis))

    first, second = await asyncio.gather(
        device_activity.fetch_latest_collect_times(["QC300"]),
        device_activity.fetch_latest_collect_times(["QC301"]),
    )
    assert redis.scan_calls == 1
    assert list(first[0]) == ["QC300"]
    assert list(second[0]) == ["QC301"]


@pytest.mark.asyncio
async def test_key_discovery_refresh_finds_new_device_and_point_group(monkeypatch):
    redis = FakeRedis({
        b"rt:latest:qc302:g001": {"1": json.dumps({"t": 1_721_800_000_000})},
    })
    clock = [100.0]
    monkeypatch.setattr(device_activity, "get_redis_client", lambda: _async_value(redis))
    monkeypatch.setattr(device_activity, "monotonic", lambda: clock[0])
    first, connected = await device_activity.fetch_latest_collect_times(["QC302"])
    assert connected
    redis.values["rt:latest:QC302:g002"] = {"2": json.dumps({"t": 1_721_800_001_000})}
    redis.values["rt:latest:qc303:g003"] = {"3": json.dumps({"t": 1_721_800_002_000})}
    assert await device_activity.fetch_latest_collect_times(["QC303"]) == ({}, True)

    clock[0] += 3.0
    refreshed, connected = await device_activity.fetch_latest_collect_times(["QC302", "QC303"])
    assert connected
    assert redis.scan_calls == 2
    assert refreshed["QC302"] > first["QC302"]
    assert refreshed["QC303"] > refreshed["QC302"]


@pytest.mark.asyncio
async def test_oversized_key_discovery_does_not_drop_devices(monkeypatch):
    redis = FakeRedis({
        "rt:latest:QC304:g001": {"1": json.dumps({"t": 1_721_800_000_000})},
        "rt:latest:QC305:g002": {"2": json.dumps({"t": 1_721_800_001_000})},
    })
    monkeypatch.setattr(device_activity, "get_redis_client", lambda: _async_value(redis))
    monkeypatch.setattr(device_activity, "_KEY_CACHE_MAX_KEYS", 1)

    result, connected = await device_activity.fetch_latest_collect_times(["QC305"])
    assert connected
    assert list(result) == ["QC305"]
    assert redis.scan_calls == 2
    assert device_activity._key_cache[1] is None


async def _async_value(value):
    return value
