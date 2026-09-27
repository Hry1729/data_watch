"""采集端拒收缺口台账（独立 SQLite）。

为什么独立文件
--------------
主 outbox 触顶拒收的原因正是它自己写不下；把缺口记在同一个库里必然跟着写失败，
等于没有记录。但它只能隔离主 SQLite 的容量限制，**不能**隔离整块磁盘写满或损坏。

持久性边界
----------
本表用 WAL + synchronous=NORMAL：进程崩溃或掉电后最近已提交事务仍可能回退，
因此它是"尽力而为的审计线索"，不是强持久审计记录。官方对 durable 与
非 durable 事务的差别有明确说明。

生命周期
--------
**跨重启保留**。缺口是历史事实，而重启 Collector 恰恰是处理故障的常见动作——
用"清空旧证据"来避免把旧缺口当成当前缺口，等于把审计线索删掉。
当前/历史靠 `run_id` 区分，容量靠归档与保留策略控制。

状态语义（刻意分开，不能互相顶替）
----------------------------------
* `collection_resumed_at`：采集是否已恢复（不再拒收）
* `backfilled_at`：缺失数据是否已补齐
* `resolved_at`：是否已由人员确认处置
**采集恢复不等于数据已补齐**，因此不允许用前者自动置上后者。

连续性
------
多次拒收不合并成"首次—末次"一个大区间：同一 (run, 设备, 分组, 周期, 原因) 下，
两次拒收的采样时间间隔超过 `contiguity_tolerance_ms` 就视为**两个缺口事件**。
当前只有采样时间戳可用，因此这是**时间推断**，不能声称精确知道每一帧是否缺失；
若将来能拿到采集任务内的采样序号，应改用它判定连续性。
"""
from __future__ import annotations

import logging
import sqlite3
import threading
import time
from pathlib import Path
from typing import Any

logger = logging.getLogger("collector.reject_gap_log")

# 台账结构版本。用于显式迁移：旧版本建的表不会被 CREATE TABLE IF NOT EXISTS 改结构，
# 必须先检测再迁移，否则新增列缺失会让初始化直接失败。
SCHEMA_VERSION = 2

# 旧版（v1，2026-09-25 首版）表结构：没有 run_id / session_id / resolved_at，
# 主键也不同。升级时必须显式迁移，不能靠 IF NOT EXISTS。
_V1_COLUMNS = {"device_code", "group_code", "device_type", "scan_rate_ms", "reason",
               "first_ts_ms", "last_ts_ms", "frames", "first_rejected_at",
               "last_rejected_at"}
_V2_COLUMNS = {"run_id", "session_id", "device_code", "group_code", "device_type",
               "scan_rate_ms", "reason", "first_ts_ms", "last_ts_ms", "frames",
               "first_rejected_at", "last_rejected_at", "contiguous_from_ts_ms",
               "gap_id", "collection_resumed_at", "backfilled_at", "resolved_at"}

# 旧记录没有运行会话信息：必须标成历史导入/未知，不能伪造成本次运行。
LEGACY_RUN_ID = "legacy-import"
UNKNOWN_SESSION_ID = "unknown"

_TABLE_V2 = """
CREATE TABLE IF NOT EXISTS collector_rejected_gaps (
    gap_id INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id TEXT NOT NULL,
    session_id TEXT NOT NULL,
    device_code TEXT NOT NULL,
    group_code TEXT NOT NULL,
    device_type TEXT NOT NULL,
    scan_rate_ms INTEGER NOT NULL,
    reason TEXT NOT NULL,
    first_ts_ms INTEGER NOT NULL,
    last_ts_ms INTEGER NOT NULL,
    contiguous_from_ts_ms INTEGER NOT NULL,
    frames INTEGER NOT NULL,
    first_rejected_at INTEGER NOT NULL,
    last_rejected_at INTEGER NOT NULL,
    collection_resumed_at INTEGER,
    backfilled_at INTEGER,
    resolved_at INTEGER
);
"""
# 迁移时用的临时表名：CREATE TABLE IF NOT EXISTS 不支持换名，这里手工替换。
# 迁移时的临时表名。迁移被中断（DROP 旧表之后、RENAME 之前崩溃）会留下它，
# 启动时必须先识别这种中间态，否则会新建空表、把历史记录遗留在临时表里。
_MIGRATING_TABLE = "collector_rejected_gaps__migrating"
_TABLE_V2_MIGRATING = _TABLE_V2.replace(
    "collector_rejected_gaps", _MIGRATING_TABLE)
_REBUILD_TABLE = "collector_rejected_gaps__rebuild"
# 历史版本真实用过的临时表名：4d86b6d 的 v1→v2 迁移把历史整表搬到
# `collector_rejected_gaps__v2`，然后 `DROP 正式表` + `RENAME __v2 → 正式表`。
# 那一版**没有**事务，两条语句之间崩溃就会留下"正式表不存在、__v2 有全部历史"，
# 而 __v2 的结构与正式 v2 表完全相同。不认识这个名字就等于把历史丢在临时表里。
_LEGACY_V2_TABLE = "collector_rejected_gaps__v2"
# 启动时必须扫描的残留临时表（新旧真实用过的名字都要覆盖）。
_LEFTOVER_TABLES = (_MIGRATING_TABLE, _LEGACY_V2_TABLE, _REBUILD_TABLE)

_INDEXES_V2 = """
CREATE INDEX IF NOT EXISTS idx_rejected_run ON collector_rejected_gaps(run_id);
CREATE INDEX IF NOT EXISTS idx_rejected_open
    ON collector_rejected_gaps(run_id, device_code, group_code, scan_rate_ms, reason,
                                collection_resumed_at);
"""


class RejectGapLogUnavailable(RuntimeError):
    """台账不可用。调用方必须把它与"没有缺口"区分开。"""


def _columns(conn: sqlite3.Connection, table: str) -> set[str]:
    rows = conn.execute(f"PRAGMA table_info({table})").fetchall()
    return {row[1] for row in rows}


class RejectGapLog:
    """拒收缺口台账。所有写入串行化；连接允许跨线程（拒收来自多个线程）。"""

    def __init__(self, path: str | Path, *, run_id: str, session_id: str,
                 contiguity_tolerance_ms: int = 0) -> None:
        self.path = Path(path)
        self.run_id = run_id
        self.session_id = session_id
        # 0 表示"只按采样周期推断"：实际容差在写入时按 scan_rate_ms 计算。
        self._contiguity_tolerance_ms = contiguity_tolerance_ms
        self._lock = threading.Lock()
        self._conn: sqlite3.Connection | None = None
        self._error: str | None = None
        try:
            self.path.parent.mkdir(parents=True, exist_ok=True)
            conn = sqlite3.connect(self.path, timeout=5, isolation_level=None,
                                   check_same_thread=False)
            conn.row_factory = sqlite3.Row
            conn.execute("PRAGMA journal_mode=WAL")
            conn.execute("PRAGMA synchronous=NORMAL")
            self._init_schema(conn)
            self._conn = conn
        except Exception as exc:
            # 保留原文件，不删除、不覆盖；把原因留成显式状态供上层报出。
            self._error = f"{type(exc).__name__}: {exc}"
            logger.exception("collector rejection gap log unavailable at %s", self.path)

    # ------------------------------------------------------------------
    # 状态
    # ------------------------------------------------------------------
    @property
    def available(self) -> bool:
        return self._conn is not None

    def status(self) -> dict[str, Any]:
        """显式健康状态：台账不可用绝不能呈现为"没有缺口"。"""
        version = None
        count = None
        if self._conn is not None:
            try:
                version = self._conn.execute("PRAGMA user_version").fetchone()[0]
                count = self._conn.execute(
                    "SELECT COUNT(*) FROM collector_rejected_gaps").fetchone()[0]
            except Exception:
                logger.exception("collector rejection gap status read failed")
        return {
            "available": self.available,
            "error": self._error,
            "path": str(self.path),
            "schema_version": version,
            "gap_rows_total": count,
            "run_id": self.run_id,
        }

    def _require(self) -> sqlite3.Connection:
        if self._conn is None:
            raise RejectGapLogUnavailable(
                f"rejection gap ledger unavailable at {self.path}: {self._error}")
        return self._conn

    # ------------------------------------------------------------------
    # 结构初始化与迁移
    # ------------------------------------------------------------------
    def _init_schema(self, conn: sqlite3.Connection) -> None:
        # 0) 先处理"迁移中断"的残留：上次迁移在 DROP 旧表之后、RENAME 之前崩溃，
        #    会留下 __migrating 临时表而正式表不存在。若此时直接建新表，
        #    历史记录就被遗留在临时表里，正常查询看不到——正是要避免的错误状态。
        self._recover_interrupted_migration(conn)

        existing = conn.execute(
            "SELECT name FROM sqlite_master WHERE type='table' "
            "AND name='collector_rejected_gaps'").fetchone()
        if existing is None:
            conn.executescript(_TABLE_V2)
            conn.executescript(_INDEXES_V2)
            conn.execute(f"PRAGMA user_version={SCHEMA_VERSION}")
            return
        columns = _columns(conn, "collector_rejected_gaps")
        if columns >= _V2_COLUMNS:
            conn.executescript(_TABLE_V2)  # 补索引
            conn.executescript(_INDEXES_V2)
            conn.execute(f"PRAGMA user_version={SCHEMA_VERSION}")
            return
        if "contiguous_from_ts_ms" in columns:
            # 已有连续区间字段，只是缺后来的状态列：补列即可，不动数据。
            for name, ddl in (("collection_resumed_at", "INTEGER"),
                              ("backfilled_at", "INTEGER"),
                              ("resolved_at", "INTEGER")):
                if name not in columns:
                    conn.execute(
                        f"ALTER TABLE collector_rejected_gaps ADD COLUMN {name} {ddl}")
            conn.executescript(_INDEXES_V2)
            conn.execute(f"PRAGMA user_version={SCHEMA_VERSION}")
            return
        if columns >= _V1_COLUMNS:
            # v1（无 run_id）与"有 run_id/session_id/resolved_at 但无连续区间字段"的
            # 中间版本都落在这里。后者已有的真实元数据必须保留，只有旧表真正缺少的
            # 信息才补"未知"。
            self._migrate_to_v2(conn, columns)
            return
        raise RuntimeError(
            "collector_rejected_gaps has an unrecognised structure: "
            f"{sorted(columns)}")

    def _table_exists(self, conn: sqlite3.Connection, name: str) -> bool:
        return conn.execute(
            "SELECT 1 FROM sqlite_master WHERE type='table' AND name=?",
            (name,)).fetchone() is not None

    def _recover_interrupted_migration(self, conn: sqlite3.Connection) -> None:
        """处理上次迁移中断的残留（含历史版本用过的临时表名）。

        现场分析（正式表 = collector_rejected_gaps）：
          * 正式表不存在、只有 1 张残留表 → 上次的换表没有提交，残留表里是全部
            历史。把它**重建成**正式表，而不是新建空表：新建空表会让正常查询
            看不到历史，等于"恢复成功"的假象。
            不能直接 RENAME：残留表可能是不完整结构，改名会把缺列的表变成正式表。
          * 正式表不存在、有 2 张以上**含数据**的残留表 → 无法判断哪张才是权威
            副本。保留全部数据并显式报错（台账变为不可用），绝不挑一张、更不新建
            空表。空残留表没有数据可保，作为换表残渣清理并记录。
          * 正式表存在、残留表行数 > 正式表行数 → 残留表可能有正式表没有的历史。
            不删除，显式报错并把两张表的行数报告出来。
          * 正式表存在、残留表**每一行都能在正式表里按内容找到**（含重数）→ 正式表
            确实包含了残留表的全部内容，丢弃残留表并记录行数。
            行数少**不足以**证明这一点：正式表 1 条 A、残留 1 条 B，或残留里有正式表
            没有的少数几条，都必须保留并报错。所以这里做的是**内容覆盖证明**，
            不是行数比较。
        """
        leftovers = self._leftover_tables(conn)
        if not leftovers:
            return
        # 空表没有任何数据可保，属于换表过程的残渣，不参与"哪张是权威副本"的判断。
        nonempty = {name: rows for name, rows in leftovers.items() if rows > 0}
        official_exists = self._table_exists(conn, "collector_rejected_gaps")
        if not official_exists:
            if len(nonempty) > 1:
                raise RuntimeError(
                    "ambiguous rejection-gap recovery at "
                    f"{self.path}: official table is missing but multiple leftover "
                    f"tables contain rows {nonempty}; refusing to pick one or to create "
                    "an empty official table. Original data is preserved for manual review."
                )
            source = None
            if nonempty:
                source, rows = next(iter(nonempty.items()))
                logger.warning(
                    "recovering interrupted rejection-gap migration at %s: rebuilding "
                    "official table from %s (%d row(s))", self.path, source, rows)
                self._rebuild_from(conn, source)
            for name in sorted(leftovers):
                if name != source:
                    logger.warning(
                        "discarding empty leftover %s at %s", name, self.path)
                    conn.execute(f"DROP TABLE IF EXISTS {name}")
            if source is not None:
                conn.execute(f"DROP TABLE IF EXISTS {source}")
            return

        official_rows = conn.execute(
            "SELECT COUNT(*) FROM collector_rejected_gaps").fetchone()[0]
        official_columns = _columns(conn, "collector_rejected_gaps")
        conflicting = {name: rows for name, rows in nonempty.items()
                       if rows > official_rows}
        if conflicting:
            raise RuntimeError(
                "conflicting rejection-gap ledgers at "
                f"{self.path}: official table has {official_rows} row(s) but leftover "
                f"tables hold more {conflicting}; refusing to delete data. "
                "Original tables are preserved for manual review."
            )
        for name, rows in sorted(leftovers.items()):
            if rows:
                covered, reason = self._content_covered_by_official(
                    conn, name, official_columns)
                if not covered:
                    raise RuntimeError(
                        "conflicting rejection-gap ledgers at "
                        f"{self.path}: leftover table {name} holds {rows} row(s) not "
                        f"provably contained in the official table {reason}; refusing "
                        "to delete data. Original tables are preserved for manual review."
                    )
            logger.warning(
                "discarding leftover %s (%d row(s), official table has %d) at %s: "
                "official table is authoritative", name, rows, official_rows, self.path)
            conn.execute(f"DROP TABLE IF EXISTS {name}")

    def _content_covered_by_official(self, conn: sqlite3.Connection, leftover: str,
                                     official_columns: set[str]) -> tuple[bool, str]:
        """残留表的每一行（按重数）是否都能在正式表里找到。

        残留表有正式表缺少的列时，其中的历史元数据无法证明已覆盖，拒绝删除。
        否则按**共同列**全等比较（`IS` 是 SQLite 的 NULL 安全等值），
        正式表多出来的列不参与比较。没有共同列也按"未覆盖"处理。
        """
        leftover_columns = _columns(conn, leftover)
        extra_columns = sorted(leftover_columns - official_columns)
        if extra_columns:
            return False, f"(extra columns only in leftover: {', '.join(extra_columns)})"
        shared = sorted(leftover_columns & official_columns)
        if not shared:
            return False, "（两张表没有可比较的共同列）"
        columns = ", ".join(shared)
        match = " AND ".join(f"o.{name} IS l.{name}" for name in shared)
        uncovered = conn.execute(
            f"SELECT COUNT(*) FROM (SELECT {columns}, COUNT(*) AS copies "
            f"FROM {leftover} GROUP BY {columns}) AS l "
            f"WHERE l.copies > (SELECT COUNT(*) FROM collector_rejected_gaps AS o "
            f"WHERE {match})"
        ).fetchone()[0]
        if uncovered:
            return False, f"（有 {uncovered} 组行的重数超过正式表）"
        return True, ""

    def _leftover_tables(self, conn: sqlite3.Connection) -> dict[str, int]:
        """启动时存在的残留临时表 → 行数（新旧真实用过的名字都扫描）。"""
        found: dict[str, int] = {}
        for name in _LEFTOVER_TABLES:
            if self._table_exists(conn, name):
                found[name] = conn.execute(
                    f"SELECT COUNT(*) FROM {name}").fetchone()[0]
        return found

    def _swap_in(self, conn: sqlite3.Connection, source_table: str,
                 *, old_table: str | None) -> None:
        """把 source_table 换名为正式表；DROP 旧表与 RENAME 在同一事务内。

        `executescript` 会隐式提交，所以这里全部用单条 execute()。
        若连接已在事务中（例如调用方自己开了事务做失败注入），不再嵌套 BEGIN，
        直接执行并让调用方决定提交或回滚——SQLite 的 DDL 在事务内是可回滚的。
        """
        owns_txn = not conn.in_transaction
        if owns_txn:
            conn.execute("BEGIN IMMEDIATE")
        try:
            if old_table is not None:
                conn.execute(f"DROP TABLE {old_table}")
            conn.execute(f"ALTER TABLE {source_table} RENAME TO collector_rejected_gaps")
            if owns_txn:
                conn.execute("COMMIT")
        except BaseException:
            if owns_txn:
                try:
                    conn.execute("ROLLBACK")
                except Exception:
                    logger.exception("gap ledger swap rollback failed")
            raise
        conn.executescript(_INDEXES_V2)
        conn.execute(f"PRAGMA user_version={SCHEMA_VERSION}")

    def _rebuild_from(self, conn: sqlite3.Connection, source_table: str) -> None:
        """把 source_table 的数据重建成规范 v2 正式表（缺失列补未知）。

        source_table 本身就是残留临时表（`__v2` / `__migrating` / `__rebuild`），
        所以重建目标必须换成**另一张**临时表名：沿用固定名字会在建表前
        把正在读的源表 DROP 掉，历史数据当场丢失。
        两张名字都在 _LEFTOVER_TABLES 里，任何中断残留下次启动都能被识别。
        """
        columns = _columns(conn, source_table)
        target = _REBUILD_TABLE if source_table != _REBUILD_TABLE else _MIGRATING_TABLE
        conn.execute(f"DROP TABLE IF EXISTS {target}")
        conn.execute(_TABLE_V2.replace("collector_rejected_gaps", target))
        select_cols: list[str] = []
        for name in ("run_id", "session_id"):
            select_cols.append(name if name in columns else "?")
        for name in ("device_code", "group_code", "device_type", "scan_rate_ms", "reason",
                     "first_ts_ms", "last_ts_ms"):
            if name not in columns:
                raise RuntimeError(
                    f"cannot rebuild gap ledger from {source_table}: missing {name}")
            select_cols.append(name)
        select_cols.append("contiguous_from_ts_ms" if "contiguous_from_ts_ms" in columns
                           else "first_ts_ms")
        for name in ("frames", "first_rejected_at", "last_rejected_at"):
            if name not in columns:
                raise RuntimeError(
                    f"cannot rebuild gap ledger from {source_table}: missing {name}")
            select_cols.append(name)
        for name in ("collection_resumed_at", "backfilled_at", "resolved_at"):
            select_cols.append(name if name in columns else "NULL")
        params: tuple = () if "run_id" in columns else (LEGACY_RUN_ID,)
        if "session_id" not in columns:
            params = params + (UNKNOWN_SESSION_ID,)
        conn.execute(
            f"INSERT INTO {target} ("
            "run_id, session_id, device_code, group_code, device_type, scan_rate_ms,"
            " reason, first_ts_ms, last_ts_ms, contiguous_from_ts_ms, frames,"
            " first_rejected_at, last_rejected_at, collection_resumed_at,"
            " backfilled_at, resolved_at"
            f") SELECT {', '.join(select_cols)} FROM {source_table}",
            params,
        )
        migrated = conn.execute(
            f"SELECT COUNT(*) FROM {target}").fetchone()[0]
        self._swap_in(conn, target, old_table=None)
        logger.warning("rebuilt %d rejection gap row(s) into the official table", migrated)

    def _migrate_to_v2(self, conn: sqlite3.Connection, columns: set[str]) -> None:
        """任意旧结构 → v2。整体事务化，且保留旧表已有的真实元数据。

        旧表真正缺少的信息才补"未知"（legacy-import / unknown / NULL）；
        已经存在的 run_id、session_id、处置状态必须原样保留，
        否则会把真实运行会话覆盖成"未知"，历史缺口也无法按运行追溯。

        事务边界：`executescript` 会隐式提交，所以**全部用单条 execute()**，
        把"DROP 旧表 + RENAME 临时表"包在同一个 BEGIN IMMEDIATE / COMMIT 内。
        提交前正式表始终是旧的，中断最多留下 __migrating 残留，
        由 _recover_interrupted_migration 处理。
        """
        logger.warning("migrating collector rejection gap ledger to v2 at %s "
                       "(source columns: %s)", self.path, sorted(columns))
        has = columns.__contains__
        run_expr = "run_id" if has("run_id") else None
        session_expr = "session_id" if has("session_id") else None
        contiguous_expr = "contiguous_from_ts_ms" if has("contiguous_from_ts_ms") else None
        resumed_expr = "collection_resumed_at" if has("collection_resumed_at") else None
        backfilled_expr = "backfilled_at" if has("backfilled_at") else None
        resolved_expr = "resolved_at" if has("resolved_at") else None

        select_cols = []
        params: list[object] = []
        for expr, fallback in (
            (run_expr, LEGACY_RUN_ID), (session_expr, UNKNOWN_SESSION_ID),
        ):
            if expr is None:
                select_cols.append("?")
                params.append(fallback)
            else:
                select_cols.append(expr)
        select_cols += ["device_code", "group_code", "device_type", "scan_rate_ms",
                        "reason", "first_ts_ms", "last_ts_ms"]
        if contiguous_expr is None:
            select_cols.append("first_ts_ms")       # 旧表没有连续起点，用首帧时间
        else:
            select_cols.append(contiguous_expr)
        select_cols += ["frames", "first_rejected_at", "last_rejected_at"]
        for expr in (resumed_expr, backfilled_expr, resolved_expr):
            select_cols.append(expr if expr is not None else "NULL")

        conn.execute(f"DROP TABLE IF EXISTS {_MIGRATING_TABLE}")
        conn.execute(_TABLE_V2_MIGRATING)           # 建临时新表（自动提交）
        insert_sql = (
            f"INSERT INTO {_MIGRATING_TABLE} ("
            "run_id, session_id, device_code, group_code, device_type, scan_rate_ms,"
            " reason, first_ts_ms, last_ts_ms, contiguous_from_ts_ms, frames,"
            " first_rejected_at, last_rejected_at, collection_resumed_at,"
            " backfilled_at, resolved_at"
            f") SELECT {', '.join(select_cols)} FROM collector_rejected_gaps"
        )
        conn.execute(insert_sql, tuple(params))
        migrated = conn.execute(
            f"SELECT COUNT(*) FROM {_MIGRATING_TABLE}").fetchone()[0]

        # 关键：换表整体事务化。DROP 之后如果崩溃，事务回滚，正式表仍是旧的。
        self._swap_in(conn, _MIGRATING_TABLE, old_table="collector_rejected_gaps")
        logger.warning("migrated %d rejection gap row(s) to v2", migrated)

    # ------------------------------------------------------------------
    # 写入
    # ------------------------------------------------------------------
    def record_rejection(self, *, device_code: str, group_code: str, device_type: str,
                         scan_rate_ms: int, reason: str, ts_ms: int,
                         now_ms: int | None = None) -> None:
        """记录一次拒收。连续拒收合并为一个缺口事件，不连续则新开一段。"""
        conn = self._require()
        now_ms = int(time.time() * 1000) if now_ms is None else now_ms
        # 容忍抖动：间隔不超过一个多采样周期视为同一段连续拒收。
        tolerance = self._contiguity_tolerance_ms or max(1, int(scan_rate_ms)) * 3 // 2
        try:
            with self._lock:
                open_gap = conn.execute(
                    """SELECT gap_id, last_ts_ms, contiguous_from_ts_ms, frames
                       FROM collector_rejected_gaps
                       WHERE run_id=? AND device_code=? AND group_code=?
                         AND scan_rate_ms=? AND reason=?
                         AND collection_resumed_at IS NULL
                       ORDER BY last_rejected_at DESC LIMIT 1""",
                    (self.run_id, device_code, group_code, scan_rate_ms, reason),
                ).fetchone()
                contiguous = (
                    open_gap is not None
                    and 0 <= (ts_ms - open_gap["last_ts_ms"]) <= tolerance
                )
                if contiguous:
                    conn.execute(
                        """UPDATE collector_rejected_gaps
                           SET last_ts_ms=MAX(last_ts_ms, ?), frames=frames+1,
                               last_rejected_at=?
                           WHERE gap_id=?""",
                        (ts_ms, now_ms, open_gap["gap_id"]),
                    )
                else:
                    conn.execute(
                        """INSERT INTO collector_rejected_gaps (
                               run_id, session_id, device_code, group_code, device_type,
                               scan_rate_ms, reason, first_ts_ms, last_ts_ms,
                               contiguous_from_ts_ms, frames, first_rejected_at,
                               last_rejected_at, collection_resumed_at, backfilled_at,
                               resolved_at)
                           VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1, ?, ?, NULL, NULL, NULL)""",
                        (self.run_id, self.session_id, device_code, group_code,
                         device_type, scan_rate_ms, reason, ts_ms, ts_ms, ts_ms,
                         now_ms, now_ms),
                    )
        except Exception:
            logger.exception("collector rejection gap record failed")

    def has_open_gap(self, *, device_code: str, group_code: str,
                     scan_rate_ms: int) -> bool:
        """该组是否还有"仍在扩大"的缺口（未标记采集恢复）。"""
        conn = self._conn
        if conn is None:
            return False
        try:
            row = conn.execute(
                """SELECT 1 FROM collector_rejected_gaps
                   WHERE run_id=? AND device_code=? AND group_code=? AND scan_rate_ms=?
                     AND collection_resumed_at IS NULL LIMIT 1""",
                (self.run_id, device_code, group_code, scan_rate_ms)).fetchone()
        except Exception:
            logger.exception("collector rejection gap open check failed")
            return False
        return row is not None

    def mark_collection_resumed(self, *, device_code: str, group_code: str,
                               scan_rate_ms: int, now_ms: int | None = None) -> None:
        """该组已能重新接收帧：关闭"仍在扩大"，但**不**表示缺失数据已补齐。"""
        conn = self._conn
        if conn is None:
            return
        now_ms = int(time.time() * 1000) if now_ms is None else now_ms
        try:
            with self._lock:
                conn.execute(
                    """UPDATE collector_rejected_gaps
                       SET collection_resumed_at=?
                       WHERE run_id=? AND device_code=? AND group_code=?
                         AND scan_rate_ms=? AND collection_resumed_at IS NULL""",
                    (now_ms, self.run_id, device_code, group_code, scan_rate_ms),
                )
        except Exception:
            logger.exception("collector rejection gap resume mark failed")

    def mark_backfilled(self, *, gap_id: int, now_ms: int | None = None) -> None:
        """缺失数据已被补齐（由补传/人工确认驱动，不由采集恢复自动触发）。"""
        conn = self._require()
        now_ms = int(time.time() * 1000) if now_ms is None else now_ms
        with self._lock:
            conn.execute(
                "UPDATE collector_rejected_gaps SET backfilled_at=? WHERE gap_id=?",
                (now_ms, gap_id),
            )

    def mark_resolved(self, *, gap_id: int, now_ms: int | None = None) -> None:
        """已由人员确认处置完毕。"""
        conn = self._require()
        now_ms = int(time.time() * 1000) if now_ms is None else now_ms
        with self._lock:
            conn.execute(
                "UPDATE collector_rejected_gaps SET resolved_at=? WHERE gap_id=?",
                (now_ms, gap_id),
            )

    # ------------------------------------------------------------------
    # 查询
    # ------------------------------------------------------------------
    _SELECT = """SELECT gap_id, run_id, session_id, device_code, group_code,
                        device_type, scan_rate_ms, reason, first_ts_ms, last_ts_ms,
                        contiguous_from_ts_ms, frames, first_rejected_at,
                        last_rejected_at, collection_resumed_at, backfilled_at,
                        resolved_at
                 FROM collector_rejected_gaps"""

    def gaps(self, *, run_id: str | None = None,
             only_run: bool = True) -> list[dict[str, Any]]:
        """查询缺口。默认只返回本次运行；run_id 显式给出时查指定运行。

        台账不可用时**抛异常**，不返回空列表：空列表必须只表示"确实没有缺口"。
        """
        conn = self._require()
        target = run_id if run_id is not None else (self.run_id if only_run else None)
        try:
            if target is None:
                rows = conn.execute(
                    f"{self._SELECT} ORDER BY last_rejected_at DESC").fetchall()
            else:
                rows = conn.execute(
                    f"{self._SELECT} WHERE run_id=? ORDER BY last_rejected_at DESC",
                    (target,)).fetchall()
        except Exception:
            logger.exception("collector rejection gap read failed")
            raise
        return [dict(row) for row in rows]

    def runs(self) -> list[dict[str, Any]]:
        """所有有缺口记录的运行批次，区分当前告警与历史审计。"""
        conn = self._require()
        rows = conn.execute(
            """SELECT run_id, MIN(first_rejected_at) AS first_at,
                      MAX(last_rejected_at) AS last_at, SUM(frames) AS frames,
                      COUNT(*) AS gap_events
               FROM collector_rejected_gaps GROUP BY run_id
               ORDER BY last_at DESC"""
        ).fetchall()
        return [dict(row) for row in rows]

    def close(self) -> None:
        with self._lock:
            if self._conn is not None:
                try:
                    self._conn.close()
                finally:
                    self._conn = None
