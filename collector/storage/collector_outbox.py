"""集中式采集 Redis 写入失败的持久化重试队列（DW-01）。

集中式 Collector 直写 Redis 若发生异常（网络中断、Redis 拒绝写入或瞬时不可用），
将采集到的原始点位数据、原始采样时间戳持久化保存在本地 SQLite 队列中。
待 Redis 恢复后，重试队列能够按原始时间戳重放数据，并在写入成功后安全清理，
同时具备容量上限（磁盘/记录数限制）以及自动逐出保护。
"""

from __future__ import annotations

import logging
import sqlite3
import threading
import time
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
            self._conn.execute("PRAGMA synchronous=NORMAL")
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
            # 检查容量上限，超限时淘汰最老的一批记录（有界缓冲区），并记录告警
            count = self._conn.execute(
                "SELECT COUNT(*) AS c FROM collector_retry_outbox"
            ).fetchone()["c"]
            if count >= self.max_records:
                to_delete = max(1, self.max_records // 10)
                self._conn.execute(
                    f"""
                    DELETE FROM collector_retry_outbox WHERE id IN (
                        SELECT id FROM collector_retry_outbox ORDER BY created_at ASC LIMIT {to_delete}
                    )
                    """
                )
                logger.warning(
                    "Collector retry outbox saturated (count=%d >= %d), purged oldest %d records",
                    count,
                    self.max_records,
                    to_delete,
                )

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
        """在 Redis 恢复后，重放此前堆积的数据。"""
        with self._lock:
            rows = self._conn.execute(
                """
                SELECT id, device_code, group_code, scan_rate_ms, ts_ms,
                       config_version, device_type, frame_json,
                       stream_values_json, stream_qualities_json,
                       latest_fields_json
                FROM collector_retry_outbox
                ORDER BY created_at ASC
                LIMIT ?
                """,
                (max_batch,),
            ).fetchall()

        replayed = 0
        for row in rows:
            try:
                frame = msgspec.json.decode(row["frame_json"])
                stream_values = msgspec.json.decode(row["stream_values_json"])
                stream_qualities = (
                    msgspec.json.decode(row["stream_qualities_json"])
                    if row["stream_qualities_json"]
                    else None
                )
                latest_fields = msgspec.json.decode(row["latest_fields_json"])

                # 使用原始时间戳与采样数据执行重放
                redis_writer.write_window_stream(
                    row["device_code"],
                    row["device_type"],
                    row["group_code"],
                    row["scan_rate_ms"],
                    row["ts_ms"],
                    row["config_version"],
                    frame,
                    stream_values,
                    stream_qualities,
                )
                redis_writer.write_latest(
                    row["device_code"],
                    row["group_code"],
                    latest_fields,
                )

                with self._lock:
                    self._conn.execute(
                        "DELETE FROM collector_retry_outbox WHERE id = ?",
                        (row["id"],),
                    )
                replayed += 1
            except Exception as exc:
                logger.error("Failed to replay outbox record %s: %s", row["id"], exc)
                with self._lock:
                    self._conn.execute(
                        "UPDATE collector_retry_outbox SET attempts = attempts + 1 WHERE id = ?",
                        (row["id"],),
                    )
                break
        return replayed

    def count(self) -> int:
        with self._lock:
            return int(
                self._conn.execute(
                    "SELECT COUNT(*) AS c FROM collector_retry_outbox"
                ).fetchone()["c"]
            )
