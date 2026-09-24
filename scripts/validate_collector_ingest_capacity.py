"""Collector 接收/回放独立容量验证（可复现基线）。

本脚本取代 `validate_collector_free_thread.py` 的判定部分。原脚本的
`target_met` 只描述“输入是否按时提交完成”，不能说明容量达标；本脚本把

    输入按时完成 / 持久接收完成 / 持续回放能力 / 稳态 / 数据正确性

分开各自给出 pass | fail | not_tested，并且：

* 逐窗（连续时间窗口）检查输入速率、回放确认速率、outbox 条数与最老年龄，
  同时给出绝对上限判定与“是否持续增长”的趋势判定；
* 在额定输入仍在进行时就观察回放确认速率，而不是只看停采后的排空；
* 另测“保持额定输入时旧积压是否持续减少”（catch-up 余量档）；
* 测试数据由 (device, point, tick) 共同决定，并对抽样帧做全字段对账。

用途仅为隔离合成负载；不含真实 PLC、TDengine、Writer 或浏览器。

默认不清理 Redis/SQLite（保留原始证据）。确认需要重复运行时显式加 `--cleanup`。
"""
from __future__ import annotations

import argparse
import json
import logging
import os
import sqlite3
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from types import SimpleNamespace
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector")]

import redis
from data_watch_shared.stream_envelope import stream_key
from storage.durable_redis_writer import DurableRedisWriter

# 与 collector/core/quality_codes.py、writer/quality_codes.py 同值。
Q_GOOD = 0
Q_UNCERTAIN = 2
Q_BAD = 5

PASS, FAIL, NOT_TESTED = "pass", "fail", "not_tested"

# 固定点位的特殊语义，用于覆盖 NULL / 布尔 / 负值 / 非存储 / STRING / 多采样周期。
P_NULL = 1          # 值为 None：只进 latest（v="")，不进 stream
P_BOOL = 2          # bool：必须序列化成 "1"/"0"，不能是 "True"/"False"
P_NEGATIVE = 3      # 负值
P_QUALITY_VARY = 4  # 每个 tick 变化的质量码（GOOD / UNCERTAIN / BAD）
P_NON_STORAGE = 6   # is_storage=0：只进 latest 与 window，不进 stream
P_STRING_TYPE = 7   # 数据类型 STRING：只进 latest 与 window，不进 stream
FIXED_SPECIAL = (P_NULL, P_BOOL, P_NEGATIVE, P_QUALITY_VARY, P_NON_STORAGE, P_STRING_TYPE)


def save_report(path: Path, report: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as output:
        json.dump(report, output, ensure_ascii=False, indent=2)
        output.write("\n")
        output.flush()
        os.fsync(output.fileno())


# ----------------------------------------------------------------------
# 测试数据：由 (device, point, tick) 共同决定
# ----------------------------------------------------------------------

def _device_seed(device: str) -> int:
    """设备号 → 稳定的小整数，仅用于让不同设备的数值互相区分。"""
    return int(device[-3:])


def expected_stream_value(device: str, point: int, tick: int) -> str | None:
    """stream 中该点位应有的字符串值；None 表示该点不进 stream。"""
    if point in (P_NULL, P_NON_STORAGE, P_STRING_TYPE):
        return None
    if point == P_BOOL:
        return "1" if (tick + _device_seed(device)) % 2 else "0"
    if point == P_NEGATIVE:
        return str(-(point * 1000 + tick))
    return str(point + tick + _device_seed(device) * 7)


def expected_latest_value(device: str, point: int, tick: int) -> str:
    """latest 中该点位应有的字符串值（NULL 点为 ""，非存储点同样写入 latest）。"""
    if point == P_NULL:
        return ""
    if point == P_BOOL:
        return "1" if (tick + _device_seed(device)) % 2 else "0"
    if point == P_NEGATIVE:
        return str(-(point * 1000 + tick))
    return str(point + tick + _device_seed(device) * 7)


def expected_quality(point: int, tick: int) -> int:
    """该点位在该 tick 的质量码；覆盖 GOOD→变化→恢复 GOOD，以及非法点。"""
    if point == P_QUALITY_VARY:
        return (Q_GOOD, Q_UNCERTAIN, Q_BAD)[tick % 3]
    if point == P_QUALITY_VARY + 1:  # 第 5 点：一次坏值后恢复，验证质量码往返
        if tick % 20 == 7:
            return Q_BAD
        return Q_GOOD
    return Q_GOOD


def sample_for(device: str, point: int, tick: int, period_ms: int, base_ts: int) -> dict:
    """构造与 executor._write_to_redis 同形的 frame / stream / latest 三元组。

    注意：本脚本直接构造 writer 入参，未经 coletor.executor 的分组与过滤代码；
    executor 自身的过滤语义由 collector/tests/test_executor.py 覆盖。
    """
    ts = base_ts + tick * period_ms
    value = expected_latest_value(device, point, tick)
    quality = expected_quality(point, tick)
    return {"value": value, "quality": quality, "ts": ts}


def build_frame(device: str, points: int, tick: int, period_ms: int, base_ts: int) -> dict:
    ts = base_ts + tick * period_ms
    window_values = {str(p): expected_latest_value(device, p, tick) for p in range(1, points + 1)}
    stream_values: dict[str, str] = {}
    stream_qualities: dict[str, int] = {}
    for p in range(1, points + 1):
        value = expected_stream_value(device, p, tick)
        if value is None:
            continue
        stream_values[str(p)] = value
        stream_qualities[str(p)] = expected_quality(p, tick)
    latest = {
        str(p): {"v": expected_latest_value(device, p, tick), "t": ts,
                 "q": expected_quality(p, tick)}
        for p in range(1, points + 1)
    }
    return {"ts": ts, "window_values": window_values,
            "stream_values": stream_values, "stream_qualities": stream_qualities,
            "latest": latest}


# ----------------------------------------------------------------------
# 回放确认计数
#
# `stream_points_written` 由 durable_redis_writer.write_durable_frames 累加，
# 口径是“已回放到 Redis 的点值数”，因此帧数 = 计数 / 每帧点值数。
# 它不包含本地接收；本地持久接收用 outbox_committed_frames。
# ----------------------------------------------------------------------

def replayed_frames(metrics: dict, points_per_frame: int) -> int:
    return metrics["counters"].get("stream_points_written", 0) // max(1, points_per_frame)


def committed_frames(metrics: dict) -> int:
    """本地持久接收（SQLite 已提交）的帧数。"""
    return metrics["counters"].get("durable_frames_accepted", 0)


def command_stats(r) -> dict:
    """Redis 侧命令计数与耗时。EVAL/EVALSHA 的 usec 是 Lua 执行 CPU，不含 WAITAOF 阻塞等待。"""
    info = r.info("commandstats")
    return {name: {field: info.get("cmdstat_" + name, {}).get(field, 0)
                   for field in ("calls", "usec")}
            for name in ("eval", "evalsha", "waitaof", "xadd", "hset", "hmget", "publish")}


# ----------------------------------------------------------------------
# 数据正确性对账
# ----------------------------------------------------------------------

def enumerate_frames(r, keys) -> list[tuple[str, str, dict]]:
    frames = []
    for key in keys:
        minimum = "-"
        while True:
            batch = r.xrange(key, min=minimum, max="+", count=500)
            if not batch:
                break
            for entry_id, fields in batch:
                frames.append((key, entry_id, fields))
            minimum = "(" + batch[-1][0]
    return frames


def select_audit_sample(entries: list, stride: int) -> list:
    """确定性抽样：保证首帧、末帧与若干等距帧一定被完整核对。"""
    if not entries:
        return []
    indices = set(range(0, len(entries), max(1, stride)))
    indices.update((0, len(entries) - 1))
    return [entries[index] for index in sorted(indices)]


def verify_frame_fields(device: str, fields: dict, points: int, tick: int,
                        period_ms: int, base_ts: int, expected: dict) -> str | None:
    """逐字段核对一帧；返回 None 表示通过，否则返回首个差异描述。"""
    expected_ts = base_ts + tick * period_ms
    if fields.get("device_code") != device:
        return f"device_code={fields.get('device_code')!r} expected {device!r}"
    if fields.get("group_code") != expected["group"]:
        return f"group_code={fields.get('group_code')!r} expected {expected['group']!r}"
    if str(fields.get("ts")) != str(expected_ts):
        return f"ts={fields.get('ts')!r} expected {expected_ts}"
    if str(fields.get("scan_rate")) != str(period_ms):
        return f"scan_rate={fields.get('scan_rate')!r} expected {period_ms}"
    if str(fields.get("contract_version")) != "2":
        return f"contract_version={fields.get('contract_version')!r} expected '2'"

    expected_frame = build_frame(device, points, tick, period_ms, base_ts)
    try:
        values = json.loads(fields.get("values") or "{}")
        qualities = json.loads(fields.get("qualities") or "{}")
    except Exception as exc:
        return f"payload not JSON: {exc!r}"

    if values != expected_frame["stream_values"]:
        diff = _first_mapping_diff(values, expected_frame["stream_values"])
        return f"values mismatch: {diff}"
    if {str(k): int(v) for k, v in qualities.items()} != expected_frame["stream_qualities"]:
        diff = _first_mapping_diff({str(k): int(v) for k, v in qualities.items()},
                                   expected_frame["stream_qualities"])
        return f"qualities mismatch: {diff}"
    return None


def _first_mapping_diff(actual: dict, wanted: dict) -> str:
    for key in sorted(set(actual) | set(wanted), key=lambda k: (len(k), k)):
        if actual.get(key) != wanted.get(key):
            return f"point {key}: got {actual.get(key)!r} want {wanted.get(key)!r}"
    return "no key difference"


def verify_latest_full(r, device: str, points: int, tick: int, period_ms: int,
                       base_ts: int, group: str) -> str | None:
    """latest 必须包含全部点位（含 NULL、非存储、STRING），值与质量码正确。"""
    raw = r.hgetall(f"rt:latest:{device}:{group}")
    if len(raw) != points:
        return f"field count {len(raw)} expected {points}"
    expected_ts = base_ts + tick * period_ms
    for point in range(1, points + 1):
        entry = json.loads(raw.get(str(point), "{}"))
        wanted_value = expected_latest_value(device, point, tick)
        wanted_quality = expected_quality(point, tick)
        if entry.get("v") != wanted_value:
            return f"point {point} v={entry.get('v')!r} want {wanted_value!r}"
        if int(entry.get("t", -1)) != expected_ts:
            return f"point {point} t={entry.get('t')!r} want {expected_ts}"
        if int(entry.get("q", -1)) != wanted_quality:
            return f"point {point} q={entry.get('q')!r} want {wanted_quality}"
    return None


# ----------------------------------------------------------------------
# 连续窗口与趋势
# ----------------------------------------------------------------------

def window_analysis(samples: list[dict], period_ms: int, devices: int,
                    min_span_s: float = 0.2) -> dict:
    """按相邻采样点构造连续窗口，给出速率、积压与趋势。

    过短的窗口会把采样抖动放大成天文数字速率，直接丢弃而不参与判定。
    """
    windows = []
    for previous, current in zip(samples, samples[1:]):
        span = current["elapsed_s"] - previous["elapsed_s"]
        if span < min_span_s:
            continue
        outbox = current["outbox"]
        previous_outbox = previous["outbox"]
        windows.append({
            "from_s": previous["elapsed_s"],
            "to_s": current["elapsed_s"],
            "input_fps": round((current["submitted"] - previous["submitted"]) / span, 2),
            "replay_confirmed_fps": round(
                (current["replayed_frames"] - previous["replayed_frames"]) / span, 2),
            "outbox_frames": outbox["count"],
            "outbox_oldest_age_ms": outbox["oldest_age_ms"],
            "outbox_delta": outbox["count"] - previous_outbox["count"],
        })
    if not windows:
        return {"windows": [], "trend": "not_tested"}
    half = len(windows) // 2
    if half == 0:
        trend = "insufficient_windows"
    else:
        first_avg = sum(window["outbox_frames"] for window in windows[:half]) / half
        last_avg = sum(window["outbox_frames"] for window in windows[half:]) / (len(windows) - half)
        if last_avg > first_avg * 1.05 and last_avg - first_avg > devices:
            trend = "growing"
        elif last_avg < first_avg * 0.95:
            trend = "shrinking"
        else:
            trend = "flat"
    return {
        "windows": windows,
        "trend": trend,
        "max_outbox_frames": max(window["outbox_frames"] for window in windows),
        "max_outbox_oldest_age_ms": max(window["outbox_oldest_age_ms"] for window in windows),
        "max_input_fps": max(window["input_fps"] for window in windows),
        "min_input_fps": min(window["input_fps"] for window in windows),
        "max_replay_confirmed_fps": max(window["replay_confirmed_fps"] for window in windows),
    }


def build_verdicts(*, target_frames: int, submitted: int, production_s: float,
                   target_seconds: float, committed: int, replayed_during_production: int,
                   windows: dict, drain_s: float, final_outbox: dict,
                   correctness_problems: list[str], target_input_fps: float,
                   catchup: dict | None) -> dict:
    verdicts: dict[str, dict] = {}

    on_schedule = submitted >= target_frames and production_s <= target_seconds + 0.25
    verdicts["input_on_schedule"] = {
        "status": PASS if on_schedule else FAIL,
        "detail": (f"提交 {submitted}/{target_frames} 帧，用时 {production_s:.3f}s，"
                   f"目标 {target_seconds}s"),
    }

    durable_ok = committed >= submitted
    verdicts["durable_received"] = {
        "status": PASS if durable_ok else FAIL,
        "detail": f"SQLite 持久提交 {committed} 帧，输入 {submitted} 帧",
    }

    # 持续回放：必须在额定输入仍在进行时就跟得上，才算持续能力。
    if target_input_fps <= 0:
        status, detail = NOT_TESTED, "未提供目标输入速率"
    else:
        actual_fps = replayed_during_production / max(production_s, 1e-9)
        ratio = actual_fps / target_input_fps
        status = PASS if ratio >= 1.0 else FAIL
        detail = (f"生产期内回放确认 {replayed_during_production} 帧，"
                  f"约 {actual_fps:.2f} 帧/秒；目标输入 {target_input_fps:.2f} 帧/秒"
                  f"（比值 {ratio:.3f}）")
    verdicts["sustained_replay"] = {"status": status, "detail": detail}

    if not windows.get("windows"):
        verdicts["steady_state"] = {"status": NOT_TESTED, "detail": "没有连续窗口样本"}
    else:
        bounded = (windows["trend"] != "growing"
                   and final_outbox["count"] == 0
                   and final_outbox["oldest_age_ms"] == 0)
        status = PASS if bounded else FAIL
        verdicts["steady_state"] = {
            "status": status,
            "detail": (f"积压趋势={windows['trend']}，窗口内最大积压 "
                       f"{windows['max_outbox_frames']} 帧 / 最老 "
                       f"{windows['max_outbox_oldest_age_ms']}ms，"
                       f"停采后 outbox={final_outbox['count']}，排空 {drain_s:.3f}s"),
        }

    verdicts["data_correctness"] = {
        "status": PASS if not correctness_problems else FAIL,
        "detail": ("全部检查通过" if not correctness_problems
                   else f"{len(correctness_problems)} 项不符：" + "; ".join(correctness_problems[:5])),
    }

    if catchup is None:
        verdicts["catchup_margin"] = {
            "status": NOT_TESTED,
            "detail": "本轮未测“保持额定输入时旧积压是否持续减少”（需 --catchup-seconds）",
        }
    else:
        drained = catchup["outbox_end"] < catchup["outbox_start"]
        status = PASS if (drained and catchup["input_fps"] >= target_input_fps) else FAIL
        verdicts["catchup_margin"] = {
            "status": status,
            "detail": (f"保持 {catchup['input_fps']:.2f} 帧/秒输入 {catchup['seconds']}s，"
                       f"积压 {catchup['outbox_start']} → {catchup['outbox_end']} 帧"
                       f"（最老 {catchup['oldest_age_start_ms']} → "
                       f"{catchup['oldest_age_end_ms']}ms）"),
        }
    return verdicts


# ----------------------------------------------------------------------
# 主流程
# ----------------------------------------------------------------------

def run(args) -> dict:
    if sys._is_gil_enabled():
        raise RuntimeError("Python 3.14t GIL was enabled")
    if not Path("/workspace/collector").is_dir() and not args.allow_host:
        raise RuntimeError(
            "run only in the isolated collector test container, "
            "or pass --allow-host to acknowledge a host run")
    if args.redis_host not in {"perf-redis", "perf-redis-profile"}:
        raise ValueError("only dedicated performance Redis hosts are allowed")
    if args.profile_phases and args.redis_host != "perf-redis-profile":
        raise ValueError("phase profiling requires the dedicated Redis instance")
    if not (1 <= args.devices <= 100 and 8 <= args.points <= 1000
            and 1 <= args.seconds <= 600 and args.period_ms >= 100
            and 10 <= args.drain_seconds <= 600):
        raise ValueError("load limits exceeded")

    token = uuid4().hex[:10]
    device_type = "perfcol" + token
    group = "gft"
    devices = [f"pc{token}{index:03d}" for index in range(args.devices)]
    keys = sorted({stream_key(device, device_type, group, 4) for device in devices})
    db_path = Path(args.outbox_dir) / f"collector-cap-{token}.sqlite"
    r = redis.Redis(host=args.redis_host, port=6379, db=14, decode_responses=True,
                    socket_timeout=15, socket_connect_timeout=3)
    if not r.ping():
        raise RuntimeError("isolated Redis is not reachable")
    if int(r.info("persistence").get("aof_enabled", 0)) != 1:
        raise RuntimeError("isolated Redis with AOF is required")
    maxmemory = int(r.config_get("maxmemory")["maxmemory"])
    if maxmemory < 256 * 1024 * 1024:
        raise RuntimeError("isolated Redis memory limit unexpectedly low")
    memory_guard = min(int(args.redis_memory_guard_mb) * 1024 * 1024, int(maxmemory * .8))

    collector_class = ProfiledDurableRedisWriter if args.profile_phases else DurableRedisWriter
    collector = collector_class(
        r, SimpleNamespace(redis=SimpleNamespace(
            stream_shard_count=4, stream_maxlen=0, stream_warn_entries=50000)),
        outbox_path=db_path,
    )
    session_id = collector.outbox._session_id
    receipt_key = "collector:receipt:v2:" + session_id
    target_input_fps = args.devices * 1000.0 / args.period_ms
    report = {
        "mode": "synthetic_python314t_real_sqlite_redis_aof_no_tdengine",
        "verdict_model": "input_on_schedule|durable_received|sustained_replay|"
                         "steady_state|data_correctness|catchup_margin",
        "token": token, "device_type": device_type, "redis_db": 14,
        "redis_host": args.redis_host, "profile_phases": args.profile_phases,
        "python_gil_enabled": sys._is_gil_enabled(),
        "devices": args.devices, "points_per_frame": args.points,
        "period_ms": args.period_ms, "target_seconds": args.seconds,
        "target_input_frames_per_second": round(target_input_fps, 2),
        "target_frames": args.devices * (args.seconds * 1000 // args.period_ms),
        "container_cpu_limit": args.cpu_limit,
        "redis_memory_guard_bytes": memory_guard,
        "outbox_path": str(db_path), "receipt_key": receipt_key,
        "audit_stride": args.audit_stride,
        "notes": [
            "输入由本脚本直接构造并调用 DurableRedisWriter，未经 collector.executor 的分组/过滤代码。",
            "stream_points_written 的口径是已回放到 Redis 的点值数，不含本地接收。",
            "不包含真实 PLC、TDengine、Writer 或浏览器。",
        ],
    }
    collector.ping()
    base_ts = int(time.time() * 1000)
    stats_before = command_stats(r)
    ticks = 0
    submitted = 0
    samples: list[dict] = []
    stop_reason = "duration"
    production_started = time.monotonic()

    def submit(device: str, tick: int) -> None:
        frame = build_frame(device, args.points, tick, args.period_ms, base_ts)
        collector.write_window_stream(
            device, device_type, group, args.period_ms, frame["ts"], 1,
            {"ts": frame["ts"], "values": frame["window_values"]},
            frame["stream_values"], frame["stream_qualities"])
        collector.write_latest(device, group, frame["latest"])

    def snapshot(label: str) -> dict:
        metrics = collector._metrics.snapshot()
        return {
            "label": label,
            "elapsed_s": round(time.monotonic() - production_started, 3),
            "submitted": submitted,
            "committed_frames": committed_frames(metrics),
            "replayed_frames": replayed_frames(metrics, args.points),
            "outbox": collector.outbox.stats(),
            "rss_bytes": metrics["runtime"]["rss_bytes"],
            "redis_used_memory_bytes": int(r.info("memory")["used_memory"]),
        }

    try:
        with ThreadPoolExecutor(max_workers=min(32, args.devices)) as pool:
            while time.monotonic() - production_started < args.seconds:
                tick = ticks
                for future in [pool.submit(submit, device, tick) for device in devices]:
                    future.result()
                ticks += 1
                submitted += args.devices
                if ticks % args.sample_every_ticks == 0:
                    current = snapshot("production")
                    samples.append(current)
                    if current["redis_used_memory_bytes"] >= memory_guard:
                        stop_reason = "redis_memory_guard"
                        break
                next_tick = production_started + ticks * args.period_ms / 1000
                time.sleep(max(0, next_tick - time.monotonic()))
        production_end = time.monotonic()
        production_snapshot = snapshot("production_end")
        production_snapshot["elapsed_s"] = round(production_end - production_started, 3)
        samples.append(production_snapshot)

        catchup = None
        if args.catchup_seconds > 0:
            catchup_start = snapshot("catchup_start")
            catchup_started = time.monotonic()
            catchup_ticks = 0
            while time.monotonic() - catchup_started < args.catchup_seconds:
                current_tick = ticks
                for future in [pool.submit(submit, device, current_tick) for device in devices]:
                    future.result()
                ticks += 1
                catchup_ticks += 1
                submitted += args.devices
                if catchup_ticks % args.sample_every_ticks == 0:
                    samples.append(snapshot("catchup"))
                next_tick = catchup_started + catchup_ticks * args.period_ms / 1000
                time.sleep(max(0, next_tick - time.monotonic()))
            catchup_end = snapshot("catchup_end")
            catchup = {
                "seconds": round(time.monotonic() - catchup_started, 3),
                "input_fps": round(catchup_ticks * args.devices
                                   / max(time.monotonic() - catchup_started, 1e-9), 2),
                "outbox_start": catchup_start["outbox"]["count"],
                "outbox_end": catchup_end["outbox"]["count"],
                "oldest_age_start_ms": catchup_start["outbox"]["oldest_age_ms"],
                "oldest_age_end_ms": catchup_end["outbox"]["oldest_age_ms"],
            }

        drain_started = time.monotonic()
        while time.monotonic() - drain_started < args.drain_seconds:
            outbox = collector.outbox.stats()
            with collector._latest_condition:
                latest_pending = len(collector._latest_pending)
            if outbox["count"] == 0 and outbox["waiting_writes"] == 0 and latest_pending == 0:
                break
            time.sleep(.2)
        drain_s = time.monotonic() - drain_started
        stats_after = command_stats(r)
        final_snapshot = snapshot("drained")
        final_outbox = collector.outbox.stats()
        with collector._latest_condition:
            final_latest_pending = len(collector._latest_pending)
        collector.close()
        with sqlite3.connect(db_path) as db:
            sqlite_rows = sum(db.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
                              for table in ("collector_retry_outbox", "collector_retry_outbox_v2"))

        # ---- 数据正确性对账 ----
        problems: list[str] = []
        entries = enumerate_frames(r, keys)
        expected_total = ticks * args.devices
        if len(entries) != expected_total:
            problems.append(f"stream 帧数 {len(entries)} != 期望 {expected_total}")

        identities: dict[tuple, int] = {}
        tick_of_ts: dict[int, int] = {}
        for _, _, fields in entries:
            key = (fields.get("device_code"), int(fields.get("ts", -1)))
            identities[key] = identities.get(key, 0) + 1
        duplicates = sum(count - 1 for count in identities.values() if count > 1)
        if duplicates:
            problems.append(f"重复帧 {duplicates}")
        for tick in range(ticks):
            tick_of_ts[base_ts + tick * args.period_ms] = tick

        audited = 0
        for stream_key_value, entry_id, fields in select_audit_sample(entries, args.audit_stride):
            tick = tick_of_ts.get(int(fields.get("ts", -1)))
            if tick is None:
                problems.append(f"帧 {entry_id} ts={fields.get('ts')!r} 不属于本次运行")
                continue
            problem = verify_frame_fields(fields.get("device_code"), fields, args.points, tick,
                                          args.period_ms, base_ts,
                                          {"group": group, "device_type": device_type})
            if problem:
                problems.append(f"{stream_key_value} {entry_id}: {problem}")
            audited += 1

        latest_problems = []
        for device in devices[:args.latest_devices]:
            problem = verify_latest_full(r, device, args.points, ticks - 1, args.period_ms,
                                         base_ts, group)
            if problem:
                latest_problems.append(f"{device}: {problem}")
        problems.extend(latest_problems)

        if final_outbox["count"] != 0 or final_outbox["waiting_writes"] != 0:
            problems.append(f"排空后 outbox 非空：{final_outbox}")
        if sqlite_rows != 0:
            problems.append(f"排空后 SQLite 仍有 {sqlite_rows} 行")

        replayed_during_production = production_snapshot["replayed_frames"]
        committed_total = final_snapshot["committed_frames"]
        windows = window_analysis(samples, args.period_ms, args.devices)
        verdicts = build_verdicts(
            target_frames=report["target_frames"], submitted=submitted,
            production_s=production_end - production_started, target_seconds=args.seconds,
            committed=committed_total, replayed_during_production=replayed_during_production,
            windows=windows, drain_s=drain_s, final_outbox=final_outbox,
            correctness_problems=problems, target_input_fps=target_input_fps,
            catchup=catchup)

        report.update({
            "verdicts": verdicts,
            "overall_passed": all(value["status"] == PASS for value in verdicts.values()),
            "stop_reason": stop_reason, "ticks": ticks, "submitted_frames": submitted,
            "production_seconds": round(production_end - production_started, 3),
            "receive_frames_per_second": round(submitted / max(production_end - production_started, 1e-9), 2),
            "committed_frames_total": committed_total,
            "replayed_frames_total": final_snapshot["replayed_frames"],
            "replayed_frames_during_production": replayed_during_production,
            "replayed_fps_during_production": round(
                replayed_during_production / max(production_end - production_started, 1e-9), 2),
            "replayed_fps_after_production": round(
                (final_snapshot["replayed_frames"] - replayed_during_production)
                / max(drain_s, 1e-9), 2),
            "drain_seconds": round(drain_s, 3),
            "audited_frames": audited,
            "total_frames_in_stream": len(entries),
            "duplicate_frames": duplicates,
            "correctness_problems": problems,
            "windows": windows,
            "catchup": catchup,
            "final_outbox": final_outbox, "sqlite_rows_after_close": sqlite_rows,
            "final_latest_pending": final_latest_pending,
            "max_rss_bytes": max(sample["rss_bytes"] for sample in samples),
            "max_redis_used_memory_bytes": max(sample["redis_used_memory_bytes"] for sample in samples),
            "commandstats_delta": {
                name: {field: stats_after[name][field] - stats_before[name][field]
                       for field in ("calls", "usec")}
                for name in stats_before},
            "timings_ms": {
                name: collector._metrics.snapshot()["timings"].get(name)
                for name in ("outbox_accept_ms", "outbox_commit_ms",
                             "redis_aof_batch_ms", "latest_fastlane_redis_ms")},
            "profile_windows": collector.profile_windows if args.profile_phases else [],
            "samples": samples,
        })
        save_report(Path(args.output), report)

        if args.cleanup and not problems:
            _cleanup_own_keys(r, keys, devices, group, args, db_path, report, receipt_key)
        elif args.cleanup_always:
            _cleanup_own_keys(r, keys, devices, group, args, db_path, report, receipt_key)
        else:
            report["cleanup"] = {
                "skipped": True,
                "reason": "evidence preserved (default)" if not args.cleanup
                          else "correctness problems present",
            }
        save_report(Path(args.output), report)
        print(json.dumps({key: value for key, value in report.items()
                          if key not in ("samples", "windows", "profile_windows")},
                         ensure_ascii=False, indent=2), flush=True)
        return report
    except Exception as exc:
        report.update({"error": repr(exc), "submitted_frames": submitted, "ticks": ticks,
                       "samples": samples})
        save_report(Path(args.output), report)
        raise
    finally:
        collector.close()
        r.close()


def _cleanup_own_keys(r, keys, devices, group, args, db_path, report, receipt_key) -> None:
    """只删除本次唯一 token 生成的键与本次自己的 SQLite 文件。"""
    own_keys = keys + [receipt_key]
    own_keys += [f"rt:latest:{device}:{group}" for device in devices]
    own_keys += [f"rt:window:{device}:{group}:{args.period_ms}" for device in devices]
    r.unlink(*own_keys)
    report["cleanup"] = {"redis_own_keys_remaining": sum(r.exists(key) for key in own_keys),
                         "redis_own_keys_deleted": len(own_keys)}
    for suffix in ("", "-wal", "-shm"):
        candidate = Path(str(db_path) + suffix)
        if candidate.exists():
            candidate.unlink()
    owner_lock = Path(str(db_path) + ".owner.lock")
    if owner_lock.exists():
        owner_lock.unlink()
    report["cleanup"]["sqlite_removed"] = not db_path.exists()


class ProfiledDurableRedisWriter(DurableRedisWriter):
    """测试专用：在同一条原生连接上把整批 EVALSHA 与末尾 WAITAOF 分段计时。

    保留生产路径“同连接写入后确认”的顺序与语义，但未复刻 NOSCRIPT 重试分支。
    """

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.profile_windows = []

    def _deliver_window(self, batch, batch_bytes):
        for script in {source for source, _ in batch}:
            if script not in self._script_shas:
                self._script_shas[script] = self.r.script_load(script)
        commands = [("EVALSHA", self._script_shas[source], *args) for source, args in batch]
        commands.append(("WAITAOF", 1, 0, 2000))
        connection = self.r.connection_pool.get_connection()
        completed = False
        try:
            started = time.perf_counter()
            packed = connection.pack_commands(commands)
            packed_at = time.perf_counter()
            connection.send_packed_command(packed)
            sent_at = time.perf_counter()
            for _ in batch:
                connection.read_response()
            eval_read_at = time.perf_counter()
            receipt = connection.read_response()
            confirmed_at = time.perf_counter()
            if not isinstance(receipt, (list, tuple)) or int(receipt[0]) < 1:
                raise RuntimeError("same-connection WAITAOF did not confirm the frame batch")
            completed = True
        finally:
            if not completed:
                connection.disconnect()
            self.r.connection_pool.release(connection)
        times = {
            "frames": len(batch), "bytes": batch_bytes,
            "pack_ms": round((packed_at - started) * 1000, 3),
            "send_ms": round((sent_at - packed_at) * 1000, 3),
            "eval_response_ms": round((eval_read_at - sent_at) * 1000, 3),
            "pre_waitaof_ms": round((eval_read_at - started) * 1000, 3),
            "waitaof_response_ms": round((confirmed_at - eval_read_at) * 1000, 3),
            "total_ms": round((confirmed_at - started) * 1000, 3),
            "waitaof_reply": receipt,
        }
        self.profile_windows.append(times)
        self._metrics.record_timing("redis_aof_batch_ms", times["total_ms"])
        self._metrics.set_gauge("redis_batch_frames", len(batch))
        self._metrics.set_gauge("redis_batch_bytes", batch_bytes)
        self._metrics.increment("redis_confirmed_batches")
        self._metrics.increment("redis_confirmed_bytes", batch_bytes)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--devices", type=int, default=100)
    parser.add_argument("--points", type=int, default=1000)
    parser.add_argument("--seconds", type=int, default=10)
    parser.add_argument("--period-ms", type=int, default=100)
    parser.add_argument("--drain-seconds", type=int, default=180)
    parser.add_argument("--catchup-seconds", type=int, default=0,
                        help="保持额定输入观察旧积压是否持续减少的时长")
    parser.add_argument("--sample-every-ticks", type=int, default=5)
    parser.add_argument("--audit-stride", type=int, default=97,
                        help="每 N 帧完整核对一帧全字段")
    parser.add_argument("--latest-devices", type=int, default=3,
                        help="全字段核对 latest 的设备数")
    parser.add_argument("--redis-memory-guard-mb", type=int, default=400)
    parser.add_argument("--cpu-limit", type=int, required=True)
    parser.add_argument("--redis-host", default="perf-redis")
    parser.add_argument("--profile-phases", action="store_true")
    parser.add_argument("--outbox-dir", default="/tmp")
    parser.add_argument("--allow-host", default=None,
                        help="在宿主机（非隔离容器）运行时必须显式声明")
    parser.add_argument("--cleanup", action="store_true",
                        help="正确性通过后删除本次唯一键；默认保留证据")
    parser.add_argument("--cleanup-always", action="store_true",
                        help="无论判定结果都删除本次唯一 token 的键（迭代基线时用，"
                             "只影响本次 token 生成的键）")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    logging.basicConfig(level=logging.WARNING)
    result = run(args)
    raise SystemExit(0 if result.get("overall_passed") else 1)
