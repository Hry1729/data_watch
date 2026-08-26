from __future__ import annotations

from datetime import datetime
import json

import pytest

from app.services import device_activity


class FakePipeline:
    def __init__(self, values):
        self.values = values
        self.keys = []

    def hgetall(self, key):
        self.keys.append(key)
        return self

    async def execute(self):
        return [self.values[key] for key in self.keys]


class FakeRedis:
    def __init__(self, values):
        self.values = values

    async def scan_iter(self, **_kwargs):
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
    assert int(result["QC01"].timestamp() * 1000) == 1_721_800_001_000


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


async def _async_value(value):
    return value
