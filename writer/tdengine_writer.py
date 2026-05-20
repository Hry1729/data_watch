"""TDengine batch writer.

Connects via WebSocket (taos-ws-py / taosws) matching the approach used in
collector/storage/tdengine.py.

write_batch() accepts a list of row dicts and groups them by child_table before
executing a single INSERT per child_table to minimise round trips.

Row dict schema:
    {
        "child_table": str,   # e.g. t_QC100_g001
        "stable":      str,   # e.g. st_qc_g001
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
        except Exception as exc:
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

    async def table_exists(self, table_name: str) -> bool:
        """Return True if child table exists in the configured database."""
        if self._conn is None:
            return False
        loop = asyncio.get_running_loop()
        try:
            result = await loop.run_in_executor(
                None,
                lambda: self._conn.query(
                    f"SHOW {_quote(self._database)}.TABLES LIKE '{table_name}'"
                ),
            )
            rows = list(result) if result else []
            return len(rows) > 0
        except Exception as exc:
            logger.warning("[tdengine] table_exists check failed for %s: %s", table_name, exc)
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

        Uses explicit column list so only the columns present in this batch
        are referenced — avoiding inserting NULLs for the ~3072 columns not
        in the current scan frame.

        SQL form:
            INSERT INTO `device_data`.`t_QC100_g001` (`ts`, `v0001`, `v0003`)
            VALUES (1716000000000, 1.0, 2.0)
            (1716000000500, 1.1, 2.1)
        """
        if not rows:
            return True

        # Collect the union of column names across all rows in this group
        all_cols: list[str] = []
        seen_cols: set[str] = set()
        for row in rows:
            for col in row["columns"]:
                if col not in seen_cols:
                    seen_cols.add(col)
                    all_cols.append(col)

        db = _quote(self._database)
        tbl = _quote(child_table)
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
            f"INSERT INTO {db}.{tbl} ({col_list}) VALUES "
            + " ".join(value_clauses)
        )
        return await self._execute(sql)
