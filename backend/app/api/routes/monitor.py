from __future__ import annotations

import asyncio

from turboapi import APIRouter, TurboAPI, WebSocket, WebSocketDisconnect, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import require_permissions, require_permissions_from_authorization
from app.core.config import get_settings
from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.tdengine import get_tdengine_client
from app.services.monitor import MonitorService
from app.services.point_resolver import PointResolverService
from app.services.ws_push import get_ws_manager


router = APIRouter(tags=["monitor"])
auth_scheme = HTTPBearer(auto_error=False)
MAX_QUERY_TOKEN_LENGTH = 4096


def _resolve_ws_authorization(websocket: WebSocket) -> str | None:
    """Prefer headers; keep a bounded query-token fallback for old clients."""
    authorization = websocket.headers.get("authorization")
    if authorization:
        return authorization

    query_token = websocket.query_params.get("token")
    if query_token and len(str(query_token)) <= MAX_QUERY_TOKEN_LENGTH:
        return str(query_token)
    return None


def _subscription_limit_allows(
    subscriptions: set[tuple[str, str]],
    key: tuple[str, str],
    max_subscriptions: int,
) -> bool:
    """Allow idempotent re-subscribe, but cap new targets per connection."""
    return key in subscriptions or len(subscriptions) < max(1, int(max_subscriptions))


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


@router.get("/api/device-monitor/monitor/point-resolve")
async def monitor_point_resolve(
    query: str,
    deviceHint: str | None = None,
    limit: int = 20,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(
        credentials,
        {
            "device_monitor:monitor:view",
            "device_monitor:realtime:view",
            "device_monitor:history:view",
            "device_monitor:point:view",
        },
    )
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await PointResolverService(session).search(
            query, device_hint=deviceHint, limit=limit
        )
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
        authorization = _resolve_ws_authorization(websocket)
        if not authorization:
            try:
                auth_msg = await asyncio.wait_for(websocket.receive_json(), timeout=5.0)
                if auth_msg.get("type") == "auth":
                    authorization = auth_msg.get("token", "")
            except (asyncio.TimeoutError, Exception):
                await websocket.send_json({"type": "error", "message": "认证超时", "code": 401})
                await websocket.close()
                return
        try:
            principal = await require_permissions_from_authorization(
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
            await require_permissions_from_authorization(
                authorization,
                "device_monitor:realtime:subscribe",
            )
            can_subscribe = True
        except ApiError:
            can_subscribe = False
        settings = get_settings()
        max_subscriptions = max(
            1, int(settings.realtime_ws_max_subscriptions_per_connection)
        )
        subscriptions: set[tuple[str, str]] = set()
        try:
            while True:
                msg = await websocket.receive_json()
                action = msg.get("action")
                device_code = str(msg.get("device_code", "")).strip()
                group_code = str(msg.get("group_code", "g001")).strip() or "g001"
                if action == "subscribe" and device_code:
                    key = (device_code, group_code)
                    if not can_subscribe:
                        await websocket.send_json(
                            {"type": "error", "message": "无实时订阅权限", "code": 403}
                        )
                        continue
                    if not _subscription_limit_allows(
                        subscriptions, key, max_subscriptions
                    ):
                        await websocket.send_json(
                            {
                                "type": "error",
                                "message": "单连接实时订阅数已达上限",
                                "code": 429,
                            }
                        )
                        continue
                    try:
                        session_factory = get_mysql_session_factory()
                        async with session_factory() as session:
                            target_service = MonitorService(
                                session=session,
                                tdengine=get_tdengine_client(),
                            )
                            known_target = await target_service.validate_realtime_target(
                                device_code, group_code
                            )
                    except Exception:
                        await websocket.send_json(
                            {"type": "error", "message": "实时订阅目标校验失败", "code": 503}
                        )
                        continue
                    if not known_target:
                        await websocket.send_json(
                            {"type": "error", "message": "设备或分组不存在", "code": 404}
                        )
                        continue
                    await manager.subscribe(websocket, device_code, group_code)
                    subscriptions.add(key)
                elif action == "unsubscribe" and device_code:
                    key = (device_code, group_code)
                    if key in subscriptions:
                        await manager.unsubscribe(websocket, device_code, group_code)
                        subscriptions.discard(key)
                elif action == "ping":
                    await websocket.send_text("pong")
        except WebSocketDisconnect:
            pass
        finally:
            await manager.remove_all_subscriptions(websocket)
