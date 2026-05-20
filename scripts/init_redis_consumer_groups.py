#!/usr/bin/env python3
"""DB-6: 初始化 Redis Stream Consumer Group。

为每个 rt:stream:{type}:{group_code}:{shard} 创建 Consumer Group writer_cg。
BUSYGROUP（已存在）视为 skip，可安全重复执行。

依赖：redis-py
用法：
  python scripts/init_redis_consumer_groups.py
  python scripts/init_redis_consumer_groups.py --host 127.0.0.1 --port 6379 --db 0
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

_PROJECT_ROOT = Path(__file__).resolve().parent.parent
_VENV_SP = _PROJECT_ROOT / "collector" / ".venv" / "Lib" / "site-packages"
if _VENV_SP.exists():
    sys.path.insert(0, str(_VENV_SP))

try:
    import redis
except ImportError:
    print("ERROR: redis not installed. Run: pip install redis", file=sys.stderr)
    sys.exit(1)

try:
    import pymysql
    import pymysql.cursors
except ImportError:
    pymysql = None  # 可选：若不连 MySQL 则用默认类型列表


CONSUMER_GROUP = "writer_cg"
SHARD_COUNT    = 4
GROUP_CODES    = ["g001"]
DEFAULT_TYPES  = ["qc", "asc", "agv"]


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Init Redis Stream Consumer Groups")
    p.add_argument("--host",        default="127.0.0.1")
    p.add_argument("--port",        type=int, default=6379)
    p.add_argument("--db",          type=int, default=0)
    p.add_argument("--password",    default="")
    p.add_argument("--my-host",     default="127.0.0.1")
    p.add_argument("--my-port",     type=int, default=3306)
    p.add_argument("--my-user",     default="root")
    p.add_argument("--my-password", default="19990910")
    p.add_argument("--my-database", default="device_watch")
    p.add_argument("--no-mysql",    action="store_true",
                   help="Skip MySQL lookup, use default type list (qc/asc/agv)")
    return p.parse_args()


def load_type_codes(args) -> list[str]:
    """从 MySQL device_type 加载类型编码（小写）。"""
    if pymysql is None or args.no_mysql:
        return DEFAULT_TYPES
    try:
        conn = pymysql.connect(
            host=args.my_host, port=args.my_port,
            user=args.my_user, password=args.my_password,
            database=args.my_database,
            charset="utf8mb4", cursorclass=pymysql.cursors.DictCursor,
        )
        with conn.cursor() as cur:
            cur.execute("SELECT DISTINCT type_code FROM device_type ORDER BY type_code")
            codes = [r["type_code"].lower() for r in cur.fetchall()]
        conn.close()
        return codes if codes else DEFAULT_TYPES
    except Exception as e:
        print(f"[WARN] MySQL query failed, using default types: {e}", file=sys.stderr)
        return DEFAULT_TYPES


def main() -> int:
    args = parse_args()
    type_codes = load_type_codes(args)
    print(f"Device types: {type_codes}")
    print(f"Group codes:  {GROUP_CODES}")
    print(f"Shard count:  {SHARD_COUNT}")
    print(f"Consumer group: {CONSUMER_GROUP}")

    r = redis.Redis(
        host=args.host, port=args.port, db=args.db,
        password=args.password or None,
        decode_responses=True,
        socket_connect_timeout=5,
    )
    try:
        r.ping()
    except Exception as e:
        print(f"ERROR: cannot connect to Redis {args.host}:{args.port}: {e}", file=sys.stderr)
        return 1

    created = skipped = errors = 0

    for dtype in type_codes:
        for gc in GROUP_CODES:
            for shard in range(SHARD_COUNT):
                key = f"rt:stream:{dtype}:{gc}:{shard}"
                try:
                    r.xgroup_create(key, CONSUMER_GROUP, id="$", mkstream=True)
                    print(f"  [created] {key} → {CONSUMER_GROUP}")
                    created += 1
                except redis.exceptions.ResponseError as e:
                    if "BUSYGROUP" in str(e):
                        print(f"  [skip]    {key} → already exists")
                        skipped += 1
                    else:
                        print(f"  [ERROR]   {key}: {e}", file=sys.stderr)
                        errors += 1

    print(f"\nDone. created={created} skipped={skipped} errors={errors}")

    # 验证抽查第一个 key
    first_key = f"rt:stream:{type_codes[0]}:{GROUP_CODES[0]}:0"
    groups = r.xinfo_groups(first_key)
    cg_names = [g["name"] for g in groups]
    print(f"\nVerify {first_key}: consumer groups = {cg_names}")

    return 0 if errors == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
