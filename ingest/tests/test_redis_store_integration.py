from __future__ import annotations

import json
import os
from uuid import uuid4

import pytest
import redis.asyncio as redis

from ingest.app import redis_store as redis_store_module
from ingest.app.config import IngestSettings
from ingest.app.redis_store import IngestRedisStore, NonceReplayError
from ingest.app.schemas import UploadItem


pytestmark = pytest.mark.skipif(
    not os.getenv("INGEST_TEST_REDIS_URL"),
    reason="set INGEST_TEST_REDIS_URL to run Redis integration tests",
)


def settings() -> IngestSettings:
    return IngestSettings(
        host="127.0.0.1",
        port=8010,
        redis_url=os.environ["INGEST_TEST_REDIS_URL"],
        mysql_host="127.0.0.1",
        mysql_port=3306,
        mysql_user="root",
        mysql_password="",
        mysql_database="device_watch",
        stream_shard_count=4,
        stream_maxlen=50000,
        signature_max_skew_seconds=300,
        nonce_ttl_seconds=600,
        max_batch_items=500,
    )


def item(
    seq: int,
    session_id: str,
    *,
    ts: int = 1000,
    device_code: str = "QC100",
    group_code: str = "g001",
    value: str = "12.5",
) -> UploadItem:
    return UploadItem(
        batch_id=str(uuid4()),
        upload_session_id=session_id,
        local_seq=seq,
        assignment_epoch=5,
        device_code=device_code,
        device_type="qc",
        group_code=group_code,
        scan_rate=100,
        ts=ts,
        config_version=12,
        values={"1": value},
        contract_version=2,
        qualities={"1": 192},
    )


@pytest.mark.asyncio
async def test_real_lua_sequence_stream_latest_and_session_fence() -> None:
    client = redis.from_url(os.environ["INGEST_TEST_REDIS_URL"], decode_responses=True)
    await client.flushdb()
    store = IngestRedisStore(client, settings())
    pubsub = client.pubsub()
    await pubsub.subscribe("rt:change")
    await pubsub.get_message(timeout=1.0)
    node_uuid = str(uuid4())
    old_session = str(uuid4())
    new_session = str(uuid4())

    first = await store.process_item(node_uuid, item(1, old_session), "accept")
    change = await pubsub.get_message(ignore_subscribe_messages=True, timeout=1.0)
    assert change is not None
    assert json.loads(change["data"]) == [
        {"device_code": "QC100", "group_code": "g001"}
    ]
    duplicate = await store.process_item(node_uuid, item(1, old_session), "accept")
    gap = await store.process_item(node_uuid, item(3, old_session), "accept")

    assert (first.status, duplicate.status, gap.status) == (
        "ack",
        "duplicate",
        "seq_reject",
    )
    stream_keys = await client.keys("rt:stream:*")
    assert len(stream_keys) == 1
    entries = await client.xrange(stream_keys[0])
    assert entries[0][1]["device_code"] == "QC100"
    assert entries[0][1]["contract_version"] == "2"
    assert json.loads(entries[0][1]["values"]) == {"1": "12.5"}

    latest_key = "rt:latest:QC100:g001"
    await client.hset(latest_key, "1", '{"v":"legacy","t":2000,"q":0}')
    accepted = await store.process_item(
        node_uuid, item(2, old_session, ts=2000), "accept"
    )
    latest = json.loads(await client.hget(latest_key, "1"))
    assert accepted.status == "ack"
    assert latest["e"] == 5
    assert latest["v"] == "12.5"

    assert await store.switch_session(node_uuid, old_session, new_session) == "switched"
    obsolete = await store.process_item(node_uuid, item(3, old_session), "accept")
    assert obsolete.status == "session_obsolete"
    assert await store.switch_session(node_uuid, old_session, new_session) == "already_switched"

    await pubsub.aclose()
    await store.close()


@pytest.mark.asyncio
async def test_real_lua_batches_latest_updates_and_keeps_newest_value() -> None:
    client = redis.from_url(os.environ["INGEST_TEST_REDIS_URL"], decode_responses=True)
    await client.flushdb()
    store = IngestRedisStore(client, settings())
    node_uuid = str(uuid4())
    session_id = str(uuid4())

    results = await store.process_items(
        node_uuid,
        [
            (item(1, session_id, ts=1000, value="first"), "accept"),
            (item(2, session_id, ts=1100, value="second"), "accept"),
        ],
        nonce="batched-latest",
    )

    latest = json.loads(await client.hget("rt:latest:QC100:g001", "1"))
    assert [result.status for result in results] == ["ack", "ack"]
    assert latest["v"] == "second"
    assert latest["t"] == 1100

    await store.close()

@pytest.mark.asyncio
async def test_real_lua_rejected_consumed_advances_watermark() -> None:
    client = redis.from_url(os.environ["INGEST_TEST_REDIS_URL"], decode_responses=True)
    await client.flushdb()
    store = IngestRedisStore(client, settings())
    node_uuid = str(uuid4())
    session_id = str(uuid4())

    rejected = await store.process_item(
        node_uuid, item(1, session_id), "consume_reject"
    )
    accepted = await store.process_item(node_uuid, item(2, session_id), "accept")

    assert rejected.status == "rejected_consumed"
    assert accepted.status == "ack"
    assert await client.get(f"hw:{node_uuid}:{session_id}") == "2"
    assert await client.xlen((await client.keys("rt:stream:*"))[0]) == 1

    await store.close()

@pytest.mark.asyncio
async def test_real_lua_tracks_trim_counts_per_stream(monkeypatch) -> None:
    monkeypatch.setattr(redis_store_module, "STREAM_TRIM_EVERY_ITEMS", 2)
    client = redis.from_url(os.environ["INGEST_TEST_REDIS_URL"], decode_responses=True)
    await client.flushdb()
    store = IngestRedisStore(client, settings())
    node_uuid = str(uuid4())
    session_id = str(uuid4())

    await store.process_item(
        node_uuid,
        item(1, session_id, device_code="QC100", group_code="g001"),
        "accept",
    )
    await store.process_item(
        node_uuid,
        item(2, session_id, device_code="QC200", group_code="g002"),
        "accept",
    )
    await store.process_item(
        node_uuid,
        item(3, session_id, device_code="QC100", group_code="g001"),
        "accept",
    )

    counts = await client.hgetall("ingest:stream_trim_item_counts")
    stream_keys = await client.keys("rt:stream:*")
    stream_by_group = {
        (await client.xrange(key))[0][1]["group_code"]: key
        for key in stream_keys
    }
    assert counts[stream_by_group["g001"]] == "0"
    assert counts[stream_by_group["g002"]] == "1"

    await store.close()


@pytest.mark.asyncio
async def test_real_lua_rejects_replayed_nonce_atomically() -> None:
    client = redis.from_url(os.environ["INGEST_TEST_REDIS_URL"], decode_responses=True)
    await client.flushdb()
    store = IngestRedisStore(client, settings())
    node_uuid = str(uuid4())
    session_id = str(uuid4())
    upload_item = item(1, session_id)

    accepted = await store.process_item(
        node_uuid, upload_item, "accept", nonce="same-nonce"
    )
    with pytest.raises(NonceReplayError, match="nonce already used"):
        await store.process_item(
            node_uuid, upload_item, "accept", nonce="same-nonce"
        )

    assert accepted.status == "ack"
    stream_keys = await client.keys("rt:stream:*")
    assert len(stream_keys) == 1
    assert await client.xlen(stream_keys[0]) == 1

    await store.close()
