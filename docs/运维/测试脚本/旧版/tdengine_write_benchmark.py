#!/usr/bin/env python3
"""Write benchmark rows to TDengine child tables every fixed interval."""

from __future__ import annotations

import argparse
import os
import queue
import re
import statistics
import sys
import threading
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

import taosws


SCRIPT_DIR = Path(__file__).resolve().parent
COLLECTOR_ROOT = SCRIPT_DIR.parent
DEFAULT_CONFIG = COLLECTOR_ROOT / "config" / "settings.toml"
sys.path.insert(0, str(COLLECTOR_ROOT))

from core.config_loader import load_collector_config  # noqa: E402


def quote_identifier(name: str) -> str:
    return f"`{name.replace('`', '``')}`"


def connect(config_path: Path):
    config = load_collector_config(str(config_path))
    td = config.tdengine
    return taosws.connect(
        user=td.user,
        password=td.password,
        host=td.host,
        port=td.port,
    )


def query_rows(conn, sql: str) -> list[tuple]:
    return [tuple(row) for row in conn.query(sql)]


def load_stables(conn, database: str) -> list[str]:
    conn.execute(f"USE {quote_identifier(database)}")
    return [str(row[0]) for row in query_rows(conn, "SHOW STABLES")]


def stable_has_current_schema(conn, database: str, stable: str) -> tuple[bool, str]:
    rows = query_rows(
        conn,
        f"DESCRIBE {quote_identifier(database)}.{quote_identifier(stable)}",
    )
    columns: set[str] = set()
    tags: set[str] = set()
    for row in rows:
        name = str(row[0]).lower()
        note = str(row[3]).upper() if len(row) > 3 else ""
        if note == "TAG":
            tags.add(name)
        else:
            columns.add(name)

    missing_columns = {"ts", "value", "quality"} - columns
    missing_tags = {"var_seq"} - tags
    if missing_columns or missing_tags:
        parts: list[str] = []
        if missing_columns:
            parts.append(f"missing columns={sorted(missing_columns)}")
        if missing_tags:
            parts.append(f"missing tags={sorted(missing_tags)}")
        return False, ", ".join(parts)
    return True, ""


def child_table_name(stable: str, var_seq: int) -> str:
    return f"{stable}_{var_seq}"


@dataclass(frozen=True)
class Target:
    database: str
    stable: str
    table_name: str
    var_seq: int


def iter_chunks(items: Sequence[Target], size: int) -> Iterable[Sequence[Target]]:
    for index in range(0, len(items), size):
        yield items[index : index + size]


def split_evenly(items: Sequence[Target], chunks: int) -> list[list[Target]]:
    chunks = max(1, chunks)
    result: list[list[Target]] = []
    for index in range(chunks):
        start = index * len(items) // chunks
        end = (index + 1) * len(items) // chunks
        part = list(items[start:end])
        if part:
            result.append(part)
    return result


def discover_targets(args: argparse.Namespace) -> dict[str, list[Target]]:
    pattern = re.compile(args.stable_filter) if args.stable_filter else None
    var_seqs = list(
        range(args.start_var_seq, args.start_var_seq + args.subtables_per_stable)
    )
    targets: dict[str, list[Target]] = {}

    conn = connect(args.config)
    try:
        for database in args.databases:
            stables = load_stables(conn, database)
            if pattern:
                stables = [name for name in stables if pattern.search(name)]
            if args.limit_stables > 0:
                stables = stables[: args.limit_stables]

            database_targets: list[Target] = []
            for stable in stables:
                ok, reason = stable_has_current_schema(conn, database, stable)
                if not ok:
                    print(f"[WARN] skip {database}.{stable}: {reason}")
                    continue
                database_targets.extend(
                    Target(
                        database=database,
                        stable=stable,
                        table_name=child_table_name(stable, seq),
                        var_seq=seq,
                    )
                    for seq in var_seqs
                )
            targets[database] = database_targets
    finally:
        conn.close()

    return targets


@dataclass
class WorkerMetric:
    database: str
    worker_id: int
    cycles: int
    attempted: int
    affected: int
    elapsed_ms: float
    lag_ms: float
    errors: int = 0


def make_values(cycle: int, row_count: int) -> tuple[list[int], list[float], list[int]]:
    ts_ms = int(time.time() * 1000)
    values = [float(cycle)] * row_count
    quality = [0] * row_count
    timestamps = [ts_ms] * row_count
    return timestamps, values, quality


def write_stmt2_batch(stmt, targets: Sequence[Target], cycle: int) -> int:
    timestamps, values, quality = make_values(cycle, len(targets))
    params = []
    for index, target in enumerate(targets):
        params.append(
            taosws.stmt2_bind_param_view(
                table_name=target.table_name,
                tags=[],
                columns=[
                    taosws.millis_timestamps_to_column([timestamps[index]]),
                    taosws.doubles_to_column([values[index]]),
                    taosws.tiny_ints_to_column([quality[index]]),
                ],
            )
        )
    stmt.bind(params)
    return int(stmt.execute())


def group_targets_by_stable(targets: Sequence[Target]) -> dict[str, list[Target]]:
    grouped: dict[str, list[Target]] = {}
    for target in targets:
        grouped.setdefault(target.stable, []).append(target)
    return grouped


def run_worker(
    config_path: Path,
    database: str,
    worker_id: int,
    targets: Sequence[Target],
    bind_batch_size: int,
    interval_seconds: float,
    stop_event: threading.Event,
    metrics: "queue.Queue[WorkerMetric]",
) -> None:
    conn = None
    statements = {}
    cycle = 0
    try:
        conn = connect(config_path)
        conn.execute(f"USE {quote_identifier(database)}")
        targets_by_stable = group_targets_by_stable(targets)

        next_start = time.perf_counter()
        while not stop_event.is_set():
            cycle += 1
            started = time.perf_counter()
            attempted = 0
            affected = 0
            errors = 0
            try:
                for stable, stable_targets in targets_by_stable.items():
                    stmt = statements.get(stable)
                    if stmt is None:
                        stmt = conn.stmt2_statement()
                        stmt.prepare(
                            f"INSERT INTO {quote_identifier(stable)} "
                            f"({quote_identifier('tbname')}, "
                            f"{quote_identifier('ts')}, "
                            f"{quote_identifier('value')}, "
                            f"{quote_identifier('quality')}) "
                            "VALUES (?,?,?,?)"
                        )
                        statements[stable] = stmt
                    for chunk in iter_chunks(stable_targets, bind_batch_size):
                        attempted += len(chunk)
                        affected += write_stmt2_batch(stmt, chunk, cycle)
            except Exception as exc:
                errors += 1
                print(
                    f"[ERROR] worker failed db={database} worker={worker_id} "
                    f"cycle={cycle} error={exc}"
                )

            elapsed_ms = (time.perf_counter() - started) * 1000
            next_start += interval_seconds
            lag_ms = max(0.0, (time.perf_counter() - next_start) * 1000)
            metrics.put(
                WorkerMetric(
                    database=database,
                    worker_id=worker_id,
                    cycles=1,
                    attempted=attempted,
                    affected=affected,
                    elapsed_ms=elapsed_ms,
                    lag_ms=lag_ms,
                    errors=errors,
                )
            )

            sleep_seconds = next_start - time.perf_counter()
            if sleep_seconds > 0:
                stop_event.wait(sleep_seconds)
            else:
                next_start = time.perf_counter()

    except Exception as exc:
        print(f"[ERROR] worker crashed db={database} worker={worker_id} error={exc}")
        metrics.put(
            WorkerMetric(
                database=database,
                worker_id=worker_id,
                cycles=0,
                attempted=0,
                affected=0,
                elapsed_ms=0.0,
                lag_ms=0.0,
                errors=1,
            )
        )
        stop_event.set()

    finally:
        for stmt in statements.values():
            stmt.close()
        if conn is not None:
            conn.close()


def print_summary(
    metrics: "queue.Queue[WorkerMetric]",
    started_at: float,
    duration_seconds: float,
    stop_event: threading.Event,
    threads: Sequence[threading.Thread],
) -> None:
    last_print = time.perf_counter()
    attempted = 0
    affected = 0
    cycles = 0
    errors = 0
    elapsed_samples: list[float] = []
    lag_samples: list[float] = []

    while any(thread.is_alive() for thread in threads):
        now = time.perf_counter()
        if duration_seconds > 0 and now - started_at >= duration_seconds:
            stop_event.set()

        try:
            item = metrics.get(timeout=0.2)
            attempted += item.attempted
            affected += item.affected
            cycles += item.cycles
            errors += item.errors
            elapsed_samples.append(item.elapsed_ms)
            lag_samples.append(item.lag_ms)
        except queue.Empty:
            pass

        now = time.perf_counter()
        if now - last_print >= 1.0:
            interval_seconds = now - last_print
            average_elapsed = statistics.fmean(elapsed_samples) if elapsed_samples else 0.0
            max_elapsed = max(elapsed_samples) if elapsed_samples else 0.0
            max_lag = max(lag_samples) if lag_samples else 0.0
            print(
                f"t={now - started_at:.1f}s "
                f"cycles={cycles} attempted={attempted} affected={affected} "
                f"rows_per_sec={affected / max(interval_seconds, 0.001):.0f} "
                f"errors={errors} avg_worker_ms={average_elapsed:.2f} "
                f"max_worker_ms={max_elapsed:.2f} max_lag_ms={max_lag:.2f}"
            )
            last_print = now
            attempted = 0
            affected = 0
            cycles = 0
            errors = 0
            elapsed_samples.clear()
            lag_samples.clear()

    stop_event.set()


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Write one row into each benchmark child table at a fixed interval."
        )
    )
    parser.add_argument(
        "--config",
        type=Path,
        default=Path(os.getenv("COLLECTOR_CONFIG", DEFAULT_CONFIG)),
        help="Collector TOML config path.",
    )
    parser.add_argument(
        "--databases",
        nargs="+",
        default=["qc", "asc", "agv"],
        help="TDengine databases to scan.",
    )
    parser.add_argument(
        "--subtables-per-stable",
        type=int,
        default=1000,
        help="Number of benchmark child tables expected for each supertable.",
    )
    parser.add_argument(
        "--start-var-seq",
        type=int,
        default=1,
        help="First var_seq value. Default targets 1..1000.",
    )
    parser.add_argument(
        "--interval-ms",
        type=int,
        default=100,
        help="Write interval per worker in milliseconds.",
    )
    parser.add_argument(
        "--duration-seconds",
        type=float,
        default=60.0,
        help="Benchmark duration. Use 0 to run until Ctrl+C.",
    )
    parser.add_argument(
        "--workers-per-db",
        type=int,
        default=4,
        help="Parallel writer threads per TDengine database.",
    )
    parser.add_argument(
        "--bind-batch-size",
        type=int,
        default=1000,
        help="Tables bound/executed per TDengine statement.",
    )
    parser.add_argument(
        "--stable-filter",
        default="",
        help="Optional regex; only matching supertables are targeted.",
    )
    parser.add_argument(
        "--limit-stables",
        type=int,
        default=0,
        help="Optional smoke-test limit per database. 0 means no limit.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Only print discovered target counts; do not write rows.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.subtables_per_stable <= 0:
        raise SystemExit("--subtables-per-stable must be greater than 0")
    if args.interval_ms <= 0:
        raise SystemExit("--interval-ms must be greater than 0")
    if args.workers_per_db <= 0:
        raise SystemExit("--workers-per-db must be greater than 0")
    if args.bind_batch_size <= 0:
        raise SystemExit("--bind-batch-size must be greater than 0")

    targets_by_db = discover_targets(args)
    total_targets = sum(len(tables) for tables in targets_by_db.values())
    print(f"Config: {args.config}")
    print("Mode: stmt2-existing")
    print(f"Interval: {args.interval_ms} ms")
    print(f"Total target child tables: {total_targets}")
    for database, tables in targets_by_db.items():
        print(f"  {database}: {len(tables)} tables")

    if total_targets == 0:
        print("No target tables discovered.")
        return 1
    if args.dry_run:
        print("Dry run complete. No rows written.")
        return 0

    stop_event = threading.Event()
    metrics: queue.Queue[WorkerMetric] = queue.Queue()
    threads: list[threading.Thread] = []
    interval_seconds = args.interval_ms / 1000.0

    for database, tables in targets_by_db.items():
        for worker_id, chunk in enumerate(
            split_evenly(tables, args.workers_per_db),
            start=1,
        ):
            thread = threading.Thread(
                target=run_worker,
                name=f"{database}-writer-{worker_id}",
                args=(
                    args.config,
                    database,
                    worker_id,
                    chunk,
                    args.bind_batch_size,
                    interval_seconds,
                    stop_event,
                    metrics,
                ),
                daemon=True,
            )
            threads.append(thread)

    started_at = time.perf_counter()
    for thread in threads:
        thread.start()

    try:
        print_summary(metrics, started_at, args.duration_seconds, stop_event, threads)
    except KeyboardInterrupt:
        print("\nStopping benchmark...")
        stop_event.set()

    for thread in threads:
        thread.join(timeout=10)
    print("Benchmark stopped.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
