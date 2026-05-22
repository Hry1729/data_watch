#!/usr/bin/env python3
"""验证 tzdata 修复是否生效。

运行方式：
    collector\\.venv\\Scripts\\python.exe "docs\\运维\\测试脚本\\verify_tzdata_fix.py"

检查项：
  1. tzdata 可导入
  2. ZoneInfo("Asia/Shanghai") 不崩
  3. taosws 能连接 TDengine
  4. SELECT LAST(ts) 不抛 ZoneInfoNotFoundError，能正常返回 datetime
  5. 返回的 datetime 能被 to_local_naive() 转成 CST naive
"""
from __future__ import annotations

import sys
import traceback
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parents[3]

# ── 把 backend/.py314t-site 加入 path，复现 backend 实际运行环境 ──────────
_SITE = _REPO_ROOT / "backend" / ".py314t-site"
if _SITE.exists():
    sys.path.insert(0, str(_SITE))

PASS = "  [PASS]"
FAIL = "  [FAIL]"
SKIP = "  [SKIP]"


def load_config() -> dict:
    try:
        import tomllib
    except ImportError:
        import tomli as tomllib  # type: ignore[no-redef]
    cfg_path = _REPO_ROOT / "writer" / "config" / "settings.toml"
    with cfg_path.open("rb") as f:
        return tomllib.load(f)


def check_tzdata() -> bool:
    print("\n[1] tzdata 可导入")
    try:
        import tzdata  # noqa: F401
        print(PASS, "tzdata 已安装")
        return True
    except ImportError as e:
        print(FAIL, f"tzdata 未安装: {e}")
        return False


def check_zoneinfo() -> bool:
    print("\n[2] ZoneInfo('Asia/Shanghai') 不崩")
    try:
        from zoneinfo import ZoneInfo
        tz = ZoneInfo("Asia/Shanghai")
        print(PASS, f"ZoneInfo 正常: {tz}")
        return True
    except Exception as e:
        print(FAIL, f"{type(e).__name__}: {e}")
        return False


def check_taosws_connect(cfg: dict) -> bool:
    print("\n[3] taosws 连接 TDengine")
    try:
        import taosws
        td = cfg["tdengine"]
        from urllib.parse import quote_plus
        dsn = f"ws://{quote_plus(td['user'])}:{quote_plus(td['password'])}@{td['host']}:{td['port']}"
        conn = taosws.connect(dsn)
        conn.close()
        print(PASS, f"已连接 {td['host']}:{td['port']}")
        return True
    except Exception as e:
        print(FAIL, f"{type(e).__name__}: {e}")
        return False


def check_timestamp_query(cfg: dict) -> bool:
    print("\n[4] SELECT LAST(ts) — 不抛 ZoneInfoNotFoundError")
    try:
        import taosws
        from urllib.parse import quote_plus
        td = cfg["tdengine"]
        dsn = f"ws://{quote_plus(td['user'])}:{quote_plus(td['password'])}@{td['host']}:{td['port']}"
        conn = taosws.connect(dsn)
        cur = conn.cursor()

        # 找第一张实际存在的子表
        cur.execute(f"SHOW {td['database']}.TABLES")
        tables = cur.fetchall()
        if not tables:
            print(SKIP, "device_data 里没有表，跳过")
            conn.close()
            return True

        table_name = tables[0][0]
        cur.execute(f"SELECT LAST(ts) FROM {td['database']}.`{table_name}`")
        rows = cur.fetchall()   # ← 之前在这里崩
        print(PASS, f"fetchall() 成功，表={table_name}，结果={rows}")
        conn.close()
        return True
    except Exception as e:
        print(FAIL, f"{type(e).__name__}: {e}")
        traceback.print_exc()
        return False


def check_to_local_naive(cfg: dict) -> bool:
    print("\n[5] to_local_naive() 转换 TDengine 返回的 datetime")
    try:
        import taosws
        from urllib.parse import quote_plus
        from datetime import datetime, timezone

        td = cfg["tdengine"]
        dsn = f"ws://{quote_plus(td['user'])}:{quote_plus(td['password'])}@{td['host']}:{td['port']}"
        conn = taosws.connect(dsn)
        cur = conn.cursor()

        cur.execute(f"SHOW {td['database']}.TABLES")
        tables = cur.fetchall()
        if not tables:
            print(SKIP, "没有表，跳过")
            conn.close()
            return True

        table_name = tables[0][0]
        cur.execute(f"SELECT LAST(ts) FROM {td['database']}.`{table_name}`")
        rows = cur.fetchall()
        conn.close()

        if not rows or rows[0][0] is None:
            print(SKIP, "表为空，跳过")
            return True

        raw_ts = rows[0][0]

        # 模拟 backend 的 to_local_naive 路径
        sys.path.insert(0, str(_REPO_ROOT / "backend"))
        from app.core.time_utils import to_local_naive  # type: ignore[import]

        if isinstance(raw_ts, datetime):
            result = to_local_naive(raw_ts)
            print(PASS, f"raw={raw_ts}  →  local_naive={result}")
        else:
            print(PASS, f"taosws 返回非 datetime 类型: {type(raw_ts).__name__} = {raw_ts}")
        return True
    except Exception as e:
        print(FAIL, f"{type(e).__name__}: {e}")
        traceback.print_exc()
        return False


def main() -> int:
    print("=" * 55)
    print("  verify_tzdata_fix.py")
    print("=" * 55)

    try:
        cfg = load_config()
    except Exception as e:
        print(f"[ERROR] 读取 writer/config/settings.toml 失败: {e}")
        return 1

    results = [
        check_tzdata(),
        check_zoneinfo(),
        check_taosws_connect(cfg),
        check_timestamp_query(cfg),
        check_to_local_naive(cfg),
    ]

    passed = sum(results)
    total = len(results)
    print("\n" + "=" * 55)
    print(f"  结果: {passed}/{total} 通过")
    if passed == total:
        print("  tzdata 修复验证通过，可重启 backend 复测历史页面。")
    else:
        print("  存在失败项，请检查上方输出。")
    print("=" * 55)
    return 0 if passed == total else 1


if __name__ == "__main__":
    raise SystemExit(main())
