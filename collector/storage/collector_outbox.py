"""集中式采集 Redis 写入失败的持久化重试队列（DW-01）。

集中式 Collector 直写 Redis 若发生异常（网络中断、Redis 拒绝写入或瞬时不可用），
将采集到的原始点位数据、原始采样时间戳持久化保存在本地 SQLite 队列中。
待 Redis 恢复后，重试队列能够按原始时间戳重放数据，并在写入成功后安全清理，
同时具备容量上限（磁盘/记录数限制）以及拒绝溢出保护。
"""

from __future__ import annotations

import logging
import os
import sqlite3
import threading
import time
import shutil
from collections import deque
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any
from uuid import uuid4

import msgspec

logger = logging.getLogger("collector.retry_outbox")

COLLECTOR_OUTBOX_SCHEMA = """
CREATE TABLE IF NOT EXISTS collector_retry_outbox (
    id TEXT PRIMARY KEY,
    device_code TEXT NOT NULL,
    group_code TEXT NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    ts_ms INTEGER NOT NULL,
    config_version INTEGER NOT NULL,
    device_type TEXT NOT NULL,
    frame_json BLOB NOT NULL,
    stream_values_json BLOB NOT NULL,
    stream_qualities_json BLOB,
    latest_fields_json BLOB NOT NULL,
    created_at INTEGER NOT NULL,
    attempts INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_retry_created ON collector_retry_outbox(created_at);
CREATE TABLE IF NOT EXISTS collector_retry_outbox_v2 (
    id TEXT PRIMARY KEY,
    session_id TEXT NOT NULL,
    seq INTEGER NOT NULL,
    device_code TEXT NOT NULL,
    group_code TEXT NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    ts_ms INTEGER NOT NULL,
    config_version INTEGER NOT NULL,
    device_type TEXT NOT NULL,
    frame_json BLOB NOT NULL,
    stream_values_json BLOB NOT NULL,
    stream_qualities_json BLOB,
    latest_fields_json BLOB NOT NULL,
    created_at INTEGER NOT NULL,
    attempts INTEGER NOT NULL DEFAULT 0,
    UNIQUE(session_id, seq)
);
CREATE INDEX IF NOT EXISTS idx_retry_v2_created ON collector_retry_outbox_v2(created_at);
CREATE TRIGGER IF NOT EXISTS block_legacy_insert_with_v2_backlog
BEFORE INSERT ON collector_retry_outbox
WHEN EXISTS (SELECT 1 FROM collector_retry_outbox_v2)
BEGIN
    SELECT RAISE(ABORT, 'legacy rollback blocked: upgrade writer or drain v2 backlog');
END;
"""

# 拒收缺口台账，**独立数据库**。
# 有限缓冲触顶时拒绝新帧是设计好的保护策略，但绝不能只保住旧积压就当作没事：
# 被拒收的采样必须能追溯到设备、分组、采样周期、时间范围、数量和原因。
# 之所以用独立文件：主 outbox 触顶的原因正是它自己写不下，
# 把缺口记在同一个库里必然跟着写失败，等于没有记录。
# 每次启动会 DROP 再重建（见 _init_reject_log），因此它
#   * 不依赖主库容量，主库满了仍能写；
#   * 不随运行时长无限增长，最坏就是每个 (设备,分组,周期,原因) 一行；
#   * 生命周期限定在"本次运行"，避免陈旧行被误读成当前缺口。
REJECT_LOG_SCHEMA = """
CREATE TABLE IF NOT EXISTS collector_rejected_gaps (
    device_code TEXT NOT NULL,
    group_code TEXT NOT NULL,
    device_type TEXT NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    reason TEXT NOT NULL,
    first_ts_ms INTEGER NOT NULL,
    last_ts_ms INTEGER NOT NULL,
    frames INTEGER NOT NULL,
    first_rejected_at INTEGER NOT NULL,
    last_rejected_at INTEGER NOT NULL,
    PRIMARY KEY (device_code, group_code, scan_rate_ms, reason)
);
"""

_PAYLOAD_SIZE_SQL = (
    "length(frame_json) + length(stream_values_json) + "
    "COALESCE(length(stream_qualities_json), 0) + length(latest_fields_json)"
)
_REPLAY_COLUMNS = (
    "id, device_code, group_code, scan_rate_ms, ts_ms, config_version, "
    "device_type, frame_json, stream_values_json, stream_qualities_json, "
    "latest_fields_json, created_at"
)


@dataclass
class _InsertRequest:
    row_id: str
    params: tuple[Any, ...]
    payload_bytes: int
    done: threading.Event = field(default_factory=threading.Event)
    error: BaseException | None = None


class CollectorRetryOutbox:
    _MAX_COMMIT_FRAMES = 50
    _MAX_COMMIT_BYTES = 1024 * 1024
    _COMMIT_WINDOW_S = 0.005
    _MAX_WAITING_WRITES = 256
    _MAX_WAITING_BYTES = 8 * 1024 * 1024
    _MAX_SINGLE_FRAME_BYTES = 1024 * 1024

    def __init__(
        self,
        db_path: str | Path,
        *,
        max_records: int = 50_000,
        metrics=None,
        on_commit=None,
    ) -> None:
        self.path = Path(db_path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self._acquire_owner()
        self.max_records = max_records
        self._metrics = metrics
        self._on_commit = on_commit
        self._lock = threading.RLock()
        self._replay_lock = threading.Lock()
        self._write_condition = threading.Condition()
        self._closed = False
        self._worker_error: BaseException | None = None
        self._write_queue: deque[_InsertRequest] = deque()
        self._waiting_bytes = 0
        # A new session on every start also isolates new frames after a backup restore.
        self._session_id = str(uuid4())
        self._next_seq = 1
        try:
            self._conn = sqlite3.connect(
                self.path,
                timeout=5,
                isolation_level=None,
                check_same_thread=False,
            )
            self._conn.row_factory = sqlite3.Row
            self._init_db()
            self._init_reject_log()
            self._count, self._payload_bytes, self._oldest_ms = self._conn.execute(
                "SELECT SUM(n), SUM(bytes), MIN(oldest) FROM ("
                f"SELECT COUNT(*) AS n, COALESCE(SUM({_PAYLOAD_SIZE_SQL}), 0) AS bytes, "
                "MIN(created_at) AS oldest FROM collector_retry_outbox UNION ALL "
                f"SELECT COUNT(*) AS n, COALESCE(SUM({_PAYLOAD_SIZE_SQL}), 0) AS bytes, "
                "MIN(created_at) AS oldest FROM collector_retry_outbox_v2)"
            ).fetchone()
            self._writer_thread = threading.Thread(target=self._write_loop, name="collector-sqlite", daemon=True)
            self._writer_thread.start()
        except BaseException:
            if hasattr(self, "_conn"):
                self._conn.close()
            if getattr(self, "_reject_conn", None) is not None:
                self._reject_conn.close()
            self._owner_file.close()
            raise

    def _acquire_owner(self) -> None:
        # Keep the lock file: unlinking it permits another process to lock a
        # different inode while the original owner is still running.
        self._owner_file = self.path.with_name(self.path.name + ".owner.lock").open("a+b")
        try:
            self._owner_file.seek(0, 2)
            if self._owner_file.tell() == 0:
                self._owner_file.write(b"0")
                self._owner_file.flush()
            self._owner_file.seek(0)
            if os.name == "nt":
                import msvcrt
                msvcrt.locking(self._owner_file.fileno(), msvcrt.LK_NBLCK, 1)
            else:
                import fcntl
                fcntl.flock(self._owner_file.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
        except OSError as exc:
            self._owner_file.close()
            raise RuntimeError("Collector durable outbox already owned by another process") from exc

    def _init_db(self) -> None:
        with self._lock:
            self._conn.execute("PRAGMA journal_mode=WAL")
            self._conn.execute("PRAGMA synchronous=FULL")
            page_size = self._conn.execute("PRAGMA page_size").fetchone()[0]
            self._conn.execute(f"PRAGMA max_page_count={1024 * 1024 * 1024 // page_size}")
            self._conn.executescript(COLLECTOR_OUTBOX_SCHEMA)

    def close(self) -> None:
        with self._write_condition:
            if self._closed:
                return
            self._closed = True
            self._write_condition.notify_all()
        self._writer_thread.join()
        with self._lock:
            try:
                self._conn.close()
            finally:
                if getattr(self, "_reject_conn", None) is not None:
                    try:
                        self._reject_conn.close()
                    finally:
                        self._reject_conn = None
                self._owner_file.close()

    def record_failure(
        self,
        *,
        device_code: str,
        group_code: str,
        scan_rate_ms: int,
        ts_ms: int,
        config_version: int,
        device_type: str,
        frame: dict[str, Any],
        stream_values: dict[str, str],
        stream_qualities: dict[str, int] | None,
        latest_fields: dict[str, dict[str, Any]],
    ) -> str:
        """Return only after the complete SQLite transaction has committed."""
        now = int(time.time() * 1000)
        row_id = str(uuid4())
        encoded_frame = msgspec.json.encode(frame)
        encoded_values = msgspec.json.encode(stream_values)
        encoded_qualities = msgspec.json.encode(stream_qualities) if stream_qualities else None
        encoded_latest = msgspec.json.encode(latest_fields)
        request = _InsertRequest(
            row_id=row_id,
            params=(row_id, device_code, group_code, scan_rate_ms, ts_ms,
                    config_version, device_type, encoded_frame, encoded_values,
                    encoded_qualities, encoded_latest, now),
            payload_bytes=sum(map(len, (encoded_frame, encoded_values, encoded_latest)))
                          + (len(encoded_qualities) if encoded_qualities else 0),
        )
        started = time.perf_counter()
        if request.payload_bytes > self._MAX_SINGLE_FRAME_BYTES:
            self._reject(request, "Collector frame exceeds durable queue byte limit")
            raise RuntimeError("Collector frame exceeds durable queue byte limit")
        with self._write_condition:
            deadline = time.monotonic() + 5
            while (len(self._write_queue) >= self._MAX_WAITING_WRITES
                   or self._waiting_bytes + request.payload_bytes > self._MAX_WAITING_BYTES):
                if self._closed or self._worker_error is not None:
                    raise RuntimeError("Collector durable queue closed") from self._worker_error
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    self._reject(request, "Collector durable write queue full")
                    raise RuntimeError("Collector durable write queue full")
                self._write_condition.wait(remaining)
            if self._closed or self._worker_error is not None:
                raise RuntimeError("Collector durable queue closed") from self._worker_error
            self._write_queue.append(request)
            self._waiting_bytes += request.payload_bytes
            self._write_condition.notify_all()
        request.done.wait()
        if request.error is not None:
            raise request.error
        if self._metrics is not None:
            try:
                self._metrics.record_timing("outbox_accept_ms", (time.perf_counter() - started) * 1000)
            except Exception:
                logger.exception("outbox acceptance metric failed")
        return row_id

    def _write_loop(self) -> None:
        batch: list[_InsertRequest] = []
        try:
            while True:
                with self._write_condition:
                    while not self._write_queue and not self._closed:
                        self._write_condition.wait()
                    if not self._write_queue:
                        return
                    first = self._write_queue.popleft()
                    batch = [first]
                    batch_bytes = first.payload_bytes
                    self._waiting_bytes -= first.payload_bytes
                    self._write_condition.notify_all()
                    deadline = time.monotonic() + self._COMMIT_WINDOW_S
                    while len(batch) < self._MAX_COMMIT_FRAMES:
                        if self._write_queue:
                            candidate = self._write_queue[0]
                            if batch_bytes + candidate.payload_bytes > self._MAX_COMMIT_BYTES:
                                break
                            batch.append(self._write_queue.popleft())
                            batch_bytes += candidate.payload_bytes
                            self._waiting_bytes -= candidate.payload_bytes
                            self._write_condition.notify_all()
                            continue
                        remaining = deadline - time.monotonic()
                        if self._closed or remaining <= 0:
                            break
                        self._write_condition.wait(remaining)
                try:
                    accepted = self._commit_batch(batch)
                except BaseException as exc:
                    for item in batch:
                        self._reject(item, str(exc))
                        item.error = exc
                        item.done.set()
                else:
                    if accepted and self._on_commit is not None:
                        try:
                            self._on_commit()
                        except Exception:
                            logger.exception("outbox commit notification failed")
                    for item in batch[:accepted]:
                        item.done.set()
                    batch = batch[accepted:]
                    for item in batch:
                        reason = "Collector durable queue full; existing backlog preserved"
                        self._reject(item, reason)
                        item.error = RuntimeError(reason)
                        item.done.set()
                batch = []
        except BaseException as exc:
            logger.exception("collector SQLite writer stopped unexpectedly")
            with self._write_condition:
                self._worker_error = exc
                waiting = list(self._write_queue)
                self._write_queue.clear()
                self._waiting_bytes = 0
                self._write_condition.notify_all()
            for item in batch + waiting:
                item.error = exc
                item.done.set()

    def _commit_batch(self, batch: list[_InsertRequest]) -> int:
        with self._lock:
            available = max(0, self.max_records - self._count)
            accepted = min(len(batch), available)
            if accepted:
                if shutil.disk_usage(self.path.parent).free < 256 * 1024 * 1024:
                    raise RuntimeError("Collector durable disk reserve exhausted; existing backlog preserved")
                else:
                    started = time.perf_counter()
                    self._conn.execute("BEGIN IMMEDIATE")
                    try:
                        if self._next_seq + accepted - 1 > 2**53 - 1:
                            raise RuntimeError("Collector session sequence exhausted")
                        self._conn.executemany(
                            """INSERT INTO collector_retry_outbox_v2 (
                                id, session_id, seq, device_code, group_code,
                                scan_rate_ms, ts_ms, config_version, device_type,
                                frame_json, stream_values_json, stream_qualities_json,
                                latest_fields_json, created_at
                            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                            [(item.params[0], self._session_id, self._next_seq + index,
                              *item.params[1:]) for index, item in enumerate(batch[:accepted])],
                        )
                        self._conn.execute("COMMIT")
                        self._next_seq += accepted
                    except BaseException:
                        try:
                            self._conn.execute("ROLLBACK")
                        except Exception:
                            logger.exception("outbox rollback failed")
                        raise
                    committed_bytes = sum(item.payload_bytes for item in batch[:accepted])
                    self._count += accepted
                    self._payload_bytes += committed_bytes
                    first_ms = batch[0].params[-1]
                    self._oldest_ms = min(self._oldest_ms, first_ms) if self._oldest_ms is not None else first_ms
                    if self._metrics is not None:
                        try:
                            self._metrics.record_timing("outbox_commit_ms", (time.perf_counter() - started) * 1000)
                            self._metrics.increment("outbox_committed_frames", accepted)
                            # 本地持久接收的帧数（与已回放到 Redis 的 stream_points_written 分开计数）。
                            self._metrics.increment("durable_frames_accepted", accepted)
                            self._metrics.increment("outbox_committed_bytes", committed_bytes)
                            self._metrics.set_gauge("outbox_commit_batch_frames", accepted)
                            self._metrics.set_gauge("outbox_commit_batch_bytes", committed_bytes)
                        except Exception:
                            logger.exception("outbox commit metric failed")
            return accepted

    def _reject(self, request: _InsertRequest, reason: str) -> None:
        if self._metrics is not None:
            try:
                self._metrics.increment("outbox_rejected_frames")
            except Exception:
                logger.exception("outbox rejection metric failed")
        logger.error("%s: device=%s group=%s ts_ms=%s", reason,
                     request.params[1], request.params[2], request.params[4])
        self._record_rejected_gap(request, reason)

    def _init_reject_log(self) -> None:
        """独立缺口台账；每次启动重建，避免陈旧行被误读为当前缺口。"""
        self._reject_conn = None
        self._reject_path = self.path.with_name(self.path.name + ".rejects.sqlite")
        # 拒收既可能发生在调用方线程（等待队列超时），也可能发生在写线程
        # （批次超出剩余容量），因此连接允许跨线程，并用独立锁串行化写入。
        self._reject_lock = threading.Lock()
        try:
            conn = sqlite3.connect(self._reject_path, timeout=5,
                                   isolation_level=None, check_same_thread=False)
            conn.row_factory = sqlite3.Row
            conn.execute("PRAGMA journal_mode=WAL")
            # 不设 synchronous=FULL：这条路径只在主队列已经拒收时才写，
            # 不能因为缺口台账自己的 fsync 再拖慢采集；丢失缺口记录虽不理想，
            # 但比不上丢采样严重，且主日志仍会逐条记录原因。
            conn.execute("PRAGMA synchronous=NORMAL")
            conn.execute("DROP TABLE IF EXISTS collector_rejected_gaps")
            conn.executescript(REJECT_LOG_SCHEMA)
            self._reject_conn = conn
        except Exception:
            logger.exception("collector rejection gap log unavailable; sampling gaps will "
                             "only be visible in the log and metrics")

    def _record_rejected_gap(self, request: _InsertRequest, reason: str) -> None:
        """按 (设备, 分组, 周期, 原因) 聚合成一行缺口，可在采集未停时随时查询。"""
        conn = getattr(self, "_reject_conn", None)
        if conn is None:
            return
        now_ms = int(time.time() * 1000)
        try:
            with self._reject_lock:
                conn.execute(
                    """INSERT INTO collector_rejected_gaps (
                        device_code, group_code, device_type, scan_rate_ms, reason,
                        first_ts_ms, last_ts_ms, frames, first_rejected_at, last_rejected_at
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, 1, ?, ?)
                    ON CONFLICT(device_code, group_code, scan_rate_ms, reason) DO UPDATE SET
                        last_ts_ms = MAX(last_ts_ms, excluded.last_ts_ms),
                        first_ts_ms = MIN(first_ts_ms, excluded.first_ts_ms),
                        frames = frames + 1,
                        last_rejected_at = excluded.last_rejected_at""",
                    (request.params[1], request.params[2], request.params[6],
                     request.params[3], reason, request.params[4], request.params[4],
                     now_ms, now_ms),
                )
        except Exception:
            logger.exception("collector rejection gap record failed")

    def rejected_gaps(self) -> list[dict[str, Any]]:
        """当前缺口台账内容；供运维与测试查询，不依赖主队列是否还能写。"""
        conn = getattr(self, "_reject_conn", None)
        if conn is None:
            return []
        try:
            rows = conn.execute(
                """SELECT device_code, group_code, device_type, scan_rate_ms, reason,
                          first_ts_ms, last_ts_ms, frames,
                          first_rejected_at, last_rejected_at
                   FROM collector_rejected_gaps ORDER BY last_rejected_at DESC"""
            ).fetchall()
        except Exception:
            logger.exception("collector rejection gap read failed")
            return []
        return [dict(row) for row in rows]

    def replay_pending(self, redis_writer, *, max_batch: int = 4096,
                       max_batch_bytes: int = 32 * 1024 * 1024) -> int:
        """One replay owner; delete only after the entire batch is durably accepted."""
        if not self._replay_lock.acquire(blocking=False):
            return 0
        try:
            with self._lock:
                rows = []
                payload_bytes = 0
                for table, protocol, identity in (
                    ("collector_retry_outbox", "legacy", "NULL AS session_id, NULL AS seq"),
                    ("collector_retry_outbox_v2", "v2", "session_id, seq"),
                ):
                    cursor = self._conn.execute(
                        f"SELECT {_REPLAY_COLUMNS}, {identity}, '{protocol}' AS protocol "
                        f"FROM {table} ORDER BY rowid LIMIT ?",
                        (max_batch - len(rows),),
                    )
                    limit_reached = False
                    for row in cursor:
                        row_bytes = (len(row["frame_json"]) + len(row["stream_values_json"])
                                     + len(row["stream_qualities_json"] or b"")
                                     + len(row["latest_fields_json"]))
                        if rows and payload_bytes + row_bytes > max_batch_bytes:
                            limit_reached = True
                            break
                        rows.append(row)
                        payload_bytes += row_bytes
                    cursor.close()
                    if limit_reached or len(rows) >= max_batch:
                        break
            if not rows:
                return 0
            redis_writer.write_durable_frames(rows)
            with self._lock:
                self._conn.execute("BEGIN IMMEDIATE")
                try:
                    for protocol, table in (("legacy", "collector_retry_outbox"),
                                            ("v2", "collector_retry_outbox_v2")):
                        self._conn.executemany(
                            f"DELETE FROM {table} WHERE id=?",
                            [(r["id"],) for r in rows if r["protocol"] == protocol],
                        )
                    self._conn.execute("COMMIT")
                except BaseException:
                    try:
                        self._conn.execute("ROLLBACK")
                    except Exception:
                        logger.exception("outbox replay cleanup rollback failed")
                    raise
                self._count -= len(rows)
                self._payload_bytes -= sum(
                    len(row["frame_json"]) + len(row["stream_values_json"])
                    + len(row["stream_qualities_json"] or b"") + len(row["latest_fields_json"])
                    for row in rows
                )
                self._oldest_ms = self._conn.execute(
                    "SELECT MIN(created_at) FROM ("
                    "SELECT created_at FROM collector_retry_outbox UNION ALL "
                    "SELECT created_at FROM collector_retry_outbox_v2)"
                ).fetchone()[0]
            return len(rows)
        finally:
            self._replay_lock.release()

    def iter_pending_latest(self, *, batch_size: int = 16):
        """Read committed latest payloads in bounded batches for fast-lane restart."""
        with self._lock:
            ceilings = {
                table: self._conn.execute(
                    f"SELECT COALESCE(MAX(rowid), 0) FROM {table}"
                ).fetchone()[0]
                for table in ("collector_retry_outbox", "collector_retry_outbox_v2")
            }
        for table, ceiling in ceilings.items():
            last_rowid = 0
            while last_rowid < ceiling:
                with self._lock:
                    rows = self._conn.execute(
                        "SELECT rowid, device_code, group_code, latest_fields_json "
                        f"FROM {table} WHERE rowid > ? AND rowid <= ? "
                        "ORDER BY rowid LIMIT ?",
                        (last_rowid, ceiling, batch_size),
                    ).fetchall()
                if not rows:
                    break
                for row in rows:
                    yield row["device_code"], row["group_code"], row["latest_fields_json"]
                last_rowid = rows[-1]["rowid"]

    def assert_legacy_rollback_ready(self) -> None:
        """An older binary cannot replay v2 rows and must wait for them to drain."""
        with self._lock:
            pending = self._conn.execute(
                "SELECT COUNT(*) FROM collector_retry_outbox_v2"
            ).fetchone()[0]
        if pending:
            raise RuntimeError(
                f"Legacy rollback blocked: {pending} v2 frames require this writer version"
            )

    def count(self) -> int:
        with self._lock:
            return int(self._count)

    def stats(self) -> dict[str, int]:
        with self._lock:
            count = int(self._count)
            payload_bytes = int(self._payload_bytes)
            oldest_age_ms = max(0, int(time.time() * 1000) - self._oldest_ms) if self._oldest_ms is not None else 0
        with self._write_condition:
            return {
                "count": count,
                "payload_bytes": payload_bytes,
                "oldest_age_ms": oldest_age_ms,
                "waiting_writes": len(self._write_queue),
                "waiting_bytes": self._waiting_bytes,
            }
