from __future__ import annotations

import asyncio
from datetime import datetime
import json

from turboapi import APIRouter, TurboAPI, WebSocket, WebSocketDisconnect, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import require_permissions, require_permissions_from_authorization
from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.tdengine import get_tdengine_client
from app.services.monitor import MonitorService


router = APIRouter(tags=["monitor"])
auth_scheme = HTTPBearer(auto_error=False)
DEFAULT_WS_POLL_INTERVAL = 2.0


@router.get("/api/device-monitor/monitor/device-tree")
async def monitor_device_tree(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = MonitorService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device_tree()
        return success_response(data=data)


@router.get("/api/device-monitor/monitor/realtime-init")
async def monitor_realtime_init(
    deviceId: int | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = MonitorService(session=session, tdengine=get_tdengine_client())
        data = await service.get_realtime_init(device_id=deviceId)
        return success_response(data=data)


def register_realtime_websocket(app: TurboAPI) -> None:
    @app.websocket("/api/device-monitor/ws/realtime")
    async def realtime_websocket(websocket: WebSocket):
        await websocket.accept()
        # 从 query 参数或 header 获取 token 进行权限校验
        authorization = websocket.query_params.get("token") or websocket.headers.get("Authorization")
        try:
            await require_permissions_from_authorization(
                authorization,
                {"device_monitor:monitor:view", "device_monitor:realtime:view"},
            )
        except ApiError as e:
            await websocket.send_json({
                "type": "error",
                "channel": "realtime",
                "timestamp": _now_timestamp(),
                "message": e.message,
                "code": e.code,
            })
            await websocket.close()
            return

        subscribed_device_ids: set[int] = set()
        last_signatures: dict[int, str] = {}
        poll_interval = DEFAULT_WS_POLL_INTERVAL

        try:
            while True:
                try:
                    payload = await asyncio.wait_for(
                        websocket.receive_json(),
                        timeout=poll_interval,
                    )
                except asyncio.TimeoutError:
                    if not subscribed_device_ids:
                        continue
                    snapshots = await _load_realtime_snapshots(sorted(subscribed_device_ids))
                    for snapshot in snapshots:
                        device_id = int(snapshot["deviceId"])
                        signature = _build_snapshot_signature(snapshot)
                        if last_signatures.get(device_id) == signature:
                            continue
                        last_signatures[device_id] = signature
                        await websocket.send_json(
                            {
                                "type": "update",
                                "channel": "realtime",
                                "deviceId": device_id,
                                "timestamp": _now_timestamp(),
                                "data": snapshot,
                            }
                        )
                    continue
                except ValueError:
                    await websocket.send_json(
                        {
                            "type": "error",
                            "channel": "realtime",
                            "timestamp": _now_timestamp(),
                            "message": "Invalid realtime websocket payload",
                        }
                    )
                    continue

                action = str(payload.get("action", "")).strip().lower()
                if action == "ping":
                    await websocket.send_json({"type": "pong", "timestamp": _now_timestamp()})
                    continue

                if action == "config":
                    rate_ms = payload.get("refreshRate")
                    if isinstance(rate_ms, (int, float)) and rate_ms >= 100:
                        poll_interval = rate_ms / 1000.0
                    continue

                if action == "subscribe":
                    device_ids = _normalize_device_ids(payload.get("deviceIds"))
                    if not device_ids:
                        await websocket.send_json(
                            {
                                "type": "error",
                                "channel": "realtime",
                                "timestamp": _now_timestamp(),
                                "message": "deviceIds is required for subscribe",
                            }
                        )
                        continue

                    subscribed_device_ids.update(device_ids)
                    snapshots = await _load_realtime_snapshots(device_ids)
                    for snapshot in snapshots:
                        last_signatures[int(snapshot["deviceId"])] = _build_snapshot_signature(snapshot)
                    await websocket.send_json(
                        {
                            "type": "snapshot",
                            "channel": "realtime",
                            "timestamp": _now_timestamp(),
                            "devices": snapshots,
                        }
                    )
                    continue

                if action == "unsubscribe":
                    device_ids = _normalize_device_ids(payload.get("deviceIds"))
                    for device_id in device_ids:
                        subscribed_device_ids.discard(device_id)
                        last_signatures.pop(device_id, None)
                    continue

                await websocket.send_json(
                    {
                        "type": "error",
                        "channel": "realtime",
                        "timestamp": _now_timestamp(),
                        "message": "Unsupported realtime websocket action",
                    }
                )
        except WebSocketDisconnect:
            return


async def _load_realtime_snapshots(device_ids: list[int]) -> list[dict]:
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = MonitorService(session=session, tdengine=get_tdengine_client())
        return await service.get_realtime_snapshots(device_ids)


def _normalize_device_ids(raw_value) -> list[int]:
    if raw_value is None:
        return []
    if not isinstance(raw_value, list):
        raw_value = [raw_value]

    normalized: list[int] = []
    for item in raw_value:
        try:
            device_id = int(item)
        except (TypeError, ValueError):
            continue
        if device_id <= 0 or device_id in normalized:
            continue
        normalized.append(device_id)
    return normalized


def _build_snapshot_signature(snapshot: dict) -> str:
    payload = {
        "deviceId": snapshot.get("deviceId"),
        "connectStatus": snapshot.get("connectStatus"),
        "lastCollectTime": snapshot.get("lastCollectTime"),
        "data": [
            {
                "varSeq": item.get("varSeq"),
                "value": item.get("value"),
                "updateTime": item.get("updateTime"),
            }
            for item in snapshot.get("data", [])
        ],
    }
    return json.dumps(payload, ensure_ascii=False, sort_keys=True)


def _now_timestamp() -> str:
    return datetime.now().strftime("%Y-%m-%d %H:%M:%S")
