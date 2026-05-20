#!/usr/bin/env python3
"""DB-7: 启动前就绪检查。

对 collector / writer 启动前的 8 项前置条件逐一验证，
任意一项失败则打印原因并以退出码 1 退出，阻止后续服务启动。

检查项：
  1. MySQL 可达，device_point_group 至少 1 条记录
  2. device_point_group 行数 = device 总数（迁移完整）
  3. device_variable 中 td_column_name IS NOT NULL 比例 = 100%
  4. Redis 可达（PING）
  5. 所有 Stream 分片的 Consumer Group writer_cg 已创建
  6. TDengine 可达
  7. 超级表 st_qc_g001 / st_asc_g001 / st_agv_g001 存在
  8. TDengine 子表数量 = device 总数

用法：
  python scripts/check_prerequisites.py
  python scripts/check_prerequisites.py --skip-tdengine   # 跳过 TDengine 检查（未启动时）
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
    import pymysql
    import pymysql.cursors
except ImportError:
    print("ERROR: PyMySQL not installed.", file=sys.stderr)
    sys.exit(1)

try:
    import redis as redis_lib
except ImportError:
    redis_lib = None

try:
    import taosws
except ImportError:
    taosws = None


CONSUMER_GROUP = "writer_cg"
SHARD_COUNT    = 4
GROUP_CODES    = ["g001"]
TD_DATABASE    = "device_data"


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Check prerequisites before starting services")
    p.add_argument("--my-host",       default="127.0.0.1")
    p.add_argument("--my-port",       type=int, default=3306)
    p.add_argument("--my-user",       default="root")
    p.add_argument("--my-password",   default="19990910")
    p.add_argument("--my-database",   default="device_watch")
    p.add_argument("--redis-host",    default="127.0.0.1")
    p.add_argument("--redis-port",    type=int, default=6379)
    p.add_argument("--redis-db",      type=int, default=0)
    p.add_argument("--redis-password",default="")
    p.add_argument("--td-host",       default="127.0.0.1")
    p.add_argument("--td-port",       type=int, default=6041)
    p.add_argument("--td-user",       default="root")
    p.add_argument("--td-password",   default="Hry19990910")
    p.add_argument("--skip-tdengine", action="store_true",
                   help="Skip TDengine checks (use when TDengine is not started yet)")
    return p.parse_args()


class CheckResult:
    def __init__(self):
        self.passed: list[str] = []
        self.failed: list[str] = []

    def ok(self, msg: str):
        print(f"  [OK]   {msg}")
        self.passed.append(msg)

    def fail(self, msg: str):
        print(f"  [FAIL] {msg}")
        self.failed.append(msg)


def check_mysql(args, result: CheckResult) -> tuple[bool, int, list[str]]:
    """检查项 1~3。返回 (success, device_count, type_codes)。"""
    try:
        conn = pymysql.connect(
            host=args.my_host, port=args.my_port,
            user=args.my_user, password=args.my_password,
            database=args.my_database,
            charset="utf8mb4", cursorclass=pymysql.cursors.DictCursor,
            connect_timeout=5,
        )
    except Exception as e:
        result.fail(f"MySQL connection failed: {e}")
        return False, 0, []

    with conn.cursor() as cur:
        # 1. device_point_group 存在且有数据
        try:
            cur.execute("SELECT COUNT(*) cnt FROM device_point_group")
            dpg_count = cur.fetchone()["cnt"]
            if dpg_count > 0:
                result.ok(f"device_point_group has {dpg_count} records")
            else:
                result.fail("device_point_group is empty — run migrate_point_groups.py first")
        except Exception as e:
            result.fail(f"device_point_group check failed: {e}")
            conn.close()
            return False, 0, []

        # 获取 device 总数和类型列表
        cur.execute("SELECT COUNT(*) cnt FROM device")
        device_count = cur.fetchone()["cnt"]

        cur.execute("SELECT DISTINCT dt.type_code FROM device d JOIN device_type dt ON dt.id=d.type_id")
        type_codes = [r["type_code"].lower() for r in cur.fetchall()]

        # 2. device_point_group 行数 = device 总数（每台设备都有 g001 分组）
        if dpg_count >= device_count:
            result.ok(f"device_point_group rows ({dpg_count}) >= device count ({device_count})")
        else:
            result.fail(
                f"device_point_group rows ({dpg_count}) < device count ({device_count}) "
                f"— migration incomplete"
            )

        # 3. device_variable td_column_name 填充率 100%
        cur.execute("SELECT COUNT(*) total FROM device_variable")
        total_vars = cur.fetchone()["total"]
        if total_vars == 0:
            result.ok("device_variable is empty (no points configured yet)")
        else:
            cur.execute("SELECT COUNT(*) mapped FROM device_variable WHERE td_column_name IS NOT NULL")
            mapped_vars = cur.fetchone()["mapped"]
            pct = mapped_vars / total_vars * 100
            if mapped_vars == total_vars:
                result.ok(f"device_variable td_column_name mapped 100% ({mapped_vars}/{total_vars})")
            else:
                result.fail(
                    f"device_variable td_column_name not fully mapped: "
                    f"{mapped_vars}/{total_vars} ({pct:.1f}%) — run migrate_point_groups.py"
                )

    conn.close()
    return True, device_count, type_codes


def check_redis(args, result: CheckResult, type_codes: list[str]) -> bool:
    """检查项 4~5。"""
    if redis_lib is None:
        result.fail("redis-py not installed — cannot check Redis")
        return False

    try:
        r = redis_lib.Redis(
            host=args.redis_host, port=args.redis_port,
            db=args.redis_db, password=args.redis_password or None,
            decode_responses=True, socket_connect_timeout=5,
        )
        r.ping()
        result.ok(f"Redis {args.redis_host}:{args.redis_port} reachable")
    except Exception as e:
        result.fail(f"Redis connection failed: {e}")
        return False

    # 5. 检查所有 Stream 分片的 Consumer Group
    missing = []
    for dtype in type_codes:
        for gc in GROUP_CODES:
            for shard in range(SHARD_COUNT):
                key = f"rt:stream:{dtype}:{gc}:{shard}"
                try:
                    groups = r.xinfo_groups(key)
                    cg_names = [g["name"] for g in groups]
                    if CONSUMER_GROUP not in cg_names:
                        missing.append(key)
                except redis_lib.exceptions.ResponseError:
                    missing.append(key)  # Stream key 不存在

    if not missing:
        result.ok(f"All {SHARD_COUNT * len(type_codes) * len(GROUP_CODES)} Stream Consumer Groups exist")
    else:
        result.fail(
            f"Consumer Group missing on {len(missing)} Stream(s): {missing[:3]}..."
            f" — run init_redis_consumer_groups.py"
        )
    return True


def check_tdengine(args, result: CheckResult, device_count: int, type_codes: list[str]) -> bool:
    """检查项 6~8。"""
    if taosws is None:
        result.fail("taosws not installed — cannot check TDengine")
        return False

    try:
        conn = taosws.connect(
            user=args.td_user, password=args.td_password,
            host=args.td_host, port=args.td_port,
        )
        result.ok(f"TDengine {args.td_host}:{args.td_port} reachable")
    except Exception as e:
        result.fail(f"TDengine connection failed: {e}")
        return False

    # 7. 超级表存在
    expected_stables = {f"st_{tc}_{gc}" for tc in type_codes for gc in GROUP_CODES}
    try:
        rows = list(conn.query(f"SHOW {TD_DATABASE}.STABLES"))
        existing = {str(r[0]) for r in rows}
        missing_st = expected_stables - existing
        if not missing_st:
            result.ok(f"Supertables {sorted(expected_stables)} all exist")
        else:
            result.fail(f"Supertables missing: {sorted(missing_st)} — run init_tdengine_v2.py")
    except Exception as e:
        result.fail(f"TDengine SHOW STABLES failed: {e}")

    # 8. 子表数量
    try:
        rows = list(conn.query(f"SHOW {TD_DATABASE}.TABLES"))
        # 只统计 v0.6 子表（以 t_ 开头）
        v2_tables = [r for r in rows if str(r[0]).startswith("t_")]
        child_count = len(v2_tables)
        if child_count >= device_count:
            result.ok(f"TDengine child tables ({child_count}) >= device count ({device_count})")
        else:
            result.fail(
                f"TDengine child tables ({child_count}) < device count ({device_count}) "
                f"— run init_tdengine_v2.py"
            )
    except Exception as e:
        result.fail(f"TDengine SHOW TABLES failed: {e}")

    conn.close()
    return True


def main() -> int:
    args = parse_args()
    result = CheckResult()

    print("=== Checking MySQL ===")
    mysql_ok, device_count, type_codes = check_mysql(args, result)

    print("\n=== Checking Redis ===")
    check_redis(args, result, type_codes if mysql_ok else ["qc", "asc", "agv"])

    if args.skip_tdengine:
        print("\n=== Skipping TDengine (--skip-tdengine) ===")
    else:
        print("\n=== Checking TDengine ===")
        check_tdengine(args, result, device_count, type_codes if mysql_ok else ["qc", "asc", "agv"])

    print(f"\n{'='*40}")
    total = len(result.passed) + len(result.failed)
    if result.failed:
        print(f"FAILED {len(result.failed)}/{total} checks:")
        for f in result.failed:
            print(f"  - {f}")
        return 1
    else:
        print(f"[OK] all {total} prerequisites satisfied")
        return 0


if __name__ == "__main__":
    raise SystemExit(main())
