"""Isolated, synthetic Redis-to-WebSocket validation in the Docker test network.

Run this only against dw-perf-20260924. It creates minimal target tables in the
disposable MySQL database and uses test authentication, not production login.
"""

from __future__ import annotations

import asyncio
import json
import os
import sys
import time
from collections import Counter

import uvicorn
import websockets
from sqlalchemy import text
from turboapi.websockets import WebSocket

from app.db.mysql import close_mysql, get_mysql_engine
from app.db.redis import close_redis, get_redis_client
from app.api.routes import monitor
from app.services import ws_push
from main import app


DEVICE_COUNT = 100
FIELDS_PER_DEVICE = 1000
ROUNDS = 12
GROUP = "gperf"
CODE_PREFIX = "wsperf-20260924-"
URL = "ws://127.0.0.1:18765/api/device-monitor/ws/realtime-push"


def require_isolation() -> None:
    expected = {
        "MYSQL_HOST": "perf-mysql",
        "MYSQL_DATABASE": "dw_perf_test",
        "REDIS_HOST": "perf-redis",
        "REDIS_DB": "15",
    }
    for key, value in expected.items():
        if os.getenv(key) != value:
            raise RuntimeError(f"{key} must equal {value} for this disposable test")
    if sys._is_gil_enabled():
        raise RuntimeError("backend enabled the GIL before the WebSocket test")


def device_code(index: int) -> str:
    return f"{CODE_PREFIX}{index:03d}"


def point_id(index: int) -> str:
    return str(9900000 + index)


async def seed_mysql() -> None:
    engine = get_mysql_engine()
    statements = (
        "CREATE TABLE IF NOT EXISTS comm_protocol (id BIGINT PRIMARY KEY, protocol VARCHAR(64))",
        "CREATE TABLE IF NOT EXISTS device_type (id BIGINT PRIMARY KEY, type_code VARCHAR(64))",
        "CREATE TABLE IF NOT EXISTS device (id BIGINT PRIMARY KEY, device_code VARCHAR(128) UNIQUE, device_name VARCHAR(128), protocol_id BIGINT NULL, type_id BIGINT NULL, status INT NOT NULL DEFAULT 1)",
        "CREATE TABLE IF NOT EXISTS device_point_group (id BIGINT PRIMARY KEY, group_code VARCHAR(64) UNIQUE)",
        "CREATE TABLE IF NOT EXISTS device_variable (id BIGINT PRIMARY KEY, device_id BIGINT NOT NULL, point_group_id BIGINT NOT NULL, var_name VARCHAR(128), var_address VARCHAR(64), var_type VARCHAR(64), var_seq INT, group_index INT, deleted INT NOT NULL DEFAULT 0, enable INT NOT NULL DEFAULT 1)",
    )
    async with engine.begin() as conn:
        assert (await conn.scalar(text("SELECT DATABASE()"))) == "dw_perf_test"
        for statement in statements:
            await conn.execute(text(statement))
        await conn.execute(text("INSERT INTO device_point_group (id,group_code) VALUES (9700000,:group) ON DUPLICATE KEY UPDATE group_code=VALUES(group_code)"), {"group": GROUP})
        await conn.execute(
            text("INSERT INTO device (id,device_code,device_name,status) VALUES (:id,:code,:code,1) ON DUPLICATE KEY UPDATE device_code=VALUES(device_code),device_name=VALUES(device_name),status=1"),
            [{"id": 9800000 + i, "code": device_code(i)} for i in range(DEVICE_COUNT)],
        )
        await conn.execute(
            text("INSERT INTO device_variable (id,device_id,point_group_id,var_name,var_address,var_type,var_seq,group_index,deleted,enable) VALUES (:id,:device,9700000,'selected','D0','int',1,1,0,1) ON DUPLICATE KEY UPDATE device_id=VALUES(device_id),point_group_id=VALUES(point_group_id),deleted=0,enable=1"),
            [{"id": int(point_id(i)), "device": 9800000 + i} for i in range(DEVICE_COUNT)],
        )


async def seed_redis() -> None:
    redis = await get_redis_client()
    stamp = int(time.time() * 1000)
    for i in range(DEVICE_COUNT):
        key = f"rt:latest:{device_code(i)}:{GROUP}"
        mapping = {f"decoy-{n}": json.dumps({"v": 0, "q": 1, "t": stamp}) for n in range(FIELDS_PER_DEVICE - 1)}
        mapping[point_id(i)] = json.dumps({"v": 0, "q": 1, "t": stamp})
        await redis.hset(key, mapping=mapping)
    assert await redis.hlen(f"rt:latest:{device_code(0)}:{GROUP}") == FIELDS_PER_DEVICE


async def publish_value(index: int, value: int, *, notify: bool = True) -> float:
    redis = await get_redis_client()
    code = device_code(index)
    started = time.perf_counter()
    pipeline = redis.pipeline(transaction=False)
    pipeline.hset(
        f"rt:latest:{code}:{GROUP}", point_id(index),
        json.dumps({"v": value, "q": 1, "t": int(time.time() * 1000)}),
    )
    if notify:
        pipeline.publish("rt:change", json.dumps({"device_code": code, "group_code": GROUP}))
    await pipeline.execute()
    return started


async def receive_json(ws, timeout: float = 20.0) -> dict:
    return json.loads(await asyncio.wait_for(ws.recv(), timeout))


async def open_subscription(index: int, *, token: str = "perf", slow: bool = False):
    headers = {"Authorization": f"Bearer {token}"}
    if slow:
        headers["x-wsperf-slow"] = "1"
    ws = await websockets.connect(URL, additional_headers=headers, max_size=2**20)
    await ws.send(json.dumps({"action": "subscribe", "device_code": device_code(index),
                              "group_code": GROUP, "point_ids": [point_id(index)],
                              "supports_freshness": True}))
    if not slow:
        initial = await receive_json(ws, 45.0)
        assert initial["type"] == "snapshot" and set(initial["values"]) == {point_id(index)}, initial
    return ws


async def main() -> None:
    require_isolation()
    await seed_mysql()
    await seed_redis()
    revoked = False

    async def test_access(authorization):
        if authorization == "Bearer revoke" and revoked:
            return {"token_exp": 0}, False
        if authorization not in {"Bearer perf", "Bearer revoke"}:
            raise AssertionError("unexpected test credential")
        return {"token_exp": 0}, True

    async def test_td_health():
        return True

    original_send_json = WebSocket.send_json

    async def delayed_send_json(self, data, *args, **kwargs):
        if self.headers.get("x-wsperf-slow") == "1" and data.get("type") in {"snapshot", "diff", "offline"}:
            await asyncio.sleep(1.0)
        return await original_send_json(self, data, *args, **kwargs)

    monitor.evaluate_realtime_ws_access = test_access
    ws_push._check_tdengine = test_td_health
    WebSocket.send_json = delayed_send_json
    ws_push._manager = ws_push.WsPushManager(send_timeout_seconds=0.25)
    manager = ws_push.get_ws_manager()
    reads = Counter()
    read_modes = Counter()
    original_read_latest = manager._read_latest

    async def counted_read_latest(code, group, point_ids=None):
        reads[(code, group)] += 1
        read_modes["hgetall" if point_ids is None else "hmget"] += 1
        return await original_read_latest(code, group, point_ids)

    manager._read_latest = counted_read_latest
    server = uvicorn.Server(uvicorn.Config(app, host="127.0.0.1", port=18765, log_level="error", lifespan="off"))
    server_task = asyncio.create_task(server.serve())
    clients = []
    readers = []
    legacy_ws = None
    received = {}
    sent = {}
    try:
        for _ in range(100):
            if server.started:
                break
            await asyncio.sleep(0.05)
        assert server.started, "WebSocket server did not start"
        for batch in range(0, DEVICE_COUNT, 10):
            clients.extend(await asyncio.gather(*(open_subscription(i) for i in range(batch, batch + 10))))
        shared = await open_subscription(0)
        clients.append(shared)

        async def reader(ws, label):
            try:
                while True:
                    msg = await receive_json(ws, 30.0)
                    values = msg.get("values") or {}
                    for item in values.values():
                        value = item.get("v") if isinstance(item, dict) else None
                        if isinstance(value, int):
                            received[(label, value)] = time.perf_counter()
            except (websockets.ConnectionClosed, asyncio.TimeoutError):
                return

        readers = [asyncio.create_task(reader(ws, i)) for i, ws in enumerate(clients)]
        await asyncio.sleep(1.1)
        before = reads[(device_code(0), GROUP)]
        sent[(0, 9000)] = await publish_value(0, 9000)
        for _ in range(30):
            if (0, 9000) in received and (100, 9000) in received:
                break
            await asyncio.sleep(0.05)
        assert (0, 9000) in received and (100, 9000) in received, "shared subscribers missed the update"
        shared_reads = reads[(device_code(0), GROUP)] - before
        assert shared_reads == 1, f"two subscribers caused {shared_reads} Redis reads"

        # A non-selected point changes; the selected client must receive no diff.
        redis = await get_redis_client()
        await redis.hset(f"rt:latest:{device_code(0)}:{GROUP}", "decoy-0",
                         json.dumps({"v": 7, "q": 1, "t": int(time.time() * 1000)}))
        await redis.publish("rt:change", json.dumps({"device_code": device_code(0), "group_code": GROUP}))
        await asyncio.sleep(0.4)
        assert (0, 7) not in received and (100, 7) not in received, "non-selected point leaked"

        for sequence in range(1, ROUNDS + 1):
            pipeline = redis.pipeline(transaction=False)
            stamp = int(time.time() * 1000)
            for i in range(DEVICE_COUNT):
                code = device_code(i)
                sent[(i, sequence)] = time.perf_counter()
                pipeline.hset(f"rt:latest:{code}:{GROUP}", point_id(i),
                              json.dumps({"v": sequence, "q": 1, "t": stamp}))
                pipeline.publish("rt:change", json.dumps({"device_code": code, "group_code": GROUP}))
            await pipeline.execute()
            await asyncio.sleep(0.25)
        await asyncio.sleep(1.0)
        latencies = [(received[key] - started) * 1000 for key, started in sent.items()
                     if key[1] <= ROUNDS and key in received]
        delivered = len(latencies)
        assert delivered >= DEVICE_COUNT, f"only {delivered} of {DEVICE_COUNT * ROUNDS} synthetic updates arrived"

        # Reset the fallback clock with a published change, then omit notification.
        sent[(0, 8888)] = await publish_value(0, 8888)
        for _ in range(50):
            if (0, 8888) in received:
                break
            await asyncio.sleep(0.05)
        assert (0, 8888) in received
        fallback_started = await publish_value(0, 8889, notify=False)
        for _ in range(130):
            if (0, 8889) in received:
                break
            await asyncio.sleep(0.05)
        assert (0, 8889) in received, "5-second fallback failed without rt:change"
        fallback_ms = (received[(0, 8889)] - fallback_started) * 1000

        # A legacy whole-group subscription still receives complete snapshots and t-bearing diffs.
        legacy_ws = await websockets.connect(URL, additional_headers={"Authorization": "Bearer perf"}, max_size=2**20)
        await legacy_ws.send(json.dumps({"action": "subscribe", "device_code": device_code(3), "group_code": GROUP}))
        legacy_snapshot = await receive_json(legacy_ws)
        assert legacy_snapshot["type"] == "snapshot" and len(legacy_snapshot["values"]) == FIELDS_PER_DEVICE
        await publish_value(3, 7777)
        legacy_diff = await receive_json(legacy_ws)
        assert legacy_diff["type"] == "diff" and legacy_diff["values"][point_id(3)]["v"] == 7777
        assert "freshness" not in legacy_diff and "t" in legacy_diff["values"][point_id(3)]
        assert read_modes["hgetall"] >= 2
        await legacy_ws.close()
        legacy_ws = None

        revoke_ws = await open_subscription(1, token="revoke")
        revoked = True
        revoke_message = await receive_json(revoke_ws, 4.0)
        assert revoke_message.get("type") == "error" and revoke_message.get("code") == 403, revoke_message
        try:
            await receive_json(revoke_ws, 2.0)
            raise AssertionError("revoked connection remained open")
        except websockets.ConnectionClosed:
            pass
        await revoke_ws.close()

        slow_ws = await open_subscription(2, slow=True)
        try:
            await receive_json(slow_ws, 3.0)
            raise AssertionError("slow-send connection unexpectedly received a snapshot")
        except websockets.ConnectionClosed:
            pass
        await slow_ws.close()

        ordered = sorted(latencies)
        def percentile(p):
            return round(ordered[min(len(ordered) - 1, int((len(ordered) - 1) * p))], 2)
        print(json.dumps({
            "python_gil_enabled": sys._is_gil_enabled(),
            "devices": DEVICE_COUNT, "redis_latest_fields_per_device": FIELDS_PER_DEVICE,
            "configured_points_per_device": 1, "selected_points_per_connection": 1,
            "websocket_connections": len(clients), "load_rounds": ROUNDS,
            "submitted_updates": DEVICE_COUNT * ROUNDS,
            "delivered_updates": delivered,
            "delivery_ratio": round(delivered / (DEVICE_COUNT * ROUNDS), 3),
            "redis_write_and_publish_to_ws_ms": {"p50": percentile(0.5), "p95": percentile(0.95), "p99": percentile(0.99), "max": round(max(ordered), 2)},
            "fallback_without_notification_ms": round(fallback_ms, 2),
            "shared_subscriber_redis_reads": shared_reads,
            "redis_read_modes": dict(read_modes),
            "legacy_full_snapshot_fields": FIELDS_PER_DEVICE,
            "revocation": "403 then closed", "slow_send": "timeout then closed",
            "auth_and_td_health": "test doubles; not a login or TDengine benchmark",
        }, ensure_ascii=False))
    finally:
        for task in readers:
            task.cancel()
        await asyncio.gather(*readers, return_exceptions=True)
        await asyncio.gather(*(ws.close() for ws in clients), return_exceptions=True)
        if legacy_ws is not None:
            await legacy_ws.close()
        server.should_exit = True
        await server_task
        await close_redis()
        await close_mysql()


if __name__ == "__main__":
    asyncio.run(main())
