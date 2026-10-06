"""Perspective websocket authentication happens before handshake/session attach."""

from __future__ import annotations

import os
import sys
from types import SimpleNamespace

import pytest
from unittest.mock import AsyncMock, MagicMock

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.api.routes import perspective
from app.core.exceptions import ApiError


class FakeWebSocket:
    def __init__(self, *, headers: dict[str, str], query_params: dict[str, str]):
        self.headers = headers
        self.query_params = query_params
        self.accepted = False
        self.closed: list[tuple[int, str | None]] = []

    async def accept(self) -> None:
        self.accepted = True

    async def close(self, code: int = 1000, reason: str | None = None) -> None:
        self.closed.append((code, reason))


class FakeApp:
    def __init__(self):
        self.handler = None

    def websocket(self, _path: str):
        def register(handler):
            self.handler = handler
            return handler

        return register


@pytest.mark.asyncio
async def test_unauthenticated_perspective_is_rejected_before_accept(monkeypatch) -> None:
    app = FakeApp()
    manager = SimpleNamespace()
    monkeypatch.setattr(
        perspective,
        "get_settings",
        lambda: SimpleNamespace(
            perspective_ws_route="/perspective",
            perspective_enabled=True,
        ),
    )
    monkeypatch.setattr(perspective, "get_perspective_session_manager", lambda: manager)

    async def reject(_authorization, _permission):
        raise ApiError(401, "未认证")

    monkeypatch.setattr(perspective, "require_permissions_from_authorization", reject)
    perspective.register_perspective_websocket(app)
    websocket = FakeWebSocket(headers={}, query_params={})

    await app.handler(
        websocket,
        perspectiveSessionId="session-1",
        connectionId="connection-1",
    )

    assert not websocket.accepted
    assert websocket.closed == [(1008, None)]


def test_header_authorization_precedes_deprecated_query_token() -> None:
    websocket = FakeWebSocket(
        headers={"authorization": "Bearer header-token"},
        query_params={"token": "query-token"},
    )

    assert perspective._resolve_websocket_authorization(websocket) == "Bearer header-token"


def test_query_token_is_bounded_compatibility_fallback() -> None:
    websocket = FakeWebSocket(headers={}, query_params={"token": "x" * 4097})

    assert perspective._resolve_websocket_authorization(websocket) is None


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", ["snapshot", "json"])
async def test_attached_session_is_released_on_transport_failure(monkeypatch, failure):
    app = FakeApp()
    manager = MagicMock()
    manager.attach_connection.return_value = (object(), False, False)
    manager.serialize_snapshot.return_value = {"recordCount": 1 if failure == "snapshot" else 0}
    monkeypatch.setattr(perspective, "get_settings", lambda: SimpleNamespace(
        perspective_ws_route="/perspective", perspective_enabled=True))
    monkeypatch.setattr(perspective, "get_perspective_session_manager", lambda: manager)
    monkeypatch.setattr(perspective, "require_permissions_from_authorization", AsyncMock(return_value={"user_id": 1}))
    websocket = FakeWebSocket(headers={}, query_params={})
    websocket.send_json = AsyncMock(side_effect=ValueError("broken transport"))
    websocket.receive_json = AsyncMock(side_effect=ValueError("invalid JSON"))
    perspective.register_perspective_websocket(app)
    with pytest.raises(ValueError):
        await app.handler(websocket, perspectiveSessionId="s1", connectionId="c1")
    manager.detach_connection.assert_called_once()
