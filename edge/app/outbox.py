from __future__ import annotations

import base64
import csv
import hashlib
import io
import os
import shutil
import sqlite3
import threading
import time
import logging
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from uuid import uuid4

import msgspec

from edge.app.snapshot_schema import (
    CONFIG_SNAPSHOT_SCHEMA,
    ensure_edge_variable_columns,
)


SENT_RETENTION_MS = 60_000  # 默认 60s 兼容已有直接调用，生产 uploader 传入 24h 参数
# dead 保留与离线 backlog 分开：后者仍是 DATAWATCH_MAX_OFFLINE_RETENTION_DAYS（默认 7）。
DEAD_RETENTION_DAYS_DEFAULT = 30
DEAD_MAX_BYTES_DEFAULT = 512 * 1024 * 1024
DEAD_EXPORT_SCHEMA_VERSION = 1
# 70% / 85% / 95% 都是磁盘告警。告警或超过字节上限时只自动导出，不提前删除。
_DISK_ALARM_WATERMARKS = frozenset({"warning", "critical", "overflow"})
_DEAD_EXPORT_PAGE_SIZE = 64
_DEAD_EXPORT_META_FIELDS = (
    "batch_id",
    "upload_session_id",
    "local_seq",
    "device_code",
    "group_code",
    "assignment_epoch",
    "config_version",
    "ts",
    "attempt_count",
    "reject_count",
    "dead_at",
    "http_status",
    "last_error",
    "exported_at",
    "replayed_at",
    "replay_ref",
)
logger = logging.getLogger("edge.outbox")

# B6：坏数据隔离预算（``reject_count``）只由「中心给出了协议级裁决」的失败消耗。
# 传输类失败（无 http_status、429/5xx/断网）只累积 ``attempt_count``（退避用），
# 绝不消耗隔离预算 —— 否则长时间断网的历史网络次数会让一次序列异常把好数据判死。
# 取值与 ``edge/app/uploader.py`` 中 ``quarantine_after_max=True`` 的两个调用点严格一致。
# ``commit_unknown`` 是中心持久化结果不确定（Redis 暂时故障），不是坏数据裁决，
# 不在此列：uploader 在同一 session 内退避重试，不消耗隔离预算。
PROTOCOL_REJECT_ERRORS = frozenset(
    {
        "http_400_contract_rejected",  # uploader.py: HTTP 400 契约拒绝（记录级）
        "seq_reject",  # uploader.py: ACK 状态 seq_reject
    }
)
# 没有启用的 assignment 但 outbox 仍有积压时的上传间隔：采集停了，积压照样要传完。
DEFAULT_UPLOAD_INTERVAL_MS = 1000

OUTBOX_SCHEMA = """
CREATE TABLE IF NOT EXISTS outbox (
    batch_id TEXT PRIMARY KEY,
    upload_session_id TEXT NOT NULL,
    local_seq INTEGER NOT NULL,
    device_code TEXT NOT NULL,
    group_code TEXT NOT NULL,
    assignment_epoch INTEGER NOT NULL,
    config_version INTEGER NOT NULL,
    ts INTEGER NOT NULL,
    payload BLOB NOT NULL,
    state TEXT NOT NULL DEFAULT 'pending',
    attempt_count INTEGER NOT NULL DEFAULT 0,
    reject_count INTEGER NOT NULL DEFAULT 0,
    next_retry_at INTEGER,
    last_error TEXT,
    http_status INTEGER,
    lease_owner TEXT,
    inflight_at INTEGER,
    lease_until INTEGER,
    acked_at INTEGER,
    dead_at INTEGER,
    exported_at INTEGER,
    replayed_at INTEGER,
    replay_ref TEXT,
    created_at INTEGER NOT NULL,
    UNIQUE (upload_session_id, local_seq)
);
CREATE INDEX IF NOT EXISTS idx_outbox_state_retry ON outbox(state, next_retry_at);
CREATE INDEX IF NOT EXISTS idx_outbox_created ON outbox(created_at);
CREATE INDEX IF NOT EXISTS idx_outbox_lease ON outbox(state, lease_until);
CREATE INDEX IF NOT EXISTS idx_outbox_seq ON outbox(upload_session_id, local_seq);
CREATE INDEX IF NOT EXISTS idx_outbox_active_seq ON outbox(upload_session_id, local_seq)
    WHERE state IN ('pending', 'retry_wait', 'inflight');
CREATE INDEX IF NOT EXISTS idx_outbox_device_ts ON outbox(device_code, ts);

CREATE TABLE IF NOT EXISTS metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS device_assignment (
    device_code TEXT PRIMARY KEY,
    assignment_epoch INTEGER NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    config_revision INTEGER NOT NULL DEFAULT 0,
    enabled INTEGER NOT NULL DEFAULT 1,
    updated_at INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS local_latest (
    device_code TEXT NOT NULL,
    group_code TEXT NOT NULL,
    var_id TEXT NOT NULL,
    value_json BLOB NOT NULL,
    updated_at INTEGER NOT NULL,
    PRIMARY KEY (device_code, group_code, var_id)
);

CREATE TABLE IF NOT EXISTS local_latest_frame (
    device_code TEXT NOT NULL,
    group_code TEXT NOT NULL,
    values_json BLOB NOT NULL,
    updated_at INTEGER NOT NULL,
    PRIMARY KEY (device_code, group_code)
) WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS dead_audit (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ts INTEGER NOT NULL,
    actor_role TEXT NOT NULL,
    action TEXT NOT NULL,
    batch_id TEXT,
    session TEXT,
    local_seq INTEGER,
    result TEXT NOT NULL,
    file_path TEXT,
    sha256 TEXT
);
"""


@dataclass(frozen=True, slots=True)
class OutboxRecord:
    batch_id: str
    upload_session_id: str
    local_seq: int
    assignment_epoch: int
    payload: bytes
    attempt_count: int
    # B6：坏数据隔离预算（只由中心给出的协议级裁决消耗，见 PROTOCOL_REJECT_ERRORS）。
    # ``None`` 表示当前 Edge Storage **未回传**该字段（旧版存储）——调用方必须显式降级，
    # 不得静默当成 0，否则真正非法的载荷将永远不被隔离（见 docs/整改记录/B6.md）。
    reject_count: int | None = None


@dataclass(frozen=True, slots=True)
class SessionSwitchState:
    old_session_id: str
    new_session_id: str


@dataclass(frozen=True, slots=True)
class OutboxCompletion:
    record: OutboxRecord
    state: str
    error: str | None = None
    # B6：终结为 ``dead`` 时可带上「致命那次裁决」的协议状态码，覆盖该行上一次
    # 传输失败留下的 http_status，避免取证指向错误的原因；为 None 时保持原值。
    http_status: int | None = None



class OutboxStore:
    def __init__(self, path: str | Path, *, clock_ms=None) -> None:
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self._clock_ms = clock_ms or (lambda: int(time.time() * 1000))
        self._lock = threading.RLock()
        self._connection = sqlite3.connect(
            self.path,
            timeout=5,
            isolation_level=None,
            check_same_thread=False,
        )
        self._connection.row_factory = sqlite3.Row
        self._initialize()

    def close(self) -> None:
        with self._lock:
            self._connection.close()

    def _initialize(self) -> None:
        with self._lock:
            self._connection.execute("PRAGMA auto_vacuum=INCREMENTAL")
            self._connection.execute("PRAGMA journal_mode=WAL")
            self._connection.execute("PRAGMA synchronous=FULL")
            self._connection.execute("PRAGMA busy_timeout=5000")
            self._connection.executescript(OUTBOX_SCHEMA)
            self._migrate_outbox_columns(self._connection)
            self._connection.executescript(CONFIG_SNAPSHOT_SCHEMA)
            ensure_edge_variable_columns(self._connection)
            self._connection.execute(
                "INSERT OR IGNORE INTO metadata(key, value) VALUES('upload_session_id', ?)",
                (str(uuid4()),),
            )
            self._connection.execute(
                "INSERT OR IGNORE INTO metadata(key, value) VALUES('last_seq', '0')"
            )
            self._connection.execute(
                "INSERT OR IGNORE INTO metadata(key, value) VALUES('dropped_records', '0')"
            )
            self._connection.execute(
                "INSERT OR IGNORE INTO metadata(key, value) VALUES('sent_records_total', '0')"
            )

    @staticmethod
    def _migrate_outbox_columns(connection: sqlite3.Connection) -> None:
        """为已存在的 edge.db 补列。

        ``CREATE TABLE IF NOT EXISTS`` 不会修改已有表，部署时升级的现场库
        必须显式补列，否则 ``reject_count`` 写入会报 no such column。
        """
        columns = {
            str(row[1]) for row in connection.execute("PRAGMA table_info(outbox)")
        }
        if "reject_count" not in columns:
            connection.execute(
                "ALTER TABLE outbox ADD COLUMN reject_count INTEGER NOT NULL DEFAULT 0"
            )
        # B6：导出/重放标记。只 ADD COLUMN，不改已有行；旧 dead 的这两列为 NULL，
        # maintenance 因此不会把尚未处置的载荷删掉。
        for name, ddl in (
            ("exported_at", "ALTER TABLE outbox ADD COLUMN exported_at INTEGER"),
            ("replayed_at", "ALTER TABLE outbox ADD COLUMN replayed_at INTEGER"),
            ("replay_ref", "ALTER TABLE outbox ADD COLUMN replay_ref TEXT"),
        ):
            if name not in columns:
                connection.execute(ddl)

    def configure_assignment(
        self,
        device_code: str,
        assignment_epoch: int,
        scan_rate_ms: int,
        config_revision: int,
        *,
        enabled: bool = True,
    ) -> None:
        if assignment_epoch < 0 or scan_rate_ms <= 0:
            raise ValueError("invalid assignment epoch or scan rate")
        now = self._clock_ms()
        with self._lock:
            self._connection.execute(
                """
                INSERT INTO device_assignment(
                    device_code, assignment_epoch, scan_rate_ms,
                    config_revision, enabled, updated_at
                ) VALUES(?, ?, ?, ?, ?, ?)
                ON CONFLICT(device_code) DO UPDATE SET
                    assignment_epoch=excluded.assignment_epoch,
                    scan_rate_ms=excluded.scan_rate_ms,
                    config_revision=excluded.config_revision,
                    enabled=excluded.enabled,
                    updated_at=excluded.updated_at
                """,
                (
                    device_code,
                    assignment_epoch,
                    scan_rate_ms,
                    config_revision,
                    int(enabled),
                    now,
                ),
            )

    def upload_interval_ms(self) -> int | None:
        with self._lock:
            row = self._connection.execute(
                """
                SELECT COALESCE(
                    (
                        SELECT MIN(variable.scan_rate_ms)
                        FROM edge_variable AS variable
                        INNER JOIN device_assignment AS assignment
                            ON assignment.device_code = variable.device_code
                        WHERE assignment.enabled = 1
                    ),
                    (
                        SELECT MIN(scan_rate_ms)
                        FROM device_assignment
                        WHERE enabled = 1
                    )
                ) AS value
                """
            ).fetchone()
            if row and row["value"] is not None:
                return int(row["value"])
            # Edge 9：上传调度与「是否还在采集」解耦。设备被禁用/迁走/时间窗结束后，
            # 未确认的积压仍按默认间隔上传，否则会拖过中心 grace 窗口而丢失。
            backlog = self._connection.execute(
                """
                SELECT 1 FROM outbox
                WHERE state IN ('pending', 'retry_wait', 'inflight')
                LIMIT 1
                """
            ).fetchone()
        return DEFAULT_UPLOAD_INTERVAL_MS if backlog is not None else None

    def append_envelope(
        self,
        *,
        device_code: str,
        device_type: str,
        group_code: str,
        scan_rate_ms: int,
        ts: int,
        config_version: int,
        values: dict[str, str],
        qualities: dict[str, int] | None,
        batch_id: str | None = None,
        latest_fields: dict[str, dict[str, Any]] | None = None,
    ) -> OutboxRecord:
        self._ensure_disk_writable()
        now = self._clock_ms()
        batch_id = batch_id or str(uuid4())
        with self._transaction() as connection:
            return self._append_envelope(
                connection,
                batch_id=batch_id,
                device_code=device_code,
                device_type=device_type,
                group_code=group_code,
                scan_rate_ms=scan_rate_ms,
                ts=ts,
                config_version=config_version,
                values=values,
                qualities=qualities,
                latest_fields=latest_fields,
                now=now,
            )

    def commit_frame(
        self,
        *,
        batch_id: str,
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
        """Persist the durable envelope and local latest values atomically."""
        result = self.commit_frames([dict(batch_id=batch_id, device_code=device_code,
            device_type=device_type, group_code=group_code, scan_rate_ms=scan_rate_ms,
            ts=ts, config_version=config_version, values=values, qualities=qualities,
            latest_fields=latest_fields)])[0]
        if isinstance(result, Exception):
            raise result
        return result

    def commit_frames(self, frames: list[dict]) -> list[OutboxRecord | Exception]:
        """Commit a queued group durably, isolating bad frames with savepoints."""
        if not frames:
            return []
        self._ensure_disk_writable()
        now = self._clock_ms()
        results = []
        with self._transaction() as connection:
            for frame in frames:
                connection.execute("SAVEPOINT frame_commit")
                try:
                    record = self._append_envelope(connection, now=now, **frame)
                    self._write_local_latest_frame(connection, frame["device_code"],
                        frame["group_code"], frame["latest_fields"], now)
                except Exception as exc:
                    connection.execute("ROLLBACK TO frame_commit")
                    results.append(exc)
                else:
                    results.append(record)
                finally:
                    connection.execute("RELEASE frame_commit")
        return results

    def write_local_latest(
        self,
        device_code: str,
        group_code: str,
        fields: dict[str, dict[str, Any]],
    ) -> None:
        now = self._clock_ms()
        with self._transaction() as connection:
            self._write_local_latest_frame(
                connection,
                device_code,
                group_code,
                fields,
                now,
            )

    def read_local_latest(self, device_code: str, group_code: str) -> dict[str, Any]:
        with self._lock:
            frame = self._connection.execute(
                """
                SELECT values_json
                FROM local_latest_frame
                WHERE device_code = ? AND group_code = ?
                """,
                (device_code, group_code),
            ).fetchone()
            if frame is not None:
                values = msgspec.json.decode(frame["values_json"])
                return dict(values) if isinstance(values, dict) else {}
            rows = self._connection.execute(
                """
                SELECT var_id, value_json
                FROM local_latest
                WHERE device_code = ? AND group_code = ?
                """,
                (device_code, group_code),
            ).fetchall()
        return {
            str(row["var_id"]): msgspec.json.decode(row["value_json"])
            for row in rows
        }

    def snapshot_target_seq(self) -> int:
        with self._lock:
            return int(self._metadata(self._connection, "last_seq"))

    def current_session_id(self) -> str:
        with self._lock:
            return self._metadata(self._connection, "upload_session_id")

    def recover_inflight(self, lease_owner: str) -> int:
        now = self._clock_ms()
        with self._lock:
            cursor = self._connection.execute(
                """
                UPDATE outbox
                SET state='pending', lease_owner=NULL, inflight_at=NULL, lease_until=NULL
                WHERE state='inflight'
                  AND (lease_owner IS NULL OR lease_owner <> ? OR lease_until < ?)
                """,
                (lease_owner, now),
            )
            return cursor.rowcount

    def _normalize_retry_clock(self, connection, now: int) -> None:
        row = connection.execute("SELECT value FROM metadata WHERE key='retry_clock_ms'").fetchone()
        previous = int(row[0]) if row is not None else now
        if now < previous:
            delta = now - previous
            connection.execute("UPDATE outbox SET next_retry_at=next_retry_at+? WHERE state='retry_wait'", (delta,))
            connection.execute("UPDATE outbox SET lease_until=lease_until+? WHERE state='inflight'", (delta,))
        connection.execute("INSERT INTO metadata(key, value) VALUES('retry_clock_ms', ?) ON CONFLICT(key) DO UPDATE SET value=excluded.value", (str(now),))

    def claim_batch(
        self,
        *,
        target_seq: int,
        lease_owner: str,
        lease_ms: int,
        max_items: int,
        max_bytes: int,
    ) -> list[OutboxRecord]:
        if max_items <= 0 or max_bytes <= 0:
            raise ValueError("batch limits must be positive")
        now = self._clock_ms()
        oversized_batch_id = None
        with self._transaction() as connection:
            self._normalize_retry_clock(connection, now)
            session_id = self._metadata(connection, "upload_session_id")
            rows = connection.execute(
                """
                SELECT batch_id, upload_session_id, local_seq, assignment_epoch,
                       payload, attempt_count, reject_count, state, next_retry_at,
                       lease_until, lease_owner
                FROM outbox INDEXED BY idx_outbox_active_seq
                WHERE upload_session_id = ?
                  AND local_seq <= ?
                  AND state IN ('pending', 'retry_wait', 'inflight')
                ORDER BY local_seq
                LIMIT ?
                """,
                (session_id, target_seq, max_items),
            ).fetchall()
            selected: list[sqlite3.Row] = []
            total_bytes = 0
            for row in rows:
                if row["state"] == "inflight" and row["lease_owner"] != lease_owner:
                    break
                if row["state"] == "retry_wait" and int(row["next_retry_at"] or 0) > now:
                    break
                payload_size = len(row["payload"])
                if selected and (
                    len(selected) >= max_items or total_bytes + payload_size > max_bytes
                ):
                    break
                if not selected and payload_size > max_bytes:
                    oversized_batch_id = str(row["batch_id"])
                    logger.error("Outbox payload quarantined batch_id=%s local_seq=%s bytes=%s max_bytes=%s", row["batch_id"], row["local_seq"], payload_size, max_bytes)
                    break
                selected.append(row)
                total_bytes += payload_size
                if len(selected) >= max_items:
                    break

            if not selected and oversized_batch_id is None:
                return []
            if selected:
                batch_ids = [str(row["batch_id"]) for row in selected]
                placeholders = ",".join(["?"] * len(batch_ids))
                connection.execute(
                    f"""
                    UPDATE outbox
                    SET state='inflight', lease_owner=?, inflight_at=?, lease_until=?
                    WHERE batch_id IN ({placeholders})
                      AND state IN ('pending', 'retry_wait')
                    """,
                    (lease_owner, now, now + lease_ms, *batch_ids),
                )
                return [
                    OutboxRecord(
                        batch_id=str(row["batch_id"]),
                        upload_session_id=str(row["upload_session_id"]),
                        local_seq=int(row["local_seq"]),
                        assignment_epoch=int(row["assignment_epoch"]),
                        payload=bytes(row["payload"]),
                        attempt_count=int(row["attempt_count"]),
                        reject_count=int(row["reject_count"]),
                    )
                    for row in selected
                ]
        self.prepare_session_switch(_quarantine_batch_id=oversized_batch_id)
        return []

    def mark_sent(self, record: OutboxRecord, lease_owner: str) -> None:
        self.complete_batch([OutboxCompletion(record, "sent")], lease_owner)

    def mark_dead(
        self,
        record: OutboxRecord,
        lease_owner: str,
        error: str | None,
    ) -> None:
        self.complete_batch([OutboxCompletion(record, "dead", error)], lease_owner)

    def complete_batch(
        self,
        completions: list[OutboxCompletion],
        lease_owner: str,
    ) -> None:
        if not completions:
            return
        now = self._clock_ms()
        sent_count = 0
        with self._transaction() as connection:
            for completion in completions:
                if completion.state not in {"sent", "dead"}:
                    raise ValueError("completion state must be sent or dead")
                timestamp_column = "acked_at" if completion.state == "sent" else "dead_at"
                record = completion.record
                cursor = connection.execute(
                    f"""
                    UPDATE outbox
                    SET state=?, {timestamp_column}=?, last_error=?,
                        http_status=COALESCE(?, http_status),
                        lease_owner=NULL, inflight_at=NULL, lease_until=NULL
                    WHERE batch_id=? AND upload_session_id=? AND local_seq=?
                      AND state='inflight' AND lease_owner=?
                    """,
                    (
                        completion.state,
                        now,
                        completion.error,
                        completion.http_status,
                        record.batch_id,
                        record.upload_session_id,
                        record.local_seq,
                        lease_owner,
                    ),
                )
                if cursor.rowcount != 1:
                    current = connection.execute(
                        "SELECT state FROM outbox WHERE batch_id=? AND upload_session_id=? AND local_seq=?",
                        (record.batch_id, record.upload_session_id, record.local_seq),
                    ).fetchone()
                    if current and current["state"] == completion.state:
                        continue
                    raise RuntimeError("outbox terminal transition lost lease")
                sent_count += int(completion.state == "sent")
            if sent_count:
                connection.execute(
                    """
                    UPDATE metadata
                    SET value=CAST(CAST(value AS INTEGER) + ? AS TEXT)
                    WHERE key='sent_records_total'
                    """,
                    (sent_count,),
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

    def mark_retry_batch(
        self,
        records: list[tuple[OutboxRecord, int]],
        lease_owner: str,
        *,
        error: str,
        http_status: int | None,
    ) -> None:
        if not records:
            return
        now = self._clock_ms()
        # B6：失败分类（批次级判定，调用方每次只传一种失败原因）。
        #   协议拒绝（200/400 且原因是中心给出的协议级裁决）→ 消耗坏数据隔离预算；
        #   传输失败（无状态码 / 429 / 5xx / 断网）→ 只累加退避计数。
        counts_toward_quarantine = (
            http_status in (200, 400) and error in PROTOCOL_REJECT_ERRORS
        )
        with self._transaction() as connection:
            self._normalize_retry_clock(connection, now)
            for record, delay_ms in records:
                cursor = connection.execute(
                    """
                    UPDATE outbox
                    SET state='retry_wait',
                        attempt_count=attempt_count + 1,
                        reject_count=reject_count + ?,
                        next_retry_at=?,
                        last_error=?,
                        http_status=?,
                        lease_owner=NULL,
                        inflight_at=NULL,
                        lease_until=NULL
                    WHERE batch_id=? AND upload_session_id=? AND local_seq=?
                      AND state='inflight' AND lease_owner=?
                    """,
                    (
                        int(counts_toward_quarantine),
                        now + delay_ms,
                        error[:1000],
                        http_status,
                        record.batch_id,
                        record.upload_session_id,
                        record.local_seq,
                        lease_owner,
                    ),
                )
                if cursor.rowcount != 1:
                    current = connection.execute(
                        "SELECT state FROM outbox WHERE batch_id=? AND upload_session_id=? AND local_seq=?",
                        (record.batch_id, record.upload_session_id, record.local_seq),
                    ).fetchone()
                    if current and current["state"] == "retry_wait":
                        continue
                    raise RuntimeError("outbox retry transition lost lease")

    def prepare_session_switch(
        self,
        expected_old_session_id: str | None = None,
        *,
        _quarantine_batch_id: str | None = None,
    ) -> SessionSwitchState:
        if expected_old_session_id is not None and expected_old_session_id != self.current_session_id():
            raise RuntimeError("refusing to replace a foreign active session")
        new_session_id = str(uuid4())
        with self._transaction() as connection:
            existing = connection.execute(
                "SELECT value FROM metadata WHERE key='pending_session_switch_old_id'"
            ).fetchone()
            if existing:
                return SessionSwitchState(
                    old_session_id=str(existing["value"]),
                    new_session_id=self._metadata(connection, "upload_session_id"),
                )
            if _quarantine_batch_id is not None:
                connection.execute("UPDATE outbox SET state='dead', dead_at=?, last_error='payload_size_limit', lease_owner=NULL, lease_until=NULL, inflight_at=NULL WHERE batch_id=?", (self._clock_ms(), _quarantine_batch_id))
            old_session_id = expected_old_session_id or self._metadata(
                connection, "upload_session_id"
            )
            connection.execute(
                "INSERT INTO metadata(key, value) VALUES('pending_session_switch_old_id', ?)",
                (old_session_id,),
            )
            connection.execute(
                "UPDATE metadata SET value=? WHERE key='upload_session_id'",
                (new_session_id,),
            )
            connection.execute("UPDATE metadata SET value='0' WHERE key='last_seq'")
            pending_rows = connection.execute(
                """
                SELECT batch_id
                FROM outbox
                WHERE state IN ('pending', 'retry_wait', 'inflight')
                ORDER BY local_seq
                """
            ).fetchall()
            # A session switch is a protocol recovery operation, not an
            # acknowledgement. Preserve every unconfirmed row and replay it
            # under the new session with a contiguous sequence.
            #
            # B6：``reject_count`` 是坏数据隔离预算，**必须跨切换保留**（不在此重排里
            # 触碰），否则每次 session 切换都会重置预算、真正非法的帧永不判 dead。
            # ``last_error``/``http_status`` 同样保留：它们是「该帧为何进入新会话」的
            # 行级取证字段（协议裁决证据），按 B6「真正非法项有独立证据」的要求不得
            # 在恢复路径上被抹掉；下一次尝试会由 mark_retry_batch/complete_batch 覆盖。
            #
            # 退避（retry_wait + next_retry_at）同样跨切换保留：切换只是重编号，
            # 清掉退避会让下一个上传周期立即重放，形成对中心的无退避重试风暴。
            for local_seq, row in enumerate(pending_rows, start=1):
                connection.execute(
                    """
                    UPDATE outbox
                    SET upload_session_id=?,
                        local_seq=?,
                        state=CASE WHEN state='retry_wait' THEN 'retry_wait' ELSE 'pending' END,
                        lease_owner=NULL,
                        inflight_at=NULL,
                        lease_until=NULL
                    WHERE batch_id=?
                    """,
                    (new_session_id, local_seq, str(row["batch_id"])),
                )
            connection.execute(
                "UPDATE metadata SET value=? WHERE key='last_seq'",
                (str(len(pending_rows)),),
            )
        return SessionSwitchState(old_session_id, new_session_id)

    def pending_session_switch(self) -> SessionSwitchState | None:
        with self._lock:
            row = self._connection.execute(
                "SELECT value FROM metadata WHERE key='pending_session_switch_old_id'"
            ).fetchone()
            if not row:
                return None
            return SessionSwitchState(
                old_session_id=str(row["value"]),
                new_session_id=self._metadata(self._connection, "upload_session_id"),
            )

    def complete_session_switch(self, state: SessionSwitchState) -> None:
        with self._transaction() as connection:
            current = self._metadata(connection, "upload_session_id")
            old = connection.execute(
                "SELECT value FROM metadata WHERE key='pending_session_switch_old_id'"
            ).fetchone()
            if current != state.new_session_id or not old or old["value"] != state.old_session_id:
                raise RuntimeError("session switch state changed")
            connection.execute(
                "DELETE FROM metadata WHERE key='pending_session_switch_old_id'"
            )
            # B003：记下本机最近一次已完成切换的旧 session，用来识别「中心掉电回滚到旧 session」，
            # 与真实的另一实例（不同 session）区分开。
            connection.execute(
                "INSERT INTO metadata(key, value) VALUES('last_session_switch_old_id', ?) "
                "ON CONFLICT(key) DO UPDATE SET value=excluded.value",
                (state.old_session_id,),
            )

    def reopen_rolled_back_switch(self, active_session_id: str | None) -> SessionSwitchState | None:
        """中心 active 恰好回到本机上一次已完成切换的旧 session 时，重新打开该切换。

        只认本机自己记录的「旧 → 当前」这一次切换；中心的 Lua 也只在 active 仍是旧值时才切，
        因此不会抢占任何其他 session。已有待完成切换、没有记录或不匹配时返回 None。
        """
        if not active_session_id:
            return None
        with self._transaction() as connection:
            pending = connection.execute(
                "SELECT value FROM metadata WHERE key='pending_session_switch_old_id'"
            ).fetchone()
            if pending:
                return None
            last = connection.execute(
                "SELECT value FROM metadata WHERE key='last_session_switch_old_id'"
            ).fetchone()
            current = self._metadata(connection, "upload_session_id")
            if not last or str(last["value"]) != active_session_id or current == active_session_id:
                return None
            connection.execute(
                "INSERT INTO metadata(key, value) VALUES('pending_session_switch_old_id', ?)",
                (active_session_id,),
            )
            return SessionSwitchState(old_session_id=active_session_id, new_session_id=current)

    def stats(self) -> dict[str, int]:
        with self._lock:
            rows = self._connection.execute(
                "SELECT state, COUNT(*) AS count FROM outbox GROUP BY state"
            ).fetchall()
        return {str(row["state"]): int(row["count"]) for row in rows}

    # ── B6：dead 载荷的查看 / 导出 / 受控重放 ──────────────────────────────────
    # 未导出且未重放的 dead 一律不删。重放不改原行状态，而是用原载荷重新入队
    # （当前 session、新 local_seq），原行只记 replayed_at / replay_ref。
    # 超过时间窗只允许导出。删除权限不覆盖尚未处置的载荷。

    # 重放时间窗：dead 超过该时长后只允许导出，不再重新入队。
    # 窗口内的重放走新行，避免把旧 session 的序号原地改回可发送状态。
    DEAD_REPLAY_WINDOW_MS = 7 * 24 * 60 * 60 * 1000

    def dead_backlog(self) -> dict[str, int]:
        """dead 记录的条数与占用字节数（供容量告警与运维决策）。"""
        with self._transaction() as connection:
            row = connection.execute(
                "SELECT COUNT(*) AS records, COALESCE(SUM(LENGTH(payload)), 0) AS bytes "
                "FROM outbox WHERE state='dead'"
            ).fetchone()
        return {"records": int(row["records"] or 0), "bytes": int(row["bytes"] or 0)}

    def list_dead(
        self,
        *,
        limit: int = 50,
        offset: int = 0,
        device_code: str | None = None,
    ) -> list[dict[str, Any]]:
        """列出 dead 记录（**不含载荷**），供运维决定导出或重放哪些批次。"""
        if not 1 <= limit <= 500:
            raise ValueError("limit must be within 1..500")
        if offset < 0:
            raise ValueError("offset must be >= 0")
        query = (
            "SELECT batch_id, upload_session_id, local_seq, device_code, group_code, "
            "assignment_epoch, attempt_count, reject_count, dead_at, http_status, last_error, "
            "exported_at, replayed_at, replay_ref, "
            "LENGTH(payload) AS payload_bytes FROM outbox WHERE state='dead'"
        )
        params: list[Any] = []
        if device_code:
            query += " AND device_code=?"
            params.append(device_code)
        query += " ORDER BY dead_at IS NULL, dead_at ASC LIMIT ? OFFSET ?"
        params.extend([limit, offset])
        with self._transaction() as connection:
            rows = connection.execute(query, tuple(params)).fetchall()
        return [dict(row) for row in rows]

    def export_dead(
        self,
        batch_id: str,
        upload_session_id: str,
        local_seq: int,
        *,
        actor_role: str = "operator",
    ) -> dict[str, Any] | None:
        """导出单条 dead 记录的完整载荷（base64）；非 dead 或不存在返回 None。

        只是查看：响应可能没送达或调用方没保存成功，所以这里**不**记 ``exported_at``
        （不授予清理凭证），只写一条不含载荷的审计。调用方可靠保存后，用
        ``confirm_dead_export`` 带上 ``payload_sha256`` 显式确认，才允许保留期后清理。
        """
        with self._transaction() as connection:
            row = connection.execute(
                "SELECT batch_id, upload_session_id, local_seq, device_code, group_code, "
                "assignment_epoch, config_version, ts, attempt_count, reject_count, dead_at, "
                "http_status, last_error, exported_at, replayed_at, replay_ref, payload FROM outbox "
                "WHERE batch_id=? AND upload_session_id=? AND local_seq=? AND state='dead'",
                (batch_id, upload_session_id, local_seq),
            ).fetchone()
            if row is None:
                return None
            raw = bytes(row["payload"] or b"")
            now = self._clock_ms()
            digest = hashlib.sha256(raw).hexdigest()
            self._insert_dead_audit(
                connection,
                ts=now,
                actor_role=actor_role,
                action="export",
                batch_id=str(row["batch_id"]),
                session=str(row["upload_session_id"]),
                local_seq=int(row["local_seq"]),
                result="ok",
                file_path=None,
                sha256=digest,
            )
        payload = {key: row[key] for key in row.keys() if key != "payload"}
        payload["payload_sha256"] = digest
        payload["payload_b64"] = base64.b64encode(raw).decode("ascii")
        return payload

    def confirm_dead_export(
        self,
        batch_id: str,
        upload_session_id: str,
        local_seq: int,
        payload_sha256: str,
        *,
        actor_role: str = "operator",
    ) -> bool:
        """调用方已可靠保存单条导出的载荷后显式确认，才记 ``exported_at``。

        按完整身份（batch_id + session + local_seq）匹配，且载荷 sha256 必须与库内一致；
        行已不是 dead、身份不符或摘要不符都返回 False，不授予清理凭证。
        """
        now = self._clock_ms()
        with self._transaction() as connection:
            row = connection.execute(
                "SELECT payload FROM outbox "
                "WHERE batch_id=? AND upload_session_id=? AND local_seq=? AND state='dead'",
                (batch_id, upload_session_id, int(local_seq)),
            ).fetchone()
            digest = (
                None if row is None else hashlib.sha256(bytes(row["payload"] or b"")).hexdigest()
            )
            confirmed = digest is not None and digest == str(payload_sha256)
            if confirmed:
                cursor = connection.execute(
                    "UPDATE outbox SET exported_at=? "
                    "WHERE batch_id=? AND upload_session_id=? AND local_seq=? AND state='dead'",
                    (now, batch_id, upload_session_id, int(local_seq)),
                )
                confirmed = int(cursor.rowcount or 0) == 1
            self._insert_dead_audit(
                connection,
                ts=now,
                actor_role=actor_role,
                action="export_confirm",
                batch_id=batch_id,
                session=upload_session_id,
                local_seq=int(local_seq),
                result="ok" if confirmed else "rejected",
                file_path=None,
                sha256=digest,
            )
        return confirmed

    def export_dead_batch(
        self,
        dest: str | Path,
        *,
        fmt: str,
        device_code: str | None = None,
        since_ms: int | None = None,
        until_ms: int | None = None,
        last_error: str | None = None,
        node_uuid: str | None = None,
        actor_role: str = "operator",
    ) -> dict[str, Any]:
        """按设备、dead_at 时间范围和 last_error 筛选，流式写出 JSONL 或 CSV。

        文件只能写到库旁的 ``dead-exports``（即 ``<data_root>/edge/dead-exports``），
        拒绝目录穿越、符号链接和已存在的文件。分页读出后在事务外写盘并 fsync，
        再开短事务，只给仍然是 dead 且载荷未变的行写 ``exported_at``。
        JSONL 含 ``payload_b64``，可以无损还原；CSV 只有元数据和 payload 的 sha256。
        只有 JSONL 在落盘并回读校验通过后才记 ``exported_at``（允许保留期后清理）；
        CSV 是排障视图，不是备份，只写审计、不授予清理凭证。
        """
        if fmt not in {"jsonl", "csv"}:
            raise ValueError("format must be jsonl or csv")
        where_sql, params = self._dead_where(
            device_code=device_code,
            since_ms=since_ms,
            until_ms=until_ms,
            last_error=last_error,
        )
        return self._export_dead_rows(
            Path(dest),
            fmt=fmt,
            where_sql=where_sql,
            params=params,
            node_uuid=self._resolve_node_uuid(node_uuid),
            actor_role=actor_role,
            action="export_batch",
            now_ms=self._clock_ms(),
        )

    def replay_dead(
        self,
        batch_id: str,
        upload_session_id: str | None = None,
        local_seq: int | None = None,
        *,
        window_ms: int | None = None,
        actor_role: str = "operator",
    ) -> dict[str, Any]:
        """把 dead 载荷重新入队为一条新 outbox 行，原行保持 dead。

        不在原 session 上改状态：旧序号可能已被中心消费。新行使用当前 session
        和新的 ``local_seq``，载荷字节保持原样。``replayed_at`` 已有时直接返回
        原来的 ``replay_ref``，不再生成第二行。分配或载荷里的 config_version
        不再有效时返回 ``assignment_changed``，只允许导出。
        """
        window = window_ms if window_ms is not None else self.DEAD_REPLAY_WINDOW_MS
        if window < 0:
            raise ValueError("window_ms must be >= 0")
        now = self._clock_ms()
        with self._transaction() as connection:
            where = "batch_id=?"
            params: list[Any] = [batch_id]
            if upload_session_id is not None:
                where += " AND upload_session_id=?"
                params.append(upload_session_id)
            if local_seq is not None:
                where += " AND local_seq=?"
                params.append(int(local_seq))
            row = connection.execute(
                "SELECT batch_id, upload_session_id, local_seq, device_code, group_code, "
                "assignment_epoch, config_version, ts, payload, state, dead_at, "
                "replayed_at, replay_ref FROM outbox WHERE " + where,
                tuple(params),
            ).fetchone()
            if row is None:
                self._insert_dead_audit(
                    connection,
                    ts=now,
                    actor_role=actor_role,
                    action="replay",
                    batch_id=batch_id,
                    session=upload_session_id,
                    local_seq=None if local_seq is None else int(local_seq),
                    result="not_found",
                    file_path=None,
                    sha256=None,
                )
                return {"replayed": False, "reason": "not_found"}
            if row["state"] != "dead":
                self._insert_dead_audit(
                    connection,
                    ts=now,
                    actor_role=actor_role,
                    action="replay",
                    batch_id=str(row["batch_id"]),
                    session=str(row["upload_session_id"]),
                    local_seq=int(row["local_seq"]),
                    result="not_dead",
                    file_path=None,
                    sha256=hashlib.sha256(bytes(row["payload"] or b"")).hexdigest(),
                )
                return {"replayed": False, "reason": "not_dead", "state": row["state"]}
            raw = bytes(row["payload"] or b"")
            digest = hashlib.sha256(raw).hexdigest()
            if row["replayed_at"] is not None:
                ref = row["replay_ref"]
                existing = self._replay_target(connection, ref)
                self._insert_dead_audit(
                    connection,
                    ts=now,
                    actor_role=actor_role,
                    action="replay",
                    batch_id=str(row["batch_id"]),
                    session=str(row["upload_session_id"]),
                    local_seq=int(row["local_seq"]),
                    result="already_replayed",
                    file_path=None,
                    sha256=digest,
                )
                return {
                    "replayed": False,
                    "reason": "already_replayed",
                    "replay_ref": ref,
                    **existing,
                }
            dead_at = row["dead_at"]
            if dead_at is not None and now - int(dead_at) > window:
                self._insert_dead_audit(
                    connection,
                    ts=now,
                    actor_role=actor_role,
                    action="replay",
                    batch_id=str(row["batch_id"]),
                    session=str(row["upload_session_id"]),
                    local_seq=int(row["local_seq"]),
                    result="outside_time_window",
                    file_path=None,
                    sha256=digest,
                )
                return {
                    "replayed": False,
                    "reason": "outside_time_window",
                    "dead_at": int(dead_at),
                    "window_ms": window,
                }
            if not self._assignment_allows_replay(connection, row):
                self._insert_dead_audit(
                    connection,
                    ts=now,
                    actor_role=actor_role,
                    action="replay",
                    batch_id=str(row["batch_id"]),
                    session=str(row["upload_session_id"]),
                    local_seq=int(row["local_seq"]),
                    result="assignment_changed",
                    file_path=None,
                    sha256=digest,
                )
                return {
                    "replayed": False,
                    "reason": "assignment_changed",
                    "recovery": "export_only",
                }
            new_batch_id, session_id, new_seq = self._enqueue_replay_copy(connection, row, now)
            self._insert_dead_audit(
                connection,
                ts=now,
                actor_role=actor_role,
                action="replay",
                batch_id=str(row["batch_id"]),
                session=str(row["upload_session_id"]),
                local_seq=int(row["local_seq"]),
                result="replayed",
                file_path=None,
                sha256=digest,
            )
            return {
                "replayed": True,
                "reason": "reenqueued",
                "replay_ref": new_batch_id,
                "upload_session_id": session_id,
                "local_seq": new_seq,
            }

    def maintenance(
        self,
        retention_days: int,
        sent_retention_ms: int | None = None,
        dead_retention_days: int | None = None,
        dead_max_bytes: int | None = None,
        *,
        node_uuid: str | None = None,
        actor_role: str = "maintenance",
    ) -> dict[str, Any]:
        if retention_days < 1:
            raise ValueError("retention_days must be positive")
        dead_days = (
            DEAD_RETENTION_DAYS_DEFAULT
            if dead_retention_days is None
            else int(dead_retention_days)
        )
        if dead_days < 1:
            raise ValueError("dead_retention_days must be positive")
        max_bytes = DEAD_MAX_BYTES_DEFAULT if dead_max_bytes is None else int(dead_max_bytes)
        if max_bytes < 1:
            raise ValueError("dead_max_bytes must be positive")
        now = self._clock_ms()
        actual_sent_retention = sent_retention_ms if sent_retention_ms is not None else SENT_RETENTION_MS
        sent_cutoff = now - actual_sent_retention
        # 离线 backlog 仍看 retention_days。dead 用单独的 dead_days，默认 30。
        retention_cutoff = now - retention_days * 24 * 60 * 60 * 1000
        dead_cutoff = now - dead_days * 24 * 60 * 60 * 1000
        dropped_records = 0
        session_switched = False
        auto_exported = 0
        auto_export_path: str | None = None
        auto_export_failed = False
        disk_now = self._disk_status()
        with self._transaction() as connection:
            # 清理前先量化 dead 积压。未导出且未重放的行不会进入删除。
            dead_before = connection.execute(
                "SELECT COUNT(*) AS records, COALESCE(SUM(LENGTH(payload)), 0) AS bytes "
                "FROM outbox WHERE state='dead'"
            ).fetchone()
            unpreserved = int(
                connection.execute(
                    "SELECT COUNT(*) AS value FROM outbox WHERE state='dead' "
                    "AND exported_at IS NULL AND replayed_at IS NULL"
                ).fetchone()["value"]
                or 0
            )
        dead_bytes_now = int(dead_before["bytes"] or 0)
        pressure = (
            dead_bytes_now > max_bytes
            or disk_now["watermark"] in _DISK_ALARM_WATERMARKS
        )
        # 超字节上限或磁盘告警：只自动导出并标记 exported_at。写文件不占写事务。
        # 已导出或已重放的行仍要 dead_at 早于保留期截止才删，两条删除路径都看这个条件。
        # B002：导出失败（磁盘满、目录不可写）只让未备份 dead 继续受保护，
        # 不能挡住下面独立且安全的 sent 清理，也不能让异常冒泡把上传一起停掉。
        if pressure and unpreserved:
            try:
                written = self._export_dead_rows(
                    self._auto_export_destination(now),
                    fmt="jsonl",
                    where_sql=(
                        "state='dead' AND exported_at IS NULL AND replayed_at IS NULL"
                    ),
                    params=[],
                    node_uuid=self._resolve_node_uuid(node_uuid),
                    actor_role=actor_role,
                    action="auto_export",
                    now_ms=now,
                )
            except Exception:
                auto_export_failed = True
                logger.exception(
                    "Edge outbox dead auto-export failed; unexported dead rows stay protected "
                    "and the export is retried on the next maintenance"
                )
            else:
                auto_exported = int(written["count"])
                if auto_exported:
                    auto_export_path = str(written["path"])
        with self._transaction() as connection:
            sent_cursor = connection.execute(
                """
                DELETE FROM outbox
                WHERE state='sent' AND acked_at IS NOT NULL AND acked_at < ?
                """,
                (sent_cutoff,),
            )
            deleted_terminal = int(sent_cursor.rowcount or 0)
            doomed = connection.execute(
                """
                SELECT batch_id, upload_session_id, local_seq, payload FROM outbox
                WHERE state='dead' AND dead_at IS NOT NULL AND dead_at < ?
                  AND (exported_at IS NOT NULL OR replayed_at IS NOT NULL)
                """,
                (dead_cutoff,),
            ).fetchall()
            for row in doomed:
                self._insert_dead_audit(
                    connection,
                    ts=now,
                    actor_role=actor_role,
                    action="purge",
                    batch_id=str(row["batch_id"]),
                    session=str(row["upload_session_id"]),
                    local_seq=int(row["local_seq"]),
                    result="deleted",
                    file_path=None,
                    sha256=hashlib.sha256(bytes(row["payload"] or b"")).hexdigest(),
                )
            if doomed:
                connection.execute(
                    """
                    DELETE FROM outbox
                    WHERE state='dead' AND dead_at IS NOT NULL AND dead_at < ?
                      AND (exported_at IS NOT NULL OR replayed_at IS NOT NULL)
                    """,
                    (dead_cutoff,),
                )
            dead_purged = len(doomed)
            deleted_terminal += dead_purged
            oldest = connection.execute(
                """
                SELECT MIN(created_at) AS value FROM outbox
                WHERE state IN ('pending', 'retry_wait', 'inflight')
                """
            ).fetchone()
            backlog_expired = bool(
                oldest is not None
                and oldest["value"] is not None
                and int(oldest["value"]) < retention_cutoff
            )
            backlog_count = int(
                connection.execute(
                    """
                    SELECT COUNT(*) AS value FROM outbox
                    WHERE state IN ('pending', 'retry_wait', 'inflight')
                    """
                ).fetchone()["value"]
                or 0
            )

        checkpoint = "passive"
        reclaimed_pages = 0
        try:
            with self._lock:
                mode = "TRUNCATE" if deleted_terminal else "PASSIVE"
                self._connection.execute(f"PRAGMA wal_checkpoint({mode})").fetchone()
                checkpoint = mode.lower()
                auto_vacuum = int(self._connection.execute("PRAGMA auto_vacuum").fetchone()[0])
                if deleted_terminal and auto_vacuum == 2:
                    before = int(self._connection.execute("PRAGMA freelist_count").fetchone()[0])
                    self._connection.execute("PRAGMA incremental_vacuum(4096)")
                    after = int(self._connection.execute("PRAGMA freelist_count").fetchone()[0])
                    reclaimed_pages = max(0, before - after)
        except sqlite3.OperationalError:
            checkpoint = "busy"

        disk = self._disk_status()
        dead_records = int(dead_before["records"] or 0)
        dead_bytes = int(dead_before["bytes"] or 0)

        degraded_reasons: list[str] = []
        if disk["watermark"] == "overflow":
            degraded_reasons.append("disk_overflow")
        if backlog_expired:
            degraded_reasons.append("backlog_exceeds_retention_window")
        if auto_export_failed:
            degraded_reasons.append("dead_auto_export_failed")
        degraded = bool(degraded_reasons)
        if degraded:
            # Retention is an operational signal, not an acknowledgement.
            # Never rotate the session or delete unconfirmed payloads here.
            logger.error(
                "Edge outbox degraded reasons=%s; preserving unconfirmed payloads",
                ",".join(degraded_reasons),
            )

        # B6：dead 的容量告警单独成列，不改动上面 degraded 的语义（避免任何 dead 记录都把
        # 整机状态拉成 degraded）。第一条在**载荷仍存在**时给出，运维据此导出或重放。
        capacity_alerts: list[str] = []
        if dead_records > 0:
            capacity_alerts.append("dead_backlog_present_export_or_replay")
            logger.error(
                "Edge outbox holds %d dead record(s) / %d bytes; export or replay them "
                "before retention can purge them. Unexported and unreplayed rows are kept",
                dead_records,
                dead_bytes,
            )
        if dead_purged > 0:
            capacity_alerts.append("dead_records_purged_requires_export")
            logger.error(
                "Edge outbox purged %d preserved dead record(s) under dead_retention_days=%d",
                dead_purged,
                dead_days,
            )

        return {
            "deleted_terminal": deleted_terminal,
            "dropped_records": dropped_records,
            "session_switched": session_switched,
            "degraded": degraded,
            "degraded_reasons": degraded_reasons,
            "capacity_alerts": capacity_alerts,
            "dead_records": dead_records,
            "dead_bytes": dead_bytes,
            "dead_purged": dead_purged,
            "dead_auto_exported": auto_exported,
            "dead_auto_export_path": auto_export_path,
            "dead_auto_export_failed": auto_export_failed,
            "dead_retention_days": dead_days,
            "backlog_expired": backlog_expired,
            "backlog_count": backlog_count,
            "checkpoint": checkpoint,
            "reclaimed_pages": reclaimed_pages,
            **self._disk_status(),
        }

    def _auto_export_destination(self, now_ms: int) -> Path:
        """``<db 所在目录>/dead-exports/auto-YYYYMMDD.jsonl``。日期是 UTC。

        库在 ``<data_root>/edge/edge.db`` 时，这就是
        ``<data_root>/edge/dead-exports/``。同一天再次导出时另起文件，不覆盖已有文件。
        """
        directory = self.path.parent / "dead-exports"
        day = time.strftime("%Y%m%d", time.gmtime(now_ms / 1000.0))
        primary = directory / f"auto-{day}.jsonl"
        if not primary.exists():
            return primary
        stamp = time.strftime("%H%M%S", time.gmtime(now_ms / 1000.0))
        secondary = directory / f"auto-{day}-{stamp}.jsonl"
        if not secondary.exists():
            return secondary
        return directory / f"auto-{day}-{stamp}-{uuid4().hex[:8]}.jsonl"

    def _resolve_node_uuid(self, node_uuid: str | None) -> str:
        if node_uuid:
            return str(node_uuid)
        configured = os.environ.get("DATAWATCH_NODE_UUID", "").strip()
        if configured:
            return configured
        with self._lock:
            row = self._connection.execute(
                "SELECT value FROM metadata WHERE key='node_uuid'"
            ).fetchone()
        return str(row["value"]) if row else ""

    @staticmethod
    def _dead_where(
        *,
        device_code: str | None = None,
        since_ms: int | None = None,
        until_ms: int | None = None,
        last_error: str | None = None,
    ) -> tuple[str, list[Any]]:
        clauses = ["state='dead'"]
        params: list[Any] = []
        if device_code:
            clauses.append("device_code=?")
            params.append(device_code)
        if since_ms is not None:
            clauses.append("dead_at IS NOT NULL AND dead_at>=?")
            params.append(int(since_ms))
        if until_ms is not None:
            clauses.append("dead_at IS NOT NULL AND dead_at<=?")
            params.append(int(until_ms))
        if last_error is not None:
            clauses.append("last_error=?")
            params.append(last_error)
        return " AND ".join(clauses), params

    def _export_root(self) -> Path:
        """导出目录：库文件旁边的 ``dead-exports``，不接受符号链接。"""
        link = self.path.parent / "dead-exports"
        if link.is_symlink():
            raise ValueError("dead-exports directory must not be a symlink")
        link.mkdir(parents=True, exist_ok=True)
        if link.is_symlink() or not link.is_dir():
            raise ValueError("dead-exports directory must not be a symlink")
        return link.resolve()

    def _validated_export_destination(self, dest: Path) -> Path:
        """只允许 ``<data_root>/edge/dead-exports/<文件名>``，拒绝穿越、符号链接和已有文件。"""
        root = self._export_root()
        raw = Path(dest)
        candidate = raw if raw.is_absolute() else root / raw
        if candidate.name in {"", ".", ".."}:
            raise ValueError("export file name is invalid")
        if candidate.is_symlink() or candidate.parent.is_symlink():
            raise ValueError("export path must not be a symlink")
        if candidate.parent.resolve() != root:
            raise ValueError("export path must stay inside dead-exports")
        final = root / candidate.name
        if final.is_symlink():
            raise ValueError("export path must not be a symlink")
        if final.exists():
            raise ValueError("export file already exists")
        return final

    def _export_dead_rows(
        self,
        dest: Path,
        *,
        fmt: str,
        where_sql: str,
        params: list[Any],
        node_uuid: str,
        actor_role: str,
        action: str,
        now_ms: int,
    ) -> dict[str, Any]:
        destination = self._validated_export_destination(dest)
        identities, file_sha, written_path = self._stream_dead_export_file(
            destination,
            fmt=fmt,
            where_sql=where_sql,
            params=params,
            node_uuid=node_uuid,
        )
        # B001：只有完整载荷（JSONL）且回读校验通过才是可恢复备份；CSV 只审计。
        is_backup = fmt == "jsonl"
        if is_backup and written_path is not None:
            self._verify_export_file(written_path, file_sha)
        marked = self._mark_exported_snapshot(
            identities,
            dest=written_path,
            actor_role=actor_role,
            action=action,
            now_ms=now_ms,
            set_exported=is_backup,
        )
        return {
            "count": marked,
            "backup": is_backup,
            "path": str(written_path) if written_path is not None else str(destination),
            "sha256": file_sha,
            "format": fmt,
            "schema_version": DEAD_EXPORT_SCHEMA_VERSION,
            "node_uuid": node_uuid,
        }

    def _snapshot_dead_page(
        self,
        where_sql: str,
        params: list[Any],
        *,
        limit: int,
        offset: int,
    ) -> list[dict[str, Any]]:
        columns = ", ".join(_DEAD_EXPORT_META_FIELDS)
        query = (
            f"SELECT {columns}, payload FROM outbox WHERE {where_sql} "
            "ORDER BY dead_at IS NULL, dead_at ASC, batch_id ASC LIMIT ? OFFSET ?"
        )
        with self._transaction() as connection:
            fetched = connection.execute(query, (*params, limit, offset)).fetchall()
            copied: list[dict[str, Any]] = []
            for row in fetched:
                item = {name: row[name] for name in _DEAD_EXPORT_META_FIELDS}
                item["payload"] = bytes(row["payload"] or b"")
                copied.append(item)
            return copied

    def _stream_dead_export_file(
        self,
        dest: Path,
        *,
        fmt: str,
        where_sql: str,
        params: list[Any],
        node_uuid: str,
    ) -> tuple[list[tuple[str, str, int, str]], str, Path | None]:
        """分页读取，事务松开后再写盘。没有匹配行时仍写出只含文件头的新文件。"""
        offset = 0
        first = self._snapshot_dead_page(
            where_sql, params, limit=_DEAD_EXPORT_PAGE_SIZE, offset=offset
        )
        temporary = dest.with_name(dest.name + f".{uuid4().hex}.tmp")
        file_digest = hashlib.sha256()
        identities: list[tuple[str, str, int, str]] = []
        descriptor = os.open(temporary, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
        try:
            with os.fdopen(descriptor, "wb") as handle:
                descriptor = -1

                def emit(chunk: bytes) -> None:
                    handle.write(chunk)
                    file_digest.update(chunk)

                if fmt == "jsonl":
                    emit(
                        msgspec.json.encode(
                            {
                                "kind": "header",
                                "schema_version": DEAD_EXPORT_SCHEMA_VERSION,
                                "node_uuid": node_uuid,
                            }
                        )
                        + b"\n"
                    )
                else:
                    emit(
                        f"# schema_version={DEAD_EXPORT_SCHEMA_VERSION} node_uuid={node_uuid}\n".encode(
                            "utf-8"
                        )
                    )
                    emit(self._csv_line([*_DEAD_EXPORT_META_FIELDS, "payload_sha256"]))
                page = first
                while page:
                    for row in page:
                        raw = bytes(row["payload"] or b"")
                        digest = hashlib.sha256(raw).hexdigest()
                        if fmt == "jsonl":
                            record = {name: row[name] for name in _DEAD_EXPORT_META_FIELDS}
                            record["payload_sha256"] = digest
                            record["payload_b64"] = base64.b64encode(raw).decode("ascii")
                            emit(msgspec.json.encode(record) + b"\n")
                        else:
                            values = [
                                "" if row[name] is None else row[name]
                                for name in _DEAD_EXPORT_META_FIELDS
                            ]
                            values.append(digest)
                            emit(self._csv_line(values))
                        identities.append(
                            (
                                str(row["batch_id"]),
                                str(row["upload_session_id"]),
                                int(row["local_seq"]),
                                digest,
                            )
                        )
                    if len(page) < _DEAD_EXPORT_PAGE_SIZE:
                        break
                    offset += len(page)
                    page = self._snapshot_dead_page(
                        where_sql, params, limit=_DEAD_EXPORT_PAGE_SIZE, offset=offset
                    )
                self._fsync_export_handle(handle)
            self._publish_export_file(temporary, dest)
        except Exception:
            if descriptor >= 0:
                os.close(descriptor)
            temporary.unlink(missing_ok=True)
            raise
        return identities, file_digest.hexdigest(), dest

    @staticmethod
    def _verify_export_file(path: Path, expected_sha256: str) -> None:
        """回读已发布的导出文件，摘要与写入时一致才算备份完成。"""
        digest = hashlib.sha256()
        with open(path, "rb") as handle:
            for chunk in iter(lambda: handle.read(1024 * 1024), b""):
                digest.update(chunk)
        if digest.hexdigest() != expected_sha256:
            raise OSError(f"dead export verification failed: {path}")

    def _fsync_export_handle(self, handle) -> None:
        handle.flush()
        os.fsync(handle.fileno())

    @staticmethod
    def _publish_export_file(temporary: Path, dest: Path) -> None:
        """硬链接到目标名。目标已存在或是符号链接时失败，不覆盖。"""
        if dest.is_symlink() or dest.exists():
            raise ValueError("export file already exists")
        try:
            os.link(temporary, dest)
        except FileExistsError as exc:
            raise ValueError("export file already exists") from exc
        finally:
            temporary.unlink(missing_ok=True)

    def _mark_exported_snapshot(
        self,
        identities: list[tuple[str, str, int, str]],
        *,
        dest: Path | None,
        actor_role: str,
        action: str,
        now_ms: int,
        set_exported: bool = True,
    ) -> int:
        if not identities or dest is None:
            return 0
        marked = 0
        file_path = str(dest)
        with self._transaction() as connection:
            for batch_id, session, seq, digest in identities:
                row = connection.execute(
                    "SELECT state, payload FROM outbox "
                    "WHERE batch_id=? AND upload_session_id=? AND local_seq=?",
                    (batch_id, session, seq),
                ).fetchone()
                if row is None or row["state"] != "dead":
                    continue
                if hashlib.sha256(bytes(row["payload"] or b"")).hexdigest() != digest:
                    continue
                if set_exported:
                    cursor = connection.execute(
                        "UPDATE outbox SET exported_at=? "
                        "WHERE batch_id=? AND upload_session_id=? AND local_seq=? AND state='dead'",
                        (now_ms, batch_id, session, seq),
                    )
                    if int(cursor.rowcount or 0) != 1:
                        continue
                self._insert_dead_audit(
                    connection,
                    ts=now_ms,
                    actor_role=actor_role,
                    action=action,
                    batch_id=batch_id,
                    session=session,
                    local_seq=seq,
                    result="ok" if set_exported else "ok_view_only",
                    file_path=file_path,
                    sha256=digest,
                )
                marked += 1
        return marked

    @staticmethod
    def _csv_line(values: list[Any]) -> bytes:
        buffer = io.StringIO()
        csv.writer(buffer, lineterminator="\n").writerow(values)
        return buffer.getvalue().encode("utf-8")

    def _assignment_allows_replay(self, connection: sqlite3.Connection, row: sqlite3.Row) -> bool:
        """载荷 config_version 与当前设备分配仍然一致时才允许重新入队。"""
        try:
            decoded = msgspec.json.decode(bytes(row["payload"] or b""))
        except Exception:
            return False
        if not isinstance(decoded, dict):
            return False
        try:
            payload_version = int(decoded.get("config_version"))
        except (TypeError, ValueError):
            return False
        if payload_version != int(row["config_version"]):
            return False
        device_code = str(row["device_code"])
        assignment = connection.execute(
            "SELECT assignment_epoch, enabled FROM device_assignment WHERE device_code=?",
            (device_code,),
        ).fetchone()
        if assignment is None or int(assignment["enabled"]) != 1:
            return False
        if int(assignment["assignment_epoch"]) != int(row["assignment_epoch"]):
            return False
        device = connection.execute(
            """
            SELECT config_version, assignment_epoch, enabled
            FROM edge_device WHERE device_code=?
            """,
            (device_code,),
        ).fetchone()
        if device is None:
            # 只有分配表时，分配行本身就是有效性依据（单测与尚未下发快照的节点）。
            return True
        if int(device["enabled"]) != 1:
            return False
        if int(device["assignment_epoch"]) != int(row["assignment_epoch"]):
            return False
        return int(device["config_version"]) == payload_version

    def _enqueue_replay_copy(
        self,
        connection: sqlite3.Connection,
        row: sqlite3.Row,
        now: int,
    ) -> tuple[str, str, int]:
        """用原载荷插入一条新的 pending 行，并在原 dead 行记下 replay_ref。"""
        raw = bytes(row["payload"] or b"")
        session_id = self._metadata(connection, "upload_session_id")
        connection.execute(
            "UPDATE metadata SET value = CAST(value AS INTEGER) + 1 WHERE key = 'last_seq'"
        )
        local_seq = int(self._metadata(connection, "last_seq"))
        new_batch_id = str(uuid4())
        connection.execute(
            """
            INSERT INTO outbox(
                batch_id, upload_session_id, local_seq,
                device_code, group_code, assignment_epoch,
                config_version, ts, payload, created_at
            ) VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                new_batch_id,
                session_id,
                local_seq,
                str(row["device_code"]),
                str(row["group_code"]),
                int(row["assignment_epoch"]),
                int(row["config_version"]),
                int(row["ts"]),
                raw,
                now,
            ),
        )
        connection.execute(
            "UPDATE outbox SET replayed_at=?, replay_ref=? WHERE batch_id=?",
            (now, new_batch_id, str(row["batch_id"])),
        )
        return new_batch_id, session_id, local_seq

    @staticmethod
    def _replay_target(connection: sqlite3.Connection, replay_ref: str | None) -> dict[str, Any]:
        if not replay_ref:
            return {"upload_session_id": None, "local_seq": None}
        found = connection.execute(
            "SELECT upload_session_id, local_seq FROM outbox WHERE batch_id=?",
            (str(replay_ref),),
        ).fetchone()
        if found is None:
            return {"upload_session_id": None, "local_seq": None}
        return {
            "upload_session_id": str(found["upload_session_id"]),
            "local_seq": int(found["local_seq"]),
        }

    @staticmethod
    def _insert_dead_audit(
        connection: sqlite3.Connection,
        *,
        ts: int,
        actor_role: str,
        action: str,
        batch_id: str | None,
        session: str | None,
        local_seq: int | None,
        result: str,
        file_path: str | None,
        sha256: str | None,
    ) -> None:
        # 审计只记身份和结果，不写载荷，也不写 token。
        connection.execute(
            """
            INSERT INTO dead_audit(
                ts, actor_role, action, batch_id, session, local_seq, result, file_path, sha256
            ) VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                int(ts),
                str(actor_role or ""),
                str(action),
                batch_id,
                session,
                local_seq,
                str(result),
                file_path,
                sha256,
            ),
        )

    def _ensure_disk_writable(self) -> None:
        if self._disk_status()["watermark"] == "overflow":
            raise RuntimeError(
                "Edge outbox disk watermark is overflow; refusing new durable data"
            )

    def _disk_status(self) -> dict[str, Any]:
        usage = shutil.disk_usage(self.path.parent)
        used_percent = round((usage.used / usage.total) * 100, 2) if usage.total else 0.0
        if used_percent >= 95:
            watermark = "overflow"
        elif used_percent >= 85:
            watermark = "critical"
        elif used_percent >= 70:
            watermark = "warning"
        else:
            watermark = "normal"
        return {
            "disk_total_bytes": int(usage.total),
            "disk_free_bytes": int(usage.free),
            "disk_used_percent": used_percent,
            "watermark": watermark,
        }

    def operational_status(self) -> dict[str, Any]:
        with self._lock:
            rows = self._connection.execute(
                "SELECT state, COUNT(*) AS count FROM outbox GROUP BY state"
            ).fetchall()
            oldest = self._connection.execute(
                """
                SELECT MIN(created_at) AS value FROM outbox
                WHERE state IN ('pending', 'retry_wait', 'inflight')
                """
            ).fetchone()
            pending_switch = self._connection.execute(
                "SELECT value FROM metadata WHERE key='pending_session_switch_old_id'"
            ).fetchone()
            session_id = self._metadata(self._connection, "upload_session_id")
            last_seq = int(self._metadata(self._connection, "last_seq"))
            dropped_records = int(self._metadata(self._connection, "dropped_records"))
            sent_records_total = int(self._metadata(self._connection, "sent_records_total"))
            page_count = int(self._connection.execute("PRAGMA page_count").fetchone()[0])
            page_size = int(self._connection.execute("PRAGMA page_size").fetchone()[0])
            free_pages = int(self._connection.execute("PRAGMA freelist_count").fetchone()[0])
            auto_vacuum = int(self._connection.execute("PRAGMA auto_vacuum").fetchone()[0])
        counts = {str(row["state"]): int(row["count"]) for row in rows}
        oldest_at = int(oldest["value"]) if oldest and oldest["value"] is not None else None
        files = (self.path, Path(str(self.path) + "-wal"), Path(str(self.path) + "-shm"))
        return {
            "counts": counts,
            "backlog_count": sum(counts.get(name, 0) for name in ("pending", "retry_wait", "inflight")),
            "oldest_backlog_at": oldest_at,
            "upload_session_id": session_id,
            "last_seq": last_seq,
            "upload_interval_ms": self.upload_interval_ms(),
            "session_switch_pending": pending_switch is not None,
            "database_bytes": sum(path.stat().st_size for path in files if path.exists()),
            "database_live_bytes": max(0, page_count - free_pages) * page_size,
            "database_reclaimable_bytes": free_pages * page_size,
            "database_free_page_ratio": round(free_pages / page_count, 4) if page_count else 0.0,
            "auto_vacuum_mode": auto_vacuum,
            "dropped_records": dropped_records,
            "sent_records_total": sent_records_total,
            **self._disk_status(),
        }

    def _append_envelope(
        self,
        connection: sqlite3.Connection,
        *,
        batch_id: str,
        device_code: str,
        device_type: str,
        group_code: str,
        scan_rate_ms: int,
        ts: int,
        config_version: int,
        values: dict[str, str],
        qualities: dict[str, int] | None,
        now: int,
        latest_fields: dict[str, dict[str, Any]] | None = None,
    ) -> OutboxRecord:
        existing = connection.execute(
            """
            SELECT batch_id, upload_session_id, local_seq, assignment_epoch,
                   payload, attempt_count, reject_count
            FROM outbox WHERE batch_id=?
            """,
            (batch_id,),
        ).fetchone()
        if existing is not None:
            return OutboxRecord(
                batch_id=str(existing["batch_id"]),
                upload_session_id=str(existing["upload_session_id"]),
                local_seq=int(existing["local_seq"]),
                assignment_epoch=int(existing["assignment_epoch"]),
                payload=bytes(existing["payload"]),
                attempt_count=int(existing["attempt_count"]),
                reject_count=int(existing["reject_count"]),
            )

        assignment = connection.execute(
            """
            SELECT assignment_epoch
            FROM device_assignment
            WHERE device_code = ? AND enabled = 1
            """,
            (device_code,),
        ).fetchone()
        if assignment is None:
            raise ValueError(f"no active assignment for device {device_code}")

        session_id = self._metadata(connection, "upload_session_id")
        connection.execute(
            "UPDATE metadata SET value = CAST(value AS INTEGER) + 1 WHERE key = 'last_seq'"
        )
        local_seq = int(self._metadata(connection, "last_seq"))
        payload: dict[str, Any] = {
            "device_code": device_code,
            "device_type": device_type,
            "group_code": group_code,
            "scan_rate": scan_rate_ms,
            "ts": ts,
            "config_version": config_version,
            "values": values,
        }
        if qualities is not None:
            payload["contract_version"] = 2
            payload["qualities"] = qualities
        if latest_fields is not None:
            # 完整实时字段与历史子集分离
            # B022：v 为 None 的坏质量点不得被 str() 成 "None" 之类的伪值，只走质量/时间戳。
            payload["latest_values"] = {
                str(k): str(v["v"]) for k, v in latest_fields.items() if v.get("v") is not None
            }
            payload["latest_timestamps"] = {str(k): int(v.get("t", ts)) for k, v in latest_fields.items()}
            payload["latest_qualities"] = {
                str(k): int(v.get("q", 0)) for k, v in latest_fields.items()
            }
        encoded = msgspec.json.encode(payload)
        connection.execute(
            """
            INSERT INTO outbox(
                batch_id, upload_session_id, local_seq,
                device_code, group_code, assignment_epoch,
                config_version, ts, payload, created_at
            ) VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                batch_id,
                session_id,
                local_seq,
                device_code,
                group_code,
                int(assignment["assignment_epoch"]),
                config_version,
                ts,
                encoded,
                now,
            ),
        )
        return OutboxRecord(
            batch_id,
            session_id,
            local_seq,
            int(assignment["assignment_epoch"]),
            encoded,
            0,
            0,
        )

    def _transaction(self):
        return _Transaction(self._connection, self._lock)

    @staticmethod
    def _write_local_latest_frame(
        connection: sqlite3.Connection,
        device_code: str,
        group_code: str,
        fields: dict[str, dict[str, Any]],
        now: int,
    ) -> None:
        if not fields:
            return
        row = connection.execute(
            "SELECT values_json FROM local_latest_frame WHERE device_code = ? AND group_code = ?",
            (device_code, group_code),
        ).fetchone()
        merged: dict[str, Any] = {}
        if row is not None and row["values_json"]:
            try:
                decoded = msgspec.json.decode(row["values_json"])
                if isinstance(decoded, dict):
                    merged = dict(decoded)
            except Exception:
                merged = {}

        # 检查当前配置中存在的有效变量，过滤已被删除的点位，避免已删点位永久残留
        active_vars_rows = connection.execute(
            "SELECT variable_id FROM edge_variable WHERE device_code = ? AND group_code = ?",
            (device_code, group_code),
        ).fetchall()
        if connection.execute("SELECT 1 FROM edge_config_meta WHERE singleton=1").fetchone():
            active_ids = {str(r["variable_id"]) for r in active_vars_rows}
            merged = {k: v for k, v in merged.items() if k in active_ids}
            fields = {k: v for k, v in fields.items() if k in active_ids}
        for key, value in fields.items():
            if key not in merged or int(value.get("t", 0)) >= int(merged[key].get("t", 0)):
                merged[key] = value

        connection.execute(
            """
            INSERT INTO local_latest_frame(
                device_code, group_code, values_json, updated_at
            ) VALUES(?, ?, ?, ?)
            ON CONFLICT(device_code, group_code) DO UPDATE SET
                values_json=excluded.values_json,
                updated_at=excluded.updated_at
            """,
            (device_code, group_code, msgspec.json.encode(merged), now),
        )

    @staticmethod
    def _metadata(connection: sqlite3.Connection, key: str) -> str:
        row = connection.execute(
            "SELECT value FROM metadata WHERE key=?",
            (key,),
        ).fetchone()
        if row is None:
            raise RuntimeError(f"missing metadata: {key}")
        return str(row["value"])


class _Transaction:
    def __init__(self, connection: sqlite3.Connection, lock: threading.RLock) -> None:
        self._connection = connection
        self._lock = lock

    def __enter__(self) -> sqlite3.Connection:
        self._lock.acquire()
        try:
            self._connection.execute("BEGIN IMMEDIATE")
        except Exception:
            self._lock.release()
            raise
        return self._connection

    def __exit__(self, exc_type, exc, tb) -> bool:
        try:
            self._connection.execute("ROLLBACK" if exc_type else "COMMIT")
        except Exception:
            try:
                if self._connection.in_transaction:
                    self._connection.execute("ROLLBACK")
            except sqlite3.Error:
                pass
            raise
        finally:
            self._lock.release()
        return False
