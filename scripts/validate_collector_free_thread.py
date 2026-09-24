"""Isolated Python 3.14t collector SQLite/Redis-AOF load test (no TDengine)."""
from __future__ import annotations

import argparse
import json
import logging
import os
import sqlite3
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from types import SimpleNamespace
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector")]

import redis
from data_watch_shared.stream_envelope import stream_key
from storage.durable_redis_writer import DurableRedisWriter


def save_report(path: Path, report: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as output:
        json.dump(report, output, ensure_ascii=False, indent=2)
        output.write("\n")
        output.flush()
        os.fsync(output.fileno())


def validate_history(r, keys, devices, *, ticks, points, base_ts, period_ms):
    expected = {(device, base_ts + tick * period_ms)
                for tick in range(ticks) for device in devices}
    seen = set()
    duplicate = 0
    malformed = 0
    actual = 0
    for key in keys:
        minimum = "-"
        while True:
            batch = r.xrange(key, min=minimum, max="+", count=250)
            if not batch:
                break
            for _, fields in batch:
                actual += 1
                identity = (fields.get("device_code"), int(fields.get("ts", -1)))
                if identity in seen:
                    duplicate += 1
                seen.add(identity)
                values = json.loads(fields.get("values", "{}"))
                if (identity not in expected or len(values) != points
                    or values.get("1") != str(1 + (identity[1] - base_ts) // period_ms)
                    or values.get(str(points)) != str(points + (identity[1] - base_ts) // period_ms)):
                    malformed += 1
            minimum = "(" + batch[-1][0]
    return {"actual_frames": actual, "expected_frames": len(expected),
            "unique_frames": len(seen), "duplicate_frames": duplicate,
            "malformed_frames": malformed, "exactly_once": actual == len(expected)
            and seen == expected and duplicate == 0 and malformed == 0}


def validate_latest(r, devices, *, ticks, points, base_ts, period_ms, group):
    if not ticks:
        return False
    expected_ts = base_ts + (ticks - 1) * period_ms
    for device in devices:
        latest = r.hgetall(f"rt:latest:{device}:{group}")
        if len(latest) != points:
            return False
        for point in range(1, points + 1):
            value = json.loads(latest.get(str(point), "{}"))
            if (value.get("t") != expected_ts or value.get("v") != str(point + ticks - 1)
                    or value.get("q") != 192):
                return False
    return True


def command_stats(r):
    info = r.info("commandstats")
    return {name: {field: info.get("cmdstat_" + name, {}).get(field, 0)
                   for field in ("calls", "usec", "usec_per_call")}
            for name in ("eval", "evalsha", "waitaof")}


class ProfiledDurableRedisWriter(DurableRedisWriter):
    """Test-only same-connection split of the production EVALSHA/WAITAOF batch."""

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.profile_windows = []

    def _deliver_window(self, batch, batch_bytes):
        for script in {source for source, _ in batch}:
            if script not in self._script_shas:
                self._script_shas[script] = self.r.script_load(script)
        commands = [("EVALSHA", self._script_shas[source], *args)
                    for source, args in batch]
        commands.append(("WAITAOF", 1, 0, 2000))
        connection = self.r.connection_pool.get_connection()
        completed = False
        try:
            started = time.perf_counter()
            packed = connection.pack_commands(commands)
            packed_at = time.perf_counter()
            connection.send_packed_command(packed)
            sent_at = time.perf_counter()
            for _ in batch:
                connection.read_response()
            eval_read_at = time.perf_counter()
            receipt = connection.read_response()
            confirmed_at = time.perf_counter()
            if not isinstance(receipt, (list, tuple)) or int(receipt[0]) < 1:
                raise RuntimeError("same-connection WAITAOF did not confirm the frame batch")
            completed = True
        finally:
            if not completed:
                connection.disconnect()
            self.r.connection_pool.release(connection)
        times = {
            "frames": len(batch), "bytes": batch_bytes,
            "pack_ms": round((packed_at - started) * 1000, 3),
            "send_ms": round((sent_at - packed_at) * 1000, 3),
            "eval_response_ms": round((eval_read_at - sent_at) * 1000, 3),
            "pre_waitaof_ms": round((eval_read_at - started) * 1000, 3),
            "waitaof_response_ms": round((confirmed_at - eval_read_at) * 1000, 3),
            "total_ms": round((confirmed_at - started) * 1000, 3),
            "waitaof_reply": receipt,
        }
        self.profile_windows.append(times)
        self._metrics.record_timing("redis_aof_batch_ms", times["total_ms"])
        self._metrics.set_gauge("redis_batch_frames", len(batch))
        self._metrics.set_gauge("redis_batch_bytes", batch_bytes)
        self._metrics.increment("redis_confirmed_batches")
        self._metrics.increment("redis_confirmed_bytes", batch_bytes)


def run(args) -> dict:
    if sys._is_gil_enabled():
        raise RuntimeError("Python 3.14t GIL was enabled")
    if not Path("/workspace/collector").is_dir():
        raise RuntimeError("run only in the isolated collector test container")
    if args.redis_host not in {"perf-redis", "perf-redis-profile"}:
        raise ValueError("only dedicated performance Redis hosts are allowed")
    if args.profile_phases and args.redis_host != "perf-redis-profile":
        raise ValueError("phase profiling requires the dedicated Redis instance")
    if not (1 <= args.devices <= 100 and 1 <= args.points <= 1000
            and 1 <= args.seconds <= 10 and args.period_ms >= 100
            and 10 <= args.drain_seconds <= 300):
        raise ValueError("load limits exceeded")
    token = uuid4().hex[:10]
    device_type = "perfcol" + token
    group = "gft"
    devices = [f"pc{token}{index:03d}" for index in range(args.devices)]
    keys = sorted({stream_key(device, device_type, group, 4) for device in devices})
    db_path = Path(f"/tmp/collector-ft-{token}.sqlite")
    r = redis.Redis(host=args.redis_host, port=6379, db=14, decode_responses=True,
                    socket_timeout=10, socket_connect_timeout=3)
    if not r.ping() or int(r.info("persistence").get("aof_enabled", 0)) != 1:
        raise RuntimeError("isolated Redis 7.4 with AOF is required")
    maxmemory = int(r.config_get("maxmemory")["maxmemory"])
    if maxmemory < 512 * 1024 * 1024:
        raise RuntimeError("isolated Redis memory limit unexpectedly low")
    memory_guard = min(400 * 1024 * 1024, int(maxmemory * .8))
    stats_before = command_stats(r)
    collector_class = ProfiledDurableRedisWriter if args.profile_phases else DurableRedisWriter
    collector = collector_class(
        r, SimpleNamespace(redis=SimpleNamespace(
            stream_shard_count=4, stream_maxlen=0, stream_warn_entries=50000)),
        outbox_path=db_path,
    )
    session_id = collector.outbox._session_id
    receipt_key = "collector:receipt:v2:" + session_id
    report = {"mode": "synthetic_python314t_real_sqlite_redis_aof_no_tdengine",
              "token": token, "device_type": device_type, "redis_db": 14,
              "redis_host": args.redis_host, "profile_phases": args.profile_phases,
              "commandstats_before": stats_before,
              "python_gil_enabled": sys._is_gil_enabled(),
              "devices": args.devices, "points_per_frame": args.points,
              "period_ms": args.period_ms, "target_seconds": args.seconds,
              "target_frames": args.devices * (args.seconds * 1000 // args.period_ms),
              "container_cpu_limit": args.cpu_limit,
              "redis_memory_guard_bytes": memory_guard,
              "outbox_path": str(db_path), "receipt_key": receipt_key}
    collector.ping()
    base_ts = int(time.time() * 1000)
    ticks = 0
    submitted = 0
    samples = []
    production_started = time.monotonic()
    stop_reason = "duration"

    def submit(device, tick):
        ts = base_ts + tick * args.period_ms
        values = {str(point): str(point + tick) for point in range(1, args.points + 1)}
        qualities = {point: 192 for point in values}
        latest = {point: {"v": value, "t": ts, "q": 192}
                  for point, value in values.items()}
        collector.write_window_stream(device, device_type, group, args.period_ms, ts, 1,
                                      {"ts": ts, "values": values}, values, qualities)
        collector.write_latest(device, group, latest)

    try:
        with ThreadPoolExecutor(max_workers=min(32, args.devices)) as pool:
            while time.monotonic() - production_started < args.seconds:
                tick = ticks
                futures = [pool.submit(submit, device, tick) for device in devices]
                for future in futures:
                    future.result()
                ticks += 1
                submitted += args.devices
                if ticks % 5 == 0:
                    memory = int(r.info("memory")["used_memory"])
                    snapshot = collector._metrics.snapshot()
                    samples.append({"elapsed_s": round(time.monotonic() - production_started, 3),
                                    "submitted": submitted, "outbox": collector.outbox.stats(),
                                    "rss_bytes": snapshot["runtime"]["rss_bytes"],
                                    "redis_used_memory_bytes": memory})
                    if memory >= memory_guard:
                        stop_reason = "redis_memory_guard"
                        break
                next_tick = production_started + ticks * args.period_ms / 1000
                time.sleep(max(0, next_tick - time.monotonic()))
        production_end = time.monotonic()
        production_metrics = collector._metrics.snapshot()
        production_outbox = collector.outbox.stats()
        drain_started = time.monotonic()
        while time.monotonic() - drain_started < args.drain_seconds:
            outbox = collector.outbox.stats()
            with collector._latest_condition:
                latest_pending = len(collector._latest_pending)
            if outbox["count"] == 0 and outbox["waiting_writes"] == 0 and latest_pending == 0:
                break
            time.sleep(.2)
        drain_end = time.monotonic()
        final_outbox = collector.outbox.stats()
        with collector._latest_condition:
            final_latest_pending = len(collector._latest_pending)
        collector.close()
        with sqlite3.connect(db_path) as db:
            sqlite_rows = sum(db.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
                              for table in ("collector_retry_outbox", "collector_retry_outbox_v2"))
        final_metrics = collector._metrics.snapshot()
        stats_after = command_stats(r)
        history = validate_history(r, keys, devices, ticks=ticks, points=args.points,
                                   base_ts=base_ts, period_ms=args.period_ms)
        latest_correct = validate_latest(r, devices, ticks=ticks, points=args.points,
                                         base_ts=base_ts, period_ms=args.period_ms, group=group)
        confirmed_frames = final_metrics["counters"].get("stream_points_written", 0) // args.points
        production_confirmed = production_metrics["counters"].get("stream_points_written", 0) // args.points
        drain_s = max(.001, drain_end - drain_started)
        production_s = production_end - production_started
        elapsed_s = drain_end - production_started
        correctness = (submitted == ticks * args.devices and history["exactly_once"]
                       and latest_correct and final_outbox["count"] == 0
                       and final_outbox["waiting_writes"] == 0 and final_latest_pending == 0
                       and sqlite_rows == 0 and confirmed_frames == submitted)
        target_met = submitted == report["target_frames"] and production_s <= args.seconds + .25
        report.update({
            "stop_reason": stop_reason, "ticks": ticks, "submitted_frames": submitted,
            "production_seconds": round(production_s, 3),
            "receive_frames_per_second": round(submitted / production_s, 2),
            "production_confirmed_frames": production_confirmed,
            "production_outbox": production_outbox,
            "drain_seconds": round(drain_s, 3),
            "replay_frames_per_second_total": round(confirmed_frames / elapsed_s, 2),
            "replay_frames_per_second_post_production": round(
                (confirmed_frames - production_confirmed) / drain_s, 2),
            "confirmed_frames": confirmed_frames,
            "max_rss_bytes": max([sample["rss_bytes"] for sample in samples]
                                 + [production_metrics["runtime"]["rss_bytes"],
                                    final_metrics["runtime"]["rss_bytes"]]),
            "max_redis_used_memory_bytes": max(
                [sample["redis_used_memory_bytes"] for sample in samples]
                + [int(r.info("memory")["used_memory"])]),
            "commandstats_after": stats_after,
            "commandstats_delta": {name: {
                field: stats_after[name][field] - stats_before[name][field]
                for field in ("calls", "usec")}
                for name in stats_before},
            "profile_windows": collector.profile_windows if args.profile_phases else [],
            "timings_ms": {name: final_metrics["timings"].get(name)
                           for name in ("outbox_accept_ms", "outbox_commit_ms",
                                        "redis_aof_batch_ms", "latest_fastlane_redis_ms")},
            "history": history, "latest_correct": latest_correct,
            "final_outbox": final_outbox, "sqlite_rows_after_close": sqlite_rows,
            "final_latest_pending": final_latest_pending,
            "correctness_passed": correctness, "target_met": target_met,
            "samples": samples,
        })
        save_report(Path(args.output), report)
        # Only this never-reused session and its exact keys are removed, and only
        # after every durable frame is confirmed and the JSON report is fsynced.
        backups = list(db_path.parent.glob(db_path.name + ".bak*"))
        if correctness and not backups:
            own_keys = keys + [receipt_key]
            own_keys += [f"rt:latest:{device}:{group}" for device in devices]
            own_keys += [f"rt:window:{device}:{group}:{args.period_ms}" for device in devices]
            r.unlink(*own_keys)
            report["cleanup"] = {"redis_own_keys_remaining": sum(r.exists(key) for key in own_keys),
                                 "sqlite_removed": False}
            for suffix in ("", "-wal", "-shm"):
                candidate = Path(str(db_path) + suffix)
                if candidate.exists():
                    candidate.unlink()
            report["cleanup"]["sqlite_removed"] = not db_path.exists()
            save_report(Path(args.output), report)
        else:
            report["cleanup"] = {"skipped": True, "reason": "unconfirmed data or backup present"}
            save_report(Path(args.output), report)
        print(json.dumps({key: value for key, value in report.items() if key != "samples"},
                         ensure_ascii=False), flush=True)
        return report
    except Exception as exc:
        report.update({"error": repr(exc), "submitted_frames": submitted,
                       "ticks": ticks, "outbox": collector.outbox.stats()})
        save_report(Path(args.output), report)
        raise
    finally:
        collector.close()
        r.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--devices", type=int, default=100)
    parser.add_argument("--points", type=int, default=1000)
    parser.add_argument("--seconds", type=int, default=10)
    parser.add_argument("--period-ms", type=int, default=100)
    parser.add_argument("--drain-seconds", type=int, default=180)
    parser.add_argument("--cpu-limit", type=int, required=True)
    parser.add_argument("--redis-host", default="perf-redis")
    parser.add_argument("--profile-phases", action="store_true")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    logging.basicConfig(level=logging.WARNING)
    result = run(args)
    raise SystemExit(0 if result.get("correctness_passed") else 1)
