"""B12 回归：边缘角色不得保存中心 Redis/TDengine 凭据。

取证：边缘运行时从不读取中心 Redis/TDengine（边缘本地 SQLite → Uploader → Ingest HTTP），
而 ``PUT /bootstrap/config`` 被中心式与边缘两个引导页共用。因此只对 edge 拒绝这两类凭据，
中心式/分布式中心的引导流程必须不受影响。
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest
from fastapi import HTTPException

AGENT_ROOT = Path(__file__).resolve().parents[1]
if str(AGENT_ROOT) not in sys.path:
    sys.path.insert(0, str(AGENT_ROOT))

from app.routers import bootstrap  # noqa: E402
from app.schemas.models import BootstrapConfigRequest  # noqa: E402


@pytest.fixture()
def captured(monkeypatch):
    """让引导入口在"未完成引导"状态下运行，并捕获写回的配置。"""
    saved: dict = {}

    monkeypatch.setattr(bootstrap, "load_config", lambda: {"agent": {"bootstrap_done": False}})
    monkeypatch.setattr(bootstrap, "save_config", lambda cfg: saved.update(cfg))

    def _role(role: str):
        monkeypatch.setattr(bootstrap, "build_role", lambda: role)

    return saved, _role


def _request(**sections) -> BootstrapConfigRequest:
    return BootstrapConfigRequest(**sections)


@pytest.mark.asyncio
async def test_edge_rejects_center_redis_and_tdengine(captured) -> None:
    saved, set_role = captured
    set_role("edge")

    for section in ("redis", "tdengine"):
        with pytest.raises(HTTPException) as excinfo:
            await bootstrap.bootstrap_config(
                _request(**{section: {"host": "10.0.0.9", "port": 6379, "password": "SENTINEL"}})
            )
        assert excinfo.value.status_code == 400
        assert "边缘角色不使用中心 Redis/TDengine" in str(excinfo.value.detail)
    # 拒绝时不得写入任何内容
    assert saved == {}


@pytest.mark.asyncio
async def test_edge_accepts_mysql_only(captured) -> None:
    saved, set_role = captured
    set_role("edge")

    await bootstrap.bootstrap_config(
        _request(mysql={"host": "10.0.0.9", "port": 3306, "user": "root", "database": "device_watch"})
    )
    assert "mysql" in saved
    assert "redis" not in saved
    assert "tdengine" not in saved


@pytest.mark.asyncio
async def test_centralized_role_still_accepts_redis_and_tdengine(captured) -> None:
    """中心式角色的引导流程必须不受影响。"""
    saved, set_role = captured
    set_role("centralized")

    await bootstrap.bootstrap_config(
        _request(
            redis={"host": "10.0.0.9", "port": 6379, "password": "pw"},
            tdengine={"host": "10.0.0.9", "port": 6041, "user": "root", "database": "device_data"},
        )
    )
    assert "redis" in saved
    assert "tdengine" in saved
