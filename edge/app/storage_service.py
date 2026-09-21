from __future__ import annotations

import base64
from collections import Counter, deque
from dataclasses import dataclass, field
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import logging
from pathlib import Path
from queue import Empty, Full, Queue
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


logger = logging.getLogger("edge.storage")


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

    def __init__(self, address: tuple[str, int], engine: StorageEngine) -> None:
        self.engine = engine
        super().__init__(address, StorageRequestHandler)


class StorageRequestHandler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    server: StorageHttpServer

    def do_GET(self) -> None:
        if self.path == "/healthz":
            self._write_json(HTTPStatus.OK, {"status": "ok"})
            return
        if self.path == "/v1/status":
            self._execute("status", {})
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
            self._execute(str(body.get("action") or ""), dict(body.get("payload") or {}))
        except (ValueError, TypeError, msgspec.DecodeError) as exc:
            self._write_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": str(exc)})

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
    }


def _record(data: dict[str, Any]) -> OutboxRecord:
    return OutboxRecord(
        batch_id=str(data["batch_id"]),
        upload_session_id=str(data["upload_session_id"]),
        local_seq=int(data["local_seq"]),
        assignment_epoch=int(data["assignment_epoch"]),
        payload=base64.b64decode(str(data["payload_b64"])),
        attempt_count=int(data.get("attempt_count") or 0),
    )


def _session_payload(state: SessionSwitchState | None) -> dict[str, str] | None:
    if state is None:
        return None
    return {
        "old_session_id": state.old_session_id,
        "new_session_id": state.new_session_id,
    }
