from __future__ import annotations

import json

import pytest

from app.api.routes import distributed_nodes


@pytest.mark.asyncio
async def test_publish_control_invalidation(monkeypatch) -> None:
    calls = []

    class FakeRedis:
        async def publish(self, channel: str, payload: str) -> None:
            calls.append((channel, payload))

    async def get_redis():
        return FakeRedis()

    monkeypatch.setattr(distributed_nodes, "get_redis_client", get_redis)
    await distributed_nodes._publish_control_invalidation(
        {"scope": "node", "node_uuid": "node-1"}
    )

    assert calls[0][0] == "ingest:control:invalidate"
    assert json.loads(calls[0][1]) == {
        "scope": "node",
        "node_uuid": "node-1",
    }

@pytest.mark.asyncio
async def test_authenticate_uses_compat_request_path(monkeypatch) -> None:
    captured = {}

    async def authenticate(_session, **kwargs):
        captured.update(kwargs)
        return "current"

    class CompatRequestStub:
        method = "POST"
        path = "/api/distributed/edge/node-1/heartbeat"
        headers = {
            "x-datawatch-timestamp": "1",
            "x-datawatch-nonce": "nonce-1",
            "x-datawatch-signature": "signature-1",
        }

    monkeypatch.setattr(distributed_nodes, "authenticate_edge_request", authenticate)

    result = await distributed_nodes._authenticate(
        object(),
        CompatRequestStub(),
        "node-1",
        {"edgeTimeMs": 1},
    )

    assert result == "current"
    assert captured["path"] == "/api/distributed/edge/node-1/heartbeat"
