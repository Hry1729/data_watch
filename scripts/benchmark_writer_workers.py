"""Writer SQL-build / event-loop benchmark: 400 frames x 509 points x 4 workers.

Two modes, same workload and same code path (``TDengineWriter.write_multi_table``):

  offline  a stub connection that only accepts INSERTs. Isolates SQL construction
           cost -- the thing the SQL-build comparison is about -- with no database
           or network in the way.
  live     the real TDengine. Also reports how long the single shared asyncio
           event loop stalls while native calls are in flight, which is the
           production symptom of a connector that holds the GIL.

The event-loop probe samples every 5 ms and reports how late it ran. When the
native connector releases the GIL the loop keeps its schedule; when it does not,
the lag tracks the blocking call duration.

Usage:
    python scripts/benchmark_writer_workers.py --mode offline --output offline.json
    python scripts/benchmark_writer_workers.py --mode live --output live.json
"""
from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "writer"))

LAG_INTERVAL_S = 0.005


def build_rows(devices: int, points: int, base_ts: int) -> list[dict]:
    """One frame per device: the collector's shape, one child table per device."""
    return [
        {
            "child_table": f"bw{device:04d}",
            "stable": "sdqc",
            "device_code": f"bw{device:04d}",
            "ts": base_ts,
            "columns": {f"p{point}": float(point) + device for point in range(points)},
            "qualities": {str(point + 1): 0 if point % 10 else 192 for point in range(points)},
        }
        for device in range(devices)
    ]


class LagProbe:
    """Samples the event loop every LAG_INTERVAL_S and records how late it ran."""

    def __init__(self) -> None:
        self.samples_ms: list[float] = []
        self._task: asyncio.Task | None = None

    async def _run(self) -> None:
        while True:
            due = time.perf_counter() + LAG_INTERVAL_S
            await asyncio.sleep(LAG_INTERVAL_S)
            self.samples_ms.append(max(0.0, (time.perf_counter() - due) * 1000))

    def start(self) -> None:
        self._task = asyncio.create_task(self._run(), name="lag-probe")

    async def stop(self) -> None:
        if self._task is not None:
            self._task.cancel()
            try:
                await self._task
            except asyncio.CancelledError:
                pass

    def snapshot(self) -> dict:
        if not self.samples_ms:
            return {"samples": 0}
        ordered = sorted(self.samples_ms)
        return {
            "samples": len(ordered),
            "p50_ms": round(ordered[len(ordered) // 2], 3),
            "p99_ms": round(ordered[min(len(ordered) - 1, int(len(ordered) * 0.99))], 3),
            "max_ms": round(ordered[-1], 3),
        }


class StubConnection:
    """Offline stand-in: accepts INSERTs, refuses anything else."""

    def __init__(self) -> None:
        self.sizes: list[int] = []

    def execute(self, sql: str) -> int:
        if not sql.startswith("INSERT INTO "):
            raise AssertionError(f"offline mode attempted non-INSERT work: {sql[:80]}")
        self.sizes.append(len(sql.encode("utf-8")))
        return 1

    def close(self) -> None:
        pass


def _writer_stats(writer) -> dict:
    def stat(name):
        value = getattr(writer, name, 0)
        return round(float(value), 3) if isinstance(value, (int, float)) else 0.0

    return {
        "build_ms": stat("_last_build_ms"),
        "build_wait_ms": stat("_last_build_wait_ms"),
        "execute_ms": stat("_last_execute_ms"),
        "sql_bytes": int(stat("_last_sql_bytes")),
        "statements": len(getattr(writer, "_last_sql_statement_bytes", []) or []),
    }


async def _warm_up_schema(writers, partitions) -> None:
    """Create the super table before the measured window.

    Each connection issues CREATE STABLE IF NOT EXISTS on its first write; the
    loser of that race DESCRIBEs an empty table, caches no columns and defers
    its whole batch as a schema retry, which the writer re-sends on a later
    flush. That is correct production behaviour but it makes a single-shot
    benchmark measure the retry instead of the write. The warm-up row is the
    worker's own first row, so re-writing it in the timed pass overwrites the
    same (device, ts) and does not disturb the row counts.
    """
    from tdengine_writer import LANDED

    pairs = [(writer, part[0]) for writer, part in zip(writers, partitions) if part]
    for _ in range(20):
        results = await asyncio.gather(
            *(writer.write_multi_table([row]) for writer, row in pairs)
        )
        if all(status == LANDED for result in results for _, status, _ in result):
            return
        await asyncio.sleep(0.5)
    raise RuntimeError("schema warm-up did not converge")


async def run(args: argparse.Namespace) -> dict:
    from tdengine_writer import BAD_DATA, FAILED_INCOMPLETE, LANDED, RETRY, TDengineWriter

    base_ts = int(time.time() * 1000)
    rows = build_rows(args.frames, args.points, base_ts)
    points_columns = {f"p{point}" for point in range(args.points)}

    # Recorded before anything is written: a database that already existed is
    # the caller's, and this run must neither count it nor drop it.
    database_pre_existing = (
        _database_exists(args.td_host, args.td_port, args.td_database)
        if args.mode == "live" else False
    )

    writers = []
    stub_sizes: list[int] = []
    for _ in range(args.workers):
        writer = TDengineWriter()
        if args.mode == "offline":
            stub = StubConnection()
            writer._database = "offline_benchmark"
            writer._conn = stub
            writer._col_cache = {"sdqc": set(points_columns)}
            writer._quality_stable_ready = {"sqqc"}
            stub_sizes.append(stub)
        else:
            await writer.connect({
                "tdengine": {
                    "host": args.td_host,
                    "port": args.td_port,
                    "user": "root",
                    "password": "taosdata",
                    "database": args.td_database,
                },
                "writer": {"stable_names": ["sdqc"]},
            })
        writers.append(writer)

    partitions = [rows[index::args.workers] for index in range(args.workers)]
    if args.mode == "live":
        await _warm_up_schema(writers, partitions)

    lag = LagProbe()
    lag.start()
    started = time.perf_counter()
    try:
        results = await asyncio.gather(
            *(writer.write_multi_table(part) for writer, part in zip(writers, partitions))
        )
    finally:
        lag_span_ms = (time.perf_counter() - started) * 1000
        await lag.stop()

    outcomes: dict[str, int] = {}
    covered = 0
    for result in results:
        for indices, status, _ in result:
            outcomes[status] = outcomes.get(status, 0) + len(indices)
            covered += len(indices)

    if args.mode == "live":
        report_verification = await _verify(writers[0], rows, args)
    else:
        report_verification = None
    for writer in writers:
        await writer.close()
    if args.mode == "live" and not database_pre_existing:
        _drop_database(args.td_database, args.td_host, args.td_port)

    report: dict = {
        "mode": args.mode,
        "td_database": args.td_database if args.mode == "live" else None,
        "td_database_pre_existing": database_pre_existing,
        "frames": args.frames,
        "points_per_frame": args.points,
        "workers": args.workers,
        "total_points": args.frames * args.points,
        "wall_ms": round(lag_span_ms, 3),
        "rows_covered": covered,
        "rows_expected": len(rows),
        "outcomes": outcomes,
        "event_loop_lag": lag.snapshot(),
        "per_worker": [_writer_stats(writer) for writer in writers],
        # Rows landing is not enough: the same run must also read back the
        # counts, a value pair and a quality pair it expected.
        "passed": (
            covered == len(rows)
            and set(outcomes) <= {LANDED}
            and _verification_ok(report_verification)
        ),
    }
    if report_verification is not None:
        report["verification"] = report_verification
    report["build_ms_total"] = round(sum(w["build_ms"] for w in report["per_worker"]), 3)
    report["execute_ms_total"] = round(sum(w["execute_ms"] for w in report["per_worker"]), 3)
    report["sql_bytes_total"] = sum(w["sql_bytes"] for w in report["per_worker"])
    report["sql_statements_total"] = sum(w["statements"] for w in report["per_worker"])
    if args.mode == "offline":
        sizes = [size for stub in stub_sizes for size in stub.sizes]
        report["sql_chunks"] = len(sizes)
        report["max_sql_bytes"] = max(sizes, default=0)
    return report


def _database_exists(host: str, port: int, database: str) -> bool:
    import taosws

    connection = taosws.connect(f"ws://root:taosdata@{host}:{port}")
    try:
        existing = {str(row[0]).lower() for row in connection.query("SHOW DATABASES")}
    finally:
        connection.close()
    return database.lower() in existing


def _drop_database(database: str, host: str, port: int) -> None:
    """Remove the database this run created.

    Only ever called when the database did not exist beforehand: a caller who
    points --td-database at real data must not lose it to a benchmark run.
    """
    import taosws

    connection = taosws.connect(f"ws://root:taosdata@{host}:{port}")
    try:
        connection.execute(f"DROP DATABASE IF EXISTS {database}")
    finally:
        connection.close()


def _verification_ok(verification: dict | None) -> bool:
    """Row counts, sample values and sample qualities must match exactly."""
    if verification is None:
        return True
    if verification["value_rows"] != verification["value_rows_expected"]:
        return False
    if verification["quality_rows"] != verification["quality_rows_expected"]:
        return False
    if [list(map(float, row)) for row in verification["sample_values"]] != [
        list(map(float, row)) for row in [verification["sample_values_expected"]]
    ]:
        return False
    actual = {int(row[0]): int(row[1]) for row in verification["sample_qualities"]}
    expected = {
        int(key): int(value)
        for key, value in verification["sample_qualities_expected"].items()
    }
    return actual == expected


async def _verify(writer, rows: list[dict], args: argparse.Namespace) -> dict:
    """Read the written rows back: counts, one value pair, one quality sample."""
    from tdengine_writer import _quality_stable_from_value_stable, _quote

    db = _quote(args.td_database)
    first = rows[0]
    # Sampled from the row's own column set and quality map so the check holds
    # for any --points/--frames, not only the defaults.
    sample_columns = [list(first["columns"])[0], list(first["columns"])[-1]]
    sample_var_ids = sorted((int(key) for key in first["qualities"]))[:2]
    value_rows = await writer._query(f"SELECT COUNT(*) FROM {db}.{_quote('sdqc')}")
    quality_stable = _quality_stable_from_value_stable("sdqc")
    quality_rows = await writer._query(f"SELECT COUNT(*) FROM {db}.{_quote(quality_stable)}")
    sample = await writer._query(
        f"SELECT {', '.join(_quote(name) for name in sample_columns)} "
        f"FROM {db}.{_quote('bw0000')}"
    )
    quality_sample = await writer._query(
        f"SELECT {_quote('var_id')}, {_quote('quality')} FROM "
        f"{db}.{_quote('bw0000_q')} "
        f"WHERE {_quote('var_id')} IN ({', '.join(str(v) for v in sample_var_ids)})"
    )
    return {
        "value_rows": int(value_rows[0][0]) if value_rows else None,
        "value_rows_expected": len(rows),
        "quality_rows": int(quality_rows[0][0]) if quality_rows else None,
        "quality_rows_expected": sum(len(row["qualities"]) for row in rows),
        "sample_columns": sample_columns,
        "sample_values": [list(row) for row in sample],
        "sample_values_expected": [first["columns"][name] for name in sample_columns],
        "sample_qualities": [list(row) for row in quality_sample],
        "sample_qualities_expected": {
            var_id: first["qualities"][str(var_id)] for var_id in sample_var_ids
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("offline", "live"), default="offline")
    parser.add_argument("--frames", type=int, default=400)
    parser.add_argument("--points", type=int, default=509)
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--td-host", default="127.0.0.1")
    parser.add_argument("--td-port", type=int, default=6041)
    # A fresh database per run keeps the row-count verification exact; the run
    # drops it again on the way out.
    parser.add_argument("--td-database", default=f"dw_writer_bench_{os.urandom(3).hex()}")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    report = asyncio.run(run(args))
    payload = json.dumps(report, ensure_ascii=False, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(payload + "\n", encoding="utf-8")
    print(payload)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
