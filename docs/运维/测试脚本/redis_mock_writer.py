#!/usr/bin/env python3
"""Simulate Redis writes for all real devices loaded from MySQL.

Key schema:

  rt:latest:{device_code}:{group_code}
      Hash  {var_id: '{"v":"float_str","t":ms_epoch,"q":1}'}

  rt:window:{device_code}:{group_code}:{scan_rate_ms}
      List  [JSON frame, ...]  maxlen=10

  rt:stream:{device_type}:{group_code}:{shard}
      Stream  fields: device_code, device_type, group_code,
              scan_rate, ts (ms int str), config_version,
              values (JSON {var_id: val_str})
      Required for writer→TDengine path. Enable with --enable-stream.

Device topology and var_ids are loaded from MySQL at startup.
Default config: writer/config/settings.toml

Typical test usage:
  # 1. dry-run: confirm devices loaded correctly
  python redis_mock_writer.py --dry-run

  # 2. write streams for 30s (writer must be running)
  python redis_mock_writer.py --enable-stream --duration-seconds 30

  # 3. continuous until Ctrl+C
  python redis_mock_writer.py --enable-stream --duration-seconds 0
"""

from __future__ import annotations

import argparse
import json
import math
import random
import sys
import threading
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Sequence

# ---------------------------------------------------------------------------
# Path setup — script lives at docs/运维/测试脚本/, repo root is 3 levels up
# ---------------------------------------------------------------------------

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO_ROOT = _SCRIPT_DIR.parents[2]
_COLLECTOR_VENV_SP = _REPO_ROOT / "collector" / ".venv" / "Lib" / "site-packages"
if _COLLECTOR_VENV_SP.exists():
    sys.path.insert(0, str(_COLLECTOR_VENV_SP))

try:
    import redis
except ImportError:
    print("ERROR: redis-py not installed.", file=sys.stderr)
    sys.exit(1)

try:
    import pymysql
    import pymysql.cursors
except ImportError:
    print("ERROR: pymysql not installed.", file=sys.stderr)
    sys.exit(1)

try:
    import tomllib
except ImportError:
    try:
        import tomli as tomllib  # type: ignore[no-redef]
    except ImportError:
        tomllib = None  # type: ignore[assignment]


# ---------------------------------------------------------------------------
# Config loader
# ---------------------------------------------------------------------------

def load_toml(path: Path) -> dict:
    if tomllib is None:
        return {}
    if not path.exists():
        return {}
    with path.open("rb") as fp:
        return tomllib.load(fp)


# ---------------------------------------------------------------------------
# Device spec (loaded from MySQL)
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class DeviceSpec:
    device_code: str
    device_type: str       # e.g. "qc", "asc", "agv"
    group_code: str        # e.g. "g001"
    config_version: int
    var_ids: tuple[str, ...]  # MySQL device_variable.id (as str)


# ---------------------------------------------------------------------------
# MySQL loader
# ---------------------------------------------------------------------------

_SQL = """
SELECT d.device_code,
       dt.type_code AS device_type,
       dpg.group_code,
       dpg.config_version,
       dv.id AS var_id
FROM device_variable dv
JOIN device_point_group dpg ON dv.point_group_id = dpg.id
JOIN device d ON dpg.device_id = d.id
JOIN device_type dt ON dt.id = d.type_id
WHERE dv.deleted = 0 AND dv.enable = 1
ORDER BY d.device_code, dpg.group_code, dv.id
"""


def load_from_mysql(mysql_cfg: dict) -> list[DeviceSpec]:
    conn = pymysql.connect(
        host=mysql_cfg["host"],
        port=int(mysql_cfg["port"]),
        user=mysql_cfg["user"],
        password=mysql_cfg["password"],
        database=mysql_cfg["database"],
        cursorclass=pymysql.cursors.DictCursor,
        connect_timeout=5,
    )
    try:
        with conn.cursor() as cur:
            cur.execute(_SQL)
            rows = cur.fetchall()
    finally:
        conn.close()

    if not rows:
        return []

    groups: dict[tuple[str, str], dict] = {}
    for row in rows:
        key = (str(row["device_code"]), str(row["group_code"]))
        if key not in groups:
            groups[key] = {
                "device_code": str(row["device_code"]),
                "device_type": (row["device_type"] or "").lower(),
                "group_code": str(row["group_code"]),
                "config_version": int(row["config_version"] or 0),
                "var_ids": [],
            }
        groups[key]["var_ids"].append(str(row["var_id"]))

    return [
        DeviceSpec(
            device_code=g["device_code"],
            device_type=g["device_type"],
            group_code=g["group_code"],
            config_version=g["config_version"],
            var_ids=tuple(g["var_ids"]),
        )
        for g in groups.values()
    ]


# ---------------------------------------------------------------------------
# Value generation (sine wave per var index)
# ---------------------------------------------------------------------------

def generate_values(var_ids: tuple[str, ...], tick: int) -> dict[str, str]:
    """Return {var_id: float_str} for all var_ids."""
    t = tick * 0.1
    return {
        vid: str(round(math.sin(t + idx * 0.01) * 50.0 + 50.0 + random.uniform(-0.5, 0.5), 4))
        for idx, vid in enumerate(var_ids)
    }


# ---------------------------------------------------------------------------
# Redis writer
# ---------------------------------------------------------------------------

STREAM_MAXLEN = 20000
SCAN_RATE_MS = 100


def write_device_frame(
    pipe,
    device: DeviceSpec,
    ts_ms: int,
    tick: int,
    shard_count: int,
    enable_stream: bool = False,
    skip_window: bool = False,
) -> None:
    val_str = generate_values(device.var_ids, tick)

    # 1. latest Hash: {var_id: '{"v":"float","t":ms_epoch,"q":1}'}
    latest_key = f"rt:latest:{device.device_code}:{device.group_code}"
    mapping = {
        vid: json.dumps({"v": v, "t": ts_ms, "q": 1}, separators=(",", ":"))
        for vid, v in val_str.items()
    }
    pipe.hset(latest_key, mapping=mapping)

    # 2. window List
    if not skip_window:
        window_key = f"rt:window:{device.device_code}:{device.group_code}:{SCAN_RATE_MS}"
        frame = {"device_code": device.device_code, "ts": ts_ms, "points": val_str}
        pipe.lpush(window_key, json.dumps(frame, separators=(",", ":")))
        pipe.ltrim(window_key, 0, 9)

    # 3. Stream (consumer expects device_type, group_code, ts as ms int str, config_version)
    if enable_stream:
        shard = hash(device.device_code) % shard_count
        stream_key = f"rt:stream:{device.device_type}:{device.group_code}:{shard}"
        pipe.xadd(
            stream_key,
            {
                "device_code": device.device_code,
                "device_type": device.device_type,
                "group_code": device.group_code,
                "scan_rate": str(SCAN_RATE_MS),
                "ts": str(ts_ms),
                "config_version": str(device.config_version),
                "values": json.dumps(val_str, separators=(",", ":")),
            },
            maxlen=STREAM_MAXLEN,
            approximate=True,
        )


# ---------------------------------------------------------------------------
# Worker thread
# ---------------------------------------------------------------------------

@dataclass
class WorkerStat:
    worker_id: int
    frames: int = 0
    errors: int = 0
    elapsed_ms_samples: list[float] = field(default_factory=list)


def run_worker(
    worker_id: int,
    devices: Sequence[DeviceSpec],
    redis_client: redis.Redis,
    interval_ms: int,
    stop_event: threading.Event,
    stat: WorkerStat,
    shard_count: int = 4,
    enable_stream: bool = False,
    skip_window: bool = False,
) -> None:
    tick = 0
    interval_s = interval_ms / 1000.0
    next_start = time.perf_counter()

    while not stop_event.is_set():
        tick += 1
        started = time.perf_counter()
        ts_ms = int(time.time() * 1000)

        try:
            pipe = redis_client.pipeline(transaction=False)
            for device in devices:
                write_device_frame(
                    pipe, device, ts_ms, tick, shard_count,
                    enable_stream=enable_stream, skip_window=skip_window,
                )
            pipe.execute()
            stat.frames += len(devices)
        except Exception as exc:
            stat.errors += 1
            print(f"[ERROR] worker={worker_id} tick={tick} error={exc}", file=sys.stderr)

        elapsed_ms = (time.perf_counter() - started) * 1000
        stat.elapsed_ms_samples.append(elapsed_ms)

        next_start += interval_s
        sleep_s = next_start - time.perf_counter()
        if sleep_s > 0:
            stop_event.wait(sleep_s)
        else:
            next_start = time.perf_counter()


# ---------------------------------------------------------------------------
# Summary printer
# ---------------------------------------------------------------------------

def print_summary(
    stats: list[WorkerStat],
    started_at: float,
    duration_s: float,
    stop_event: threading.Event,
    threads: list[threading.Thread],
    report_interval: float = 2.0,
) -> None:
    last_print = time.perf_counter()
    total_frames = 0
    total_errors = 0

    while any(t.is_alive() for t in threads):
        if duration_s > 0 and time.perf_counter() - started_at >= duration_s:
            stop_event.set()

        time.sleep(0.05)

        now = time.perf_counter()
        if now - last_print >= report_interval:
            batch_frames = 0
            batch_errors = 0
            all_elapsed: list[float] = []

            for stat in stats:
                batch_frames += stat.frames
                batch_errors += stat.errors
                all_elapsed.extend(stat.elapsed_ms_samples)
                stat.frames = 0
                stat.errors = 0
                stat.elapsed_ms_samples.clear()

            total_frames += batch_frames
            total_errors += batch_errors
            interval_actual = now - last_print
            fps = batch_frames / max(interval_actual, 0.001)
            avg_ms = sum(all_elapsed) / len(all_elapsed) if all_elapsed else 0.0
            max_ms = max(all_elapsed) if all_elapsed else 0.0

            print(
                f"t={now - started_at:.1f}s  "
                f"frames/s={fps:.0f}  "
                f"total_frames={total_frames}  "
                f"errors={total_errors}  "
                f"avg_pipe_ms={avg_ms:.1f}  "
                f"max_pipe_ms={max_ms:.1f}"
            )
            last_print = now

    stop_event.set()


# ---------------------------------------------------------------------------
# Argument parsing & main
# ---------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    default_cfg = _REPO_ROOT / "writer" / "config" / "settings.toml"
    parser = argparse.ArgumentParser(
        description="Simulate Redis writes using v0.6 key schema, devices loaded from MySQL."
    )
    parser.add_argument("--config", type=Path, default=default_cfg,
                        help="writer/config/settings.toml path (MySQL/Redis config source).")
    parser.add_argument("--redis-host", default=None)
    parser.add_argument("--redis-port", type=int, default=None)
    parser.add_argument("--redis-db", type=int, default=None)
    parser.add_argument("--redis-password", default=None)
    parser.add_argument("--mysql-host", default=None)
    parser.add_argument("--mysql-port", type=int, default=None)
    parser.add_argument("--mysql-user", default=None)
    parser.add_argument("--mysql-password", default=None)
    parser.add_argument("--mysql-db", default=None)
    parser.add_argument("--interval-ms", type=int, default=100)
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--duration-seconds", type=float, default=60.0,
                        help="0 = run until Ctrl+C.")
    parser.add_argument("--enable-stream", action="store_true",
                        help="Write to Redis Stream (required for writer→TDengine path).")
    parser.add_argument("--skip-window", action="store_true")
    parser.add_argument("--dry-run", action="store_true",
                        help="Print device topology and exit.")
    return parser.parse_args()


def split_evenly(items: list, n: int) -> list[list]:
    n = max(1, n)
    result = []
    for i in range(n):
        start = i * len(items) // n
        end = (i + 1) * len(items) // n
        part = items[start:end]
        if part:
            result.append(part)
    return result


def main() -> int:
    args = parse_args()

    cfg = load_toml(args.config)
    redis_cfg = cfg.get("redis", {})
    mysql_cfg = cfg.get("mysql", {})

    redis_host = args.redis_host or redis_cfg.get("host", "127.0.0.1")
    redis_port = args.redis_port or int(redis_cfg.get("port", 6379))
    redis_db = args.redis_db if args.redis_db is not None else int(redis_cfg.get("db", 0))
    redis_pw = args.redis_password if args.redis_password is not None else redis_cfg.get("password", "")
    shard_count = int(redis_cfg.get("stream_shard_count", 4))

    mysql_params = {
        "host": args.mysql_host or mysql_cfg.get("host", "127.0.0.1"),
        "port": args.mysql_port or int(mysql_cfg.get("port", 3306)),
        "user": args.mysql_user or mysql_cfg.get("user", "root"),
        "password": args.mysql_password if args.mysql_password is not None else mysql_cfg.get("password", ""),
        "database": args.mysql_db or mysql_cfg.get("database", "device_watch"),
    }

    print(f"Loading devices from MySQL {mysql_params['host']}:{mysql_params['port']} db={mysql_params['database']}")
    try:
        all_devices = load_from_mysql(mysql_params)
    except Exception as exc:
        print(f"ERROR: MySQL connect failed: {exc}", file=sys.stderr)
        return 1

    if not all_devices:
        print("ERROR: no enabled devices/variables found in MySQL.", file=sys.stderr)
        return 1

    by_type: dict[str, int] = {}
    for d in all_devices:
        by_type[d.device_type] = by_type.get(d.device_type, 0) + 1
    total_vars = sum(len(d.var_ids) for d in all_devices)

    print(f"Device groups: {dict(by_type)}  total={len(all_devices)}")
    print(f"Total var_ids: {total_vars:,}")
    print(f"Redis: {redis_host}:{redis_port}/db{redis_db}  shards={shard_count}")
    print(f"Interval: {args.interval_ms}ms  Workers: {args.workers}  Stream: {args.enable_stream}")

    if args.dry_run:
        print("\nSample devices:")
        for d in all_devices[:5]:
            print(f"  {d.device_code} type={d.device_type} group={d.group_code} "
                  f"config_version={d.config_version} vars={len(d.var_ids)}")
        return 0

    r = redis.Redis(
        host=redis_host, port=redis_port, db=redis_db,
        password=redis_pw or None,
        decode_responses=True,
        socket_connect_timeout=5,
    )
    try:
        r.ping()
    except Exception as exc:
        print(f"ERROR: cannot connect to Redis: {exc}", file=sys.stderr)
        return 1
    print("Redis connected.\n")

    chunks = split_evenly(all_devices, args.workers)
    stop_event = threading.Event()
    stats: list[WorkerStat] = []
    threads: list[threading.Thread] = []

    for worker_id, chunk in enumerate(chunks, start=1):
        worker_redis = redis.Redis(
            host=redis_host, port=redis_port, db=redis_db,
            password=redis_pw or None,
            decode_responses=True,
        )
        stat = WorkerStat(worker_id=worker_id)
        stats.append(stat)
        t = threading.Thread(
            target=run_worker,
            name=f"redis-writer-{worker_id}",
            args=(worker_id, chunk, worker_redis, args.interval_ms, stop_event, stat),
            kwargs={
                "shard_count": shard_count,
                "enable_stream": args.enable_stream,
                "skip_window": args.skip_window,
            },
            daemon=True,
        )
        threads.append(t)

    started_at = time.perf_counter()
    for t in threads:
        t.start()

    try:
        print_summary(stats, started_at, args.duration_seconds, stop_event, threads)
    except KeyboardInterrupt:
        print("\nStopping...")
        stop_event.set()

    for t in threads:
        t.join(timeout=5)

    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
