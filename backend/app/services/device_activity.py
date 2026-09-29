from __future__ import annotations

import asyncio
from collections.abc import Iterable
from contextvars import Context
from datetime import datetime
import json
import logging
from time import monotonic
from typing import Any

from app.core.request_metrics import measure_operation
from app.core.time_utils import parse_local_datetime
from app.db.redis import get_redis_client


logger = logging.getLogger("backend.device_activity")
LATEST_KEY_PREFIX = "rt:latest:"
_CACHE_TTL_SECONDS = 2.0
_CACHE_STALE_SECONDS = 30.0
_CACHE_MAX_ENTRIES = 16
_REDIS_READ_TIMEOUT_SECONDS = 3.0
_cache: dict[frozenset[str], tuple[float, dict[str, datetime]]] = {}
_inflight: dict[frozenset[str], asyncio.Task[tuple[dict[str, datetime], bool]]] = {}
_last_failed: set[frozenset[str]] = set()
_retry_after: dict[frozenset[str], float] = {}


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

    target_codes = frozenset(targets)
    cached = _cache.get(target_codes)
    now = monotonic()
    if cached is not None:
        age = now - cached[0]
        if age < _CACHE_TTL_SECONDS:
            return {targets[code]: timestamp for code, timestamp in cached[1].items()}, True
        if age < _CACHE_STALE_SECONDS:
            if target_codes not in _inflight and now >= _retry_after.get(target_codes, 0):
                _start_fetch(target_codes, background=True)
            return (
                {targets[code]: timestamp for code, timestamp in cached[1].items()},
                target_codes not in _last_failed,
            )

    task = _inflight.get(target_codes)
    if task is None:
        task = _start_fetch(target_codes)

    latest_times, connected = await asyncio.shield(task)
    return {targets[code]: timestamp for code, timestamp in latest_times.items()}, connected


def _start_fetch(
    target_codes: frozenset[str], *, background: bool = False,
) -> asyncio.Task[tuple[dict[str, datetime], bool]]:
    context = Context() if background else None
    task = asyncio.create_task(_load_latest_collect_times(target_codes), context=context)
    _inflight[target_codes] = task
    task.add_done_callback(lambda done: _finish_fetch(target_codes, done), context=context)
    return task


def _finish_fetch(
    target_codes: frozenset[str],
    task: asyncio.Task[tuple[dict[str, datetime], bool]],
) -> None:
    if _inflight.get(target_codes) is task:
        del _inflight[target_codes]
    try:
        latest_times, connected = task.result()
    except (Exception, asyncio.CancelledError):
        logger.exception("Redis latest activity query failed")
        return
    if connected:
        if target_codes not in _cache and len(_cache) >= _CACHE_MAX_ENTRIES:
            evicted = next(iter(_cache))
            del _cache[evicted]
            _last_failed.discard(evicted)
            _retry_after.pop(evicted, None)
        _cache[target_codes] = (monotonic(), latest_times)
        _last_failed.discard(target_codes)
        _retry_after.pop(target_codes, None)
    else:
        if target_codes in _cache:
            _last_failed.add(target_codes)
            _retry_after[target_codes] = monotonic() + _CACHE_TTL_SECONDS


async def _load_latest_collect_times(
    target_codes: frozenset[str],
) -> tuple[dict[str, datetime], bool]:
    try:
        async with asyncio.timeout(_REDIS_READ_TIMEOUT_SECONDS):
            with measure_operation("redis"):
                redis = await get_redis_client()
                keys: list[tuple[str, str]] = []
                async for raw_key in redis.scan_iter(match=f"{LATEST_KEY_PREFIX}*", count=500):
                    key = raw_key.decode("utf-8") if isinstance(raw_key, bytes) else str(raw_key)
                    device_code, separator, _group_code = key[len(LATEST_KEY_PREFIX) :].rpartition(":")
                    normalized_code = device_code.casefold()
                    if separator and normalized_code in target_codes:
                        keys.append((key, normalized_code))

                if not keys:
                    return {}, True

                pipeline = redis.pipeline(transaction=False)
                for key, _device_code in keys:
                    pipeline.hgetall(key)
                hashes = await pipeline.execute()
            return await asyncio.to_thread(_decode_latest_times, keys, hashes)
    except Exception as exc:
        logger.warning("Redis latest activity query failed: %s", exc)
        return {}, False


def _decode_latest_times(
    keys: list[tuple[str, str]], hashes: list[dict[str, Any]],
) -> tuple[dict[str, datetime], bool]:
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
