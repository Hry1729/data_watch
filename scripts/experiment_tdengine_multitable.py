"""TDengine 多表 INSERT 实验（性能方案阶段 5 / §7.6 前置实测）。

回答 writer 多表批写改造前必须先用真机定论的四个问题：
  1) 一条 SQL 写多个子表，部分子表数据坏时是「全有或全无」还是「部分写入」？
  2) 单条 INSERT 的子表数上限（table count）。
  3) 单条 INSERT 的 SQL 字节上限。
  4) 坏数据失败时的错误形状（供二分降级的检测逻辑参考）。

通过 taosws（WebSocket，与 writer/tdengine_writer.py 一致）连真机 TDengine，
在一个一次性实验库里跑受控实验，原始结果归档为 JSON，并打印人类可读摘要。
实验库默认结束时 DROP（--keep 保留以便人工核查）。

运行（开发机已起名为 tdengine 的容器，6041 暴露；密码改用真实值或环境注入，勿入库）：
    collector/.venv/Scripts/python.exe scripts/experiment_tdengine_multitable.py ^
        --host 127.0.0.1 --port 6041 --user root --password <TDENGINE_PASSWORD> ^
        --output D:\\data_watch_release\\tdengine-experiments
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
from datetime import datetime, timezone
from urllib.parse import quote_plus


def _now_iso() -> str:
    return datetime.now(timezone.utc).astimezone().isoformat()


def _git(args: list[str]) -> str:
    try:
        out = subprocess.run(
            ["git", *args],
            capture_output=True,
            text=True,
            cwd=os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        )
        return out.stdout.strip()
    except Exception:
        return ""


def _quote(name: str) -> str:
    return f"`{name.replace('`', '``')}`"


class TD:
    """taosws 连接的最小封装，统一捕获 execute/query 的成败与错误文本。"""

    def __init__(self, dsn: str) -> None:
        import taosws  # type: ignore[import]

        self._conn = taosws.connect(dsn)

    def execute(self, sql: str) -> tuple[bool, object]:
        """返回 (ok, affected_rows | error_str)。"""
        try:
            affected = self._conn.execute(sql)
            return True, affected
        except Exception as exc:  # noqa: BLE001 - 实验需如实记录任意错误
            return False, str(exc)

    def query_scalar(self, sql: str) -> object:
        try:
            rows = list(self._conn.query(sql))
            if rows and rows[0]:
                return rows[0][0]
        except Exception as exc:  # noqa: BLE001
            return f"<query-error: {exc}>"
        return None

    def count(self, db: str, table: str) -> int | str:
        val = self.query_scalar(f"SELECT COUNT(*) FROM {_quote(db)}.{_quote(table)}")
        if isinstance(val, int):
            return val
        # 子表从未建出（整条语句失败）→ 查询会报表不存在，记为 0
        if isinstance(val, str) and ("not exist" in val.lower() or "invalid" in val.lower()):
            return 0
        return val if isinstance(val, int) else 0

    def close(self) -> None:
        try:
            self._conn.close()
        except Exception:
            pass


# ---------------------------------------------------------------------------
# SQL 构造
# ---------------------------------------------------------------------------

STABLE = "sdexp"
NUM_COLS = 8  # c1..c8 DOUBLE


def _stable_ddl(db: str) -> str:
    cols = ", ".join(f"{_quote(f'c{i}')} DOUBLE" for i in range(1, NUM_COLS + 1))
    return (
        f"CREATE STABLE IF NOT EXISTS {_quote(db)}.{_quote(STABLE)} "
        f"(`ts` TIMESTAMP, {cols}) TAGS (`device_code` VARCHAR(32))"
    )


def _subtable_clause(db: str, table: str, ts: int, values: list[str]) -> str:
    """单子表 USING 子句：INSERT INTO `db`.`tbl` USING `db`.`sdexp` TAGS('tbl') (ts,c1..) VALUES (...)。"""
    cols = ", ".join([_quote("ts")] + [_quote(f"c{i}") for i in range(1, len(values) + 1)])
    vals = ", ".join([str(ts)] + values)
    return (
        f"{_quote(db)}.{_quote(table)} USING {_quote(db)}.{_quote(STABLE)} "
        f"TAGS ('{table}') ({cols}) VALUES ({vals})"
    )


def _build_multi(db: str, clauses: list[str]) -> str:
    return "INSERT INTO " + " ".join(clauses)


# ---------------------------------------------------------------------------
# 实验
# ---------------------------------------------------------------------------

def exp_atomicity(td: TD, db: str, base_ts: int) -> list[dict]:
    """实验 A/B：一条 SQL 写 3 个子表，中间子表含坏行，看是否部分写入。

    A: 坏值 = 非数值字面量（DOUBLE 列写 'BAD'）—— 偏 SQL 解析期错误。
    B: 坏值 = 越界时间戳（远早于 KEEP 窗口）—— 偏运行期逐行校验错误。
    每个用例把坏子表放在中间位置，执行后分别 COUNT 三个子表，判定语义。
    """
    results = []

    cases = [
        {
            "name": "bad_value_non_numeric",
            "tables": ["expA_t1", "expA_t2_bad", "expA_t3"],
            "rows": [["1.0"], ["'BAD'"], ["3.0"]],
            "ts": [base_ts, base_ts, base_ts],
        },
        {
            "name": "bad_ts_out_of_range",
            "tables": ["expB_t1", "expB_t2_bad", "expB_t3"],
            "rows": [["1.0"], ["2.0"], ["3.0"]],
            # 第二个子表时间戳设到 100 天前（越过生产 KEEP 14d 窗口 → 运行期逐行拒绝）
            "ts": [base_ts, base_ts - 100 * 24 * 3600 * 1000, base_ts],
        },
    ]

    for case in cases:
        clauses = [
            _subtable_clause(db, tbl, ts, vals)
            for tbl, ts, vals in zip(case["tables"], case["ts"], case["rows"])
        ]
        sql = _build_multi(db, clauses)
        ok, info = td.execute(sql)
        counts = {tbl: td.count(db, tbl) for tbl in case["tables"]}
        good_written = all(
            isinstance(counts[t], int) and counts[t] >= 1
            for t in (case["tables"][0], case["tables"][2])
        )
        results.append(
            {
                "experiment": "atomicity",
                "case": case["name"],
                "sql_bytes": len(sql.encode("utf-8")),
                "execute_ok": ok,
                "execute_result": info,
                "counts": counts,
                "good_subtables_written": good_written,
                "verdict": (
                    "all-or-nothing"
                    if (not ok and not good_written)
                    else "partial-write" if (not ok and good_written)
                    else "all-success" if ok else "unknown"
                ),
            }
        )
    return results


def _probe_boundary(probe, lo_start: int, hard_cap: int) -> dict:
    """倍增探到首个失败值，再二分定位最后成功值。probe(n) → (ok, detail)。"""
    history = []
    # 倍增找上界
    n = lo_start
    last_ok = 0
    first_fail = None
    while n <= hard_cap:
        ok, detail = probe(n)
        history.append({"n": n, "ok": ok, **detail})
        if ok:
            last_ok = n
            n *= 2
        else:
            first_fail = n
            break
    if first_fail is None:
        return {"max_ok": last_ok, "first_fail": None, "note": f"hit hard_cap {hard_cap} without failing", "history": history}
    # 二分 last_ok..first_fail
    lo, hi = last_ok, first_fail
    while hi - lo > 1:
        mid = (lo + hi) // 2
        ok, detail = probe(mid)
        history.append({"n": mid, "ok": ok, **detail})
        if ok:
            lo = mid
        else:
            hi = mid
    return {"max_ok": lo, "first_fail": hi, "history": history}


def exp_table_count(td: TD, db: str, base_ts: int) -> dict:
    """实验 C：单条 INSERT 的子表数上限（每子表 1 行）。"""
    counter = {"i": 0}

    def probe(n: int):
        counter["i"] += 1
        # 每轮用独立子表前缀，避免重复 ts 覆盖影响计数
        prefix = f"expC_{counter['i']}_"
        clauses = [
            _subtable_clause(db, f"{prefix}{j}", base_ts + j, ["1.0"])
            for j in range(n)
        ]
        sql = _build_multi(db, clauses)
        ok, info = td.execute(sql)
        return ok, {"sql_bytes": len(sql.encode("utf-8")), "result": info if not ok else "ok"}

    out = _probe_boundary(probe, lo_start=50, hard_cap=50000)
    out["experiment"] = "max_subtables_per_statement"
    return out


def exp_sql_bytes(td: TD, db: str, base_ts: int) -> dict:
    """实验 D：单子表单条 INSERT 的行数/字节上限（找 SQL 字节阈值）。"""
    counter = {"i": 0}

    def probe(n: int):
        counter["i"] += 1
        table = f"expD_{counter['i']}"
        clauses = [
            f"{_quote(db)}.{_quote(table)} USING {_quote(db)}.{_quote(STABLE)} "
            f"TAGS ('{table}') ({_quote('ts')}, {_quote('c1')}) VALUES "
            + " ".join(f"({base_ts + k}, {k % 1000}.5)" for k in range(n))
        ]
        sql = "INSERT INTO " + clauses[0]
        ok, info = td.execute(sql)
        return ok, {"rows": n, "sql_bytes": len(sql.encode("utf-8")), "result": info if not ok else "ok"}

    out = _probe_boundary(probe, lo_start=1000, hard_cap=4_000_000)
    out["experiment"] = "max_rows_bytes_single_subtable"
    return out


# ---------------------------------------------------------------------------
# 主流程
# ---------------------------------------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser(description="TDengine 多表 INSERT 实验（§7.6 前置）")
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=6041)
    ap.add_argument("--user", default="root")
    ap.add_argument("--password", default="taosdata")
    ap.add_argument("--database", default="perf_exp_multitable")
    ap.add_argument("--output", default="", help="结果 JSON 归档目录；留空只打印")
    ap.add_argument("--keep", action="store_true", help="结束后保留实验库便于人工核查")
    args = ap.parse_args()

    dsn = f"ws://{quote_plus(args.user)}:{quote_plus(args.password)}@{args.host}:{args.port}"
    td = TD(dsn)

    db = args.database
    report: dict = {
        "started_at": _now_iso(),
        "env": {
            "taosws_version": __import__("taosws").__version__ if hasattr(__import__("taosws"), "__version__") else "?",
            "server_version": td.query_scalar("SELECT SERVER_VERSION()"),
            "python": sys.version.split()[0],
            "git_commit": _git(["rev-parse", "HEAD"]),
            "git_dirty": bool(_git(["status", "--short"])),
            "host": f"{args.host}:{args.port}",
            "database": db,
        },
        "experiments": [],
    }

    try:
        # 生产同款 KEEP 14d / DURATION 1d：有效 ts（≈now）在窗口内，坏用例 B 的 100 天前 ts 越界。
        td.execute(f"DROP DATABASE IF EXISTS {_quote(db)}")
        ok, info = td.execute(f"CREATE DATABASE {_quote(db)} KEEP 14d DURATION 1d")
        if not ok:
            raise RuntimeError(f"create database failed: {info}")
        ok, info = td.execute(_stable_ddl(db))
        if not ok:
            raise RuntimeError(f"create stable failed: {info}")

        base_ts = int(time.time() * 1000)

        report["experiments"].extend(exp_atomicity(td, db, base_ts))
        report["experiments"].append(exp_table_count(td, db, base_ts + 100_000))
        report["experiments"].append(exp_sql_bytes(td, db, base_ts + 5_000_000))
    finally:
        if not args.keep:
            td.execute(f"DROP DATABASE IF EXISTS {_quote(db)}")
        report["finished_at"] = _now_iso()
        td.close()

    # 摘要
    print("=" * 72)
    print(f"TDengine 多表 INSERT 实验  server={report['env']['server_version']}")
    print("=" * 72)
    for e in report["experiments"]:
        exp = e.get("experiment")
        if exp == "atomicity":
            print(
                f"[原子性/{e['case']}] execute_ok={e['execute_ok']} 判定={e['verdict']}\n"
                f"    counts={e['counts']}\n"
                f"    error={e['execute_result'] if not e['execute_ok'] else '-'}"
            )
        elif exp == "max_subtables_per_statement":
            print(
                f"[子表数上限] 最大成功={e['max_ok']} 首个失败={e.get('first_fail')}"
            )
            ff = [h for h in e["history"] if not h["ok"]]
            if ff:
                print(f"    失败样例: n={ff[0]['n']} sql_bytes={ff[0]['sql_bytes']} err={ff[0]['result']}")
        elif exp == "max_rows_bytes_single_subtable":
            print(
                f"[单子表行/字节上限] 最大成功行={e['max_ok']} 首个失败={e.get('first_fail')}"
            )
            oks = [h for h in e["history"] if h["ok"]]
            if oks:
                print(f"    最大成功 SQL 字节≈{max(h['sql_bytes'] for h in oks)}")
            ff = [h for h in e["history"] if not h["ok"]]
            if ff:
                print(f"    失败样例: rows={ff[0]['rows']} sql_bytes={ff[0]['sql_bytes']} err={ff[0]['result']}")

    if args.output:
        os.makedirs(args.output, exist_ok=True)
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        path = os.path.join(args.output, f"tdengine-multitable-{stamp}.json")
        with open(path, "w", encoding="utf-8") as f:
            json.dump(report, f, ensure_ascii=False, indent=2, default=str)
        print(f"\n原始结果已归档: {path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
