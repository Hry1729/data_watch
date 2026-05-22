#!/usr/bin/env python3
"""
TDengine 超级表重命名脚本
  device_data.qc  → device_data.sdqc
  device_data.asc → device_data.sdasc
  device_data.agv → device_data.sdagv

操作：
  1. DROP 旧超级表（子表随之删除）
  2. CREATE 新超级表（仅含 ts + device_code TAG，列由 writer 按 MySQL 点位动态添加）

用法：
  python scripts/migrate_tdengine_rename.py --dry-run   # 只打印 SQL，不执行
  python scripts/migrate_tdengine_rename.py             # 实际执行
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_VENV = Path(__file__).resolve().parent.parent / "collector" / ".venv" / "Lib" / "site-packages"
if _VENV.exists():
    sys.path.insert(0, str(_VENV))

try:
    import taosws
except ImportError:
    print("ERROR: taosws not found. Run: pip install taos-ws-py", file=sys.stderr)
    sys.exit(1)

DATABASE = "device_data"
TAG_COL = "device_code"
TAG_TYPE = "VARCHAR(32)"

NEW_STABLES = ["sdqc", "sdasc", "sdagv"]
OLD_STABLES = ["qc", "asc", "agv"]


def q(name: str) -> str:
    return f"`{name.replace('`', '``')}`"


def run(conn, sql: str, dry_run: bool) -> None:
    if dry_run:
        print(f"  [DRY] {sql}")
    else:
        conn.execute(sql)


def main() -> None:
    parser = argparse.ArgumentParser(description="Rename TDengine super tables")
    parser.add_argument("--dry-run", action="store_true", help="只打印 SQL，不执行")
    args = parser.parse_args()

    if not args.dry_run:
        print("警告：此操作将删除旧超级表及其所有子表（历史数据丢失）。")
        print("新超级表仅含 ts + device_code TAG，列由 writer 按 MySQL 点位动态添加。")
        if input("输入 yes 继续：").strip().lower() != "yes":
            print("已取消。")
            return

    conn = taosws.connect("ws://root:Hry19990910@127.0.0.1:6041")
    try:
        for old, new in zip(OLD_STABLES, NEW_STABLES):
            print(f"\n{'='*50}")
            print(f"  {old}  →  {new}")

            run(conn, f"DROP STABLE IF EXISTS {q(DATABASE)}.{q(old)}", args.dry_run)
            print(f"  旧超级表 {old} 已删除")

            run(conn,
                f"CREATE STABLE IF NOT EXISTS {q(DATABASE)}.{q(new)} "
                f"(ts TIMESTAMP) TAGS ({q(TAG_COL)} {TAG_TYPE})",
                args.dry_run)
            print(f"  新超级表 {new} 已创建（无数据列，writer 运行时动态添加）")
    finally:
        conn.close()

    status = "Dry-run" if args.dry_run else "迁移"
    print(f"\n✓ {status} 完成。")


if __name__ == "__main__":
    main()
