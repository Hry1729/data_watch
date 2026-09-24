"""Bounded, read-only inspection of collector receipt keys."""

from __future__ import annotations

import argparse
import json
import os
import sys
from typing import Any


def inspect_receipts(
    client: Any,
    *,
    max_keys: int = 1000,
    max_scan_calls: int = 20,
    count: int = 100,
) -> dict[str, Any]:
    """Measure a bounded sample of receipt keys without changing Redis."""
    if max_keys < 1 or max_scan_calls < 1 or count < 1:
        raise ValueError("limits must be positive")

    cursor = 0
    seen: set[Any] = set()
    scan_calls = 0
    scan_complete = False
    truncated = False

    while scan_calls < max_scan_calls:
        cursor, batch = client.scan(
            cursor=cursor, match="collector:receipt:*", count=count
        )
        scan_calls += 1
        for key in batch:
            if key in seen:
                continue
            if len(seen) >= max_keys:
                truncated = True
                break
            seen.add(key)
        if truncated:
            break
        if cursor == 0:
            scan_complete = True
            break
        if len(seen) >= max_keys:
            truncated = True
            break

    if not scan_complete:
        truncated = True

    observed_bytes = 0
    measured_keys = 0
    missing_keys = 0
    persistent_keys = 0
    ttls: list[int] = []
    for key in seen:
        memory = client.memory_usage(key)
        ttl = client.ttl(key)
        if memory is not None:
            observed_bytes += int(memory)
            measured_keys += 1
        if memory is None or ttl == -2:
            missing_keys += 1
        elif ttl == -1:
            persistent_keys += 1
        elif ttl >= 0:
            ttls.append(int(ttl))

    return {
        "scanned_keys": len(seen),
        "measured_keys": measured_keys,
        "missing_keys": missing_keys,
        "observed_bytes": observed_bytes,
        "ttl_min_seconds": min(ttls) if ttls else None,
        "ttl_max_seconds": max(ttls) if ttls else None,
        "persistent_keys": persistent_keys,
        "scan_calls": scan_calls,
        "scan_complete": scan_complete,
        "truncated": truncated,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--max-keys", type=int, default=1000)
    parser.add_argument("--max-scan-calls", type=int, default=20)
    args = parser.parse_args(argv)
    url = os.environ.get("REDIS_URL")
    if not url:
        print("REDIS_URL is not set; inspection unavailable.", file=sys.stderr)
        return 2

    client = None
    try:
        import redis

        client = redis.Redis.from_url(
            url, socket_connect_timeout=5, socket_timeout=5
        )
        result = inspect_receipts(
            client, max_keys=args.max_keys, max_scan_calls=args.max_scan_calls
        )
        print(json.dumps(result, sort_keys=True))
        return 0
    except Exception:
        print("Receipt capacity inspection failed; details suppressed.", file=sys.stderr)
        return 1
    finally:
        if client is not None:
            try:
                client.close()
            except Exception:
                pass


if __name__ == "__main__":
    raise SystemExit(main())
