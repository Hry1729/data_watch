from __future__ import annotations

import asyncio
import hashlib
import json
import sqlite3
import threading
import time
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Any, NamedTuple

from core.config import CollectorConfig, DeviceConfig, VariableConfig


from edge.app.snapshot_schema import (
    CONFIG_SNAPSHOT_SCHEMA,
    EDGE_SCHEDULE_SCHEMA,
    ensure_edge_variable_columns,
)
from edge.app.storage_client import EdgeStorageClient


class EdgeDeviceSignature(NamedTuple):
    config_version: int
    endpoint: str
    protocol_id: int
    rack: int
    slot: int
    scan_cycle: int
    read_timeout_ms: int | None

    def device_fields_changed(self, other: "EdgeDeviceSignature") -> bool:
        return self[1:] != other[1:]


@dataclass(frozen=True, slots=True)
class SnapshotInfo:
    config_revision: int
    checksum: str
    read_timeout_ms: int | None
    applied_at: int


def snapshot_checksum(devices: list[dict[str, Any]]) -> str:
    payload = json.dumps(
        devices,
        ensure_ascii=True,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def _normalize_protocol(value: Any) -> str:
    raw = str(value or "s7").strip().lower()
    return {
        "gesrtp": "ge_srtp",
        "ge-srtp": "ge_srtp",
        "ge srtp": "ge_srtp",
    }.get(raw, raw)


class ConfigSnapshotStore:
    """Atomic edge topology snapshots stored beside the outbox."""

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
        self._connection.execute("PRAGMA foreign_keys=ON")
        self._connection.execute("PRAGMA journal_mode=WAL")
        self._connection.execute("PRAGMA synchronous=NORMAL")
        self._connection.execute("PRAGMA busy_timeout=5000")
        self._connection.executescript(CONFIG_SNAPSHOT_SCHEMA)
        ensure_edge_variable_columns(self._connection)

    def close(self) -> None:
        with self._lock:
            self._connection.close()

    def apply_snapshot(
        self,
        *,
        config_revision: int,
        checksum: str,
        devices: list[dict[str, Any]],
        read_timeout_ms: int | None = None,
    ) -> SnapshotInfo:
        if config_revision < 0:
            raise ValueError("config_revision must be non-negative")
        if read_timeout_ms is not None and read_timeout_ms <= 0:
            raise ValueError("read_timeout_ms must be positive when configured")
        calculated = snapshot_checksum(devices)
        if calculated != checksum:
            raise ValueError("config snapshot checksum mismatch")

        device_rows: list[tuple[Any, ...]] = []
        variable_rows: list[tuple[Any, ...]] = []
        assignment_rows: list[tuple[Any, ...]] = []
        schedule_rows: list[tuple[Any, ...]] = []
        seen_devices: set[str] = set()
        seen_variables: set[str] = set()
        now = self._clock_ms()

        for raw_device in devices:
            device_code = str(raw_device["device_code"]).strip()
            if not device_code or device_code in seen_devices:
                raise ValueError(f"invalid or duplicate device_code: {device_code!r}")
            seen_devices.add(device_code)
            scan_rate_ms = int(raw_device["scan_rate_ms"])
            assignment_epoch = int(raw_device["assignment_epoch"])
            if scan_rate_ms <= 0 or assignment_epoch < 0:
                raise ValueError(f"invalid assignment for device {device_code}")
            base_enabled = int(bool(raw_device.get("enabled", True)))
            collect_from_ms = _optional_millis(raw_device.get("collect_from_ms"))
            collect_until_ms = _optional_millis(raw_device.get("collect_until_ms"))
            enabled = _schedule_enabled(
                base_enabled, collect_from_ms, collect_until_ms, now
            )
            endpoint = str(raw_device.get("endpoint") or "")
            device_rows.append(
                (
                    device_code,
                    raw_device.get("device_pk"),
                    str(raw_device.get("device_type") or "UNKNOWN"),
                    _normalize_protocol(raw_device["protocol"]),
                    endpoint,
                    str(raw_device.get("ip") or _endpoint_host(endpoint)),
                    int(raw_device.get("port") or 0),
                    int(raw_device.get("rack") or 0),
                    int(raw_device.get("slot") if raw_device.get("slot") is not None else 1),
                    scan_rate_ms,
                    raw_device.get("opc_server"),
                    int(raw_device.get("config_version") or 1),
                    assignment_epoch,
                    int(enabled),
                )
            )
            assignment_rows.append(
                (
                    device_code,
                    assignment_epoch,
                    scan_rate_ms,
                    config_revision,
                    int(enabled),
                    now,
                )
            )
            schedule_rows.append(
                (
                    device_code,
                    assignment_epoch,
                    base_enabled,
                    collect_from_ms,
                    collect_until_ms,
                )
            )
            for raw_variable in raw_device.get("variables") or []:
                variable_id = str(raw_variable["variable_id"])
                if variable_id in seen_variables:
                    raise ValueError(f"duplicate variable_id: {variable_id}")
                seen_variables.add(variable_id)
                variable_scan_rate_ms = int(
                    raw_variable.get("scan_rate_ms") or scan_rate_ms
                )
                if variable_scan_rate_ms <= 0:
                    raise ValueError(f"invalid scan rate for variable {variable_id}")
                variable_rows.append(
                    (
                        variable_id,
                        device_code,
                        str(raw_variable["address"]),
                        str(raw_variable.get("point_name") or "").strip(),
                        str(raw_variable.get("data_type") or "INT").upper(),
                        variable_scan_rate_ms,
                        int(bool(raw_variable.get("is_storage", True))),
                        int(raw_variable.get("var_seq") or 0),
                        str(raw_variable.get("group_code") or "g001"),
                        int(raw_variable.get("group_config_version") or 1),
                    )
                )

        with self._transaction() as connection:
            connection.execute("DELETE FROM edge_assignment_schedule")
            connection.execute("DELETE FROM edge_variable")
            connection.execute("DELETE FROM edge_device")
            connection.executemany(
                """
                INSERT INTO edge_device(
                    device_code, device_pk, device_type, protocol, endpoint, ip,
                    port, rack, slot, scan_rate_ms, opc_server, config_version,
                    assignment_epoch, enabled
                ) VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                device_rows,
            )
            connection.executemany(
                """
                INSERT INTO edge_variable(
                    variable_id, device_code, address, point_name, data_type,
                    scan_rate_ms, is_storage, var_seq, group_code,
                    group_config_version
                ) VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                variable_rows,
            )
            connection.execute("UPDATE device_assignment SET enabled=0, updated_at=?", (now,))
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
                INSERT INTO edge_config_meta(
                    singleton, config_revision, checksum, read_timeout_ms, applied_at
                ) VALUES(1, ?, ?, ?, ?)
                ON CONFLICT(singleton) DO UPDATE SET
                    config_revision=excluded.config_revision,
                    checksum=excluded.checksum,
                    read_timeout_ms=excluded.read_timeout_ms,
                    applied_at=excluded.applied_at
                """,
                (config_revision, checksum, read_timeout_ms, now),
            )
            for row in connection.execute("SELECT device_code, group_code, values_json FROM local_latest_frame").fetchall():
                valid = {str(v[0]) for v in connection.execute(
                    "SELECT variable_id FROM edge_variable WHERE device_code=? AND group_code=?", (row["device_code"], row["group_code"]))}
                values = json.loads(row["values_json"])
                kept = {k: v for k, v in values.items() if k in valid}
                if kept:
                    connection.execute("UPDATE local_latest_frame SET values_json=? WHERE device_code=? AND group_code=?",
                        (json.dumps(kept), row["device_code"], row["group_code"]))
                else:
                    connection.execute("DELETE FROM local_latest_frame WHERE device_code=? AND group_code=?", (row["device_code"], row["group_code"]))
            connection.execute("DELETE FROM local_latest WHERE NOT EXISTS (SELECT 1 FROM edge_variable v WHERE v.device_code=local_latest.device_code AND v.group_code=local_latest.group_code AND v.variable_id=local_latest.var_id)")
        return SnapshotInfo(config_revision, checksum, read_timeout_ms, now)

    def snapshot_info(self) -> SnapshotInfo | None:
        with self._lock:
            row = self._connection.execute(
                """
                SELECT config_revision, checksum, read_timeout_ms, applied_at
                FROM edge_config_meta WHERE singleton=1
                """
            ).fetchone()
        if row is None:
            return None
        return SnapshotInfo(
            int(row["config_revision"]),
            str(row["checksum"]),
            int(row["read_timeout_ms"]) if row["read_timeout_ms"] is not None else None,
            int(row["applied_at"]),
        )

    def revision(self) -> int:
        snapshot = self.snapshot_info()
        return snapshot.config_revision if snapshot is not None else 0

    def apply(self, snapshot: dict[str, Any]) -> int:
        info = self.apply_snapshot(
            config_revision=int(snapshot["configRevision"]),
            checksum=str(snapshot.get("checksum") or ""),
            devices=list(snapshot.get("devices") or []),
            read_timeout_ms=(
                int(snapshot["readTimeoutMs"])
                if snapshot.get("readTimeoutMs") is not None
                else None
            ),
        )
        return info.config_revision

    def refresh_schedule(self, now_ms: int | None = None) -> None:
        now = self._clock_ms() if now_ms is None else int(now_ms)
        with self._transaction() as connection:
            _refresh_schedule(connection, now)

    def update_device_status(
        self,
        device_code: str,
        status: int,
        message: str = "",
        event_type: str = "status_update",
    ) -> None:
        with self._transaction() as connection:
            connection.execute(
                """
                INSERT INTO edge_device_status(
                    device_code, status, message, event_type, updated_at
                ) VALUES(?, ?, ?, ?, ?)
                ON CONFLICT(device_code) DO UPDATE SET
                    status=excluded.status,
                    message=excluded.message,
                    event_type=excluded.event_type,
                    updated_at=excluded.updated_at
                """,
                (device_code, status, message, event_type, self._clock_ms()),
            )

    def list_devices(self) -> list[dict[str, Any]]:
        with self._lock:
            _refresh_schedule(self._connection, self._clock_ms())
            rows = self._connection.execute(
                """
                SELECT d.device_code, d.device_type, d.protocol, d.endpoint,
                       d.scan_rate_ms, d.config_version, d.assignment_epoch,
                       d.enabled, s.status, s.message, s.updated_at AS status_updated_at
                FROM edge_device AS d
                LEFT JOIN edge_device_status AS s ON s.device_code=d.device_code
                ORDER BY d.device_code
                """
            ).fetchall()
        return [dict(row) for row in rows]

    def _transaction(self):
        return _Transaction(self._connection, self._lock)


class EdgeConfigManager:
    """Collector config manager backed only by the local edge snapshot."""

    def __init__(
        self,
        path: str | Path,
        *,
        clock_ms=None,
        storage: EdgeStorageClient | None = None,
    ) -> None:
        self.path = Path(path)
        self._clock_ms = clock_ms or (lambda: int(time.time() * 1000))
        self._storage = storage

    async def close(self) -> None:
        if self._storage is not None:
            await asyncio.to_thread(self._storage.close)

    async def load_config(self, base_config: CollectorConfig | None = None) -> CollectorConfig:
        config = base_config or CollectorConfig()
        config.devices, config.variables = await self.load_devices_and_variables()
        return config

    async def load_devices_and_variables(self) -> tuple[list[DeviceConfig], list[VariableConfig]]:
        await self._refresh_schedule()
        with self._connect() as connection:
            meta = connection.execute(
                "SELECT read_timeout_ms FROM edge_config_meta WHERE singleton=1"
            ).fetchone()
            read_timeout_ms = (
                int(meta["read_timeout_ms"])
                if meta is not None and meta["read_timeout_ms"] is not None
                else None
            )
            device_rows = connection.execute(
                "SELECT * FROM edge_device WHERE enabled=1 ORDER BY device_code"
            ).fetchall()
            variable_rows = connection.execute(
                """
                SELECT v.* FROM edge_variable AS v
                JOIN edge_device AS d ON d.device_code=v.device_code
                WHERE d.enabled=1 ORDER BY v.device_code, v.variable_id
                """
            ).fetchall()
        devices = [_device_config(row, read_timeout_ms) for row in device_rows]
        variables = [_variable_config(row) for row in variable_rows]
        return devices, variables

    async def get_device_config_versions(self) -> dict[str, EdgeDeviceSignature]:
        await self._refresh_schedule()
        with self._connect() as connection:
            meta = connection.execute(
                "SELECT read_timeout_ms FROM edge_config_meta WHERE singleton=1"
            ).fetchone()
            read_timeout_ms = (
                int(meta["read_timeout_ms"])
                if meta is not None and meta["read_timeout_ms"] is not None
                else None
            )
            rows = connection.execute(
                "SELECT * FROM edge_device WHERE enabled=1 ORDER BY device_code"
            ).fetchall()
        return {
            str(row["device_code"]): EdgeDeviceSignature(
                int(row["config_version"]),
                str(row["endpoint"]),
                zlib.crc32(str(row["protocol"]).encode("utf-8")),
                int(row["rack"]),
                int(row["slot"]),
                int(row["scan_rate_ms"]),
                read_timeout_ms,
            )
            for row in rows
        }

    async def update_device_status(
        self,
        device_code: str,
        status: int,
        message: str = "",
        event_type: str = "status_update",
    ) -> None:
        if self._storage is not None:
            await asyncio.to_thread(
                self._storage.update_device_status,
                device_code,
                status,
                message,
                event_type,
            )
            return
        with self._connect() as connection:
            connection.execute(
                """
                INSERT INTO edge_device_status(
                    device_code, status, message, event_type, updated_at
                ) VALUES(?, ?, ?, ?, ?)
                ON CONFLICT(device_code) DO UPDATE SET
                    status=excluded.status,
                    message=excluded.message,
                    event_type=excluded.event_type,
                    updated_at=excluded.updated_at
                """,
                (device_code, status, message, event_type, self._clock_ms()),
            )

    async def write_system_log(self, **_payload: Any) -> None:
        return None

    def _connect(self) -> sqlite3.Connection:
        if self._storage is not None:
            connection = sqlite3.connect(
                f"file:{self.path.resolve().as_posix()}?mode=ro",
                uri=True,
                timeout=5,
            )
        else:
            connection = sqlite3.connect(self.path, timeout=5)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA busy_timeout=5000")
        if self._storage is not None:
            connection.execute("PRAGMA query_only=ON")
        else:
            connection.executescript(EDGE_SCHEDULE_SCHEMA)
        return connection

    async def _refresh_schedule(self) -> None:
        if self._storage is not None:
            await asyncio.to_thread(self._storage.refresh_schedule, self._clock_ms())
            return
        with self._connect() as connection:
            _refresh_schedule(connection, self._clock_ms())


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

    def __exit__(self, exc_type, exc, traceback) -> bool:
        try:
            self._connection.execute("ROLLBACK" if exc_type else "COMMIT")
        finally:
            self._lock.release()
        return False


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
    assignment_table = connection.execute(
        "SELECT 1 FROM sqlite_master WHERE type='table' AND name='device_assignment'"
    ).fetchone()
    if assignment_table is not None:
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

def _endpoint_host(endpoint: str) -> str:
    text = endpoint.strip()
    if "://" in text:
        text = text.split("://", 1)[1]
    return text.split(":", 1)[0] or "127.0.0.1"


def _device_config(row: sqlite3.Row, read_timeout_ms: int | None = None) -> DeviceConfig:
    return DeviceConfig(
        device_id=str(row["device_code"]),
        device_type=str(row["device_type"]),
        protocol=_normalize_protocol(row["protocol"]),
        ip=str(row["ip"]),
        endpoint=str(row["endpoint"]),
        port=int(row["port"]),
        rack=int(row["rack"]),
        slot=int(row["slot"]),
        scan_rate=int(row["scan_rate_ms"]) / 1000.0,
        device_pk=int(row["device_pk"]) if row["device_pk"] is not None else None,
        opc_server=row["opc_server"],
        config_version=int(row["config_version"]),
        read_timeout_ms=read_timeout_ms,
    )


def _variable_config(row: sqlite3.Row) -> VariableConfig:
    return VariableConfig(
        variable_id=str(row["variable_id"]),
        device_id=str(row["device_code"]),
        address=str(row["address"]),
        data_type=str(row["data_type"]),
        scan_rate=int(row["scan_rate_ms"]) / 1000.0,
        is_storage=bool(row["is_storage"]),
        var_seq=int(row["var_seq"]),
        group_code=str(row["group_code"]),
        group_config_version=int(row["group_config_version"]),
    )
