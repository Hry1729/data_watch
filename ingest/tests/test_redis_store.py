from __future__ import annotations

import asyncio
import json
from uuid import uuid4

import pytest

from ingest.app.config import IngestSettings
from ingest.app.redis_store import CONTROL_INVALIDATION_CHANNEL, IngestRedisStore
from ingest.app.schemas import UploadItem


class FakeRedis:
    def __init__(self) -> None:
        self.eval_call = None

    async def eval(self, script, numkeys, *args):
        self.eval_call = (script, numkeys, args)
        return json.dumps(["ack"])

    async def aclose(self) -> None:
        return None


class FakePubSub:
    def __init__(self) -> None:
        self.subscribed = None
        self.closed = False

    async def subscribe(self, channel: str) -> None:
        self.subscribed = channel

    async def get_message(self, **kwargs):
        await asyncio.sleep(0)
        return {"data": '{"scope":"assignments"}'}

    async def aclose(self) -> None:
        self.closed = True


class FakePubSubRedis(FakeRedis):
    def __init__(self) -> None:
        super().__init__()
        self.subscription = FakePubSub()

    def pubsub(self) -> FakePubSub:
        return self.subscription

def _settings() -> IngestSettings:
    return IngestSettings(
        host="127.0.0.1",
        port=8010,
        redis_url="redis://127.0.0.1:6379/0",
        mysql_host="127.0.0.1",
        mysql_port=3306,
        mysql_user="root",
        mysql_password="",
        mysql_database="device_watch",
        stream_shard_count=4,
        stream_maxlen=50_000,
        signature_max_skew_seconds=300,
        nonce_ttl_seconds=600,
        max_batch_items=500,
    )


@pytest.mark.asyncio
async def test_control_invalidation_subscription_dispatches_payload() -> None:
    client = FakePubSubRedis()
    store = IngestRedisStore(client, _settings())
    received = []
    delivered = asyncio.Event()

    def callback(payload: dict) -> None:
        received.append(payload)
        delivered.set()

    task = asyncio.create_task(store.listen_control_invalidations(callback))
    await asyncio.wait_for(delivered.wait(), timeout=1.0)
    task.cancel()
    with pytest.raises(asyncio.CancelledError):
        await task

    assert received[0] == {"scope": "assignments"}
    assert client.subscription.subscribed == CONTROL_INVALIDATION_CHANNEL
    assert client.subscription.closed is True

@pytest.mark.asyncio
async def test_process_item_publishes_change_on_atomic_ack_path() -> None:
    client = FakeRedis()
    store = IngestRedisStore(client, _settings())
    session_id = str(uuid4())
    item = UploadItem(
        batch_id=str(uuid4()),
        upload_session_id=session_id,
        local_seq=1,
        assignment_epoch=2,
        device_code="QC100",
        device_type="qc",
        group_code="g001",
        scan_rate=100,
        ts=1_721_800_000_000,
        config_version=3,
        values={"1": "12.5"},
    )

    result = await store.process_item(str(uuid4()), item, "accept")

    assert result.status == "ack"
    assert client.eval_call is not None
    script, numkeys, args = client.eval_call
    assert numkeys == 7
    assert args[2] == "rt:change"
    assert args[5].startswith("ingest:nonce:")
    assert args[6] == "ingest:stream_trim_item_counts"
    payload = json.loads(args[-3])
    assert args[-2] == "5000"
    assert args[-1] == "600"
    assert json.loads(payload[0]["envelope"]["values_json"]) == {"1": "12.5"}
    assert "values" not in payload[0]["envelope"]
    assert payload[0]["change"] == {
        "device_code": "QC100",
        "group_code": "g001",
    }
    assert '"SET", KEYS[#KEYS - 1], "1", "NX", "EX", tonumber(ARGV[5])' in script
    assert 'redis.call("PUBLISH", KEYS[3], cjson.encode(changes))' in script
    assert '"HINCRBY", KEYS[#KEYS], touched_stream, item_count' in script
    assert "trim_count >= trim_every_items" in script
    assert 'redis.call("XTRIM", touched_stream, "MAXLEN", "~", stream_maxlen)' in script
    assert '"MAXLEN", "~", stream_maxlen, "*"' not in script