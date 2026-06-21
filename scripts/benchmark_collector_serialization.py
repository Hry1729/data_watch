"""Benchmark Collector Redis payload serialization candidates."""

from __future__ import annotations

import argparse
import json
import platform
import random
import statistics
import time
from typing import Callable

import msgspec


SEPARATORS = (",", ":")


def build_payloads(point_count: int, seed: int) -> tuple[dict, dict, dict]:
    rng = random.Random(seed)
    latest: dict[str, dict] = {}
    values: dict[str, str] = {}
    for index in range(point_count):
        value = f"{rng.uniform(-10000, 10000):.6f}"
        if index % 97 == 0:
            value = f"桥吊-{index}-\\\"状态\n正常"
        key = str(index + 1)
        latest[key] = {"v": value, "t": 1_750_000_000_000 + index, "q": index % 256}
        values[key] = value

    frame = {
        "device_code": "qc100",
        "group_code": "g001",
        "ts": 1_750_000_000_000,
        "scan_rate": 100,
        "values": values,
    }
    return latest, frame, values


def encode_stdlib(latest: dict, frame: dict, values: dict) -> int:
    encoded_latest = [json.dumps(item, separators=SEPARATORS) for item in latest.values()]
    encoded_frame = json.dumps(frame, separators=SEPARATORS)
    encoded_values = json.dumps(values, separators=SEPARATORS)
    return sum(len(item.encode("utf-8")) for item in encoded_latest) + len(
        encoded_frame.encode("utf-8")
    ) + len(encoded_values.encode("utf-8"))


def encode_msgspec(latest: dict, frame: dict, values: dict) -> int:
    encoded_latest = [msgspec.json.encode(item) for item in latest.values()]
    encoded_frame = msgspec.json.encode(frame)
    encoded_values = msgspec.json.encode(values)
    return sum(map(len, encoded_latest)) + len(encoded_frame) + len(encoded_values)


def measure(
    encoder: Callable[[dict, dict, dict], int],
    payloads: tuple[dict, dict, dict],
    iterations: int,
    repeats: int,
) -> tuple[float, int]:
    for _ in range(3):
        encoded_bytes = encoder(*payloads)

    samples: list[float] = []
    for _ in range(repeats):
        started = time.perf_counter()
        for _ in range(iterations):
            encoded_bytes = encoder(*payloads)
        samples.append((time.perf_counter() - started) / iterations)
    return statistics.median(samples), encoded_bytes


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--points", type=int, default=1000)
    parser.add_argument("--iterations", type=int, default=100)
    parser.add_argument("--repeats", type=int, default=5)
    parser.add_argument("--seed", type=int, default=20260619)
    args = parser.parse_args()

    payloads = build_payloads(args.points, args.seed)
    stdlib_seconds, stdlib_bytes = measure(
        encode_stdlib, payloads, args.iterations, args.repeats
    )
    msgspec_seconds, msgspec_bytes = measure(
        encode_msgspec, payloads, args.iterations, args.repeats
    )
    result = {
        "python": platform.python_version(),
        "msgspec_version": msgspec.__version__,
        "seed": args.seed,
        "points": args.points,
        "iterations": args.iterations,
        "repeats": args.repeats,
        "stdlib": {
            "cycle_ms_median": round(stdlib_seconds * 1000, 6),
            "cycles_per_second": round(1 / stdlib_seconds, 2),
            "encoded_bytes": stdlib_bytes,
        },
        "msgspec": {
            "cycle_ms_median": round(msgspec_seconds * 1000, 6),
            "cycles_per_second": round(1 / msgspec_seconds, 2),
            "encoded_bytes": msgspec_bytes,
        },
        "speedup": round(stdlib_seconds / msgspec_seconds, 3),
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
