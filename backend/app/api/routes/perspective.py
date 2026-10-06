from __future__ import annotations

from turboapi import Query, TurboAPI, WebSocket, WebSocketDisconnect

from app.api.permissions import require_permissions_from_authorization
from app.core.config import get_settings
from app.core.exceptions import ApiError
from app.services.perspective import get_perspective_session_manager


MAX_QUERY_TOKEN_LENGTH = 4096


def _resolve_websocket_authorization(websocket: WebSocket) -> str | None:
    """Prefer headers; retain a bounded query-token fallback for old browsers."""
    authorization = websocket.headers.get("authorization")
    if authorization:
        return authorization

    # Deprecated compatibility path: URL tokens can be copied into proxy/access logs.
    query_token = websocket.query_params.get("token")
    if query_token and len(str(query_token)) <= MAX_QUERY_TOKEN_LENGTH:
        return str(query_token)
    return None


def register_perspective_websocket(app: TurboAPI) -> None:
    settings = get_settings()
    manager = get_perspective_session_manager()

    @app.websocket(settings.perspective_ws_route)
    async def perspective_websocket(
        websocket: WebSocket,
        perspectiveSessionId: str = Query(...),
        connectionId: str = Query(...),
    ):
        authorization = _resolve_websocket_authorization(websocket)
        try:
            principal = await require_permissions_from_authorization(
                authorization,
                "device_monitor:history:view",
            )
        except ApiError:
            # Reject the handshake before accepting or attaching any server
            # state. The client receives a normal WebSocket policy failure.
            await websocket.close(code=1008)
            return

        await websocket.accept()

        if not settings.perspective_enabled:
            await websocket.send_json(
                {
                    "type": "error",
                    "message": "Perspective websocket is disabled",
                }
            )
            await websocket.close(code=1008)
            return

        owner_key = manager.build_owner_key(str(principal["user_id"]))
        try:
            session, restored, conflict = manager.attach_connection(
                owner_key=owner_key,
                session_id=perspectiveSessionId,
                connection_id=connectionId,
            )
        except RuntimeError as exc:
            await websocket.send_json(
                {
                    "type": "error",
                    "message": str(exc),
                }
            )
            await websocket.close(code=1013)
            return

        if conflict:
            await websocket.send_json(
                {
                    "type": "session_conflict",
                    "sessionId": perspectiveSessionId,
                    "message": "Perspective session already occupied by another active page instance",
                }
            )
            await websocket.close(code=1008)
            return

        try:
            if restored:
                await websocket.send_json(
                    {
                        "type": "session_restored",
                        "sessionId": perspectiveSessionId,
                    }
                )

            manager.bind_transport(
                owner_key=owner_key,
                session_id=perspectiveSessionId,
                connection_id=connectionId,
                transport=websocket,
            )

            snapshot = manager.serialize_snapshot(session)
            if snapshot["recordCount"] > 0:
                await websocket.send_json(snapshot)

            while True:
                payload = await websocket.receive_json()
                action = str(payload.get("action", "")).strip().lower()
                if action == "ping":
                    await websocket.send_json({"type": "pong"})
                    continue

                await websocket.send_json(
                    {
                        "type": "error",
                        "message": "Unsupported perspective websocket action",
                    }
                )
        except WebSocketDisconnect:
            pass
        finally:
            manager.detach_connection(
                owner_key=owner_key,
                session_id=perspectiveSessionId,
                connection_id=connectionId,
            )
