from __future__ import annotations

import base64
import json
import os
from pathlib import Path
import sys
import threading
from typing import Any
from uuid import uuid4

import httpx
import msgspec

from edge.app.outbox import (
    OutboxCompletion,
    OutboxRecord,
    SessionSwitchState,
)
from edge.app.paths import edge_database_path


# --- Edge Storage IPC credential contract ---------------------------------
#
# Shared by the server (edge.app.storage_service) and every client. The IPC
# endpoint is reachable by every process on the host, so a loopback bind address
# is not caller authentication: callers present a per-role token from a
# deployment-local file that is kept readable only by the edge accounts.
IPC_TOKEN_HEADER = "X-DataWatch-Storage-Token"
IPC_TOKEN_ENV = "DATAWATCH_EDGE_STORAGE_TOKEN"
IPC_TOKEN_FILE_ENV = "DATAWATCH_EDGE_STORAGE_TOKEN_FILE"
IPC_TOKEN_FILE_NAME = "storage-ipc-tokens.json"
IPC_ROLE_ENV = "DATAWATCH_EDGE_STORAGE_ROLE"


class StorageUnavailable(RuntimeError):
    pass


class StorageUnauthorized(StorageUnavailable):
    """The IPC credential was rejected (HTTP 401/403).

    P2-D：401/403 说明「服务在跑，但凭据错了/该角色无权调用」，与「服务不可达」是
    两类完全不同的排障方向。它是 ``StorageUnavailable`` 的子类，因此只关心「本次调用
    没有被服务端执行」的既有调用方无需改动。
    """


def storage_base_url() -> str:
    return os.environ.get("DATAWATCH_EDGE_STORAGE_URL", "http://127.0.0.1:7073").rstrip("/")


def default_token_file() -> Path:
    configured = os.environ.get(IPC_TOKEN_FILE_ENV)
    if configured:
        return Path(configured)
    return edge_database_path().parent / IPC_TOKEN_FILE_NAME


def read_role_tokens(path: Path) -> dict[str, str]:
    data = json.loads(Path(path).read_text(encoding="utf-8"))
    tokens = data.get("tokens") if isinstance(data, dict) else None
    if not isinstance(tokens, dict) or not tokens:
        raise ValueError(f"invalid Edge Storage IPC token file: {path}")
    return {str(role): str(value) for role, value in tokens.items()}


def resolve_client_role() -> str | None:
    """Least-privilege IPC role for this process.

    ``DATAWATCH_EDGE_STORAGE_ROLE`` wins; otherwise the role is inferred from the
    running entry point so the documented launches (``edge/collector_main.py``,
    ``edge/uploader_main.py`` and the packaged ``data-watch-edge-collector.exe``
    / ``data-watch-uploader.exe``) need no call-site changes.

    Returns ``None`` when the caller cannot be identified. That is deliberate:
    there is no privileged fallback, so an unidentified process sends no token
    and the server rejects it instead of being silently trusted.
    """
    configured = os.environ.get(IPC_ROLE_ENV)
    if configured:
        return configured.strip().lower()
    names = [Path(sys.executable).name]
    if sys.argv and sys.argv[0]:
        names.append(Path(sys.argv[0]).name)
    main_file = getattr(sys.modules.get("__main__"), "__file__", None)
    if main_file:
        names.append(Path(main_file).name)
    for name in names:
        lowered = name.lower()
        if "uploader" in lowered:
            return "uploader"
        if "collector" in lowered:
            return "collector"
    return None


def load_client_token(role: str | None) -> str | None:
    """Token for ``role`` from the environment or the deployment token file."""
    if not role:
        return None
    configured = os.environ.get(IPC_TOKEN_ENV)
    if configured:
        return configured.strip()
    try:
        tokens = read_role_tokens(default_token_file())
    except (OSError, ValueError):
        return None
    return tokens.get(role)


class EdgeStorageClient:
    """Synchronous local IPC client used by edge worker processes."""

    def __init__(
        self,
        base_url: str | None = None,
        *,
        role: str | None = None,
        token: str | None = None,
        timeout_seconds: float = 35.0,
        client: httpx.Client | None = None,
    ) -> None:
        self.base_url = (base_url or storage_base_url()).rstrip("/")
        self.role = role or resolve_client_role()
        self._token = token if token is not None else load_client_token(self.role)
        # Token was taken from the deployment file/env rather than injected by the caller:
        # such a client may re-read it after a 401 so token rotation does not require a restart.
        self._token_from_deployment = token is None
        self._client = client or httpx.Client(
            base_url=self.base_url,
            timeout=timeout_seconds,
            limits=httpx.Limits(max_connections=1, max_keepalive_connections=1),
        )
        self._owns_client = client is None
        self._request_lock = threading.Lock()

    def close(self) -> None:
        if self._owns_client:
            self._client.close()

    def ping(self) -> None:
        self.status()

    def status(self) -> dict[str, Any]:
        return dict(self._command("status"))

    def commit_frame(
        self,
        *,
        batch_id: str | None = None,
        device_code: str,
        device_type: str,
        group_code: str,
        scan_rate_ms: int,
        ts: int,
        config_version: int,
        values: dict[str, str],
        qualities: dict[str, int] | None,
        latest_fields: dict[str, dict[str, Any]],
    ) -> OutboxRecord:
        data = self._command(
            "frame.commit",
            {
                "batch_id": batch_id or str(uuid4()),
                "device_code": device_code,
                "device_type": device_type,
                "group_code": group_code,
                "scan_rate_ms": scan_rate_ms,
                "ts": ts,
                "config_version": config_version,
                "values": values,
                "qualities": qualities,
                "latest_fields": latest_fields,
            },
        )
        return self._record(data)

    def write_local_latest(
        self,
        device_code: str,
        group_code: str,
        fields: dict[str, dict[str, Any]],
    ) -> None:
        self._command(
            "latest.write",
            {
                "device_code": device_code,
                "group_code": group_code,
                "fields": fields,
            },
        )

    def recover_inflight(self, lease_owner: str) -> int:
        return int(self._command("outbox.recover", {"lease_owner": lease_owner}))

    def upload_interval_ms(self) -> int | None:
        value = self._command("outbox.upload_interval")
        return int(value) if value is not None else None

    def snapshot_target_seq(self) -> int:
        return int(self._command("outbox.snapshot"))

    def claim_batch(
        self,
        *,
        target_seq: int,
        lease_owner: str,
        lease_ms: int,
        max_items: int,
        max_bytes: int,
    ) -> list[OutboxRecord]:
        rows = self._command(
            "outbox.claim",
            {
                "target_seq": target_seq,
                "lease_owner": lease_owner,
                "lease_ms": lease_ms,
                "max_items": max_items,
                "max_bytes": max_bytes,
            },
        )
        return [self._record(row) for row in rows]

    def complete_batch(
        self,
        completions: list[OutboxCompletion],
        lease_owner: str,
    ) -> None:
        self._command(
            "outbox.complete",
            {
                "lease_owner": lease_owner,
                "completions": [
                    {
                        "record": self._record_payload(item.record),
                        "state": item.state,
                        "error": item.error,
                        "http_status": item.http_status,
                    }
                    for item in completions
                ],
            },
        )

    def mark_sent(self, record: OutboxRecord, lease_owner: str) -> None:
        self.complete_batch([OutboxCompletion(record, "sent")], lease_owner)

    def mark_dead(
        self,
        record: OutboxRecord,
        lease_owner: str,
        error: str | None,
    ) -> None:
        self.complete_batch([OutboxCompletion(record, "dead", error)], lease_owner)

    def mark_retry_batch(
        self,
        records: list[tuple[OutboxRecord, int]],
        lease_owner: str,
        *,
        error: str,
        http_status: int | None,
    ) -> None:
        self._command(
            "outbox.retry",
            {
                "lease_owner": lease_owner,
                "error": error,
                "http_status": http_status,
                "records": [
                    {"record": self._record_payload(record), "delay_ms": delay_ms}
                    for record, delay_ms in records
                ],
            },
        )

    def mark_retry(
        self,
        record: OutboxRecord,
        lease_owner: str,
        *,
        error: str,
        http_status: int | None,
        delay_ms: int,
    ) -> None:
        self.mark_retry_batch(
            [(record, delay_ms)],
            lease_owner,
            error=error,
            http_status=http_status,
        )

    def pending_session_switch(self) -> SessionSwitchState | None:
        data = self._command("session.pending")
        return self._session(data) if data else None

    def prepare_session_switch(self) -> SessionSwitchState:
        return self._session(self._command("session.prepare"))

    def complete_session_switch(self, state: SessionSwitchState) -> None:
        self._command(
            "session.complete",
            {
                "old_session_id": state.old_session_id,
                "new_session_id": state.new_session_id,
            },
        )

    def current_session_id(self) -> str:
        return str(self._command("session.current"))

    def maintenance(self, retention_days: int, sent_retention_ms: int | None = None) -> dict[str, Any]:
        return dict(
            self._command("maintenance", {"retention_days": retention_days, "sent_retention_ms": sent_retention_ms})
        )

    def revision(self) -> int:
        return int(self._command("config.revision"))

    def apply(self, snapshot: dict[str, Any]) -> int:
        return int(self._command("config.apply", {"snapshot": snapshot}))

    def refresh_schedule(self, now_ms: int | None = None) -> None:
        self._command("config.refresh_schedule", {"now_ms": now_ms})

    def update_device_status(
        self,
        device_code: str,
        status: int,
        message: str = "",
        event_type: str = "status_update",
    ) -> None:
        self._command(
            "device.status",
            {
                "device_code": device_code,
                "status": status,
                "message": message,
                "event_type": event_type,
            },
        )

    def _command(self, action: str, payload: dict[str, Any] | None = None) -> Any:
        body = {"action": action, "payload": payload or {}}
        encoded = msgspec.json.encode(body)
        headers = {"Content-Type": "application/json"}
        if self._token:
            headers[IPC_TOKEN_HEADER] = self._token
        last_error: Exception | None = None
        for _attempt in range(2):
            try:
                with self._request_lock:
                    response = self._client.post(
                        "/v1/command",
                        content=encoded,
                        headers=headers,
                    )
                response.raise_for_status()
                envelope = msgspec.json.decode(response.content)
                if not isinstance(envelope, dict):
                    raise ValueError("storage response must be an object")
                if not envelope.get("ok"):
                    raise StorageUnavailable(str(envelope.get("error") or "storage command failed"))
                return envelope.get("data")
            except httpx.HTTPStatusError as exc:
                # P2-D：凭据被拒是可判定的确定性失败，既不重试，也不折叠成
                # "unavailable"——运维据此区分「凭据/权限错」与「服务不可达」。
                if exc.response.status_code in (401, 403):
                    # 401 且令牌来自部署文件时，重读一次以吸收轮换（吊销后旧令牌必然失败，
                    # 这里拿到的是新令牌）；403 是角色越权，重读无意义，直接抛出。
                    if (
                        exc.response.status_code == 401
                        and self._token_from_deployment
                        and _attempt == 0
                    ):
                        refreshed = load_client_token(self.role)
                        if refreshed and refreshed != self._token:
                            self._token = refreshed
                            headers[IPC_TOKEN_HEADER] = refreshed
                            continue
                    raise StorageUnauthorized(
                        "Edge Storage rejected the IPC credential "
                        f"(HTTP {exc.response.status_code}, role={self.role or 'unidentified'}): "
                        "check the role token in the Edge Storage IPC token file"
                    ) from exc
                last_error = exc
            except (httpx.HTTPError, ValueError, KeyError) as exc:
                last_error = exc
        raise StorageUnavailable(f"Edge Storage unavailable: {last_error}") from last_error

    @staticmethod
    def _record(data: dict[str, Any]) -> OutboxRecord:
        # B6：``reject_count`` 是判断坏数据隔离预算的唯一依据。字段**缺失**表示对端
        # 是尚未升级的 Edge Storage（旧版只回传 ``attempt_count``）——按存在性判定，
        # 显式降级为 ``None`` 交调用方处理，绝不静默当成 0。
        return OutboxRecord(
            batch_id=str(data["batch_id"]),
            upload_session_id=str(data["upload_session_id"]),
            local_seq=int(data["local_seq"]),
            assignment_epoch=int(data["assignment_epoch"]),
            payload=base64.b64decode(str(data["payload_b64"])),
            attempt_count=int(data.get("attempt_count") or 0),
            reject_count=(
                int(data["reject_count"]) if data.get("reject_count") is not None else None
            ),
        )

    @staticmethod
    def _record_payload(record: OutboxRecord) -> dict[str, Any]:
        return {
            "batch_id": record.batch_id,
            "upload_session_id": record.upload_session_id,
            "local_seq": record.local_seq,
            "assignment_epoch": record.assignment_epoch,
            "payload_b64": base64.b64encode(record.payload).decode("ascii"),
            "attempt_count": record.attempt_count,
            "reject_count": record.reject_count,
        }

    @staticmethod
    def _session(data: dict[str, Any]) -> SessionSwitchState:
        return SessionSwitchState(
            old_session_id=str(data["old_session_id"]),
            new_session_id=str(data["new_session_id"]),
        )
