"""Reproduce bounded Writer checks against isolated TDengine and a CPU profile.

Run in the dw-perf-runner container with DW_TD_PASSWORD set. Each run creates a
unique child table; cost mode creates two unique test databases and retains them.
No production key, database, or service is touched by this script.
"""
from __future__ import annotations

import argparse
import asyncio
import json
import os
import sys
import time
from pathlib import Path
from types import SimpleNamespace
from uuid import uuid4

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from consumer import StreamConsumer, _prepare_payload_batch
from mysql_mapper import GroupMapping
from tdengine_writer import TDengineWriter


def config(database: str) -> dict:
    password = os.environ.get("DW_TD_PASSWORD")
    if not password:
        raise RuntimeError("set DW_TD_PASSWORD for the isolated TDengine test service")
    if not database.startswith("dw_writer_"):
        raise ValueError("test database must start with dw_writer_")
    return {
        "tdengine": {
            "host": os.environ.get("DW_TD_HOST", "perf-td"),
            "port": int(os.environ.get("DW_TD_PORT", "6041")),
            "user": os.environ.get("DW_TD_USER", "root"),
            "password": password,
            "database": database,
        },
        "writer": {"stable_names": []},
    }


def frames(child: str, count: int, points: int, quality: bool) -> list[dict]:
    columns = {f"p{point:04d}": float(point) for point in range(1, points + 1)}
    qualities = {str(point): 0 for point in range(1, points + 1)} if quality else {}
    base = int(time.time() * 1000) + 10000
    return [
        {"child_table": child, "stable": "sdqc", "device_code": child,
         "ts": base + index, "columns": columns, "qualities": qualities}
        for index in range(count)
    ]


async def write_case(database: str, child: str, count: int, points: int,
                     quality: bool, fail_second_insert: bool = False,
                     flush_disk: bool = False) -> dict:
    writer = TDengineWriter()
    await writer.connect(config(database))
    try:
        rows = frames(child, count, points, quality)
        calls = 0
        if fail_second_insert:
            original_execute = writer._execute_ex
            async def injected(sql: str):
                nonlocal calls
                if sql.lstrip().upper().startswith("INSERT"):
                    calls += 1
                    if calls == 2:
                        return False, ConnectionError("simulated connection reset")
                return await original_execute(sql)
            writer._execute_ex = injected
        started = time.perf_counter()
        results = await writer.write_multi_table(rows)
        elapsed_ms = (time.perf_counter() - started) * 1000
        if fail_second_insert and calls < 2:
            raise RuntimeError("partial mode requires a load spanning at least two INSERT statements")
        outcomes = {status: sum(len(indices) for indices, current, _ in results
                                if current == status)
                    for status in {status for _, status, _ in results}}
        value_count = (await writer._query(
            f"SELECT COUNT(*) FROM {database}.{child}"))[0][0]
        quality_count = (await writer._query(
            f"SELECT COUNT(*) FROM {database}.{child}_q"))[0][0] if quality else 0
        report = {
            "database": database, "child": child, "frames": count, "points": points,
            "quality": quality, "injected_second_insert": fail_second_insert,
            "insert_attempts": calls if fail_second_insert else None,
            "covered": sorted(index for indices, _, _ in results for index in indices)
                       == list(range(count)),
            "outcomes": outcomes, "value_count": value_count,
            "quality_count": quality_count, "elapsed_ms": round(elapsed_ms, 3),
            "build_ms": round(writer._last_build_ms, 3),
            "execute_ms": round(writer._last_execute_ms, 3),
            "sql_bytes": writer._last_sql_bytes,
            "sql_statement_bytes": writer._last_sql_statement_bytes,
            "sql_value_rows_attempted": writer._last_sql_value_rows_attempted,
            "sql_quality_rows_attempted": writer._last_sql_quality_rows_attempted,
            "sql_value_rows_succeeded": writer._last_sql_value_rows_succeeded,
            "sql_quality_rows_succeeded": writer._last_sql_quality_rows_succeeded,
        }
        if flush_disk:
            ok, error = await writer._execute_ex(f"FLUSH DATABASE {database}")
            if not ok:
                raise RuntimeError(f"test database flush failed: {error}")
            disk = []
            for _ in range(20):
                disk = await writer._query(
                    "SELECT * FROM INFORMATION_SCHEMA.INS_DISK_USAGE "
                    f"WHERE db_name='{database}'"
                )
                if sum(row[3] for row in disk) > 0:
                    break
                await asyncio.sleep(0.2)
            report["disk_kib"] = {
                "wal": sum(row[2] for row in disk),
                "data": sum(row[3] + row[4] + row[5] for row in disk),
                "cache_rdb": sum(row[6] for row in disk),
                "table_meta": sum(row[7] for row in disk),
                "raw_data_estimate": sum(row[9] for row in disk),
            }
        return report
    finally:
        await writer.close()


class _Mapper:
    def __init__(self, mapping):
        self.mapping = mapping
    def get_cached(self, *_args):
        return self.mapping


async def profile_case(points: int, count: int, batched: bool) -> dict:
    mapping = GroupMapping(
        config_version=1, child_table="qcprofile", stable="sdqc",
        var_map={str(i): f"p{i:04d}" for i in range(1, points + 1)},
    )
    values = json.dumps({str(i): str(float(i)) for i in range(1, points + 1)})
    qualities = json.dumps({str(i): 0 for i in range(1, points + 1)})
    entry = {"device_code": "qcprofile", "device_type": "qc", "group_code": "g001",
             "ts": str(int(time.time() * 1000)), "config_version": "1",
             "values": values, "qualities": qualities, "contract_version": "2"}
    consumer = StreamConsumer(
        {"writer": {"flush_interval_ms": 500, "batch_count": count,
                    "max_buffer_rows": count + 1, "max_buffer_bytes": 32 * 1024 * 1024,
                    "pending_min_idle_ms": 60000},
         "redis": {"group_name": "test", "consumer_name": "test"}},
        _Mapper(mapping), None, SimpleNamespace(),
    )
    stopped = False
    lags = []
    async def ticker():
        nonlocal stopped
        due = time.perf_counter() + 0.002
        while not stopped:
            await asyncio.sleep(max(0, due - time.perf_counter()))
            lags.append(max(0.0, (time.perf_counter() - due) * 1000))
            due = time.perf_counter() + 0.002
    task = asyncio.create_task(ticker())
    await asyncio.sleep(0.01)
    started = time.perf_counter()
    prepared = (await consumer._run_blocking(
        _prepare_payload_batch, [entry] * count, 32 * 1024 * 1024
    )) if batched else [None] * count
    for index, item in enumerate(prepared):
        await consumer._handle_entry("test", f"{index + 1}-0", entry, 1, prepared=item)
        if batched and index % 8 == 7:
            await asyncio.sleep(0)
    elapsed_ms = (time.perf_counter() - started) * 1000
    await asyncio.sleep(0.01)
    stopped = True
    await task
    return {"mode": "batched_json" if batched else "synchronous",
            "frames": len(consumer._buffer), "elapsed_ms": round(elapsed_ms, 3),
            "max_event_loop_lag_ms": round(max(lags, default=0), 3)}


async def run(args) -> dict:
    if args.mode == "profile":
        return {"runtime": sys.version, "gil_enabled": getattr(sys, "_is_gil_enabled", lambda: True)(),
                "synchronous": await profile_case(args.points, args.frames, False),
                "batched_json": await profile_case(args.points, args.frames, True)}
    token = uuid4().hex[:8]
    if args.mode == "cost":
        value = await write_case(f"dw_writer_v{token}", f"qc{token}", args.frames,
                                 args.points, False, flush_disk=True)
        quality = await write_case(f"dw_writer_q{token}", f"qc{token}", args.frames,
                                   args.points, True, flush_disk=True)
        return {"value_only": value, "value_and_quality": quality}
    database = args.database
    return await write_case(database, f"qc{token}", args.frames, args.points,
                            True, fail_second_insert=args.mode == "partial")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("wide", "partial", "cost", "profile"))
    parser.add_argument("--database", default="dw_writer_validation")
    parser.add_argument("--frames", type=int, default=200)
    parser.add_argument("--points", type=int, default=1000)
    args = parser.parse_args()
    if not (2 <= args.frames <= 200 and 1 <= args.points <= 1000):
        parser.error("isolated load limits: 2..200 frames and 1..1000 points")
    print(json.dumps(asyncio.run(run(args)), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
