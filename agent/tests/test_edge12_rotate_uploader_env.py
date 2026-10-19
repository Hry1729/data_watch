"""Edge 12：轮换密钥后重启 Uploader 必须用统一的 _uploader_env（含自定义 Storage URL），
并持有与 admin 相同的生命周期锁。"""
from __future__ import annotations

import base64
from types import SimpleNamespace

import pytest

from app.routers import admin, local
from app.services.edge_sync import generate_identity


class FakePM:
    def __init__(self):
        self.modules = {"uploader": object()}
        self.restarts = []

    def status(self, name):
        return SimpleNamespace(status="running")

    async def restart(self, name, extra_args=None, env=None):
        lock = getattr(self, "_collector_lifecycle_lock", None)
        self.restarts.append((name, env, lock is not None and lock.locked()))


@pytest.mark.asyncio
async def test_rotate_restarts_uploader_with_unified_env(monkeypatch, tmp_path):
    current = generate_identity()
    cfg = {
        "edge": {
            "node_uuid": current.node_uuid,
            "center_url": "http://center",
            "private_key_enc": current.private_key_pem,
        },
        "storage": {"host": "127.0.0.1", "port": 7999},
    }

    def mutate(mutator):
        mutator(cfg)
        return cfg

    async def rotate_key(self, public_key_pem):
        return {"keyVersion": 2}

    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    for module in (local, admin):
        monkeypatch.setattr(module, "load_config", lambda: cfg)
        monkeypatch.setattr(module, "dpapi_decrypt", lambda value: value)
    monkeypatch.setattr(local, "mutate_config", mutate)
    monkeypatch.setattr(local, "dpapi_encrypt", lambda value: value)
    monkeypatch.setattr(local, "_require_edge", lambda: None)
    monkeypatch.setattr(local.CenterClient, "rotate_key", rotate_key)

    pm = FakePM()
    request = SimpleNamespace(app=SimpleNamespace(state=SimpleNamespace(pm=pm)))
    result = await local.rotate_identity(request, _user="admin")

    assert result["uploaderRestarted"] is True
    [(name, env, locked)] = pm.restarts
    assert name == "uploader"
    assert locked is True
    assert env["DATAWATCH_EDGE_STORAGE_URL"] == "http://127.0.0.1:7999"
    new_key = base64.b64decode(env["DATAWATCH_UPLOADER_PRIVATE_KEY_B64"]).decode("utf-8")
    assert new_key == cfg["edge"]["private_key_enc"]
    assert new_key != current.private_key_pem
