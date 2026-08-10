from __future__ import annotations

from collections.abc import Iterable
from datetime import datetime
import json
import logging
from typing import Any

from app.core.time_utils import LOCAL_TZ, to_local_naive
from app.db.redis import get_redis_client


logger = logging.getLogger("backend.device_activity")
LATEST_KEY_PREFIX = "rt:latest:"


async def fetch_latest_collect_times(
    device_codes: Iterable[str],
) -> tuple[dict[str, datetime], bool]:
    targets = {str(code) for code in device_codes if str(code)}
    if not targets:
        return {}, True

    try:
        redis = await get_redis_client()
        keys: list[tuple[str, str]] = []
        async for raw_key in redis.scan_iter(match=f"{LATEST_KEY_PREFIX}*", count=500):
            key = raw_key.decode("utf-8") if isinstance(raw_key, bytes) else str(raw_key)
            device_code, separator, _group_code = key[len(LATEST_KEY_PREFIX) :].rpartition(":")
            if separator and device_code in targets:
                keys.append((key, device_code))

        if not keys:
            return {}, True

        pipeline = redis.pipeline(transaction=False)
        for key, _device_code in keys:
            pipeline.hgetall(key)
        hashes = await pipeline.execute()
    except Exception as exc:
        logger.warning("Redis latest activity query failed: %s", exc)
        return {}, False

    latest_ms: dict[str, float] = {}
    for (_key, device_code), values in zip(keys, hashes, strict=False):
        for raw_value in values.values():
            try:
                entry: Any = json.loads(raw_value)
            except (TypeError, ValueError, json.JSONDecodeError):
                continue
            timestamp = entry.get("t") if isinstance(entry, dict) else None
            if isinstance(timestamp, (int, float)):
                latest_ms[device_code] = max(latest_ms.get(device_code, 0), timestamp)

    return {
        device_code: to_local_naive(datetime.fromtimestamp(timestamp / 1000, tz=LOCAL_TZ))
        for device_code, timestamp in latest_ms.items()
    }, True
