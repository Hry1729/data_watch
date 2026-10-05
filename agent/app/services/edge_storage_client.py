from __future__ import annotations

import json
import os
from pathlib import Path
import time
from typing import Any
import urllib.error
import urllib.request

from app.core.config import data_root


# S3: the Edge Storage IPC endpoint authenticates every caller, so the Agent uses
# its own dedicated least-privilege "agent" token. This is a purpose-built IPC
# credential and must never be the Agent management service token.
IPC_TOKEN_HEADER = "X-DataWatch-Storage-Token"
IPC_ROLE = "agent"
IPC_TOKEN_ENV = "DATAWATCH_EDGE_STORAGE_TOKEN"
IPC_TOKEN_FILE_ENV = "DATAWATCH_EDGE_STORAGE_TOKEN_FILE"
IPC_TOKEN_FILE_NAME = "storage-ipc-tokens.json"


class StorageUnavailable(RuntimeError):
    """The Edge Storage IPC call did not execute (endpoint down, or rejected it)."""


class StorageUnauthorized(StorageUnavailable):
    """The IPC credential was rejected (HTTP 401/403).

    401/403 说明「服务在跑，但凭据错了 / 该角色无权调用」，与「服务不可达」是两类完全不同的
    排障方向。它是 ``StorageUnavailable``（进而 ``RuntimeError``）的子类，因此只关心「本次调用
    没有被服务端执行」的既有调用方无需改动。
    """


def ipc_token_file() -> Path:
    configured = os.environ.get(IPC_TOKEN_FILE_ENV)
    if configured:
        return Path(configured)
    database = os.environ.get("DATAWATCH_EDGE_DB")
    if database:
        return Path(database).parent / IPC_TOKEN_FILE_NAME
    # The Agent hands DATAWATCH_DATA_ROOT to the storage/collector/uploader
    # children, so the edge side resolves the same file.
    return data_root() / "edge" / IPC_TOKEN_FILE_NAME


def ipc_token() -> str | None:
    configured = os.environ.get(IPC_TOKEN_ENV)
    if configured:
        return configured.strip()
    try:
        payload = json.loads(ipc_token_file().read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return None
    tokens = payload.get("tokens") if isinstance(payload, dict) else None
    if not isinstance(tokens, dict):
        return None
    token = tokens.get(IPC_ROLE)
    return str(token) if token else None


class EdgeStorageClient:
    """Agent-side client for the local Edge Storage command endpoint."""

    def __init__(self, base_url: str = "http://127.0.0.1:7073", *, timeout_seconds: float = 10.0) -> None:
        self.base_url = base_url.rstrip("/")
        self.timeout_seconds = timeout_seconds
        self._token = ipc_token()

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
        # Storage may create or rotate its token after this client is constructed.
        self._token = ipc_token()
        body = json.dumps(
            {"action": action, "payload": payload or {}},
            ensure_ascii=True,
            separators=(",", ":"),
        ).encode("utf-8")
        headers = {"Content-Type": "application/json"}
        if self._token:
            headers[IPC_TOKEN_HEADER] = self._token
        request = urllib.request.Request(
            f"{self.base_url}/v1/command",
            data=body,
            headers=headers,
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=self.timeout_seconds) as response:
                envelope = json.loads(response.read().decode("utf-8"))
        except urllib.error.HTTPError as exc:
            # 401/403 是可判定的确定性失败：服务活着，凭据/权限不对。它属于
            # ``StorageUnauthorized``（``StorageUnavailable`` 的子类），既与「服务不可达」
            # 分开，也不破坏只关心「本次调用没有被执行」的既有调用方。
            if exc.code in (401, 403):
                raise StorageUnauthorized(
                    "Edge Storage rejected the IPC credential "
                    f"(HTTP {exc.code}): check the '{IPC_ROLE}' role token in "
                    f"{IPC_TOKEN_FILE_NAME}"
                ) from exc
            raise StorageUnavailable(f"Edge Storage unavailable: {exc}") from exc
        except (urllib.error.URLError, OSError, ValueError) as exc:
            raise StorageUnavailable(f"Edge Storage unavailable: {exc}") from exc
        if not envelope.get("ok"):
            raise RuntimeError(str(envelope.get("error") or "Edge Storage command failed"))
        return envelope.get("data")
