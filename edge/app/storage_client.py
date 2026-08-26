from __future__ import annotations

import base64
import os
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


class StorageUnavailable(RuntimeError):
    pass


def storage_base_url() -> str:
    return os.environ.get("DATAWATCH_EDGE_STORAGE_URL", "http://127.0.0.1:7073").rstrip("/")


class EdgeStorageClient:
    """Synchronous local IPC client used by edge worker processes."""

    def __init__(
        self,
        base_url: str | None = None,
        *,
        timeout_seconds: float = 35.0,
        client: httpx.Client | None = None,
    ) -> None:
        self.base_url = (base_url or storage_base_url()).rstrip("/")
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

    def maintenance(self, retention_days: int) -> dict[str, Any]:
        return dict(
            self._command("maintenance", {"retention_days": retention_days})
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
        last_error: Exception | None = None
        for _attempt in range(2):
            try:
                with self._request_lock:
                    response = self._client.post(
                        "/v1/command",
                        content=encoded,
                        headers={"Content-Type": "application/json"},
                    )
                response.raise_for_status()
                envelope = msgspec.json.decode(response.content)
                if not isinstance(envelope, dict):
                    raise ValueError("storage response must be an object")
                if not envelope.get("ok"):
                    raise StorageUnavailable(str(envelope.get("error") or "storage command failed"))
                return envelope.get("data")
            except (httpx.HTTPError, ValueError, KeyError) as exc:
                last_error = exc
        raise StorageUnavailable(f"Edge Storage unavailable: {last_error}") from last_error

    @staticmethod
    def _record(data: dict[str, Any]) -> OutboxRecord:
        return OutboxRecord(
            batch_id=str(data["batch_id"]),
            upload_session_id=str(data["upload_session_id"]),
            local_seq=int(data["local_seq"]),
            assignment_epoch=int(data["assignment_epoch"]),
            payload=base64.b64decode(str(data["payload_b64"])),
            attempt_count=int(data.get("attempt_count") or 0),
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
        }

    @staticmethod
    def _session(data: dict[str, Any]) -> SessionSwitchState:
        return SessionSwitchState(
            old_session_id=str(data["old_session_id"]),
            new_session_id=str(data["new_session_id"]),
        )
