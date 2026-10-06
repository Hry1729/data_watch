from __future__ import annotations

import base64
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

from edge.app.snapshot_schema import CONFIG_SNAPSHOT_SCHEMA


SENT_RETENTION_MS = 60_000  # 默认 60s 兼容已有直接调用，生产 uploader 传入 24h 参数
logger = logging.getLogger("edge.outbox")

# B6：坏数据隔离预算（``reject_count``）只由「中心给出了协议级裁决」的失败消耗。
# 传输类失败（无 http_status、429/5xx/断网）只累积 ``attempt_count``（退避用），
# 绝不消耗隔离预算 —— 否则长时间断网的历史网络次数会让一次序列异常把好数据判死。
# 取值与 ``edge/app/uploader.py`` 中 ``quarantine_after_max=True`` 的两个调用点严格一致。
PROTOCOL_REJECT_ERRORS = frozenset(
    {
        "http_400_contract_rejected",  # uploader.py: HTTP 400 契约拒绝
        "seq_reject",  # uploader.py: ACK 状态 seq_reject
        "commit_unknown",  # uploader.py: ACK 状态 commit_unknown
    }
)

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
        return int(row["value"]) if row and row["value"] is not None else None

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
            for local_seq, row in enumerate(pending_rows, start=1):
                connection.execute(
                    """
                    UPDATE outbox
                    SET upload_session_id=?,
                        local_seq=?,
                        state='pending',
                        next_retry_at=NULL,
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

    def stats(self) -> dict[str, int]:
        with self._lock:
            rows = self._connection.execute(
                "SELECT state, COUNT(*) AS count FROM outbox GROUP BY state"
            ).fetchall()
        return {str(row["state"]): int(row["count"]) for row in rows}

    # ── B6：dead 载荷的查看 / 导出 / 受控重放 ──────────────────────────────────
    # 背景：maintenance() 会按 retention_days 删除 dead 行。在这些入口存在之前，运维
    # **没有任何途径**查看或取回即将被删除的唯一载荷——即"没有恢复入口却自动删除"。
    # 现在先可恢复（查看/导出）、再有条件地重放，删除权限不被自动放大。

    # 重放时间窗：dead 超过该时长后只允许**导出**，不在原位重放
    # （避免把久远的历史帧混入当前 session 的序列与幂等边界）。
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
    ) -> dict[str, Any] | None:
        """导出单条 dead 记录的**完整载荷**（base64）；非 dead 或不存在返回 None。"""
        with self._transaction() as connection:
            row = connection.execute(
                "SELECT batch_id, upload_session_id, local_seq, device_code, group_code, "
                "assignment_epoch, attempt_count, reject_count, dead_at, http_status, last_error, "
                "payload FROM outbox "
                "WHERE batch_id=? AND upload_session_id=? AND local_seq=? AND state='dead'",
                (batch_id, upload_session_id, local_seq),
            ).fetchone()
        if row is None:
            return None
        payload = dict(row)
        raw = payload.pop("payload")
        payload["payload_b64"] = base64.b64encode(bytes(raw or b"")).decode("ascii")
        return payload

    def replay_dead(
        self,
        batch_id: str,
        upload_session_id: str,
        local_seq: int,
        *,
        window_ms: int | None = None,
    ) -> dict[str, Any]:
        """检查 dead 恢复请求；无法证明身份仍可提交时保留载荷并明确拒绝。

        约束（缺一不可）：
        - **幂等**：只改动 ``state='dead'`` 的行；重复调用返回 ``replayed=False`` +
          ``reason='not_dead'``，既不报错也不重复计数；
        - **时间窗**：``dead_at`` 早于窗口的只允许导出、不原位重放（``outside_time_window``）；
        - **身份**：旧会话或已被中心消费的序号不能靠修改本地状态重放。当前没有中心
          身份核对/新身份分配协议，故窗口内也返回 ``identity_requires_reconciliation``；
          保持 dead、预算及审计不变，运维应导出载荷并人工核对。
        - **权限**：由 IPC 层保证——``outbox.dead_replay`` 只授予 uploader 角色。
        """
        window = window_ms if window_ms is not None else self.DEAD_REPLAY_WINDOW_MS
        if window < 0:
            raise ValueError("window_ms must be >= 0")
        now = self._clock_ms()
        with self._transaction() as connection:
            row = connection.execute(
                "SELECT state, dead_at FROM outbox "
                "WHERE batch_id=? AND upload_session_id=? AND local_seq=?",
                (batch_id, upload_session_id, local_seq),
            ).fetchone()
            if row is None:
                return {"replayed": False, "reason": "not_found"}
            if row["state"] != "dead":
                return {"replayed": False, "reason": "not_dead", "state": row["state"]}
            dead_at = row["dead_at"]
            if dead_at is not None and now - int(dead_at) > window:
                return {
                    "replayed": False,
                    "reason": "outside_time_window",
                    "dead_at": int(dead_at),
                    "window_ms": window,
                }
            return {"replayed": False, "reason": "identity_requires_reconciliation",
                    "state": "dead", "recovery": "export_and_reconcile"}

    def maintenance(
        self,
        retention_days: int,
        sent_retention_ms: int | None = None,
    ) -> dict[str, Any]:
        if retention_days < 1:
            raise ValueError("retention_days must be positive")
        now = self._clock_ms()
        actual_sent_retention = sent_retention_ms if sent_retention_ms is not None else SENT_RETENTION_MS
        sent_cutoff = now - actual_sent_retention
        retention_cutoff = now - retention_days * 24 * 60 * 60 * 1000
        dropped_records = 0
        session_switched = False
        with self._transaction() as connection:
            # B6：清理**之前**先量化 dead 的"当前积压"与"即将被删"，让运维在载荷仍存在时
            # 就得到可观测信号。恢复入口（dead_list/export/replay）存在之后，删除仍可能是
            # 无声的——容量告警补上这一环。
            dead_before = connection.execute(
                "SELECT COUNT(*) AS records, COALESCE(SUM(LENGTH(payload)), 0) AS bytes "
                "FROM outbox WHERE state='dead'"
            ).fetchone()
            dead_purged = int(
                connection.execute(
                    "SELECT COUNT(*) AS value FROM outbox "
                    "WHERE state='dead' AND dead_at IS NOT NULL AND dead_at < ?",
                    (retention_cutoff,),
                ).fetchone()["value"]
                or 0
            )
            terminal = connection.execute(
                """
                DELETE FROM outbox
                WHERE (state='sent' AND acked_at IS NOT NULL AND acked_at < ?)
                   OR (state='dead' AND dead_at IS NOT NULL AND dead_at < ?)
                """,
                (sent_cutoff, retention_cutoff),
            )
            deleted_terminal = int(terminal.rowcount or 0)
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
                "(outbox.dead_list / dead_export / dead_replay) before retention purges them",
                dead_records,
                dead_bytes,
            )
        if dead_purged > 0:
            capacity_alerts.append("dead_records_purged_requires_export")
            logger.error(
                "Edge outbox purged %d dead record(s) under retention_days=%d; "
                "if those payloads were still needed they must be exported beforehand",
                dead_purged,
                retention_days,
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
            "backlog_expired": backlog_expired,
            "backlog_count": backlog_count,
            "checkpoint": checkpoint,
            "reclaimed_pages": reclaimed_pages,
            **self._disk_status(),
        }

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
            payload["latest_values"] = {
                str(k): str(v["v"]) for k, v in latest_fields.items() if "v" in v
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
