from __future__ import annotations

import json

from cli.realtime import RealtimeWatcher


class FakeWebSocket:
    def __init__(self) -> None:
        self.incoming = iter(
            [
                json.dumps({"type": "diff", "values": {"12": {"v": 2}}}),
                json.dumps(
                    {
                        "type": "diff",
                        "values": {"11": {"v": 1}, "12": {"v": 2}},
                    }
                ),
            ]
        )
        self.sent: list[dict] = []

    def __enter__(self) -> "FakeWebSocket":
        return self

    def __exit__(self, *_args: object) -> None:
        pass

    def send(self, raw: str) -> None:
        self.sent.append(json.loads(raw))

    def recv(self, timeout=None) -> str:
        return next(self.incoming)


def test_point_watch_skips_unrelated_diffs_before_counting_max_events() -> None:
    socket = FakeWebSocket()
    watcher = RealtimeWatcher(
        "http://backend.local",
        "Bearer token",
        timeout_seconds=5,
        connect_factory=lambda *_args, **_kwargs: socket,
    )

    events = list(
        watcher.events("PLC001", "g002", point_id=11, max_events=1)
    )

    assert events == [
        {"type": "diff", "values": {"11": {"v": 1}}, "pointId": 11}
    ]
    assert socket.sent[-1]["action"] == "unsubscribe"
