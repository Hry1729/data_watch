from __future__ import annotations

from collections.abc import AsyncIterator
from functools import lru_cache

from sqlalchemy import event
from sqlalchemy.ext.asyncio import AsyncEngine, AsyncSession, async_sessionmaker, create_async_engine

from app.core.config import get_settings


@lru_cache(maxsize=1)
def get_mysql_engine() -> AsyncEngine:
    settings = get_settings()
    engine = create_async_engine(settings.mysql_async_dsn, pool_pre_ping=True)

    @event.listens_for(engine.sync_engine, "connect")
    def _set_connection_timezone(dbapi_connection, _connection_record) -> None:
        cursor = dbapi_connection.cursor()
        try:
            cursor.execute("SET time_zone = '+08:00'")
        finally:
            cursor.close()

    return engine


@lru_cache(maxsize=1)
def get_mysql_session_factory() -> async_sessionmaker[AsyncSession]:
    return async_sessionmaker(get_mysql_engine(), expire_on_commit=False, class_=AsyncSession)


async def get_mysql_session() -> AsyncIterator[AsyncSession]:
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        yield session
