"""A方-10：check_protocols center/sidecar 独立去重，同 endpoint 两类协议不互相屏蔽。"""

import pytest

import check_protocols as cp


class _FakeSession:
    def __init__(self, rows):
        self._rows = rows

    async def __aenter__(self):
        return self

    async def __aexit__(self, *exc):
        return False

    async def execute(self, _stmt):
        return self._rows


class _FakeEngine:
    async def dispose(self):
        pass


def _patch_db(monkeypatch, rows):
    monkeypatch.setattr(
        "sqlalchemy.ext.asyncio.create_async_engine", lambda *a, **k: _FakeEngine()
    )
    monkeypatch.setattr(
        "sqlalchemy.ext.asyncio.async_sessionmaker",
        lambda *a, **k: (lambda: _FakeSession(rows)),
    )
    monkeypatch.setattr("sqlalchemy.text", lambda q: q)


_MYSQL = {"user": "u", "password": "p", "host": "h", "port": 3306, "database": "d"}


@pytest.mark.asyncio
async def test_same_endpoint_center_and_sidecar_both_recorded(monkeypatch):
    """同一 endpoint 既配 center 又配 sidecar → 两类都被收录（修复前 sidecar 被漏）。"""
    rows = [
        ("DEV_C", "172.27.18.221", "opcda_center"),
        ("DEV_S", "172.27.18.221", "opcda_sidecar"),
    ]
    _patch_db(monkeypatch, rows)

    result = await cp.check_protocols(_MYSQL)

    assert "172.27.18.221" in result["center_collection"]
    assert "172.27.18.221" in result["sidecar_push"]
    assert result["has_center"] is True
    assert result["has_sidecar"] is True


@pytest.mark.asyncio
async def test_duplicate_endpoint_within_same_class_still_deduped(monkeypatch):
    """同类内重复 endpoint 仍去重：两条 center 同 endpoint 只收一次、只分一个端口。"""
    rows = [
        ("DEV_C1", "172.27.18.221", "opcda_center"),
        ("DEV_C2", "172.27.18.221", "opcda_center"),
    ]
    _patch_db(monkeypatch, rows)

    result = await cp.check_protocols(_MYSQL)

    assert result["center_collection"] == ["172.27.18.221"]
    assert list(result["port_mapping"].keys()) == ["172.27.18.221"]
