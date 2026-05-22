#!/usr/bin/env python3
"""Create wide-table TDengine benchmark topology.

Topology:
- One database: device_data
- Three supertables: qc, asc, agv
- One child table per device from MySQL device rows
- 1000 signal columns per supertable by default
"""

from __future__ import annotations

import argparse
import asyncio
import os
import re
import sys
import time
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


def quote_string(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


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


def database_exists(conn, database: str) -> bool:
    rows = query_rows(conn, "SHOW DATABASES")
    return any(str(row[0]).lower() == database.lower() for row in rows)


def build_create_database_sql(
    database: str,
    *,
    vgroups: int,
    buffer: int,
    pages: int,
    keep_days: int,
    duration_days: int,
    wal_level: int,
    wal_fsync_period: int,
) -> str:
    return (
        f"CREATE DATABASE IF NOT EXISTS {quote_identifier(database)} "
        "PRECISION 'ms' "
        f"KEEP {keep_days} "
        f"DURATION {duration_days}d "
        f"VGROUPS {vgroups} "
        f"BUFFER {buffer} "
        f"PAGES {pages} "
        f"WAL_LEVEL {wal_level} "
        f"WAL_FSYNC_PERIOD {wal_fsync_period} "
        "CACHEMODEL 'none' "
        "STT_TRIGGER 2"
    )


def ensure_database(conn, args: argparse.Namespace) -> None:
    existed = database_exists(conn, args.database)
    if existed:
        print(
            f"[{args.database}] database exists; keeping current options. "
            "CREATE DATABASE IF NOT EXISTS will not alter BUFFER/PAGES/VGROUPS."
        )
        return

    print(
        f"[{args.database}] create database: "
        f"vgroups={args.db_vgroups}, buffer={args.db_buffer}, pages={args.db_pages}"
    )
    if not args.dry_run:
        conn.execute(
            build_create_database_sql(
                args.database,
                vgroups=args.db_vgroups,
                buffer=args.db_buffer,
                pages=args.db_pages,
                keep_days=args.db_keep_days,
                duration_days=args.db_duration_days,
                wal_level=args.wal_level,
                wal_fsync_period=args.wal_fsync_period,
            )
        )


def build_create_stable_sql(database: str, stable: str, points_per_device: int) -> str:
    columns = ["`ts` TIMESTAMP"]
    columns.extend(
        f"{quote_identifier(signal_column_name(stable, index))} DOUBLE"
        for index in range(1, points_per_device + 1)
    )
    return (
        f"CREATE STABLE IF NOT EXISTS "
        f"{quote_identifier(database)}.{quote_identifier(stable)} "
        f"({', '.join(columns)}) TAGS (`device_code` VARCHAR(32))"
    )


def ensure_stable(
    conn,
    database: str,
    stable: str,
    points_per_device: int,
    dry_run: bool,
) -> None:
    if dry_run:
        return
    conn.execute(build_create_stable_sql(database, stable, points_per_device))


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


def iter_chunks(items: Sequence[str], size: int) -> Iterable[Sequence[str]]:
    for index in range(0, len(items), size):
        yield items[index : index + size]


def build_create_child_tables_sql(
    database: str,
    stable: str,
    devices: Sequence[str],
) -> str:
    database_id = quote_identifier(database)
    stable_id = quote_identifier(stable)
    parts = []
    for device in devices:
        device_id = quote_identifier(device)
        parts.append(
            f"{database_id}.{device_id} "
            f"USING {database_id}.{stable_id} TAGS ({quote_string(device)})"
        )
    return "CREATE TABLE IF NOT EXISTS " + " ".join(parts)


def create_child_tables(
    conn,
    database: str,
    stable: str,
    devices: Sequence[str],
    batch_size: int,
    dry_run: bool,
    continue_on_error: bool,
) -> tuple[int, int]:
    created_targets = 0
    errors = 0
    for chunk in iter_chunks(list(devices), batch_size):
        if dry_run:
            created_targets += len(chunk)
            continue

        sql = build_create_child_tables_sql(database, stable, chunk)
        try:
            conn.execute(sql)
            created_targets += len(chunk)
        except Exception as exc:
            errors += 1
            print(
                f"[WARN] batch create failed stable={stable} "
                f"count={len(chunk)} error={exc}"
            )
            for device in chunk:
                try:
                    conn.execute(build_create_child_tables_sql(database, stable, [device]))
                    created_targets += 1
                except Exception as item_exc:
                    errors += 1
                    print(
                        f"[ERROR] create failed table="
                        f"{database}.{device} error={item_exc}"
                    )
                    if not continue_on_error:
                        raise

    return created_targets, errors


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Create device_data with qc/asc/agv wide supertables and one "
            "child table per device."
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
        help="TDengine database to create/use.",
    )
    parser.add_argument(
        "--device-types",
        nargs="+",
        default=list(DEFAULT_DEVICE_TYPES),
        help="Device types from MySQL device_type.type_code to create as supertables.",
    )
    parser.add_argument(
        "--points-per-device",
        type=int,
        default=1000,
        help="Number of signal columns to create for each device type.",
    )
    parser.add_argument(
        "--create-batch-size",
        type=int,
        default=200,
        help="How many child tables to include in each CREATE TABLE statement.",
    )
    parser.add_argument(
        "--db-vgroups",
        type=int,
        default=16,
        help="Database VGROUPS option for newly created database.",
    )
    parser.add_argument(
        "--db-buffer",
        type=int,
        default=96,
        help="Database BUFFER option for newly created database.",
    )
    parser.add_argument(
        "--db-pages",
        type=int,
        default=256,
        help="Database PAGES option for newly created database.",
    )
    parser.add_argument(
        "--db-keep-days",
        type=int,
        default=365,
        help="Database KEEP option in days for newly created database.",
    )
    parser.add_argument(
        "--db-duration-days",
        type=int,
        default=30,
        help="Database DURATION option in days for newly created database.",
    )
    parser.add_argument(
        "--wal-level",
        type=int,
        default=1,
        help="Database WAL_LEVEL option for newly created database.",
    )
    parser.add_argument(
        "--wal-fsync-period",
        type=int,
        default=3000,
        help="Database WAL_FSYNC_PERIOD option for newly created database.",
    )
    parser.add_argument(
        "--device-filter",
        default="",
        help="Optional regex; only matching device child tables are created.",
    )
    parser.add_argument(
        "--limit-devices",
        type=int,
        default=0,
        help="Optional smoke-test limit per device type. 0 means no limit.",
    )
    parser.add_argument(
        "--continue-on-error",
        action="store_true",
        help="Continue after individual child table creation errors.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Only print planned work; do not create databases or tables.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.points_per_device <= 0:
        raise SystemExit("--points-per-device must be greater than 0")
    if args.create_batch_size <= 0:
        raise SystemExit("--create-batch-size must be greater than 0")
    for value_name in [
        "db_vgroups",
        "db_buffer",
        "db_pages",
        "db_keep_days",
        "db_duration_days",
        "wal_level",
        "wal_fsync_period",
    ]:
        if getattr(args, value_name) <= 0:
            raise SystemExit(f"--{value_name.replace('_', '-')} must be greater than 0")

    device_types = [
        normalize_identifier(item, prefix="type")
        for item in args.device_types
        if str(item).strip()
    ]
    if not device_types:
        raise SystemExit("--device-types must include at least one type")

    pattern = re.compile(args.device_filter) if args.device_filter else None
    devices_by_type = load_mysql_devices(args.config, device_types)

    print(f"Config: {args.config}")
    print(f"Database: {args.database}")
    print(f"Supertables: {', '.join(device_types)}")
    print(f"Signal columns per supertable: {args.points_per_device}")
    print(
        "New database options: "
        f"vgroups={args.db_vgroups}, buffer={args.db_buffer}, pages={args.db_pages}"
    )
    print("Child table rule: one child table per MySQL device_code")
    print("Tag rule: device_code tag is retained because TDengine supertables require TAGS")
    print(f"Dry run: {args.dry_run}")

    start = time.perf_counter()
    total_stables = 0
    total_child_tables = 0
    total_errors = 0

    conn = None
    try:
        if not args.dry_run:
            conn = connect(args.config)
            ensure_database(conn, args)
        else:
            print(f"[{args.database}] dry run; skip TDengine connection and creation.")

        for stable in device_types:
            devices = devices_by_type.get(stable, [])
            if pattern:
                devices = [name for name in devices if pattern.search(name)]
            if args.limit_devices > 0:
                devices = devices[: args.limit_devices]

            print(f"\n[{stable}] devices selected: {len(devices)}")
            if not devices:
                print(f"  WARN no MySQL devices selected for {stable}.")
                continue

            if conn is not None:
                ensure_stable(
                    conn,
                    args.database,
                    stable,
                    args.points_per_device,
                    args.dry_run,
                )
            if not args.dry_run:
                if conn is None:
                    raise RuntimeError("TDengine connection is required outside dry-run")
                ok, reason = stable_has_current_schema(
                    conn,
                    args.database,
                    stable,
                    args.points_per_device,
                )
                if not ok:
                    total_errors += 1
                    print(f"  SKIP {stable}: {reason}")
                    if not args.continue_on_error:
                        raise SystemExit(
                            f"Stable schema is not current: {args.database}.{stable}"
                        )
                    continue

            created, errors = create_child_tables(
                conn=conn,
                database=args.database,
                stable=stable,
                devices=devices,
                batch_size=args.create_batch_size,
                dry_run=args.dry_run,
                continue_on_error=args.continue_on_error,
            )
            print(
                f"  stable={stable}: signal_columns={args.points_per_device}, "
                f"target_child_tables={created}"
            )
            total_stables += 1
            total_child_tables += created
            total_errors += errors

    finally:
        if conn is not None:
            conn.close()

    elapsed = time.perf_counter() - start
    print(
        "\nDone. "
        f"processed_stables={total_stables}, "
        f"target_child_tables={total_child_tables}, "
        f"errors={total_errors}, "
        f"elapsed_seconds={elapsed:.3f}"
    )
    return 1 if total_errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
