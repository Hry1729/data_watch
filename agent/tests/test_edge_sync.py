from __future__ import annotations

import base64
import json

from cryptography.hazmat.primitives.serialization import load_pem_public_key

from app.services.edge_config_store import EdgeConfigStore, snapshot_checksum
from app.services.edge_sync import CenterClient, EdgeSyncService, generate_identity, signing_message


def test_center_client_signs_unicode_payload_with_identity() -> None:
    identity = generate_identity()
    captured = {}

    def transport(method, url, body, headers):
        captured.update(method=method, url=url, body=body, headers=headers)
        return 200, {"code": 200, "data": {"desiredRevision": 0}}

    client = CenterClient("http://center", identity, transport=transport)
    payload = {"lastError": "设备读取超时"}

    import asyncio

    asyncio.run(client.heartbeat(payload))
    path = f"/api/distributed/edge/{identity.node_uuid}/heartbeat"
    public_key = load_pem_public_key(identity.public_key_pem.encode("ascii"))
    public_key.verify(
        base64.b64decode(captured["headers"]["x-datawatch-signature"]),
        signing_message(
            "POST",
            path,
            captured["headers"]["x-datawatch-timestamp"],
            captured["headers"]["x-datawatch-nonce"],
            payload,
        ),
    )
    assert json.loads(captured["body"].decode("utf-8")) == payload


def test_sync_heartbeats_and_applies_desired_snapshot(tmp_path) -> None:
    identity = generate_identity()
    devices = [
        {
            "device_code": "D1",
            "device_type": "QC",
            "protocol": "s7",
            "endpoint": "10.0.0.1:102",
            "ip": "10.0.0.1",
            "port": 102,
            "rack": 0,
            "slot": 1,
            "scan_rate_ms": 400,
            "config_version": 4,
            "assignment_epoch": 1,
            "enabled": True,
            "variables": [],
        }
    ]
    cfg = {
        "edge": {
            "center_url": "http://center",
            "node_uuid": identity.node_uuid,
            "private_key_enc": identity.private_key_pem,
            "registered": True,
        }
    }
    requests = []

    def transport(method, url, body, headers):
        requests.append((method, url, body, headers))
        if url.endswith("/heartbeat"):
            return 200, {"code": 200, "data": {"desiredRevision": 4}}
        return 200, {
            "code": 200,
            "data": {
                "changed": True,
                "configRevision": 4,
                "checksum": snapshot_checksum(devices),
                "readTimeoutMs": None,
                "devices": devices,
            },
        }

    def mutate(mutator):
        mutator(cfg)
        return cfg

    service = EdgeSyncService(
        tmp_path / "edge.db",
        load_config=lambda: cfg,
        mutate_config=mutate,
        decrypt=lambda value: value,
        process_status=lambda _name: "running",
        transport=transport,
    )

    import asyncio

    result = asyncio.run(service.run_once())

    assert result == {"status": "approved", "appliedRevision": 4, "desiredRevision": 4}
    assert EdgeConfigStore(tmp_path / "edge.db").revision() == 4
    assert cfg["edge"]["registration_status"] == "approved"
    assert len(requests) == 2


def _sync_service(tmp_path, **overrides) -> EdgeSyncService:
    cfg: dict = {}
    options = {
        "load_config": lambda: cfg,
        "mutate_config": lambda mutator: mutator(cfg) or cfg,
        "decrypt": lambda value: value,
        "process_status": lambda _name: "running",
        "interval_seconds": 0.01,
    }
    options.update(overrides)
    return EdgeSyncService(tmp_path / "edge.db", **options)


def test_record_sync_failure_does_not_kill_sync_loop(tmp_path, monkeypatch) -> None:
    """_record_sync 抛错不得让 run_forever 退出。

    冻结包里缺 win32timezone 时，config 保存路径会抛 ModuleNotFoundError；异常此前
    逃逸出 while 循环，同步协程静默死亡 = 节点永久停止同步。
    """
    import asyncio

    calls: list[int] = []
    service = _sync_service(tmp_path)

    async def fake_run_once():
        calls.append(1)
        if len(calls) >= 3:
            service.stop()
        raise RuntimeError("节点未注册已登录")

    def boom(**_kwargs):
        raise ModuleNotFoundError("No module named 'win32timezone'")

    monkeypatch.setattr(service, "run_once", fake_run_once)
    monkeypatch.setattr(service, "_record_sync", boom)

    asyncio.run(service.run_forever())

    assert len(calls) == 3


def test_repeated_identical_sync_failure_is_logged_once(
    tmp_path, monkeypatch, caplog
) -> None:
    """同一条同步失败只报一次：中心未注册可能持续数天，逐次刷屏曾把日志撑到 22MB。"""
    import asyncio
    import logging

    calls: list[int] = []
    service = _sync_service(tmp_path)

    async def fake_run_once():
        calls.append(1)
        if len(calls) >= 5:
            service.stop()
        raise RuntimeError("节点未注册已登录")

    monkeypatch.setattr(service, "run_once", fake_run_once)

    with caplog.at_level(logging.WARNING, logger="app.services.edge_sync"):
        asyncio.run(service.run_forever())

    failures = [r for r in caplog.records if "Edge center sync failed" in r.getMessage()]
    assert len(calls) == 5
    assert len(failures) == 1
