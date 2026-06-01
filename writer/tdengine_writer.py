"""TDengine batch writer.

Connects via WebSocket (taos-ws-py / taosws) matching the approach used in
collector/storage/tdengine.py.

write_batch() executes a single INSERT for one child_table. The caller is
responsible for grouping rows by child_table before invoking (see consumer._flush).

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


def _is_connection_error(exc: Exception) -> bool:
    """Heuristic: does this exception indicate a dropped/unhealthy connection?"""
    msg = str(exc).lower()
    return any(kw in msg for kw in _CONNECTION_ERROR_KEYWORDS)


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

    async def _execute(self, sql: str) -> bool:
        if self._conn is None:
            logger.error("[tdengine] not connected, cannot execute SQL")
            if self._config is not None and not self._reconnecting:
                await self._try_reconnect()
            return False
        loop = asyncio.get_running_loop()
        try:
            await loop.run_in_executor(None, lambda: self._conn.execute(sql))
            return True
        except Exception as exc:
            logger.error("[tdengine] execute failed: %s | SQL: %.200s", exc, sql)
            if (
                self._config is not None
                and not self._reconnecting
                and _is_connection_error(exc)
            ):
                await self._try_reconnect()
            return False

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
        sql = f"CREATE DATABASE IF NOT EXISTS {_quote(self._database)}"
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

    async def write_batch(self, rows: list[dict]) -> bool:
        """Write a list of rows that all belong to a single child_table.

        Caller MUST group rows by child_table before calling (see consumer._flush).
        Returns True on success, False if the INSERT failed.
        """
        if not rows:
            return True
        async with self._lock:
            return await self._write_table_rows(rows[0]["child_table"], rows)

    async def _write_table_rows(self, child_table: str, rows: list[dict]) -> bool:
        """Build and execute one INSERT for all rows of a single child_table.

        Uses USING syntax so TDengine auto-creates the child table if it does
        not yet exist.

        SQL form:
            INSERT INTO `device_data`.`qc100`
            USING `device_data`.`sdqc` TAGS ('qc100')
            (`ts`, `qc1`, `qc3`)
            VALUES (1716000000000, 1.0, 2.0)
            (1716000000500, 1.1, 2.1)
        """
        if not rows:
            return True

        first = rows[0]
        stable = first["stable"]
        device_code = first["device_code"]

        # Collect the union of column names across all rows in this group
        all_cols: list[str] = []
        seen_cols: set[str] = set()
        for row in rows:
            for col in row["columns"]:
                if col not in seen_cols:
                    seen_cols.add(col)
                    all_cols.append(col)

        await self._ensure_columns(stable, all_cols)

        db = _quote(self._database)
        tbl = _quote(child_table)
        stbl = _quote(stable)
        tag_val = device_code.replace("'", "''")
        col_list = ", ".join([_quote("ts")] + [_quote(c) for c in all_cols])

        value_clauses: list[str] = []
        for row in rows:
            ts = int(row["ts"])
            col_vals = row["columns"]
            vals: list[str] = [str(ts)]
            for col in all_cols:
                raw = col_vals.get(col)
                if raw is None:
                    vals.append("NULL")
                else:
                    try:
                        vals.append(repr(float(raw)))
                    except (TypeError, ValueError):
                        vals.append("NULL")
            value_clauses.append(f"({', '.join(vals)})")

        sql = (
            f"INSERT INTO {db}.{tbl} USING {db}.{stbl} TAGS ('{tag_val}') "
            f"({col_list}) VALUES "
            + " ".join(value_clauses)
        )
        return await self._execute(sql)
