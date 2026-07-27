"""Writer group 单一配置来源与 module_monitor 指标测试。"""

from __future__ import annotations

import sys
from unittest.mock import MagicMock

import pytest

# module_monitor 依赖 sqlalchemy；本测只覆盖 Redis 指标逻辑，先桩住再导入
sys.modules.setdefault("sqlalchemy", MagicMock())
sys.modules.setdefault("sqlalchemy.ext", MagicMock())
sys.modules.setdefault("sqlalchemy.ext.asyncio", MagicMock())

from app.core.writer_group import get_writer_group_name  # noqa: E402
from app.services.module_monitor import ModuleMonitorService  # noqa: E402
from app.services import rate_sampler as rate_sampler_mod  # noqa: E402


def test_writer_group_name_matches_settings(monkeypatch):
    monkeypatch.delenv("WRITER_REDIS_GROUP_NAME", raising=False)
    from app.core import config as config_mod
    from app.core import writer_group as wg

    wg.get_writer_group_name.cache_clear()
    config_mod.get_settings.cache_clear()
    expected = config_mod.get_settings().writer_redis_group_name
    assert get_writer_group_name() == expected
    # rate_sampler 在导入时缓存了组名；与当前解析结果对齐即可
    assert rate_sampler_mod.WRITER_GROUP_NAME in {expected, get_writer_group_name()}


@pytest.mark.asyncio
async def test_writer_metrics_counts_only_writer_group(monkeypatch):
    """主指标只计 writer 组；其它 group 进 otherGroups；lag=None 保留。"""
    writer_name = get_writer_group_name()

    class _FakeRedis:
        async def scan_iter(self, match="", count=100):
            if match.startswith("rt:stream:"):
                yield "rt:stream:0"
            return
            yield  # pragma: no cover

        async def xinfo_groups(self, key):
            return [
                {"name": writer_name, "lag": None, "pending": 2},
                {"name": "other_cg", "lag": 9999, "pending": 50},
            ]

        async def xlen(self, key):
            return 10

        async def llen(self, key):
            return 0

    async def _get_redis():
        return _FakeRedis()

    monkeypatch.setattr(
        "app.services.module_monitor.get_redis_client", _get_redis
    )

    svc = ModuleMonitorService(session=MagicMock())
    metrics = await svc.get_writer_metrics()

    assert metrics["writerGroupName"] == writer_name
    assert metrics["streamLagTotal"] == 0  # lag=None 不计入合计
    assert metrics["pelPendingTotal"] == 2
    assert len(metrics["streams"]) == 1
    stream = metrics["streams"][0]
    assert stream["lag"] is None
    assert stream["pel"] == 2
    assert stream["otherGroups"] == [
        {"name": "other_cg", "lag": 9999, "pending": 50}
    ]


@pytest.mark.asyncio
async def test_writer_metrics_missing_writer_group(monkeypatch):
    class _FakeRedis:
        async def scan_iter(self, match="", count=100):
            if match.startswith("rt:stream:"):
                yield "rt:stream:0"
            return
            yield  # pragma: no cover

        async def xinfo_groups(self, key):
            return [{"name": "someone_else", "lag": 5, "pending": 1}]

        async def xlen(self, key):
            return 3

        async def llen(self, key):
            return 0

    async def _get_redis():
        return _FakeRedis()

    monkeypatch.setattr(
        "app.services.module_monitor.get_redis_client", _get_redis
    )

    svc = ModuleMonitorService(session=MagicMock())
    metrics = await svc.get_writer_metrics()
    stream = metrics["streams"][0]
    assert stream["lag"] is None
    assert stream["pel"] == 0
    assert stream["otherGroups"][0]["name"] == "someone_else"
