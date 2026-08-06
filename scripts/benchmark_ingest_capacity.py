from __future__ import annotations

import argparse
import asyncio
import base64
import json
import math
import sys
import time
from collections import Counter
from concurrent.futures import ProcessPoolExecutor
from dataclasses import dataclass, field
from pathlib import Path
from urllib.parse import urlsplit
from uuid import UUID, uuid4, uuid5

from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey

REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from ingest.app.security import canonical_json, signing_message


BENCHMARK_NAMESPACE = UUID("b9c27ab6-1bb5-4f46-8f49-986115ef91c6")


@dataclass
class Metrics:
    latencies_ms: list[float] = field(default_factory=list)
    statuses: Counter[str] = field(default_factory=Counter)
    errors: Counter[str] = field(default_factory=Counter)
    error_samples: list[str] = field(default_factory=list)

    def record_error(self, category: str, detail: str) -> None:
        self.errors[category] += 1
        if len(self.error_samples) < 10:
            self.error_samples.append(detail[:500])

    def merge(self, other: "Metrics") -> None:
        self.latencies_ms.extend(other.latencies_ms)
        self.statuses.update(other.statuses)
        self.errors.update(other.errors)
        remaining = 10 - len(self.error_samples)
        if remaining > 0:
            self.error_samples.extend(other.error_samples[:remaining])

class RawHttpConnection:
    def __init__(self, base_url: str, timeout_seconds: float) -> None:
        parsed = urlsplit(base_url)
        if parsed.scheme != "http" or not parsed.hostname:
            raise ValueError("benchmark URL must use http://host[:port]")
        self._host = parsed.hostname
        self._port = parsed.port or 80
        self._timeout_seconds = timeout_seconds
        self._reader: asyncio.StreamReader | None = None
        self._writer: asyncio.StreamWriter | None = None

    async def post(self, body: bytes, headers: dict[str, str]) -> tuple[int, bytes]:
        try:
            return await asyncio.wait_for(
                self._post(body, headers), timeout=self._timeout_seconds
            )
        except Exception:
            await self.close()
            raise

    async def _post(
        self, body: bytes, headers: dict[str, str]
    ) -> tuple[int, bytes]:
        if self._writer is None or self._writer.is_closing():
            self._reader, self._writer = await asyncio.open_connection(
                self._host, self._port
            )
        request_headers = {
            "Host": f"{self._host}:{self._port}",
            "Content-Type": "application/json",
            "Content-Length": str(len(body)),
            "Connection": "keep-alive",
            **headers,
        }
        head = "POST /ingest HTTP/1.1\r\n" + "".join(
            f"{name}: {value}\r\n" for name, value in request_headers.items()
        )
        assert self._writer is not None
        assert self._reader is not None
        self._writer.write(head.encode("ascii") + b"\r\n" + body)
        await self._writer.drain()

        status_line = await self._reader.readline()
        if not status_line:
            raise ConnectionError("server closed connection before response")
        parts = status_line.decode("ascii", errors="replace").split()
        if len(parts) < 2:
            raise ValueError(f"invalid HTTP status line: {status_line!r}")
        status_code = int(parts[1])
        response_headers: dict[str, str] = {}
        while True:
            line = await self._reader.readline()
            if line in {b"\r\n", b"\n", b""}:
                break
            name, value = line.decode("latin-1").split(":", 1)
            response_headers[name.strip().lower()] = value.strip()
        if response_headers.get("transfer-encoding", "").lower() == "chunked":
            response_body = await self._read_chunked_body()
        else:
            content_length = int(response_headers.get("content-length", "0"))
            response_body = await self._reader.readexactly(content_length)
        if response_headers.get("connection", "").lower() == "close":
            await self.close()
        return status_code, response_body

    async def _read_chunked_body(self) -> bytes:
        assert self._reader is not None
        chunks = []
        while True:
            size_line = await self._reader.readline()
            size = int(size_line.split(b";", 1)[0].strip(), 16)
            if size == 0:
                while await self._reader.readline() not in {b"\r\n", b"\n", b""}:
                    pass
                return b"".join(chunks)
            chunks.append(await self._reader.readexactly(size))
            if await self._reader.readexactly(2) != b"\r\n":
                raise ValueError("invalid HTTP chunk terminator")

    async def close(self) -> None:
        if self._writer is not None:
            self._writer.close()
            try:
                await self._writer.wait_closed()
            except Exception:
                pass
        self._reader = None
        self._writer = None

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Load-test the signed Data Watch Ingest HTTP path."
    )
    parser.add_argument("--url", default="http://127.0.0.1:8010")
    parser.add_argument("--nodes", type=int, default=100)
    parser.add_argument("--node-offset", type=int, default=0)
    parser.add_argument("--client-workers", type=int, default=1)
    parser.add_argument("--items-per-batch", type=int, default=10)
    parser.add_argument("--period-ms", type=float, default=100.0)
    parser.add_argument("--duration-seconds", type=float, default=60.0)
    parser.add_argument("--warmup-seconds", type=float, default=5.0)
    parser.add_argument("--timeout-seconds", type=float, default=10.0)
    parser.add_argument("--private-key-hex", default="01" * 32)
    parser.add_argument(
        "--phase-mode",
        choices=("staggered", "synchronized"),
        default="staggered",
    )
    parser.add_argument("--min-qps", type=float)
    parser.add_argument("--max-p99-ms", type=float)
    parser.add_argument("--max-error-rate", type=float, default=0.001)
    return parser.parse_args()


def percentile(values: list[float], fraction: float) -> float:
    if not values:
        return math.nan
    ordered = sorted(values)
    index = max(0, math.ceil(len(ordered) * fraction) - 1)
    return ordered[index]


def node_uuid(index: int) -> str:
    return f"00000000-0000-0000-0000-{index + 1:012x}"


def node_phase_offset(index: int, node_count: int, period_seconds: float) -> float:
    return period_seconds * index / node_count


def build_payload(
    *,
    node_index: int,
    session_id: str,
    first_seq: int,
    item_count: int,
    scan_rate: int,
) -> dict:
    sample_ts = int(time.time() * 1000)
    items = []
    for item_index in range(item_count):
        device_index = node_index * item_count + item_index
        items.append(
            {
                "batch_id": str(uuid4()),
                "upload_session_id": session_id,
                "local_seq": first_seq + item_index,
                "assignment_epoch": 1,
                "device_code": f"N{node_index:03d}D{item_index:02d}",
                "device_type": "bench",
                "group_code": "g001",
                "scan_rate": scan_rate,
                "ts": sample_ts,
                "config_version": 1,
                "contract_version": 2,
                "values": {str(device_index + 1): "12.5"},
                "qualities": {str(device_index + 1): 192},
            }
        )
    return {
        "node_uuid": node_uuid(node_index),
        "request_id": str(uuid4()),
        "items": items,
    }


def signature_headers(private_key: Ed25519PrivateKey, body: bytes) -> dict[str, str]:
    timestamp = str(int(time.time()))
    nonce = str(uuid4())
    message = signing_message(
        "POST", "/ingest", timestamp, nonce, body
    )
    return {
        "x-datawatch-timestamp": timestamp,
        "x-datawatch-nonce": nonce,
        "x-datawatch-signature": base64.b64encode(
            private_key.sign(message)
        ).decode("ascii"),
    }


async def run_node(
    base_url: str,
    private_key: Ed25519PrivateKey,
    metrics: Metrics,
    *,
    node_index: int,
    item_count: int,
    period_seconds: float,
    started_at: float,
    measure_started_at: float,
    ends_at: float,
    timeout_seconds: float,
) -> None:
    connection = RawHttpConnection(base_url, timeout_seconds)
    session_id = str(uuid5(BENCHMARK_NAMESPACE, f"node-{node_index}"))
    next_seq = 1
    deadline = started_at
    scan_rate = max(1, round(period_seconds * 1000))
    loop = asyncio.get_running_loop()
    try:
        while loop.time() < ends_at:
            await asyncio.sleep(max(0.0, deadline - loop.time()))
            if loop.time() >= ends_at:
                break
            payload = build_payload(
                node_index=node_index,
                session_id=session_id,
                first_seq=next_seq,
                item_count=item_count,
                scan_rate=scan_rate,
            )
            measured = loop.time() >= measure_started_at
            try:
                body = canonical_json(payload)
                headers = signature_headers(private_key, body)
                request_started = loop.time()
                status_code, response_body = await connection.post(body, headers)
                latency_ms = (loop.time() - request_started) * 1000
                if measured:
                    metrics.latencies_ms.append(latency_ms)
                if status_code != 200:
                    if measured:
                        metrics.record_error(
                            f"http_{status_code}",
                            response_body.decode("utf-8", errors="replace"),
                        )
                else:
                    results = json.loads(response_body).get("results", [])
                    statuses = [str(result.get("status")) for result in results]
                    accepted = all(
                        status in {"ack", "duplicate", "stale_config_warning"}
                        for status in statuses
                    )
                    if measured:
                        metrics.statuses.update(statuses)
                        if not accepted or len(statuses) != item_count:
                            metrics.record_error(
                                "unexpected_result", json.dumps(results)[:500]
                            )
                    if accepted and len(statuses) == item_count:
                        next_seq += item_count
            except Exception as exc:
                if measured:
                    metrics.record_error(type(exc).__name__, repr(exc))
            deadline += period_seconds
    finally:
        await connection.close()

async def run_shard(
    args: argparse.Namespace,
    node_start: int,
    node_count: int,
) -> Metrics:
    private_key = Ed25519PrivateKey.from_private_bytes(
        bytes.fromhex(args.private_key_hex)
    )
    metrics = Metrics()
    loop = asyncio.get_running_loop()
    period_seconds = args.period_ms / 1000
    started_at = loop.time() + 1.0
    measure_started_at = started_at + args.warmup_seconds
    ends_at = measure_started_at + args.duration_seconds
    await asyncio.gather(
        *(
            run_node(
                args.url.rstrip("/"),
                private_key,
                metrics,
                node_index=args.node_offset + index,
                item_count=args.items_per_batch,
                period_seconds=period_seconds,
                started_at=(
                    started_at + node_phase_offset(index, args.nodes, period_seconds)
                    if args.phase_mode == "staggered"
                    else started_at
                ),
                measure_started_at=measure_started_at,
                ends_at=ends_at,
                timeout_seconds=args.timeout_seconds,
            )
            for index in range(node_start, node_start + node_count)
        )
    )
    return metrics


def _run_shard_sync(
    args: argparse.Namespace,
    node_start: int,
    node_count: int,
) -> Metrics:
    return asyncio.run(run_shard(args, node_start, node_count))


def run(args: argparse.Namespace) -> Metrics:
    if args.nodes <= 0 or args.items_per_batch <= 0:
        raise ValueError("nodes and items per batch must be positive")
    if args.period_ms <= 0 or args.duration_seconds <= 0:
        raise ValueError("period and duration must be positive")
    if args.client_workers <= 0 or args.client_workers > args.nodes:
        raise ValueError("client_workers must be between 1 and nodes")

    base_count, remainder = divmod(args.nodes, args.client_workers)
    partitions = []
    node_start = 0
    for worker_index in range(args.client_workers):
        node_count = base_count + (1 if worker_index < remainder else 0)
        partitions.append((node_start, node_count))
        node_start += node_count

    if len(partitions) == 1:
        return _run_shard_sync(args, *partitions[0])

    merged = Metrics()
    with ProcessPoolExecutor(max_workers=len(partitions)) as executor:
        futures = [
            executor.submit(_run_shard_sync, args, start, count)
            for start, count in partitions
        ]
        for future in futures:
            merged.merge(future.result())
    return merged


def build_report(args: argparse.Namespace, metrics: Metrics) -> dict:
    request_count = len(metrics.latencies_ms)
    error_count = sum(metrics.errors.values())
    return {
        "nodes": args.nodes,
        "client_workers": args.client_workers,
        "items_per_batch": args.items_per_batch,
        "period_ms": args.period_ms,
        "duration_seconds": args.duration_seconds,
        "phase_mode": args.phase_mode,
        "latency_scope": "http_round_trip",
        "expected_qps": args.nodes * 1000 / args.period_ms,
        "requests": request_count,
        "qps": request_count / args.duration_seconds,
        "error_count": error_count,
        "error_rate": error_count / request_count if request_count else 1.0,
        "latency_ms": {
            "p50": percentile(metrics.latencies_ms, 0.50),
            "p95": percentile(metrics.latencies_ms, 0.95),
            "p99": percentile(metrics.latencies_ms, 0.99),
            "max": max(metrics.latencies_ms, default=math.nan),
        },
        "statuses": dict(metrics.statuses),
        "errors": dict(metrics.errors),
        "error_samples": metrics.error_samples,
    }


def main() -> int:
    args = parse_args()
    report = build_report(args, run(args))
    print(json.dumps(report, ensure_ascii=False, indent=2))
    failed = report["error_rate"] > args.max_error_rate
    if args.min_qps is not None and report["qps"] < args.min_qps:
        failed = True
    if (
        args.max_p99_ms is not None
        and report["latency_ms"]["p99"] > args.max_p99_ms
    ):
        failed = True
    return 1 if failed else 0

if __name__ == "__main__":
    raise SystemExit(main())
