"""Write-ahead SQLite queue and AOF-confirmed Redis delivery for central collection."""
from __future__ import annotations

import logging
import threading
import time
from collections import OrderedDict
from dataclasses import dataclass
from pathlib import Path

import msgspec
import redis
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
local changed = false
local ids = {}
local values = {}
local function flush_latest()
    if #ids == 0 then return end
    local old_values = redis.call('HMGET', KEYS[3], unpack(ids))
    for i = 1, #ids do
        local old = old_values[i]
        local value = values[i]
        if not old or tonumber(cjson.decode(old).t or 0) < tonumber(value.t) then
            redis.call('HSET', KEYS[3], ids[i], cjson.encode(value))
            changed = true
        end
    end
    ids = {}
    values = {}
end
for id, value in pairs(incoming) do
    ids[#ids + 1] = id
    values[#values + 1] = value
    if #ids == 256 then flush_latest() end
end
flush_latest()
if changed then
    redis.call('PUBLISH', 'rt:change', cjson.encode({device_code=ARGV[4], group_code=ARGV[5]}))
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


# One hash per never-reused producer session. high is contiguous; at most
# 1024 out-of-order sequence numbers are remembered as g:<seq> fields.
FRAME_LUA_V2 = r"""
local seq = tonumber(ARGV[6])
if not seq or seq < 1 or seq > 9007199254740991 or seq ~= math.floor(seq) then
    return redis.error_reply('invalid collector sequence')
end
local state_type = redis.call('TYPE', KEYS[1]).ok
if state_type ~= 'none' and state_type ~= 'hash' then
    return redis.error_reply('invalid collector receipt type')
end
local stream_type = redis.call('TYPE', KEYS[2]).ok
if stream_type ~= 'none' and stream_type ~= 'stream' then
    return redis.error_reply('invalid collector stream type')
end
local high = tonumber(redis.call('HGET', KEYS[1], 'high') or '0')
local gaps = tonumber(redis.call('HGET', KEYS[1], 'gaps') or '0')
if not high or not gaps or high < 0 or gaps < 0 then
    return redis.error_reply('invalid collector receipt state')
end
local gap_field = 'g:' .. ARGV[6]
local duplicate = seq <= high or redis.call('HEXISTS', KEYS[1], gap_field) == 1
if not duplicate and seq > high + 1 and gaps >= 1024 then
    return redis.error_reply('collector receipt gap limit reached')
end
-- Decode before XADD: a Lua error after XADD does not roll back the append.
local fields = cjson.decode(ARGV[1])
local incoming = cjson.decode(ARGV[2])
local frame = cjson.decode(ARGV[3])
if not duplicate then
    if #fields > 0 then redis.call('XADD', KEYS[2], '*', unpack(fields)) end
    if seq == high + 1 then
        high = seq
        while redis.call('HEXISTS', KEYS[1], 'g:' .. string.format('%.0f', high + 1)) == 1 do
            high = high + 1
            redis.call('HDEL', KEYS[1], 'g:' .. string.format('%.0f', high))
            gaps = gaps - 1
        end
        redis.call('HSET', KEYS[1], 'high', string.format('%.0f', high))
        redis.call('HSET', KEYS[1], 'gaps', tostring(gaps))
    else
        redis.call('HSET', KEYS[1], gap_field, '1')
        redis.call('HSET', KEYS[1], 'high', string.format('%.0f', high))
        redis.call('HSET', KEYS[1], 'gaps', tostring(gaps + 1))
    end
end
local changed = false
local ids = {}
local values = {}
local function flush_latest()
    if #ids == 0 then return end
    local old_values = redis.call('HMGET', KEYS[3], unpack(ids))
    for i = 1, #ids do
        local old = old_values[i]
        local value = values[i]
        if not old or tonumber(cjson.decode(old).t or 0) < tonumber(value.t) then
            redis.call('HSET', KEYS[3], ids[i], cjson.encode(value))
            changed = true
        end
    end
    ids = {}
    values = {}
end
for id, value in pairs(incoming) do
    ids[#ids + 1] = id
    values[#values + 1] = value
    if #ids == 256 then flush_latest() end
end
flush_latest()
if changed then
    redis.call('PUBLISH', 'rt:change', cjson.encode({device_code=ARGV[4], group_code=ARGV[5]}))
end
local recent = redis.call('LINDEX', KEYS[4], 0)
if not recent or tonumber(cjson.decode(recent).ts or 0) < tonumber(frame.ts or 0) then
    redis.call('LPUSH', KEYS[4], ARGV[3])
    redis.call('LTRIM', KEYS[4], 0, 9)
end
-- A duplicate also advances this connection's write offset for WAITAOF.
redis.call('HINCRBY', KEYS[1], 'fence', 1)
return duplicate and 0 or 1
"""


LATEST_LUA = r"""
local incoming = cjson.decode(ARGV[1])
local changed = false
for id, value in pairs(incoming) do
    local old = redis.call('HGET', KEYS[1], id)
    if not old or tonumber(cjson.decode(old).t or 0) < tonumber(value.t) then
        redis.call('HSET', KEYS[1], id, cjson.encode(value))
        changed = true
    end
end
if changed then
    redis.call('PUBLISH', 'rt:change', cjson.encode({device_code=ARGV[2], group_code=ARGV[3]}))
end
return changed and 1 or 0
"""


@dataclass
class _LatestState:
    fields: dict
    version: int
    payload_bytes: int


class DurableRedisWriter(RedisWriter):
    storage_path = "sqlite_outbox_redis_aof"
    _MAX_REDIS_WINDOW_BYTES = 64 * 1024 * 1024
    _MAX_REDIS_SINGLE_BYTES = 16 * 1024 * 1024

    def __init__(self, r, config, *, outbox_path: Path, metrics=None):
        super().__init__(r, config, metrics)
        self._pending_event = threading.Event()
        self.outbox = CollectorRetryOutbox(
            outbox_path, metrics=self._metrics, on_commit=self._pending_event.set,
        )
        self._pending_frame = threading.local()
        self._stop = threading.Event()
        self._thread = None
        self._latest_thread = None
        self._latest_condition = threading.Condition()
        self._latest_pending: OrderedDict[tuple[str, str], _LatestState] = OrderedDict()
        self._latest_bytes = 0
        self._latest_version = 0
        self._latest_overflow_logged_at = 0.0
        self._script_shas = {}

    def ping(self):
        if self._thread is None:
            self._latest_thread = threading.Thread(target=self._latest_loop, name="collector-latest", daemon=True)
            self._latest_thread.start()
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
        try:
            self._queue_latest(device_code, group_code, fields)
        except Exception:
            logger.exception("latest fast lane enqueue failed; history remains in durable outbox")

    def _queue_latest(self, device_code, group_code, fields):
        if not fields:
            return
        key = (device_code, group_code)
        with self._latest_condition:
            previous = self._latest_pending.get(key)
            merged = dict(previous.fields) if previous is not None else {}
            for point_id, value in fields.items():
                old = merged.get(point_id)
                if old is None or int(old.get("t", 0)) < int(value.get("t", 0)):
                    merged[point_id] = value
            if previous is not None and merged == previous.fields:
                return
            payload_bytes = len(msgspec.json.encode(merged)) + len(device_code.encode()) + len(group_code.encode()) + 64
            next_bytes = self._latest_bytes - (previous.payload_bytes if previous else 0) + payload_bytes
            if (previous is None and len(self._latest_pending) >= 1024) or next_bytes > 8 * 1024 * 1024:
                self._metrics.increment("latest_fastlane_deferred_frames")
                now = time.monotonic()
                if now - self._latest_overflow_logged_at >= 10:
                    logger.warning("latest fast lane full; historical outbox remains authoritative")
                    self._latest_overflow_logged_at = now
                return
            self._latest_version += 1
            self._latest_pending[key] = _LatestState(merged, self._latest_version, payload_bytes)
            self._latest_pending.move_to_end(key)
            self._latest_bytes = next_bytes
            self._metrics.set_gauge("latest_fastlane_groups", len(self._latest_pending))
            self._metrics.set_gauge("latest_fastlane_bytes", self._latest_bytes)
            self._latest_condition.notify()

    def _latest_client(self):
        if isinstance(self.r, redis.Redis):
            return redis.Redis(connection_pool=self.r.connection_pool, single_connection_client=True)
        return self.r

    def _latest_loop(self):
        client = None
        try:
            for device_code, group_code, encoded in self.outbox.iter_pending_latest():
                if self._stop.is_set():
                    return
                try:
                    self._queue_latest(device_code, group_code, msgspec.json.decode(encoded))
                except Exception:
                    logger.exception("latest fast lane could not restore device=%s group=%s; history remains queued",
                                     device_code, group_code)
            while not self._stop.is_set():
                with self._latest_condition:
                    while not self._latest_pending and not self._stop.is_set():
                        self._latest_condition.wait(1)
                    if self._stop.is_set():
                        return
                    key, state = next(iter(self._latest_pending.items()))
                    payload = msgspec.json.encode(state.fields)
                    version = state.version
                try:
                    if client is None:
                        client = self._latest_client()
                    started = time.perf_counter()
                    client.eval(LATEST_LUA, 1, f"rt:latest:{key[0]}:{key[1]}", payload, key[0], key[1])
                    duration_ms = (time.perf_counter() - started) * 1000
                except Exception:
                    self._metrics.increment("latest_fastlane_retries")
                    logger.warning("latest fast lane deferred; historical outbox retained", exc_info=True)
                    with self._latest_condition:
                        if key in self._latest_pending:
                            self._latest_pending.move_to_end(key)
                    if client is not None and client is not self.r:
                        try:
                            client.close()
                        except Exception:
                            logger.exception("latest fast lane connection close failed")
                    client = None
                    self._stop.wait(0.5)
                    continue
                try:
                    self._metrics.record_timing("latest_fastlane_redis_ms", duration_ms)
                except Exception:
                    logger.exception("latest fast lane metric failed")
                with self._latest_condition:
                    current = self._latest_pending.get(key)
                    if current is not None and current.version == version:
                        self._latest_bytes -= current.payload_bytes
                        del self._latest_pending[key]
                    elif current is not None:
                        self._latest_pending.move_to_end(key)
                    self._metrics.set_gauge("latest_fastlane_groups", len(self._latest_pending))
                    self._metrics.set_gauge("latest_fastlane_bytes", self._latest_bytes)
                self._metrics.increment("latest_fastlane_sent_groups")
        except Exception:
            logger.exception("latest fast lane stopped; historical outbox remains available")
        finally:
            if client is not None and client is not self.r:
                client.close()

    def write_durable_frames(self, rows):
        max_batch_bytes = self._MAX_REDIS_WINDOW_BYTES
        max_single_bytes = self._MAX_REDIS_SINGLE_BYTES
        batch = []
        batch_bytes = 0
        for row in rows:
            values = msgspec.json.decode(row["stream_values_json"])
            qualities = msgspec.json.decode(row["stream_qualities_json"]) if row["stream_qualities_json"] is not None else None
            fields = build_stream_fields(device_code=row["device_code"], device_type=row["device_type"],
                group_code=row["group_code"], scan_rate_ms=row["scan_rate_ms"], ts=row["ts_ms"],
                config_version=row["config_version"], values=values, qualities=qualities) if values else {}
            flat = []
            for k, v in fields.items():
                flat.extend((k, v.decode() if isinstance(v, bytes) else v))
            session_id = row["session_id"] if "session_id" in row.keys() else None
            if session_id is None:
                script = FRAME_LUA
                receipt_key = "collector:receipt:" + row["id"]
                sequence_args = ()
            else:
                script = FRAME_LUA_V2
                receipt_key = "collector:receipt:v2:" + session_id
                sequence_args = (row["seq"],)
            args = (4, receipt_key,
                stream_key(row["device_code"], row["device_type"], row["group_code"], self.shard_count),
                f"rt:latest:{row['device_code']}:{row['group_code']}",
                f"rt:window:{row['device_code']}:{row['group_code']}:{row['scan_rate_ms']}",
                msgspec.json.encode(flat), row["latest_fields_json"], row["frame_json"],
                row["device_code"], row["group_code"], *sequence_args)
            # EVALSHA transmits a 40-byte digest, not the Lua source per frame.
            wire_bytes = 40 + sum(
                len(arg if isinstance(arg, bytes) else str(arg).encode()) + 64 for arg in args
            )
            if wire_bytes > max_single_bytes:
                raise RuntimeError(f"collector frame exceeds finite Redis single-frame limit; retained id={row['id']} bytes={wire_bytes}")
            if batch and (len(batch) >= 4096 or batch_bytes + wire_bytes > max_batch_bytes):
                self._deliver_window(batch, batch_bytes)
                batch = []
                batch_bytes = 0
            batch.append((script, args))
            batch_bytes += wire_bytes
        if batch:
            self._deliver_window(batch, batch_bytes)
        # 注意：history_frames_confirmed 不在这里累加。
        # 本方法返回后，调用方（replay_pending）才删除 SQLite 行并提交。
        # 若在这里加计数，而随后的本地 DELETE 事务失败，同一批帧下次重放会再加一次，
        # 实际唯一帧 100 而计数变 200——Redis 幂等并不等于进程内计数器幂等。
        # 计数改由调用方在"删除已提交"的同一事务内累加，详见 collector_outbox.replay_pending。
        self._metrics.increment("stream_points_written", sum(len(msgspec.json.decode(r["stream_values_json"])) for r in rows))

    def _deliver_window(self, batch, batch_bytes):
        # One pipeline owns one connection through all writes and its WAITAOF.
        scripts = {script for script, _ in batch}
        for script in scripts:
            if script not in self._script_shas:
                self._script_shas[script] = self.r.script_load(script)
        for attempt in range(2):
            try:
                with self.r.pipeline(transaction=False) as pipe:
                    for script, args in batch:
                        pipe.evalsha(self._script_shas[script], *args)
                    pipe.execute_command("WAITAOF", 1, 0, 2000)
                    started = time.perf_counter()
                    results = pipe.execute()
                    duration_ms = (time.perf_counter() - started) * 1000
                    if not results or not isinstance(results[-1], (list, tuple)) or int(results[-1][0]) < 1:
                        raise RuntimeError("Redis AOF persistence not confirmed; backlog retained")
                break
            except redis.exceptions.ResponseError as exc:
                if attempt or not (
                    isinstance(exc, redis.exceptions.NoScriptError)
                    or "NOSCRIPT" in str(exc).upper()
                ):
                    raise
                # Earlier EVALSHA commands may have succeeded. Keep their original
                # identities and retry the whole window after restoring script cache.
                for script in scripts:
                    self._script_shas[script] = self.r.script_load(script)
        try:
            self._metrics.record_timing("redis_aof_batch_ms", duration_ms)
            self._metrics.set_gauge("redis_batch_frames", len(batch))
            self._metrics.set_gauge("redis_batch_bytes", batch_bytes)
            self._metrics.increment("redis_confirmed_batches")
            self._metrics.increment("redis_confirmed_bytes", batch_bytes)
        except Exception:
            logger.exception("Redis batch metric failed after AOF confirmation")

    def _replay_loop(self):
        last_stats = 0.0
        while not self._stop.is_set():
            self._pending_event.clear()
            now = time.monotonic()
            if now - last_stats >= 10:
                try:
                    stats = self.outbox.stats()
                    for name, value in stats.items():
                        self._metrics.set_gauge("outbox_" + name, value)
                except Exception:
                    logger.exception("outbox telemetry failed")
                last_stats = now
            try:
                count = self.outbox.replay_pending(self)
            except Exception:
                logger.warning("durable delivery deferred; local backlog retained", exc_info=True)
                self._stop.wait(1)
                continue
            if not count:
                self._pending_event.wait(1)

    def close(self):
        self._stop.set()
        self._pending_event.set()
        with self._latest_condition:
            self._latest_condition.notify_all()
        if self._thread is not None:
            self._thread.join()
        if self._latest_thread is not None:
            self._latest_thread.join()
        self.outbox.close()
        super().close()
