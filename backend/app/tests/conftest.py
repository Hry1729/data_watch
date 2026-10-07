import os
from urllib.parse import urlparse
from uuid import uuid4

import pytest
import pytest_asyncio
from redis.asyncio import Redis
from app.core.login_limiter import LoginLimiter


@pytest_asyncio.fixture
async def login_redis(monkeypatch):
    url = os.environ.get("DATAWATCH_TEST_LOGIN_REDIS_URL")
    if not url:
        pytest.skip("set DATAWATCH_TEST_LOGIN_REDIS_URL to an isolated loopback Redis port")
    parsed = urlparse(url)
    assert parsed.hostname in {"127.0.0.1", "localhost"} and parsed.port not in {None, 6379}, "dedicated Redis port required"
    client = Redis.from_url(url, decode_responses=True, socket_timeout=3, socket_connect_timeout=3)
    assert (await client.config_get("maxmemory-policy"))["maxmemory-policy"] == "noeviction"
    prefix = "login-test:" + uuid4().hex + ":"
    original_key = LoginLimiter._key
    monkeypatch.setattr(LoginLimiter, "_key", lambda self, name, ip: prefix + original_key(self, name, ip))
    async def get_client():
        return client
    monkeypatch.setattr("app.core.login_limiter.get_redis_client", get_client)
    try:
        yield client
    finally:
        batch = []
        async for key in client.scan_iter(match=prefix + "*", count=500):
            batch.append(key)
            if len(batch) == 500:
                await client.delete(*batch)
                batch.clear()
        if batch:
            await client.delete(*batch)
        await client.aclose()
