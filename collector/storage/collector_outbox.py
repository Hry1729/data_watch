"""集中式采集 Redis 写入失败的持久化重试队列（DW-01）。

集中式 Collector 直写 Redis 若发生异常（网络中断、Redis 拒绝写入或瞬时不可用），
将采集到的原始点位数据、原始采样时间戳持久化保存在本地 SQLite 队列中。
待 Redis 恢复后，重试队列能够按原始时间戳重放数据，并在写入成功后安全清理，
同时具备容量上限（磁盘/记录数限制）以及拒绝溢出保护。
"""

from __future__ import annotations

import logging
import sqlite3
import threading
import time
import shutil
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
"""


class CollectorRetryOutbox:
    def __init__(
        self,
        db_path: str | Path,
        *,
        max_records: int = 50_000,
    ) -> None:
        self.path = Path(db_path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.max_records = max_records
        self._lock = threading.RLock()
        self._replay_lock = threading.Lock()
        self._conn = sqlite3.connect(
            self.path,
            timeout=5,
            isolation_level=None,
            check_same_thread=False,
        )
        self._conn.row_factory = sqlite3.Row
        self._init_db()

    def _init_db(self) -> None:
        with self._lock:
            self._conn.execute("PRAGMA journal_mode=WAL")
            self._conn.execute("PRAGMA synchronous=FULL")
            page_size = self._conn.execute("PRAGMA page_size").fetchone()[0]
            self._conn.execute(f"PRAGMA max_page_count={1024 * 1024 * 1024 // page_size}")
            self._conn.executescript(COLLECTOR_OUTBOX_SCHEMA)

    def close(self) -> None:
        with self._lock:
            self._conn.close()

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
        """保存采集成功的原始数据与时间戳，以便后续重放。"""
        now = int(time.time() * 1000)
        row_id = str(uuid4())
        with self._lock:
            count = self._conn.execute("SELECT COUNT(*) FROM collector_retry_outbox").fetchone()[0]
            if count >= self.max_records or shutil.disk_usage(self.path.parent).free < 256 * 1024 * 1024:
                raise RuntimeError("Collector durable queue full; refusing new sample, existing backlog preserved")

            self._conn.execute(
                """
                INSERT INTO collector_retry_outbox (
                    id, device_code, group_code, scan_rate_ms, ts_ms,
                    config_version, device_type, frame_json,
                    stream_values_json, stream_qualities_json,
                    latest_fields_json, created_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    row_id,
                    device_code,
                    group_code,
                    scan_rate_ms,
                    ts_ms,
                    config_version,
                    device_type,
                    msgspec.json.encode(frame),
                    msgspec.json.encode(stream_values),
                    msgspec.json.encode(stream_qualities) if stream_qualities else None,
                    msgspec.json.encode(latest_fields),
                    now,
                ),
            )
        return row_id

    def replay_pending(self, redis_writer, *, max_batch: int = 50) -> int:
        """One replay owner; delete only after the entire batch is durably accepted."""
        if not self._replay_lock.acquire(blocking=False):
            return 0
        try:
            with self._lock:
                rows = self._conn.execute(
                    "SELECT * FROM collector_retry_outbox ORDER BY created_at, rowid LIMIT ?", (max_batch,)
                ).fetchall()
            if not rows:
                return 0
            redis_writer.write_durable_frames(rows)
            with self._lock:
                self._conn.execute("BEGIN IMMEDIATE")
                try:
                    self._conn.executemany("DELETE FROM collector_retry_outbox WHERE id=?", [(r["id"],) for r in rows])
                    self._conn.execute("COMMIT")
                except BaseException:
                    self._conn.execute("ROLLBACK")
                    raise
            return len(rows)
        finally:
            self._replay_lock.release()

    def count(self) -> int:
        with self._lock:
            return int(
                self._conn.execute(
                    "SELECT COUNT(*) AS c FROM collector_retry_outbox"
                ).fetchone()["c"]
            )
