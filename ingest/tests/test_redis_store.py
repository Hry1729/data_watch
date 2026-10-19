from __future__ import annotations

import asyncio
import json
from uuid import uuid4

import pytest

from redis.exceptions import (
    ConnectionError as RedisConnectionError,
    OutOfMemoryError,
    ResponseError,
    TimeoutError as RedisTimeoutError,
)

from ingest.app.config import IngestSettings
from ingest.app.redis_store import CONTROL_INVALIDATION_CHANNEL, IngestRedisStore
from ingest.app.schemas import UploadItem


class FakeRedis:
    def __init__(self) -> None:
        self.eval_call = None

    def pipeline(self, transaction=False):
        assert transaction is False
        return self

    async def __aenter__(self):
        return self

    async def __aexit__(self, *args):
        pass

    def eval(self, script, numkeys, *args):
        self.eval_call = (script, numkeys, args)
        return self

    def execute_command(self, *args):
        assert args == ("WAITAOF", 1, 0, 2000)
        return self

    async def execute(self, raise_on_error=True):
        if getattr(self, "raise_exc", None) is not None:
            raise self.raise_exc
        return [getattr(self, "eval_result", json.dumps(["ack"])), getattr(self, "persisted", [1, 0])]

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
    assert 'redis.call("HMGET", unpack(get_args))' in script
    assert 'redis.call("HSET", unpack(set_args))' in script
    assert 'redis.call("HGET"' not in script
    assert "local latest_chunk_size = 128" in script

@pytest.mark.asyncio
async def test_missing_aof_confirmation_never_returns_ack():
    from ingest.app.redis_store import CommitUnknownError
    client = FakeRedis()
    client.persisted = [0, 0]
    store = IngestRedisStore(client, _settings())
    entry = UploadItem(batch_id=str(uuid4()), upload_session_id=str(uuid4()), local_seq=1,
        assignment_epoch=1, device_code="x", device_type="qc", group_code="g", scan_rate=100,
        ts=1000, config_version=1, values={})
    with pytest.raises(CommitUnknownError, match="persistence"):
        await store.process_item(str(uuid4()), entry, "accept")


def _entry() -> UploadItem:
    return UploadItem(batch_id=str(uuid4()), upload_session_id=str(uuid4()), local_seq=1,
        assignment_epoch=1, device_code="x", device_type="qc", group_code="g", scan_rate=100,
        ts=1000, config_version=1, values={})


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "configure",
    [
        # AOF 关闭 / Redis < 7.2：EVAL 已执行，WAITAOF 报错。
        lambda c: setattr(c, "persisted", ResponseError(
            "ERR WAITAOF cannot be used when numlocal is set but appendonly is disabled.")),
        lambda c: setattr(c, "persisted", [0, 0]),
        lambda c: setattr(c, "raise_exc", RedisConnectionError("Connection refused")),
        lambda c: setattr(c, "raise_exc", RedisTimeoutError("Timeout reading from socket")),
        lambda c: setattr(c, "eval_result", OutOfMemoryError(
            "command not allowed when used memory > 'maxmemory'.")),
    ],
    ids=["waitaof-error", "waitaof-unconfirmed", "connection", "timeout", "oom"],
)
async def test_transient_persistence_failures_are_reported_as_transient(configure):
    """S1：暂时性持久化故障与确定性数据裁决分开，供 HTTP 层映射为 503。"""
    from ingest.app.redis_store import TransientStoreError
    client = FakeRedis()
    configure(client)
    store = IngestRedisStore(client, _settings())
    with pytest.raises(TransientStoreError):
        await store.process_item(str(uuid4()), _entry(), "accept")


@pytest.mark.asyncio
async def test_unexpected_script_error_stays_commit_unknown():
    from ingest.app.redis_store import CommitUnknownError, TransientStoreError
    client = FakeRedis()
    client.eval_result = ResponseError("ERR user_script:1: Script attempted to access nonexistent global variable")
    store = IngestRedisStore(client, _settings())
    with pytest.raises(CommitUnknownError) as info:
        await store.process_item(str(uuid4()), _entry(), "accept")
    assert not isinstance(info.value, TransientStoreError)


@pytest.mark.asyncio
async def test_realtime_only_payload_preserves_each_timestamp():
    client = FakeRedis()
    entry = UploadItem(batch_id=str(uuid4()), upload_session_id=str(uuid4()), local_seq=1,
        assignment_epoch=1, device_code="x", device_type="qc", group_code="g", scan_rate=100,
        ts=2000, config_version=1, values={}, latest_values={"fast":"hi","slow":"there"},
        latest_timestamps={"fast":2000,"slow":1000})
    await IngestRedisStore(client, _settings()).process_item(str(uuid4()), entry, "accept")
    payload = json.loads(client.eval_call[2][-3])[0]
    assert payload["latest_updates"]["slow"]["t"] == 1000
    assert payload["envelope"]["values_json"] == "{}"
    assert client.eval_call[2][-4] == "0"  # prohibit lossy trim


@pytest.mark.asyncio
async def test_quality_only_latest_is_forwarded_without_value():
    """高-3：只有质量的点在中心 latest 中更新 q/t，且不伪造 v。"""
    client = FakeRedis()
    entry = UploadItem(batch_id=str(uuid4()), upload_session_id=str(uuid4()), local_seq=1,
        assignment_epoch=3, device_code="x", device_type="qc", group_code="g", scan_rate=100,
        ts=2000, config_version=1, values={}, latest_values={"ok": "1"},
        latest_qualities={"ok": 0, "bad": 8}, latest_timestamps={"ok": 2000, "bad": 1500})
    await IngestRedisStore(client, _settings()).process_item(str(uuid4()), entry, "accept")
    payload = json.loads(client.eval_call[2][-3])[0]
    assert payload["latest_updates"]["bad"] == {"t": 1500, "q": 8, "e": 3}
    assert payload["latest_updates"]["ok"]["v"] == "1"
