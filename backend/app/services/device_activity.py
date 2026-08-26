from __future__ import annotations

from collections.abc import Iterable
from datetime import datetime
import json
import logging
from typing import Any

from app.core.time_utils import parse_local_datetime
from app.db.redis import get_redis_client


logger = logging.getLogger("backend.device_activity")
LATEST_KEY_PREFIX = "rt:latest:"


async def fetch_latest_collect_times(
    device_codes: Iterable[str],
) -> tuple[dict[str, datetime], bool]:
    targets = {
        normalized.casefold(): normalized
        for code in device_codes
        if (normalized := str(code).strip())
    }
    if not targets:
        return {}, True

    try:
        redis = await get_redis_client()
        keys: list[tuple[str, str]] = []
        async for raw_key in redis.scan_iter(match=f"{LATEST_KEY_PREFIX}*", count=500):
            key = raw_key.decode("utf-8") if isinstance(raw_key, bytes) else str(raw_key)
            device_code, separator, _group_code = key[len(LATEST_KEY_PREFIX) :].rpartition(":")
            target_code = targets.get(device_code.casefold())
            if separator and target_code is not None:
                keys.append((key, target_code))

        if not keys:
            return {}, True

        pipeline = redis.pipeline(transaction=False)
        for key, _device_code in keys:
            pipeline.hgetall(key)
        hashes = await pipeline.execute()
    except Exception as exc:
        logger.warning("Redis latest activity query failed: %s", exc)
        return {}, False

    latest_times: dict[str, datetime] = {}
    for (_key, device_code), values in zip(keys, hashes, strict=False):
        for raw_value in values.values():
            try:
                entry: Any = json.loads(raw_value)
            except (TypeError, ValueError, json.JSONDecodeError):
                continue
            timestamp = entry.get("t") if isinstance(entry, dict) else None
            collected_at = parse_local_datetime(timestamp)
            if collected_at is not None:
                latest_times[device_code] = max(
                    latest_times.get(device_code, collected_at),
                    collected_at,
                )

    return latest_times, True
