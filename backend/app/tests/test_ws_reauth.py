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


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", [401,403])
async def test_live_route_revokes_before_stalled_error_send(monkeypatch, failure):
    from types import SimpleNamespace
    from app.api.routes import monitor
    class App:
        def websocket(self, path):
            def register(fn):self.handler=fn;return fn
            return register
    class WS:
        headers={"authorization":"Bearer t"};query_params={}
        async def accept(self):pass
        async def receive_json(self):await asyncio.Event().wait()
        async def send_json(self,msg):await asyncio.Event().wait()
        async def close(self):pass
    manager=SimpleNamespace(revoke_connection=AsyncMock(),remove_all_subscriptions=AsyncMock())
    checks=0
    async def access(auth):
        nonlocal checks
        checks+=1
        if checks>1:raise ApiError(failure,"revoked")
        return {"token_exp":int(time.time())+60},True
    monkeypatch.setattr(monitor,"evaluate_realtime_ws_access",access)
    monkeypatch.setattr(monitor,"get_ws_manager",lambda:manager)
    monkeypatch.setattr(monitor,"get_settings",lambda:SimpleNamespace(
        realtime_ws_max_subscriptions_per_connection=20,realtime_ws_reauth_interval_seconds=1,
        realtime_ws_reauth_fail_closed=3))
    app=App();monitor.register_ws_push_websocket(app)
    task=asyncio.create_task(app.handler(WS()))
    try:
        deadline=time.monotonic()+2
        while manager.revoke_connection.await_count==0 and time.monotonic()<deadline:
            await asyncio.sleep(.01)
        assert manager.revoke_connection.await_count==1
    finally:
        task.cancel()
        with pytest.raises(asyncio.CancelledError):await task


@pytest.mark.asyncio
async def test_revocation_during_snapshot_read_cannot_resubscribe():
    class WS:
        async def send_json(self,msg):raise AssertionError("revoked data leaked")
    ws=WS();manager=WsPushManager();entered=asyncio.Event();release=asyncio.Event()
    async def latest(*args):entered.set();await release.wait();return {"1":{"t":time.time()*1000}}
    manager._read_latest=latest
    with patch("app.services.ws_push._check_tdengine",AsyncMock(return_value=True)):
        task=asyncio.create_task(manager.subscribe(ws,"D","g"))
        await entered.wait();await manager.revoke_connection(ws);release.set();await task
    assert not manager._subscribers
    assert manager.sender_task_count()==0
