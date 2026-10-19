"""D4：驱动恢复门禁只管 Collector，不得锁住 Edge Storage；恢复循环被门禁拦下时要记原因。"""
from __future__ import annotations

import asyncio
import logging
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest
from fastapi import HTTPException
from starlette.requests import Request

from app import factory
from app.routers import admin


class BlockedInstaller:
    def __init__(self):
        self._lock = asyncio.Lock()
        self.recovery_blocked = True

    def recovery_status(self):
        return {"blocked": True, "reasons": [{"error": "启动恢复异常", "verified": False}], "incomplete": []}


def _request(installer):
    return Request({"type": "http", "app": SimpleNamespace(state=SimpleNamespace(driver_installer=installer))})


def test_driver_gate_still_blocks_collector():
    with pytest.raises(HTTPException) as exc:
        admin._require_driver_idle("collector", _request(BlockedInstaller()))
    assert exc.value.status_code == 409


def test_driver_gate_does_not_block_storage():
    admin._require_driver_idle("storage", _request(BlockedInstaller()))


@pytest.mark.asyncio
async def test_storage_crash_recovery_not_blocked_by_driver_gate(tmp_path, monkeypatch):
    delays = []

    async def sleep(delay):
        delays.append(delay)
        if len(delays) == 2:
            raise asyncio.CancelledError()

    monkeypatch.setattr(factory.asyncio, "sleep", sleep)
    monkeypatch.setattr(factory, "_storage_env", lambda: {})
    restart = AsyncMock()
    pm = SimpleNamespace(
        wants_running=lambda *a, **k: True,
        status=lambda name: SimpleNamespace(status="crashed"),
        restart=restart,
        log_file=lambda name: tmp_path / "current.log",
    )
    app = SimpleNamespace(state=SimpleNamespace(
        pm=pm, driver_installer=BlockedInstaller(),
        edge_storage=SimpleNamespace(wait_ready=lambda: None), log_manager=MagicMock(),
    ))
    with pytest.raises(asyncio.CancelledError):
        await factory._recover_edge_storage(app)
    assert restart.await_count == 1


@pytest.mark.asyncio
async def test_process_recovery_logs_driver_gate_reason(monkeypatch, caplog):
    calls = [0]

    async def sleep(_delay):
        calls[0] += 1
        if calls[0] >= 2:
            raise asyncio.CancelledError()

    async def gated(*_args, **_kwargs):
        raise HTTPException(status_code=409, detail="驱动事务进行中或等待恢复，暂不能操作采集进程")

    monkeypatch.setattr(factory, "load_config", lambda: {"agent": {"bootstrap_done": True}})
    monkeypatch.setattr(factory.asyncio, "sleep", sleep)
    monkeypatch.setattr(factory, "start_process", gated)
    pm = SimpleNamespace(
        modules={"collector": None}, wants_running=lambda name: True,
        status=lambda name: SimpleNamespace(status="stopped", uptime_seconds=0),
    )
    app = SimpleNamespace(state=SimpleNamespace(pm=pm, driver_installer=BlockedInstaller()))
    with caplog.at_level(logging.WARNING, logger="app.factory"):
        with pytest.raises(asyncio.CancelledError):
            await factory._recover_processes(app)
    messages = [r.getMessage() for r in caplog.records]
    assert any("collector" in m and "启动恢复异常" in m for m in messages), messages
