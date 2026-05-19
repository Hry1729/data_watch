#!/usr/bin/env python3
"""Create TDengine benchmark databases, supertables, and child tables.

The script is intentionally non-destructive: it creates missing objects and
never drops existing TDengine databases, supertables, or child tables.
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

import taosws
from sqlalchemy import select


SCRIPT_DIR = Path(__file__).resolve().parent
COLLECTOR_ROOT = SCRIPT_DIR.parent
DEFAULT_CONFIG = COLLECTOR_ROOT / "config" / "settings.toml"
sys.path.insert(0, str(COLLECTOR_ROOT))

from core.config_loader import load_collector_config  # noqa: E402
from core.config_db import ConfigDBManager, DatabaseConfig, DBDevice, DBDeviceType  # noqa: E402


DEFAULT_DB_OPTIONS = {
    "qc": {"vgroups": 4},
    "asc": {"vgroups": 12},
    "agv": {"vgroups": 16},
}

STABLE_SCHEMA = (
    "(`ts` TIMESTAMP, `value` DOUBLE, `quality` TINYINT) "
    "TAGS (`var_seq` INT)"
)
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


def database_name_from_device_type(device_type: str) -> str:
    return normalize_identifier(device_type, prefix="db")


def stable_name_from_device_code(device_code: str) -> str:
    return normalize_identifier(device_code, prefix="dev")


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


async def load_mysql_stables_async(config_path: Path) -> dict[str, list[str]]:
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

    stables_by_db: dict[str, list[str]] = {}
    for row in rows:
        device_code = str(row.device_code or "").strip()
        device_type = str(row.type_code or "").strip()
        if not device_code or not device_type:
            continue
        database = database_name_from_device_type(device_type)
        stable = stable_name_from_device_code(device_code)
        stables_by_db.setdefault(database, []).append(stable)

    for database, stables in stables_by_db.items():
        stables_by_db[database] = list(dict.fromkeys(stables))
    return stables_by_db


def load_mysql_stables(config_path: Path) -> dict[str, list[str]]:
    return asyncio.run(load_mysql_stables_async(config_path))


def database_exists(conn, database: str) -> bool:
    return any(str(row[0]).lower() == database.lower() for row in query_rows(conn, "SHOW DATABASES"))


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


def resolve_vgroups(database: str, default_vgroups: int) -> int:
    return DEFAULT_DB_OPTIONS.get(database.lower(), {}).get("vgroups", default_vgroups)


def ensure_database(conn, database: str, args: argparse.Namespace) -> bool:
    existed = database_exists(conn, database)
    vgroups = resolve_vgroups(database, args.default_vgroups)
    sql = build_create_database_sql(
        database,
        vgroups=vgroups,
        buffer=args.db_buffer,
        pages=args.db_pages,
        keep_days=args.db_keep_days,
        duration_days=args.db_duration_days,
        wal_level=args.wal_level,
        wal_fsync_period=args.wal_fsync_period,
    )

    if existed:
        print(
            f"[{database}] database exists; keeping current options. "
            "CREATE DATABASE IF NOT EXISTS will not alter BUFFER/PAGES/VGROUPS."
        )
        return True

    print(
        f"[{database}] create database: "
        f"vgroups={vgroups}, buffer={args.db_buffer}, pages={args.db_pages}"
    )
    if not args.dry_run:
        conn.execute(sql)
    return False


def stable_has_current_schema(conn, database: str, stable: str) -> tuple[bool, str]:
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

    missing_columns = {"ts", "value", "quality"} - columns
    missing_tags = {"var_seq"} - tags
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


def child_table_name(stable: str, var_seq: int) -> str:
    return f"{stable}_{var_seq}"


def build_create_stable_sql(database: str, stable: str) -> str:
    return (
        f"CREATE STABLE IF NOT EXISTS "
        f"{quote_identifier(database)}.{quote_identifier(stable)} "
        f"{STABLE_SCHEMA}"
    )


def ensure_stable(conn, database: str, stable: str, dry_run: bool) -> None:
    if dry_run:
        return
    conn.execute(build_create_stable_sql(database, stable))


def build_create_sql(
    database: str,
    stable: str,
    var_seqs: Sequence[int],
) -> str:
    database_id = quote_identifier(database)
    stable_id = quote_identifier(stable)
    parts = []
    for var_seq in var_seqs:
        table_id = quote_identifier(child_table_name(stable, var_seq))
        parts.append(
            f"{database_id}.{table_id} "
            f"USING {database_id}.{stable_id} TAGS ({var_seq})"
        )
    return "CREATE TABLE IF NOT EXISTS " + " ".join(parts)


def create_child_tables(
    conn,
    database: str,
    stable: str,
    var_seqs: Sequence[int],
    batch_size: int,
    dry_run: bool,
    continue_on_error: bool,
) -> tuple[int, int]:
    created_targets = 0
    errors = 0
    for chunk in iter_chunks(list(var_seqs), batch_size):
        if dry_run:
            created_targets += len(chunk)
            continue

        sql = build_create_sql(database, stable, chunk)
        try:
            conn.execute(sql)
            created_targets += len(chunk)
        except Exception as exc:
            errors += 1
            print(
                f"[WARN] batch create failed db={database} stable={stable} "
                f"count={len(chunk)} error={exc}"
            )
            for var_seq in chunk:
                try:
                    conn.execute(build_create_sql(database, stable, [var_seq]))
                    created_targets += 1
                except Exception as item_exc:
                    errors += 1
                    print(
                        f"[ERROR] create failed table="
                        f"{database}.{child_table_name(stable, var_seq)} "
                        f"error={item_exc}"
                    )
                    if not continue_on_error:
                        raise

    return created_targets, errors


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Create benchmark databases, supertables, and child tables named "
            "{stable}_{var_seq}."
        )
    )
    parser.add_argument(
        "--config",
        type=Path,
        default=Path(os.getenv("COLLECTOR_CONFIG", DEFAULT_CONFIG)),
        help="Collector TOML config path.",
    )
    parser.add_argument(
        "--databases",
        nargs="+",
        default=["qc", "asc", "agv"],
        help="TDengine databases to create/use.",
    )
    parser.add_argument(
        "--stable-names",
        nargs="*",
        default=[],
        help=(
            "Optional explicit supertables to create/use, formatted as "
            "db=stable1,stable2. If omitted, MySQL device rows are used."
        ),
    )
    parser.add_argument(
        "--subtables-per-stable",
        type=int,
        default=1000,
        help="Number of benchmark child tables to create for each supertable.",
    )
    parser.add_argument(
        "--start-var-seq",
        type=int,
        default=1,
        help="First var_seq value. Default creates 1..1000.",
    )
    parser.add_argument(
        "--create-batch-size",
        type=int,
        default=200,
        help="How many child tables to include in each CREATE TABLE statement.",
    )
    parser.add_argument(
        "--db-buffer",
        type=int,
        default=96,
        help="Database BUFFER option for newly created databases.",
    )
    parser.add_argument(
        "--db-pages",
        type=int,
        default=256,
        help="Database PAGES option for newly created databases.",
    )
    parser.add_argument(
        "--default-vgroups",
        type=int,
        default=4,
        help="VGROUPS for non-qc/asc/agv databases.",
    )
    parser.add_argument(
        "--db-keep-days",
        type=int,
        default=365,
        help="Database KEEP option in days for newly created databases.",
    )
    parser.add_argument(
        "--db-duration-days",
        type=int,
        default=30,
        help="Database DURATION option in days for newly created databases.",
    )
    parser.add_argument(
        "--wal-level",
        type=int,
        default=1,
        help="Database WAL_LEVEL option for newly created databases.",
    )
    parser.add_argument(
        "--wal-fsync-period",
        type=int,
        default=3000,
        help="Database WAL_FSYNC_PERIOD option for newly created databases.",
    )
    parser.add_argument(
        "--stable-filter",
        default="",
        help="Optional regex; only matching supertables are processed.",
    )
    parser.add_argument(
        "--limit-stables",
        type=int,
        default=0,
        help="Optional smoke-test limit per database. 0 means no limit.",
    )
    parser.add_argument(
        "--continue-on-error",
        action="store_true",
        help="Continue after individual child table creation errors.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Only print planned work; do not create tables.",
    )
    return parser.parse_args()


def parse_explicit_stables(raw_items: Sequence[str]) -> dict[str, list[str]]:
    explicit: dict[str, list[str]] = {}
    for item in raw_items:
        if "=" not in item:
            raise SystemExit(
                "--stable-names entries must be formatted as db=stable1,stable2"
            )
        database, stables_raw = item.split("=", 1)
        names = [name.strip() for name in stables_raw.split(",") if name.strip()]
        if not database.strip() or not names:
            raise SystemExit(
                "--stable-names entries must include a database and at least one stable"
            )
        explicit[database.strip().lower()] = names
    return explicit


def main() -> int:
    args = parse_args()
    if args.subtables_per_stable <= 0:
        raise SystemExit("--subtables-per-stable must be greater than 0")
    if args.create_batch_size <= 0:
        raise SystemExit("--create-batch-size must be greater than 0")
    for value_name in [
        "db_buffer",
        "db_pages",
        "default_vgroups",
        "db_keep_days",
        "db_duration_days",
        "wal_level",
        "wal_fsync_period",
    ]:
        if getattr(args, value_name) <= 0:
            raise SystemExit(f"--{value_name.replace('_', '-')} must be greater than 0")

    pattern = re.compile(args.stable_filter) if args.stable_filter else None
    explicit_stables = parse_explicit_stables(args.stable_names)
    var_seqs = list(
        range(args.start_var_seq, args.start_var_seq + args.subtables_per_stable)
    )

    print(f"Config: {args.config}")
    print(f"Databases: {', '.join(args.databases)}")
    print(
        "New database options: "
        f"buffer={args.db_buffer}, pages={args.db_pages}, "
        "vgroups defaults qc=4 asc=12 agv=16"
    )
    print(
        "Child table rule: {stable}_{var_seq}, "
        f"var_seq={var_seqs[0]}..{var_seqs[-1]}"
    )
    print(f"Dry run: {args.dry_run}")

    start = time.perf_counter()
    total_stables = 0
    total_targets = 0
    total_errors = 0
    mysql_stables_by_db: dict[str, list[str]] | None = None

    conn = connect(args.config)
    try:
        for database in args.databases:
            ensure_database(conn, database, args)
            if database.lower() in explicit_stables:
                stables = explicit_stables[database.lower()]
            else:
                if mysql_stables_by_db is None:
                    try:
                        mysql_stables_by_db = load_mysql_stables(args.config)
                    except Exception as exc:
                        raise SystemExit(
                            f"Failed to load MySQL device metadata: {exc}"
                        ) from exc
                stables = mysql_stables_by_db.get(database.lower(), [])
            if pattern:
                stables = [name for name in stables if pattern.search(name)]
            if args.limit_stables > 0:
                stables = stables[: args.limit_stables]

            print(f"\n[{database}] supertables selected: {len(stables)}")
            if not stables:
                print(
                    f"  WARN no supertables selected for {database}. "
                    "Use --stable-names db=stable1,stable2 for a fresh database."
                )
                continue
            for index, stable in enumerate(stables, start=1):
                ensure_stable(conn, database, stable, args.dry_run)
                if not args.dry_run:
                    ok, reason = stable_has_current_schema(conn, database, stable)
                    if not ok:
                        total_errors += 1
                        print(f"  SKIP {stable}: {reason}")
                        if not args.continue_on_error:
                            raise SystemExit(
                                f"Stable schema is not current: {database}.{stable}"
                            )
                        continue

                print(
                    f"  {index}/{len(stables)} {stable}: "
                    f"target child tables={len(var_seqs)}"
                )
                created, errors = create_child_tables(
                    conn=conn,
                    database=database,
                    stable=stable,
                    var_seqs=var_seqs,
                    batch_size=args.create_batch_size,
                    dry_run=args.dry_run,
                    continue_on_error=args.continue_on_error,
                )
                total_stables += 1
                total_targets += created
                total_errors += errors

    finally:
        conn.close()

    elapsed = time.perf_counter() - start
    print(
        "\nDone. "
        f"processed_stables={total_stables}, "
        f"target_child_tables={total_targets}, "
        f"errors={total_errors}, "
        f"elapsed_seconds={elapsed:.3f}"
    )
    return 1 if total_errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
