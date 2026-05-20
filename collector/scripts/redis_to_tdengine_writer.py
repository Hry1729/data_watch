#!/usr/bin/env python3
"""Consume Redis Stream rt:stream and batch-write to TDengine wide tables.

Reads from: rt:stream (produced by redis_mock_writer.py or real collector)
Writes to:  TDengine device_data.{qc,asc,agv} supertables (wide-row model)

Stream entry format (set by redis_mock_writer.py):
  device_code : e.g. "qc1"
  type        : e.g. "qc"  (= supertable name)
  ts          : ISO timestamp, e.g. "2026-05-19T13:00:00.100"
  payload     : JSON object {"qc1": 1.23, "qc2": 4.56, ...}

Column naming follows tdengine_write_benchmark.py signal_column_name():
  {stable}{index}  e.g. qc1..qc1000 for qc supertable
                        asc1..asc1000 for asc supertable
                        agv1..agv1000 for agv supertable

TDengine schema expected (created by init_tdengine_benchmark_tables.py):
  device_data.qc   supertable: ts TIMESTAMP, qc1..qc1000 DOUBLE  TAGS(device_code)
  device_data.asc  supertable: ts TIMESTAMP, asc1..asc1000 DOUBLE TAGS(device_code)
  device_data.agv  supertable: ts TIMESTAMP, agv1..agv1000 DOUBLE TAGS(device_code)

Usage:
  python redis_to_tdengine_writer.py [options]

  Required companion: collector config (settings.toml) for TDengine credentials.
  The script runs from collector/ root or pass --config explicitly.
"""

from __future__ import annotations

import argparse
import json
import queue
import re
import statistics
import sys
import threading
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

SCRIPT_DIR = Path(__file__).resolve().parent
COLLECTOR_ROOT = SCRIPT_DIR.parent

# Auto-detect dependencies: vendor dir (taosws wheel) and/or venv site-packages.
VENDOR_DIR = COLLECTOR_ROOT / "vendor"
if VENDOR_DIR.exists():
    sys.path.insert(0, str(VENDOR_DIR))

VENV_DIR = COLLECTOR_ROOT / ".venv"
if VENV_DIR.exists():
    # Windows venv: venv/Lib/site-packages
    _win_sp = VENV_DIR / "Lib" / "site-packages"
    if _win_sp.exists():
        sys.path.insert(0, str(_win_sp))
    # Linux/macOS venv: venv/lib/python3.x/site-packages
    else:
        import glob as _glob
        for _sp in _glob.glob(str(VENV_DIR / "lib" / "python*" / "site-packages")):
            sys.path.insert(0, _sp)

try:
    import taosws
except ImportError:
    print("ERROR: taosws not found. Ensure collector venv is active.", file=sys.stderr)
    sys.exit(1)

try:
    import redis as redis_lib
except ImportError:
    print("ERROR: redis-py not installed. Run: pip install redis", file=sys.stderr)
    sys.exit(1)

sys.path.insert(0, str(COLLECTOR_ROOT))
from core.config_loader import load_collector_config  # noqa: E402

DEFAULT_CONFIG = COLLECTOR_ROOT / "config" / "settings.toml"
STREAM_KEY = "rt:stream"
CONSUMER_GROUP = "writer-group"
IDENTIFIER_RE = re.compile(r"[^A-Za-z0-9_]")


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def quote_id(name: str) -> str:
    return f"`{name.replace('`', '``')}`"


def normalize_id(raw: str) -> str:
    cleaned = IDENTIFIER_RE.sub("_", str(raw).strip()).lower()
    return cleaned if cleaned else "unknown"


def parse_ts_to_ms(ts: str) -> int:
    """Parse ISO timestamp string to milliseconds epoch."""
    # e.g. "2026-05-19T13:00:00.100"
    try:
        base, _, frac = ts.partition(".")
        frac_ms = int(frac[:3].ljust(3, "0")) if frac else 0
        import calendar
        t = time.strptime(base, "%Y-%m-%dT%H:%M:%S")
        epoch_s = int(calendar.timegm(t))
        # Use local time assumption (match redis_mock_writer which uses localtime)
        epoch_s = int(time.mktime(t))
        return epoch_s * 1000 + frac_ms
    except Exception:
        return int(time.time() * 1000)


# ---------------------------------------------------------------------------
# TDengine connection helpers
# ---------------------------------------------------------------------------

def td_connect(config_path: Path):
    config = load_collector_config(str(config_path))
    td = config.tdengine
    return taosws.connect(
        user=td.user,
        password=td.password,
        host=td.host,
        port=td.port,
    )


def td_execute(conn, sql: str) -> None:
    conn.execute(sql)


# ---------------------------------------------------------------------------
# Per-device batch accumulator
# A single "wide row" contains ts + all 1000 column values.
# We accumulate rows and flush when the batch is large enough.
# ---------------------------------------------------------------------------

@dataclass
class DeviceBatch:
    device_code: str
    type_prefix: str
    rows: list[tuple[int, dict[str, float]]] = field(default_factory=list)
    # ts_ms → column_values

    def add(self, ts_ms: int, values: dict[str, float]) -> None:
        self.rows.append((ts_ms, values))

    def clear(self) -> None:
        self.rows.clear()


# ---------------------------------------------------------------------------
# TDengine batch writer using stmt2 (wide supertable INSERT)
# INSERT INTO {stable} (`tbname`, `ts`, col1, col2, ...) VALUES (?, ?, ?, ?, ...)
# ---------------------------------------------------------------------------

class DeviceStmt2Writer:
    """Manages one stmt2 per supertable for bulk wide-row inserts."""

    def __init__(
        self,
        conn,
        database: str,
        stable: str,
        points_per_device: int,
    ):
        self.database = database
        self.stable = stable
        self._conn = conn
        # Column names follow signal_column_name(stable, i) = f"{stable}{i}"
        # exactly matching tdengine_write_benchmark.py
        self.column_names: list[str] = [
            f"{stable}{i}" for i in range(1, points_per_device + 1)
        ]
        self._stmt = None
        self._created_tables: set[str] = set()  # cache to avoid repeated CREATE TABLE
        self._build_stmt(conn)

    def _build_stmt(self, conn) -> None:
        cols = [quote_id("tbname"), quote_id("ts")]
        cols.extend(quote_id(c) for c in self.column_names)
        placeholders = ", ".join("?" for _ in cols)
        sql = (
            f"INSERT INTO {quote_id(self.stable)} "
            f"({', '.join(cols)}) VALUES ({placeholders})"
        )
        conn.execute(f"USE {quote_id(self.database)}")
        self._stmt = conn.stmt2_statement()
        self._stmt.prepare(sql)

    def _ensure_tables(self, device_codes: list[str]) -> None:
        """CREATE TABLE IF NOT EXISTS for device codes not yet seen.

        Follows the same batch-create pattern as init_tdengine_benchmark_tables.py.
        Runs only once per device_code (cached in _created_tables).
        """
        new_codes = [c for c in device_codes if c not in self._created_tables]
        if not new_codes:
            return
        db_id = quote_id(self.database)
        st_id = quote_id(self.stable)
        # TDengine 3.x batch CREATE TABLE requires IF NOT EXISTS before EACH table name.
        # Putting it only once at the start only guards the first table.
        parts = [
            f"IF NOT EXISTS {db_id}.{quote_id(code)} USING {db_id}.{st_id} TAGS ('{code}')"
            for code in new_codes
        ]
        sql = "CREATE TABLE " + " ".join(parts)
        # Update cache first so we never retry even if the execute fails.
        self._created_tables.update(new_codes)
        self._conn.execute(sql)

    def write_batches(self, batches: list[DeviceBatch]) -> int:
        """Bind all device batches and execute in one stmt2 call."""
        if not batches:
            return 0

        # Auto-create child tables for any device_code seen for the first time.
        self._ensure_tables([b.device_code for b in batches if b.rows])

        params = []
        for batch in batches:
            if not batch.rows:
                continue
            timestamps = [ts_ms for ts_ms, _ in batch.rows]
            ts_col = taosws.millis_timestamps_to_column(timestamps)
            # Build one column array per signal column
            signal_cols = []
            for col_name in self.column_names:
                values = [float(row_vals.get(col_name, 0.0)) for _, row_vals in batch.rows]
                signal_cols.append(taosws.doubles_to_column(values))
            params.append(
                taosws.stmt2_bind_param_view(
                    table_name=batch.device_code,
                    tags=[],
                    columns=[ts_col] + signal_cols,
                )
            )
        if not params:
            return 0
        self._stmt.bind(params)
        return int(self._stmt.execute())

    def close(self) -> None:
        if self._stmt is not None:
            self._stmt.close()
            self._stmt = None


# ---------------------------------------------------------------------------
# Stream consumer + writer worker
# ---------------------------------------------------------------------------

@dataclass
class WorkerMetric:
    worker_id: int
    entries_read: int = 0
    rows_written: int = 0
    errors: int = 0
    ack_ids: list[str] = field(default_factory=list)
    elapsed_ms: float = 0.0


def build_redis_client(args: argparse.Namespace) -> redis_lib.Redis:
    return redis_lib.Redis(
        host=args.redis_host,
        port=args.redis_port,
        db=args.redis_db,
        password=args.redis_password or None,
        decode_responses=True,
        socket_connect_timeout=5,
    )


def ensure_consumer_group(r: redis_lib.Redis) -> None:
    """Create consumer group if not exists (MKSTREAM creates stream too)."""
    try:
        r.xgroup_create(STREAM_KEY, CONSUMER_GROUP, id="0", mkstream=True)
        print(f"[stream] created consumer group '{CONSUMER_GROUP}'")
    except redis_lib.exceptions.ResponseError as exc:
        if "BUSYGROUP" in str(exc):
            pass  # already exists
        else:
            raise


def run_writer_worker(
    worker_id: int,
    args: argparse.Namespace,
    metric_queue: "queue.Queue[WorkerMetric]",
    stop_event: threading.Event,
) -> None:
    """Single writer thread: read from stream → batch by device → write TDengine."""
    consumer_name = f"writer-{worker_id}"
    r = build_redis_client(args)
    conn = td_connect(args.config)
    conn.execute(f"USE {quote_id(args.database)}")

    # stmt2 writers keyed by stable (type_prefix), one per supertable
    stmt_writers: dict[str, DeviceStmt2Writer] = {}
    # column names per stable (shared by all devices of same type)
    # follows signal_column_name(stable, i) = f"{stable}{i}"
    POINTS_PER_DEVICE = args.points_per_device

    def get_stmt_writer(stable: str) -> DeviceStmt2Writer:
        if stable not in stmt_writers:
            stmt_writers[stable] = DeviceStmt2Writer(
                conn, args.database, stable, POINTS_PER_DEVICE
            )
        return stmt_writers[stable]

    flush_interval_s = args.flush_interval_ms / 1000.0
    batch_count = args.batch_count

    # Pending device batches keyed by device_code
    pending: dict[str, DeviceBatch] = {}
    pending_ids: list[str] = []

    def flush() -> WorkerMetric:
        metric = WorkerMetric(worker_id=worker_id)
        if not pending:
            return metric

        # Group device batches by stable (type_prefix)
        by_stable: dict[str, list[DeviceBatch]] = {}
        for batch in pending.values():
            if not batch.rows:
                continue
            st = batch.type_prefix
            if st not in by_stable:
                by_stable[st] = []
            by_stable[st].append(batch)

        start = time.perf_counter()
        total_written = 0
        for stable, batches in by_stable.items():
            try:
                writer = get_stmt_writer(stable)
                total_written += writer.write_batches(batches)
            except Exception as exc:
                metric.errors += 1
                print(
                    f"[ERROR] worker={worker_id} stable={stable} error={exc}",
                    file=sys.stderr,
                )

        metric.rows_written = total_written
        metric.elapsed_ms = (time.perf_counter() - start) * 1000
        metric.ack_ids = list(pending_ids)

        pending.clear()
        pending_ids.clear()
        return metric

    next_flush = time.perf_counter() + flush_interval_s

    try:
        while not stop_event.is_set():
            # XREADGROUP: read up to batch_count new messages
            try:
                response = r.xreadgroup(
                    groupname=CONSUMER_GROUP,
                    consumername=consumer_name,
                    streams={STREAM_KEY: ">"},
                    count=batch_count,
                    block=int(flush_interval_s * 1000 / 2),
                )
            except Exception as exc:
                print(f"[ERROR] worker={worker_id} xreadgroup error={exc}", file=sys.stderr)
                time.sleep(1)
                continue

            # Parse entries
            if response:
                for _stream_key, entries in response:
                    for entry_id, entry_data in entries:
                        device_code = entry_data.get("device_code", "")
                        type_prefix = entry_data.get("type", "")
                        ts_str = entry_data.get("ts", "")
                        payload_str = entry_data.get("payload", "{}")

                        if not device_code or not type_prefix:
                            pending_ids.append(entry_id)
                            continue

                        try:
                            payload: dict[str, float] = json.loads(payload_str)
                        except Exception:
                            pending_ids.append(entry_id)
                            continue

                        ts_ms = parse_ts_to_ms(ts_str) if ts_str else int(time.time() * 1000)

                        if device_code not in pending:
                            pending[device_code] = DeviceBatch(
                                device_code=device_code,
                                type_prefix=type_prefix,
                            )
                        pending[device_code].add(ts_ms, payload)
                        pending_ids.append(entry_id)

            # Flush when interval elapsed or buffer is large enough
            now = time.perf_counter()
            buffer_size = sum(len(b.rows) for b in pending.values())
            if now >= next_flush or buffer_size >= args.max_buffer_rows:
                metric = flush()
                # ACK successfully processed IDs
                if metric.ack_ids and metric.errors == 0:
                    # Batch ACK in chunks
                    ack_ids = metric.ack_ids
                    chunk_size = 500
                    for i in range(0, len(ack_ids), chunk_size):
                        chunk = ack_ids[i : i + chunk_size]
                        try:
                            r.xack(STREAM_KEY, CONSUMER_GROUP, *chunk)
                        except Exception as exc:
                            print(f"[WARN] xack failed: {exc}", file=sys.stderr)

                metric.entries_read = len(metric.ack_ids)
                metric_queue.put(metric)
                next_flush = time.perf_counter() + flush_interval_s

    finally:
        # Final flush
        metric = flush()
        if metric.ack_ids and metric.errors == 0:
            try:
                r.xack(STREAM_KEY, CONSUMER_GROUP, *metric.ack_ids)
            except Exception:
                pass
        for w in stmt_writers.values():
            w.close()
        conn.close()


# ---------------------------------------------------------------------------
# Stats printer
# ---------------------------------------------------------------------------

def print_stats(
    metric_queue: "queue.Queue[WorkerMetric]",
    started_at: float,
    duration_s: float,
    stop_event: threading.Event,
    threads: list[threading.Thread],
    report_interval: float = 2.0,
) -> None:
    last_print = time.perf_counter()
    total_entries = 0
    total_rows = 0
    total_errors = 0
    elapsed_samples: list[float] = []

    while any(t.is_alive() for t in threads):
        if duration_s > 0 and time.perf_counter() - started_at >= duration_s:
            stop_event.set()

        try:
            m = metric_queue.get(timeout=0.1)
            total_entries += m.entries_read
            total_rows += m.rows_written
            total_errors += m.errors
            if m.elapsed_ms > 0:
                elapsed_samples.append(m.elapsed_ms)
        except queue.Empty:
            pass

        now = time.perf_counter()
        if now - last_print >= report_interval:
            interval = now - last_print
            rows_per_s = total_rows / max(time.perf_counter() - started_at, 0.001)
            avg_ms = statistics.fmean(elapsed_samples) if elapsed_samples else 0.0
            print(
                f"t={now - started_at:.1f}s  "
                f"stream_entries={total_entries}  "
                f"td_rows={total_rows}  "
                f"rows/s={rows_per_s:.0f}  "
                f"errors={total_errors}  "
                f"avg_write_ms={avg_ms:.1f}"
            )
            last_print = now
            elapsed_samples.clear()

    stop_event.set()


# ---------------------------------------------------------------------------
# Argument parsing & main
# ---------------------------------------------------------------------------

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Consume Redis Stream rt:stream and batch-write wide rows to TDengine. "
            "Designed to work with redis_mock_writer.py output."
        )
    )
    # TDengine
    parser.add_argument(
        "--config",
        type=Path,
        default=Path(DEFAULT_CONFIG),
        help="Collector settings.toml path for TDengine credentials.",
    )
    parser.add_argument("--database", default="device_data", help="TDengine database.")

    # Redis
    parser.add_argument("--redis-host", default="127.0.0.1", help="Redis host.")
    parser.add_argument("--redis-port", type=int, default=6379, help="Redis port.")
    parser.add_argument("--redis-db", type=int, default=0, help="Redis DB index.")
    parser.add_argument("--redis-password", default="", help="Redis password.")

    # Writer behaviour
    parser.add_argument(
        "--flush-interval-ms",
        type=int,
        default=500,
        help="How often to flush buffered entries to TDengine (ms). Default: 500.",
    )
    parser.add_argument(
        "--batch-count",
        type=int,
        default=200,
        help="Max stream entries to read per XREADGROUP call. Default: 200.",
    )
    parser.add_argument(
        "--max-buffer-rows",
        type=int,
        default=2000,
        help="Flush early if buffered row count exceeds this. Default: 2000.",
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=1,
        help="Number of parallel writer threads (each has its own stream consumer). Default: 1.",
    )
    parser.add_argument(
        "--duration-seconds",
        type=float,
        default=60.0,
        help="Run duration. 0 = run until Ctrl+C. Default: 60.",
    )
    parser.add_argument(
        "--points-per-device",
        type=int,
        default=1000,
        help="Signal columns per device row (must match TDengine supertable schema). Default: 1000.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Connect and read stream but do not write to TDengine.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()

    print(f"Config: {args.config}")
    print(f"TDengine database: {args.database}")
    print(f"Redis: {args.redis_host}:{args.redis_port}/{args.redis_db}")
    print(f"Stream key: {STREAM_KEY}  Consumer group: {CONSUMER_GROUP}")
    print(f"Flush interval: {args.flush_interval_ms} ms  Workers: {args.workers}")

    if args.dry_run:
        print("Dry run mode: no TDengine writes.")

    # Verify Redis connection and ensure consumer group
    r = redis_lib.Redis(
        host=args.redis_host,
        port=args.redis_port,
        db=args.redis_db,
        password=args.redis_password or None,
        decode_responses=True,
        socket_connect_timeout=5,
    )
    try:
        r.ping()
    except Exception as exc:
        print(f"ERROR: cannot connect to Redis: {exc}", file=sys.stderr)
        return 1

    ensure_consumer_group(r)
    r.close()

    stop_event = threading.Event()
    metric_queue: queue.Queue[WorkerMetric] = queue.Queue()
    threads: list[threading.Thread] = []

    for worker_id in range(1, args.workers + 1):
        t = threading.Thread(
            target=run_writer_worker,
            name=f"td-writer-{worker_id}",
            args=(worker_id, args, metric_queue, stop_event),
            daemon=True,
        )
        threads.append(t)

    started_at = time.perf_counter()
    for t in threads:
        t.start()
    print(f"\nStarted {len(threads)} writer thread(s). Press Ctrl+C to stop.\n")

    try:
        print_stats(metric_queue, started_at, args.duration_seconds, stop_event, threads)
    except KeyboardInterrupt:
        print("\nStopping...")
        stop_event.set()

    for t in threads:
        t.join(timeout=10)

    print("Done.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
