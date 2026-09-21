"""DW-08：密码计算有界执行与登录失败限流。

验收：
- 并发密码运算时事件循环心跳仍能推进
- 超过容量立即拒绝，不产生无界等待
- 登录失败滑窗限流生效，成功登录后清零
"""
from __future__ import annotations

import asyncio
import threading
import time
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from app.core.exceptions import ApiError
from app.core.login_limiter import LoginLimiter
from app.core.password_worker import PasswordWorker, PasswordWorkerBusy
from app.services.auth import AuthService


@pytest.mark.asyncio
async def test_event_loop_advances_during_bounded_password_work() -> None:
    worker = PasswordWorker(max_workers=2, max_queue=2)
    ticks: list[int] = []

    async def ticker() -> None:
        for i in range(4):
            ticks.append(i)
            await asyncio.sleep(0.02)

    async def job() -> None:
        await worker.run(time.sleep, 0.08)

    await asyncio.gather(ticker(), job(), job())
    assert ticks == [0, 1, 2, 3]
    worker.shutdown()


@pytest.mark.asyncio
async def test_password_worker_rejects_when_capacity_full() -> None:
    worker = PasswordWorker(max_workers=1, max_queue=1)
    started = threading.Event()
    release = threading.Event()

    def _block() -> None:
        started.set()
        release.wait(timeout=5)

    first = asyncio.create_task(worker.run(_block))
    await asyncio.to_thread(started.wait, 5)
    second = asyncio.create_task(worker.run(time.sleep, 0.05))
    for _ in range(50):
        if worker._in_flight >= 2:
            break
        await asyncio.sleep(0.01)
    assert worker._in_flight == 2

    with pytest.raises(PasswordWorkerBusy):
        await worker.run(time.sleep, 0.01)

    release.set()
    await asyncio.gather(first, second)
    worker.shutdown()


@pytest.mark.asyncio
async def test_login_rate_limit_blocks_after_failures(monkeypatch) -> None:
    limiter = LoginLimiter(max_failures=3, window_seconds=60)
    monkeypatch.setattr("app.services.auth.get_login_limiter", lambda: limiter)
    monkeypatch.setattr("app.services.auth.run_password_op", AsyncMock(return_value=False))
    monkeypatch.setattr("app.services.auth.write_system_log", AsyncMock())

    user = SimpleNamespace(
        id=1,
        user_name="op",
        password_hash="hash",
        status="1",
        deleted=False,
        token_version=0,
        must_change_password=False,
    )
    service = AuthService(session=SimpleNamespace(commit=AsyncMock()))
    service._fetch_user_by_name = AsyncMock(return_value=user)

    for _ in range(3):
        with pytest.raises(ApiError) as err:
            await service.login(user_name="op", password="bad", client_ip="10.0.0.1")
        assert err.value.code == 401

    with pytest.raises(ApiError) as blocked:
        await service.login(user_name="op", password="bad", client_ip="10.0.0.1")
    assert blocked.value.code == 429


@pytest.mark.asyncio
async def test_login_success_resets_rate_limit(monkeypatch) -> None:
    limiter = LoginLimiter(max_failures=2, window_seconds=60)
    monkeypatch.setattr("app.services.auth.get_login_limiter", lambda: limiter)
    monkeypatch.setattr("app.services.auth.run_password_op", AsyncMock(return_value=True))
    monkeypatch.setattr("app.services.auth.write_system_log", AsyncMock())
    monkeypatch.setattr(
        "app.services.auth.create_signed_token",
        lambda *args, **kwargs: "Bearer t",
    )

    user = SimpleNamespace(
        id=1,
        user_name="op",
        password_hash="hash",
        status="1",
        deleted=False,
        token_version=0,
        must_change_password=False,
        last_login_time=None,
        last_login_ip=None,
        update_by=None,
    )
    service = AuthService(session=SimpleNamespace(commit=AsyncMock()))
    service._fetch_user_by_name = AsyncMock(return_value=user)
    service._fetch_user_roles = AsyncMock(return_value=["R_USER"])

    limiter.record_failure("op", "10.0.0.1")
    data = await service.login(user_name="op", password="ok", client_ip="10.0.0.1")
    assert data["token"] == "Bearer t"
    assert limiter.is_blocked("op", "10.0.0.1") is False


@pytest.mark.asyncio
async def test_change_password_maps_worker_overload(monkeypatch) -> None:
    service = AuthService(session=SimpleNamespace(commit=AsyncMock()))
    service.resolve_principal = AsyncMock(return_value={"user_id": 7})
    service._fetch_user_by_id = AsyncMock(
        return_value=SimpleNamespace(
            id=7, user_name="op", password_hash="h", deleted=False, token_version=1
        )
    )

    async def _busy(*_args, **_kwargs):
        raise PasswordWorkerBusy()

    monkeypatch.setattr("app.services.auth.run_password_op", _busy)
    monkeypatch.setattr("app.services.auth.write_system_log", AsyncMock())

    from app.schemas.auth import ChangePasswordPayload

    with pytest.raises(ApiError) as err:
        await service.change_password(
            "Bearer t",
            ChangePasswordPayload(old_password="old", new_password="new-password"),
        )
    assert err.value.code == 503
