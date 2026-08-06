from __future__ import annotations

import hashlib
import json
import sqlite3
import time
from pathlib import Path
from typing import Any


SCHEMA = """
CREATE TABLE IF NOT EXISTS edge_config_meta (
    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
    config_revision INTEGER NOT NULL,
    checksum TEXT NOT NULL,
    read_timeout_ms INTEGER,
    applied_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS edge_device (
    device_code TEXT PRIMARY KEY,
    device_pk INTEGER,
    device_type TEXT NOT NULL,
    protocol TEXT NOT NULL,
    endpoint TEXT NOT NULL,
    ip TEXT NOT NULL,
    port INTEGER NOT NULL,
    rack INTEGER NOT NULL,
    slot INTEGER NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    opc_server TEXT,
    config_version INTEGER NOT NULL,
    assignment_epoch INTEGER NOT NULL,
    enabled INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS edge_variable (
    variable_id TEXT PRIMARY KEY,
    device_code TEXT NOT NULL,
    address TEXT NOT NULL,
    data_type TEXT NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    is_storage INTEGER NOT NULL,
    var_seq INTEGER NOT NULL,
    group_code TEXT NOT NULL,
    group_config_version INTEGER NOT NULL,
    FOREIGN KEY (device_code) REFERENCES edge_device(device_code) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_edge_variable_device ON edge_variable(device_code);
CREATE TABLE IF NOT EXISTS edge_device_status (
    device_code TEXT PRIMARY KEY,
    status INTEGER NOT NULL,
    message TEXT NOT NULL,
    event_type TEXT NOT NULL,
    updated_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS device_assignment (
    device_code TEXT PRIMARY KEY,
    assignment_epoch INTEGER NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    config_revision INTEGER NOT NULL DEFAULT 0,
    enabled INTEGER NOT NULL DEFAULT 1,
    updated_at INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS edge_assignment_schedule (
    device_code TEXT PRIMARY KEY,
    assignment_epoch INTEGER NOT NULL,
    base_enabled INTEGER NOT NULL,
    collect_from_ms INTEGER,
    collect_until_ms INTEGER
);
"""


def snapshot_checksum(devices: list[dict[str, Any]]) -> str:
    payload = json.dumps(
        devices,
        ensure_ascii=True,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


class EdgeConfigStore:
    def __init__(self, path: str | Path) -> None:
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)

    def revision(self) -> int:
        with self._connect() as connection:
            row = connection.execute(
                "SELECT config_revision FROM edge_config_meta WHERE singleton=1"
            ).fetchone()
        return int(row[0]) if row else 0

    def apply(self, snapshot: dict[str, Any]) -> int:
        revision = int(snapshot["configRevision"])
        devices = snapshot.get("devices")
        checksum = str(snapshot.get("checksum") or "")
        read_timeout_ms = snapshot.get("readTimeoutMs")
        if revision < 0 or not isinstance(devices, list):
            raise ValueError("invalid config snapshot")
        if read_timeout_ms is not None and int(read_timeout_ms) <= 0:
            raise ValueError("readTimeoutMs must be positive")
        if snapshot_checksum(devices) != checksum:
            raise ValueError("config snapshot checksum mismatch")

        now = int(time.time() * 1000)
        device_rows, variable_rows, assignment_rows, schedule_rows = _snapshot_rows(
            devices,
            revision,
            now,
        )
        with self._connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            try:
                connection.execute("DELETE FROM edge_assignment_schedule")
                connection.execute("DELETE FROM edge_variable")
                connection.execute("DELETE FROM edge_device")
                connection.executemany(
                    """
                    INSERT INTO edge_device VALUES(
                        ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
                    )
                    """,
                    device_rows,
                )
                connection.executemany(
                    "INSERT INTO edge_variable VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?)",
                    variable_rows,
                )
                connection.execute(
                    "UPDATE device_assignment SET enabled=0, updated_at=?",
                    (now,),
                )
                connection.executemany(
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
                    assignment_rows,
                )
                connection.executemany(
                    """
                    INSERT INTO edge_assignment_schedule(
                        device_code, assignment_epoch, base_enabled,
                        collect_from_ms, collect_until_ms
                    ) VALUES(?, ?, ?, ?, ?)
                    """,
                    schedule_rows,
                )
                connection.execute(
                    """
                    INSERT INTO edge_config_meta VALUES(1, ?, ?, ?, ?)
                    ON CONFLICT(singleton) DO UPDATE SET
                        config_revision=excluded.config_revision,
                        checksum=excluded.checksum,
                        read_timeout_ms=excluded.read_timeout_ms,
                        applied_at=excluded.applied_at
                    """,
                    (revision, checksum, read_timeout_ms, now),
                )
                connection.commit()
            except Exception:
                connection.rollback()
                raise
        return revision

    def refresh_schedule(self, now_ms: int | None = None) -> None:
        now = int(time.time() * 1000) if now_ms is None else int(now_ms)
        with self._connect() as connection:
            connection.execute("BEGIN IMMEDIATE")
            _refresh_schedule(connection, now)
            connection.commit()

    def _connect(self) -> sqlite3.Connection:
        connection = sqlite3.connect(self.path, timeout=5)
        connection.execute("PRAGMA foreign_keys=ON")
        connection.execute("PRAGMA journal_mode=WAL")
        connection.execute("PRAGMA synchronous=FULL")
        connection.execute("PRAGMA busy_timeout=5000")
        connection.executescript(SCHEMA)
        return connection


def _snapshot_rows(devices: list[dict[str, Any]], revision: int, now: int):
    device_rows = []
    variable_rows = []
    assignment_rows = []
    schedule_rows = []
    seen_devices: set[str] = set()
    seen_variables: set[str] = set()
    for device in devices:
        code = str(device["device_code"]).strip()
        scan_rate_ms = int(device["scan_rate_ms"])
        assignment_epoch = int(device["assignment_epoch"])
        if not code or code in seen_devices or scan_rate_ms <= 0 or assignment_epoch < 0:
            raise ValueError("invalid or duplicate device assignment")
        seen_devices.add(code)
        base_enabled = int(bool(device.get("enabled", True)))
        collect_from_ms = _optional_millis(device.get("collect_from_ms"))
        collect_until_ms = _optional_millis(device.get("collect_until_ms"))
        enabled = int(
            _schedule_enabled(base_enabled, collect_from_ms, collect_until_ms, now)
        )
        device_rows.append(
            (
                code,
                device.get("device_pk"),
                str(device.get("device_type") or "UNKNOWN"),
                str(device["protocol"]),
                str(device.get("endpoint") or ""),
                str(device.get("ip") or "127.0.0.1"),
                int(device.get("port") or 0),
                int(device.get("rack") or 0),
                int(device.get("slot") if device.get("slot") is not None else 1),
                scan_rate_ms,
                device.get("opc_server"),
                int(device.get("config_version") or 1),
                assignment_epoch,
                enabled,
            )
        )
        assignment_rows.append((code, assignment_epoch, scan_rate_ms, revision, enabled, now))
        schedule_rows.append(
            (code, assignment_epoch, base_enabled, collect_from_ms, collect_until_ms)
        )
        for variable in device.get("variables") or []:
            variable_id = str(variable["variable_id"])
            variable_rate = int(variable.get("scan_rate_ms") or scan_rate_ms)
            if not variable_id or variable_id in seen_variables or variable_rate <= 0:
                raise ValueError("invalid or duplicate variable")
            seen_variables.add(variable_id)
            variable_rows.append(
                (
                    variable_id,
                    code,
                    str(variable["address"]),
                    str(variable.get("data_type") or "INT").upper(),
                    variable_rate,
                    int(bool(variable.get("is_storage", True))),
                    int(variable.get("var_seq") or 0),
                    str(variable.get("group_code") or "g001"),
                    int(variable.get("group_config_version") or 1),
                )
            )
    return device_rows, variable_rows, assignment_rows, schedule_rows


def _optional_millis(value: Any) -> int | None:
    if value is None:
        return None
    parsed = int(value)
    if parsed < 0:
        raise ValueError("assignment schedule timestamp must be non-negative")
    return parsed


def _schedule_enabled(
    base_enabled: int,
    collect_from_ms: int | None,
    collect_until_ms: int | None,
    now_ms: int,
) -> bool:
    return bool(base_enabled) and (
        collect_from_ms is None or now_ms >= collect_from_ms
    ) and (collect_until_ms is None or now_ms < collect_until_ms)


def _refresh_schedule(connection: sqlite3.Connection, now_ms: int) -> None:
    connection.execute(
        """
        UPDATE edge_device
        SET enabled = COALESCE((
            SELECT CASE
                WHEN s.base_enabled=1
                 AND (s.collect_from_ms IS NULL OR s.collect_from_ms <= ?)
                 AND (s.collect_until_ms IS NULL OR ? < s.collect_until_ms)
                THEN 1 ELSE 0 END
            FROM edge_assignment_schedule AS s
            WHERE s.device_code=edge_device.device_code
        ), 0)
        WHERE device_code IN (SELECT device_code FROM edge_assignment_schedule)
        """,
        (now_ms, now_ms),
    )
    connection.execute(
        """
        UPDATE device_assignment
        SET enabled = COALESCE((
                SELECT d.enabled FROM edge_device AS d
                WHERE d.device_code=device_assignment.device_code
            ), 0),
            updated_at = ?
        WHERE device_code IN (SELECT device_code FROM edge_assignment_schedule)
        """,
        (now_ms,),
    )