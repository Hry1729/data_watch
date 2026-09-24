"""Reproducible offline SQL packing check; never connects to TDengine or Redis."""
from __future__ import annotations

import argparse
import asyncio
import json
from pathlib import Path
import sys
import time
from collections import Counter

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "writer"))
import tdengine_writer
from tdengine_writer import LANDED, TDengineWriter


def build_rows(devices: int, points: int, frames: int) -> list[dict]:
    if min(devices, points, frames) < 1:
        raise ValueError("devices, points and frames must be positive")
    return [
        {
            "child_table": f"qc{device}", "stable": "sdqc",
            "device_code": f"qc{device}", "ts": 1750000000000 + frame * 100,
            "columns": {f"p{point}": float(point + frame) for point in range(points)},
            "qualities": {str(point + 1): 0 if point % 10 else 192 for point in range(points)},
        }
        for device in range(devices) for frame in range(frames)
    ]


async def benchmark(devices: int, points: int, frames: int) -> dict:
    rows = build_rows(devices, points, frames)
    sizes: list[int] = []

    class OfflineConnection:
        def execute(self, sql: str) -> int:
            if not sql.startswith("INSERT INTO "):
                raise AssertionError("offline benchmark unexpectedly attempted schema work")
            sizes.append(len(sql.encode("utf-8")))
            return 1

    writer = TDengineWriter()
    writer._database = "offline_benchmark"
    writer._conn = OfflineConnection()
    writer._col_cache = {"sdqc": {f"p{point}" for point in range(points)}}
    writer._quality_stable_ready = {"sqqc"}
    started = time.perf_counter()
    results = await writer.write_multi_table(rows)
    elapsed = time.perf_counter() - started
    coverage = Counter(index for indices, _, _ in results for index in indices)
    passed = (
        coverage == Counter(range(len(rows)))
        and all(status == LANDED for _, status, _ in results)
        and bool(sizes)
        and max(sizes) <= tdengine_writer.MAX_SQL_BYTES
    )
    return {
        "mode": "offline_mock_execution_not_database_throughput",
        "devices": devices, "points_per_device": points, "frames_per_device": frames,
        "sample_period_ms": 100, "logical_frames": len(rows),
        "expected_value_rows": len(rows), "expected_quality_rows": len(rows) * points,
        "sql_chunks": len(sizes), "sql_bytes": sum(sizes),
        "max_sql_bytes": max(sizes, default=0), "sql_byte_budget": tdengine_writer.MAX_SQL_BYTES,
        "packing_and_mock_execution_ms": round(elapsed * 1000, 3),
        "unique_complete_frame_results": sum(1 for count in coverage.values() if count == 1),
        "passed": passed,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--devices", type=int, default=100)
    parser.add_argument("--points", type=int, default=1000)
    parser.add_argument("--frames", type=int, default=2)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if min(args.devices, args.points, args.frames) < 1:
        parser.error("devices, points and frames must be positive")
    result = asyncio.run(benchmark(args.devices, args.points, args.frames))
    payload = json.dumps(result, ensure_ascii=False, indent=2)
    if args.output:
        args.output.write_text(payload + "\n", encoding="utf-8")
    print(payload)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
