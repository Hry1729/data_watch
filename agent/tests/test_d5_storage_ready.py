"""D5：Storage 就绪等待需独立且足够长的启动超时，失败抛 ProcessManagerError（调用方已处理）。"""
from __future__ import annotations

import time

import pytest

from app.services.edge_storage_client import (
    EdgeStorageClient,
    StorageUnauthorized,
    StorageUnavailable,
)
from app.services.process_manager import ProcessManagerError


def test_startup_timeout_is_separate_and_long_enough() -> None:
    client = EdgeStorageClient("http://127.0.0.1:1", timeout_seconds=2.0)
    assert client.timeout_seconds == 2.0
    assert client.startup_timeout_seconds >= 60.0


def test_wait_ready_uses_startup_timeout_and_raises_process_manager_error(monkeypatch) -> None:
    client = EdgeStorageClient("http://127.0.0.1:1", timeout_seconds=2.0, startup_timeout_seconds=0.3)
    calls = []

    def down():
        calls.append(1)
        raise StorageUnavailable("Edge Storage unavailable: refused")

    monkeypatch.setattr(client, "status", down)
    started = time.monotonic()
    with pytest.raises(ProcessManagerError, match="Edge Storage"):
        client.wait_ready()
    assert time.monotonic() - started >= 0.3
    assert len(calls) > 1


def test_wait_ready_succeeds_after_slow_startup(monkeypatch) -> None:
    client = EdgeStorageClient("http://127.0.0.1:1", timeout_seconds=2.0, startup_timeout_seconds=5.0)
    attempts = []

    def eventually():
        attempts.append(1)
        if len(attempts) < 3:
            raise StorageUnavailable("migrating")
        return {"counts": {}}

    monkeypatch.setattr(client, "status", eventually)
    assert client.wait_ready() == {"counts": {}}


def test_wait_ready_fails_fast_on_unauthorized(monkeypatch) -> None:
    client = EdgeStorageClient("http://127.0.0.1:1", timeout_seconds=2.0, startup_timeout_seconds=30.0)

    def denied():
        raise StorageUnauthorized("bad token")

    monkeypatch.setattr(client, "status", denied)
    started = time.monotonic()
    with pytest.raises(ProcessManagerError, match="bad token"):
        client.wait_ready()
    assert time.monotonic() - started < 1.0
