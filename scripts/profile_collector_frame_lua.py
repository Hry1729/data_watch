"""Measure the redundant historical latest loop on a disposable Redis only."""
from __future__ import annotations

import json
from pathlib import Path
import statistics
import time
from uuid import uuid4

import redis

import sys
ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector")]
from data_watch_shared.stream_envelope import build_stream_fields
from storage.durable_redis_writer import FRAME_LUA_V2

URL = "redis://dw-v2-redis-fault-20260924:6379/0"
OUTPUT = Path("/tmp/dw-frame-lua-profile-20260924.json")
POINTS = 1000
FRAMES = 100


def percentile(values, fraction):
    return sorted(values)[min(len(values) - 1, int(len(values) * fraction))]


def cpu_seconds(client):
    info = client.info("cpu")
    return float(info["used_cpu_user"]) + float(info["used_cpu_sys"])


def run_variant(client, name, script, include_ts_args=False):
    token = uuid4().hex
    keys = [f"dw:frame-profile:{token}:{suffix}" for suffix in
            ("receipt", "stream", "latest", "window")]
    base = int(time.time() * 1000)
    values = {str(point): str(point) for point in range(1, POINTS + 1)}
    qualities = {point: 192 for point in values}
    latest = {point: json.dumps({"v": value, "t": base + 10000, "q": 192})
              for point, value in values.items()}
    client.hset(keys[2], mapping=latest)
    digest = client.script_load(script)
    calls = []
    for seq in range(1, FRAMES + 1):
        ts = base + seq
        fields = build_stream_fields(
            device_code="profile", device_type="profile", group_code="g",
            scan_rate_ms=100, ts=ts, config_version=1,
            values=values, qualities=qualities,
        )
        flat = []
        for key, value in fields.items():
            flat.extend((key, value.decode() if isinstance(value, bytes) else value))
        incoming = {point: {"v": value, "t": ts, "q": 192}
                    for point, value in values.items()}
        calls.append((json.dumps(flat), json.dumps(incoming),
                      json.dumps({"ts": ts, "values": values})))
    timings = []
    try:
        cpu_before = cpu_seconds(client)
        elapsed_start = time.perf_counter()
        for seq, args in enumerate(calls, 1):
            started = time.perf_counter()
            extra = (base + seq, base + seq - 1) if include_ts_args else ()
            client.evalsha(digest, 4, *keys, *args, "profile", "g", seq, *extra)
            timings.append((time.perf_counter() - started) * 1000)
        elapsed = time.perf_counter() - elapsed_start
        cpu = cpu_seconds(client) - cpu_before
        assert client.xlen(keys[1]) == FRAMES
        assert client.hlen(keys[2]) == POINTS
        assert client.hget(keys[0], "high") == str(FRAMES).encode()
        return {"variant": name, "frames": FRAMES, "points_per_frame": POINTS,
                "elapsed_s": elapsed, "redis_cpu_s": cpu,
                "eval_ms_p50": statistics.median(timings),
                "eval_ms_p95": percentile(timings, .95),
                "eval_ms_mean": statistics.mean(timings),
                "stream_len": FRAMES}
    finally:
        client.delete(*keys)


def main():
    client = redis.Redis.from_url(URL, socket_timeout=10)
    assert client.ping()
    previous = client.config_get("appendfsync")["appendfsync"]
    start = FRAME_LUA_V2.index("local changed = false")
    end = FRAME_LUA_V2.index("local recent =", start)
    no_latest_loop = FRAME_LUA_V2[:start] + "local changed = false" + chr(10) + FRAME_LUA_V2[end:]
    original_loop = FRAME_LUA_V2[start:end]
    batched_loop = """local ids = {}
local values = {}
for id, value in pairs(incoming) do
    ids[#ids + 1] = id
    values[#values + 1] = value
end
local previous = {}
for first = 1, #ids, 256 do
    local batch = {}
    for i = first, math.min(first + 255, #ids) do
        batch[#batch + 1] = ids[i]
    end
    local found = redis.call('HMGET', KEYS[3], unpack(batch))
    for i = 1, #found do
        previous[first + i - 1] = found[i]
    end
end
local changed = false
for i = 1, #ids do
    local id = ids[i]
    local value = values[i]
    local old = previous[i]
    if not old or tonumber(cjson.decode(old).t or 0) < tonumber(value.t) then
        redis.call('HSET', KEYS[3], id, cjson.encode(value))
        changed = true
    end
end
if changed then
    redis.call('PUBLISH', 'rt:change', cjson.encode({device_code=ARGV[4], group_code=ARGV[5]}))
end
"""
    batched_get_script = FRAME_LUA_V2.replace(original_loop, batched_loop, 1)
    report = {"redis_url_host": "dw-v2-redis-fault-20260924",
              "redis_version": client.info("server")["redis_version"],
              "original_appendfsync": previous, "profile_appendfsync": "everysec",
              "method": "prepopulate all 1000 latest points at newer timestamps; identical Redis XADD/receipt/window work; skip-loop script is measurement only",
              "runs": []}
    try:
        client.config_set("appendfsync", "everysec")
        for name, script in (("full", FRAME_LUA_V2), ("hmget_256", batched_get_script),
                             ("skip_latest_loop", no_latest_loop),
                             ("skip_latest_loop", no_latest_loop),
                             ("hmget_256", batched_get_script), ("full", FRAME_LUA_V2)):
            report["runs"].append(run_variant(client, name, script))
        means = {name: statistics.mean(run["eval_ms_mean"] for run in report["runs"]
                                       if run["variant"] == name)
                 for name in ("full", "hmget_256", "skip_latest_loop")}
        report["mean_eval_ms_by_variant"] = means
        report["measured_full_minus_hmget_ms"] = means["full"] - means["hmget_256"]
        report["measured_full_minus_skip_ms"] = means["full"] - means["skip_latest_loop"]
    finally:
        client.config_set("appendfsync", previous)
        report["restored_appendfsync"] = client.config_get("appendfsync")["appendfsync"]
        client.close()
        OUTPUT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + chr(10),
                          encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
