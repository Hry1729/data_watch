#!/usr/bin/env python3
"""DB-5: 初始化 TDengine v0.6 宽表结构。

为每种设备类型创建超级表 st_{type}_g001，列名 v0001~v3072（全 DOUBLE），
再为每台设备创建子表 t_{device_code}_g001。

幂等性：CREATE STABLE/TABLE IF NOT EXISTS，可重复执行。
不影响旧表（qc / asc / agv 窄表超级表保留不动）。

依赖：taos-ws-py (taosws)、PyMySQL
用法：
  python scripts/init_tdengine_v2.py
  python scripts/init_tdengine_v2.py --td-host 127.0.0.1 --td-port 6041
"""

from __future__ import annotations

import argparse
import sys
import os
from pathlib import Path

# 自动检测 collector/.venv
_PROJECT_ROOT = Path(__file__).resolve().parent.parent
_VENV_SP = _PROJECT_ROOT / "collector" / ".venv" / "Lib" / "site-packages"
if _VENV_SP.exists():
    sys.path.insert(0, str(_VENV_SP))

try:
    import taosws
except ImportError:
    print("ERROR: taosws not installed. Run: pip install taos-ws-py", file=sys.stderr)
    sys.exit(1)

try:
    import pymysql
    import pymysql.cursors
except ImportError:
    print("ERROR: PyMySQL not installed. Run: pip install PyMySQL", file=sys.stderr)
    sys.exit(1)


GROUP_CODE     = "g001"
MAX_COLUMNS    = 3072
TD_DATABASE    = "device_data"


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Init TDengine v0.6 supertables and child tables")
    p.add_argument("--td-host",     default="127.0.0.1")
    p.add_argument("--td-port",     type=int, default=6041)
    p.add_argument("--td-user",     default="root")
    p.add_argument("--td-password", default="Hry19990910")
    p.add_argument("--my-host",     default="127.0.0.1")
    p.add_argument("--my-port",     type=int, default=3306)
    p.add_argument("--my-user",     default="root")
    p.add_argument("--my-password", default="19990910")
    p.add_argument("--my-database", default="device_watch")
    p.add_argument("--dry-run",     action="store_true",
                   help="Print SQL without executing")
    return p.parse_args()


def build_stable_sql(type_lower: str) -> str:
    """生成 CREATE STABLE SQL，含 ts + v0001..v3072 DOUBLE + TAGS。"""
    cols = ", ".join(f"v{i:04d} DOUBLE" for i in range(1, MAX_COLUMNS + 1))
    stable = f"{TD_DATABASE}.st_{type_lower}_{GROUP_CODE}"
    return (
        f"CREATE STABLE IF NOT EXISTS {stable} "
        f"(ts TIMESTAMP, {cols}) "
        f"TAGS (device_code NCHAR(32))"
    )


def build_child_sql(type_lower: str, device_code: str) -> str:
    stable = f"{TD_DATABASE}.st_{type_lower}_{GROUP_CODE}"
    child  = f"{TD_DATABASE}.t_{device_code}_{GROUP_CODE}"
    return (
        f"CREATE TABLE IF NOT EXISTS {child} "
        f"USING {stable} TAGS ('{device_code}')"
    )


def load_devices(my_args) -> dict[str, list[str]]:
    """从 MySQL 加载 type_code → [device_code, ...] 映射。"""
    conn = pymysql.connect(
        host=my_args.my_host, port=my_args.my_port,
        user=my_args.my_user, password=my_args.my_password,
        database=my_args.my_database,
        charset="utf8mb4", cursorclass=pymysql.cursors.DictCursor,
    )
    result: dict[str, list[str]] = {}
    try:
        with conn.cursor() as cur:
            cur.execute("""
                SELECT d.device_code, dt.type_code
                FROM device d
                JOIN device_type dt ON dt.id = d.type_id
                ORDER BY dt.type_code, d.device_code
            """)
            for row in cur.fetchall():
                tc = row["type_code"].lower()
                result.setdefault(tc, []).append(row["device_code"])
    finally:
        conn.close()
    return result


def main() -> int:
    args = parse_args()
    print(f"Loading devices from MySQL {args.my_host}:{args.my_port}/{args.my_database} ...")
    devices_by_type = load_devices(args)

    total_devices = sum(len(v) for v in devices_by_type.values())
    print(f"Device types: {list(devices_by_type.keys())}  total={total_devices}")

    if args.dry_run:
        for tc, codes in devices_by_type.items():
            sql = build_stable_sql(tc)
            print(f"\n[dry-run] STABLE SQL (first 120 chars): {sql[:120]}...")
            print(f"[dry-run] Would create {len(codes)} child tables for type={tc}")
        return 0

    print(f"Connecting to TDengine {args.td_host}:{args.td_port} ...")
    conn = taosws.connect(
        user=args.td_user, password=args.td_password,
        host=args.td_host, port=args.td_port,
    )

    # 确保数据库存在
    conn.execute(f"CREATE DATABASE IF NOT EXISTS {TD_DATABASE}")
    print(f"Database {TD_DATABASE}: OK")

    stable_created = stable_skipped = child_created = child_skipped = 0

    for tc, device_codes in devices_by_type.items():
        # 创建/确认超级表
        stable_name = f"st_{tc}_{GROUP_CODE}"
        sql = build_stable_sql(tc)
        print(f"\nCreating supertable {TD_DATABASE}.{stable_name} ({MAX_COLUMNS} columns) ...")
        conn.execute(sql)
        print(f"  OK: {stable_name}")
        stable_created += 1

        # 创建子表
        print(f"  Creating {len(device_codes)} child tables ...")
        for device_code in device_codes:
            try:
                conn.execute(build_child_sql(tc, device_code))
                child_created += 1
            except Exception as e:
                if "already exist" in str(e).lower() or "table already exists" in str(e).lower():
                    child_skipped += 1
                else:
                    print(f"  [WARN] {device_code}: {e}")

    conn.close()

    print(f"\nDone.")
    print(f"  Supertables: created={stable_created} skipped={stable_skipped}")
    print(f"  Child tables: created={child_created} skipped={child_skipped}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
