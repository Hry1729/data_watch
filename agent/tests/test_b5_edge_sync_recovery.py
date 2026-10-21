"""审查批次5：B015（配置下发联动 Sidecar）与 B016（pending key 恢复轮换）回归。"""
from __future__ import annotations

import asyncio
import base64
from types import SimpleNamespace

import pytest
from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives.serialization import load_pem_public_key

from app.routers import admin, local
from app.services.edge_sync import (
    CenterClient,
    CenterRequestError,
    EdgeSyncService,
    generate_identity,
    signing_message,
)


class _Store:
    def __init__(self, revision: int = 0):
        self.rev = revision
        self.applied = []

    def status(self):
        return {}

    def revision(self):
        return self.rev

    def refresh_schedule(self, _now):
        return None

    def apply(self, snapshot):
        self.applied.append(snapshot)
        self.rev = int(snapshot["configRevision"])
        return self.rev


def _signed_by(identity, method, url, body, headers) -> bool:
    path = url.split("://", 1)[1]
    path = path[path.index("/"):].split("?", 1)[0]
    import json

    payload = json.loads(body.decode("utf-8")) if body else {}
    public_key = load_pem_public_key(identity.public_key_pem.encode("ascii"))
    try:
        public_key.verify(
            base64.b64decode(headers["x-datawatch-signature"]),
            signing_message(
                method,
                path,
                headers["x-datawatch-timestamp"],
                headers["x-datawatch-nonce"],
                payload,
            ),
        )
        return True
    except InvalidSignature:
        return False


# ── B015 ──────────────────────────────────────────────────────────────


def _registered_cfg(identity):
    return {
        "edge": {
            "center_url": "http://center",
            "node_uuid": identity.node_uuid,
            "private_key_enc": identity.private_key_pem,
            "registered": True,
        }
    }


def _snapshot_transport(desired: int):
    def transport(method, url, body, headers):
        if url.endswith("/heartbeat"):
            return 200, {"code": 200, "data": {"desiredRevision": desired}}
        return 200, {"code": 200, "data": {"changed": True, "configRevision": desired, "devices": []}}

    return transport


def test_config_apply_triggers_sidecar_reconcile_and_retries_until_success(tmp_path):
    identity = generate_identity()
    cfg = _registered_cfg(identity)
    store = _Store()
    calls = []

    async def reconcile():
        calls.append(1)
        if len(calls) == 1:
            raise RuntimeError("proxy failed to listen")

    service = EdgeSyncService(
        tmp_path / "edge.db",
        load_config=lambda: cfg,
        mutate_config=lambda m: m(cfg) or cfg,
        decrypt=lambda v: v,
        process_status=lambda _n: "running",
        transport=_snapshot_transport(5),
        config_store=store,
        on_config_applied=reconcile,
    )
    asyncio.run(service.run_once())
    assert store.rev == 5 and len(calls) == 1
    # 运行依赖未就绪：不得报告完全收敛
    assert "sidecar reconcile failed" in cfg["edge"]["last_sync_error"]

    # 下一轮 revision 已一致，但 reconcile 待办仍须重试
    asyncio.run(service.run_once())
    assert len(calls) == 2
    assert cfg["edge"]["last_sync_error"] == ""
    asyncio.run(service.run_once())
    assert len(calls) == 2  # 成功后不再重复


def test_no_reconcile_when_snapshot_unchanged(tmp_path):
    identity = generate_identity()
    cfg = _registered_cfg(identity)
    calls = []

    async def reconcile():
        calls.append(1)

    service = EdgeSyncService(
        tmp_path / "edge.db",
        load_config=lambda: cfg,
        mutate_config=lambda m: m(cfg) or cfg,
        decrypt=lambda v: v,
        process_status=lambda _n: "running",
        transport=_snapshot_transport(0),
        config_store=_Store(),
        on_config_applied=reconcile,
    )
    asyncio.run(service.run_once())
    assert calls == []


@pytest.mark.asyncio
async def test_factory_reconcile_starts_new_proxies_while_collector_running(monkeypatch, tmp_path):
    from app import factory

    settings = tmp_path / "settings.toml"
    settings.write_text("")
    plans = []

    class SM:
        def __init__(self, had):
            self._proxies = {"A": object()} if had else {}
            self.collector_settings_path = settings

        def prepare_for_collector_start(self):
            plans.append(1)
            return SimpleNamespace(port_mapping_path=tmp_path / "port_mapping.json")

    class PM:
        runtime_dir = tmp_path

        def __init__(self, status="running"):
            self._status = status
            self.restarts = []

        def status(self, _name):
            return SimpleNamespace(status=self._status)

        async def restart(self, name, extra_args=None, env=None):
            self.restarts.append((name, extra_args))

    monkeypatch.setattr(factory, "_collector_env", lambda: {})

    # 已有代理（Collector 已带 --port-mapping）：只 reconcile，不重启
    pm = PM()
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, sidecar_manager=SM(had=True)))
    await factory._reconcile_sidecars_after_config_apply(app)
    assert plans == [1] and pm.restarts == []

    # 此前无任何 OPC 端点：须重启 Collector 让其拿到 --port-mapping
    pm = PM()
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, sidecar_manager=SM(had=False)))
    await factory._reconcile_sidecars_after_config_apply(app)
    [(name, args)] = pm.restarts
    assert name == "collector" and "--port-mapping" in args

    # Collector 未运行：不做任何事（下次启动走 prepare）
    plans.clear()
    pm = PM(status="stopped")
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, sidecar_manager=SM(had=False)))
    await factory._reconcile_sidecars_after_config_apply(app)
    assert plans == [] and pm.restarts == []


# ── B016 ──────────────────────────────────────────────────────────────


class _UploaderPM:
    modules = {"uploader": object()}

    def status(self, _name):
        return SimpleNamespace(status="stopped")


@pytest.mark.asyncio
async def test_manual_rotate_recovers_with_pending_key_after_grace(monkeypatch, tmp_path):
    old = generate_identity()
    pending = generate_identity()
    cfg = {
        "edge": {
            "node_uuid": old.node_uuid,
            "center_url": "http://center",
            "private_key_enc": old.private_key_pem,
            "pending_private_key_enc": pending.private_key_pem,
            "key_version": 1,
        }
    }
    signers = []

    async def rotate_key(self, public_key_pem):
        signers.append(self.identity.private_key_pem)
        # 中心已提交 pending 为 current，旧 key grace 已过 → 旧 key 401
        if self.identity.private_key_pem != pending.private_key_pem:
            raise CenterRequestError(401, "节点签名无效")
        assert public_key_pem == pending.public_key_pem
        return {"keyVersion": 2, "status": "already_rotated"}

    def no_new_key():
        raise AssertionError("pending 存在时不得生成第三把 key")

    for module in (local, admin):
        monkeypatch.setattr(module, "load_config", lambda: cfg)
        monkeypatch.setattr(module, "dpapi_decrypt", lambda value: value)
    monkeypatch.setattr(local, "mutate_config", lambda m: m(cfg) or cfg)
    monkeypatch.setattr(local, "dpapi_encrypt", lambda value: value)
    monkeypatch.setattr(local, "_require_edge", lambda: None)
    monkeypatch.setattr(local, "generate_identity", no_new_key)
    monkeypatch.setattr(CenterClient, "rotate_key", rotate_key)

    request = SimpleNamespace(app=SimpleNamespace(state=SimpleNamespace(pm=_UploaderPM())))
    result = await local.rotate_identity(request, _user="admin")

    assert result["status"] == "already_rotated"
    assert signers == [old.private_key_pem, pending.private_key_pem]
    assert cfg["edge"]["private_key_enc"] == pending.private_key_pem
    assert "pending_private_key_enc" not in cfg["edge"]
    assert cfg["edge"]["key_version"] == 2


@pytest.mark.asyncio
async def test_manual_rotate_still_fails_when_center_never_committed(monkeypatch):
    from fastapi import HTTPException

    old = generate_identity()
    pending = generate_identity()
    cfg = {
        "edge": {
            "node_uuid": old.node_uuid,
            "center_url": "http://center",
            "private_key_enc": old.private_key_pem,
            "pending_private_key_enc": pending.private_key_pem,
        }
    }

    async def rotate_key(self, public_key_pem):
        raise CenterRequestError(401, "节点签名无效")

    monkeypatch.setattr(local, "load_config", lambda: cfg)
    monkeypatch.setattr(local, "dpapi_decrypt", lambda value: value)
    monkeypatch.setattr(local, "mutate_config", lambda m: m(cfg) or cfg)
    monkeypatch.setattr(local, "_require_edge", lambda: None)
    monkeypatch.setattr(CenterClient, "rotate_key", rotate_key)

    request = SimpleNamespace(app=SimpleNamespace(state=SimpleNamespace(pm=_UploaderPM())))
    with pytest.raises(HTTPException) as exc:
        await local.rotate_identity(request, _user="admin")
    assert exc.value.status_code == 502
    assert cfg["edge"]["private_key_enc"] == old.private_key_pem
    assert cfg["edge"]["pending_private_key_enc"] == pending.private_key_pem


def test_background_sync_promotes_pending_key_and_restarts_uploader(tmp_path):
    old = generate_identity()
    pending = generate_identity()
    pending_id = SimpleNamespace(
        node_uuid=old.node_uuid,
        private_key_pem=pending.private_key_pem,
        public_key_pem=pending.public_key_pem,
    )
    cfg = {
        "edge": {
            "center_url": "http://center",
            "node_uuid": old.node_uuid,
            "private_key_enc": old.private_key_pem,
            "pending_private_key_enc": pending.private_key_pem,
            "registered": True,
        }
    }
    seen = []

    def transport(method, url, body, headers):
        ok = _signed_by(pending_id, method, url, body, headers)
        seen.append((url.rsplit("/", 1)[-1], ok))
        if not ok:
            return 401, {"code": 401, "msg": "节点签名无效"}
        if url.endswith("/rotate-key"):
            return 200, {"code": 200, "data": {"keyVersion": 3, "status": "already_rotated"}}
        return 200, {"code": 200, "data": {"desiredRevision": 0}}

    rotated = []

    async def on_rotated():
        rotated.append(1)

    service = EdgeSyncService(
        tmp_path / "edge.db",
        load_config=lambda: cfg,
        mutate_config=lambda m: m(cfg) or cfg,
        decrypt=lambda v: v,
        process_status=lambda _n: "running",
        transport=transport,
        config_store=_Store(),
        on_identity_rotated=on_rotated,
    )
    result = asyncio.run(service.run_once())

    assert result["status"] == "approved"
    assert seen == [("heartbeat", False), ("rotate-key", True), ("heartbeat", True)]
    assert cfg["edge"]["private_key_enc"] == pending.private_key_pem
    assert "pending_private_key_enc" not in cfg["edge"]
    assert cfg["edge"]["key_version"] == 3
    assert rotated == [1]


def test_background_sync_without_pending_still_raises(tmp_path):
    old = generate_identity()
    cfg = _registered_cfg(old)

    def transport(method, url, body, headers):
        return 401, {"code": 401, "msg": "节点签名无效"}

    service = EdgeSyncService(
        tmp_path / "edge.db",
        load_config=lambda: cfg,
        mutate_config=lambda m: m(cfg) or cfg,
        decrypt=lambda v: v,
        process_status=lambda _n: "running",
        transport=transport,
        config_store=_Store(),
    )
    with pytest.raises(CenterRequestError):
        asyncio.run(service.run_once())
    assert cfg["edge"]["private_key_enc"] == old.private_key_pem


# ── 审查 R2：Collector 重启失败后重试必须真的重启 ─────────────────────────


@pytest.mark.asyncio
async def test_factory_reconcile_retries_collector_restart_until_success(monkeypatch, tmp_path):
    """首次 restart 失败（代理已起好，_proxies 非空）后，后续每次重试都要真正 restart，
    直到成功；成功后不再重启。"""
    from app import factory

    settings = tmp_path / "settings.toml"
    settings.write_text("")

    class SM:
        def __init__(self):
            self._proxies = {}
            self.collector_settings_path = settings

        def prepare_for_collector_start(self):
            self._proxies["A"] = object()  # 真实实现：起好代理后登记
            return SimpleNamespace(port_mapping_path=tmp_path / "port_mapping.json")

    class PM:
        runtime_dir = tmp_path

        def __init__(self):
            self.restarts = []
            self.fail = 2

        def status(self, _name):
            return SimpleNamespace(status="running")

        def wants_running(self, _name):
            return True

        async def restart(self, name, extra_args=None, env=None):
            self.restarts.append((name, extra_args))
            if self.fail:
                self.fail -= 1
                raise RuntimeError("stop timed out")
            return SimpleNamespace(status="running")

    monkeypatch.setattr(factory, "_collector_env", lambda: {})
    pm = PM()
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, sidecar_manager=SM()))
    for _ in range(2):
        with pytest.raises(RuntimeError):
            await factory._reconcile_sidecars_after_config_apply(app)
    await factory._reconcile_sidecars_after_config_apply(app)
    assert len(pm.restarts) == 3
    assert all("--port-mapping" in args for _n, args in pm.restarts)
    # 成功后：代理已在、无待办 → 不再重启
    await factory._reconcile_sidecars_after_config_apply(app)
    assert len(pm.restarts) == 3


@pytest.mark.asyncio
async def test_factory_reconcile_starts_collector_left_stopped_by_failed_restart(monkeypatch, tmp_path):
    """restart 停掉了旧进程但启动失败（状态 stopped）：重试须带 --port-mapping 启动，而不是
    以“未运行”为由清掉待办。"""
    from app import factory

    settings = tmp_path / "settings.toml"
    settings.write_text("")

    class SM:
        def __init__(self):
            self._proxies = {}
            self.collector_settings_path = settings

        def prepare_for_collector_start(self):
            self._proxies["A"] = object()
            return SimpleNamespace(port_mapping_path=tmp_path / "port_mapping.json")

    class PM:
        runtime_dir = tmp_path

        def __init__(self):
            self.state = "running"
            self.calls = []

        def status(self, _name):
            return SimpleNamespace(status=self.state)

        def wants_running(self, _name):
            return True

        async def restart(self, name, extra_args=None, env=None):
            self.calls.append(("restart", extra_args))
            self.state = "stopped"
            raise RuntimeError("spawn failed")

        async def start(self, name, extra_args=None, env=None):
            self.calls.append(("start", extra_args))
            self.state = "running"
            return SimpleNamespace(status="running")

    monkeypatch.setattr(factory, "_collector_env", lambda: {})
    pm = PM()
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, sidecar_manager=SM()))
    with pytest.raises(RuntimeError):
        await factory._reconcile_sidecars_after_config_apply(app)
    await factory._reconcile_sidecars_after_config_apply(app)
    assert [c[0] for c in pm.calls] == ["restart", "start"]
    assert "--port-mapping" in pm.calls[1][1]
    assert app.state._collector_port_mapping_restart_pending is False


def test_edge_sync_keeps_reconcile_pending_until_collector_restart_succeeds(tmp_path):
    """EdgeSyncService 层：回调失败保留待办，后续心跳继续调用，直到成功。"""
    identity = generate_identity()
    cfg = _registered_cfg(identity)
    outcomes = [RuntimeError("restart failed"), RuntimeError("restart failed"), None]
    calls = []

    async def reconcile():
        calls.append(1)
        outcome = outcomes.pop(0)
        if outcome is not None:
            raise outcome

    service = EdgeSyncService(
        tmp_path / "edge.db",
        load_config=lambda: cfg,
        mutate_config=lambda m: m(cfg) or cfg,
        decrypt=lambda v: v,
        process_status=lambda _n: "running",
        transport=_snapshot_transport(1),
        config_store=_Store(),
        on_config_applied=reconcile,
    )
    for _ in range(4):
        asyncio.run(service.run_once())
    assert len(calls) == 3
    assert cfg["edge"]["last_sync_error"] == ""


# ── 审查 R2：pending key 提升成功但 Uploader 更新失败须持续重试 ─────────────


def test_uploader_update_retried_on_later_heartbeats_after_callback_failure(tmp_path):
    old = generate_identity()
    pending = generate_identity()
    pending_id = SimpleNamespace(
        node_uuid=old.node_uuid,
        private_key_pem=pending.private_key_pem,
        public_key_pem=pending.public_key_pem,
    )
    cfg = {
        "edge": {
            "center_url": "http://center",
            "node_uuid": old.node_uuid,
            "private_key_enc": old.private_key_pem,
            "pending_private_key_enc": pending.private_key_pem,
            "registered": True,
        }
    }

    def transport(method, url, body, headers):
        if not _signed_by(pending_id, method, url, body, headers):
            return 401, {"code": 401, "msg": "节点签名无效"}
        if url.endswith("/rotate-key"):
            return 200, {"code": 200, "data": {"keyVersion": 3, "status": "already_rotated"}}
        return 200, {"code": 200, "data": {"desiredRevision": 0}}

    attempts = []

    async def on_rotated():
        attempts.append(1)
        if len(attempts) <= 2:
            raise RuntimeError("cannot decrypt uploader env")

    service = EdgeSyncService(
        tmp_path / "edge.db",
        load_config=lambda: cfg,
        mutate_config=lambda m: m(cfg) or cfg,
        decrypt=lambda v: v,
        process_status=lambda _n: "running",
        transport=transport,
        config_store=_Store(),
        on_identity_rotated=on_rotated,
    )
    asyncio.run(service.run_once())
    assert cfg["edge"]["private_key_enc"] == pending.private_key_pem
    assert cfg["edge"].get("identity_propagation_pending") is True
    assert "uploader identity update failed" in cfg["edge"]["last_sync_error"]
    asyncio.run(service.run_once())  # 第二次仍失败，标记保留
    assert cfg["edge"].get("identity_propagation_pending") is True
    asyncio.run(service.run_once())  # 第三次成功
    assert len(attempts) == 3
    assert "identity_propagation_pending" not in cfg["edge"]
    assert cfg["edge"]["last_sync_error"] == ""
    asyncio.run(service.run_once())
    assert len(attempts) == 3
