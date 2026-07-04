"""TDengine batch writer.

Connects via WebSocket (taos-ws-py / taosws) matching the approach used in
collector/storage/tdengine.py.

write_multi_table() packs rows across many child tables into chunked multi-table
INSERT statements bounded by MAX_SQL_BYTES, returning per-chunk success so the
caller (consumer._flush) can ACK landed rows and retry the rest.

Row dict schema:
    {
        "child_table": str,   # e.g. qc100
        "stable":      str,   # e.g. sdqc
        "device_code": str,   # tag value, e.g. qc100
        "ts":          int,   # epoch milliseconds
        "columns":     dict[str, float],  # td_column_name → value
    }
"""

from __future__ import annotations

import asyncio
import logging
import math
import time
from typing import Any
from urllib.parse import quote_plus

logger = logging.getLogger("writer.tdengine")


def _quote(name: str) -> str:
    return f"`{name.replace('`', '``')}`"


_CONNECTION_ERROR_KEYWORDS = (
    "closed", "connection", "timeout", "timed out",
    "broken pipe", "eof", "reset", "refused", "unavailable",
)

_KNOWN_TAG_COLUMNS = {"device_code"}

# 单条 INSERT 的 SQL 字节上限。实测 TDengine 3.4.0.9 硬上限 ≈4 MiB（超出报
# 0x0219 SQL statement too long），取 ~3MB 留余量（scripts/experiment_tdengine_multitable.py）。
MAX_SQL_BYTES = 3 * 1024 * 1024

# write_multi_table 的行级结果状态（§7.6 二分降级）：
#   LANDED   已确认落库 → 调用方 XACK
#   BAD_DATA 明确坏数据，二分到单行仍失败 → 调用方进 DLQ
#   RETRY    连接错/未知错/二分超限 → 调用方留 buffer 重试（绝不进 DLQ）
LANDED = "landed"
BAD_DATA = "bad_data"
RETRY = "retry"

# 明确的"坏数据 / SQL"错误才二分，且只认 TDengine 的具体错误码：解析期坏值整条
# 拒绝(0x0216)、运行期逐行错如越界 ts(0x060b)、SQL 过长(0x0219)。**故意不匹配
# "invalid"/"out of range"/"syntax" 等英文词**——它们也出现在 0x80000112(配置越界)、
# 0x80000115~11E(协议/版本错) 等瞬时/非数据错误里，会把它们误判成坏数据送进 DLQ，
# 违背硬约束①。未命中（未知错误）一律 RETRY，绝不送 DLQ；真坏行最终经 delivery_count
# 超限走 _handle_entry 的 DLQ 兜底。
_DATA_ERROR_KEYWORDS = (
    "0x0216", "0x060b", "0x0219",
)

# 二分递归深度上限（防病态输入无限二分）。半切每层至少减半，正常 ceil(log2(行数))
# 即触底到单行；24 层覆盖 ~1600 万行，超限的极端块整体 RETRY 兜底、不强判坏行。
_MAX_BISECT_DEPTH = 24


def _is_connection_error(exc: Exception) -> bool:
    """Heuristic: does this exception indicate a dropped/unhealthy connection?"""
    msg = str(exc).lower()
    return any(kw in msg for kw in _CONNECTION_ERROR_KEYWORDS)


def _is_data_error(exc: Exception | None) -> bool:
    """White-list: is this a *definite* bad-data / over-long-SQL error?

    Only these are eligible for binary-search isolation. Anything else (including
    None / connection errors) is treated as transient and retried, never DLQ'd.
    """
    if exc is None:
        return False
    msg = str(exc).lower()
    return any(kw in msg for kw in _DATA_ERROR_KEYWORDS)


class TDengineWriter:
    def __init__(self, syslog=None) -> None:
        self._conn: Any = None
        self._database: str = "device_data"
        self._lock = asyncio.Lock()
        self._col_cache: dict[str, set[str]] = {}
        self._config: dict | None = None
        self._reconnecting: bool = False
        self._syslog = syslog
        # 节流：重连成功/失败在持续抖动下都可能每批触发，各自 ≥60s 限流
        self._last_reconnect_ok_log: float | None = None
        self._last_reconnect_fail_log: float | None = None

    # ------------------------------------------------------------------
    # Lifecycle
    # ------------------------------------------------------------------

    async def connect(self, config: dict) -> None:
        """Open a WebSocket connection to TDengine (runs taosws.connect in executor)."""
        from taosws import connect  # type: ignore[import]

        self._config = config
        td = config["tdengine"]
        self._database = td["database"]

        user = quote_plus(str(td["user"]))
        password = quote_plus(str(td["password"]))
        host = td["host"]
        port = td["port"]
        dsn = f"ws://{user}:{password}@{host}:{port}"

        loop = asyncio.get_running_loop()
        try:
            self._conn = await asyncio.wait_for(
                loop.run_in_executor(None, lambda: connect(dsn)),
                timeout=10,
            )
            logger.info("[tdengine] connected to %s:%s db=%s", host, port, self._database)
            await self._ensure_database()
            await self._preload_stables(config)
        except Exception as exc:
            if self._conn is not None:
                try:
                    self._conn.close()
                except Exception:
                    pass
            self._conn = None
            logger.error("[tdengine] connect failed: %s", exc)
            raise

    async def close(self) -> None:
        if self._conn is not None:
            try:
                loop = asyncio.get_running_loop()
                await loop.run_in_executor(None, self._conn.close)
            except Exception as exc:
                logger.warning("[tdengine] close error: %s", exc)
            finally:
                self._conn = None
                logger.info("[tdengine] disconnected")

    # ------------------------------------------------------------------
    # Query helpers (run blocking taosws calls in executor)
    # ------------------------------------------------------------------

    async def _execute_ex(self, sql: str) -> tuple[bool, Exception | None]:
        """Execute SQL, returning (ok, exception).

        Unlike _execute (bool), this surfaces the failure so the write path can
        classify it (connection vs bad-data vs unknown) and decide whether to
        retry the whole chunk or binary-search to isolate bad rows. Connection
        errors still trigger reconnect here, in one place.
        """
        if self._conn is None:
            logger.error("[tdengine] not connected, cannot execute SQL")
            if self._config is not None and not self._reconnecting:
                await self._try_reconnect()
            return False, None
        loop = asyncio.get_running_loop()
        try:
            await loop.run_in_executor(None, lambda: self._conn.execute(sql))
            return True, None
        except Exception as exc:
            logger.error("[tdengine] execute failed: %s | SQL: %.200s", exc, sql)
            if (
                self._config is not None
                and not self._reconnecting
                and _is_connection_error(exc)
            ):
                await self._try_reconnect()
            return False, exc

    async def _execute(self, sql: str) -> bool:
        ok, _ = await self._execute_ex(sql)
        return ok

    async def _query(self, sql: str) -> list[Any]:
        if self._conn is None:
            logger.error("[tdengine] not connected, cannot query SQL")
            return []
        loop = asyncio.get_running_loop()
        try:
            result = await loop.run_in_executor(None, lambda: self._conn.query(sql))
            return list(result) if result else []
        except Exception as exc:
            logger.error("[tdengine] query failed: %s | SQL: %.200s", exc, sql)
            return []

    async def _try_reconnect(self) -> None:
        """Re-establish the TDengine connection after a drop.

        Guarded by _reconnecting so the _execute calls inside connect()
        (via _ensure_database / _preload_stables) cannot recurse back here.
        """
        self._reconnecting = True
        logger.info("[tdengine] attempting reconnect...")
        try:
            if self._conn is not None:
                try:
                    self._conn.close()
                except Exception:
                    pass
                self._conn = None
            await self.connect(self._config)
            logger.info("[tdengine] reconnect succeeded")
            if self._syslog is not None:
                now = time.monotonic()
                if self._last_reconnect_ok_log is None or now - self._last_reconnect_ok_log >= 60.0:
                    self._last_reconnect_ok_log = now
                    self._syslog.emit(
                        level="WARN",
                        log_type="storage",
                        source="writer.tdengine",
                        content="tdengine reconnect succeeded",
                    )
        except Exception as exc:
            logger.error("[tdengine] reconnect failed: %s", exc)
            if self._syslog is not None:
                now = time.monotonic()
                if self._last_reconnect_fail_log is None or now - self._last_reconnect_fail_log >= 60.0:
                    self._last_reconnect_fail_log = now
                    self._syslog.emit(
                        level="ERROR",
                        log_type="storage",
                        source="writer.tdengine",
                        content="tdengine reconnect failed",
                    )
        finally:
            self._reconnecting = False

    # ------------------------------------------------------------------
    # Schema management
    # ------------------------------------------------------------------

    async def _ensure_database(self) -> None:
        # 保留期与磁盘预算挂钩：KEEP 14 天 + DURATION 1 天（按天分文件，过期精确到约 14~15 天）。
        # DURATION 仅首次建库生效、之后不可 ALTER；KEEP 可后续 ALTER。须与
        # scripts/init_tdengine_v2.py 及 deploy/taos.cfg(logKeepDays/minimalDataDirGB) 保持一致。
        sql = f"CREATE DATABASE IF NOT EXISTS {_quote(self._database)} KEEP 14d DURATION 1d"
        if not await self._execute(sql):
            raise RuntimeError(f"failed to ensure TDengine database: {self._database}")

    async def _preload_stables(self, config: dict) -> None:
        writer_config = config.get("writer", {})
        stable_names = writer_config.get("stable_names")
        if not stable_names:
            stable_names = [f"sd{device_type}" for device_type in writer_config.get("device_types", [])]

        for stable in stable_names:
            stable_name = str(stable)
            columns = await self._describe_columns(stable_name)
            if columns:
                self._col_cache[stable_name] = columns
                logger.info(
                    "[tdengine] stable cache loaded: %s.%s (%d columns)",
                    self._database,
                    stable_name,
                    len(columns),
                )
            else:
                logger.info(
                    "[tdengine] stable not preloaded, will create on first write if needed: %s.%s",
                    self._database,
                    stable_name,
                )

    async def _ensure_stable(self, stable: str, initial_columns: list[str] | None = None) -> None:
        if stable in self._col_cache:
            return

        valid_cols = [col for col in initial_columns or [] if col]
        if not valid_cols:
            logger.info(
                "[tdengine] stable %s.%s has no cached columns and no initial column to create with",
                self._database,
                stable,
            )
            return

        db = _quote(self._database)
        stbl = _quote(stable)
        # M1：一次性包含全部初始列，避免后续对同一 stable 发起 N 条 ALTER STABLE
        cols_sql = ", ".join(f"{_quote(col)} DOUBLE" for col in valid_cols)
        sql = (
            f"CREATE STABLE IF NOT EXISTS {db}.{stbl} "
            f"(`ts` TIMESTAMP, {cols_sql}) "
            f"TAGS (`device_code` VARCHAR(32))"
        )
        if not await self._execute(sql):
            logger.error("[tdengine] failed to ensure stable: %s.%s", self._database, stable)
            return

        self._col_cache[stable] = await self._describe_columns(stable)
        logger.info(
            "[tdengine] stable ready: %s.%s (%d cached columns)",
            self._database,
            stable,
            len(self._col_cache[stable]),
        )

    async def _describe_columns(self, stable: str) -> set[str]:
        rows = await self._query(f"DESCRIBE {_quote(self._database)}.{_quote(stable)}")
        columns: set[str] = set()
        for row in rows:
            if not row:
                continue
            col_name = str(row[0] or "").strip()
            if not col_name or col_name.lower() == "ts":
                continue

            # DESCRIBE returns tag metadata differently across connectors/versions.
            # The live schema has device_code as the only tag, so keep it out of
            # the data-column cache even when no explicit TAG marker is present.
            note = " ".join(str(item or "") for item in row[3:]).upper()
            if col_name.lower() in _KNOWN_TAG_COLUMNS or "TAG" in note:
                continue
            columns.add(col_name.lower())
        return columns

    async def _ensure_columns(self, stable: str, columns: list[str]) -> None:
        await self._ensure_stable(stable, columns)

        cached = self._col_cache.get(stable)
        if cached is None:
            logger.error("[tdengine] stable is not ready for column checks: %s.%s", self._database, stable)
            return

        missing = [col for col in columns if col.lower() not in cached]
        if not missing:
            return

        for col in missing:
            ok = await self._add_column(stable, col)
            if ok:
                cached.add(col.lower())
                continue

            refreshed = await self._describe_columns(stable)
            if refreshed:
                self._col_cache[stable] = refreshed
                cached = refreshed
            if col.lower() in cached:
                logger.info("[tdengine] column already exists after refresh: %s.%s", stable, col)
            else:
                logger.error(
                    "[tdengine] column still missing after ALTER failure: %s.%s",
                    stable,
                    col,
                )

    async def _add_column(self, stable: str, col: str) -> bool:
        sql = (
            f"ALTER STABLE {_quote(self._database)}.{_quote(stable)} "
            f"ADD COLUMN {_quote(col)} DOUBLE"
        )
        if self._conn is None:
            logger.error("[tdengine] not connected, cannot alter stable")
            return False

        loop = asyncio.get_running_loop()
        try:
            await loop.run_in_executor(None, lambda: self._conn.execute(sql))
            logger.info("[tdengine] added column: %s.%s DOUBLE", stable, col)
            return True
        except Exception as exc:
            message = str(exc).lower()
            if "already exist" in message or "already exists" in message or "duplicated" in message:
                logger.info("[tdengine] column already exists: %s.%s", stable, col)
            else:
                logger.error("[tdengine] add column failed: %s.%s: %s", stable, col, exc)
            return False

    # ------------------------------------------------------------------
    # Write
    # ------------------------------------------------------------------

    async def write_multi_table(
        self, rows: list[dict]
    ) -> list[tuple[list[int], str, float]]:
        """Write rows across many child tables as chunked multi-table INSERTs.

        rows[i] = {child_table, stable, device_code, ts, columns}. Rows of any
        number of child tables (and stables) are packed into single INSERT
        statements bounded by MAX_SQL_BYTES, since one TDengine statement can
        cover thousands of subtables but is capped at ~4 MiB of SQL.

        Returns (row_indices, status, elapsed_ms) outcome groups covering every
        input row exactly once. status ∈ {LANDED, BAD_DATA, RETRY} (§7.6):
        a chunk that commits → one LANDED group; a chunk that fails on a definite
        data/SQL error is binary-searched so good rows still land (LANDED) and only
        rows that fail alone are BAD_DATA; connection/unknown errors mark the whole
        chunk RETRY without bisecting (never DLQ'd). The caller ACKs LANDED, pushes
        BAD_DATA to the DLQ, and keeps RETRY buffered.
        """
        if not rows:
            return []

        async with self._lock:
            # Ensure columns per stable (union across all rows of that stable),
            # so the INSERT never references a column the stable lacks.
            cols_by_stable: dict[str, list[str]] = {}
            seen_by_stable: dict[str, set[str]] = {}
            for row in rows:
                stable = row["stable"]
                seen = seen_by_stable.setdefault(stable, set())
                cols = cols_by_stable.setdefault(stable, [])
                for col in row["columns"]:
                    if col not in seen:
                        seen.add(col)
                        cols.append(col)
            for stable, cols in cols_by_stable.items():
                await self._ensure_columns(stable, cols)

            results: list[tuple[list[int], str, float]] = []
            for indices, sql in self._iter_chunks(rows):
                results.extend(await self._write_chunk(rows, indices, sql))
            return results

    async def _write_chunk(
        self,
        rows: list[dict],
        indices: list[int],
        sql: str | None = None,
        depth: int = 0,
    ) -> list[tuple[list[int], str, float]]:
        """Execute one chunk; on a data error, binary-search to isolate bad rows.

        Returns (indices, status, elapsed_ms) outcome groups. Recursion halves the
        index set each level, so it terminates at single rows (ceil(log2 n) deep);
        _MAX_BISECT_DEPTH is a safety valve for pathological inputs.
        """
        if not indices:
            return []
        if sql is None:
            sql = self._build_chunk_sql(rows, indices)

        started = time.perf_counter()
        ok, exc = await self._execute_ex(sql)
        elapsed = (time.perf_counter() - started) * 1000

        if ok:
            return [(indices, LANDED, elapsed)]

        # 连接错(含未连接 exc=None) / 未知错误 → 整块重试，绝不二分进 DLQ。
        if exc is None or _is_connection_error(exc) or not _is_data_error(exc):
            return [(indices, RETRY, elapsed)]

        # 明确的数据 / SQL 错误：单行仍失败才判坏；否则二分隔离。
        if len(indices) == 1:
            return [(indices, BAD_DATA, elapsed)]
        if depth >= _MAX_BISECT_DEPTH:
            logger.error(
                "[tdengine] bisect depth limit reached (%d rows), retrying instead of DLQ",
                len(indices),
            )
            return [(indices, RETRY, elapsed)]

        mid = len(indices) // 2
        left = await self._write_chunk(rows, indices[:mid], None, depth + 1)
        right = await self._write_chunk(rows, indices[mid:], None, depth + 1)
        return left + right

    def _build_chunk_sql(self, rows: list[dict], indices: list[int]) -> str:
        """Build one multi-table INSERT covering exactly `indices` (for bisection).

        Groups indices by child table and unions columns per table, mirroring
        _iter_chunks but without the byte-budget packing (a bisected half is
        already smaller than the chunk that failed).
        """
        by_table: dict[str, list[int]] = {}
        for i in indices:
            by_table.setdefault(rows[i]["child_table"], []).append(i)
        fragments = [
            self._subtable_fragment(rows, idxs, self._union_cols(rows, idxs))
            for idxs in by_table.values()
        ]
        return "INSERT INTO " + " ".join(fragments)

    @staticmethod
    def _format_value(raw: object) -> str:
        if raw is None:
            return "NULL"
        try:
            value = float(raw)
        except (TypeError, ValueError):
            return "NULL"
        if not math.isfinite(value):
            return "NULL"
        return repr(value)

    @staticmethod
    def _union_cols(rows: list[dict], idxs: list[int]) -> list[str]:
        all_cols: list[str] = []
        seen: set[str] = set()
        for i in idxs:
            for col in rows[i]["columns"]:
                if col not in seen:
                    seen.add(col)
                    all_cols.append(col)
        return all_cols

    def _subtable_header(self, row: dict, all_cols: list[str]) -> str:
        db = _quote(self._database)
        tbl = _quote(row["child_table"])
        stbl = _quote(row["stable"])
        tag_val = str(row["device_code"]).replace("'", "''")
        col_list = ", ".join([_quote("ts")] + [_quote(c) for c in all_cols])
        return f"{db}.{tbl} USING {db}.{stbl} TAGS ('{tag_val}') ({col_list}) VALUES "

    def _value_clause(self, row: dict, all_cols: list[str]) -> str:
        col_vals = row["columns"]
        vals = [str(int(row["ts"]))]
        for col in all_cols:
            vals.append(self._format_value(col_vals.get(col)))
        return "(" + ", ".join(vals) + ")"

    def _subtable_fragment(self, rows: list[dict], idxs: list[int], all_cols: list[str]) -> str:
        header = self._subtable_header(rows[idxs[0]], all_cols)
        return header + " ".join(self._value_clause(rows[i], all_cols) for i in idxs)

    def _split_subtable(self, rows: list[dict], idxs: list[int], all_cols: list[str]):
        """Split one child table's rows so each fragment stays under MAX_SQL_BYTES.

        Measures header + per-row clause bytes incrementally to avoid O(n^2).
        A single row clause that alone exceeds the cap is still emitted on its own
        (cannot be split further); TDengine would reject it, surfacing as failure.
        """
        header_bytes = len(self._subtable_header(rows[idxs[0]], all_cols).encode("utf-8"))
        cur: list[int] = []
        cur_bytes = header_bytes
        for i in idxs:
            clause_bytes = len(self._value_clause(rows[i], all_cols).encode("utf-8"))
            add = clause_bytes + (1 if cur else 0)  # space separator between value clauses
            if cur and cur_bytes + add > MAX_SQL_BYTES:
                yield cur
                cur = [i]
                cur_bytes = header_bytes + clause_bytes
            else:
                cur.append(i)
                cur_bytes += add
        if cur:
            yield cur

    def _iter_chunks(self, rows: list[dict]):
        """Yield (row_indices, sql) chunks, each a multi-table INSERT <= MAX_SQL_BYTES."""
        prefix = "INSERT INTO "
        prefix_bytes = len(prefix.encode("utf-8"))

        # Group row indices by child table, preserving first-seen order.
        by_table: dict[str, list[int]] = {}
        for i, row in enumerate(rows):
            by_table.setdefault(row["child_table"], []).append(i)

        # Build per-subtable fragments (splitting oversized subtables by rows).
        fragments: list[tuple[str, list[int], int]] = []
        for idxs in by_table.values():
            all_cols = self._union_cols(rows, idxs)
            for frag_idxs in self._split_subtable(rows, idxs, all_cols):
                frag = self._subtable_fragment(rows, frag_idxs, all_cols)
                fragments.append((frag, frag_idxs, len(frag.encode("utf-8"))))

        # Next-fit pack fragments into chunks.
        cur_frags: list[str] = []
        cur_idxs: list[int] = []
        cur_bytes = prefix_bytes
        for frag, frag_idxs, frag_bytes in fragments:
            sep = 1 if cur_frags else 0
            if cur_frags and cur_bytes + sep + frag_bytes > MAX_SQL_BYTES:
                yield cur_idxs, prefix + " ".join(cur_frags)
                cur_frags = []
                cur_idxs = []
                cur_bytes = prefix_bytes
            cur_frags.append(frag)
            cur_idxs.extend(frag_idxs)
            cur_bytes += (1 if len(cur_frags) > 1 else 0) + frag_bytes
        if cur_frags:
            yield cur_idxs, prefix + " ".join(cur_frags)
