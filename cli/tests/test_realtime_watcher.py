from __future__ import annotations

import json

import pytest

from cli.errors import CliError, EXIT_AUTH_REQUIRED
from cli.realtime import RealtimeWatcher


class FakeWebSocket:
    def __init__(self, incoming: list[str]) -> None:
        self.incoming = iter(incoming)
        self.sent: list[dict] = []

    def __enter__(self) -> "FakeWebSocket":
        return self

    def __exit__(self, *_args: object) -> None:
        pass

    def send(self, raw: str) -> None:
        self.sent.append(json.loads(raw))

    def recv(self, timeout=None) -> str:
        return next(self.incoming)


def test_watch_authenticates_by_message_with_original_token_then_subscribes() -> None:
    socket = FakeWebSocket(
        [json.dumps({"type": "snapshot", "device_code": "PLC001", "values": {}})]
    )
    connection: dict = {}

    def connect_factory(url: str, **kwargs):
        connection["url"] = url
        connection["kwargs"] = kwargs
        return socket

    watcher = RealtimeWatcher(
        "https://backend.example.com",
        "Bearer signed-token",
        timeout_seconds=5,
        connect_factory=connect_factory,
    )

    events = list(watcher.events("PLC001", "g001", max_events=1))

    assert connection["url"] == (
        "wss://backend.example.com/api/device-monitor/ws/realtime-push"
    )
    assert "token" not in connection["url"]
    assert socket.sent[0] == {"type": "auth", "token": "Bearer signed-token"}
    assert socket.sent[1] == {
        "action": "subscribe",
        "device_code": "PLC001",
        "group_code": "g001",
    }
    assert socket.sent[2]["action"] == "unsubscribe"
    assert events[0]["type"] == "snapshot"


def test_websocket_auth_error_maps_to_auth_exit_code() -> None:
    socket = FakeWebSocket(
        [json.dumps({"type": "error", "code": 401, "message": "令牌无效"})]
    )
    watcher = RealtimeWatcher(
        "http://backend.local",
        "Bearer expired",
        timeout_seconds=5,
        connect_factory=lambda *_args, **_kwargs: socket,
    )

    with pytest.raises(CliError) as captured:
        list(watcher.events("PLC001", "g001", max_events=1))

    assert captured.value.exit_code == EXIT_AUTH_REQUIRED
    assert captured.value.status == 401

def test_watch_reconnects_and_reauthenticates_after_connection_loss() -> None:
    first = FakeWebSocket([])
    second = FakeWebSocket(
        [json.dumps({"type": "update", "device_code": "QC100", "values": {}})]
    )

    def failed_recv(timeout=None):
        raise OSError("connection lost")

    first.recv = failed_recv
    sockets = iter([first, second])
    delays: list[float] = []
    watcher = RealtimeWatcher(
        "http://backend.local",
        "Bearer token",
        timeout_seconds=5,
        connect_factory=lambda *_args, **_kwargs: next(sockets),
        max_reconnects=1,
        reconnect_backoff_seconds=0.1,
        sleep=delays.append,
    )

    events = list(watcher.events("QC100", "g001", max_events=1))

    assert events[0]["type"] == "update"
    assert first.sent[0]["type"] == "auth"
    assert second.sent[0]["type"] == "auth"
    assert delays == [0.1]


def test_window_timeout_returns_collected_events_without_error() -> None:
    socket = FakeWebSocket([])

    def timeout_recv(timeout=None):
        raise TimeoutError()

    socket.recv = timeout_recv
    watcher = RealtimeWatcher(
        "http://backend.local",
        "Bearer token",
        timeout_seconds=5,
        connect_factory=lambda *_args, **_kwargs: socket,
    )

    assert list(watcher.events("QC100", "g001", duration_seconds=0.01)) == []


def test_successful_messages_reset_consecutive_reconnect_budget():
    class IntermittentSocket(FakeWebSocket):
        def recv(self, timeout=None):
            try:
                return super().recv(timeout)
            except StopIteration:
                raise OSError("connection lost")

    sockets = iter(IntermittentSocket([json.dumps({"type": "update", "values": {}})]) for _ in range(6))
    watcher = RealtimeWatcher("http://backend.local", "token", timeout_seconds=5,
        connect_factory=lambda *a, **k: next(sockets), max_reconnects=1, sleep=lambda _: None)
    assert len(list(watcher.events("QC100", "g001", max_events=6))) == 6
