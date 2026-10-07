"""Redis 登录失败滑窗限流（DW-08）。"""
from __future__ import annotations

import threading
import asyncio
import hashlib
import json
from uuid import uuid4
from redis.exceptions import RedisError
from app.core.exceptions import ApiError
from app.db.redis import get_redis_client

_SCRIPT = """
local key = KEYS[1]
if ARGV[1] == 'reset' then return redis.call('DEL', key) end
local clock = redis.call('TIME')
local now = tonumber(clock[1]) * 1000000 + tonumber(clock[2])
local window = tonumber(ARGV[2])
redis.call('ZREMRANGEBYSCORE', key, '-inf', now - window)
if ARGV[1] == 'record' then
    redis.call('ZADD', key, now, ARGV[4])
    redis.call('ZREMRANGEBYRANK', key, 0, -(tonumber(ARGV[3]) + 1))
    redis.call('PEXPIRE', key, math.ceil(window / 1000) + 1)
end
return redis.call('ZCARD', key)
"""


class LoginLimiter:
    def __init__(self, max_failures: int, window_seconds: float) -> None:
        self._max_failures = max(1, int(max_failures))
        self._window_us = int(max(1.0, float(window_seconds)) * 1000000)

    def _key(self, user_name: str, client_ip: str) -> str:
        identity = (str(user_name or "").strip().lower(), str(client_ip or "").strip())
        digest = hashlib.sha256(json.dumps(identity, ensure_ascii=False, separators=(",", ":")).encode("utf-8", "surrogatepass")).hexdigest()
        return "datawatch:auth:login-failures:v1:" + digest

    async def _apply(self, action: str, user_name: str, client_ip: str) -> int:
        try:
            async with asyncio.timeout(3):
                client = await get_redis_client()
                return await client.eval(_SCRIPT, 1, self._key(user_name, client_ip), action,
                    self._window_us, self._max_failures, uuid4().hex if action == "record" else "")
        except (RedisError, TimeoutError) as exc:
            raise ApiError(503, "登录验证服务暂不可用，请稍后重试", status_code=503) from exc

    async def is_blocked(self, user_name: str, client_ip: str) -> bool:
        return await self._apply("check", user_name, client_ip) >= self._max_failures

    async def record_failure(self, user_name: str, client_ip: str) -> None:
        await self._apply("record", user_name, client_ip)

    async def reset(self, user_name: str, client_ip: str) -> None:
        await self._apply("reset", user_name, client_ip)


_limiter: LoginLimiter | None = None
_limiter_lock = threading.Lock()


def get_login_limiter() -> LoginLimiter:
    global _limiter
    if _limiter is None:
        with _limiter_lock:
            if _limiter is None:
                from app.core.config import get_settings

                settings = get_settings()
                _limiter = LoginLimiter(
                    max_failures=settings.login_fail_max,
                    window_seconds=settings.login_fail_window_seconds,
                )
    return _limiter
