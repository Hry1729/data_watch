from __future__ import annotations

import base64
from collections import Counter, deque
from dataclasses import dataclass, field
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import hmac
import json
import logging
import os
from pathlib import Path
from queue import Empty, Full, Queue
import secrets
import subprocess
import threading
import time
from typing import Any

import msgspec

from edge.app.config_snapshot import ConfigSnapshotStore
from edge.app.outbox import (
    OutboxCompletion,
    OutboxRecord,
    OutboxStore,
    SessionSwitchState,
)
from edge.app.storage_client import (
    IPC_TOKEN_HEADER,
    default_token_file,
    read_role_tokens,
)


logger = logging.getLogger("edge.storage")


# --- Edge Storage IPC trust boundary -------------------------------------
#
# The IPC endpoint is reachable by every process on the host, so a loopback-only
# bind address is not caller authentication. Callers present a per-role token
# read from a deployment-local file kept readable only by the edge accounts.
#
# Least-privilege command surface per role, derived from the actual call sites:
#   collector - edge/collector_main.py (EdgeOutboxWriter + the EdgeConfigManager
#               in edge/app/config_snapshot.py)
#   uploader  - edge/uploader_main.py (edge/app/uploader.py)
#   agent     - the Agent edge-sync client (agent/app/services/edge_storage_client.py)
IPC_ROLE_ACTIONS: dict[str, frozenset[str]] = {
    "collector": frozenset(
        {
            "frame.commit",
            "latest.write",
            "outbox.snapshot",
            "config.refresh_schedule",
            "device.status",
        }
    ),
    "uploader": frozenset(
        {
            "outbox.recover",
            "outbox.upload_interval",
            "outbox.snapshot",
            "outbox.claim",
            "outbox.complete",
            "outbox.retry",
            "session.pending",
            "session.prepare",
            "session.complete",
            "session.current",
            "maintenance",
            "status",
        }
    ),
    "agent": frozenset(
        {
            "status",
            "config.revision",
            "config.apply",
            "config.refresh_schedule",
        }
    ),
}
# No catch-all role exists on purpose: a caller that cannot be identified gets no
# token and is rejected, rather than being handed an implicit super-role.


def _restrict_token_file_acl(path: Path) -> None:
    """Best-effort ACL tightening: owner + SYSTEM + Administrators only."""
    if os.name != "nt":
        return
    grants = ["*S-1-5-18:F", "*S-1-5-32-544:F"]
    owner = os.environ.get("USERNAME")
    if owner:
        grants.append(f"{owner}:F")
    try:
        subprocess.run(
            ["icacls", str(path), "/inheritance:r", "/grant:r", *grants],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=15,
        )
    except (OSError, subprocess.SubprocessError) as exc:
        logger.warning(
            "Could not restrict permissions on Edge Storage IPC token file %s: %s", path, exc
        )


def ensure_token_file(path: Path | None = None) -> Path:
    """Return the IPC token file, creating it with fresh per-role tokens on first use."""
    target = Path(path) if path is not None else default_token_file()
    if target.exists():
        return target
    target.parent.mkdir(parents=True, exist_ok=True)
    payload = json.dumps(
        {"version": 1, "tokens": {role: secrets.token_hex(32) for role in IPC_ROLE_ACTIONS}},
        indent=2,
    ).encode("utf-8")
    try:
        descriptor = os.open(target, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError:
        # Another edge process won the race; its file is just as good.
        return target
    with os.fdopen(descriptor, "wb") as handle:
        handle.write(payload)
    _restrict_token_file_acl(target)
    return target


class IpcAuthenticator:
    """Resolves the caller role from a token and enforces the role allowlist."""

    def __init__(self, tokens: dict[str, str]) -> None:
        if not tokens:
            raise ValueError("Edge Storage IPC token file contains no tokens")
        self._tokens = tokens

    @classmethod
    def from_file(cls, path: Path | None = None) -> IpcAuthenticator:
        return cls(read_role_tokens(ensure_token_file(path)))

    def role_for(self, token: str | None) -> str | None:
        if not token:
            return None
        for role, expected in self._tokens.items():
            if hmac.compare_digest(token, expected):
                return role
        return None

    def permits(self, role: str, action: str) -> bool:
        return action in IPC_ROLE_ACTIONS.get(role, frozenset())


@dataclass(slots=True)
class _Command:
    action: str
    payload: dict[str, Any]
    queued_at: float = field(default_factory=time.perf_counter)
    done: threading.Event = field(default_factory=threading.Event)
    result: Any = None
    error: Exception | None = None


class StorageQueueFull(RuntimeError):
    pass


class StorageEngine:
    def __init__(
        self,
        database_path: str | Path,
        *,
        max_queue: int = 2048,
        shutdown_timeout_seconds: float = 30.0,
    ) -> None:
        self.database_path = Path(database_path)
        self.max_queue = max_queue
        if shutdown_timeout_seconds <= 0:
            raise ValueError("shutdown_timeout_seconds must be positive")
        self.shutdown_timeout_seconds = shutdown_timeout_seconds
        self._queue: Queue[_Command | None] = Queue(maxsize=max_queue)
        self._ready = threading.Event()
        self._stopped = threading.Event()
        self._lifecycle_lock = threading.Lock()
        self._closing = False
        self._started = False
        self._sentinel_requested = threading.Event()
        self._sentinel_enqueued = threading.Event()
        self._startup_error: Exception | None = None
        # Keep the process alive until accepted commands are drained; close()
        # reports a timeout rather than allowing a daemon thread to vanish.
        self._worker = threading.Thread(target=self._run, name="edge-storage-writer", daemon=False)
        self._latencies_ms: deque[float] = deque(maxlen=2000)
        self._recent_frames: deque[tuple[float, int]] = deque(maxlen=10000)
        self._action_counts: Counter[str] = Counter()
        self._errors_total = 0
        self._started_at = time.time()

    def start(self, timeout_seconds: float = 10.0) -> None:
        with self._lifecycle_lock:
            if self._closing:
                raise RuntimeError("Edge Storage is stopping")
            if self._started:
                return
            self._started = True
        self._worker.start()
        if not self._ready.wait(timeout_seconds):
            raise RuntimeError("Edge Storage writer did not become ready")
        if self._startup_error is not None:
            raise RuntimeError(f"Edge Storage initialization failed: {self._startup_error}")

    def close(self, timeout_seconds: float | None = None) -> None:
        with self._lifecycle_lock:
            if not self._closing:
                self._closing = True
                self._stopped.set()
                try:
                    # The lock makes this enqueue atomic with submit(): every
                    # accepted command is ahead of the sentinel.
                    self._queue.put_nowait(None)
                    self._sentinel_enqueued.set()
                except Full:
                    # Do not block a shutdown behind a full queue. The worker
                    # inserts the sentinel only after it has drained the queue.
                    self._sentinel_requested.set()
        if self._started:
            timeout = self.shutdown_timeout_seconds if timeout_seconds is None else timeout_seconds
            if timeout <= 0:
                raise ValueError("shutdown timeout must be positive")
            self._worker.join(timeout=timeout)
            if self._worker.is_alive():
                logger.critical(
                    "Edge Storage shutdown timed out after %.3fs; accepted commands remain in the writer",
                    timeout,
                )
                raise TimeoutError("Edge Storage writer did not stop before shutdown timeout")

    def submit(
        self,
        action: str,
        payload: dict[str, Any] | None = None,
        *,
        timeout_seconds: float = 30.0,
    ) -> Any:
        if timeout_seconds <= 0:
            raise ValueError("command timeout must be positive")
        deadline = time.monotonic() + timeout_seconds
        with self._lifecycle_lock:
            if not self._started:
                raise RuntimeError("Edge Storage has not started")
            if self._closing:
                raise RuntimeError("Edge Storage is stopping")
            command = _Command(action=action, payload=payload or {})
            try:
                # A full queue is transient backpressure, not permission to
                # discard a command that already reached the storage process.
                self._queue.put(command, timeout=timeout_seconds)
            except Full as exc:
                raise StorageQueueFull(
                    "Edge Storage write queue remained full until command timeout"
                ) from exc
        remaining = max(0.0, deadline - time.monotonic())
        if not command.done.wait(remaining):
            raise TimeoutError(f"Edge Storage command timed out: {action}")
        if command.error is not None:
            raise command.error
        return command.result

    def _run(self) -> None:
        outbox: OutboxStore | None = None
        config: ConfigSnapshotStore | None = None
        try:
            outbox = OutboxStore(self.database_path)
            config = ConfigSnapshotStore(self.database_path)
        except Exception as exc:
            self._startup_error = exc
            self._ready.set()
            return
        self._ready.set()
        try:
            while True:
                try:
                    command = self._queue.get(timeout=0.5)
                except Empty:
                    if self._sentinel_requested.is_set() and not self._sentinel_enqueued.is_set():
                        try:
                            self._queue.put_nowait(None)
                            self._sentinel_enqueued.set()
                        except Full:
                            continue
                    continue
                if command is None:
                    break
                started = time.perf_counter()
                try:
                    command.result = self._dispatch(outbox, config, command.action, command.payload)
                    self._action_counts[command.action] += 1
                except Exception as exc:
                    self._errors_total += 1
                    command.error = exc
                    logger.exception("Storage command failed: %s", command.action)
                finally:
                    if command.action == "frame.commit":
                        self._latencies_ms.append(
                            (time.perf_counter() - command.queued_at) * 1000
                        )
                    command.done.set()
                if (
                    self._sentinel_requested.is_set()
                    and not self._sentinel_enqueued.is_set()
                    and self._queue.empty()
                ):
                    try:
                        self._queue.put_nowait(None)
                        self._sentinel_enqueued.set()
                    except Full:
                        pass
        finally:
            config.close()
            outbox.close()

    def _dispatch(
        self,
        outbox: OutboxStore,
        config: ConfigSnapshotStore,
        action: str,
        payload: dict[str, Any],
    ) -> Any:
        if action == "frame.commit":
            record = outbox.commit_frame(**payload)
            point_count = len(payload.get("values") or {})
            self._recent_frames.append((time.monotonic(), point_count))
            return _record_payload(record)
        if action == "latest.write":
            outbox.write_local_latest(
                str(payload["device_code"]),
                str(payload["group_code"]),
                dict(payload.get("fields") or {}),
            )
            return None
        if action == "outbox.recover":
            return outbox.recover_inflight(str(payload["lease_owner"]))
        if action == "outbox.upload_interval":
            return outbox.upload_interval_ms()
        if action == "outbox.snapshot":
            return outbox.snapshot_target_seq()
        if action == "outbox.claim":
            return [_record_payload(row) for row in outbox.claim_batch(**payload)]
        if action == "outbox.complete":
            completions = [
                OutboxCompletion(
                    _record(item["record"]),
                    str(item["state"]),
                    item.get("error"),
                    http_status=(
                        int(item["http_status"])
                        if item.get("http_status") is not None
                        else None
                    ),
                )
                for item in payload.get("completions") or []
            ]
            outbox.complete_batch(completions, str(payload["lease_owner"]))
            return None
        if action == "outbox.retry":
            records = [
                (_record(item["record"]), int(item["delay_ms"]))
                for item in payload.get("records") or []
            ]
            outbox.mark_retry_batch(
                records,
                str(payload["lease_owner"]),
                error=str(payload.get("error") or ""),
                http_status=(
                    int(payload["http_status"])
                    if payload.get("http_status") is not None
                    else None
                ),
            )
            return None
        if action == "session.pending":
            return _session_payload(outbox.pending_session_switch())
        if action == "session.prepare":
            return _session_payload(outbox.prepare_session_switch())
        if action == "session.complete":
            outbox.complete_session_switch(
                SessionSwitchState(
                    old_session_id=str(payload["old_session_id"]),
                    new_session_id=str(payload["new_session_id"]),
                )
            )
            return None
        if action == "session.current":
            return outbox.current_session_id()
        if action == "maintenance":
            return outbox.maintenance(int(payload["retention_days"]), payload.get("sent_retention_ms"))
        if action == "config.revision":
            return config.revision()
        if action == "config.apply":
            return config.apply(dict(payload["snapshot"]))
        if action == "config.refresh_schedule":
            config.refresh_schedule(payload.get("now_ms"))
            return None
        if action == "device.status":
            config.update_device_status(
                str(payload["device_code"]),
                int(payload["status"]),
                str(payload.get("message") or ""),
                str(payload.get("event_type") or "status_update"),
            )
            return None
        if action == "status":
            return {**outbox.operational_status(), **self._runtime_status()}
        raise ValueError(f"unknown storage action: {action}")

    def _runtime_status(self) -> dict[str, Any]:
        now = time.monotonic()
        while self._recent_frames and now - self._recent_frames[0][0] > 60:
            self._recent_frames.popleft()
        window = max(1.0, min(60.0, now - (self._recent_frames[0][0] if self._recent_frames else now)))
        latencies = sorted(self._latencies_ms)
        p99_index = max(0, min(len(latencies) - 1, int(len(latencies) * 0.99) - 1))
        return {
            "writer_queue_depth": self._queue.qsize(),
            "writer_queue_capacity": self.max_queue,
            "commands_total": sum(self._action_counts.values()),
            "command_counts": dict(self._action_counts),
            "errors_total": self._errors_total,
            "commit_p99_ms": round(latencies[p99_index], 3) if latencies else 0.0,
            "frames_per_second": round(len(self._recent_frames) / window, 2),
            "points_per_second": round(
                sum(points for _stamp, points in self._recent_frames) / window,
                2,
            ),
            "storage_uptime_seconds": max(0, int(time.time() - self._started_at)),
        }


class StorageHttpServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(
        self,
        address: tuple[str, int],
        engine: StorageEngine,
        authenticator: IpcAuthenticator | None = None,
    ) -> None:
        self.engine = engine
        # Default to the deployment token file so an endpoint cannot be started
        # without authentication by omitting an argument.
        self.authenticator = authenticator or IpcAuthenticator.from_file()
        super().__init__(address, StorageRequestHandler)


class StorageRequestHandler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    server: StorageHttpServer

    def do_GET(self) -> None:
        if self.path == "/healthz":
            # Liveness only: no storage state is read or written.
            self._write_json(HTTPStatus.OK, {"status": "ok"})
            return
        if self.path == "/v1/status":
            self._authorized_execute("status", {})
            return
        self._write_json(HTTPStatus.NOT_FOUND, {"ok": False, "error": "not found"})

    def do_POST(self) -> None:
        if self.path != "/v1/command":
            self._write_json(HTTPStatus.NOT_FOUND, {"ok": False, "error": "not found"})
            return
        try:
            length = int(self.headers.get("Content-Length") or 0)
            if length <= 0 or length > 32 * 1024 * 1024:
                raise ValueError("invalid request size")
            body = msgspec.json.decode(self.rfile.read(length))
            if not isinstance(body, dict):
                raise ValueError("request body must be an object")
            self._authorized_execute(str(body.get("action") or ""), dict(body.get("payload") or {}))
        except (ValueError, TypeError, msgspec.DecodeError) as exc:
            self._write_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": str(exc)})

    def _authorized_execute(self, action: str, payload: dict[str, Any]) -> None:
        role = self.server.authenticator.role_for(self.headers.get(IPC_TOKEN_HEADER))
        if role is None:
            self._write_json(
                HTTPStatus.UNAUTHORIZED,
                {"ok": False, "error": "missing or invalid Edge Storage IPC token"},
            )
            return
        if not self.server.authenticator.permits(role, action):
            self._write_json(
                HTTPStatus.FORBIDDEN,
                {"ok": False, "error": f"role {role!r} is not permitted to run action {action!r}"},
            )
            return
        self._execute(action, payload)

    def _execute(self, action: str, payload: dict[str, Any]) -> None:
        try:
            result = self.server.engine.submit(action, payload)
        except StorageQueueFull as exc:
            self._write_json(HTTPStatus.SERVICE_UNAVAILABLE, {"ok": False, "error": str(exc)})
        except (ValueError, KeyError) as exc:
            self._write_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": str(exc)})
        except Exception as exc:
            self._write_json(HTTPStatus.INTERNAL_SERVER_ERROR, {"ok": False, "error": str(exc)})
        else:
            self._write_json(HTTPStatus.OK, {"ok": True, "data": result})

    def _write_json(self, status: HTTPStatus, payload: dict[str, Any]) -> None:
        encoded = msgspec.json.encode(payload)
        self.send_response(int(status))
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(encoded)))
        self.end_headers()
        self.wfile.write(encoded)

    def log_message(self, format: str, *args: Any) -> None:
        logger.debug(format, *args)


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


def _record(data: dict[str, Any]) -> OutboxRecord:
    # B6：``reject_count`` 由 claim 回传给 Uploader 作为隔离判定依据；字段缺失
    # （旧版客户端）按存在性判成 ``None``，绝不静默当成 0。
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


def _session_payload(state: SessionSwitchState | None) -> dict[str, str] | None:
    if state is None:
        return None
    return {
        "old_session_id": state.old_session_id,
        "new_session_id": state.new_session_id,
    }
