"""Isolate JSON timestamp decode cost on the disposable Redis instance only."""
from __future__ import annotations

import json
from pathlib import Path
import statistics

import redis

import profile_collector_frame_lua as benchmark
from profile_collector_frame_lua import URL, run_variant
from storage.durable_redis_writer import FRAME_LUA_V2

OUTPUT = Path("/tmp/dw-frame-lua-decode-profile-20260924.json")
benchmark.FRAMES = 300


def variants():
    script = FRAME_LUA_V2
    frame_decode = "local frame = cjson.decode(ARGV[3])"
    recent_decode = "tonumber(cjson.decode(recent).ts or 0)"
    assert script.count(frame_decode) == 1
    assert script.count(recent_decode) == 1
    frame_arg = script.replace(frame_decode, "local frame = {ts = tonumber(ARGV[7])}")
    recent_arg = script.replace(recent_decode, "tonumber(ARGV[8])")
    both_arg = frame_arg.replace(recent_decode, "tonumber(ARGV[8])")
    start = script.index("local changed = false")
    end = script.index("local recent =", start)
    skip_latest = script[:start] + "local changed = false\n" + script[end:]
    return {
        "hmget_baseline": script,
        "hmget_frame_ts_arg": frame_arg,
        "hmget_recent_ts_arg": recent_arg,
        "hmget_both_ts_args": both_arg,
        "skip_latest_baseline": skip_latest,
        "skip_latest_frame_ts_arg": skip_latest.replace(
            frame_decode, "local frame = {ts = tonumber(ARGV[7])}"
        ),
        "skip_latest_recent_ts_arg": skip_latest.replace(
            recent_decode, "tonumber(ARGV[8])"
        ),
        "skip_latest_both_ts_args": skip_latest.replace(
            frame_decode, "local frame = {ts = tonumber(ARGV[7])}"
        ).replace(recent_decode, "tonumber(ARGV[8])"),
    }


def main():
    client = redis.Redis.from_url(URL, socket_timeout=10)
    assert client.ping()
    previous = client.config_get("appendfsync")["appendfsync"]
    choices = variants()
    order = list(choices) + list(reversed(choices))
    report = {
        "redis_url_host": "dw-v2-redis-fault-20260924",
        "redis_version": client.info("server")["redis_version"],
        "original_appendfsync": previous,
        "profile_appendfsync": "everysec",
        "method": (
            "1000-point frames, prepopulated newer latest values; 300 frames "
            "per variant twice, forward/reverse order. Test-only variants "
            "substitute known monotonic frame and prior-window timestamps "
            "as ARGV[7]/ARGV[8]; all variants retain identical XADD, receipt, "
            "LINDEX, LPUSH and LTRIM. Skip-latest variants are unsafe in production."
        ),
        "runs": [],
    }
    try:
        client.config_set("appendfsync", "everysec")
        for name in order:
            report["runs"].append(
                run_variant(client, name, choices[name], include_ts_args=True)
            )
        means = {
            name: statistics.mean(
                run["eval_ms_mean"] for run in report["runs"]
                if run["variant"] == name
            )
            for name in choices
        }
        report["mean_eval_ms_by_variant"] = means
        for prefix in ("hmget", "skip_latest"):
            baseline = means[prefix + "_baseline"]
            report[prefix + "_frame_decode_delta_ms"] = (
                baseline - means[prefix + "_frame_ts_arg"]
            )
            report[prefix + "_recent_decode_delta_ms"] = (
                baseline - means[prefix + "_recent_ts_arg"]
            )
            report[prefix + "_both_decode_delta_ms"] = (
                baseline - means[prefix + "_both_ts_args"]
            )
    finally:
        client.config_set("appendfsync", previous)
        report["restored_appendfsync"] = client.config_get("appendfsync")["appendfsync"]
        client.close()
        OUTPUT.write_text(
            json.dumps(report, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
