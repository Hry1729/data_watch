"""Realtime target validation and per-connection subscription limits."""

from __future__ import annotations

import os
import sys
from unittest.mock import AsyncMock, MagicMock

import pytest

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.api.routes.monitor import _resolve_ws_authorization, _subscription_limit_allows
from app.services.monitor import MonitorService


def test_subscription_limit_allows_idempotent_resubscribe_only() -> None:
    subscriptions = {("device-a", "g001")}

    assert _subscription_limit_allows(subscriptions, ("device-a", "g001"), 1)
    assert not _subscription_limit_allows(subscriptions, ("device-b", "g001"), 1)
    assert _subscription_limit_allows(subscriptions, ("device-b", "g001"), 2)


def test_realtime_authorization_prefers_header_and_bounds_query_fallback() -> None:
    websocket = MagicMock(
        headers={"authorization": "Bearer header-token"},
        query_params={"token": "query-token"},
    )
    assert _resolve_ws_authorization(websocket) == "Bearer header-token"

    websocket = MagicMock(headers={}, query_params={"token": "x" * 4097})
    assert _resolve_ws_authorization(websocket) is None


@pytest.mark.asyncio
async def test_realtime_target_requires_known_device_and_enabled_group_points() -> None:
    service = MonitorService(MagicMock(), MagicMock())
    service._fetch_target_device = AsyncMock(
        return_value={"device_id": 11, "device_code": "device-a"}
    )
    service._fetch_device_points = AsyncMock(return_value=[{"var_seq": 1}])

    assert await service.validate_realtime_target(" device-a ", "g001")
    service._fetch_device_points.return_value = []
    assert not await service.validate_realtime_target("device-a", "g002")

    service._fetch_target_device.return_value = None
    assert not await service.validate_realtime_target("missing", "g001")


@pytest.mark.asyncio
async def test_realtime_target_rejects_oversized_identifiers_without_database_access() -> None:
    service = MonitorService(MagicMock(), MagicMock())
    service._fetch_target_device = AsyncMock()

    assert not await service.validate_realtime_target("x" * 129, "g001")
    service._fetch_target_device.assert_not_awaited()
