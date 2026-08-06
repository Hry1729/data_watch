from __future__ import annotations

import base64
import hashlib
import json
import time
from typing import Any

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives.serialization import load_pem_public_key
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError
from app.db.redis import get_redis_client
from app.services.distributed_nodes import DistributedNodeService


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
    payload: dict[str, Any],
) -> bytes:
    digest = hashlib.sha256(canonical_json(payload)).hexdigest()
    return f"{method.upper()}\n{path}\n{timestamp}\n{nonce}\n{digest}".encode("utf-8")


async def authenticate_edge_request(
    session: AsyncSession,
    *,
    node_uuid: str,
    method: str,
    path: str,
    payload: dict[str, Any],
    timestamp: str,
    nonce: str,
    signature: str,
) -> str:
    try:
        timestamp_value = int(timestamp)
    except ValueError as exc:
        raise ApiError(401, "节点签名时间戳无效", status_code=401) from exc
    if abs(int(time.time()) - timestamp_value) > 300:
        raise ApiError(401, "节点签名已过期", status_code=401)
    if not nonce or len(nonce) > 128:
        raise ApiError(401, "节点 nonce 无效", status_code=401)

    public_keys = await DistributedNodeService(
        session
    ).public_keys_for_approved_node(node_uuid)
    if not public_keys:
        raise ApiError(403, "节点未批准或已吊销", status_code=403)
    try:
        decoded_signature = base64.b64decode(signature, validate=True)
    except (ValueError, TypeError) as exc:
        raise ApiError(401, "节点签名无效", status_code=401) from exc
    message = signing_message(method, path, timestamp, nonce, payload)
    authenticated_key = ""
    for key_name, public_key_pem in public_keys.items():
        try:
            public_key = load_pem_public_key(public_key_pem.encode("utf-8"))
            public_key.verify(decoded_signature, message)
            authenticated_key = key_name
            break
        except (ValueError, TypeError, InvalidSignature):
            continue
    if not authenticated_key:
        raise ApiError(401, "节点签名无效", status_code=401)

    redis = await get_redis_client()
    nonce_key = f"edge:nonce:{node_uuid}:{nonce}"
    if not await redis.set(nonce_key, "1", ex=600, nx=True):
        raise ApiError(409, "节点请求重复", status_code=409)
    return authenticated_key
