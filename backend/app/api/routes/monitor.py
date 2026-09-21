from __future__ import annotations

import asyncio
import time

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


def next_ws_reauth_delay(
    token_exp: int,
    interval_seconds: float,
    *,
    now: float | None = None,
) -> float:
    """周期复核间隔，令牌更早到期则提前唤醒。"""
    delay = max(0.05, float(interval_seconds))
    exp = int(token_exp or 0)
    if exp <= 0:
        return delay
    remaining = exp - (time.time() if now is None else now)
    return min(delay, max(0.0, remaining))


async def evaluate_realtime_ws_access(authorization: str | None) -> tuple[dict, bool]:
    """核验实时推送身份与订阅权限。身份/查看权失败抛 ApiError；仅缺订阅权则 can_subscribe=False。"""
    principal = await require_permissions_from_authorization(
        authorization,
        {"device_monitor:monitor:view", "device_monitor:realtime:view"},
    )
    try:
        await require_permissions_from_authorization(
            authorization,
            "device_monitor:realtime:subscribe",
        )
    except ApiError as exc:
        if exc.code == 403:
            return principal, False
        raise
    return principal, True


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
            principal, can_subscribe = await asyncio.wait_for(evaluate_realtime_ws_access(authorization), timeout=5.0)
        except ApiError as e:
            await websocket.send_json({
                "type": "error",
                "message": e.message,
                "code": e.code,
            })
            await websocket.close()
            return

        manager = get_ws_manager()
        settings = get_settings()
        max_subscriptions = max(
            1, int(settings.realtime_ws_max_subscriptions_per_connection)
        )
        reauth_interval = max(
            1, int(settings.realtime_ws_reauth_interval_seconds)
        )
        fail_closed_limit = max(
            1, int(settings.realtime_ws_reauth_fail_closed)
        )
        subscriptions: set[tuple[str, str]] = set()
        state = {
            "can_subscribe": can_subscribe,
            "token_exp": int(principal.get("token_exp") or 0),
            "closed": False,
        }

        async def _drop_subscriptions() -> None:
            await manager.revoke_connection(websocket)
            subscriptions.clear()

        async def _close_unauthorized(code: int, message: str) -> None:
            if state["closed"]:
                return
            state["closed"] = True
            await _drop_subscriptions()
            try:
                await asyncio.wait_for(websocket.send_json(
                    {"type": "error", "message": message, "code": code}
                ), timeout=2.0)
            except Exception:
                pass
            try:
                await asyncio.wait_for(websocket.close(), timeout=2.0)
            except Exception:
                pass

        async def _reauth_watch() -> None:
            consecutive_infra = 0
            while not state["closed"]:
                delay = next_ws_reauth_delay(state["token_exp"], reauth_interval)
                await asyncio.sleep(delay)
                if state["closed"]:
                    return
                if state["token_exp"] and time.time() >= state["token_exp"]:
                    await _close_unauthorized(401, "令牌已过期")
                    return
                try:
                    refreshed, can_sub = await asyncio.wait_for(evaluate_realtime_ws_access(authorization), timeout=5.0)
                    consecutive_infra = 0
                    state["can_subscribe"] = can_sub
                    state["token_exp"] = int(refreshed.get("token_exp") or 0)
                    if not can_sub:
                        await _close_unauthorized(403, "实时订阅权限已失效")
                        return
                except ApiError as exc:
                    await _close_unauthorized(exc.code, exc.message)
                    return
                except Exception:
                    consecutive_infra += 1
                    if consecutive_infra >= fail_closed_limit:
                        await _close_unauthorized(503, "实时认证复核失败")
                        return

        reauth_task = asyncio.create_task(_reauth_watch())
        try:
            while not state["closed"]:
                msg = await websocket.receive_json()
                action = msg.get("action")
                device_code = str(msg.get("device_code", "")).strip()
                group_code = str(msg.get("group_code", "g001")).strip() or "g001"
                if action == "subscribe" and device_code:
                    key = (device_code, group_code)
                    try:
                        refreshed, can_sub = await asyncio.wait_for(evaluate_realtime_ws_access(authorization), timeout=5.0)
                        state["can_subscribe"] = can_sub
                        state["token_exp"] = int(refreshed.get("token_exp") or 0)
                    except ApiError as e:
                        if e.code in (401, 403):
                            await _close_unauthorized(e.code, e.message)
                            return
                        await websocket.send_json(
                            {"type": "error", "message": e.message, "code": e.code}
                        )
                        continue
                    except Exception:
                        await websocket.send_json(
                            {"type": "error", "message": "实时认证复核失败", "code": 503}
                        )
                        continue
                    if state["closed"]:
                        return
                    if not state["can_subscribe"]:
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
                    if state["closed"] or not state["can_subscribe"]:
                        return
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
            state["closed"] = True
            reauth_task.cancel()
            try:
                await reauth_task
            except (asyncio.CancelledError, Exception):
                pass
            await manager.remove_all_subscriptions(websocket)
