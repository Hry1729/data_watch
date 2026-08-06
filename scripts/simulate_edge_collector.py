from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
import math
import os
from pathlib import Path
import sqlite3
import sys
import time
from typing import Iterable
import zlib


REPO_ROOT = Path(__file__).resolve().parents[1]
COLLECTOR_ROOT = REPO_ROOT / "collector"
for source_root in (REPO_ROOT, COLLECTOR_ROOT):
    if str(source_root) not in sys.path:
        sys.path.insert(0, str(source_root))

from edge.app.outbox import OutboxRecord, OutboxStore


QUALITY_GOOD = 0
DEFAULT_INSTALLED_DB = Path(r"C:\ProgramData\DataWatch Edge\edge\edge.db")


@dataclass(frozen=True, slots=True)
class SimulatedVariable:
    variable_id: str
    data_type: str
    var_seq: int


@dataclass(frozen=True, slots=True)
class SimulatedGroup:
    device_code: str
    device_type: str
    group_code: str
    config_version: int
    scan_rate_ms: int
    variables: tuple[SimulatedVariable, ...]


def resolve_database(explicit: str | None) -> Path:
    if explicit:
        path = Path(explicit).expanduser()
        if not path.is_file():
            raise FileNotFoundError(f"edge database not found: {path}")
        return path

    candidates: list[Path] = []
    configured = os.environ.get("DATAWATCH_EDGE_DB")
    if configured:
        candidates.append(Path(configured))
    data_root = os.environ.get("DATAWATCH_DATA_ROOT")
    if data_root:
        candidates.append(Path(data_root) / "edge" / "edge.db")
    candidates.extend(
        (
            DEFAULT_INSTALLED_DB,
            Path(r"C:\ProgramData\DataWatch\edge\edge.db"),
            REPO_ROOT / "edge" / "data" / "edge.db",
        )
    )
    for path in candidates:
        if path.is_file():
            return path
    raise FileNotFoundError(
        "edge database was not found; pass --db with the full edge.db path"
    )


def _readonly_connection(path: Path) -> sqlite3.Connection:
    connection = sqlite3.connect(
        f"file:{path.resolve().as_posix()}?mode=ro",
        uri=True,
        timeout=5,
    )
    connection.row_factory = sqlite3.Row
    connection.execute("PRAGMA busy_timeout=5000")
    return connection


def load_groups(
    path: Path,
    device_codes: Iterable[str],
    *,
    point_limit: int,
    period_ms: int | None = None,
) -> list[SimulatedGroup]:
    requested = [value.strip().lower() for value in device_codes if value.strip()]
    if not requested:
        raise ValueError("at least one device code is required")
    if point_limit < 0:
        raise ValueError("point_limit must be zero or positive")
    if period_ms is not None and period_ms <= 0:
        raise ValueError("period_ms must be positive")

    placeholders = ",".join("?" for _ in requested)
    with _readonly_connection(path) as connection:
        rows = connection.execute(
            f"""
            SELECT
                d.device_code,
                d.device_type,
                v.variable_id,
                v.data_type,
                v.var_seq,
                v.group_code,
                v.group_config_version,
                v.scan_rate_ms
            FROM edge_device AS d
            JOIN device_assignment AS a ON a.device_code=d.device_code
            JOIN edge_variable AS v ON v.device_code=d.device_code
            WHERE LOWER(d.device_code) IN ({placeholders})
              AND d.enabled=1
              AND a.enabled=1
              AND v.is_storage=1
              AND UPPER(v.data_type) <> 'STRING'
            ORDER BY d.device_code, v.scan_rate_ms, v.group_code, v.var_seq, v.variable_id
            """,
            requested,
        ).fetchall()

    found = {str(row["device_code"]).lower() for row in rows}
    unavailable = [code for code in requested if code not in found]
    if unavailable:
        raise ValueError(
            "devices are missing, disabled, or have no numeric storage points: "
            + ", ".join(unavailable)
        )

    selected_per_device: Counter[str] = Counter()
    grouped: dict[tuple[str, str, int, int], list[SimulatedVariable]] = {}
    device_types: dict[str, str] = {}
    for row in rows:
        device_code = str(row["device_code"])
        if point_limit and selected_per_device[device_code] >= point_limit:
            continue
        selected_per_device[device_code] += 1
        device_types[device_code] = str(row["device_type"] or "UNKNOWN").lower()
        scan_rate_ms = period_ms or int(row["scan_rate_ms"])
        key = (
            device_code,
            str(row["group_code"] or "g001"),
            int(row["group_config_version"] or 1),
            scan_rate_ms,
        )
        grouped.setdefault(key, []).append(
            SimulatedVariable(
                variable_id=str(row["variable_id"]),
                data_type=str(row["data_type"] or "INT").upper(),
                var_seq=int(row["var_seq"] or 0),
            )
        )

    return [
        SimulatedGroup(
            device_code=device_code,
            device_type=device_types[device_code],
            group_code=group_code,
            config_version=config_version,
            scan_rate_ms=scan_rate_ms,
            variables=tuple(variables),
        )
        for (device_code, group_code, config_version, scan_rate_ms), variables
        in grouped.items()
    ]


def generated_value(variable: SimulatedVariable, cycle: int, device_code: str) -> str:
    data_type = variable.data_type.upper()
    device_seed = zlib.crc32(device_code.encode("utf-8")) % 100
    if data_type in {"BOOL", "BOOLEAN"}:
        return "1" if (cycle + variable.var_seq) % 2 else "0"
    if data_type in {"BYTE", "UBYTE", "UINT8"}:
        return str((device_seed + variable.var_seq + cycle) % 200)
    if data_type in {"REAL", "FLOAT", "DOUBLE", "DECIMAL"}:
        value = 100 + device_seed + (variable.var_seq % 100) / 100
        return f"{value + math.sin(cycle / 10):.3f}"
    return str(10_000 + device_seed * 100 + variable.var_seq + cycle % 100)


def write_group(
    store: OutboxStore,
    group: SimulatedGroup,
    *,
    cycle: int,
    timestamp_ms: int | None = None,
) -> OutboxRecord:
    ts = timestamp_ms or int(time.time() * 1000)
    values = {
        variable.variable_id: generated_value(variable, cycle, group.device_code)
        for variable in group.variables
    }
    qualities = {variable_id: QUALITY_GOOD for variable_id in values}
    fields = {
        variable_id: {"v": value, "t": ts, "q": QUALITY_GOOD}
        for variable_id, value in values.items()
    }

    record = store.append_envelope(
        device_code=group.device_code,
        device_type=group.device_type,
        group_code=group.group_code,
        scan_rate_ms=group.scan_rate_ms,
        ts=ts,
        config_version=group.config_version,
        values=values,
        qualities=qualities,
    )
    store.write_local_latest(group.device_code, group.group_code, fields)
    return record


def record_state_counts(path: Path, batch_ids: list[str]) -> Counter[str]:
    counts: Counter[str] = Counter()
    if not batch_ids:
        return counts
    with _readonly_connection(path) as connection:
        for offset in range(0, len(batch_ids), 500):
            chunk = batch_ids[offset:offset + 500]
            placeholders = ",".join("?" for _ in chunk)
            rows = connection.execute(
                f"SELECT state, COUNT(*) AS count FROM outbox "
                f"WHERE batch_id IN ({placeholders}) GROUP BY state",
                chunk,
            ).fetchall()
            for row in rows:
                counts[str(row["state"])] += int(row["count"])
    return counts


def _print_status(path: Path, batch_ids: list[str], points: int) -> Counter[str]:
    counts = record_state_counts(path, batch_ids)
    details = " ".join(
        f"{state}={counts.get(state, 0)}"
        for state in ("pending", "inflight", "retry_wait", "sent", "dead")
    )
    print(f"generated={len(batch_ids)} envelopes points={points} {details}", flush=True)
    return counts


def run_simulation(args: argparse.Namespace) -> int:
    path = resolve_database(args.db)
    device_codes = [value for part in args.devices for value in part.split(",")]
    groups = load_groups(
        path,
        device_codes,
        point_limit=args.point_limit,
        period_ms=args.period_ms,
    )

    print(f"edge database: {path}")
    for group in groups:
        point_ids = ",".join(variable.variable_id for variable in group.variables)
        print(
            f"{group.device_code}/{group.group_code}: period={group.scan_rate_ms}ms "
            f"points={len(group.variables)} ids=[{point_ids}]"
        )
    if args.dry_run:
        print("dry-run complete; no outbox records were written")
        return 0

    store = OutboxStore(path)
    upload_interval_ms = store.upload_interval_ms()
    fastest_group_ms = min(group.scan_rate_ms for group in groups)
    if upload_interval_ms and upload_interval_ms > fastest_group_ms:
        print(
            "WARNING: uploader interval is "
            f"{upload_interval_ms}ms while simulated points run at {fastest_group_ms}ms; "
            "records will be uploaded in larger batches."
        )
    print("Keep Uploader, center Ingest and Writer running. Press Ctrl+C to stop.")

    started = time.monotonic()
    ends_at = started + args.duration_seconds if args.duration_seconds > 0 else None
    next_due = [started for _ in groups]
    cycles = [0 for _ in groups]
    batch_ids: list[str] = []
    generated_points = 0
    next_status_at = started + args.status_interval_seconds

    try:
        while ends_at is None or time.monotonic() < ends_at:
            now = time.monotonic()
            due_indexes = [index for index, due_at in enumerate(next_due) if due_at <= now]
            if not due_indexes:
                wake_at = min(min(next_due), next_status_at)
                if ends_at is not None:
                    wake_at = min(wake_at, ends_at)
                time.sleep(max(0.001, wake_at - now))
                continue

            for index in due_indexes:
                group = groups[index]
                cycles[index] += 1
                record = write_group(store, group, cycle=cycles[index])
                batch_ids.append(record.batch_id)
                generated_points += len(group.variables)
                period_seconds = group.scan_rate_ms / 1000
                next_due[index] = max(next_due[index] + period_seconds, time.monotonic())

            if time.monotonic() >= next_status_at:
                _print_status(path, batch_ids, generated_points)
                next_status_at = time.monotonic() + args.status_interval_seconds
    except KeyboardInterrupt:
        print("simulation stopped by user")

    drain_deadline = time.monotonic() + args.drain_wait_seconds
    while batch_ids and time.monotonic() < drain_deadline:
        counts = record_state_counts(path, batch_ids)
        if counts.get("sent", 0) + counts.get("dead", 0) >= len(batch_ids):
            break
        time.sleep(0.5)

    counts = _print_status(path, batch_ids, generated_points)
    store.close()
    sent = counts.get("sent", 0)
    if sent:
        print(
            f"Uploader/Ingest ACK observed for {sent}/{len(batch_ids)} simulated envelopes. "
            "Use center realtime/history pages to confirm Writer and TDengine persistence."
        )
    else:
        print(
            "No ACK was observed. Check that Uploader and Ingest are running, then inspect "
            "retry_wait/dead records and the Uploader log."
        )
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Simulate the edge Collector by writing real outbox envelopes."
    )
    parser.add_argument("--db", help="Full path to edge.db; installed Edge path is auto-detected")
    parser.add_argument(
        "--devices",
        action="append",
        default=None,
        help="Comma-separated device codes; repeatable (default: qc100,qc101)",
    )
    parser.add_argument(
        "--point-limit",
        type=int,
        default=10,
        help="Maximum numeric storage points per device; 0 selects all (default: 10)",
    )
    parser.add_argument(
        "--period-ms",
        type=int,
        help="Override all point scan periods; default uses edge_variable.scan_rate_ms",
    )
    parser.add_argument("--duration-seconds", type=float, default=30.0)
    parser.add_argument("--drain-wait-seconds", type=float, default=5.0)
    parser.add_argument("--status-interval-seconds", type=float, default=1.0)
    parser.add_argument("--dry-run", action="store_true")
    return parser


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()
    if args.devices is None:
        args.devices = ["qc100,qc101"]
    if args.duration_seconds < 0:
        parser.error("duration-seconds must be zero or positive")
    if args.drain_wait_seconds < 0:
        parser.error("drain-wait-seconds must be zero or positive")
    if args.status_interval_seconds <= 0:
        parser.error("status-interval-seconds must be positive")
    try:
        return run_simulation(args)
    except sqlite3.OperationalError as exc:
        if "readonly database" in str(exc).lower():
            print(
                "ERROR: the installed edge.db and its WAL files are read-only for this "
                "Windows session. Open PowerShell as Administrator and rerun the same command.",
                file=sys.stderr,
            )
            return 3
        print(f"ERROR: SQLite operation failed: {exc}", file=sys.stderr)
        return 2
    except (FileNotFoundError, sqlite3.Error, ValueError) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
