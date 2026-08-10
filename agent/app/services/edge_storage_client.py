from __future__ import annotations

import json
import time
from typing import Any
import urllib.error
import urllib.request


class EdgeStorageClient:
    """Agent-side client for the local Edge Storage command endpoint."""

    def __init__(self, base_url: str = "http://127.0.0.1:7073", *, timeout_seconds: float = 10.0) -> None:
        self.base_url = base_url.rstrip("/")
        self.timeout_seconds = timeout_seconds

    def status(self) -> dict[str, Any]:
        return dict(self._command("status"))

    def wait_ready(self, timeout_seconds: float = 5.0) -> dict[str, Any]:
        deadline = time.monotonic() + timeout_seconds
        last_error: RuntimeError | None = None
        while time.monotonic() < deadline:
            try:
                return self.status()
            except RuntimeError as exc:
                last_error = exc
                time.sleep(0.1)
        raise RuntimeError(f"Edge Storage did not become ready: {last_error}")

    def revision(self) -> int:
        return int(self._command("config.revision"))

    def apply(self, snapshot: dict[str, Any]) -> int:
        return int(self._command("config.apply", {"snapshot": snapshot}))

    def refresh_schedule(self, now_ms: int | None = None) -> None:
        self._command("config.refresh_schedule", {"now_ms": now_ms})

    def _command(self, action: str, payload: dict[str, Any] | None = None) -> Any:
        body = json.dumps(
            {"action": action, "payload": payload or {}},
            ensure_ascii=True,
            separators=(",", ":"),
        ).encode("utf-8")
        request = urllib.request.Request(
            f"{self.base_url}/v1/command",
            data=body,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=self.timeout_seconds) as response:
                envelope = json.loads(response.read().decode("utf-8"))
        except (urllib.error.URLError, OSError, ValueError) as exc:
            raise RuntimeError(f"Edge Storage unavailable: {exc}") from exc
        if not envelope.get("ok"):
            raise RuntimeError(str(envelope.get("error") or "Edge Storage command failed"))
        return envelope.get("data")
