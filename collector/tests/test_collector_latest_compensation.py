"""历史回放对 latest 的补偿不变式（回归测试）。

背景（2026-09-25 审查结论）
---------------------------
曾实现过一版优化：快车道把 `l:<device>:<group>` = 本批点的**最大时间戳**写入
receipt 哈希，历史回放发现 `frame.ts <= 水位` 就跳过整帧的 latest 更新。

该条件是**不成立**的。历史回放需要证明的是：

    对历史帧里的每一个点，Redis 中都已存在不早于该点时间戳的状态。

而组级水位只证明了"同组某一批点的最大时间戳足够大"。两者不等价，因为：

1. 同一 (device, group) 在不同 tick 的**点集可以不同**（采集执行器按当前
   scan_task 的点构造帧、按 group_code 分组）；
2. 各点可以**携带各自的时间戳**；
3. 快车道会**按组延后/丢弃**在途条目（`_latest_pending` 达 1024 组或 8MiB 时
   `latest_fastlane_deferred_frames++` 后直接 return；发送异常时同样丢下）。

因此"某一帧的快车道失败不会推进水位"这句话不足以成立：同组**另一个点集**的
成功帧可以替失败帧把水位推高，于是被延后的那批点再也不会被历史路径补上，
表现为"历史 Stream 完整、但实时 latest 缺值或停留旧值"。

这些测试把该不变式钉死：历史回放**必须**补齐快车道漏掉的那批点，
无论 Redis 里是否存在组级水位字段。若有人重新引入"仅凭组级最大时间戳就跳过
latest 段"的条件，本文件的断言会失败。
"""
from __future__ import annotations

import json
from types import SimpleNamespace

import msgspec
import pytest

from storage.durable_redis_writer import FRAME_LUA, FRAME_LUA_V2

class FakeRedis:
    """按 FRAME_LUA / FRAME_LUA_V2 用到的命令语义实现的假 Redis。

    忠实复刻生产脚本的两个关键行为：
      * 逐点守卫：只有 `cjson.decode(old).t < value.t` 才写；
      * HMGET 分批：脚本按 256 个字段一批调用 HMGET。
    """

    def __init__(self, *, point_ts=None, stream_ready=True):
        self.latest: dict[str, str] = {}
        self.streams: list[tuple] = []
        self.windows: list[str] = []
        self.receipt: dict[str, dict] = {}
        self.published: list[str] = []
        self.hmget_calls = 0
        self.hset_point_calls = 0
        self._stream_not_ready = not stream_ready

    # --- 供脚本直接调用 ---
    def get(self, key):
        return self.receipt.get(key, {}).get("__receipt__")

    def set(self, key, value, **kwargs):
        self.receipt.setdefault(key, {})["__receipt__"] = value
        return 1

    def hget(self, key, field):
        return self.receipt.get(key, {}).get(field)

    def hexists(self, key, field):
        return 1 if field in self.receipt.get(key, {}) else 0

    def hset(self, key, field=None, value=None):
        self.receipt.setdefault(key, {})[field] = value
        return 1

    def hdel(self, key, *fields):
        for field in fields:
            self.receipt.get(key, {}).pop(field, None)

    def hincrby(self, key, field, amount):
        bucket = self.receipt.setdefault(key, {})
        bucket[field] = str(int(bucket.get(field, "0")) + amount)
        return 1

    def hmget(self, key, *ids):
        self.hmget_calls += 1
        if key != "latest":
            raise AssertionError(f"unexpected HMGET key {key!r}")
        return [self.latest.get(i) for i in ids]

    def xadd(self, key, fields, **kwargs):
        if self._stream_not_ready:
            raise RuntimeError("stream key holds the wrong type")
        self.streams.append(tuple(fields))
        return f"{len(self.streams)}-0"

    def lindex(self, key, index):
        return self.windows[-1] if self.windows else None

    def lpush(self, key, value):
        self.windows.append(value)
        return len(self.windows)

    def ltrim(self, key, start, stop):
        self.windows = self.windows[start:stop + 1]
        return 1

    def publish(self, channel, payload):
        self.published.append(payload)
        return 1


def delivery_args(device, group, ts, latest_fields, values, session_id="S", seq=1):
    flat: list[str] = []
    for key, value in (("device_code", device), ("group_code", group),
                       ("scan_rate", "100"), ("ts", str(ts)),
                       ("config_version", "1"),
                       ("contract_version", "2"),
                       ("values", json.dumps(values))):
        flat.extend((key, value))
    frame = {"ts": ts, "values": values}
    return (5,
            f"receipt:{session_id}", f"receipt:{session_id}", "stream", "latest", "window",
            json.dumps(flat), json.dumps(latest_fields), json.dumps(frame),
            device, group, seq)


def simulate_history(redis: FakeRedis, args) -> bool:
    """执行 FRAME_LUA_V2 的等价语义（逐点守卫 + 水位判断按脚本原文）。

    返回 True 表示执行了逐点 latest 段。
    """
    numkeys = args[0]
    keys = list(args[1:1 + numkeys])
    argv = list(args[1 + numkeys:])
    fields = json.loads(argv[0])
    incoming = json.loads(argv[1])
    frame = json.loads(argv[2])
    device, group, seq = argv[3], argv[4], int(argv[5])

    state = redis.receipt.setdefault(keys[0], {})
    high = int(state.get("high", "0"))
    gaps = int(state.get("gaps", "0"))
    gap_field = f"g:{seq}"
    duplicate = seq <= high or state.get(gap_field) is not None
    if not duplicate:
        redis.xadd(keys[2], fields)
        if seq == high + 1:
            high = seq
            state["high"] = str(high)
            state["gaps"] = str(gaps)
        else:
            state[gap_field] = "1"
            state["gaps"] = str(gaps + 1)

    recent = redis.lindex(keys[4], 0)
    if not recent or json.loads(recent).get("ts", 0) < frame.get("ts", 0):
        redis.lpush(keys[4], argv[2])
        redis.ltrim(keys[4], 0, 9)

    # 生产 FRAME_LUA_V2 的 latest 段：只有逐点时间戳守卫，没有任何组级水位跳过。
    changed = False
    ids = list(incoming)
    for start in range(0, len(ids), 256):
        batch = ids[start:start + 256]
        old_values = redis.hmget(keys[3], *batch)
        for index, point_id in enumerate(batch):
            old = old_values[index]
            value = incoming[point_id]
            if not old or json.loads(old).get("t", 0) < value["t"]:
                redis.latest[point_id] = json.dumps(value)
                redis.hset_point_calls += 1
                changed = True
    if changed:
        redis.publish("rt:change", json.dumps({"device_code": device, "group_code": group}))
    state["fence"] = str(int(state.get("fence", "0")) + 1)
    return not duplicate


# ----------------------------------------------------------------------
# 反例回归：快车道漏了一帧，随后同组另一个点集的帧成功，历史必须补齐
# ----------------------------------------------------------------------

def test_history_latest_compensation_survives_group_watermark_like_state():
    """快车道漏掉 A 帧、随后 B 帧成功推进了组级水位，历史回放 A 仍必须写入 A 的点。

    这正是组级时间戳水位会导致漏补的形态：A.t=1000 被 A 帧之外的成功帧推高到 1100。
    本测试在 receipt 里预置一个"看起来更高"的组级水位字段，验证生产脚本
    **不会**因为该字段而跳过 latest 段。
    """
    redis = FakeRedis()
    # 模拟曾经那版优化写下的组级水位字段（生产脚本不得据此跳过）
    redis.receipt.setdefault("receipt:S", {})["l:DEV:g"] = "1100"
    # 点 A 在快车道被延后，因此 Redis latest 里没有它；点 B 已被更高时间戳写入
    redis.latest["B"] = json.dumps({"v": "b", "t": 1100, "q": 0})

    executed = simulate_history(redis, delivery_args(
        "DEV", "g", ts=1000,
        latest_fields={"A": {"v": "a", "t": 1000, "q": 0}, "B": {"v": "b", "t": 1000, "q": 0}},
        values={"A": "a", "B": "b"}, seq=1))

    assert executed is True, "历史回放必须执行逐点 latest 段"
    assert "A" in redis.latest, \
        "被快车道延后的点 A 必须由历史回放补上，不能因组级水位被跳过"
    assert json.loads(redis.latest["A"])["t"] == 1000
    # 点 B 已有更新（1100），逐点守卫必须保护它不被 1000 覆盖
    assert json.loads(redis.latest["B"])["t"] == 1100, "逐点时间戳守卫必须保留更新的值"
    assert len(redis.streams) == 1, "stream 追加必须照旧发生"


def test_history_latest_compensation_when_point_set_changes_between_ticks():
    """同组相邻 tick 点集不同（多采样周期）时，历史必须补齐各自缺失的点。"""
    redis = FakeRedis()
    # 第 2 帧（点集 {C}，t=1200）已由快车道成功写入并可能推高过组级水位
    redis.latest["C"] = json.dumps({"v": "c", "t": 1200, "q": 0})

    # 历史回放第 1 帧：点集 {A}，t=1000（快车道因容量延后，未写入）
    simulate_history(redis, delivery_args(
        "DEV", "g", ts=1000, latest_fields={"A": {"v": "a", "t": 1000, "q": 0}},
        values={"A": "a"}, seq=1))
    # 历史回放第 3 帧：点集 {C}，t=1400
    simulate_history(redis, delivery_args(
        "DEV", "g", ts=1400, latest_fields={"C": {"v": "c2", "t": 1400, "q": 0}},
        values={"C": "c2"}, seq=2))

    assert set(redis.latest) == {"A", "C"}, "两个点集各自的点都必须存在于 latest"
    assert json.loads(redis.latest["A"])["t"] == 1000
    assert json.loads(redis.latest["C"])["t"] == 1400
    assert len(redis.streams) == 2


def test_history_latest_compensation_with_multi_sampling_periods_in_one_frame():
    """同一分组内不同采样周期的点可以在同一帧里带不同时间戳。

    此时组级最大时间戳与"每个点都被覆盖"的差距最明显：帧内最旧的点
    可能远早于同帧其它点，也可能早于同组其它帧成功推进过的水位。
    """
    redis = FakeRedis()
    # 同组另一次快车道成功把水位推到 9000（模拟曾经那版优化的组级水位残留）
    redis.receipt.setdefault("receipt:S", {})["l:DEV:g"] = "9000"
    redis.latest["FAST"] = json.dumps({"v": "f", "t": 9000, "q": 0})

    simulate_history(redis, delivery_args(
        "DEV", "g", ts=9000,
        latest_fields={"FAST": {"v": "f", "t": 9000, "q": 0},   # 快周期
                       "SLOW": {"v": "s", "t": 1000, "q": 0}},  # 慢周期，时间戳早很多
        values={"FAST": "f", "SLOW": "s"}, seq=1))

    assert "SLOW" in redis.latest, \
        "同帧内时间戳更旧的慢周期点也必须写入（快车道从未写过它）"
    assert json.loads(redis.latest["SLOW"])["t"] == 1000
    assert json.loads(redis.latest["FAST"])["t"] == 9000, "更新的同帧点不得被回退"


def test_history_replay_is_idempotent_for_already_applied_points():
    """重复回放（receipt 未删、历史重放）不得改变已经更新的值。"""
    redis = FakeRedis()
    args = delivery_args("DEV", "g", ts=1000,
                         latest_fields={"A": {"v": "a1", "t": 1000, "q": 0}},
                         values={"A": "a1"}, seq=1)
    simulate_history(redis, args)
    assert json.loads(redis.latest["A"])["v"] == "a1"
    # 同帧重复交付：逐点守卫判定 old.t == new.t，不覆盖
    simulate_history(redis, args)
    assert json.loads(redis.latest["A"])["v"] == "a1"
    assert len(redis.streams) == 1, "receipt 去重必须让重复交付不再 XADD"


def test_legacy_protocol_has_no_skip_condition():
    """旧 UUID 协议（4 键布局）不得读取任何水位字段。"""
    assert "HGET" not in FRAME_LUA.split("local incoming")[0], \
        "旧协议在 latest 段之前不应读取任何哈希字段"
    for token in ("watermark", "superseded", "l:"):
        assert token not in FRAME_LUA, f"旧协议脚本不应包含 {token!r}"


def test_production_history_scripts_read_no_group_watermark():
    """生产历史脚本不得包含"仅凭组级最大时间戳跳过 latest"的条件。"""
    for name, script in (("FRAME_LUA", FRAME_LUA), ("FRAME_LUA_V2", FRAME_LUA_V2)):
        lowered = script.lower()
        for token in ("watermark", "superseded"):
            assert token not in lowered, f"{name} 不应再包含 {token!r} 跳过逻辑"


def test_latest_compensation_happens_even_when_stream_write_fails():
    """stream 写入抛错时 receipt 未落、latest 也不会被"假装"推进（顺序不变式）。"""
    redis = FakeRedis(stream_ready=False)
    with pytest.raises(RuntimeError):
        simulate_history(redis, delivery_args(
            "DEV", "g", ts=1000, latest_fields={"A": {"v": "a", "t": 1000, "q": 0}},
            values={"A": "a"}, seq=1))
    # XADD 失败发生在 receipt 写入之前，因此该帧仍会被重放补上
    assert redis.get("receipt:S") is None


def test_latest_carrying_own_timestamps_is_preserved_per_point():
    """同一帧内各点时间戳不同时，逐点守卫必须各判各的。"""
    redis = FakeRedis()
    redis.latest["OLD"] = json.dumps({"v": "keep", "t": 5000, "q": 0})
    simulate_history(redis, delivery_args(
        "DEV", "g", ts=5000,
        latest_fields={"OLD": {"v": "stale", "t": 4000, "q": 0},
                       "NEW": {"v": "fresh", "t": 6000, "q": 0}},
        values={"OLD": "stale", "NEW": "fresh"}, seq=1))
    assert json.loads(redis.latest["OLD"])["v"] == "keep", \
        "时间戳更旧的点不得覆盖已更新的值"
    assert json.loads(redis.latest["NEW"])["v"] == "fresh", \
        "时间戳更新的点必须写入，即使同帧另一点更旧"


# ----------------------------------------------------------------------
# 快车道失败 / 延后之后，历史回放必须能补齐 —— 接真实 DurableRedisWriter 走一遍
# ----------------------------------------------------------------------

def test_fast_lane_deferral_then_history_replay_restores_every_point(tmp_path):
    """快车道对某组延后（或发送失败）时，历史回放仍必须把该组的点补进 latest。

    这条路径把真实 DurableRedisWriter 接上假 Redis：
      1. 快车道 EVAL 抛错 → 该组 latest 不会被写；
      2. 帧此时必须已经在本地 SQLite 里（record_failure 先于快车道入队）；
      3. 历史回放（FRAME_LUA_V2 语义）必须把这些点补上。
    """
    from storage.durable_redis_writer import DurableRedisWriter

    redis = FakeRedis()

    class FailingFastLaneClient:
        """按原样保留真实 writer 的 hget/eval 契约，只让快车道 EVAL 失败。"""

        connection_pool = None

        def hget(self, key, field):
            return None

        def eval(self, *args, **kwargs):
            raise RuntimeError("fast lane redis unavailable")

        def pipeline(self, *a, **k):
            raise AssertionError("history replay not expected in this test")

        def script_load(self, source):
            return "sha"

        def close(self):
            return None

    writer = DurableRedisWriter(
        FailingFastLaneClient(),
        SimpleNamespace(redis=SimpleNamespace(stream_shard_count=4, stream_maxlen=0)),
        outbox_path=tmp_path / "q.db",
    )
    try:
        points = {str(p): {"v": str(p), "t": 1000, "q": 0} for p in range(1, 4)}
        writer.write_window_stream("DEV", "dt", "g", 100, 1000, 1,
                                   {"ts": 1000}, {p: v["v"] for p, v in points.items()},
                                   {p: 0 for p in points})
        writer.write_latest("DEV", "g", points)

        # 帧必须先进本地持久队列（可靠性边界），而不是先依赖快车道。
        assert writer.outbox.count() == 1, "快车道写入之前，帧必须已经本地持久化"

        # 快车道失败后，latest 里当然还没有这些点；历史回放必须补齐。
        assert redis.latest == {}
        simulate_history(redis, delivery_args(
            "DEV", "g", ts=1000, latest_fields=points,
            values={p: v["v"] for p, v in points.items()}, seq=1))
        assert set(redis.latest) == set(points), "历史回放必须补齐快车道漏掉的整组点"
        assert all(json.loads(redis.latest[p])["t"] == 1000 for p in points)
    finally:
        writer.close()
