"""Write-ahead SQLite queue and AOF-confirmed Redis delivery for central collection."""
from __future__ import annotations

import logging
import threading
from pathlib import Path

import msgspec
from data_watch_shared.stream_envelope import build_stream_fields, stream_key
from storage.collector_outbox import CollectorRetryOutbox
from storage.redis_writer import RedisWriter

logger = logging.getLogger("collector.durable")

FRAME_LUA = r"""
local receipt = redis.call('GET', KEYS[1])
if not receipt then
    local fields = cjson.decode(ARGV[1])
    if #fields > 0 then redis.call('XADD', KEYS[2], '*', unpack(fields)) end
    -- Save receipt immediately after history append: later runtime errors must
    -- not append the same historical sample twice on retry.
    redis.call('SET', KEYS[1], '1', 'EX', 604800)
end
local incoming = cjson.decode(ARGV[2])
for id, value in pairs(incoming) do
    local old = redis.call('HGET', KEYS[3], id)
    if not old or tonumber(cjson.decode(old).t or 0) <= tonumber(value.t) then
        redis.call('HSET', KEYS[3], id, cjson.encode(value))
    end
end
local frame = cjson.decode(ARGV[3])
local recent = redis.call('LINDEX', KEYS[4], 0)
if not recent or tonumber(cjson.decode(recent).ts or 0) < tonumber(frame.ts or 0) then
    redis.call('LPUSH', KEYS[4], ARGV[3])
    redis.call('LTRIM', KEYS[4], 0, 9)
end
-- Force a write even on duplicate replay; WAITAOF covers the global prefix.
redis.call('SET', KEYS[1], '1', 'EX', 604800)
return 1
"""

class DurableRedisWriter(RedisWriter):
    storage_path = "sqlite_outbox_redis_aof"

    def __init__(self, r, config, *, outbox_path: Path, metrics=None):
        super().__init__(r, config, metrics)
        self.outbox = CollectorRetryOutbox(outbox_path)
        self._pending_frame = threading.local()
        self._stop = threading.Event()
        self._thread = None

    def ping(self):
        if self._thread is None:
            self._thread = threading.Thread(target=self._replay_loop, name="collector-outbox", daemon=True)
            self._thread.start()
        super().ping()

    def write_window_stream(self, device_code, device_type, group_code, scan_rate_ms, ts,
                            config_version, frame, stream_values, stream_qualities=None):
        self._pending_frame.value = dict(device_code=device_code, device_type=device_type,
            group_code=group_code, scan_rate_ms=scan_rate_ms, ts_ms=ts, config_version=config_version,
            frame=frame, stream_values=stream_values, stream_qualities=stream_qualities)

    def write_window(self, device_code, group_code, scan_rate_ms, frame):
        # HTTP interface ingestion uses window -> stream -> latest separately.
        self.write_window_stream(device_code, "realtime", group_code, scan_rate_ms,
            int(frame["ts"]), 0, frame, {}, None)

    def write_stream(self, device_code, device_type, group_code, scan_rate_ms, ts,
                     config_version, values, qualities=None):
        pending = getattr(self._pending_frame, "value", None)
        if pending is None or (pending["device_code"], pending["group_code"]) != (device_code, group_code):
            raise ValueError("stream requires a matching window frame")
        pending.update(device_type=device_type, ts_ms=ts, config_version=config_version,
                       stream_values=values, stream_qualities=qualities)

    def write_latest(self, device_code, group_code, fields):
        pending = getattr(self._pending_frame, "value", None)
        self._pending_frame.value = None
        if pending is None:
            # Bootstrap/status-only writes have no history to retain.
            return super().write_latest(device_code, group_code, fields)
        if (pending["device_code"], pending["group_code"]) != (device_code, group_code):
            raise ValueError("frame/latest identity mismatch")
        self.outbox.record_failure(**pending, latest_fields=fields)

    def write_durable_frames(self, rows):
        with self.r.pipeline(transaction=False) as pipe:
            for row in rows:
                values = msgspec.json.decode(row["stream_values_json"])
                qualities = msgspec.json.decode(row["stream_qualities_json"]) if row["stream_qualities_json"] is not None else None
                fields = build_stream_fields(device_code=row["device_code"], device_type=row["device_type"],
                    group_code=row["group_code"], scan_rate_ms=row["scan_rate_ms"], ts=row["ts_ms"],
                    config_version=row["config_version"], values=values, qualities=qualities) if values else {}
                flat = []
                for k, v in fields.items():
                    flat.extend((k, v.decode() if isinstance(v, bytes) else v))
                pipe.eval(FRAME_LUA, 4, "collector:receipt:" + row["id"],
                    stream_key(row["device_code"], row["device_type"], row["group_code"], self.shard_count),
                    f"rt:latest:{row['device_code']}:{row['group_code']}",
                    f"rt:window:{row['device_code']}:{row['group_code']}:{row['scan_rate_ms']}",
                    msgspec.json.encode(flat), row["latest_fields_json"], row["frame_json"])
            pipe.execute_command("WAITAOF", 1, 0, 2000)
            results = pipe.execute()
            if not results or not isinstance(results[-1], (list, tuple)) or int(results[-1][0]) < 1:
                raise RuntimeError("Redis AOF persistence not confirmed; backlog retained")
        self._metrics.increment("stream_points_written", sum(len(msgspec.json.decode(r["stream_values_json"])) for r in rows))

    def _replay_loop(self):
        while not self._stop.is_set():
            try:
                count = self.outbox.replay_pending(self)
            except Exception:
                logger.warning("durable delivery deferred; local backlog retained", exc_info=True)
                count = 0
            if not count:
                self._stop.wait(1)

    def close(self):
        self._stop.set()
        if self._thread is not None:
            self._thread.join()
        self.outbox.close()
        super().close()
