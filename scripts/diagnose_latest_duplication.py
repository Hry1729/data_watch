"""诊断：历史回放与 latest 快车道的重复处理成本（只读隔离 Redis，禁止用于生产开关）。

要回答的问题
------------
1. 同一帧的"latest 更新"在两条路径上各做一遍：历史 Lua 的补偿段 + 独立快车道脚本。
   这段重复到底占多少？必须**同时**测两条路径，否则历史脚本变快可能只是把工作推给了快车道。
2. 每个点的成本里，"读取+解码+比较"和"编码+写入"各占多少？
   历史段按 256 点分块做 HMGET，快车道逐点 HGET——两者都是"读一遍"，但 redis.call 次数不同。
3. latest Hash 的编码（listpack / hashtable）在什么规模翻转，是否影响逐点查找成本。

方法（见报告"技术判断边界"）
--------------------------
* 变体只在**本工具内**，不改生产代码；`history_no_latest` 明确标记 diagnostic_only，
  不参与正确性与可靠性验收，也不能变成生产可启用的绕过开关。
* 操作次数取自 Redis 自己的 commandstats（hget/hmget/hset 计数），不是脚本里自报的数。
* `eval`/`evalsha` 的计时**不**与内部 hget/hset 的计时相加（父子重复计时）。
* CPU 分别采进程总计与主线程（used_cpu_*_main_thread）。
* 分位数来自**原始样本**（排序后取索引），不是直方图桶；样本量随结果一起输出。

用法
----
    python scripts/diagnose_latest_duplication.py --redis-host perf-redis \
        --points 500 --frames 200 --output outputs/<本轮目录>/latest-cost.json
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import platform
from pathlib import Path
import statistics
import sys
import time
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector")]

import redis  # noqa: E402
from data_watch_shared.stream_envelope import build_stream_fields, stream_key  # noqa: E402
from storage.durable_redis_writer import FRAME_LUA_V2, LATEST_LUA  # noqa: E402

ALLOWED_HOSTS = {"perf-redis", "perf-redis-profile"}
LATEST_COUNTERS = ("hget", "hmget", "hset", "hdel", "publish", "xadd", "lindex",
                   "lpush", "ltrim", "hincrby", "hexists", "type", "eval", "evalsha")


# ----------------------------------------------------------------------
# 变体脚本（诊断专用）
# ----------------------------------------------------------------------

def history_without_latest_compensation(script: str) -> str:
    """移除历史 Lua 的 latest 补偿段，保留其余历史处理（XADD/receipt/window/fence）。"""
    start = script.index("local changed = false")
    end = script.index("local recent =", start)
    return script[:start] + "local changed = false\n" + script[end:]


def latest_with_chunked_hmget(script: str, chunk: int = 256) -> str:
    """快车道：逐点 HGET 改为分块 HMGET（比较语义不变：只有更新的 t 才写）。"""
    start = script.index("local changed = false")
    end = script.index("if changed then")
    body = """local changed = false
local ids = {}
local values = {}
for id, value in pairs(incoming) do
    ids[#ids + 1] = id
    values[#values + 1] = value
end
for first = 1, #ids, __CHUNK__ do
    local batch = {}
    local last = math.min(first + __CHUNK__ - 1, #ids)
    for i = first, last do batch[#batch + 1] = ids[i] end
    local old_values = redis.call('HMGET', KEYS[1], unpack(batch))
    for offset = 1, #old_values do
        local old = old_values[offset]
        local value = values[first + offset - 1]
        if not old or tonumber(cjson.decode(old).t or 0) < tonumber(value.t) then
            redis.call('HSET', KEYS[1], ids[first + offset - 1], cjson.encode(value))
            changed = true
        end
    end
end
""".replace("__CHUNK__", str(chunk))
    return script[:start] + body + script[end:]


# ----------------------------------------------------------------------
# 采样
# ----------------------------------------------------------------------

def cpu_snapshot(client) -> dict:
    info = client.info("cpu")
    main_sys = info.get("used_cpu_sys_main_thread")
    main_user = info.get("used_cpu_user_main_thread")
    process = float(info["used_cpu_sys"]) + float(info["used_cpu_user"])
    main = (None if main_sys is None or main_user is None
            else float(main_sys) + float(main_user))
    return {"process_cpu_s": process, "main_thread_cpu_s": main,
            "main_thread_fields_available": main is not None}


def command_snapshot(client) -> dict:
    stats = client.info("commandstats")
    out = {}
    for key, value in stats.items():
        name = key.split("_", 1)[1] if "_" in key else key
        out[name] = {"calls": int(value.get("calls", 0)),
                     "usec": int(value.get("usec", 0))}
    return out


def command_delta(before: dict, after: dict) -> dict:
    out = {}
    for name in LATEST_COUNTERS:
        b = before.get(name, {"calls": 0, "usec": 0})
        a = after.get(name, {"calls": 0, "usec": 0})
        out[name] = {"calls": a["calls"] - b["calls"], "usec": a["usec"] - b["usec"]}
    return out


def percentile(values: list[float], fraction: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    index = min(len(ordered) - 1, max(0, int(len(ordered) * fraction)))
    return ordered[index]


def describe(samples: list[float]) -> dict:
    if not samples:
        return {"count": 0}
    return {"count": len(samples), "mean_ms": round(statistics.mean(samples), 3),
            "p50_ms": round(percentile(samples, .50), 3),
            "p95_ms": round(percentile(samples, .95), 3),
            "p99_ms": round(percentile(samples, .99), 3),
            "max_ms": round(max(samples), 3),
            "provenance": "raw_samples_sorted_index"}


def latest_key_stats(client, key: str) -> dict:
    try:
        encoding = client.object("encoding", key)
    except Exception:
        encoding = None
    memory = None
    try:
        memory = client.memory_usage(key)
    except Exception:
        memory = None
    return {"key": key, "encoding": encoding,
            "encoding_is_bytes": isinstance(encoding, bytes),
            "hlen": client.hlen(key), "memory_usage_bytes": memory}


# ----------------------------------------------------------------------
# 载荷构造
# ----------------------------------------------------------------------

def build_calls(points: int, frames: int, base_ts: int, *, latest_state: str) -> list[dict]:
    """构造与生产同形的三份载荷。

    关键：预置的 latest 时间戳必须相对**整段帧范围**统一取新或取旧。
    第一版按每帧重算 stored_ts，结果"skip"档只在前半段跳过、后半段照样写，
    把"半段写入"当成了"不写入"——诊断场景本身必须先自证（见 run_* 里的 op 数断言）。

    latest_state:
      * "no_write" → 预置 ts 比所有帧都新：补偿段只读不写（测 读取+解码+比较）
      * "write"    → 预置 ts 比所有帧都旧：每帧每个点都写（测 读取+解码+比较+编码+写入）
    """
    values = {str(point): str(point * 7 % 1000) for point in range(1, points + 1)}
    qualities = {point: 192 for point in values}
    timestamps = [base_ts + seq * 100 for seq in range(1, frames + 1)]
    if latest_state == "no_write":
        stored_ts = max(timestamps) + 10_000
    elif latest_state == "write":
        stored_ts = min(timestamps) - 10_000
    else:
        raise ValueError(f"unknown latest_state {latest_state!r}")
    stored = {point: {"v": "stale", "t": stored_ts, "q": 192} for point in values}
    calls = []
    for seq, ts in enumerate(timestamps, 1):
        fields = build_stream_fields(
            device_code="diag", device_type="diag", group_code="g",
            scan_rate_ms=100, ts=ts, config_version=1,
            values=values, qualities=qualities)
        flat = []
        for key, value in fields.items():
            flat.extend((key, value.decode() if isinstance(value, bytes) else value))
        incoming = {point: {"v": value, "t": ts, "q": 192}
                    for point, value in values.items()}
        calls.append({"seq": seq, "fields_json": json.dumps(flat),
                      "incoming_json": json.dumps(incoming),
                      "frame_json": json.dumps({"ts": ts, "values": values}),
                      "stored": stored,
                      "incoming_points": len(incoming),
                      "stream_fields": len(flat) // 2})
    return calls


def run_history_variant(client, *, name, script, calls, points, latest_state,
                        base_ts, shard_count=4) -> dict:
    token = uuid4().hex[:10]
    receipt = f"diag:receipt:v2:{token}"
    stream = stream_key("diag", "diag", "g", shard_count)
    latest = f"diag:latest:{token}"
    window = f"diag:window:{token}"
    keys = [receipt, stream, latest, window]
    digest = client.script_load(script)
    script_bytes = len(script.encode())
    timings: list[float] = []
    try:
        for call in calls:
            # latest 里每个点存的是 JSON 字符串（生产脚本用 cjson.encode），不是嵌套字段
            client.hset(latest, mapping={point: json.dumps(value)
                                         for point, value in call["stored"].items()})
            break  # 预置一次即可：后续帧时间戳递增，语义与生产一致
        before_cmd = command_snapshot(client)
        before_cpu = cpu_snapshot(client)
        persistence_before = client.info("persistence")
        started = time.perf_counter()
        points_read = points_written = 0
        for call in calls:
            t0 = time.perf_counter()
            client.evalsha(digest, 4, *keys, call["fields_json"], call["incoming_json"],
                           call["frame_json"], "diag", "g", call["seq"])
            timings.append((time.perf_counter() - t0) * 1000)
        elapsed = time.perf_counter() - started
        after_cpu = cpu_snapshot(client)
        after_cmd = command_snapshot(client)
        persistence_after = client.info("persistence")
        delta = command_delta(before_cmd, after_cmd)
        # 场景自证：期望 = latest 写入（按档位/变体）+ 回执行写入（见 expected_hset_calls）
        expected_hset = expected_hset_calls(name, latest_state, len(calls), points)
        scenario_ok = delta["hset"]["calls"] == expected_hset
        return {
            "variant": name, "script_sha256": hashlib.sha256(script.encode()).hexdigest(),
            "script_bytes": script_bytes, "transport": "evalsha",
            "points_per_frame": points, "frames": len(calls),
            "latest_state": latest_state,
            "expected_hset_calls": expected_hset,
            "measured_hset_calls": delta["hset"]["calls"],
            "scenario_selfcheck": "pass" if scenario_ok else "fail",
            "elapsed_s": round(elapsed, 6),
            "frames_per_second": round(len(calls) / elapsed, 2),
            "points_per_second": round(len(calls) * points / elapsed, 2),
            "per_call_ms": describe(timings),
            # 秒级量用 6 位小数：3 位小数会把 ~1ms 档的 0.0007s 显示成 0.0，
            # 而同一条记录里的 busy ratio 用未取整值计算，看起来自相矛盾。
            "redis_cpu_process_s": round(
                after_cpu["process_cpu_s"] - before_cpu["process_cpu_s"], 6),
            "redis_cpu_main_thread_s": (None if after_cpu["main_thread_cpu_s"] is None
                                        else round(after_cpu["main_thread_cpu_s"]
                                                   - before_cpu["main_thread_cpu_s"], 6)),
            "cpu_busy_ratio_main_thread": (None if after_cpu["main_thread_cpu_s"] is None
                                           else round((after_cpu["main_thread_cpu_s"]
                                                       - before_cpu["main_thread_cpu_s"]) / elapsed, 4)),
            "inner_command_calls": {k: v["calls"] for k, v in delta.items() if v["calls"]},
            "inner_command_usec": {k: v["usec"] for k, v in delta.items() if v["calls"]},
            "stream_len": client.xlen(stream),
            "receipt_high": client.hget(receipt, "high"),
            "latest_hlen": client.hlen(latest),
            "aof_bytes_before": persistence_before.get("aof_current_size"),
            "aof_bytes_after": persistence_after.get("aof_current_size"),
            "aof_rewrite_in_progress": persistence_after.get("aof_rewrite_in_progress"),
            "latest_key": latest_key_stats(client, latest),
            "note": "inner_command_* 的耗时已包含在脚本耗时里，不得与脚本耗时相加",
        }
    finally:
        client.delete(*keys)


def latest_decomposition_variants(script: str) -> dict[str, str]:
    """把快车道脚本按"读+比较 / 编码 / 写入"三段拆开，量出各自占比。

    这些变体**只用于测量**，都不改变存储格式，也不是可选的生产行为：
      * no_write     ：保留 HGET+decode+比较，去掉 cjson.encode 与 HSET
                       → 差值 = 编码+写入成本
      * no_compare   ：去掉 HGET+decode+比较，直接写客户端已编码好的字符串
                       → 差值 = 读取+解码+比较成本
      * preencoded   ：保留 HGET+decode+比较，写入客户端已编码好的字符串
                       → 差值 = 逐点 cjson.encode 成本
    """
    start = script.index("local changed = false")
    end = script.index("if changed then")
    tail = script[end:]
    header_read = """local changed = false
local old_values_missing = 0
for id, value in pairs(incoming) do
    local old = redis.call('HGET', KEYS[1], id)
"""
    body_compare = """    if not old or tonumber(cjson.decode(old).t or 0) < tonumber(value.t) then
        changed = true
    end
end
"""
    body_compare_write = """    if not old or tonumber(cjson.decode(old).t or 0) < tonumber(value.t) then
        redis.call('HSET', KEYS[1], id, cjson.encode(value))
        changed = true
    end
end
"""
    # preencoded：incoming 的值本身就是 JSON 字符串（客户端编码一次）
    body_preencoded = """    if not old or tonumber(cjson.decode(old).t or 0) < tonumber(value.t) then
        redis.call('HSET', KEYS[1], id, value.j)
        changed = true
    end
end
"""
    body_no_compare = """    redis.call('HSET', KEYS[1], id, value.j)
    changed = true
end
"""
    return {
        "fastlane_no_write": script[:start] + header_read + body_compare + tail,
        "fastlane_preencoded": script[:start]
                                + script[start:end].replace("cjson.encode(value)", "value.j")
                                + tail,
        "fastlane_no_compare": script[:start] + """local changed = false
for id, value in pairs(incoming) do
""" + body_no_compare + tail,
    }


# ----------------------------------------------------------------------
# 期望值（纯函数，便于离线套用在历史 JSON 上）
#
# FRAME_LUA_V2 在**顺序路径**（seq == high + 1）每帧对回执哈希 KEYS[1] 写 2 次 HSET：
# `high` 与 `gaps`（collector/storage/durable_redis_writer.py:105-106）。
# 乱序分支会写 3 次（:108-110），但本诊断的 seq 恒为 1..frames，不会走到那个分支。
# latest 哈希 KEYS[3] 的 HSET 只在 flush_latest() 内（:123），是否写入取决于
# 存储的 t 是否比来帧更旧 —— 与回执无关。
# 实测核对：3 帧 × 8 点，write 档 30 = 24(latest) + 6(回执)，no_write 档 6 = 0 + 6
# （见证据目录 receipt-hset-probe.json）。
# ----------------------------------------------------------------------

RECEIPT_HSET_PER_FRAME = 2
# 这些变体在任何档位下都不写 latest：
#   * fastlane_no_write  —— 反证用，只读不写
#   * history_no_latest  —— 消融用，移除了历史 latest 补偿段
LATEST_NEVER_WRITTEN = frozenset({"fastlane_no_write", "history_no_latest"})
# 该变体只写不读（反证用），任何档位都写满
LATEST_ALWAYS_WRITTEN = frozenset({"fastlane_no_compare"})
# 走 FRAME_LUA_V2 的变体（会产生回执 HSET）
HISTORY_VARIANTS = frozenset({"history_full", "history_no_latest", "smoke_history"})


def expected_hset_calls(variant: str, latest_state: str, frames: int, points: int) -> int:
    """该变体在该档位下应产生的 HSET 总次数 = latest 写入 + 回执写入。

    纯函数：不依赖 Redis，可直接套用在历史运行的原始字段上做离线自证。
    """
    if variant in LATEST_NEVER_WRITTEN:
        latest = 0
    elif variant in LATEST_ALWAYS_WRITTEN:
        latest = frames * points
    else:
        latest = 0 if latest_state == "no_write" else frames * points
    receipt = RECEIPT_HSET_PER_FRAME * frames if variant in HISTORY_VARIANTS else 0
    return latest + receipt


def run_fastlane_variant(client, *, name, script, calls, points, latest_state,
                         base_ts, payload_style="plain", transport="evalsha") -> dict:
    """独立快车道：每次调用发送该组的完整点集（与生产 _latest_loop 同形）。

    payload_style="plain"      → {id: {"v":..,"t":..,"q":..}}（生产现状）
    payload_style="preencoded" → {id: {"t":..,"j":"<该点的 JSON 字符串>"}}（测量用）
    """
    token = uuid4().hex[:10]
    latest = f"diag:fastlane:{token}"
    digest = client.script_load(script)
    timings: list[float] = []
    try:
        client.hset(latest, mapping={point: json.dumps(value)
                                     for point, value in calls[0]["stored"].items()})
        before_cmd = command_snapshot(client)
        before_cpu = cpu_snapshot(client)
        persistence_before = client.info("persistence")
        started = time.perf_counter()
        payload_points = 0
        payload_bytes = 0
        for call in calls:
            incoming = json.loads(call["incoming_json"])
            if payload_style == "preencoded":
                payload = json.dumps({point: {"t": value["t"], "j": json.dumps(value)}
                                      for point, value in incoming.items()},
                                     separators=(",", ":"))
            else:
                payload = json.dumps(incoming, separators=(",", ":"))
            payload_points += len(incoming)
            payload_bytes += len(payload.encode())
            t0 = time.perf_counter()
            if transport == "eval":
                client.eval(script, 1, latest, payload, "diag", "g")
            else:
                client.evalsha(digest, 1, latest, payload, "diag", "g")
            timings.append((time.perf_counter() - t0) * 1000)
        elapsed = time.perf_counter() - started
        after_cpu = cpu_snapshot(client)
        after_cmd = command_snapshot(client)
        persistence_after = client.info("persistence")
        delta = command_delta(before_cmd, after_cmd)
        # 期望值按变体区分（fastlane_no_write 恒 0、fastlane_no_compare 恒 frames×points）
        expected_hset = expected_hset_calls(name, latest_state, len(calls), points)
        return {
            "variant": name, "script_sha256": hashlib.sha256(script.encode()).hexdigest(),
            "script_bytes": len(script.encode()), "transport": transport,
            "points_per_call": points, "calls": len(calls),
            "points_sent_total": payload_points,
            "payload_bytes_total": payload_bytes,
            "payload_style": payload_style,
            "latest_state": latest_state,
            "expected_hset_calls": expected_hset,
            "measured_hset_calls": delta["hset"]["calls"],
            "scenario_selfcheck": ("pass" if delta["hset"]["calls"] == expected_hset
                                   else "fail"),
            "elapsed_s": round(elapsed, 6),
            "calls_per_second": round(len(calls) / elapsed, 2),
            "points_per_second": round(payload_points / elapsed, 2),
            "per_call_ms": describe(timings),
            "redis_cpu_process_s": round(
                after_cpu["process_cpu_s"] - before_cpu["process_cpu_s"], 6),
            "redis_cpu_main_thread_s": (None if after_cpu["main_thread_cpu_s"] is None
                                        else round(after_cpu["main_thread_cpu_s"]
                                                   - before_cpu["main_thread_cpu_s"], 6)),
            "cpu_busy_ratio_main_thread": (None if after_cpu["main_thread_cpu_s"] is None
                                           else round((after_cpu["main_thread_cpu_s"]
                                                       - before_cpu["main_thread_cpu_s"]) / elapsed, 4)),
            "inner_command_calls": {k: v["calls"] for k, v in delta.items() if v["calls"]},
            "inner_command_usec": {k: v["usec"] for k, v in delta.items() if v["calls"]},
            "latest_hlen": client.hlen(latest),
            "aof_bytes_before": persistence_before.get("aof_current_size"),
            "aof_bytes_after": persistence_after.get("aof_current_size"),
            "latest_key": latest_key_stats(client, latest),
            "note": "inner_command_* 的耗时已包含在脚本耗时里，不得与脚本耗时相加",
        }
    finally:
        client.delete(latest)


def encoding_probe(client, *, field_counts, output_dir: Path) -> list[dict]:
    """A3：实际 latest 键在不同字段数下的编码/内存/查找成本。"""
    config = client.config_get("hash-max-listpack-entries", "hash-max-listpack-value")
    results = []
    for count in field_counts:
        token = uuid4().hex[:8]
        key = f"diag:encoding:{token}:{count}"
        value = json.dumps({"v": "12345.678", "t": int(time.time() * 1000), "q": 192})
        try:
            client.hset(key, mapping={f"p{index}": value for index in range(count)})
            stats = latest_key_stats(client, key)
            ids = [f"p{index}" for index in range(0, count, max(1, count // 50))]
            started = time.perf_counter()
            for _ in range(20):
                client.hmget(key, ids)
            lookup_ms = (time.perf_counter() - started) * 1000 / 20
            results.append({**stats, "field_count": count,
                            "value_bytes": len(value),
                            "hmget_50_keys_ms": round(lookup_ms, 4),
                            "hash_max_listpack_entries": config.get("hash-max-listpack-entries"),
                            "hash_max_listpack_value": config.get("hash-max-listpack-value")})
        finally:
            client.delete(key)
    (output_dir / "encoding-probe.json").write_text(
        json.dumps({"probe": results}, ensure_ascii=False, indent=2), encoding="utf-8")
    return results


def provenance() -> dict:
    files = ["collector/storage/durable_redis_writer.py",
             "data_watch_shared/stream_envelope.py", "scripts/diagnose_latest_duplication.py"]
    return {"git_head": _git_head(),
            "file_sha256": {name: hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
                            for name in files},
            "python": sys.version.split()[0],
            "gil_enabled": sys._is_gil_enabled(),
            "platform": platform.platform()}


def _git_head():
    try:
        import subprocess
        return subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True,
                              text=True, timeout=10).stdout.strip() or None
    except Exception:
        return None


def main() -> int:
    parser = argparse.ArgumentParser(description="latest 重复处理成本诊断（隔离环境）")
    parser.add_argument("--redis-host", default="perf-redis")
    parser.add_argument("--redis-db", type=int, default=14)
    parser.add_argument("--points", type=int, default=500)
    parser.add_argument("--frames", type=int, default=200)
    parser.add_argument("--output", required=True)
    parser.add_argument("--encoding-fields", default="200,500,513,1000")
    parser.add_argument("--allow-host", action="store_true")
    args = parser.parse_args()

    if args.redis_host not in ALLOWED_HOSTS:
        raise ValueError("only dedicated performance Redis hosts are allowed")
    if sys._is_gil_enabled() and not args.allow_host:
        raise RuntimeError("run in the free-threaded test container (or pass --allow-host)")

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    client = redis.Redis(host=args.redis_host, port=6379, db=args.redis_db,
                         decode_responses=True, socket_timeout=30)
    if not client.ping():
        raise RuntimeError("isolated Redis not reachable")

    base_ts = int(time.time() * 1000)
    report = {"provenance": provenance(),
              "redis": {"host": args.redis_host, "db": args.redis_db,
                        "version": client.info("server")["redis_version"],
                        "config": {name: client.config_get(name).get(name) for name in
                                   ("appendfsync", "appendonly", "maxmemory",
                                    "maxmemory-policy", "hash-max-listpack-entries",
                                    "hash-max-listpack-value")}},
              "parameters": {"points": args.points, "frames": args.frames},
              "diagnostic_only": ["history_no_latest"],
              "runs": [], "comparisons": {}}

    history_no_latest = history_without_latest_compensation(FRAME_LUA_V2)
    fastlane_hmget = latest_with_chunked_hmget(LATEST_LUA, 256)
    decomposition = latest_decomposition_variants(LATEST_LUA)

    # 冒烟：小规模先确认工具有效（帧数与键都很少）
    smoke_calls = build_calls(8, 3, base_ts, latest_state="write")
    smoke = run_history_variant(client, name="smoke_history", script=FRAME_LUA_V2,
                                calls=smoke_calls, points=8, latest_state="write",
                                base_ts=base_ts)
    report["smoke"] = {"history": smoke,
                       "assertions": {"stream_len": smoke["stream_len"] == 3,
                                      "receipt_high": smoke["receipt_high"] == "3",
                                      "latest_hlen": smoke["latest_hlen"] == 8}}

    for latest_state in ("no_write", "write"):
        calls = build_calls(args.points, args.frames, base_ts, latest_state=latest_state)
        for name, script in (("history_full", FRAME_LUA_V2),
                             ("history_no_latest", history_no_latest)):
            report["runs"].append(run_history_variant(
                client, name=name, script=script, calls=calls, points=args.points,
                latest_state=latest_state, base_ts=base_ts))
            # 反序再跑一次，减少顺序/缓存影响
        for name, script in (("history_no_latest", history_no_latest),
                             ("history_full", FRAME_LUA_V2)):
            report["runs"].append(run_history_variant(
                client, name=name, script=script, calls=calls, points=args.points,
                latest_state=latest_state, base_ts=base_ts))
        for name, script in (("fastlane_full", LATEST_LUA),
                             ("fastlane_hmget256", fastlane_hmget),
                             ("fastlane_full", LATEST_LUA),
                             ("fastlane_hmget256", fastlane_hmget)):
            report["runs"].append(run_fastlane_variant(
                client, name=name, script=script, calls=calls, points=args.points,
                latest_state=latest_state, base_ts=base_ts))
        # 成本拆解：读+比较 / 编码 / 写入 三段，外加 eval(带源码) vs evalsha 的传输成本
        for name, script, style, transport in (
                ("fastlane_no_write", decomposition["fastlane_no_write"], "plain", "evalsha"),
                ("fastlane_preencoded", decomposition["fastlane_preencoded"], "preencoded", "evalsha"),
                ("fastlane_no_compare", decomposition["fastlane_no_compare"], "preencoded", "evalsha"),
                ("fastlane_full_eval", LATEST_LUA, "plain", "eval"),
                ("fastlane_full", LATEST_LUA, "plain", "evalsha")):
            report["runs"].append(run_fastlane_variant(
                client, name=name, script=script, calls=calls, points=args.points,
                latest_state=latest_state, base_ts=base_ts, payload_style=style,
                transport=transport))

    report["encoding_probe"] = encoding_probe(
        client, field_counts=[int(x) for x in args.encoding_fields.split(",")],
        output_dir=output.parent)

    def mean_of(variant, state, field="per_call_ms"):
        values = [run[field]["mean_ms"] for run in report["runs"]
                  if run["variant"] == variant and run["latest_state"] == state
                  and run.get(field, {}).get("mean_ms") is not None]
        return round(statistics.mean(values), 3) if values else None

    for state in ("no_write", "write"):
        full = mean_of("history_full", state)
        skip = mean_of("history_no_latest", state)
        lane = [run["per_call_ms"]["mean_ms"] for run in report["runs"]
                if run["variant"] == "fastlane_full" and run["latest_state"] == state]
        lane_hm = [run["per_call_ms"]["mean_ms"] for run in report["runs"]
                   if run["variant"] == "fastlane_hmget256" and run["latest_state"] == state]
        report["comparisons"][state] = {
            "history_full_mean_ms": full, "history_no_latest_mean_ms": skip,
            "history_latest_segment_mean_ms": (None if full is None or skip is None
                                               else round(full - skip, 3)),
            "fastlane_full_mean_ms": round(statistics.mean(lane), 3) if lane else None,
            "fastlane_hmget256_mean_ms": round(statistics.mean(lane_hm), 3) if lane_hm else None,
            "fastlane_hmget_saving_ms": (None if not lane or not lane_hm
                                         else round(statistics.mean(lane) - statistics.mean(lane_hm), 3)),
        }

    output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "runs"},
                     ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
