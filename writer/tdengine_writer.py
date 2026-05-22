"""TDengine batch writer.

Connects via WebSocket (taos-ws-py / taosws) matching the approach used in
collector/storage/tdengine.py.

write_batch() accepts a list of row dicts and groups them by child_table before
executing a single INSERT per child_table to minimise round trips.

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
from typing import Any
from urllib.parse import quote_plus

logger = logging.getLogger("writer.tdengine")


def _quote(name: str) -> str:
    return f"`{name.replace('`', '``')}`"


class TDengineWriter:
    def __init__(self) -> None:
        self._conn: Any = None
        self._database: str = "device_data"
        self._lock = asyncio.Lock()
        self._col_cache: dict[str, set[str]] = {}

    # ------------------------------------------------------------------
    # Lifecycle
    # ------------------------------------------------------------------

    async def connect(self, config: dict) -> None:
        """Open a WebSocket connection to TDengine (runs taosws.connect in executor)."""
        from taosws import connect  # type: ignore[import]

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
            return False
        loop = asyncio.get_running_loop()
        try:
            await loop.run_in_executor(None, lambda: self._conn.execute(sql))
            return True
        except Exception as exc:
            logger.error("[tdengine] execute failed: %s | SQL: %.200s", exc, sql)
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

        first_col = next((col for col in initial_columns or [] if col), "")
        if not first_col:
            logger.info(
                "[tdengine] stable %s.%s has no cached columns and no initial column to create with",
                self._database,
                stable,
            )
            return

        db = _quote(self._database)
        stbl = _quote(stable)
        sql = (
            f"CREATE STABLE IF NOT EXISTS {db}.{stbl} "
            f"(`ts` TIMESTAMP, {_quote(first_col)} DOUBLE) "
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
            if col_name.lower() == "device_code" or "TAG" in note:
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
        """Write a list of row dicts to TDengine.

        Groups rows by child_table so each child_table gets a single
        multi-row INSERT statement.

        Returns True if all groups were written without error, False if any failed.
        """
        if not rows:
            return True

        # Group by child_table
        by_table: dict[str, list[dict]] = {}
        for row in rows:
            tbl = row["child_table"]
            by_table.setdefault(tbl, []).append(row)

        all_ok = True
        async with self._lock:
            for child_table, table_rows in by_table.items():
                ok = await self._write_table_rows(child_table, table_rows)
                if not ok:
                    all_ok = False
        return all_ok

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
