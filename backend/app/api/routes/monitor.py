from __future__ import annotations

from turboapi import APIRouter, TurboAPI, WebSocket, WebSocketDisconnect, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import require_permissions, require_permissions_from_authorization
from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.tdengine import get_tdengine_client
from app.services.monitor import MonitorService
from app.services.ws_push import get_ws_manager


router = APIRouter(tags=["monitor"])
auth_scheme = HTTPBearer(auto_error=False)


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
    groupCode: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = MonitorService(session=session, tdengine=get_tdengine_client())
        data = await service.get_realtime_init(device_id=deviceId, group_code=groupCode or "g001")
        return success_response(data=data)


@router.get("/api/device-monitor/monitor/realtime-window")
async def monitor_realtime_window(
    deviceId: int,
    groupCode: str | None = None,
    scanRateMs: int | None = None,
    limit: int | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = MonitorService(session=session, tdengine=get_tdengine_client())
        data = await service.get_realtime_window(
            device_id=deviceId,
            group_code=groupCode or "g001",
            scan_rate_ms=scanRateMs,
            limit=limit if limit is not None else 10,
        )
        return success_response(data=data)


@router.get("/api/device-monitor/realtime-window")
async def monitor_realtime_window_by_code(
    device_code: str,
    group_code: str | None = None,
    scan_rate_ms: int | None = None,
    limit: int | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = MonitorService(session=session, tdengine=get_tdengine_client())
        data = await service.get_realtime_window(
            device_code=device_code,
            group_code=group_code or "g001",
            scan_rate_ms=scan_rate_ms,
            limit=limit if limit is not None else 10,
        )
        return success_response(data=data)


def register_ws_push_websocket(app: TurboAPI) -> None:
    @app.websocket("/api/device-monitor/ws/realtime-push")
    async def ws_realtime_push(websocket: WebSocket):
        await websocket.accept()
        authorization = websocket.query_params.get("token") or websocket.headers.get("Authorization")
        try:
            await require_permissions_from_authorization(
                authorization,
                {"device_monitor:monitor:view", "device_monitor:realtime:view"},
            )
        except ApiError as e:
            await websocket.send_json({
                "type": "error",
                "message": e.message,
                "code": e.code,
            })
            await websocket.close()
            return

        manager = get_ws_manager()
        try:
            while True:
                msg = await websocket.receive_json()
                action = msg.get("action")
                device_code = str(msg.get("device_code", "")).strip()
                group_code = str(msg.get("group_code", "g001")).strip() or "g001"
                if action == "subscribe" and device_code:
                    await manager.subscribe(websocket, device_code, group_code)
                elif action == "unsubscribe" and device_code:
                    await manager.unsubscribe(websocket, device_code, group_code)
                elif action == "ping":
                    await websocket.send_text("pong")
        except WebSocketDisconnect:
            await manager.remove_all_subscriptions(websocket)
