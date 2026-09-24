"""Real Redis StreamConsumer recovery after a dedicated TDengine process stop.

Run only in the isolated Docker runner. The host operator stops and starts
dw-perf-td-fault-20260924 after the status reaches ready/outage_verified.
No deployment database or credentials are read.
"""
from __future__ import annotations

import asyncio
import json
import logging
from pathlib import Path
import socket
import sys
import time
from types import SimpleNamespace
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector"), str(ROOT / "writer")]

import redis
import taosws
from consumer import StreamConsumer
from data_watch_shared.stream_envelope import build_stream_fields, stream_key
from mysql_mapper import GroupMapping, MysqlMapper
from tdengine_writer import TDengineWriter

STATUS = Path("/tmp/dw-td-fault-status-20260924.json")
RESULT = Path("/tmp/dw-td-fault-result-20260924.json")
TD_HOST = "perf-td-fault"
TD_PORT = 6041
FRAMES = 100
POINTS = 1000


def write_status(phase: str, **details):
    record = {"phase": phase, "time_unix": time.time(), **details}
    temporary = STATUS.with_suffix(".tmp")
    temporary.write_text(json.dumps(record, ensure_ascii=False), encoding="utf-8")
    temporary.replace(STATUS)


def td_port_open() -> bool:
    try:
        with socket.create_connection((TD_HOST, TD_PORT), timeout=1):
            return True
    except OSError:
        return False


def redis_state(client, key, device_type):
    try:
        groups = client.xinfo_groups(key)
        writer_group = next((g for g in groups if g["name"] == "writer"), None)
    except redis.ResponseError:
        writer_group = None
    pending = int(writer_group["pending"]) if writer_group else 0
    lag = writer_group.get("lag") if writer_group else None
    dlq = sum(client.llen(k) for k in client.scan_iter(f"rt:dlq:{device_type}:*"))
    return {"stream_len": client.xlen(key), "pending": pending, "lag": lag, "dlq": dlq}


def td_counts(database, stable, quality_stable):
    connection = taosws.connect(f"taosws://root:taosdata@{TD_HOST}:{TD_PORT}")
    try:
        values = list(connection.query(f"SELECT count(*) FROM `{database}`.`{stable}`"))[0][0]
        quality = list(connection.query(f"SELECT count(*) FROM `{database}`.`{quality_stable}`"))[0][0]
        bad_quality = list(connection.query(
            f"SELECT count(*) FROM `{database}`.`{quality_stable}` WHERE quality != 192"
        ))[0][0]
        return int(values), int(quality), int(bad_quality)
    finally:
        connection.close()


async def run():
    token = uuid4().hex[:8]
    database = f"dw_td_fault_{token}"
    device_type = f"tf{token}"
    device, group = f"tf{token}d", "g"
    stable = f"sd{device_type}"
    quality_stable = f"sq{device_type}"
    key = stream_key(device, device_type, group, 4)
    config = {
        "tdengine": {"host": TD_HOST, "port": TD_PORT, "user": "root",
                     "password": "taosdata", "database": database},
        "redis": {"group_name": "writer", "consumer_name": f"td-fault-{token}"},
        "writer": {"flush_interval_ms": 100, "batch_count": 200,
                   "max_buffer_rows": 1000, "max_buffer_bytes": 32 * 1024 * 1024,
                   "pending_min_idle_ms": 1000, "pending_drain_interval_s": 1,
                   "device_types": [device_type]},
    }
    result = {
        "redis_stream": key, "td_database": database, "device_type": device_type,
        "frames": FRAMES, "points_per_frame": POINTS,
        "expected_value_rows": FRAMES, "expected_quality_rows": FRAMES * POINTS,
        "td_host": TD_HOST, "redis_host": "perf-redis",
    }
    RESULT.unlink(missing_ok=True)
    write_status("starting", **result)
    client = redis.Redis(host="perf-redis", decode_responses=True, socket_timeout=10)
    assert client.ping()
    mapper = MysqlMapper.__new__(MysqlMapper)
    mapper._conn = None
    mapper._cache = {
        f"{device}:{group}": GroupMapping(
            config_version=1, child_table=device, stable=stable,
            var_map={str(i): f"p{i}" for i in range(1, POINTS + 1)},
            var_type={str(i): "float" for i in range(1, POINTS + 1)},
        )
    }
    td = TDengineWriter()
    consumer = None
    task = None
    try:
        await td.connect(config)
        await td._ensure_stable(stable, [f"p{i}" for i in range(1, POINTS + 1)])
        consumer = StreamConsumer(config, mapper, td, client)
        task = asyncio.create_task(consumer.run_loop([key]))
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            try:
                groups = client.xinfo_groups(key)
            except redis.ResponseError:
                groups = []
            if any(g["name"] == "writer" for g in groups):
                break
            if task.done():
                task.result()
            await asyncio.sleep(.1)
        else:
            raise RuntimeError("consumer group was not created")
        write_status("ready", **result)
        deadline = time.monotonic() + 120
        while td_port_open():
            if time.monotonic() >= deadline:
                raise RuntimeError("dedicated TDengine was not stopped within 120 seconds")
            if task.done():
                task.result()
            await asyncio.sleep(.2)

        base_ts = int(time.time() * 1000)
        original = []
        for frame_no in range(FRAMES):
            timestamp = base_ts + frame_no * 100
            values = {str(i): str(i + frame_no) for i in range(1, POINTS + 1)}
            qualities = {point_id: 192 for point_id in values}
            fields = build_stream_fields(
                device_code=device, device_type=device_type, group_code=group,
                scan_rate_ms=100, ts=timestamp, config_version=1,
                values=values, qualities=qualities,
            )
            original.append(client.xadd(key, fields))
        result["first_entry_id"] = original[0]
        result["last_entry_id"] = original[-1]
        result["source_base_ts"] = base_ts
        deadline = time.monotonic() + 60
        while time.monotonic() < deadline:
            outage = redis_state(client, key, device_type)
            if outage["pending"] == FRAMES:
                break
            if task.done():
                task.result()
            await asyncio.sleep(.2)
        else:
            raise RuntimeError(f"not all frames entered PEL during outage: {outage}")
        actual_ids = [entry_id for entry_id, _ in client.xrange(key)]
        assert actual_ids == original, "original stream frames changed during outage"
        assert outage["stream_len"] == FRAMES and outage["dlq"] == 0, outage
        result["outage"] = outage
        result["original_stream_ids_retained"] = True
        write_status("outage_verified", **result)
        deadline = time.monotonic() + 120
        while not td_port_open():
            if time.monotonic() >= deadline:
                raise RuntimeError("dedicated TDengine was not restarted within 120 seconds")
            await asyncio.sleep(.2)

        deadline = time.monotonic() + 240
        while time.monotonic() < deadline:
            state = redis_state(client, key, device_type)
            if state["pending"] == 0 and state["stream_len"] == 0 and state["lag"] == 0:
                values, quality, bad_quality = await asyncio.to_thread(
                    td_counts, database, stable, quality_stable
                )
                if values == FRAMES and quality == FRAMES * POINTS:
                    result["recovered"] = state
                    result["value_rows"] = values
                    result["quality_rows"] = quality
                    result["incorrect_quality_rows"] = bad_quality
                    result["passed"] = bad_quality == 0 and state["dlq"] == 0
                    break
            if task.done():
                task.result()
            await asyncio.sleep(.5)
        else:
            raise RuntimeError(f"recovery timeout: {state}")
        write_status("complete", **result)
    except Exception as exc:
        result["passed"] = False
        result["error"] = str(exc)
        write_status("failed", **result)
        raise
    finally:
        if task is not None:
            task.cancel()
            await asyncio.gather(task, return_exceptions=True)
        await td.close()
        client.close()
        RESULT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + chr(10), encoding="utf-8")


if __name__ == "__main__":
    logging.basicConfig(level=logging.WARNING)
    asyncio.run(run())
