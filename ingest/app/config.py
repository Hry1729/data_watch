from __future__ import annotations

import os
from dataclasses import dataclass
from functools import lru_cache

try:
    import psutil
except ImportError:  # pragma: no cover - packaged Ingest declares psutil
    psutil = None


@dataclass(frozen=True, slots=True)
class IngestSettings:
    host: str
    port: int
    redis_url: str
    mysql_host: str
    mysql_port: int
    mysql_user: str
    mysql_password: str
    mysql_database: str
    stream_shard_count: int
    stream_maxlen: int
    signature_max_skew_seconds: int
    nonce_ttl_seconds: int
    max_batch_items: int
    max_body_bytes: int = 4 * 1024 * 1024
    max_offline_retention_days: int = 7
    control_cache_ttl_ms: int = 5000
    access_log: bool = False
    workers: int = 4
    mysql_pool_maxsize: int = 5

    @classmethod
    def from_env(cls) -> "IngestSettings":
        settings = cls(
            host=os.getenv("INGEST_HOST", "0.0.0.0"),
            port=int(os.getenv("INGEST_PORT", "8010")),
            redis_url=os.getenv("INGEST_REDIS_URL", "redis://127.0.0.1:6379/0"),
            mysql_host=os.getenv("MYSQL_HOST", "127.0.0.1"),
            mysql_port=int(os.getenv("MYSQL_PORT", "3306")),
            mysql_user=os.getenv("MYSQL_USER", "root"),
            mysql_password=os.getenv("MYSQL_PASSWORD", ""),
            mysql_database=os.getenv("MYSQL_DATABASE", "device_watch"),
            stream_shard_count=int(os.getenv("INGEST_STREAM_SHARD_COUNT", "4")),
            # 0 means unlimited; preserving the stream is the safe default.
            stream_maxlen=int(os.getenv("INGEST_STREAM_MAXLEN", "0")),
            signature_max_skew_seconds=int(os.getenv("INGEST_SIGNATURE_MAX_SKEW_SECONDS", "300")),
            nonce_ttl_seconds=int(os.getenv("INGEST_NONCE_TTL_SECONDS", "600")),
            max_batch_items=int(os.getenv("INGEST_MAX_BATCH_ITEMS", "500")),
            max_body_bytes=int(
                os.getenv(
                    "INGEST_MAX_BODY_BYTES",
                    os.getenv("DATAWATCH_MAX_HTTP_BODY_BYTES", str(4 * 1024 * 1024)),
                )
            ),
            max_offline_retention_days=int(
                os.getenv("INGEST_MAX_OFFLINE_RETENTION_DAYS", "7")
            ),
            control_cache_ttl_ms=int(
                os.getenv("INGEST_CONTROL_CACHE_TTL_MS", "30000")
            ),
            access_log=_env_bool("INGEST_ACCESS_LOG", False),
            workers=int(os.getenv("INGEST_WORKERS", str(_default_workers()))),
            mysql_pool_maxsize=int(os.getenv("INGEST_MYSQL_POOL_MAXSIZE", "5")),
        )
        if settings.stream_shard_count <= 0 or settings.stream_maxlen < 0:
            raise ValueError("stream shard count must be positive and maxlen non-negative")
        # B27：`INGEST_STREAM_MAXLEN` 目前是**失效配置**——提交路径的 Lua 实参被硬编码为 0
        # （ingest/app/redis_store.py 的 pipe.eval：`"0",  # never trim unconsumed data`），
        # 因为**按长度裁剪会丢掉尚未被 Writer 消费的帧**（那不是稳定，而是丢数据）。
        # 与其"静默忽略正值"，不如**显式拒绝**：配置正值绝不产生与说明相反的行为。
        # 容量边界必须由显式背压/告警处理；已确认残留的清理属 B31，不走这里。
        if settings.stream_maxlen > 0:
            raise ValueError(
                "INGEST_STREAM_MAXLEN must be 0: trimming the stream by length would drop "
                "unconsumed frames (the commit path deliberately never trims unacked data). "
                "Handle capacity with explicit backpressure/alerts instead."
            )
        if settings.max_batch_items <= 0:
            raise ValueError("max batch items must be positive")
        if settings.max_body_bytes <= 0:
            raise ValueError("max body bytes must be positive")
        if settings.max_offline_retention_days <= 0:
            raise ValueError("max offline retention days must be positive")
        if settings.control_cache_ttl_ms <= 0:
            raise ValueError("control cache ttl must be positive")
        if settings.workers <= 0:
            raise ValueError("workers must be positive")
        if settings.mysql_pool_maxsize <= 0:
            raise ValueError("mysql pool maxsize must be positive")
        return settings


def _env_bool(name: str, default: bool) -> bool:
    value = os.getenv(name)
    if value is None:
        return default
    normalized = value.strip().lower()
    if normalized in {"1", "true", "yes", "on"}:
        return True
    if normalized in {"0", "false", "no", "off"}:
        return False
    raise ValueError(f"{name} must be a boolean")


def _physical_cpu_count() -> int:
    if psutil is not None:
        try:
            physical = psutil.cpu_count(logical=False)
        except Exception:
            physical = None
        if physical is not None and physical > 0:
            return int(physical)

    logical = os.cpu_count() or 4
    return max(1, (logical + 1) // 2)


def _default_workers() -> int:
    physical = _physical_cpu_count()
    return min(8, max(2, (physical + 1) // 2))


@lru_cache(maxsize=1)
def get_settings() -> IngestSettings:
    return IngestSettings.from_env()
