"""DW-06：实时 WebSocket 周期复核与订阅时重新核验。"""
from __future__ import annotations

import asyncio
import time
from unittest.mock import AsyncMock, patch

import pytest

from app.api.routes.monitor import evaluate_realtime_ws_access, next_ws_reauth_delay
from app.core.exceptions import ApiError
from app.services.ws_push import WsPushManager


def test_reauth_delay_caps_to_token_expiry() -> None:
    now = 1_700_000_000.0
    delay = next_ws_reauth_delay(int(now + 8), 30, now=now)
    assert delay == 8
    assert next_ws_reauth_delay(0, 30, now=now) == 30
    assert next_ws_reauth_delay(int(now - 5), 30, now=now) == 0.0


@pytest.mark.asyncio
async def test_subscribe_rechecks_permissions() -> None:
    with patch(
        "app.api.routes.monitor.require_permissions_from_authorization",
        AsyncMock(side_effect=ApiError(401, "当前登录状态已失效，请重新登录")),
    ):
        with pytest.raises(ApiError) as err:
            await evaluate_realtime_ws_access("Bearer stale")
        assert err.value.code == 401


@pytest.mark.asyncio
async def test_subscribe_permission_loss_does_not_grant_subscribe() -> None:
    async def _require(authorization, required):
        if required == "device_monitor:realtime:subscribe":
            raise ApiError(403, "无权限访问该接口")
        return {"user_id": 1, "token_exp": int(time.time()) + 60}

    with patch(
        "app.api.routes.monitor.require_permissions_from_authorization",
        side_effect=_require,
    ):
        principal, can_subscribe = await evaluate_realtime_ws_access("Bearer t")
    assert principal["user_id"] == 1
    assert can_subscribe is False


@pytest.mark.asyncio
async def test_revoke_clears_pending_protected_data() -> None:
    manager = WsPushManager(send_timeout_seconds=1)
    gate = asyncio.Event()

    class SlowWebSocket:
        async def send_json(self, message):
            if message.get("type") != "snapshot":
                await gate.wait()

    ws = SlowWebSocket()
    manager._read_latest = AsyncMock(return_value={"1": {"v": 1, "t": time.time() * 1000}})
    with patch("app.services.ws_push._check_tdengine", AsyncMock(return_value=True)):
        await manager.subscribe(ws, "PLC001", "g001")
    await manager._broadcast(("PLC001", "g001"), {"type": "diff", "values": {"hold": {"v": 1}}})
    await asyncio.sleep(0.02)
    await manager._broadcast(("PLC001", "g001"), {"type": "diff", "values": {"secret": {"v": 9}}})
    assert manager.pending_count(ws) == 1
    await manager.revoke_connection(ws)
    assert manager.pending_count(ws) == 0
    assert manager.sender_task_count() == 0
    gate.set()
