#!/usr/bin/env python3
"""Simulate Redis writes for all real devices loaded from TDengine.

Device topology: loaded from TDengine child tables at startup (no MySQL needed).
  qc  supertable → SHOW device_data.TABLES LIKE 'qc%'
  asc supertable → SHOW device_data.TABLES LIKE 'asc%'
  agv supertable → SHOW device_data.TABLES LIKE 'agv%'

Signal column naming follows tdengine_write_benchmark.py:
  signal_column_name(stable, i) = f"{stable}{i}"
  → qc  supertable columns: qc1,  qc2,  ..., qc1000   (shared by ALL qc  devices)
  → asc supertable columns: asc1, asc2, ..., asc1000
  → agv supertable columns: agv1, agv2, ..., agv1000

Redis key design:
  rt:{device_code}:latest  → Hash  {col_name: "float_str", "_ts": "iso_str"}  TTL 30s
  rt:{device_code}:window  → List  [JSON frame, ...]  maxlen=10   TTL 60s
  rt:stream                → Stream  XADD entries (requires Redis >= 5.0)
                             Enable with --enable-stream flag.
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
# .venv auto-detection (same logic as redis_to_tdengine_writer.py)
# ---------------------------------------------------------------------------

_SCRIPT_DIR = Path(__file__).resolve().parent
_COLLECTOR_ROOT = _SCRIPT_DIR.parent
_VENV_SP = _COLLECTOR_ROOT / ".venv" / "Lib" / "site-packages"
if _VENV_SP.exists():
    sys.path.insert(0, str(_VENV_SP))
sys.path.insert(0, str(_COLLECTOR_ROOT))

try:
    import taosws
except ImportError:
    taosws = None  # TDengine optional; fall back to static topology

try:
    import redis
except ImportError:
    print("ERROR: redis-py not installed. Run: pip install redis", file=sys.stderr)
    sys.exit(1)


# ---------------------------------------------------------------------------
# Device topology
# ---------------------------------------------------------------------------

STABLES = ("qc", "asc", "agv")   # supertable names
POINTS_PER_DEVICE = 1000


@dataclass(frozen=True)
class DeviceSpec:
    type_prefix: str   # "qc" / "asc" / "agv"  (= supertable name)
    device_code: str   # e.g. "qc100", "asc7011"  (= child table name)
    columns: tuple[str, ...]  # signal_column_name(stable, i) = f"{stable}{i}"
                               # e.g. ("qc1", "qc2", ..., "qc1000") for ALL qc devices


def _load_from_tdengine(config_path: Path) -> list[DeviceSpec]:
    """Query TDengine for real child table names."""
    from core.config_loader import load_collector_config
    config = load_collector_config(str(config_path))
    td = config.tdengine
    conn = taosws.connect(user=td.user, password=td.password, host=td.host, port=td.port)
    devices: list[DeviceSpec] = []
    try:
        for prefix in STABLES:
            cols = tuple(f"{prefix}{i}" for i in range(1, POINTS_PER_DEVICE + 1))
            rows = list(conn.query(f"SHOW device_data.TABLES LIKE '{prefix}%'"))
            for row in rows:
                code = str(row[0])
                devices.append(DeviceSpec(type_prefix=prefix, device_code=code, columns=cols))
    finally:
        conn.close()
    return devices


def _load_static() -> list[DeviceSpec]:
    """Fallback: generate sequential device codes when TDengine is unavailable."""
    FALLBACK: dict[str, tuple[int, int]] = {
        "qc": (29, 100), "asc": (121, 7011), "agv": (155, 801),
    }
    devices: list[DeviceSpec] = []
    for prefix, (count, start) in FALLBACK.items():
        cols = tuple(f"{prefix}{i}" for i in range(1, POINTS_PER_DEVICE + 1))
        for seq in range(start, start + count):
            devices.append(DeviceSpec(
                type_prefix=prefix,
                device_code=f"{prefix}{seq}",
                columns=cols,
            ))
    return devices


def build_all_devices(config_path: Path | None = None) -> list[DeviceSpec]:
    if taosws is not None and config_path is not None and config_path.exists():
        try:
            devices = _load_from_tdengine(config_path)
            if devices:
                return devices
        except Exception as exc:
            print(f"[WARN] TDengine query failed, using static topology: {exc}", file=sys.stderr)
    return _load_static()


# ---------------------------------------------------------------------------
# Value generation (sine wave per column, cheap and repeatable)
# ---------------------------------------------------------------------------

def generate_values(device: DeviceSpec, tick: int) -> dict[str, float]:
    """Generate 1000 float values for one device frame."""
    t = tick * 0.1  # 100ms tick → seconds
    return {
        col: round(math.sin(t + col_idx * 0.01) * 50.0 + 50.0 + random.uniform(-0.5, 0.5), 4)
        for col_idx, col in enumerate(device.columns)
    }


# ---------------------------------------------------------------------------
# Redis writer
# ---------------------------------------------------------------------------

STREAM_KEY = "rt:stream"
STREAM_MAXLEN = 20000   # keep at most 20k unack-ed entries
WINDOW_MAXLEN = 10      # frames per device
LATEST_TTL = 30         # seconds
WINDOW_TTL = 60         # seconds


def write_device_frame(
    pipe,
    device: DeviceSpec,
    values: dict[str, float],
    ts_ms: int,
    enable_stream: bool = False,
    skip_window: bool = False,
) -> None:
    """Queue one device frame into a Redis pipeline (not yet executed)."""
    ts_iso = time.strftime("%Y-%m-%dT%H:%M:%S", time.localtime(ts_ms / 1000))
    ts_iso += f".{ts_ms % 1000:03d}"

    latest_key = f"rt:{device.device_code}:latest"

    # 1. latest Hash: lightweight — each field is just the float value string.
    #    _ts is a reserved field for the frame timestamp.
    mapping = {col: str(v) for col, v in values.items()}
    mapping["_ts"] = ts_iso
    pipe.hmset(latest_key, mapping)
    pipe.expire(latest_key, LATEST_TTL)

    # 2. window List: push full frame JSON (skip for stress-test to save ~150ms/tick)
    if not skip_window:
        window_key = f"rt:{device.device_code}:window"
        frame_json = json.dumps(
            {"device_code": device.device_code, "ts": ts_iso, "points": values},
            separators=(",", ":"),
        )
        pipe.lpush(window_key, frame_json)
        pipe.ltrim(window_key, 0, WINDOW_MAXLEN - 1)
        pipe.expire(window_key, WINDOW_TTL)

    # 3. Stream (requires Redis >= 5.0, disabled by default)
    if enable_stream:
        pipe.xadd(
            STREAM_KEY,
            {
                "device_code": device.device_code,
                "type": device.type_prefix,
                "ts": ts_iso,
                "payload": json.dumps(values, separators=(",", ":")),
            },
            maxlen=STREAM_MAXLEN,
            approximate=True,
        )


# ---------------------------------------------------------------------------
# Worker thread: handles a slice of devices at 100ms intervals
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

        # One pipeline per worker tick (all devices batched together).
        # With the lightweight value format (~8 bytes/field), total data per worker is
        # ~76 devices x 8KB = ~608KB — safe to send as one pipeline (was 3.8MB before).
        # This replaces 76 sequential round-trips with 1, cutting tick time ~10x.
        try:
            pipe = redis_client.pipeline(transaction=False)
            for device in devices:
                values = generate_values(device, tick)
                write_device_frame(pipe, device, values, ts_ms, enable_stream, skip_window)
            pipe.execute()
            stat.frames += len(devices)
        except Exception as exc:
            stat.errors += 1
            print(
                f"[ERROR] worker={worker_id} tick={tick} error={exc}",
                file=sys.stderr,
            )

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
            interval_s_actual = now - last_print
            fps = batch_frames / max(interval_s_actual, 0.001)
            avg_ms = sum(all_elapsed) / len(all_elapsed) if all_elapsed else 0.0
            max_ms = max(all_elapsed) if all_elapsed else 0.0

            print(
                f"t={now - started_at:.1f}s  "
                f"frames/s={fps:.0f}  "
                f"total_frames={total_frames + batch_frames}  "
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
    parser = argparse.ArgumentParser(
        description=(
            "Simulate Redis writes for real devices loaded from TDengine child tables, "
            "at configurable intervals. Each device frame writes latest Hash, "
            "window List, and optionally one Stream entry."
        )
    )
    parser.add_argument(
        "--config",
        type=Path,
        default=_COLLECTOR_ROOT / "config" / "settings.toml",
        help="Collector settings.toml path (used to load TDengine device list).",
    )
    parser.add_argument("--host", default="127.0.0.1", help="Redis host.")
    parser.add_argument("--port", type=int, default=6379, help="Redis port.")
    parser.add_argument("--db", type=int, default=0, help="Redis DB index.")
    parser.add_argument("--password", default="", help="Redis password.")
    parser.add_argument(
        "--interval-ms",
        type=int,
        default=100,
        help="Write interval per worker in milliseconds. Default: 100.",
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=4,
        help="Number of parallel writer threads (devices split evenly). Default: 4.",
    )
    parser.add_argument(
        "--duration-seconds",
        type=float,
        default=60.0,
        help="Run duration in seconds. 0 = run until Ctrl+C. Default: 60.",
    )
    parser.add_argument(
        "--enable-stream",
        action="store_true",
        help="Enable Redis Stream XADD (requires Redis >= 5.0). Default: disabled.",
    )
    parser.add_argument(
        "--skip-window",
        action="store_true",
        help="Skip window List writes (saves ~150ms/tick via json.dumps). Use for TDengine stress-test.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print device topology and exit without writing.",
    )
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

    print(f"Loading device list from TDengine via config: {args.config}")
    all_devices = build_all_devices(args.config)
    total = len(all_devices)
    by_type: dict[str, int] = {}
    for d in all_devices:
        by_type[d.type_prefix] = by_type.get(d.type_prefix, 0) + 1

    print(f"Device topology: {dict(by_type)}  total={total}")
    print(f"Points per device: {POINTS_PER_DEVICE}")
    print(f"Total signal columns: {total * POINTS_PER_DEVICE:,}")
    print(f"Interval: {args.interval_ms} ms   Workers: {args.workers}")
    print(f"Redis: {args.host}:{args.port}/{args.db}")
    print(f"Stream key: {STREAM_KEY}  maxlen≈{STREAM_MAXLEN}")

    if args.dry_run:
        print("\nDry run. Sample device codes:")
        for d in all_devices[:5]:
            print(f"  {d.device_code}: {d.columns[0]} .. {d.columns[-1]}")
        return 0

    r = redis.Redis(
        host=args.host,
        port=args.port,
        db=args.db,
        password=args.password or None,
        decode_responses=True,
        socket_connect_timeout=5,
    )
    try:
        r.ping()
    except Exception as exc:
        print(f"ERROR: cannot connect to Redis: {exc}", file=sys.stderr)
        return 1

    print("\nStarting writers...")

    chunks = split_evenly(all_devices, args.workers)
    stop_event = threading.Event()
    stats: list[WorkerStat] = []
    threads: list[threading.Thread] = []

    for worker_id, chunk in enumerate(chunks, start=1):
        # Each worker gets its own Redis connection for thread safety
        worker_redis = redis.Redis(
            host=args.host,
            port=args.port,
            db=args.db,
            password=args.password or None,
            decode_responses=True,
        )
        stat = WorkerStat(worker_id=worker_id)
        stats.append(stat)
        t = threading.Thread(
            target=run_worker,
            name=f"redis-writer-{worker_id}",
            args=(worker_id, chunk, worker_redis, args.interval_ms, stop_event, stat),
            kwargs={"enable_stream": args.enable_stream, "skip_window": args.skip_window},
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
