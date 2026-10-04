"""HTTP history query and Perspective WebSocket must resolve the same owner key (B2)."""

from __future__ import annotations

import os
import sys
from contextlib import asynccontextmanager
from datetime import datetime
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.api.routes import history as history_routes
from app.services import history as history_service_module
from app.services.history import HistoryDevice, HistoryPoint, HistoryService
from app.services.perspective import PerspectiveProtectionConfig, PerspectiveSessionManager


def _manager() -> PerspectiveSessionManager:
    return PerspectiveSessionManager(
        PerspectiveProtectionConfig(
            enabled=True,
            ws_path="/perspective",
            max_clients=10,
            session_idle_ttl_seconds=600,
            session_restore_ttl_seconds=600,
            max_sessions_per_owner=5,
            max_sessions_global=50,
            max_points_per_query=10,
            max_range_hours=24,
            max_records_per_query=100,
            ping_interval_seconds=30,
            timeout_seconds=60,
        )
    )


class FakeTransport:
    def __init__(self) -> None:
        self.sent: list[dict] = []

    async def send_json(self, payload: dict) -> None:
        self.sent.append(payload)


@pytest.mark.asyncio
async def test_http_snapshot_reaches_websocket_session_of_same_user(monkeypatch) -> None:
    manager = _manager()
    monkeypatch.setattr(history_service_module, "get_perspective_session_manager", lambda: manager)

    # Same derivation as app/api/routes/perspective.py for the WebSocket side.
    ws_owner_key = manager.build_owner_key("42")
    manager.attach_connection(ws_owner_key, "session-1", "conn-1")
    transport = FakeTransport()
    manager.bind_transport(ws_owner_key, "session-1", "conn-1", transport)

    service = HistoryService(MagicMock(), MagicMock())
    service.settings = SimpleNamespace(perspective_max_records_per_query=10)
    service._fetch_device = AsyncMock(return_value=HistoryDevice(1, "dev001", "Device", "qc"))
    service._fetch_selected_points = AsyncMock(
        return_value=[HistoryPoint(1, "p1", "a1", "REAL", 1, None)]
    )
    service._validate_query_points = MagicMock()
    service._validate_query_range = MagicMock(
        return_value=(datetime(2026, 1, 1), datetime(2026, 1, 2))
    )
    service._format_timestamp = MagicMock(side_effect=lambda value: value.isoformat())
    service._fetch_point_series = AsyncMock(return_value=[(datetime(2026, 1, 1, 0, 1), 1.5)])

    await service.query_history(
        1,
        [1],
        "2026-01-01",
        "2026-01-02",
        perspective_session_id="session-1",
        perspective_query_version=1,
        owner_identity="42",
    )

    assert len(transport.sent) == 1
    assert transport.sent[0]["recordCount"] == 1
    assert manager.get_session(manager.build_owner_key("Bearer some-token"), "session-1") is None


class FakeQueryParams:
    def __init__(self, values: dict[str, list[str]]) -> None:
        self._values = values

    def getlist(self, key: str) -> list[str]:
        return self._values.get(key, [])


@pytest.mark.asyncio
async def test_history_route_passes_principal_user_id_as_owner_identity(monkeypatch) -> None:
    captured: dict = {}

    async def allow(_credentials, _permission):
        return {"user_id": 42}

    @asynccontextmanager
    async def session_scope():
        yield MagicMock()

    class FakeHistoryService:
        def __init__(self, **_kwargs) -> None:
            pass

        async def query_history(self, **kwargs):
            captured.update(kwargs)
            return {}

    monkeypatch.setattr(history_routes, "require_permissions", allow)
    monkeypatch.setattr(history_routes, "get_mysql_session_factory", lambda: session_scope)
    monkeypatch.setattr(history_routes, "get_tdengine_client", lambda: MagicMock())
    monkeypatch.setattr(history_routes, "HistoryService", FakeHistoryService)

    request = SimpleNamespace(
        headers={
            "Authorization": "Bearer some-token",
            "X-Perspective-Session-Id": "session-1",
        },
        query_params=FakeQueryParams({"varSeqs": ["1"]}),
    )
    await history_routes.history_query(
        request,
        deviceId="1",
        startTime="2026-01-01",
        endTime="2026-01-02",
        credentials=None,
    )

    assert captured["owner_identity"] == "42"
    assert "authorization" not in captured
