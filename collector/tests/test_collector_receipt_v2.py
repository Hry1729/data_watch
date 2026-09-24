"""Receipt protocol migration and replay invariants."""
import os
import sqlite3
import subprocess
import sys
from unittest.mock import MagicMock
from uuid import uuid4

import msgspec
import pytest
import redis

from storage.collector_outbox import CollectorRetryOutbox
from storage.durable_redis_writer import FRAME_LUA, FRAME_LUA_V2


def _sample(ts):
    return dict(device_code="D", group_code="g", device_type="qc", scan_rate_ms=100,
                ts_ms=ts, config_version=2, frame={"ts": ts, "values": {"1": "7"}},
                stream_values={"1": "7"}, stream_qualities={"1": 192},
                latest_fields={"1": {"v": "7", "t": ts, "q": 192}})


def test_v2_rows_are_hidden_from_legacy_binary_and_rollback_is_gated(tmp_path):
    q = CollectorRetryOutbox(tmp_path / "queue.db")
    q.record_failure(**_sample(1))
    assert q._conn.execute("SELECT COUNT(*) FROM collector_retry_outbox").fetchone()[0] == 0
    assert q._conn.execute("SELECT COUNT(*) FROM collector_retry_outbox_v2").fetchone()[0] == 1
    with pytest.raises(RuntimeError, match="Legacy rollback blocked"):
        q.assert_legacy_rollback_ready()
    legacy_sql = (
        "INSERT INTO collector_retry_outbox "
        "(id,device_code,group_code,scan_rate_ms,ts_ms,config_version,device_type,"
        "frame_json,stream_values_json,stream_qualities_json,latest_fields_json,created_at) "
        "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)"
    )
    legacy_row = (str(uuid4()), "D", "g", 100, 2, 2, "qc",
                  msgspec.json.encode({"ts": 2}), msgspec.json.encode({"1": "7"}),
                  None, msgspec.json.encode({}), 2)
    with pytest.raises(sqlite3.IntegrityError, match="legacy rollback blocked"):
        q._conn.execute(legacy_sql, legacy_row)
    writer = MagicMock()
    assert q.replay_pending(writer) == 1
    q.assert_legacy_rollback_ready()
    q._conn.execute(legacy_sql, legacy_row)
    assert q._conn.execute("SELECT COUNT(*) FROM collector_retry_outbox").fetchone()[0] == 1
    q.close()


def test_restart_and_backup_rollback_issue_new_session_identity(tmp_path):
    path = tmp_path / "queue.db"
    snapshot = tmp_path / "snapshot.db"
    q = CollectorRetryOutbox(path)
    q.record_failure(**_sample(1))
    first = q._conn.execute(
        "SELECT session_id, seq FROM collector_retry_outbox_v2"
    ).fetchone()
    with sqlite3.connect(snapshot) as backup:
        q._conn.backup(backup)
    q.close()

    restarted = CollectorRetryOutbox(path)
    restarted.record_failure(**_sample(2))
    second = restarted._conn.execute(
        "SELECT session_id, seq FROM collector_retry_outbox_v2 WHERE ts_ms=2"
    ).fetchone()
    assert first["session_id"] != second["session_id"]
    assert first["seq"] == second["seq"] == 1
    restarted.close()

    rolled_back = CollectorRetryOutbox(snapshot)
    rolled_back.record_failure(**_sample(3))
    third = rolled_back._conn.execute(
        "SELECT session_id, seq FROM collector_retry_outbox_v2 WHERE ts_ms=3"
    ).fetchone()
    assert third["session_id"] not in {first["session_id"], second["session_id"]}
    assert third["seq"] == 1
    rolled_back.close()


def test_legacy_uuid_row_remains_replayable_after_v2_upgrade(tmp_path):
    path = tmp_path / "queue.db"
    q = CollectorRetryOutbox(path)
    q.close()
    with sqlite3.connect(path) as old_db:
        old_db.execute(
            "INSERT INTO collector_retry_outbox "
            "(id,device_code,group_code,scan_rate_ms,ts_ms,config_version,device_type,"
            "frame_json,stream_values_json,stream_qualities_json,latest_fields_json,created_at) "
            "VALUES (?,?,?,?,?,?,?,?,?,?,?,?)",
            (str(uuid4()), "D", "g", 100, 1, 2, "qc",
             msgspec.json.encode(_sample(1)["frame"]),
             msgspec.json.encode({"1": "7"}),
             msgspec.json.encode({"1": 192}),
             msgspec.json.encode(_sample(1)["latest_fields"]), 1),
        )
    q = CollectorRetryOutbox(path)
    q.record_failure(**_sample(2))
    writer = MagicMock()
    assert q.replay_pending(writer) == 2
    rows = writer.write_durable_frames.call_args.args[0]
    assert [row["protocol"] for row in rows] == ["legacy", "v2"]
    assert rows[0]["session_id"] is None
    assert rows[1]["session_id"] is not None
    q.close()


@pytest.fixture
def real_redis():
    url = os.environ.get("DW_TEST_REDIS_URL")
    if not url:
        pytest.skip("isolated Redis URL not configured")
    client = redis.Redis.from_url(url, socket_timeout=3)
    client.ping()
    try:
        yield client
    finally:
        client.close()


def _keys():
    prefix = "dw:receipt-v2-test:" + uuid4().hex
    return [prefix + suffix for suffix in (":receipt", ":stream", ":latest", ":window")]


def _eval_v2(client, keys, seq):
    return client.eval(
        FRAME_LUA_V2, 4, *keys,
        msgspec.json.encode(["sample", str(seq)]),
        b"{}", msgspec.json.encode({"ts": seq}),
        "receipt-test", "g", seq,
    )


def test_real_redis_v2_gap_watermark_and_duplicate_replay(real_redis):
    keys = _keys()
    try:
        assert _eval_v2(real_redis, keys, 2) == 1
        assert real_redis.hget(keys[0], "high") == b"0"
        assert real_redis.hget(keys[0], "gaps") == b"1"
        assert _eval_v2(real_redis, keys, 2) == 0
        assert real_redis.xlen(keys[1]) == 1
        assert _eval_v2(real_redis, keys, 1) == 1
        assert real_redis.hget(keys[0], "high") == b"2"
        assert real_redis.hget(keys[0], "gaps") == b"0"
        assert real_redis.xlen(keys[1]) == 2
        assert real_redis.ttl(keys[0]) == -1
    finally:
        real_redis.delete(*keys)


def test_real_redis_v2_gap_limit_blocks_before_xadd(real_redis):
    keys = _keys()
    try:
        for seq in range(2, 1026):
            assert _eval_v2(real_redis, keys, seq) == 1
        assert real_redis.xlen(keys[1]) == 1024
        with pytest.raises(redis.ResponseError, match="gap limit"):
            _eval_v2(real_redis, keys, 1026)
        assert real_redis.xlen(keys[1]) == 1024
        assert _eval_v2(real_redis, keys, 1) == 1
        assert real_redis.hget(keys[0], "high") == b"1025"
        assert real_redis.hget(keys[0], "gaps") == b"0"
    finally:
        real_redis.delete(*keys)


def test_real_redis_v2_same_connection_waitaof_and_lost_reply(real_redis):
    keys = _keys()
    try:
        with real_redis.pipeline(transaction=False) as pipe:
            pipe.eval(FRAME_LUA_V2, 4, *keys,
                      msgspec.json.encode(["sample", "1"]), b"{}",
                      msgspec.json.encode({"ts": 1}), "receipt-test", "g", 1)
            pipe.execute_command("WAITAOF", 1, 0, 2000)
            result = pipe.execute()
        assert result[0] == 1
        assert result[-1][0] == 1
        # Simulate a client losing the successful reply and replaying its original identity.
        assert _eval_v2(real_redis, keys, 1) == 0
        assert real_redis.xlen(keys[1]) == 1
    finally:
        real_redis.delete(*keys)


def test_real_redis_legacy_uuid_protocol_remains_separate(real_redis):
    keys = _keys()
    try:
        legacy_key = keys[0] + ":uuid"
        args = (4, legacy_key, *keys[1:],
                msgspec.json.encode(["sample", "legacy"]), b"{}",
                msgspec.json.encode({"ts": 1}), "receipt-test", "g")
        real_redis.eval(FRAME_LUA, *args)
        real_redis.eval(FRAME_LUA, *args)
        assert real_redis.xlen(keys[1]) == 1
        assert real_redis.ttl(legacy_key) > 0
        assert _eval_v2(real_redis, keys, 1) == 1
        assert real_redis.xlen(keys[1]) == 2
    finally:
        real_redis.delete(*keys, keys[0] + ":uuid")


def test_v2_replay_uses_insert_order_when_wall_clock_rolls_back(tmp_path):
    path = tmp_path / "queue.db"
    q = CollectorRetryOutbox(path)
    q.close()
    session = str(uuid4())
    with sqlite3.connect(path) as db:
        db.executemany(
            "INSERT INTO collector_retry_outbox_v2 "
            "(id,session_id,seq,device_code,group_code,scan_rate_ms,ts_ms,"
            "config_version,device_type,frame_json,stream_values_json,"
            "stream_qualities_json,latest_fields_json,created_at) "
            "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
            [(str(uuid4()), session, seq, "D", "g", 100, seq, 2, "qc",
              msgspec.json.encode({"ts": seq}), msgspec.json.encode({"1": "7"}),
              None, msgspec.json.encode({}), 2000 - seq)
             for seq in range(1, 1031)],
        )
    q = CollectorRetryOutbox(path)
    writer = MagicMock()
    assert q.replay_pending(writer, max_batch=1024) == 1024
    first = [row["seq"] for row in writer.write_durable_frames.call_args.args[0]]
    assert q.replay_pending(writer) == 6
    second = [row["seq"] for row in writer.write_durable_frames.call_args.args[0]]
    assert first + second == list(range(1, 1031))
    q.close()


def test_real_redis_v2_high_water_is_exact_near_lua_integer_limit(real_redis):
    keys = _keys()
    try:
        real_redis.hset(keys[0], mapping={"high": "9007199254740988", "gaps": "0"})
        assert _eval_v2(real_redis, keys, 9007199254740990) == 1
        assert _eval_v2(real_redis, keys, 9007199254740989) == 1
        assert real_redis.hget(keys[0], "high") == b"9007199254740990"
        assert _eval_v2(real_redis, keys, 9007199254740991) == 1
        assert real_redis.hget(keys[0], "high") == b"9007199254740991"
        assert _eval_v2(real_redis, keys, 9007199254740991) == 0
        assert real_redis.xlen(keys[1]) == 3
    finally:
        real_redis.delete(*keys)


def test_dedicated_redis_noscript_after_partial_pipeline_retries(tmp_path):
    url = os.environ.get("DW_TEST_FAULT_REDIS_URL")
    if not url:
        pytest.skip("dedicated fault-test Redis URL not configured")
    from types import SimpleNamespace
    from storage.durable_redis_writer import DurableRedisWriter
    client = redis.Redis.from_url(url, socket_timeout=3)
    config = SimpleNamespace(redis=SimpleNamespace(stream_shard_count=4, stream_maxlen=0))
    writer = DurableRedisWriter(client, config, outbox_path=tmp_path / "queue.db")
    keys = _keys()
    legacy_key = keys[0] + ":uuid"
    fields = msgspec.json.encode(["sample", "value"])
    frame = msgspec.json.encode({"ts": 1})
    legacy_args = (4, legacy_key, *keys[1:], fields, b"{}", frame, "receipt-test", "g")
    v2_args = (4, *keys, fields, b"{}", frame, "receipt-test", "g", 1)
    try:
        writer._script_shas[FRAME_LUA] = client.script_load(FRAME_LUA)
        writer._script_shas[FRAME_LUA_V2] = client.script_load(FRAME_LUA_V2)
        client.script_flush()
        client.script_load(FRAME_LUA)
        writer._deliver_window([(FRAME_LUA, legacy_args), (FRAME_LUA_V2, v2_args)], 2048)
        assert client.xlen(keys[1]) == 2
        assert client.hget(keys[0], "high") == b"1"
        assert client.get(legacy_key) == b"1"
    finally:
        client.delete(*keys, legacy_key)
        writer.close()


def test_outbox_owner_lock_rejects_second_process_and_crash_releases(tmp_path):
    path = tmp_path / "single-owner.db"
    child_code = (
        "import sys; from storage.collector_outbox import CollectorRetryOutbox; "
        "q=CollectorRetryOutbox(sys.argv[1]); print('ready', flush=True); sys.stdin.read()"
    )
    child = subprocess.Popen(
        [sys.executable, "-c", child_code, str(path)],
        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, env=os.environ.copy(),
    )
    try:
        assert child.stdout.readline().strip() == "ready"
        with pytest.raises(RuntimeError, match="already owned"):
            CollectorRetryOutbox(path)
    finally:
        child.kill()
        child.wait(timeout=5)
        child.stdin.close()
        child.stdout.close()
        child.stderr.close()
    assert path.with_name(path.name + ".owner.lock").exists()
    reopened = CollectorRetryOutbox(path)
    reopened.close()
    reopened.close()


def test_outbox_owner_lock_released_after_init_failure(tmp_path, monkeypatch):
    path = tmp_path / "failed-init.db"
    with monkeypatch.context() as patch:
        patch.setattr(CollectorRetryOutbox, "_init_db",
                      lambda self: (_ for _ in ()).throw(RuntimeError("init failed")))
        with pytest.raises(RuntimeError, match="init failed"):
            CollectorRetryOutbox(path)
    reopened = CollectorRetryOutbox(path)
    reopened.close()


@pytest.mark.parametrize("protocol", ["legacy", "v2"])
@pytest.mark.parametrize("point_count", [0, 1, 256, 257, 513])
def test_real_redis_history_hmget_preserves_latest_and_duplicate_receipt(
    real_redis, protocol, point_count,
):
    keys = _keys()
    if protocol == "legacy":
        keys[0] += ":uuid"
    incoming = {
        str(i): {"v": "new", "t": 10, "q": 192}
        for i in range(1, point_count + 1)
    }
    previous = {
        str(i): msgspec.json.encode({
            "v": "old", "t": {0: 11, 1: 10, 2: 9}[i % 3], "q": 0,
        })
        for i in range(1, point_count + 1)
    }
    script = FRAME_LUA if protocol == "legacy" else FRAME_LUA_V2
    args = (
        msgspec.json.encode(["sample", "one"]),
        msgspec.json.encode(incoming),
        msgspec.json.encode({"ts": 10}),
        "receipt-test", "g",
    )
    if protocol == "v2":
        args += (1,)
    try:
        if previous:
            real_redis.hset(keys[2], mapping=previous)
        assert real_redis.eval(script, 4, *keys, *args) == 1
        assert real_redis.xlen(keys[1]) == 1
        assert real_redis.eval(script, 4, *keys, *args) == (
            1 if protocol == "legacy" else 0
        )
        assert real_redis.xlen(keys[1]) == 1
        actual = real_redis.hgetall(keys[2])
        assert len(actual) == point_count
        for i in range(1, point_count + 1):
            expected = (
                msgspec.json.decode(previous[str(i)])
                if i % 3 in (0, 1) else incoming[str(i)]
            )
            assert msgspec.json.decode(actual[str(i).encode()]) == expected
    finally:
        real_redis.delete(*keys)


@pytest.mark.parametrize("protocol", ["legacy", "v2"])
def test_real_redis_history_hmget_error_after_xadd_replays_without_duplicate(
    real_redis, protocol,
):
    keys = _keys()
    if protocol == "legacy":
        keys[0] += ":uuid"
    script = FRAME_LUA if protocol == "legacy" else FRAME_LUA_V2
    args = (
        msgspec.json.encode(["sample", "one"]),
        msgspec.json.encode({"1": {"v": "new", "t": 10, "q": 192}}),
        msgspec.json.encode({"ts": 10}),
        "receipt-test", "g",
    )
    if protocol == "v2":
        args += (1,)
    try:
        real_redis.set(keys[2], "wrong latest type")
        with pytest.raises(redis.ResponseError, match="WRONGTYPE"):
            real_redis.eval(script, 4, *keys, *args)
        assert real_redis.xlen(keys[1]) == 1
        if protocol == "legacy":
            assert real_redis.get(keys[0]) == b"1"
        else:
            assert real_redis.hget(keys[0], "high") == b"1"
        real_redis.delete(keys[2])
        real_redis.eval(script, 4, *keys, *args)
        assert real_redis.xlen(keys[1]) == 1
        assert msgspec.json.decode(real_redis.hget(keys[2], "1")) == {
            "v": "new", "t": 10, "q": 192,
        }
    finally:
        real_redis.delete(*keys)
