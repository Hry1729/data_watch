"""死信队列辅助函数。超过重试限制的失败流条目被推送到 Redis List，键为 rt:dlq:{device_type}:{group_code}。"""

from __future__ import annotations

import json
import logging
import time

logger = logging.getLogger("writer.dlq")

DEFAULT_DLQ_MAX_SIZE = 0
DEFAULT_DLQ_ALERT_THRESHOLD = 0.9
DEFAULT_DLQ_WARN_ENTRIES = 1000
# 兼容已有调用方/配置检查。
MAX_DLQ_SIZE = DEFAULT_DLQ_MAX_SIZE


def push_dlq(
    r,
    device_type: str,
    group_code: str,
    entry_id: str,
    stream_key: str,
    data: dict,
    retry_count: int,
    last_error: object,
    max_size: int = DEFAULT_DLQ_MAX_SIZE,
    alert_threshold: float = DEFAULT_DLQ_ALERT_THRESHOLD,
    warn_entries: int = DEFAULT_DLQ_WARN_ENTRIES,
) -> bool:
    dlq_key = f"rt:dlq:{device_type}:{group_code}"
    max_size = max(0, int(max_size))
    alert_threshold = min(1.0, max(0.0, float(alert_threshold)))
    warn_entries = max(0, int(warn_entries))
    payload = {
        "entry_id": entry_id,
        "stream_key": stream_key,
        "retry_count": retry_count,
        "last_error": str(last_error),
        "failed_at": int(time.time() * 1000),
        "data": data,
    }
    try:
        pipe = r.pipeline()
        pipe.lpush(dlq_key, json.dumps(payload))
        if max_size > 0:
            pipe.ltrim(dlq_key, 0, max_size - 1)
        results = pipe.execute()
    except Exception as exc:
        logger.error("[DLQ] lpush failed for entry %s: %s", entry_id, exc)
        return False
    if results:
        try:
            current_size = int(results[0])
        except (TypeError, ValueError):
            current_size = None
        if (
            max_size > 0
            and alert_threshold > 0.0
            and current_size is not None
            and current_size >= max_size * alert_threshold
        ):
            logger.warning(
                "[DLQ] queue %s is near capacity: size=%d max_size=%d threshold=%.2f",
                dlq_key,
                current_size,
                max_size,
                alert_threshold,
            )
        elif max_size == 0 and warn_entries > 0 and current_size is not None and current_size >= warn_entries:
            logger.warning(
                "[DLQ] unbounded queue %s reached warning threshold: size=%d warn_entries=%d",
                dlq_key,
                current_size,
                warn_entries,
            )
    logger.error(
        "[DLQ] entry %s moved to dead letter queue %s after %d retries: %s",
        entry_id,
        dlq_key,
        retry_count,
        last_error,
    )
    return True
