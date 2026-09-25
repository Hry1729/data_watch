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
_TABLE_V2_MIGRATING = _TABLE_V2.replace(
    "collector_rejected_gaps", "collector_rejected_gaps__v2")

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
        if "contiguous_from_ts_ms" in columns and "run_id" in columns:
            # v2 早期形态（缺 collection_resumed_at 等新列）：补列即可
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
            self._migrate_v1_to_v2(conn)
            return
        raise RuntimeError(
            "collector_rejected_gaps has an unrecognised structure: "
            f"{sorted(columns)}")

    def _migrate_v1_to_v2(self, conn: sqlite3.Connection) -> None:
        """v1 → v2：新建 v2 表、搬迁全部历史、替换旧表。

        旧记录没有运行会话信息，标为 legacy-import / unknown；
        不能伪造成"本次运行"，否则历史缺口会被当成当前告警。

        实现注意：`executescript` 会先隐式提交，因此**不能**在它前后用显式
        BEGIN/COMMIT 包住（会报 "cannot commit - no transaction is active"）。
        这里改用"建新表 → 迁移 → 换名"的顺序，任何一步失败都不会丢掉旧表。
        """
        logger.warning("migrating collector rejection gap ledger v1 -> v2 at %s", self.path)
        conn.execute("DROP TABLE IF EXISTS collector_rejected_gaps__v2")
        conn.executescript(_TABLE_V2_MIGRATING)
        conn.execute(
            """INSERT INTO collector_rejected_gaps__v2 (
                   run_id, session_id, device_code, group_code, device_type,
                   scan_rate_ms, reason, first_ts_ms, last_ts_ms,
                   contiguous_from_ts_ms, frames, first_rejected_at,
                   last_rejected_at, collection_resumed_at, backfilled_at,
                   resolved_at)
               SELECT ?, ?, device_code, group_code, device_type, scan_rate_ms,
                      reason, first_ts_ms, last_ts_ms, first_ts_ms, frames,
                      first_rejected_at, last_rejected_at, NULL, NULL, NULL
               FROM collector_rejected_gaps""",
            (LEGACY_RUN_ID, UNKNOWN_SESSION_ID),
        )
        migrated = conn.execute(
            "SELECT COUNT(*) FROM collector_rejected_gaps__v2").fetchone()[0]
        conn.execute("DROP TABLE collector_rejected_gaps")
        conn.execute("ALTER TABLE collector_rejected_gaps__v2 "
                     "RENAME TO collector_rejected_gaps")
        conn.executescript(_INDEXES_V2)
        conn.execute(f"PRAGMA user_version={SCHEMA_VERSION}")
        logger.warning("migrated %d legacy rejection gap row(s) to v2", migrated)

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
