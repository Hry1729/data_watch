"""Measure 1000-point frame JSON parsing inside disposable Redis Lua."""
from __future__ import annotations

import json
from pathlib import Path
import statistics
import time

import msgspec
import redis

URL = "redis://dw-v2-redis-fault-20260924:6379/0"
OUTPUT = Path("/tmp/dw-json-decode-profile-20260924.json")
REPEATS = 20
DECODES_PER_REPEAT = 100

DECODE_LUA = """
local result = 0
for i = 1, tonumber(ARGV[2]) do
    local frame = cjson.decode(ARGV[1])
    result = result + tonumber(frame.ts or 0)
end
return result
"""

CONTROL_LUA = """
local result = 0
for i = 1, tonumber(ARGV[2]) do
    result = result + tonumber(ARGV[3])
end
return result
"""


def main():
    client = redis.Redis.from_url(URL, socket_timeout=10)
    assert client.ping()
    frame = msgspec.json.encode({
        "ts": 1,
        "values": {str(point): str(point) for point in range(1, 1001)},
    })
    scripts = {
        "control": client.script_load(CONTROL_LUA),
        "decode_frame": client.script_load(DECODE_LUA),
    }
    samples = {name: [] for name in scripts}
    try:
        for cycle in range(REPEATS):
            order = list(scripts) if cycle % 2 == 0 else list(reversed(scripts))
            for name in order:
                started = time.perf_counter_ns()
                result = client.evalsha(
                    scripts[name], 0, frame, DECODES_PER_REPEAT, 1
                )
                elapsed_us = (time.perf_counter_ns() - started) / 1000
                assert result == DECODES_PER_REPEAT
                samples[name].append(elapsed_us / DECODES_PER_REPEAT)
        medians = {name: statistics.median(values)
                   for name, values in samples.items()}
        report = {
            "redis_url_host": "dw-v2-redis-fault-20260924",
            "redis_version": client.info("server")["redis_version"],
            "frame_json_bytes": len(frame),
            "points_per_frame": 1000,
            "repeats": REPEATS,
            "decodes_per_repeat": DECODES_PER_REPEAT,
            "microseconds_per_decode_samples": samples,
            "median_microseconds_per_decode": medians,
            "net_decode_microseconds_median": (
                medians["decode_frame"] - medians["control"]
            ),
            "method": (
                "Read-only Lua EVALSHA client timings (100 decodes per call); one full 1000-point "
                "frame JSON decode per loop; alternating control/decode order. "
                "This measures parse cost only, not XADD or latest updates."
            ),
        }
        OUTPUT.write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        print(json.dumps(report, ensure_ascii=False))
    finally:
        client.close()


if __name__ == "__main__":
    main()
