from __future__ import annotations

import base64
import time

import pytest
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat

from app.services import node_signature


class _NonceRedis:
    async def set(self, *_args, **_kwargs):
        return True


@pytest.mark.asyncio
async def test_backend_accepts_same_unicode_signature_as_uploader(monkeypatch) -> None:
    private_key = Ed25519PrivateKey.generate()
    public_key_pem = private_key.public_key().public_bytes(
        Encoding.PEM,
        PublicFormat.SubjectPublicKeyInfo,
    ).decode("ascii")
    payload = {"lastError": "设备读取超时", "edgeTimeMs": 123}
    timestamp = str(int(time.time()))
    nonce = "nonce-1"
    signature = private_key.sign(
        node_signature.signing_message(
            "POST",
            "/api/distributed/edge/node/heartbeat",
            timestamp,
            nonce,
            payload,
        )
    )

    async def _public_keys(_self, _node_uuid):
        return {"current": public_key_pem}

    async def _redis():
        return _NonceRedis()

    monkeypatch.setattr(
        node_signature.DistributedNodeService,
        "public_keys_for_approved_node",
        _public_keys,
    )
    monkeypatch.setattr(node_signature, "get_redis_client", _redis)

    await node_signature.authenticate_edge_request(
        object(),
        node_uuid="3911959d-3e0d-45a4-880a-d3c7df3a2d65",
        method="POST",
        path="/api/distributed/edge/node/heartbeat",
        payload=payload,
        timestamp=timestamp,
        nonce=nonce,
        signature=base64.b64encode(signature).decode("ascii"),
    )