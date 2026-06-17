from types import SimpleNamespace

import redis

from storage import redis_client


def test_get_redis_uses_bounded_blocking_connection_pool():
    config = SimpleNamespace(
        redis=SimpleNamespace(
            host="127.0.0.1",
            port=6379,
            db=0,
            password="",
        )
    )

    redis_client.close_pool()
    try:
        client = redis_client.get_redis(config)

        assert isinstance(client.connection_pool, redis.BlockingConnectionPool)
        assert client.connection_pool.max_connections == 64
        assert client.connection_pool.timeout == 2
    finally:
        redis_client.close_pool()
