from __future__ import annotations

import base64
import hashlib
import json
import time
from dataclasses import dataclass
from functools import lru_cache
from typing import Any, Protocol

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives.serialization import load_pem_public_key


class AuthenticationError(ValueError):
    pass


class NonceStore(Protocol):
    async def reserve_nonce(self, node_uuid: str, nonce: str, ttl_seconds: int) -> bool: ...


class NodeKeyRepository(Protocol):
    async def get_approved_node_public_keys(self, node_uuid: str) -> list[str]: ...


@dataclass(frozen=True, slots=True)
class SignatureHeaders:
    timestamp: str
    nonce: str
    signature: str


@lru_cache(maxsize=2048)
def _load_public_key(public_key_pem: str):
    return load_pem_public_key(public_key_pem.encode("utf-8"))


def canonical_json(payload: dict[str, Any]) -> bytes:
    return json.dumps(
        payload,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")


def signing_message(
    method: str,
    path: str,
    timestamp: str,
    nonce: str,
    body: bytes,
) -> bytes:
    body_hash = hashlib.sha256(body).hexdigest()
    return "\n".join((method.upper(), path, timestamp, nonce, body_hash)).encode("utf-8")


class NodeSignatureAuthenticator:
    def __init__(
        self,
        repository: NodeKeyRepository,
        nonce_store: NonceStore,
        *,
        max_skew_seconds: int,
        nonce_ttl_seconds: int,
        clock=time.time,
    ) -> None:
        self._repository = repository
        self._nonce_store = nonce_store
        self._max_skew_seconds = max_skew_seconds
        self._nonce_ttl_seconds = nonce_ttl_seconds
        self._clock = clock

    async def verify(
        self,
        *,
        node_uuid: str,
        method: str,
        path: str,
        payload: dict[str, Any],
        headers: SignatureHeaders,
    ) -> None:
        try:
            timestamp = int(headers.timestamp)
        except (TypeError, ValueError) as exc:
            raise AuthenticationError("invalid signature timestamp") from exc
        if abs(int(self._clock()) - timestamp) > self._max_skew_seconds:
            raise AuthenticationError("signature timestamp outside allowed skew")
        if not headers.nonce or len(headers.nonce) > 128:
            raise AuthenticationError("invalid nonce")

        public_keys = await self._repository.get_approved_node_public_keys(node_uuid)
        if not public_keys:
            raise AuthenticationError("node is not approved")

        try:
            signature = base64.b64decode(headers.signature, validate=True)
        except (ValueError, TypeError) as exc:
            raise AuthenticationError("invalid request signature") from exc
        message = signing_message(
            method,
            path,
            headers.timestamp,
            headers.nonce,
            canonical_json(payload),
        )
        verified = False
        for public_key_pem in public_keys:
            try:
                public_key = _load_public_key(public_key_pem)
                public_key.verify(signature, message)
                verified = True
                break
            except (ValueError, TypeError, InvalidSignature):
                continue
        if not verified:
            raise AuthenticationError("invalid request signature")

    async def reserve_nonce(self, node_uuid: str, nonce: str) -> None:
        if not await self._nonce_store.reserve_nonce(
            node_uuid, nonce, self._nonce_ttl_seconds
        ):
            raise AuthenticationError("nonce already used")

    async def authenticate(
        self,
        *,
        node_uuid: str,
        method: str,
        path: str,
        payload: dict[str, Any],
        headers: SignatureHeaders,
    ) -> None:
        await self.verify(
            node_uuid=node_uuid,
            method=method,
            path=path,
            payload=payload,
            headers=headers,
        )
        await self.reserve_nonce(node_uuid, headers.nonce)
