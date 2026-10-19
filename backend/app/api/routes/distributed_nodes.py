from __future__ import annotations

import json
import logging
from typing import Any

from turboapi import APIRouter, Depends, Request
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import require_permissions
from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.redis import get_redis_client
from app.services.distributed_nodes import DistributedNodeService
from app.services.node_signature import authenticate_edge_request


router = APIRouter(tags=["distributed-nodes"])
auth_scheme = HTTPBearer(auto_error=False)
_ADMIN_ROLES = {"R_SUPER", "R_ADMIN"}
_CONTROL_INVALIDATION_CHANNEL = "ingest:control:invalidate"
logger = logging.getLogger(__name__)


async def _admin(
    credentials: HTTPAuthorizationCredentials | None,
) -> dict[str, Any]:
    principal = await require_permissions(credentials, [])
    if not _ADMIN_ROLES.intersection(principal.get("roles") or []):
        raise ApiError(403, "仅管理员可管理边缘节点")
    return principal


@router.post("/api/distributed/edge/register")
async def register_edge(payload: dict[str, Any]):
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).register_node(payload)
        return success_response(data=data)


@router.post("/api/distributed/edge/{node_uuid}/heartbeat")
async def edge_heartbeat(
    node_uuid: str,
    payload: dict[str, Any],
    request: Request,
):
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        await _authenticate(session, request, node_uuid, payload)
        data = await DistributedNodeService(session).heartbeat(node_uuid, payload)
        return success_response(data=data)


@router.post("/api/distributed/edge/{node_uuid}/rotate-key")
async def edge_rotate_key(
    node_uuid: str,
    payload: dict[str, Any],
    request: Request,
):
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        authenticated_key = await _authenticate(session, request, node_uuid, payload)
        data = await DistributedNodeService(session).rotate_public_key(
            node_uuid,
            payload.get("newPublicKeyPem"),
            authenticated_key,
        )
        await _publish_control_invalidation(
            {"scope": "node", "node_uuid": node_uuid}
        )
        return success_response(data=data)


@router.get("/api/distributed/edge/{node_uuid}/config")
async def edge_config(
    node_uuid: str,
    request: Request,
    revision: int | None = None,
):
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        await _authenticate(session, request, node_uuid, {})
        data = await DistributedNodeService(session).config_snapshot(
            node_uuid,
            revision,
        )
        return success_response(data=data)


@router.get("/api/distributed/nodes")
async def list_nodes(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _admin(credentials)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).list_nodes()
        return success_response(data={"items": data})


@router.get("/api/distributed/assignments/occupancy")
async def list_assignment_occupancy(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _admin(credentials)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).list_assignment_occupancy()
        return success_response(data={"items": data})


@router.post("/api/distributed/nodes/sync-device-status")
async def sync_device_statuses(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _admin(credentials)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).sync_device_statuses()
        await _publish_control_invalidation({"scope": "assignments"})
        return success_response(data=data)


@router.post("/api/distributed/nodes/registration-codes")
async def create_registration_code(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _admin(credentials)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).create_registration_code(
            node_name=str(payload.get("nodeName") or ""),
            actor=str(principal["user_name"]),
            expires_minutes=payload.get("expiresMinutes", 30),
        )
        return success_response(data=data)


@router.patch("/api/distributed/nodes/{node_uuid}/status")
async def update_node_status(
    node_uuid: str,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _admin(credentials)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).set_node_status(
            node_uuid,
            str(payload.get("status") or ""),
        )
        await _publish_control_invalidation(
            {"scope": "node", "node_uuid": node_uuid, "assignments": True}
        )
        return success_response(data=data)


@router.patch("/api/distributed/nodes/{node_uuid}/settings")
async def update_node_settings(
    node_uuid: str,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _admin(credentials)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).update_node_settings(
            node_uuid,
            payload,
        )
        return success_response(data=data)


@router.put("/api/distributed/nodes/{node_uuid}/assignments")
async def assign_devices(
    node_uuid: str,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _admin(credentials)
    # M4：addDeviceIds / removeDeviceIds 为增量操作，由后端在节点行锁内合并当前分配；
    # deviceIds 仍为整体替换。
    mode = "set"
    device_ids = payload.get("deviceIds")
    for key, key_mode in (("addDeviceIds", "add"), ("removeDeviceIds", "remove")):
        if key in payload:
            if device_ids is not None or mode != "set":
                raise ApiError(400, "deviceIds、addDeviceIds、removeDeviceIds 只能三选一")
            mode = key_mode
            device_ids = payload.get(key)
    if not isinstance(device_ids, list):
        raise ApiError(400, "deviceIds 必须是数组")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).assign_devices(
            node_uuid,
            device_ids,
            mode=mode,
        )
        await _publish_control_invalidation({"scope": "assignments"})
        return success_response(data=data)


@router.get("/api/distributed/nodes/{node_uuid}/assignments")
async def list_assignments(
    node_uuid: str,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _admin(credentials)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await DistributedNodeService(session).list_assignments(node_uuid)
        return success_response(data={"items": data})


async def _authenticate(
    session,
    request: Request,
    node_uuid: str,
    payload: dict[str, Any],
) -> str:
    return await authenticate_edge_request(
        session,
        node_uuid=node_uuid,
        method=str(request.method),
        path=str(request.path),
        payload=payload,
        timestamp=request.headers.get("x-datawatch-timestamp", ""),
        nonce=request.headers.get("x-datawatch-nonce", ""),
        signature=request.headers.get("x-datawatch-signature", ""),
    )


async def _publish_control_invalidation(payload: dict[str, Any]) -> None:
    try:
        redis = await get_redis_client()
        await redis.publish(
            _CONTROL_INVALIDATION_CHANNEL,
            json.dumps(payload, ensure_ascii=True, separators=(",", ":")),
        )
    except Exception:
        logger.exception("failed to publish Ingest control cache invalidation")