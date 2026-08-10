from __future__ import annotations

import shutil
import sqlite3
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any
from uuid import uuid4

import msgspec

from edge.app.snapshot_schema import CONFIG_SNAPSHOT_SCHEMA


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
"""


@dataclass(frozen=True, slots=True)
class OutboxRecord:
    batch_id: str
    upload_session_id: str
    local_seq: int
    assignment_epoch: int
    payload: bytes
    attempt_count: int


@dataclass(frozen=True, slots=True)
class SessionSwitchState:
    old_session_id: str
    new_session_id: str


@dataclass(frozen=True, slots=True)
class OutboxCompletion:
    record: OutboxRecord
    state: str
    error: str | None = None



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
            self._connection.execute("PRAGMA journal_mode=WAL")
            self._connection.execute("PRAGMA synchronous=NORMAL")
            self._connection.execute("PRAGMA busy_timeout=5000")
            self._connection.executescript(OUTBOX_SCHEMA)
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
    ) -> OutboxRecord:
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
        now = self._clock_ms()
        with self._transaction() as connection:
            record = self._append_envelope(
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
                now=now,
            )
            rows = [
                (device_code, group_code, str(var_id), msgspec.json.encode(value), now)
                for var_id, value in latest_fields.items()
            ]
            if rows:
                connection.executemany(
                    """
                    INSERT INTO local_latest(
                        device_code, group_code, var_id, value_json, updated_at
                    ) VALUES(?, ?, ?, ?, ?)
                    ON CONFLICT(device_code, group_code, var_id) DO UPDATE SET
                        value_json=excluded.value_json,
                        updated_at=excluded.updated_at
                    """,
                    rows,
                )
            return record

    def write_local_latest(
        self,
        device_code: str,
        group_code: str,
        fields: dict[str, dict[str, Any]],
    ) -> None:
        now = self._clock_ms()
        rows = [
            (device_code, group_code, str(var_id), msgspec.json.encode(value), now)
            for var_id, value in fields.items()
        ]
        with self._transaction() as connection:
            connection.executemany(
                """
                INSERT INTO local_latest(
                    device_code, group_code, var_id, value_json, updated_at
                ) VALUES(?, ?, ?, ?, ?)
                ON CONFLICT(device_code, group_code, var_id) DO UPDATE SET
                    value_json=excluded.value_json,
                    updated_at=excluded.updated_at
                """,
                rows,
            )

    def read_local_latest(self, device_code: str, group_code: str) -> dict[str, Any]:
        with self._lock:
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
        with self._transaction() as connection:
            session_id = self._metadata(connection, "upload_session_id")
            rows = connection.execute(
                """
                SELECT batch_id, upload_session_id, local_seq, assignment_epoch,
                       payload, attempt_count, state, next_retry_at, lease_until,
                       lease_owner
                FROM outbox
                WHERE upload_session_id = ?
                  AND local_seq <= ?
                  AND state IN ('pending', 'retry_wait', 'inflight')
                ORDER BY local_seq
                """,
                (session_id, target_seq),
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
                    raise ValueError("single outbox payload exceeds max_bytes")
                selected.append(row)
                total_bytes += payload_size
                if len(selected) >= max_items:
                    break

            if not selected:
                return []
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
                )
                for row in selected
            ]

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
                        lease_owner=NULL, inflight_at=NULL, lease_until=NULL
                    WHERE batch_id=? AND upload_session_id=? AND local_seq=?
                      AND state='inflight' AND lease_owner=?
                    """,
                    (
                        completion.state,
                        now,
                        completion.error,
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
        with self._transaction() as connection:
            for record, delay_ms in records:
                cursor = connection.execute(
                    """
                    UPDATE outbox
                    SET state='retry_wait',
                        attempt_count=attempt_count + 1,
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

    def prepare_session_switch(self) -> SessionSwitchState:
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
            old_session_id = self._metadata(connection, "upload_session_id")
            connection.execute(
                "INSERT INTO metadata(key, value) VALUES('pending_session_switch_old_id', ?)",
                (old_session_id,),
            )
            connection.execute(
                "UPDATE metadata SET value=? WHERE key='upload_session_id'",
                (new_session_id,),
            )
            connection.execute("UPDATE metadata SET value='0' WHERE key='last_seq'")
            connection.execute(
                """
                DELETE FROM outbox
                WHERE state IN ('pending', 'retry_wait', 'inflight')
                """
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

    def maintenance(self, retention_days: int) -> dict[str, Any]:
        if retention_days < 1:
            raise ValueError("retention_days must be positive")
        now = self._clock_ms()
        cutoff = now - retention_days * 24 * 60 * 60 * 1000
        dropped_records = 0
        session_switched = False
        with self._transaction() as connection:
            terminal = connection.execute(
                """
                DELETE FROM outbox
                WHERE (state='sent' AND acked_at IS NOT NULL AND acked_at < ?)
                   OR (state='dead' AND dead_at IS NOT NULL AND dead_at < ?)
                """,
                (cutoff, cutoff),
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
                and int(oldest["value"]) < cutoff
            )

        checkpoint = "passive"
        try:
            with self._lock:
                mode = "TRUNCATE" if deleted_terminal else "PASSIVE"
                self._connection.execute(f"PRAGMA wal_checkpoint({mode})").fetchone()
                checkpoint = mode.lower()
        except sqlite3.OperationalError:
            checkpoint = "busy"

        disk = self._disk_status()
        if disk["watermark"] == "overflow" or backlog_expired:
            with self._transaction() as connection:
                pending_switch = connection.execute(
                    "SELECT 1 FROM metadata WHERE key='pending_session_switch_old_id'"
                ).fetchone()
                if not pending_switch:
                    count_row = connection.execute(
                        """
                        SELECT COUNT(*) AS value FROM outbox
                        WHERE state IN ('pending', 'retry_wait', 'inflight')
                        """
                    ).fetchone()
                    dropped_records = int(count_row["value"] or 0)
                    if dropped_records:
                        old_session_id = self._metadata(connection, "upload_session_id")
                        connection.execute(
                            "INSERT INTO metadata(key, value) VALUES('pending_session_switch_old_id', ?)",
                            (old_session_id,),
                        )
                        connection.execute(
                            "UPDATE metadata SET value=? WHERE key='upload_session_id'",
                            (str(uuid4()),),
                        )
                        connection.execute("UPDATE metadata SET value='0' WHERE key='last_seq'")
                        connection.execute(
                            """
                            DELETE FROM outbox
                            WHERE state IN ('pending', 'retry_wait', 'inflight')
                            """
                        )
                        connection.execute(
                            """
                            UPDATE metadata
                            SET value=CAST(CAST(value AS INTEGER) + ? AS TEXT)
                            WHERE key='dropped_records'
                            """,
                            (dropped_records,),
                        )
                        session_switched = True
            if dropped_records:
                try:
                    with self._lock:
                        self._connection.execute("PRAGMA wal_checkpoint(TRUNCATE)").fetchone()
                        checkpoint = "truncate"
                except sqlite3.OperationalError:
                    checkpoint = "busy"

        return {
            "deleted_terminal": deleted_terminal,
            "dropped_records": dropped_records,
            "session_switched": session_switched,
            "checkpoint": checkpoint,
            **self._disk_status(),
        }

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
    ) -> OutboxRecord:
        existing = connection.execute(
            """
            SELECT batch_id, upload_session_id, local_seq, assignment_epoch,
                   payload, attempt_count
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
        )

    def _transaction(self):
        return _Transaction(self._connection, self._lock)

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
        finally:
            self._lock.release()
        return False
