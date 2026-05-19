#!/usr/bin/env python3
"""Write benchmark rows to wide TDengine device child tables."""

from __future__ import annotations

import argparse
import asyncio
import os
import queue
import re
import statistics
import sys
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

SCRIPT_DIR = Path(__file__).resolve().parent
COLLECTOR_ROOT = SCRIPT_DIR.parent
VENDOR_DIR = COLLECTOR_ROOT / "vendor"
if VENDOR_DIR.exists():
    sys.path.insert(0, str(VENDOR_DIR))

import taosws
from sqlalchemy import select


DEFAULT_CONFIG = COLLECTOR_ROOT / "config" / "settings.toml"
DEFAULT_DEVICE_TYPES = ("qc", "asc", "agv")
sys.path.insert(0, str(COLLECTOR_ROOT))

from core.config_loader import load_collector_config  # noqa: E402
from core.config_db import ConfigDBManager, DatabaseConfig, DBDevice, DBDeviceType  # noqa: E402


IDENTIFIER_PATTERN = re.compile(r"[^A-Za-z0-9_]")


def quote_identifier(name: str) -> str:
    return f"`{name.replace('`', '``')}`"


def normalize_identifier(raw_name: str, prefix: str) -> str:
    cleaned = IDENTIFIER_PATTERN.sub("_", str(raw_name).strip())
    if not cleaned:
        cleaned = f"{prefix}_unknown"
    if cleaned[0].isdigit():
        cleaned = f"{prefix}_{cleaned}"
    return cleaned.lower()


def signal_column_name(stable: str, signal_index: int) -> str:
    return f"{stable}{signal_index}"


def connect(config_path: Path):
    config = load_collector_config(str(config_path))
    td = config.tdengine
    return taosws.connect(
        user=td.user,
        password=td.password,
        host=td.host,
        port=td.port,
    )


def query_rows(conn, sql: str) -> list[tuple]:
    return [tuple(row) for row in conn.query(sql)]


async def load_mysql_devices_async(
    config_path: Path,
    device_types: Sequence[str],
) -> dict[str, list[str]]:
    config = load_collector_config(str(config_path))
    manager = ConfigDBManager(DatabaseConfig.from_mysql_config(config.mysql))
    try:
        async with manager.get_session() as session:
            rows = (
                await session.execute(
                    select(DBDevice.device_code, DBDeviceType.type_code)
                    .outerjoin(DBDeviceType, DBDevice.type_id == DBDeviceType.id)
                    .order_by(DBDeviceType.type_code, DBDevice.device_code)
                )
            ).all()
    finally:
        await manager.close()

    wanted = {normalize_identifier(item, prefix="type") for item in device_types}
    devices_by_type: dict[str, list[str]] = {item: [] for item in wanted}
    for row in rows:
        device_code = normalize_identifier(str(row.device_code or ""), prefix="dev")
        device_type = normalize_identifier(str(row.type_code or ""), prefix="type")
        if not device_code or device_type not in wanted:
            continue
        devices_by_type.setdefault(device_type, []).append(device_code)

    for device_type, devices in devices_by_type.items():
        devices_by_type[device_type] = list(dict.fromkeys(devices))
    return devices_by_type


def load_mysql_devices(
    config_path: Path,
    device_types: Sequence[str],
) -> dict[str, list[str]]:
    return asyncio.run(load_mysql_devices_async(config_path, device_types))


def stable_has_current_schema(
    conn,
    database: str,
    stable: str,
    points_per_device: int,
) -> tuple[bool, str]:
    rows = query_rows(
        conn,
        f"DESCRIBE {quote_identifier(database)}.{quote_identifier(stable)}",
    )
    columns: set[str] = set()
    tags: set[str] = set()
    for row in rows:
        name = str(row[0]).lower()
        note = str(row[3]).upper() if len(row) > 3 else ""
        if note == "TAG":
            tags.add(name)
        else:
            columns.add(name)

    required_columns = {
        "ts",
        signal_column_name(stable, 1),
        signal_column_name(stable, points_per_device),
    }
    missing_columns = required_columns - columns
    missing_tags = {"device_code"} - tags
    if missing_columns or missing_tags:
        parts: list[str] = []
        if missing_columns:
            parts.append(f"missing columns={sorted(missing_columns)}")
        if missing_tags:
            parts.append(f"missing tags={sorted(missing_tags)}")
        return False, ", ".join(parts)
    return True, ""


@dataclass(frozen=True)
class Target:
    database: str
    stable: str
    table_name: str


def iter_chunks(items: Sequence[Target], size: int) -> Iterable[Sequence[Target]]:
    for index in range(0, len(items), size):
        yield items[index : index + size]


def split_evenly(items: Sequence[Target], chunks: int) -> list[list[Target]]:
    chunks = max(1, chunks)
    result: list[list[Target]] = []
    for index in range(chunks):
        start = index * len(items) // chunks
        end = (index + 1) * len(items) // chunks
        part = list(items[start:end])
        if part:
            result.append(part)
    return result


def discover_targets(args: argparse.Namespace) -> dict[str, list[Target]]:
    device_types = [
        normalize_identifier(item, prefix="type")
        for item in args.device_types
        if str(item).strip()
    ]
    if not device_types:
        raise SystemExit("--device-types must include at least one type")

    devices_by_type = load_mysql_devices(args.config, device_types)
    pattern = re.compile(args.device_filter) if args.device_filter else None
    targets: dict[str, list[Target]] = {}
    for stable in device_types:
        devices = devices_by_type.get(stable, [])
        if pattern:
            devices = [name for name in devices if pattern.search(name)]
        if args.limit_devices > 0:
            devices = devices[: args.limit_devices]
        targets[stable] = [
            Target(database=args.database, stable=stable, table_name=device)
            for device in devices
        ]

    if args.dry_run:
        return targets

    conn = connect(args.config)
    try:
        conn.execute(f"USE {quote_identifier(args.database)}")
        for stable in device_types:
            ok, reason = stable_has_current_schema(
                conn,
                args.database,
                stable,
                args.points_per_device,
            )
            if not ok:
                print(f"[WARN] skip stable {args.database}.{stable}: {reason}")
                targets[stable] = []
                continue
    finally:
        conn.close()

    return targets


@dataclass
class WorkerMetric:
    stable: str
    worker_id: int
    cycles: int
    attempted_rows: int
    affected_rows: int
    signal_values: int
    elapsed_ms: float
    lag_ms: float
    errors: int = 0


def build_insert_sql(stable: str, points_per_device: int) -> str:
    columns = [quote_identifier("tbname"), quote_identifier("ts")]
    columns.extend(
        quote_identifier(signal_column_name(stable, index))
        for index in range(1, points_per_device + 1)
    )
    placeholders = ", ".join("?" for _ in columns)
    return (
        f"INSERT INTO {quote_identifier(stable)} "
        f"({', '.join(columns)}) VALUES ({placeholders})"
    )


def make_columns(
    cycle: int,
    points_per_device: int,
    rows_per_device: int,
    interval_seconds: float,
) -> list:
    step_ms = max(1, int(interval_seconds * 1000 / rows_per_device))
    base_ts_ms = int(time.time() * 1000) - step_ms * (rows_per_device - 1)
    timestamps = [base_ts_ms + step_ms * index for index in range(rows_per_device)]
    signal_values = [
        float(cycle) + (index / max(1, rows_per_device))
        for index in range(rows_per_device)
    ]
    columns = [taosws.millis_timestamps_to_column(timestamps)]
    columns.extend(
        taosws.doubles_to_column(signal_values)
        for _ in range(points_per_device)
    )
    return columns


def write_stmt2_batch(
    stmt,
    targets: Sequence[Target],
    cycle: int,
    points_per_device: int,
    rows_per_device: int,
    interval_seconds: float,
) -> int:
    params = []
    for target in targets:
        params.append(
            taosws.stmt2_bind_param_view(
                table_name=target.table_name,
                tags=[],
                columns=make_columns(
                    cycle,
                    points_per_device,
                    rows_per_device,
                    interval_seconds,
                ),
            )
        )
    stmt.bind(params)
    return int(stmt.execute())


def run_worker(
    config_path: Path,
    database: str,
    stable: str,
    worker_id: int,
    targets: Sequence[Target],
    points_per_device: int,
    rows_per_device: int,
    bind_batch_size: int,
    interval_seconds: float,
    stop_event: threading.Event,
    metrics: "queue.Queue[WorkerMetric]",
) -> None:
    conn = None
    stmt = None
    cycle = 0
    try:
        conn = connect(config_path)
        conn.execute(f"USE {quote_identifier(database)}")
        stmt = conn.stmt2_statement()
        stmt.prepare(build_insert_sql(stable, points_per_device))

        next_start = time.perf_counter()
        while not stop_event.is_set():
            cycle += 1
            started = time.perf_counter()
            attempted_rows = 0
            affected_rows = 0
            errors = 0
            try:
                for chunk in iter_chunks(targets, bind_batch_size):
                    attempted_rows += len(chunk) * rows_per_device
                    affected_rows += write_stmt2_batch(
                        stmt,
                        chunk,
                        cycle,
                        points_per_device,
                        rows_per_device,
                        interval_seconds,
                    )
            except Exception as exc:
                errors += 1
                print(
                    f"[ERROR] worker failed stable={stable} worker={worker_id} "
                    f"cycle={cycle} error={exc}"
                )

            elapsed_ms = (time.perf_counter() - started) * 1000
            next_start += interval_seconds
            lag_ms = max(0.0, (time.perf_counter() - next_start) * 1000)
            metrics.put(
                WorkerMetric(
                    stable=stable,
                    worker_id=worker_id,
                    cycles=1,
                    attempted_rows=attempted_rows,
                    affected_rows=affected_rows,
                    signal_values=affected_rows * points_per_device,
                    elapsed_ms=elapsed_ms,
                    lag_ms=lag_ms,
                    errors=errors,
                )
            )

            sleep_seconds = next_start - time.perf_counter()
            if sleep_seconds > 0:
                stop_event.wait(sleep_seconds)
            else:
                next_start = time.perf_counter()

    except Exception as exc:
        print(f"[ERROR] worker crashed stable={stable} worker={worker_id} error={exc}")
        metrics.put(
            WorkerMetric(
                stable=stable,
                worker_id=worker_id,
                cycles=0,
                attempted_rows=0,
                affected_rows=0,
                signal_values=0,
                elapsed_ms=0.0,
                lag_ms=0.0,
                errors=1,
            )
        )
        stop_event.set()

    finally:
        if stmt is not None:
            stmt.close()
        if conn is not None:
            conn.close()


def print_summary(
    metrics: "queue.Queue[WorkerMetric]",
    started_at: float,
    duration_seconds: float,
    stop_event: threading.Event,
    threads: Sequence[threading.Thread],
) -> None:
    last_print = time.perf_counter()
    attempted_rows = 0
    affected_rows = 0
    signal_values = 0
    cycles = 0
    errors = 0
    elapsed_samples: list[float] = []
    lag_samples: list[float] = []

    while any(thread.is_alive() for thread in threads):
        now = time.perf_counter()
        if duration_seconds > 0 and now - started_at >= duration_seconds:
            stop_event.set()

        try:
            item = metrics.get(timeout=0.2)
            attempted_rows += item.attempted_rows
            affected_rows += item.affected_rows
            signal_values += item.signal_values
            cycles += item.cycles
            errors += item.errors
            elapsed_samples.append(item.elapsed_ms)
            lag_samples.append(item.lag_ms)
        except queue.Empty:
            pass

        now = time.perf_counter()
        if now - last_print >= 1.0:
            interval_seconds = now - last_print
            average_elapsed = statistics.fmean(elapsed_samples) if elapsed_samples else 0.0
            max_elapsed = max(elapsed_samples) if elapsed_samples else 0.0
            max_lag = max(lag_samples) if lag_samples else 0.0
            print(
                f"t={now - started_at:.1f}s "
                f"cycles={cycles} device_rows={attempted_rows} "
                f"affected_rows={affected_rows} "
                f"device_rows_per_sec={affected_rows / max(interval_seconds, 0.001):.0f} "
                f"signal_values_per_sec={signal_values / max(interval_seconds, 0.001):.0f} "
                f"errors={errors} avg_worker_ms={average_elapsed:.2f} "
                f"max_worker_ms={max_elapsed:.2f} max_lag_ms={max_lag:.2f}"
            )
            last_print = now
            attempted_rows = 0
            affected_rows = 0
            signal_values = 0
            cycles = 0
            errors = 0
            elapsed_samples.clear()
            lag_samples.clear()

    stop_event.set()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Write one wide row into each device child table at a fixed interval."
        )
    )
    parser.add_argument(
        "--config",
        type=Path,
        default=Path(os.getenv("COLLECTOR_CONFIG", DEFAULT_CONFIG)),
        help="Collector TOML config path.",
    )
    parser.add_argument(
        "--database",
        default="device_data",
        help="TDengine database to scan/write.",
    )
    parser.add_argument(
        "--device-types",
        nargs="+",
        default=list(DEFAULT_DEVICE_TYPES),
        help="Device types to write, matching supertable names.",
    )
    parser.add_argument(
        "--points-per-device",
        type=int,
        default=1000,
        help="Number of signal columns expected per device row.",
    )
    parser.add_argument(
        "--rows-per-device",
        type=int,
        default=10,
        help="Rows to write for each device table per batch.",
    )
    parser.add_argument(
        "--interval-ms",
        type=int,
        default=1000,
        help="Write batch interval per worker in milliseconds.",
    )
    parser.add_argument(
        "--duration-seconds",
        type=float,
        default=60.0,
        help="Benchmark duration. Use 0 to run until Ctrl+C.",
    )
    parser.add_argument(
        "--workers-per-stable",
        type=int,
        default=1,
        help="Parallel writer threads per supertable.",
    )
    parser.add_argument(
        "--bind-batch-size",
        type=int,
        default=200,
        help="Device rows bound/executed per TDengine statement call.",
    )
    parser.add_argument(
        "--device-filter",
        default="",
        help="Optional regex; only matching device child tables are targeted.",
    )
    parser.add_argument(
        "--limit-devices",
        type=int,
        default=0,
        help="Optional smoke-test limit per device type. 0 means no limit.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Only print discovered target counts; do not write rows.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.points_per_device <= 0:
        raise SystemExit("--points-per-device must be greater than 0")
    if args.rows_per_device <= 0:
        raise SystemExit("--rows-per-device must be greater than 0")
    if args.interval_ms <= 0:
        raise SystemExit("--interval-ms must be greater than 0")
    if args.workers_per_stable <= 0:
        raise SystemExit("--workers-per-stable must be greater than 0")
    if args.bind_batch_size <= 0:
        raise SystemExit("--bind-batch-size must be greater than 0")

    targets_by_stable = discover_targets(args)
    total_device_tables = sum(len(tables) for tables in targets_by_stable.values())
    total_device_rows_per_batch = total_device_tables * args.rows_per_device
    total_signal_values = total_device_rows_per_batch * args.points_per_device
    target_signal_values_per_second = total_signal_values / (args.interval_ms / 1000.0)

    print(f"Config: {args.config}")
    print("Mode: wide-stmt2-existing")
    print(f"Database: {args.database}")
    print(f"Batch interval: {args.interval_ms} ms")
    print(f"Rows per device per batch: {args.rows_per_device}")
    print(f"Signal columns per device row: {args.points_per_device}")
    print(f"Total target child tables: {total_device_tables}")
    print(f"Device rows per full batch: {total_device_rows_per_batch}")
    print(f"Signal values per full batch: {total_signal_values}")
    print(f"Target signal values per second: {target_signal_values_per_second:.0f}")
    for stable, tables in targets_by_stable.items():
        print(f"  {stable}: {len(tables)} device tables")

    if total_device_tables == 0:
        print("No target tables discovered.")
        return 1
    if args.dry_run:
        print("Dry run complete. No rows written.")
        return 0

    stop_event = threading.Event()
    metrics: queue.Queue[WorkerMetric] = queue.Queue()
    threads: list[threading.Thread] = []
    interval_seconds = args.interval_ms / 1000.0

    for stable, tables in targets_by_stable.items():
        for worker_id, chunk in enumerate(
            split_evenly(tables, args.workers_per_stable),
            start=1,
        ):
            thread = threading.Thread(
                target=run_worker,
                name=f"{stable}-writer-{worker_id}",
                args=(
                    args.config,
                    args.database,
                    stable,
                    worker_id,
                    chunk,
                    args.points_per_device,
                    args.rows_per_device,
                    args.bind_batch_size,
                    interval_seconds,
                    stop_event,
                    metrics,
                ),
                daemon=True,
            )
            threads.append(thread)

    started_at = time.perf_counter()
    for thread in threads:
        thread.start()

    try:
        print_summary(metrics, started_at, args.duration_seconds, stop_event, threads)
    except KeyboardInterrupt:
        print("\nStopping benchmark...")
        stop_event.set()

    for thread in threads:
        thread.join(timeout=10)
    print("Benchmark stopped.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
