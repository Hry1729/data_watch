"""Isolated Docker integration/soak test; synthetic inputs, real storage pipeline.

Run only in the dedicated dw-perf runner. Never reads deployment credentials.
Creates unique test databases and preserves them for inspection.
"""
from __future__ import annotations

import argparse
import asyncio
from concurrent.futures import ThreadPoolExecutor
import json
import logging
from pathlib import Path
import statistics
import sys
import time
from types import SimpleNamespace
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector"), str(ROOT / "writer")]

import psutil
import pymysql
import redis
import taosws
from consumer import StreamConsumer
from data_watch_shared.stream_envelope import stream_key
from mysql_mapper import MysqlMapper
from monitor.performance import TimingHistogram
from storage.durable_redis_writer import DurableRedisWriter
from tdengine_writer import TDengineWriter


def percentile(values, fraction):
    return sorted(values)[min(len(values) - 1, int(len(values) * fraction))] if values else 0


async def run(args):
    token = uuid4().hex[:10]
    database = f"dw_pipeline_{token}"
    device_type, group = f"perf{token}", "g"
    stable = f"sd{device_type}"
    r = redis.Redis(host="perf-redis", decode_responses=True, socket_timeout=10)
    assert r.ping()
    mysql = pymysql.connect(host="perf-mysql", user="root", autocommit=True)
    devices = [f"pf{token}d{i}" for i in range(args.devices)]
    with mysql.cursor() as cur:
        cur.execute(f"CREATE DATABASE `{database}`")
        cur.execute(f"USE `{database}`")
        cur.execute("CREATE TABLE point_storage_mapping (point_id BIGINT, td_column_name VARCHAR(32), "
                    "var_type VARCHAR(32), group_code VARCHAR(32), device_code VARCHAR(32), "
                    "device_type_code VARCHAR(32), stable_name VARCHAR(64), child_table VARCHAR(32))")
        for device in devices:
            cur.executemany("INSERT INTO point_storage_mapping VALUES (%s,%s,%s,%s,%s,%s,%s,%s)",
                            [(p, f"p{p}", "float", group, device, device_type, stable, device)
                             for p in range(1, args.points + 1)])
    config = {
        "tdengine": {"host": "perf-td", "port": 6041, "user": "root", "password": "taosdata", "database": database},
        "redis": {"group_name": "writer", "consumer_name": f"perf-{token}"},
        "writer": {"flush_interval_ms": 100, "batch_count": 200, "max_buffer_rows": 1000,
                   "max_buffer_bytes": 32 * 1024 * 1024, "pending_min_idle_ms": 1000,
                   "device_types": [device_type], "pending_drain_interval_s": 1},
    }
    commit_latency = TimingHistogram((100, 200, 300, 500, 1000, 1500, 2000, 3000, 5000, 10000, float("inf")))
    class MeasuredWriter(TDengineWriter):
        async def write_multi_table(self, rows):
            outcomes = await super().write_multi_table(rows)
            confirmed_ms = time.time() * 1000
            for indices, status, _ in outcomes:
                if status == "landed":
                    for index in indices:
                        commit_latency.record(confirmed_ms - rows[index]["ts"])
            return outcomes
    td = MeasuredWriter()
    await td.connect(config)
    consumer = StreamConsumer(config, MysqlMapper(mysql), td, r)
    keys = sorted({stream_key(d, device_type, group, 4) for d in devices})
    consume_task = asyncio.create_task(consumer.run_loop(keys))
    collector = DurableRedisWriter(r, SimpleNamespace(redis=SimpleNamespace(
        stream_shard_count=4, stream_maxlen=0, stream_warn_entries=50000)),
        outbox_path=Path(f"/tmp/{database}.sqlite"))
    collector.ping()
    samples, submit_ms, pacing_ms, loop_lag_ms = [], [], [], []
    done = asyncio.Event()
    generated = 0
    started = time.monotonic()
    base_ts = int(time.time() * 1000)

    def submit(device, tick):
        ts = base_ts + tick * args.period_ms
        values = {str(p): str(p + tick) for p in range(1, args.points + 1)}
        quality = {p: 192 for p in values}
        latest = {p: {"v": v, "t": ts, "q": 192} for p, v in values.items()}
        before = time.monotonic()
        collector.write_window_stream(device, device_type, group, args.period_ms, ts, 1,
                                      {"ts": ts, "values": values}, values, quality)
        collector.write_latest(device, group, latest)
        return (time.monotonic() - before) * 1000

    def state():
        pending, lag, unknown = 0, 0, 0
        for key in keys:
            for info in r.xinfo_groups(key):
                if info["name"] == "writer":
                    pending += info["pending"]
                    if info.get("lag") is None:
                        unknown += 1
                    else:
                        lag += info["lag"]
        return {"elapsed_s": round(time.monotonic() - started, 3),
                "generated": generated, "outbox": collector.outbox.stats(),
                "pending": pending, "lag": None if unknown else lag,
                "unknown_lag_streams": unknown, "rss_bytes": psutil.Process().memory_info().rss,
                "writer": consumer._metrics.snapshot(), "collector": collector._metrics.snapshot()}

    async def monitor():
        while not done.is_set():
            await asyncio.sleep(1)
            snapshot = await asyncio.to_thread(state)
            samples.append(snapshot)
            if len(samples) % 30 == 0:
                print(json.dumps({"progress": snapshot}, ensure_ascii=False), flush=True)

    async def loop_monitor():
        while not done.is_set():
            before = time.monotonic()
            await asyncio.sleep(.05)
            loop_lag_ms.append(max(0, (time.monotonic() - before - .05) * 1000))

    monitors = [asyncio.create_task(monitor()), asyncio.create_task(loop_monitor())]
    try:
        with ThreadPoolExecutor(max_workers=min(32, args.devices)) as pool:
            loop = asyncio.get_running_loop()
            tick = 0
            while time.monotonic() - started < args.seconds:
                pacing_ms.append(max(0, (time.monotonic() - started) * 1000 - tick * args.period_ms))
                submit_ms.extend(await asyncio.gather(*[
                    loop.run_in_executor(pool, submit, device, tick) for device in devices]))
                generated += len(devices)
                tick += 1
                if consume_task.done():
                    consume_task.result()
                await asyncio.sleep(max(0, started + tick * args.period_ms / 1000 - time.monotonic()))
        production_s = time.monotonic() - started
        production_end = await asyncio.to_thread(state)
        drain_deadline = time.monotonic() + args.drain_seconds
        while time.monotonic() < drain_deadline:
            final = await asyncio.to_thread(state)
            if (final["outbox"]["count"] == 0 and final["pending"] == 0
                    and final["lag"] == 0 and not consumer._buffer and consumer._inflight_rows == 0):
                break
            if consume_task.done():
                consume_task.result()
            await asyncio.sleep(.2)
        final = await asyncio.to_thread(state)
        def verify_database():
            c = taosws.connect("taosws://root:taosdata@perf-td:6041")
            value_rows = list(c.query(f"SELECT count(*) FROM `{database}`.`{stable}`"))[0][0]
            quality_rows = list(c.query(f"SELECT count(*) FROM `{database}`.`sq{device_type}`"))[0][0]
            incorrect = list(c.query(f"SELECT count(*) FROM `{database}`.`{stable}` "
                                    f"WHERE p1 != 1 + (cast(ts as bigint) - {base_ts}) / {args.period_ms} "
                                    f"OR p{args.points} != {args.points} + (cast(ts as bigint) - {base_ts}) / {args.period_ms}"))[0][0]
            incorrect_quality = list(c.query(f"SELECT count(*) FROM `{database}`.`sq{device_type}` "
                                            "WHERE quality != 192"))[0][0]
            c.close()
            return value_rows, quality_rows, incorrect, incorrect_quality
        value_rows, quality_rows, incorrect, incorrect_quality = await asyncio.to_thread(verify_database)
        latest_ok = all(json.loads(r.hget(f"rt:latest:{d}:{group}", "1"))["t"] ==
                        base_ts + (tick - 1) * args.period_ms for d in devices)
        dlq_entries = sum(r.llen(k) for k in r.scan_iter(f"rt:dlq:{device_type}:*"))
        passed = (value_rows == generated and quality_rows == generated * args.points
                  and incorrect == 0 and incorrect_quality == 0 and latest_ok and dlq_entries == 0
                  and final["outbox"]["count"] == 0 and final["pending"] == 0 and final["lag"] == 0)
        steady_state_passed = (production_end["outbox"]["oldest_age_ms"] <= 5000
            and production_end["outbox"]["count"] < args.devices * 1000 / args.period_ms * 5
            and max(pacing_ms) < 1000)
        result = {"passed": passed and steady_state_passed, "correctness_passed": passed,
                  "steady_state_passed": steady_state_passed,
                  "production_end": production_end, "source_to_td_confirm_ms": commit_latency.snapshot(), "mode": "synthetic_inputs_real_sqlite_redis_mysql_tdengine",
                  "database": database, "devices": args.devices, "points": args.points,
                  "period_ms": args.period_ms, "production_seconds": production_s,
                  "generated_frames": generated, "frames_per_second": generated / production_s,
                  "value_rows": value_rows, "quality_rows": quality_rows,
                  "incorrect_value_rows": incorrect, "incorrect_quality_rows": incorrect_quality,
                  "latest_correct": latest_ok, "dlq_entries": dlq_entries,
                  "submit_ms_p50": statistics.median(submit_ms), "submit_ms_p95": percentile(submit_ms, .95),
                  "submit_ms_p99": percentile(submit_ms, .99), "pacing_delay_ms_max": max(pacing_ms),
                  "event_loop_lag_ms_p99": percentile(loop_lag_ms, .99),
                  "event_loop_lag_ms_max": max(loop_lag_ms, default=0),
                  "max_rss_bytes": max((s["rss_bytes"] for s in samples), default=0),
                  "final": final, "samples": samples}
        Path(args.output).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps({k: v for k, v in result.items() if k != "samples"}, ensure_ascii=False), flush=True)
        return result["passed"]
    except Exception as exc:
        Path(args.output).write_text(json.dumps({"passed": False, "database": database,
            "error": str(exc), "generated_frames": generated, "samples": samples},
            ensure_ascii=False, indent=2), encoding="utf-8")
        raise
    finally:
        done.set()
        for task in monitors + [consume_task]:
            task.cancel()
        await asyncio.gather(*monitors, consume_task, return_exceptions=True)
        await asyncio.to_thread(collector.close)
        await td.close()
        mysql.close()
        r.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--devices", type=int, default=5)
    parser.add_argument("--points", type=int, default=100)
    parser.add_argument("--seconds", type=int, default=60)
    parser.add_argument("--period-ms", type=int, default=100)
    parser.add_argument("--drain-seconds", type=int, default=180)
    parser.add_argument("--output", default="/tmp/pipeline-validation.json")
    args = parser.parse_args()
    if min(args.devices, args.points, args.seconds, args.period_ms, args.drain_seconds) < 1:
        parser.error("all load and duration values must be positive")
    logging.basicConfig(level=logging.WARNING)
    raise SystemExit(0 if asyncio.run(run(args)) else 1)
