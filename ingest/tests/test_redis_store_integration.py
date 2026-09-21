from __future__ import annotations

import json
import os
from uuid import uuid4

import pytest
import pytest_asyncio
import redis.asyncio as redis

from ingest.app import redis_store as redis_store_module
from ingest.app.config import IngestSettings
from ingest.app.redis_store import IngestRedisStore, NonceReplayError
from ingest.app.schemas import UploadItem
from ingest.tests.redis_test_guard import (
    ALLOW_REMOTE_ENV,
    MARKER_KEY,
    RedisTestGuardError,
    validate_test_redis_url,
)


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


@pytest_asyncio.fixture
async def guarded_client(monkeypatch):
    """Require a pre-provisioned disposable instance; never clear a database."""
    global TEST_DEVICE, TEST_GROUP
    TEST_DEVICE = "QC-" + uuid4().hex
    TEST_GROUP = "test-" + uuid4().hex
    url = validate_test_redis_url(
        os.environ["INGEST_TEST_REDIS_URL"],
        allow_remote=os.getenv(ALLOW_REMOTE_ENV) == "1",
    )
    client = redis.from_url(url, decode_responses=True)
    # Marker must be provisioned outside the test on a disposable instance.
    expected = os.getenv("INGEST_TEST_REDIS_MARKER")
    if not expected or await client.get(MARKER_KEY) != expected:
        await client.aclose()
        raise RedisTestGuardError("pre-provisioned disposable-instance marker missing or mismatched")
    # No FLUSHDB: dispose of the isolated instance after testing.
    try:
        yield client
    finally:
        await client.aclose()



def item(
    seq: int,
    session_id: str,
    *,
    ts: int = 1000,
    device_code: str | None = None,
    group_code: str | None = None,
    value: str = "12.5",
) -> UploadItem:
    return UploadItem(
        batch_id=str(uuid4()),
        upload_session_id=session_id,
        local_seq=seq,
        assignment_epoch=5,
        device_code=device_code or TEST_DEVICE,
        device_type="qc",
        group_code=group_code or TEST_GROUP,
        scan_rate=100,
        ts=ts,
        config_version=12,
        values={"1": value},
        contract_version=2,
        qualities={"1": 192},
    )


@pytest.mark.asyncio
async def test_real_lua_sequence_stream_latest_and_session_fence(guarded_client) -> None:
    client = guarded_client
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
        {"device_code": TEST_DEVICE, "group_code": TEST_GROUP}
    ]
    duplicate = await store.process_item(node_uuid, item(1, old_session), "accept")
    gap = await store.process_item(node_uuid, item(3, old_session), "accept")

    assert (first.status, duplicate.status, gap.status) == (
        "ack",
        "duplicate",
        "seq_reject",
    )
    stream_keys = await client.keys(f"rt:stream:qc:{TEST_GROUP}*:*")
    assert len(stream_keys) == 1
    entries = await client.xrange(stream_keys[0])
    assert entries[0][1]["device_code"] == TEST_DEVICE
    assert entries[0][1]["contract_version"] == "2"
    assert json.loads(entries[0][1]["values"]) == {"1": "12.5"}

    latest_key = f"rt:latest:{TEST_DEVICE}:{TEST_GROUP}"
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
async def test_real_lua_batches_latest_updates_and_keeps_newest_value(guarded_client) -> None:
    client = guarded_client
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

    latest = json.loads(await client.hget(f"rt:latest:{TEST_DEVICE}:{TEST_GROUP}", "1"))
    assert [result.status for result in results] == ["ack", "ack"]
    assert latest["v"] == "second"
    assert latest["t"] == 1100

    await store.close()

@pytest.mark.asyncio
async def test_real_lua_rejected_consumed_advances_watermark(guarded_client) -> None:
    client = guarded_client
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
    assert await client.xlen((await client.keys(f"rt:stream:qc:{TEST_GROUP}*:*"))[0]) == 1

    await store.close()

@pytest.mark.asyncio
async def test_real_lua_never_trims_unconsumed_streams(guarded_client, monkeypatch) -> None:
    monkeypatch.setattr(redis_store_module, "STREAM_TRIM_EVERY_ITEMS", 2)
    client = guarded_client
    store = IngestRedisStore(client, settings())
    node_uuid = str(uuid4())
    session_id = str(uuid4())

    await store.process_item(
        node_uuid,
        item(1, session_id, device_code=TEST_DEVICE, group_code=TEST_GROUP),
        "accept",
    )
    await store.process_item(
        node_uuid,
        item(2, session_id, device_code=TEST_DEVICE + "-2", group_code=TEST_GROUP + "-2"),
        "accept",
    )
    await store.process_item(
        node_uuid,
        item(3, session_id, device_code=TEST_DEVICE, group_code=TEST_GROUP),
        "accept",
    )

    counts = await client.hgetall("ingest:stream_trim_item_counts")
    stream_keys = await client.keys(f"rt:stream:qc:{TEST_GROUP}*:*")
    stream_by_group = {
        (await client.xrange(key))[0][1]["group_code"]: key
        for key in stream_keys
    }
    assert counts == {}
    assert await client.xlen(stream_by_group[TEST_GROUP]) == 2
    assert await client.xlen(stream_by_group[TEST_GROUP + "-2"]) == 1

    await store.close()


@pytest.mark.asyncio
async def test_real_lua_rejects_replayed_nonce_atomically(guarded_client) -> None:
    client = guarded_client
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
    stream_keys = await client.keys(f"rt:stream:qc:{TEST_GROUP}*:*")
    assert len(stream_keys) == 1
    assert await client.xlen(stream_keys[0]) == 1

    await store.close()
