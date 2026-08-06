from __future__ import annotations

import base64
from uuid import uuid4

import pytest
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat

from ingest.app.security import (
    AuthenticationError,
    NodeSignatureAuthenticator,
    SignatureHeaders,
    canonical_json,
    signing_message,
    _load_public_key,
)


class SecurityRepository:
    def __init__(self, public_key_pem: str | list[str]) -> None:
        self.public_keys = (
            public_key_pem if isinstance(public_key_pem, list) else [public_key_pem]
        )

    async def get_approved_node_public_keys(self, node_uuid: str) -> list[str]:
        return self.public_keys


class NonceMemoryStore:
    def __init__(self) -> None:
        self.nonces: set[tuple[str, str]] = set()

    async def reserve_nonce(self, node_uuid: str, nonce: str, ttl_seconds: int) -> bool:
        key = (node_uuid, nonce)
        if key in self.nonces:
            return False
        self.nonces.add(key)
        return True


@pytest.mark.asyncio
async def test_signature_and_nonce_replay_protection() -> None:
    private_key = Ed25519PrivateKey.generate()
    public_pem = private_key.public_key().public_bytes(
        Encoding.PEM, PublicFormat.SubjectPublicKeyInfo
    ).decode("utf-8")
    node_uuid = str(uuid4())
    payload = {"node_uuid": node_uuid, "request_id": "r1", "items": []}
    timestamp = "1000"
    nonce = "nonce-1"
    signature = base64.b64encode(
        private_key.sign(
            signing_message(
                "POST", "/ingest", timestamp, nonce, canonical_json(payload)
            )
        )
    ).decode("ascii")
    authenticator = NodeSignatureAuthenticator(
        SecurityRepository(public_pem),
        NonceMemoryStore(),
        max_skew_seconds=30,
        nonce_ttl_seconds=60,
        clock=lambda: 1000,
    )
    headers = SignatureHeaders(timestamp, nonce, signature)

    await authenticator.authenticate(
        node_uuid=node_uuid,
        method="POST",
        path="/ingest",
        payload=payload,
        headers=headers,
    )
    with pytest.raises(AuthenticationError, match="nonce already used"):
        await authenticator.authenticate(
            node_uuid=node_uuid,
            method="POST",
            path="/ingest",
            payload=payload,
            headers=headers,
        )

@pytest.mark.asyncio
async def test_verify_does_not_reserve_nonce() -> None:
    private_key = Ed25519PrivateKey.generate()
    public_pem = private_key.public_key().public_bytes(
        Encoding.PEM, PublicFormat.SubjectPublicKeyInfo
    ).decode("utf-8")
    node_uuid = str(uuid4())
    payload = {"node_uuid": node_uuid, "request_id": "r1", "items": []}
    timestamp = "1000"
    nonce = "verify-only"
    signature = base64.b64encode(
        private_key.sign(
            signing_message(
                "POST", "/ingest", timestamp, nonce, canonical_json(payload)
            )
        )
    ).decode("ascii")
    store = NonceMemoryStore()
    authenticator = NodeSignatureAuthenticator(
        SecurityRepository(public_pem),
        store,
        max_skew_seconds=30,
        nonce_ttl_seconds=60,
        clock=lambda: 1000,
    )

    await authenticator.verify(
        node_uuid=node_uuid,
        method="POST",
        path="/ingest",
        payload=payload,
        headers=SignatureHeaders(timestamp, nonce, signature),
    )

    assert store.nonces == set()


@pytest.mark.asyncio
async def test_signature_accepts_previous_key_during_rotation_window() -> None:
    current = Ed25519PrivateKey.generate()
    previous = Ed25519PrivateKey.generate()
    keys = [
        key.public_key().public_bytes(
            Encoding.PEM, PublicFormat.SubjectPublicKeyInfo
        ).decode("utf-8")
        for key in (current, previous)
    ]
    node_uuid = str(uuid4())
    payload = {"node_uuid": node_uuid, "request_id": "rotation-retry", "items": []}
    timestamp = "1000"
    nonce = "rotation-nonce"
    signature = base64.b64encode(
        previous.sign(
            signing_message(
                "POST", "/ingest", timestamp, nonce, canonical_json(payload)
            )
        )
    ).decode("ascii")
    authenticator = NodeSignatureAuthenticator(
        SecurityRepository(keys),
        NonceMemoryStore(),
        max_skew_seconds=30,
        nonce_ttl_seconds=60,
        clock=lambda: 1000,
    )

    await authenticator.authenticate(
        node_uuid=node_uuid,
        method="POST",
        path="/ingest",
        payload=payload,
        headers=SignatureHeaders(timestamp, nonce, signature),
    )



def test_public_key_pem_is_parsed_once() -> None:
    private_key = Ed25519PrivateKey.generate()
    public_pem = private_key.public_key().public_bytes(
        Encoding.PEM, PublicFormat.SubjectPublicKeyInfo
    ).decode("utf-8")
    _load_public_key.cache_clear()

    first = _load_public_key(public_pem)
    second = _load_public_key(public_pem)

    assert first is second
    assert _load_public_key.cache_info().misses == 1
    assert _load_public_key.cache_info().hits == 1