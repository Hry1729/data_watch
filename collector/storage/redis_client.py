"""Redis 连接池封装（COL-1）。"""

from __future__ import annotations

import redis

_pool: redis.ConnectionPool | None = None


def get_redis(config) -> redis.Redis:
    global _pool
    if _pool is None:
        _pool = redis.BlockingConnectionPool(
            host=config.redis.host,
            port=config.redis.port,
            db=config.redis.db,
            password=config.redis.password or None,
            decode_responses=True,
            max_connections=64,
            socket_connect_timeout=3,
            socket_timeout=5,
            timeout=2,
        )
    return redis.Redis(connection_pool=_pool)


def close_pool() -> None:
    global _pool
    if _pool is not None:
        _pool.disconnect()
        _pool = None
