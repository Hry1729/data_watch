"""Redis 写入封装（COL-2）：latest Hash / window List / stream Stream。"""

from __future__ import annotations

import time

import msgspec
import redis

from data_watch_shared.stream_envelope import (
    build_stream_fields,
    encode_latest_mapping,
    latest_key,
    stream_key,
)
from monitor.performance import PerformanceMetrics
from storage.redis_client import close_pool


class RedisWriter:
    storage_path = "redis_only"

    def __init__(
        self,
        r: redis.Redis,
        config,
        metrics: PerformanceMetrics | None = None,
    ) -> None:
        self.r = r
        self.shard_count: int = config.redis.stream_shard_count
        self._maxlen: int = config.redis.stream_maxlen
        self._metrics = metrics or PerformanceMetrics()
        self._clock = time.perf_counter

    def ping(self) -> None:
        self.r.ping()

    def close(self) -> None:
        close_pool()

    def publish_stream_points(self, delta: int) -> None:
        self.r.hincrby("rt:metrics:collector", "stream_points", delta)

    def write_latest(
        self,
        device_code: str,
        group_code: str,
        fields: dict[str, dict],
    ) -> None:
        """写 latest Hash。fields: {var_id: {"v": str, "t": int, "q": int}}"""
        key = latest_key(device_code, group_code)
        started = self._clock()
        try:
            mapping = encode_latest_mapping(fields)
        finally:
            self._metrics.record_timing("json_ms", (self._clock() - started) * 1000)

        started = self._clock()
        try:
            self.r.hset(key, mapping=mapping)
        finally:
            self._metrics.record_timing("redis_io_ms", (self._clock() - started) * 1000)
        # 不设 TTL，由设备管理操作显式清理

    def write_window(
        self,
        device_code: str,
        group_code: str,
        scan_rate_ms: int,
        frame: dict,
    ) -> None:
        """写 window List，保留最近 10 帧。frame: 完整帧 JSON 对象"""
        key = f"rt:window:{device_code}:{group_code}:{scan_rate_ms}"
        started = self._clock()
        try:
            encoded_frame = msgspec.json.encode(frame)
        finally:
            self._metrics.record_timing("json_ms", (self._clock() - started) * 1000)

        started = self._clock()
        pipe = self.r.pipeline(transaction=False)
        try:
            pipe.lpush(key, encoded_frame)
            pipe.ltrim(key, 0, 9)
            pipe.execute()
        finally:
            self._metrics.record_timing("redis_io_ms", (self._clock() - started) * 1000)

    def write_stream(
        self,
        device_code: str,
        device_type: str,
        group_code: str,
        scan_rate_ms: int,
        ts: int,
        config_version: int,
        values: dict[str, str],
        qualities: dict[str, int] | None = None,
    ) -> None:
        """写 stream Stream。values: {var_id: value_str}，已过滤 STRING 和 is_storage=0。

        Args:
            qualities: v2 质量码映射 {var_id: quality_code}。None 时产 v1（兼容渐进迁移），
                非 None 时产 v2（contract_version=2 + qualities 字段）。
        """
        key = stream_key(device_code, device_type, group_code, self.shard_count)
        started = self._clock()
        try:
            fields = build_stream_fields(
                device_code=device_code,
                device_type=device_type,
                group_code=group_code,
                scan_rate_ms=scan_rate_ms,
                ts=ts,
                config_version=config_version,
                values=values,
                qualities=qualities,
            )
        finally:
            self._metrics.record_timing("json_ms", (self._clock() - started) * 1000)

        started = self._clock()
        try:
            self.r.xadd(
                key,
                fields,
                maxlen=self._maxlen,
                approximate=True,
            )
        finally:
            self._metrics.record_timing("redis_io_ms", (self._clock() - started) * 1000)

    def write_window_stream(
        self,
        device_code: str,
        device_type: str,
        group_code: str,
        scan_rate_ms: int,
        ts: int,
        config_version: int,
        frame: dict,
        stream_values: dict[str, str],
        stream_qualities: dict[str, int] | None = None,
    ) -> None:
        """window + stream 合并进一个 pipeline（§7.5：每设备每周期往返 3→2）。

        stream_values 为空时只写 window。任一命令失败时 execute() 抛出，调用方据此
        跳过 latest，保住 latest-last 不变式。非事务 pipeline，语义与原先两次独立写一致。

        Args:
            stream_qualities: v2 质量码映射。None 时产 v1，非 None 时产 v2。
        """
        window_key = f"rt:window:{device_code}:{group_code}:{scan_rate_ms}"
        started = self._clock()
        try:
            encoded_frame = msgspec.json.encode(frame)
            stream_fields = (
                build_stream_fields(
                    device_code=device_code,
                    device_type=device_type,
                    group_code=group_code,
                    scan_rate_ms=scan_rate_ms,
                    ts=ts,
                    config_version=config_version,
                    values=stream_values,
                    qualities=stream_qualities,
                )
                if stream_values
                else None
            )
        finally:
            self._metrics.record_timing("json_ms", (self._clock() - started) * 1000)

        started = self._clock()
        pipe = self.r.pipeline(transaction=False)
        try:
            pipe.lpush(window_key, encoded_frame)
            pipe.ltrim(window_key, 0, 9)
            if stream_fields is not None:
                key = stream_key(device_code, device_type, group_code, self.shard_count)
                pipe.xadd(
                    key,
                    stream_fields,
                    maxlen=self._maxlen,
                    approximate=True,
                )
            pipe.execute()
        finally:
            self._metrics.record_timing("redis_io_ms", (self._clock() - started) * 1000)

        # 本地累计 Redis 有效点数（已过滤 is_storage=0/STRING），不访问 Redis；
        # 由 main.py _performance_log_loop 60s 上报一次，避免每批 HINCRBY 拖高 CPU。
        if stream_values:
            self._metrics.increment("stream_points_written", len(stream_values))
