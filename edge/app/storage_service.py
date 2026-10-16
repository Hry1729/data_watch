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
            # 容量视图仍留给 uploader。取回载荷和重放改由 operator 角色持有。
            "outbox.dead_backlog",
            "status",
        }
    ),
    # 本机运维入口。不开放 maintenance，避免操作员令牌把未处置的 dead 删掉。
    "operator": frozenset(
        {
            "outbox.dead_list",
            "outbox.dead_export",
            "outbox.dead_export_batch",
            "outbox.dead_replay",
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
    """Best-effort ACL tightening: owner + SYSTEM + Administrators only.

    B50/S3：**仅** `/inheritance:r /grant:r` 不足以形成白名单——它只删**继承**的 ACE、只替换
    **所列 SID** 的显式 ACE。安装器用 `icacls /T /grant:r "…:(OI)(CI)RX"` 递归执行时，`OI/CI`
    是目录专用标志，落到**文件**上退化为一条**显式** `BUILTIN\\Users:RX`，`/inheritance:r`
    并不会删掉它。因此这里必须再**显式回收**广域 SID，白名单才真正成立：

      * ``S-1-5-32-545`` BUILTIN\\Users
      * ``S-1-1-0``      Everyone
      * ``S-1-5-11``     Authenticated Users

    不回收的代价是"任意本机用户可读服务令牌"。
    """
    if os.name != "nt":
        return
    grants = ["*S-1-5-18:F", "*S-1-5-32-544:F"]
    owner = os.environ.get("USERNAME")
    if owner:
        grants.append(f"{owner}:F")
    try:
        subprocess.run(
            [
                "icacls",
                str(path),
                "/inheritance:r",
                "/grant:r",
                *grants,
                "/remove:g",
                "*S-1-5-32-545",
                "*S-1-1-0",
                "*S-1-5-11",
            ],
            check=True,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=15,
        )
    except (OSError, subprocess.SubprocessError) as exc:
        logger.warning(
            "Could not restrict permissions on Edge Storage IPC token file %s: %s", path, exc
        )


_TOKEN_ACL_HARDENED: set[str] = set()


def _ensure_missing_role_tokens(path: Path) -> None:
    """给令牌文件补上尚未出现的角色，已有角色的令牌保持不变。"""
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError, UnicodeError):
        return
    tokens = data.get("tokens") if isinstance(data, dict) else None
    if not isinstance(tokens, dict):
        return
    missing = [role for role in IPC_ROLE_ACTIONS if role not in tokens]
    if not missing:
        return
    for role in missing:
        tokens[role] = secrets.token_hex(32)
    data["tokens"] = {role: tokens[role] for role in IPC_ROLE_ACTIONS if role in tokens}
    temporary = path.with_name(path.name + f".roles-{os.getpid()}")
    descriptor = os.open(temporary, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    with os.fdopen(descriptor, "wb") as handle:
        handle.write(json.dumps(data, indent=2).encode("utf-8"))
    os.replace(temporary, path)
    _restrict_token_file_acl(path)
    _TOKEN_ACL_HARDENED.add(str(path))


def _harden_existing_token_file(path: Path) -> None:
    """B50/S3：**既有**令牌文件也要收敛 ACL。

    覆盖升级后，旧版本（或安装器 `/T` 递归授权）留在文件上的宽松授权不会因为"文件已存在"而
    消失，所以"存在即直接返回"的旧路径必须补做一次加固。进程内每个路径只做一次，避免把
    `icacls` 放进热路径。
    """
    key = str(path)
    if key in _TOKEN_ACL_HARDENED:
        return
    _TOKEN_ACL_HARDENED.add(key)
    _restrict_token_file_acl(path)


def _new_token_payload() -> bytes:
    return json.dumps(
        {"version": 1, "tokens": {role: secrets.token_hex(32) for role in IPC_ROLE_ACTIONS}},
        indent=2,
    ).encode("utf-8")


def ensure_token_file(path: Path | None = None, *, rotate: bool = False) -> Path:
    """Return the IPC token file, creating it with fresh per-role tokens on first use.

    ``rotate=True`` regenerates **all** role tokens atomically (temp file + ``os.replace``).
    That is the supported revocation path: a leaked token stops matching as soon as the
    running Storage reloads the file (see ``IpcAuthenticator``) — **no restart required**.
    """
    target = Path(path) if path is not None else default_token_file()
    if target.exists() and not rotate:
        # B50/S3：**既有**文件也必须收敛 ACL。覆盖升级后，旧版本或安装器 `/T` 递归授权留下的
        # 宽松授权（尤其显式 `BUILTIN\Users:RX`）不会因为"文件已存在"而消失；旧实现直接
        # `return target` 会让这些权限永久保留。进程内每路径只做一次（见该函数注释）。
        _harden_existing_token_file(target)
        # 新增角色（operator）补一把令牌，不轮换已经发出的 collector/uploader/agent。
        _ensure_missing_role_tokens(target)
        return target
    target.parent.mkdir(parents=True, exist_ok=True)
    if rotate:
        temporary = target.with_name(target.name + f".rotate-{os.getpid()}")
        descriptor = os.open(temporary, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        with os.fdopen(descriptor, "wb") as handle:
            handle.write(_new_token_payload())
        os.replace(temporary, target)
        _restrict_token_file_acl(target)
        _TOKEN_ACL_HARDENED.add(str(target))
        return target
    try:
        descriptor = os.open(target, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError:
        # Another edge process won the race; its file is just as good（但仍需收敛其 ACL）。
        _harden_existing_token_file(target)
        return target
    with os.fdopen(descriptor, "wb") as handle:
        handle.write(_new_token_payload())
    _restrict_token_file_acl(target)
    _TOKEN_ACL_HARDENED.add(str(target))
    return target


class IpcAuthenticator:
    """Resolves the caller role from a token and enforces the role allowlist.

    The token file is **re-read when it changes**, so rotation/revocation takes effect
    without restarting Edge Storage. The previous behaviour cached tokens for the whole
    process lifetime, which made prompt revocation impossible. The stat check is
    throttled to bound syscalls on the request path.
    """

    _RELOAD_INTERVAL_SECONDS = 1.0

    def __init__(self, tokens: dict[str, str], *, path: Path | None = None) -> None:
        if not tokens:
            raise ValueError("Edge Storage IPC token file contains no tokens")
        self._tokens = tokens
        self._path = Path(path) if path is not None else None
        self._stamp = self._file_stamp()
        self._next_check = 0.0
        self._reload_lock = threading.Lock()

    @classmethod
    def from_file(cls, path: Path | None = None) -> IpcAuthenticator:
        target = ensure_token_file(path)
        return cls(read_role_tokens(target), path=target)

    def _file_stamp(self) -> tuple[int, int] | None:
        if self._path is None:
            return None
        try:
            stat = self._path.stat()
        except OSError:
            return None
        return (stat.st_mtime_ns, stat.st_size)

    def _maybe_reload(self) -> None:
        if self._path is None:
            return
        now = time.monotonic()
        if now < self._next_check:
            return
        self._next_check = now + self._RELOAD_INTERVAL_SECONDS
        stamp = self._file_stamp()
        if stamp is None or stamp == self._stamp:
            return
        with self._reload_lock:
            stamp = self._file_stamp()
            if stamp is None or stamp == self._stamp:
                return
            try:
                tokens = read_role_tokens(self._path)
            except (OSError, ValueError) as exc:
                # Keep the previous tokens rather than failing open or locking everyone out.
                logger.warning("Could not reload Edge Storage IPC tokens from %s: %s", self._path, exc)
                return
            self._tokens = tokens
            self._stamp = stamp
            logger.warning(
                "Edge Storage IPC tokens reloaded from %s; previously issued tokens are no longer accepted",
                self._path,
            )

    def role_for(self, token: str | None) -> str | None:
        self._maybe_reload()
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

    def start(self, timeout_seconds: float | None = 10.0) -> None:
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
        pending = None
        has_pending = False
        try:
            while True:
                try:
                    if has_pending:
                        command, has_pending = pending, False
                    else:
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
                if command.action == "frame.commit":
                    commands = [command]
                    while len(commands) < 32:
                        try:
                            following = self._queue.get_nowait()
                        except Empty:
                            break
                        if following is None or following.action != "frame.commit":
                            pending, has_pending = following, True
                            break
                        commands.append(following)
                    self._commit_frame_commands(outbox, commands)
                else:
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

    def _commit_frame_commands(self, outbox: OutboxStore, commands: list[_Command]) -> None:
        # 批量提交不经过 _dispatch，鉴权层写入的 _actor_role 不能进 outbox 信封。
        frames = []
        for command in commands:
            frame = dict(command.payload)
            frame.pop("_actor_role", None)
            frames.append(frame)
        try:
            results = outbox.commit_frames(frames)
        except Exception as exc:
            logger.exception("Storage frame group commit failed")
            results = [exc] * len(commands)
        for command, result in zip(commands, results):
            try:
                if isinstance(result, Exception):
                    raise result
                command.result = _record_payload(result)
                self._action_counts["frame.commit"] += 1
                self._recent_frames.append((time.monotonic(), len(command.payload.get("values") or {})))
            except Exception as exc:
                self._errors_total += 1
                command.error = exc
                logger.error("Storage frame command failed: %s", exc)
            finally:
                self._latencies_ms.append((time.perf_counter() - command.queued_at) * 1000)
                command.done.set()

    def _dispatch(
        self,
        outbox: OutboxStore,
        config: ConfigSnapshotStore,
        action: str,
        payload: dict[str, Any],
    ) -> Any:
        # 角色由鉴权层写入，调用方自己带的同名字段不能冒充。
        payload = dict(payload)
        actor_role = str(payload.pop("_actor_role", "") or "storage")
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
            sent_retention = payload.get("sent_retention_ms")
            dead_days = payload.get("dead_retention_days")
            dead_max = payload.get("dead_max_bytes")
            return outbox.maintenance(
                int(payload["retention_days"]),
                None if sent_retention is None else int(sent_retention),
                None if dead_days is None else int(dead_days),
                None if dead_max is None else int(dead_max),
                node_uuid=None if payload.get("node_uuid") is None else str(payload.get("node_uuid")),
                actor_role=actor_role,
            )
        # B6：dead 载荷的恢复入口。operator 可列出、导出、批量导出和重放；
        # uploader 只保留 dead_backlog。审计里的 actor_role 来自已鉴权的角色。
        if action == "outbox.dead_backlog":
            return outbox.dead_backlog()
        if action == "outbox.dead_list":
            return outbox.list_dead(
                limit=int(payload.get("limit", 50)),
                offset=int(payload.get("offset", 0)),
                device_code=payload.get("device_code"),
            )
        if action == "outbox.dead_export":
            record = outbox.export_dead(
                str(payload["batch_id"]),
                str(payload["upload_session_id"]),
                int(payload["local_seq"]),
                actor_role=actor_role,
            )
            if record is None:
                raise ValueError("no dead record matches the given identity")
            return record
        if action == "outbox.dead_export_batch":
            export_format = str(payload.get("format") or "")
            destination = payload.get("out")
            if not destination:
                raise ValueError("out path is required")
            since_ms = payload.get("since_ms")
            until_ms = payload.get("until_ms")
            return outbox.export_dead_batch(
                str(destination),
                fmt=export_format,
                device_code=payload.get("device_code"),
                since_ms=None if since_ms is None else int(since_ms),
                until_ms=None if until_ms is None else int(until_ms),
                last_error=payload.get("last_error"),
                node_uuid=None if payload.get("node_uuid") is None else str(payload.get("node_uuid")),
                actor_role=actor_role,
            )
        if action == "outbox.dead_replay":
            session_id = payload.get("upload_session_id")
            local_seq = payload.get("local_seq")
            return outbox.replay_dead(
                str(payload["batch_id"]),
                None if session_id is None else str(session_id),
                None if local_seq is None else int(local_seq),
                window_ms=payload.get("window_ms"),
                actor_role=actor_role,
            )
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
        authorized = dict(payload)
        authorized["_actor_role"] = role
        self._execute(action, authorized)

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
