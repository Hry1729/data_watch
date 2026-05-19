#!/usr/bin/env python3
"""Drop benchmark TDengine databases.

This script is destructive only when --yes is provided. By default it prints
the DROP DATABASE statements that would run.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import taosws


SCRIPT_DIR = Path(__file__).resolve().parent
COLLECTOR_ROOT = SCRIPT_DIR.parent
DEFAULT_CONFIG = COLLECTOR_ROOT / "config" / "settings.toml"
DEFAULT_DATABASES = ("qc", "asc", "agv")
sys.path.insert(0, str(COLLECTOR_ROOT))

from core.config_loader import load_collector_config  # noqa: E402


def quote_identifier(name: str) -> str:
    return f"`{name.replace('`', '``')}`"


def connect(config_path: Path):
    config = load_collector_config(str(config_path))
    td = config.tdengine
    return taosws.connect(
        user=td.user,
        password=td.password,
        host=td.host,
        port=td.port,
    )


def build_drop_database_sql(database: str) -> str:
    return f"DROP DATABASE IF EXISTS {quote_identifier(database)}"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Drop TDengine benchmark databases. Defaults to dry-run; "
            "use --yes to execute."
        )
    )
    parser.add_argument(
        "--config",
        default=str(DEFAULT_CONFIG),
        help="Collector TOML config path.",
    )
    parser.add_argument(
        "--databases",
        nargs="+",
        default=list(DEFAULT_DATABASES),
        help="TDengine databases to drop. Defaults to qc asc agv.",
    )
    parser.add_argument(
        "--yes",
        action="store_true",
        help="Actually execute DROP DATABASE. Without this flag, only prints SQL.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    databases = [database.strip() for database in args.databases if database.strip()]
    if not databases:
        print("No databases selected.")
        return 1

    print(f"Config: {Path(args.config).resolve()}")
    print(f"Databases: {', '.join(databases)}")
    print(f"Execute drops: {args.yes}")

    statements = [build_drop_database_sql(database) for database in databases]
    if not args.yes:
        print("Dry run only. Add --yes to execute:")
        for sql in statements:
            print(f"  {sql};")
        return 0

    conn = connect(Path(args.config))
    try:
        for sql in statements:
            print(sql)
            conn.execute(sql)
    finally:
        conn.close()

    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
