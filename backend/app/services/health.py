from __future__ import annotations

import logging
import time
from collections.abc import Awaitable, Callable

logger = logging.getLogger("backend.health")

_HEALTH_TTL_SECONDS = 30.0
_health_cache: dict[str, tuple[bool, float]] = {}


async def _cached_check(name: str, checker: Callable[[], Awaitable[None]]) -> bool:
    now = time.monotonic()
    cached = _health_cache.get(name)
    if cached is not None:
        ok, checked_at = cached
        if now - checked_at < _HEALTH_TTL_SECONDS:
            return ok

    try:
        await checker()
    except Exception as exc:
        logger.warning("%s health check failed: %s", name, exc)
        _health_cache[name] = (False, now)
        return False

    _health_cache[name] = (True, now)
    return True


async def check_tdengine_health() -> bool:
    async def _check() -> None:
        from app.db.tdengine import get_tdengine_client

        client = get_tdengine_client()
        await client.fetch_all("SELECT SERVER_STATUS()")

    return await _cached_check("tdengine", _check)
