from __future__ import annotations

import asyncio
from dataclasses import dataclass
import re
from typing import Any

from app.core.config import get_settings


@dataclass
class TDengineClient:
    host: str
    port: int
    user: str
    password: str
    database: str
    use_ssl: bool = False

    _identifier_pattern = re.compile(r"^[A-Za-z0-9_]+$")

    def build_ws_dsn(self, database: str | None = None) -> str:
        scheme = "wss" if self.use_ssl else "ws"
        database_name = database or self.database
        return f"{scheme}://{self.user}:{self.password}@{self.host}:{self.port}/{database_name}"

    @property
    def rest_url(self) -> str:
        scheme = "https" if self.use_ssl else "http"
        return f"{scheme}://{self.host}:{self.port}"

    def connect(self, database: str | None = None):
        ws_error: Exception | None = None
        try:
            from taosws import connect as ws_connect

            return ws_connect(self.build_ws_dsn(database=database))
        except Exception as exc:
            ws_error = exc

        try:
            from taosrest import connect as rest_connect

            return rest_connect(
                url=self.rest_url,
                user=self.user,
                password=self.password,
                database=database or self.database,
            )
        except Exception as exc:
            if ws_error is not None:
                raise RuntimeError(
                    f"TDengine connect failed via taosws ({ws_error}) and taosrest ({exc})"
                ) from exc
            raise

    def quote_identifier(self, value: str) -> str:
        if not self._identifier_pattern.fullmatch(value):
            raise ValueError(f"Invalid TDengine identifier: {value}")
        return f"`{value}`"

    def qualify_table(self, database_or_table: str, table_name: str | None = None) -> str:
        if table_name is None:
            database_name = self.database
            resolved_table_name = database_or_table
        else:
            database_name = database_or_table
            resolved_table_name = table_name
        return f"{self.quote_identifier(database_name)}.{self.quote_identifier(resolved_table_name)}"

    async def fetch_all(self, sql: str, database: str | None = None) -> list[tuple[Any, ...]]:
        return await asyncio.to_thread(self._fetch_all_sync, sql, database)

    def _fetch_all_sync(self, sql: str, database: str | None = None) -> list[tuple[Any, ...]]:
        conn = self.connect(database=database)
        cursor = conn.cursor()
        try:
            cursor.execute(sql)
            rows = cursor.fetchall()
            return list(rows or [])
        finally:
            cursor.close()
            conn.close()


def get_tdengine_client() -> TDengineClient:
    settings = get_settings()
    return TDengineClient(
        host=settings.tdengine_host,
        port=settings.tdengine_port,
        user=settings.tdengine_user,
        password=settings.tdengine_password,
        database=settings.tdengine_database,
        use_ssl=settings.tdengine_use_ssl,
    )
