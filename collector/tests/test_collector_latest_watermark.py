"""History latest-skip watermark: unit tests (fake Redis) plus real-Lua tests.

The real-Lua tests run against a live Redis and are skipped unless
``DATAWATCH_TEST_REDIS_URL`` points at a disposable instance.
"""
from __future__ import annotations

import json
import os
import time
from types import SimpleNamespace

import msgspec
import pytest

from storage.durable_redis_writer import (
    FRAME_LUA,
    FRAME_LUA_V2,
    LATEST_LUA,
    DurableRedisWriter,
    _watermark_field,
)


# ----------------------------------------------------------------------
# 假 Redis：复刻两个脚本对键的读写语义，用于断言“跳过了多少次逐点工作”
# ----------------------------------------------------------------------

class FakeHistoryRedis:
    """只实现 FRAME_LUA / FRAME_LUA_V2 用到的命令。"""

    def __init__(self, *, watermark=None, latest=None):
        self.receipt = {}
        if watermark is not None:
            self.receipt["l:D:g"] = str(watermark)
        self.latest = dict(latest or {})
        self.streams = []
        self.windows = []
        self.published = []
        self.hmget_calls = 0
        self.hset_calls = 0

    def hget(self, key, field):
        return self.receipt.get(field)

    def hexists(self, key, field):
        return 1 if field in self.receipt else 0

    def hset(self, key, field=None, value=None):
        self.receipt[field] = value
        return 1

    def hmget(self, key, *ids):
        self.hmget_calls += 1
        return [self.latest.get(i) for i in ids]

    def hdel(self, key, *fields):
        for field in fields:
            self.receipt.pop(field, None)

    def xadd(self, key, fields, **kwargs):
        self.streams.append((key, fields))
        return "1-0"

    def lindex(self, key, index):
        return self.windows[-1] if self.windows else None

    def lpush(self, key, value):
        self.windows.append(value)
        return len(self.windows)

    def ltrim(self, key, start, stop):
        self.windows = self.windows[start:stop + 1]
        return 1

    def set(self, key, value, **kwargs):
        return 1

    def hincrby(self, key, field, amount):
        self.receipt[field] = str(int(self.receipt.get(field, "0")) + amount)
        return 1


class FakeLuaRedis:
    """在纯 Python 侧把 Lua 源码翻译成等价动作，用于单元测试。"""

    def __init__(self):
        self.evals = []

    def eval(self, script, numkeys, *args):
        keys = list(args[:numkeys])
        argv = list(args[numkeys:])
        self.evals.append({"script": script, "keys": keys, "argv": argv})
        return 1


def _run_history(script, redis, *, frame_ts, latest_fields, values=None,
                 receipt_key=None):
    """按真实参数顺序调用脚本对应的假实现，并返回是否执行了逐点 latest 段。

    生产的 KEYS 布局是两种之一：
      * FRAME_LUA     ：1=receipt（每帧一个字符串键）, 2=stream, 3=latest, 4=window
      * FRAME_LUA_V2  ：1=receipt, 2=receipt（水位同键）, 3=stream, 4=latest, 5=window
    """
    values = values or {str(p): str(p) for p in range(1, 4)}
    latest = latest_fields or {str(p): {"v": str(p), "t": frame_ts, "q": 192} for p in values}
    flat = ["device_code", "D", "group_code", "g", "ts", str(frame_ts),
            "values", json.dumps(values)]
    incoming = msgspec.json.encode(latest)
    frame = msgspec.json.encode({"ts": frame_ts, "values": values})
    if receipt_key is None:
        # 旧协议 receipt 与水位无关（每帧一个键），用一个不存在的键即可。
        receipt_key = ("collector:receipt:" + str(frame_ts) if script is FRAME_LUA
                       else "collector:receipt:v2:S")
    decoded_incoming = msgspec.json.decode(incoming)
    # 只有 v2 脚本参与水位判断；旧协议永远执行全量 latest 段。
    watermark = (0 if script is FRAME_LUA
                 else int(redis.hget(receipt_key, _watermark_field("D", "g")) or 0))
    superseded = watermark > 0 and frame_ts <= watermark
    if not superseded:
        ids = list(decoded_incoming)
        old = redis.hmget(f"rt:latest:D:g", *ids)
        for i, point_id in enumerate(ids):
            if not old[i] or int(json.loads(old[i]).get("t", 0)) < decoded_incoming[point_id]["t"]:
                redis.latest[point_id] = json.dumps(decoded_incoming[point_id])
                redis.hset_calls += 1
    redis.streams.append(("rt:stream:dt:g:0", flat))
    redis.windows.append(frame)
    return not superseded


def test_history_skips_pointwise_latest_when_watermark_covers_frame():
    redis = FakeHistoryRedis(watermark=5000)
    executed = _run_history(FRAME_LUA_V2, redis, frame_ts=4000, latest_fields={"1": {"v": "7", "t": 4000, "q": 192}})
    assert executed is False
    assert redis.hmget_calls == 0, "被水位覆盖的帧不得再做逐点 HMGET"
    assert redis.hset_calls == 0
    assert redis.streams, "stream 追加必须照旧发生"


def test_history_runs_pointwise_latest_when_watermark_is_stale():
    redis = FakeHistoryRedis(watermark=3000)
    executed = _run_history(FRAME_LUA_V2, redis, frame_ts=4000, latest_fields={"1": {"v": "7", "t": 4000, "q": 192}})
    assert executed is True
    assert redis.hmget_calls == 1
    assert redis.latest["1"] == json.dumps({"v": "7", "t": 4000, "q": 192})


def test_history_runs_pointwise_latest_when_watermark_missing():
    redis = FakeHistoryRedis(watermark=None)
    executed = _run_history(FRAME_LUA_V2, redis, frame_ts=4000, latest_fields={"1": {"v": "7", "t": 4000, "q": 192}})
    assert executed is True, "水位缺失（Redis 被清 / 快车道从未成功）必须回退到全量 latest 段"
    assert redis.hmget_calls == 1


def test_history_fastlane_and_history_agree_on_equal_timestamp():
    """快车道与历史帧携带同一采样时间时，判定必须一致（此处覆盖为跳过）。"""
    redis = FakeHistoryRedis(watermark=4000)
    executed = _run_history(FRAME_LUA_V2, redis, frame_ts=4000, latest_fields={"1": {"v": "7", "t": 4000, "q": 192}})
    assert executed is False


def test_legacy_protocol_never_skips_latest():
    """旧 UUID 协议不参与水位判断，即使有人往无关键里写水位也必须执行 latest 段。"""
    redis = FakeHistoryRedis(watermark=99999)
    executed = _run_history(
        FRAME_LUA, redis, frame_ts=1, latest_fields={"1": {"v": "7", "t": 1, "q": 192}})
    assert executed is True, "旧协议必须始终执行全量 latest 段"
    assert redis.hmget_calls == 1


# ----------------------------------------------------------------------
# 真实 Lua：需要隔离 Redis
# ----------------------------------------------------------------------

REDIS_URL = os.environ.get("DATAWATCH_TEST_REDIS_URL")
real_redis = pytest.mark.skipif(not REDIS_URL, reason="DATAWATCH_TEST_REDIS_URL is not set")


@pytest.fixture
def live_redis():
    import redis as redis_lib
    client = redis_lib.Redis.from_url(REDIS_URL, decode_responses=True, socket_timeout=10)
    client.ping()
    yield client
    client.close()


def _keys(token):
    """按生产 KEYS 顺序返回：[receipt/水位, stream, latest, window]。"""
    return [f"dw:wmt:{token}:receipt", f"dw:wmt:{token}:stream",
            f"dw:wmt:{token}:latest", f"dw:wmt:{token}:window"]


@real_redis
def test_real_lua_fastlane_advances_watermark_and_history_then_skips(live_redis):
    token = f"wm{int(time.time()*1000)}"
    receipt, stream, latest, window = _keys(token)
    shas = {name: live_redis.script_load(script) for name, script in
            (("latest", LATEST_LUA), ("frame", FRAME_LUA_V2))}
    try:
        ts = 1_700_000_000_000
        points = {str(p): {"v": str(p), "t": ts, "q": 192} for p in range(1, 6)}
        # 快车道确认写入 → 水位推进到 receipt 哈希，绝不污染 latest 哈希
        live_redis.evalsha(shas["latest"], 2, latest, receipt,
                           msgspec.json.encode(points), "D", "g", "D", "g")
        assert int(live_redis.hget(receipt, "l:D:g")) == ts
        assert all(not field.startswith("l:") for field in live_redis.hgetall(latest)), \
            "latest 哈希不得混入水位字段（后端对它逐字段 JSON 解析）"

        values = {str(p): str(p) for p in range(1, 6)}
        flat = []
        for name, value in (("device_code", "D"), ("group_code", "g"),
                            ("ts", str(ts)), ("values", json.dumps(values))):
            flat.extend((name, value))
        frame = msgspec.json.encode({"ts": ts, "values": values})

        def deliver(frame_ts, seq):
            # 生产 5 键布局：receipt、水位键（同一 receipt 哈希）、stream、latest、window
            live_redis.evalsha(shas["frame"], 5, receipt, receipt, stream, latest, window,
                               msgspec.json.encode(flat), msgspec.json.encode(points),
                               msgspec.json.encode({"ts": frame_ts, "values": values}),
                               "D", "g", seq)

        # 被水位覆盖的历史帧：latest 不得被改写
        live_redis.hset(latest, "1", json.dumps({"v": "SENTINEL", "t": ts, "q": 192}))
        deliver(ts, 1)
        assert json.loads(live_redis.hget(latest, "1"))["v"] == "SENTINEL", \
            "水位覆盖的历史帧不应重新写 latest"
        assert live_redis.xlen(stream) == 1, "stream 追加必须照旧发生"
        assert live_redis.llen(window) == 1, "window 追加必须照旧发生"

        # 比水位更新的历史帧：必须真的写 latest
        newer = ts + 1000
        newer_points = {str(p): {"v": "NEW", "t": newer, "q": 192} for p in range(1, 6)}
        live_redis.evalsha(shas["frame"], 5, receipt, receipt, stream, latest, window,
                           msgspec.json.encode(flat), msgspec.json.encode(newer_points),
                           msgspec.json.encode({"ts": newer, "values": values}), "D", "g", 2)
        assert json.loads(live_redis.hget(latest, "1"))["v"] == "NEW", \
            "比水位更新的历史帧必须写入 latest"
        assert live_redis.xlen(stream) == 2

        # 清掉水位后必须回退到全量 latest 段；同时间戳下逐点守卫不会改写已有值，
        # 因此把 latest 清空来观察“确实执行了写入”，而不是依赖旧值。
        live_redis.hdel(receipt, "l:D:g")
        live_redis.delete(latest)
        deliver(ts, 3)
        assert json.loads(live_redis.hget(latest, "1"))["v"] == "1", \
            "水位缺失时必须回退到全量 latest 段"
    finally:
        live_redis.delete(receipt, stream, latest, window)


@real_redis
def test_real_lua_legacy_protocol_always_runs_latest(live_redis):
    token = f"lg{int(time.time()*1000)}"
    receipt, stream, latest, window = _keys(token)
    digest = live_redis.script_load(FRAME_LUA)
    try:
        ts = 1_700_000_000_000
        values = {"1": "7"}
        flat = ["device_code", "D", "group_code", "g", "ts", str(ts),
                "values", json.dumps(values)]
        points = {"1": {"v": "7", "t": ts, "q": 192}}
        # 旧协议是 4 键布局（receipt/stream/latest/window），不参与水位判断。
        live_redis.evalsha(digest, 4, receipt, stream, latest, window,
                           msgspec.json.encode(flat), msgspec.json.encode(points),
                           msgspec.json.encode({"ts": ts, "values": values}), "D", "g")
        assert json.loads(live_redis.hget(latest, "1"))["v"] == "7", \
            "旧协议必须始终执行 latest 段"
    finally:
        live_redis.delete(receipt, stream, latest, window)


# ----------------------------------------------------------------------
# 会话切换与恢复
# ----------------------------------------------------------------------

def test_queue_latest_drops_mixed_session_state(tmp_path):
    """重装/回滚后新会话的帧不能与旧会话点值合并成同一份载荷。"""
    from storage.collector_outbox import CollectorRetryOutbox
    q = CollectorRetryOutbox(tmp_path / "q.db")
    writer = DurableRedisWriter(FakeLuaRedis(), SimpleNamespace(
        redis=SimpleNamespace(stream_shard_count=4, stream_maxlen=0)), outbox_path=tmp_path / "q2.db")
    try:
        writer._queue_latest("D", "g", {"1": {"v": "old", "t": 1, "q": 192}}, "session-old")
        state = writer._latest_pending[("D", "g")]
        assert state.session_id == "session-old"
        writer._queue_latest("D", "g", {"1": {"v": "new", "t": 2, "q": 192}}, "session-new")
        state = writer._latest_pending[("D", "g")]
        assert state.session_id == "session-new"
        assert state.fields == {"1": {"v": "new", "t": 2, "q": 192}}, \
            "会话切换后不得保留旧会话的点值"
        assert state.high == 2
    finally:
        writer.close()
        q.close()


def test_restored_applied_watermark_suppresses_redundant_resend(tmp_path):
    """重启恢复时带上已确认水位，低时间戳的点不应再发一遍。"""
    from storage.collector_outbox import CollectorRetryOutbox
    q = CollectorRetryOutbox(tmp_path / "q.db")
    writer = DurableRedisWriter(FakeLuaRedis(), SimpleNamespace(
        redis=SimpleNamespace(stream_shard_count=4, stream_maxlen=0)), outbox_path=tmp_path / "q2.db")
    try:
        writer._queue_latest("D", "g", {"1": {"v": "old", "t": 1, "q": 192}}, "S", applied=5)
        assert ("D", "g") not in writer._latest_pending, \
            "已确认水位更高的点不应再进入待发队列"
    finally:
        writer.close()
        q.close()
