"""Dead Letter Queue helpers.

Failed stream entries that exceeded the retry limit are pushed to a Redis List
keyed by rt:dlq:{device_type}:{group_code}.
"""

from __future__ import annotations

import json
import logging
import time

logger = logging.getLogger("writer.dlq")


def push_dlq(
    r,
    device_type: str,
    group_code: str,
    entry_id: str,
    stream_key: str,
    data: dict,
    retry_count: int,
    last_error: object,
) -> bool:
    dlq_key = f"rt:dlq:{device_type}:{group_code}"
    payload = {
        "entry_id": entry_id,
        "stream_key": stream_key,
        "retry_count": retry_count,
        "last_error": str(last_error),
        "failed_at": int(time.time() * 1000),
        "data": data,
    }
    try:
        r.lpush(dlq_key, json.dumps(payload))
    except Exception as exc:
        logger.error("[DLQ] lpush failed for entry %s: %s", entry_id, exc)
        return False
    logger.error(
        "[DLQ] entry %s moved to dead letter queue %s after %d retries: %s",
        entry_id,
        dlq_key,
        retry_count,
        last_error,
    )
    return True
