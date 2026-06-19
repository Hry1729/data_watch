"""Redis 写入封装（COL-2）：latest Hash / window List / stream Stream。"""

from __future__ import annotations

import json
import time
import zlib

import redis

from monitor.performance import PerformanceMetrics


class RedisWriter:
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

    def write_latest(
        self,
        device_code: str,
        group_code: str,
        fields: dict[str, dict],
    ) -> None:
        """写 latest Hash。fields: {var_id: {"v": str, "t": int, "q": int}}"""
        key = f"rt:latest:{device_code}:{group_code}"
        started = self._clock()
        try:
            mapping = {
                str(k): json.dumps(v, separators=(",", ":"))
                for k, v in fields.items()
            }
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
            encoded_frame = json.dumps(frame, separators=(",", ":"))
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
    ) -> None:
        """写 stream Stream。values: {var_id: value_str}，已过滤 STRING 和 is_storage=0"""
        # 稳定哈希：内置 hash(str) 受 PYTHONHASHSEED 影响每进程不同，
        # 会导致 collector 重启后同设备落到不同 shard。crc32 进程间稳定。
        shard_no = zlib.crc32(device_code.encode("utf-8")) % self.shard_count
        stream_key = f"rt:stream:{device_type}:{group_code}:{shard_no}"
        started = self._clock()
        try:
            encoded_values = json.dumps(values, separators=(",", ":"))
        finally:
            self._metrics.record_timing("json_ms", (self._clock() - started) * 1000)

        started = self._clock()
        try:
            self.r.xadd(
                stream_key,
                {
                    "device_code": device_code,
                    "device_type": device_type,
                    "group_code": group_code,
                    "scan_rate": str(scan_rate_ms),
                    "ts": str(ts),
                    "config_version": str(config_version),
                    "values": encoded_values,
                },
                maxlen=self._maxlen,
                approximate=True,
            )
        finally:
            self._metrics.record_timing("redis_io_ms", (self._clock() - started) * 1000)
