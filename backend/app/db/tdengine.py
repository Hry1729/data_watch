from __future__ import annotations

import asyncio
from dataclasses import dataclass
import logging
import re
import threading
from typing import Any
from urllib.parse import quote

from sqlalchemy import create_engine
from sqlalchemy.engine import Engine, URL
from sqlalchemy.exc import NoSuchModuleError

from app.core.config import get_settings

logger = logging.getLogger("backend.tdengine")

_POOL_SIZE = 5
_query_semaphore = asyncio.Semaphore(_POOL_SIZE)
_engine_lock = threading.Lock()
_tdengine_engine: Engine | None = None
_tdengine_engine_key: tuple[str, int, str, str] | None = None
_pool_unavailable_logged = False


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
        # user/password 经 URL 编码，避免密码含 @ : / # % 等字符时 DSN 被解析错误。
        user = quote(self.user, safe="")
        password = quote(self.password, safe="")
        return f"{scheme}://{user}:{password}@{self.host}:{self.port}/{database_name}"

    @property
    def rest_url(self) -> str:
        scheme = "https" if self.use_ssl else "http"
        return f"{scheme}://{self.host}:{self.port}"

    def build_sqlalchemy_url(self) -> URL:
        return URL.create(
            "taosws",
            username=self.user,
            password=self.password,
            query={"hosts": f"{self.host}:{self.port}"},
        )

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
        async with _query_semaphore:
            return await asyncio.to_thread(self._fetch_all_sync, sql, database)

    def _fetch_all_sync(self, sql: str, database: str | None = None) -> list[tuple[Any, ...]]:
        if self.use_ssl:
            return self._fetch_all_legacy_sync(sql, database)

        try:
            engine = get_tdengine_engine(self)
        except (ModuleNotFoundError, NoSuchModuleError) as exc:
            if isinstance(exc, ModuleNotFoundError) and exc.name != "taosws":
                raise
            _log_pool_unavailable_once(exc)
            return self._fetch_all_legacy_sync(sql, database)

        with engine.connect() as conn:
            result = conn.exec_driver_sql(sql)
            return [tuple(row) for row in result.fetchall()]

    def _fetch_all_legacy_sync(self, sql: str, database: str | None = None) -> list[tuple[Any, ...]]:
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


def get_tdengine_engine(client: TDengineClient | None = None) -> Engine:
    global _tdengine_engine, _tdengine_engine_key

    client = client or get_tdengine_client()
    if client.use_ssl:
        raise RuntimeError("TDengine SQLAlchemy pool is not enabled for SSL connections")

    key = (client.host, client.port, client.user, client.password)
    with _engine_lock:
        if _tdengine_engine is not None and _tdengine_engine_key == key:
            return _tdengine_engine
        if _tdengine_engine is not None:
            _tdengine_engine.dispose()
        _tdengine_engine = create_engine(
            client.build_sqlalchemy_url(),
            pool_pre_ping=True,
            pool_size=_POOL_SIZE,
            max_overflow=0,
            pool_timeout=5,
            pool_recycle=3600,
        )
        _tdengine_engine_key = key
        return _tdengine_engine


def _log_pool_unavailable_once(exc: Exception) -> None:
    global _pool_unavailable_logged

    if _pool_unavailable_logged:
        return
    logger.warning(
        "TDengine SQLAlchemy pool unavailable because taosws is not installed; falling back to legacy per-query connections: %s",
        exc,
    )
    _pool_unavailable_logged = True


async def close_tdengine() -> None:
    await asyncio.to_thread(_close_tdengine_sync)


def _close_tdengine_sync() -> None:
    global _tdengine_engine, _tdengine_engine_key

    with _engine_lock:
        if _tdengine_engine is not None:
            _tdengine_engine.dispose()
            _tdengine_engine = None
            _tdengine_engine_key = None
