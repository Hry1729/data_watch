from __future__ import annotations

from turboapi import Query, TurboAPI, WebSocket, WebSocketDisconnect

from app.core.config import get_settings
from app.services.perspective import get_perspective_session_manager


def register_perspective_websocket(app: TurboAPI) -> None:
    settings = get_settings()
    manager = get_perspective_session_manager()

    @app.websocket(settings.perspective_ws_route)
    async def perspective_websocket(
        websocket: WebSocket,
        perspectiveSessionId: str = Query(...),
        connectionId: str = Query(...),
    ):
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

        owner_key = manager.build_owner_key(websocket.headers.get("authorization"))
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

        try:
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
            manager.detach_connection(
                owner_key=owner_key,
                session_id=perspectiveSessionId,
                connection_id=connectionId,
            )
