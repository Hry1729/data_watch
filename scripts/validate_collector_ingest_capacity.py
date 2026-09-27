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

import hashlib
import redis
from data_watch_shared.acceptance import (
    FAIL,
    NOT_TESTED,
    PASS,
    batch_timeline_analysis,
    build_frame,
    correctness_verdict,
    expected_latest_value,
    expected_quality,
    not_tested,
    overall_status,
    queue_conservation,
    window_analysis,
)
from data_watch_shared.stream_envelope import stream_key
from storage.durable_redis_writer import DurableRedisWriter

PACKAGE_ROOT = Path(__file__).resolve().parent.parent


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    digest.update(Path(path).read_bytes())
    return digest.hexdigest()


def provenance() -> dict:
    """把"这份结果由哪一版代码产生"绑进结果本身。

    未提交的工作区无法用 commit 标识，所以记录脚本与共享规则模块的内容哈希：
    复核者只要重算这两个哈希，就能确认证据与实现对得上。
    """
    module = PACKAGE_ROOT / "data_watch_shared" / "acceptance.py"
    return {
        "script_sha256": file_sha256(Path(__file__).resolve()),
        "acceptance_sha256": file_sha256(module) if module.exists() else None,
        "git_head": _git_head(),
    }


def _git_head() -> str | None:
    try:
        import subprocess
        return subprocess.run(["git", "rev-parse", "HEAD"], cwd=PACKAGE_ROOT,
                              capture_output=True, text=True, timeout=10).stdout.strip() or None
    except Exception:
        return None


def _redis_cpu_summary(samples) -> dict:
    """Redis 单线程忙碌程度：CPU 秒数增量 ÷ 墙钟秒数增量。

    这是判断"回放确认慢"究竟是 Redis 执行命令慢、还是客户端等待的信号之一：
    比值接近 1 说明该 Redis 实例几乎一直在执行命令（单线程已饱和）。
    """
    points = [(sample.get("elapsed_s"), sample.get("redis_cpu_seconds"))
              for sample in samples
              if sample.get("redis_cpu_seconds") is not None]
    if len(points) < 2:
        return {"status": NOT_TESTED, "detail": "CPU 采样不足"}
    (t0, c0), (t1, c1) = points[0], points[-1]
    span = (t1 or 0) - (t0 or 0)
    if span <= 0:
        return {"status": NOT_TESTED, "detail": "CPU 采样时间跨度不足"}
    busy = ((c1 or 0) - (c0 or 0)) / span
    return {"status": PASS, "cpu_seconds_delta": round((c1 or 0) - (c0 or 0), 3),
            "wall_seconds": round(span, 3), "busy_ratio": round(busy, 4),
            "detail": (f"Redis 实例 CPU 忙碌比 {busy:.3f}（单线程执行；接近 1 表示"
                       "该实例几乎一直在执行命令，客户端等待里包含排队)")}


def save_report(path: Path, report: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8") as output:
        json.dump(report, output, ensure_ascii=False, indent=2)
        output.write("\n")
        output.flush()
        os.fsync(output.fileno())


def append_progress(path: Path, event: dict) -> None:
    """Append a small durable checkpoint without rewriting the final report."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "a", encoding="utf-8") as output:
        output.write(json.dumps(event, ensure_ascii=False) + "\n")
        output.flush()
        os.fsync(output.fileno())


# ----------------------------------------------------------------------
# 测试数据：由 (device, point, tick) 共同决定
#
# 期望数据模型与判定词表已统一到 data_watch_shared/acceptance.py：
# 完整链路脚本用的是同一套 (device, point, tick) 期望值与同一套 pass/fail/not_tested，
# 不再由各脚本各写一份"通过"口径。
# ----------------------------------------------------------------------

def sample_for(device: str, point: int, tick: int, period_ms: int, base_ts: int) -> dict:
    """构造与 executor._write_to_redis 同形的 frame / stream / latest 三元组。

    注意：本脚本直接构造 writer 入参，未经 coletor.executor 的分组与过滤代码；
    executor 自身的过滤语义由 collector/tests/test_executor.py 覆盖。
    """
    ts = base_ts + tick * period_ms
    value = expected_latest_value(device, point, tick)
    quality = expected_quality(point, tick)
    return {"value": value, "quality": quality, "ts": ts}


# ----------------------------------------------------------------------
# 回放确认计数
#
# `stream_points_written` 由 durable_redis_writer.write_durable_frames 累加，
# 口径是“已回放到 Redis 的点值数”，因此帧数 = 计数 / 每帧点值数。
# 它不包含本地接收；本地持久接收用 outbox_committed_frames。
# ----------------------------------------------------------------------

def replayed_frames(metrics: dict, points_per_frame: int) -> int:
    """历史回放**逻辑帧**数。

    必须用 durable_redis_writer 在整批 WAITAOF 确认后累加的 history_frames_confirmed，
    不能再用 stream_points_written 除以"标称每帧点数"：帧里只有 is_storage 且非 STRING
    且值非 None 的点才进 stream，标称点数与实际进历史的点集不同，换算会系统性少算
    （本脚本的合成数据固定有 3 个点不进 stream，200 点时就把 100% 显示成 98.5%）。
    points_per_frame 仅保留作调用兼容。
    """
    return metrics["counters"].get("history_frames_confirmed", 0)


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

def enumerate_frames(r, keys):
    """Read bounded Redis pages; never retain every wide frame in Python."""
    for key in keys:
        minimum = "-"
        while True:
            batch = r.xrange(key, min=minimum, max="+", count=500)
            if not batch:
                break
            for entry_id, fields in batch:
                yield key, entry_id, fields
            minimum = "(" + batch[-1][0]


def audit_stream_frames(r, keys, devices: list[str], device_type: str, group: str,
                        points: int, ticks: int, period_ms: int, base_ts: int,
                        stride: int) -> dict:
    """Check every identity and sample complete fields with bounded memory."""
    seen = {device: bytearray(ticks) for device in devices}
    total = duplicates = invalid = audited = unique = 0
    problems: list[str] = []
    last = None
    last_audited = False
    expected = {"group": group, "device_type": device_type}

    def audit(entry):
        nonlocal audited
        stream_name, entry_id, fields, device, tick = entry
        problem = verify_frame_fields(device, fields, points, tick, period_ms,
                                      base_ts, expected)
        if problem and len(problems) < 20:
            problems.append(f"{stream_name} {entry_id}: {problem}")
        audited += 1

    for stream_name, entry_id, fields in enumerate_frames(r, keys):
        total += 1
        device = fields.get("device_code")
        try:
            ts = int(fields.get("ts"))
        except (TypeError, ValueError):
            ts = None
        offset = None if ts is None else ts - base_ts
        tick = (offset // period_ms if offset is not None and offset % period_ms == 0
                else None)
        if (device not in seen or tick is None or not 0 <= tick < ticks
                or stream_name != stream_key(device, device_type, group, 4)):
            invalid += 1
            if len(problems) < 20:
                problems.append(f"{stream_name} {entry_id}: 非本次设备/时间戳/分片身份 "
                                f"device={device!r} ts={fields.get('ts')!r}")
            last = None
            continue
        if seen[device][tick]:
            duplicates += 1
        else:
            seen[device][tick] = 1
            unique += 1
        last = (stream_name, entry_id, fields, device, tick)
        last_audited = (total - 1) % max(1, stride) == 0
        if last_audited:
            audit(last)
    if last is not None and not last_audited:
        audit(last)
    missing = ticks * len(devices) - unique
    if missing:
        problems.append(f"缺失帧 {missing}")
    if duplicates:
        problems.append(f"重复帧 {duplicates}")
    if invalid:
        problems.append(f"非法身份帧 {invalid}")
    if total != ticks * len(devices):
        problems.append(f"stream 帧数 {total} != 期望 {ticks * len(devices)}")
    return {"total": total, "duplicates": duplicates, "invalid": invalid,
            "missing": missing, "audited": audited, "problems": problems}


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
#
# `window_analysis()` 已统一到 data_watch_shared/acceptance.py：
# 容量脚本与完整链路脚本必须用同一套"预热后稳态窗口 + 总增量÷总时长"口径，
# 否则同一批采样在两处会得出不同的速率与趋势。
# ----------------------------------------------------------------------


def margin_verdict(measured: dict | None, *, target_input_fps: float,
                   min_effective_seconds: float, rate_multiple: float,
                   blocked_reason: str | None) -> dict:
    """处理余量判定：必须用**额定输入**下测得的能力，不能用降速输入顶替。

    measured 的语义：
      * input_fps        该阶段实际输入帧/秒
      * confirmed_fps    该阶段实际确认帧/秒（= 处理能力，处理端全程有任务时）
      * capacity_fps_estimate = confirmed_frames / window_seconds
      * margin_fraction  = capacity / input − 1（= 净消减 ÷ 输入）
      * effective_window 积压被处理完的时刻不早于 min_effective_seconds

    判定规则：
      1. 没有测量 → not_tested（未测不等于余量为零）；
      2. 追平开始时没有可处理积压 → not_tested（测的会是正常生产尾部积压）；
      3. 输入未达额定 → fail（能力结论不成立）；
      4. 有效区间不足 → not_tested（积压被处理得太快，不能据此判无余量）；
      5. 余量 ≥ 30% → pass；否则 fail（这是额定输入下的真实测量）。
    """
    if measured is None:
        return not_tested(blocked_reason or "本轮未运行该阶段：未测不等于余量为零")
    if not measured["backlog_start_frames"]:
        return not_tested(
            "追平开始时没有可处理积压（可处理积压=本地持久队列未领取帧数）："
            "此时测到的是正常生产的尾部积压，不能当作处理余量证据")
    input_fps = measured["input_fps"]
    if input_fps < target_input_fps * rate_multiple * 0.98:
        return {
            "status": FAIL,
            "detail": (f"输入 {input_fps:.2f} 帧/秒 未达目标 "
                       f"{target_input_fps * rate_multiple:.2f} 帧/秒，"
                       "该阶段的能力结论不成立"),
        }
    capacity = measured["capacity_fps_estimate"]
    margin = measured["margin_fraction"]
    if margin >= 0.30:
        status = PASS
    elif measured["effective_window"]:
        status = FAIL
    else:
        return not_tested(
            f"积压在 {measured['backlog_exhausted_at_s']}s 内被处理完，"
            f"有效测量区间不足 {min_effective_seconds}s：区间太短，"
            "不能据此判定余量（未测不等于无余量）。可加大 --seed-backlog-frames 重测")
    return {
        "status": status,
        "detail": (f"额定输入 {input_fps:.2f} 帧/秒持续 {measured['seconds']}s："
                   f"确认 {measured['confirmed_fps']:.2f} 帧/秒，"
                   f"估算处理能力 {capacity:.2f} 帧/秒，"
                   f"额定输入下的处理余量 {margin * 100:.1f}%（目标 30%）；"
                   f"可处理积压 {measured['backlog_start_frames']} → "
                   f"{measured['backlog_end_frames']} 帧，"
                   f"已领取未确认在途估计 {measured['claimed_unconfirmed_frames_estimate']} 帧，"
                   f"最老积压 {measured['backlog_oldest_age_start_ms']} → "
                   f"{measured['backlog_oldest_age_end_ms']}ms"),
    }


def attempted_verdict_names(*, seeded: bool, rated: bool, degraded: bool) -> list[str]:
    """本次实际尝试测量的判定项。

    没跑的档位（例如额定轮次里的降速档）不该把总判定压成 not_tested，否则
    "诚实报未测"会让任何单轮运行都永远拿不到总判定；但**跑了却没测出结果**
    （例如稳态窗口不足）仍然计入并拉低总判定。两回事分开，不许互相掩盖。
    """
    names = ["input_on_schedule", "durable_received", "sustained_replay", "steady_state",
             "data_correctness", "post_stop_drain"]
    if seeded:
        names.append("seeded_backlog")
    if rated:
        names.append("rated_catchup_margin")
    if degraded:
        names.append("degraded_recovery")
    return names


def build_verdicts(*, target_frames: int, production_submitted: int, production_s: float,
                   target_seconds: float, committed: int, replayed_during_production: int,
                   windows: dict, drain_s: float, final_outbox: dict,
                   correctness_problems: list[str], target_input_fps: float,
                   seeded: dict | None, rated: dict | None, degraded: dict | None,
                   min_effective_seconds: float) -> dict:
    verdicts: dict[str, dict] = {}

    # 只比较**生产阶段**的提交量：追平阶段的帧不属于额定输入，不能算进"按时完成"。
    on_schedule = production_submitted >= target_frames and production_s <= target_seconds + 0.25
    verdicts["input_on_schedule"] = {
        "status": PASS if on_schedule else FAIL,
        "detail": (f"生产阶段提交 {production_submitted}/{target_frames} 帧，"
                   f"用时 {production_s:.3f}s，目标 {target_seconds}s"),
    }

    durable_ok = committed >= production_submitted
    verdicts["durable_received"] = {
        "status": PASS if durable_ok else FAIL,
        "detail": f"SQLite 持久提交 {committed} 帧，生产阶段输入 {production_submitted} 帧",
    }

    # 持续回放：用**预热后的稳态窗口**Δ已确认逻辑帧/Δt，与额定输入比较。
    # 不用"生产期累计确认 ÷ 生产总时长"：异步流水线在停止输入时还有正常在途尾批，
    # 那种算法会把正常的约一个批次延迟误判成容量不足。
    steady_fps = windows.get("steady_confirmed_fps")
    if target_input_fps <= 0:
        status, detail = NOT_TESTED, "未提供目标输入速率"
    elif steady_fps is None:
        status, detail = NOT_TESTED, "稳态窗口不足（样本太少或时长过短），无法给出持续确认速率"
    else:
        ratio = steady_fps / target_input_fps
        status = PASS if ratio >= 1.0 else FAIL
        detail = (f"预热 {windows.get('warmup_s', 0)}s 后稳态确认约 {steady_fps:.2f} 帧/秒"
                  f"（{windows.get('steady_windows')} 个窗口）；目标输入 "
                  f"{target_input_fps:.2f} 帧/秒（比值 {ratio:.3f}）。"
                  f"生产期累计确认 {replayed_during_production} 帧仅供参考，不单独作为容量判据")
    verdicts["sustained_replay"] = {"status": status, "detail": detail}

    # 稳态只描述**生产期预热后**的积压趋势：把"停采后能排空"混进来会把
    # 一个必然后果当成容量证据，所以排空单独成为 post_stop_drain。
    if not windows.get("windows"):
        verdicts["steady_state"] = not_tested("没有连续窗口样本")
    elif windows.get("trend") == "insufficient_windows" or not windows.get("steady_windows"):
        # 有效窗口不足时必须如实报"未测"，不能因为趋势"不叫 growing"就算通过。
        verdicts["steady_state"] = not_tested(
            f"预热后有效窗口不足（{windows.get('steady_windows')} 个），无法判定稳态；"
            f"未测不等于通过。建议延长 --seconds 或减小 --sample-every-ticks")
    else:
        growing = windows["trend"] == "growing"
        verdicts["steady_state"] = {
            "status": FAIL if growing else PASS,
            "detail": (f"生产期积压趋势={windows['trend']}，稳态窗口 "
                       f"{windows['steady_windows']} 个，窗口内最大积压 "
                       f"{windows['max_outbox_frames']} 帧 / 最老 "
                       f"{windows['max_outbox_oldest_age_ms']}ms"),
        }

    # 数据正确性用共享判定：完整链路脚本走的是同一个 correctness_verdict，
    # 不允许两处对"不符"给出不同的通过口径。
    verdicts["data_correctness"] = correctness_verdict(
        correctness_problems, checked="Collector→Redis 全字段对账")

    # 停采排空：单独命名、单独判定。停采后能排空不证明额定输入下的持续容量达标。
    drained = final_outbox["count"] == 0 and final_outbox["oldest_age_ms"] == 0
    verdicts["post_stop_drain"] = {
        "status": PASS if drained else FAIL,
        "detail": (f"停采后 outbox={final_outbox['count']}，最老 "
                   f"{final_outbox['oldest_age_ms']}ms，排空 {drain_s:.3f}s。"
                   "注：停采后能排空只是必要后果，不证明额定输入的持续容量"),
    }

    # 额定追平余量：只能用额定输入（--catchup-rate 1.0）的测量。
    if rated is not None:
        verdicts["rated_catchup_margin"] = margin_verdict(
            rated, target_input_fps=target_input_fps,
            min_effective_seconds=min_effective_seconds, rate_multiple=1.0,
            blocked_reason=None)
    elif degraded is not None:
        verdicts["rated_catchup_margin"] = not_tested(
            "本轮只在降速输入下测量（--catchup-rate < 1）：降速后的净消减比例"
            "不能顶替额定输入下的处理余量。请用 --catchup-rate 1.0 重测")
    else:
        verdicts["rated_catchup_margin"] = not_tested(
            "尚未验证额定处理余量：需 --seed-backlog-frames（先不启动回放预置有界积压）"
            "+ --catchup-seconds（额定输入与回放同时启动）。未测不等于余量为零")

    # 降速恢复：与额定余量分开命名、分开判定，绝不参与额定余量结论。
    if degraded is None:
        verdicts["degraded_recovery"] = not_tested(
            "本轮未运行降速恢复档（需 --catchup-rate < 1 且 --catchup-seconds）")
    else:
        verdicts["degraded_recovery"] = margin_verdict(
            degraded, target_input_fps=target_input_fps,
            min_effective_seconds=min_effective_seconds,
            rate_multiple=degraded["rate_multiple"],
            blocked_reason=None)
        verdicts["degraded_recovery"]["detail"] = (
            f"（降速 {degraded['rate_multiple']}× 额定，仅作恢复能力描述，"
            f"不作为额定余量） " + verdicts["degraded_recovery"]["detail"])

    # 预置积压是否真的形成：工具能跑不等于余量验收通过，这一步单独报。
    if seeded is None:
        verdicts["seeded_backlog"] = not_tested(
            "本轮未预置受控积压（需 --seed-backlog-frames）；"
            "没有受控积压就无法把可处理积压与正常生产尾部积压分开")
    else:
        # 暂停没生效 / 预置期间回放仍前进 → 预置积压数字不可信，直接判失败。
        trustworthy = (seeded["target_reached"]
                       and seeded["replay_quiescent_before_seed"]
                       and seeded["replay_paused_during_seed"])
        verdicts["seeded_backlog"] = {
            "status": PASS if trustworthy else FAIL,
            "detail": (f"暂停回放（静止确认 {seeded['replay_quiescent_before_seed']}）后"
                       f"按正常持久接收路径预置 {seeded['produced_frames']} 帧，"
                       f"可处理积压 {seeded['backlog_frames']}/{seeded['target_frames']} 帧，"
                       f"耗时 {seeded['seconds']}s，最老 {seeded['backlog_oldest_age_ms']}ms，"
                       f"本地在途写 {seeded['in_flight_local_writes']} 帧，"
                       f"预置期间回放又前进 {seeded['replay_advanced_during_seed']} 帧"),
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
    # 上限 3600s 是为"长测是否持续积压"这一档留的：600s 只能看到锯齿，
    # 不足以区分"围绕某值波动"和"持续增长"。其余上限（设备/点位/周期/排空）不变。
    if not (1 <= args.devices <= 100 and 8 <= args.points <= 1000
            and 1 <= args.seconds <= 3600 and args.period_ms >= 100
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
    # 本次运行的 Redis 有效配置（appendfsync 等单因素对照必须能从这里核对）。
    redis_config = {
        name: r.config_get(name).get(name)
        for name in ("appendfsync", "appendonly", "maxmemory", "maxmemory-policy",
                     "auto-aof-rewrite-percentage", "auto-aof-rewrite-min-size")
    }

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
        "provenance": provenance(),
        # 有效配置必须绑在结果里：appendfsync 是本次要对照的单因素，
        # AOF 重写阈值会 fork 写盘、影响长测，都不能只写在报告正文里。
        "redis_config": redis_config,
        "verdict_model": "input_on_schedule|durable_received|sustained_replay|"
                         "steady_state|data_correctness|seeded_backlog|"
                         "rated_catchup_margin|degraded_recovery|post_stop_drain",
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
            "可处理积压 = 本地持久队列里还没被回放领取的帧数（outbox.count）；"
            "已接收未提交(outbox.waiting_writes)与已领取未确认删除的帧属于在途量，"
            "单独记录，不计入积压。",
            "处理余量只在额定输入（--catchup-rate 1.0）与预置积压并存的有效区间测量；"
            "降速档单列 degraded_recovery，停采排空单列 post_stop_drain，"
            "三者不得互相顶替。",
        ],
    }
    # 生产默认行为不变：回放随 ping 启动。
    collector.ping()
    base_ts = int(time.time() * 1000)
    stats_before = command_stats(r)
    ticks = 0
    submitted = 0
    production_submitted = 0
    samples: list[dict] = []
    stop_reason = "duration"
    production_started = time.monotonic()
    phase_started = production_started
    progress_path = Path(str(args.output) + ".progress.jsonl")
    next_progress_at = production_started + 30
    append_progress(progress_path, {"token": token, "status": "started",
                                    "report": report, "time_ms": int(time.time() * 1000)})

    def checkpoint(sample: dict, *, force: bool = False) -> None:
        nonlocal next_progress_at
        now = time.monotonic()
        if force or now >= next_progress_at:
            append_progress(progress_path, {"token": token, "status": "running",
                                            "time_ms": int(time.time() * 1000),
                                            "stop_reason": stop_reason,
                                            "sample": sample})
            next_progress_at = now + 30

    def submit(device: str, tick: int) -> None:
        frame = build_frame(device, args.points, tick, args.period_ms, base_ts)
        collector.write_window_stream(
            device, device_type, group, args.period_ms, frame["ts"], 1,
            {"ts": frame["ts"], "values": frame["window_values"]},
            frame["stream_values"], frame["stream_qualities"])
        collector.write_latest(device, group, frame["latest"])

    def snapshot(label: str, phase: str | None = None) -> dict:
        with collector.outbox._lock:
            metrics = collector._metrics.snapshot()
            outbox_stats = collector.outbox.stats()
        cpu = r.info("cpu")
        persistence = r.info("persistence")
        return {
            "label": label,
            "phase": phase or label.split("_")[0],
            "elapsed_s": round(time.monotonic() - phase_started, 3),
            "submitted": submitted,
            "production_submitted": production_submitted,
            "committed_frames": committed_frames(metrics),
            "replayed_frames": replayed_frames(metrics, args.points),
            # 守恒对账用的显式口径（键名与 queue_conservation 读取的一致）：
            # A = 本地持久提交，D = 已确认并清理完成。
            "accepted": committed_frames(metrics),
            "confirmed": replayed_frames(metrics, args.points),
            "queue_count": outbox_stats.get("count"),
            "claimed_frames": outbox_stats.get("claimed_frames"),
            "waiting_writes": outbox_stats.get("waiting_writes"),
            "oldest_age_ms": outbox_stats.get("oldest_age_ms"),
            "outbox": outbox_stats,
            "rss_bytes": metrics["runtime"]["rss_bytes"],
            "redis_used_memory_bytes": int(r.info("memory")["used_memory"]),
            # Redis 是单线程执行命令：CPU 秒数 / 墙钟秒数 ≈ 该实例的忙碌程度。
            "redis_cpu_seconds": round(
                float(cpu["used_cpu_sys"]) + float(cpu["used_cpu_user"]), 3),
            # AOF 重写会 fork 并大量写盘，是长测里最可能的外部扰动；
            # 记下来是为了让扰动出现在证据里，而不是被当成"系统变慢"。
            "redis_aof_rewrite_in_progress": int(persistence.get("aof_rewrite_in_progress", 0)),
            "redis_aof_size_bytes": int(persistence.get("aof_current_size", 0) or 0),
        }

    # 生产与追平共用同一个线程池：线程池必须在两个阶段都存活。
    # 曾把追平循环写在 with 块之外，命中 --catchup-seconds 时会抛
    # RuntimeError: cannot schedule new futures after shutdown。
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
                    checkpoint(current)
                    if current["redis_used_memory_bytes"] >= memory_guard:
                        stop_reason = "redis_memory_guard"
                        break
                next_tick = production_started + ticks * args.period_ms / 1000
                time.sleep(max(0, next_tick - time.monotonic()))
            production_end = time.monotonic()
            production_submitted = submitted
            production_snapshot = snapshot("production_end", phase="production_end")
            production_snapshot["elapsed_s"] = round(production_end - production_started, 3)
            samples.append(production_snapshot)
            checkpoint(production_snapshot, force=True)

            # ---- 受控积压预置（先暂停回放）----
            # 预置必须在回放**确实停下**之后进行。上一轮
            # margin-20dev-500pt-70pct.json 的 seeded_backlog.seconds=0 /
            # produced_frames=0 就是这么来的：回放一直在消费本地队列，
            # `count < target` 一开始就不成立，所谓"320 帧预置积压"其实只是
            # 正常生产的尾部积压。
            seeded = None
            if args.seed_backlog_frames > 0:
                # 同步等到在飞批次结束：只置位不够，一批 WAITAOF 可能正在途中。
                quiescent = collector.pause_replay()
                if not quiescent:
                    stop_reason = "replay_did_not_pause"
                confirmed_at_pause = replayed_frames(collector._metrics.snapshot(),
                                                     args.points)
                seed_started = time.monotonic()
                seed_deadline = seed_started + args.drain_seconds
                seed_ticks = 0
                while True:
                    if collector.outbox.stats()["count"] >= args.seed_backlog_frames:
                        break
                    if time.monotonic() > seed_deadline:
                        stop_reason = "seed_deadline"
                        break
                    tick = ticks
                    for future in [pool.submit(submit, device, tick) for device in devices]:
                        future.result()
                    ticks += 1
                    seed_ticks += 1
                    submitted += args.devices
                    # 节流必须用**本阶段自己的** tick 计数，不能用全局 ticks
                    # （全局已累计生产阶段的时间，算出来的时间点早已过期 → 不节流）。
                    time.sleep(max(0, seed_started + seed_ticks * args.period_ms / 1000
                                   - time.monotonic()))
                backlog = collector.outbox.stats()
                seed_seconds = time.monotonic() - seed_started
                confirmed_at_seed_end = replayed_frames(collector._metrics.snapshot(),
                                                        args.points)
                seeded = {
                    "seconds": round(seed_seconds, 3),
                    "produced_frames": seed_ticks * args.devices,
                    "replay_pause_requested": True,
                    "replay_quiescent_before_seed": quiescent,
                    # 暂停期间回放帧数必须不增长，否则"预置积压"被回放吃掉，
                    # 数字不可信 —— 这是本轮新增的反证。
                    "replay_advanced_during_seed": confirmed_at_seed_end - confirmed_at_pause,
                    "replay_paused_during_seed": confirmed_at_seed_end == confirmed_at_pause,
                    # 可处理积压 = 本地持久队列里**还没被回放领取**的帧数。
                    "backlog_frames": backlog["count"],
                    "backlog_bytes": backlog["payload_bytes"],
                    "backlog_oldest_age_ms": backlog["oldest_age_ms"],
                    # 已接收但尚未本地提交的量属于在途写，不是可处理积压。
                    "in_flight_local_writes": backlog["waiting_writes"],
                    "target_frames": args.seed_backlog_frames,
                    "target_reached": backlog["count"] >= args.seed_backlog_frames,
                }
                seed_snapshot = snapshot("seed_end", phase="seed")
                samples.append(seed_snapshot)
                checkpoint(seed_snapshot, force=True)

            # ---- 额定追平 / 降速恢复（分别命名、分别判定）----
            # 积压形成之后再同时启动回放与输入；只在积压未耗尽、处理端仍有任务的
            # 区间测量能力，积压一被处理完就停止计时（否则尾巴上净消减必然是 0）。
            rated = None
            degraded = None
            if args.catchup_seconds > 0:
                # 积压确认形成之后，才同时恢复回放并开始额定输入。
                collector.resume_replay()
                rate = max(args.catchup_rate, 1e-9)
                phase_name = "rated_catchup" if rate >= 1.0 else "degraded_recovery"
                phase_started = time.monotonic()
                window_started = phase_started
                window_start = snapshot(phase_name + "_start", phase=phase_name)
                window_start["elapsed_s"] = 0.0
                samples.append(window_start)
                checkpoint(window_start, force=True)
                window_ticks = 0
                exhausted_at = None
                period_s = (args.period_ms / 1000.0) / rate
                while time.monotonic() - window_started < args.catchup_seconds:
                    current_tick = ticks
                    for future in [pool.submit(submit, device, current_tick) for device in devices]:
                        future.result()
                    ticks += 1
                    window_ticks += 1
                    submitted += args.devices
                    if window_ticks % args.sample_every_ticks == 0:
                        current = snapshot(phase_name, phase=phase_name)
                        samples.append(current)
                        checkpoint(current)
                    if (args.seed_backlog_frames > 0
                            and collector.outbox.stats()["count"] == 0):
                        exhausted_at = time.monotonic() - window_started
                        break
                    time.sleep(max(0, window_started + window_ticks * period_s
                                   - time.monotonic()))
                window_s = time.monotonic() - window_started
                window_end = snapshot(phase_name + "_end", phase=phase_name)
                window_end["elapsed_s"] = round(window_s, 3)
                samples.append(window_end)
                checkpoint(window_end, force=True)
                confirmed = (window_end["replayed_frames"]
                             - window_start["replayed_frames"])
                backlog_drop = (window_start["outbox"]["count"]
                                - window_end["outbox"]["count"])
                input_fps = round(window_ticks * args.devices / max(window_s, 1e-9), 2)
                measured = {
                    "seconds": round(window_s, 3),
                    "rate_multiple": rate,
                    "seeded_backlog": seeded,
                    "input_fps": input_fps,
                    "confirmed_frames": confirmed,
                    "confirmed_fps": round(confirmed / max(window_s, 1e-9), 2),
                    "backlog_start_frames": window_start["outbox"]["count"],
                    "backlog_end_frames": window_end["outbox"]["count"],
                    "backlog_bytes_start": window_start["outbox"]["payload_bytes"],
                    "backlog_bytes_end": window_end["outbox"]["payload_bytes"],
                    "backlog_oldest_age_start_ms": window_start["outbox"]["oldest_age_ms"],
                    "backlog_oldest_age_end_ms": window_end["outbox"]["oldest_age_ms"],
                    "net_drain_fps": round(backlog_drop / max(window_s, 1e-9), 2),
                    # 已领取但尚未确认删除的在途量（估计）：积压减少量里没有被
                    # "已确认逻辑帧"解释掉的部分。它与可处理积压分开记录。
                    "claimed_unconfirmed_frames_estimate": max(0, backlog_drop - confirmed),
                    "backlog_exhausted_at_s": (round(exhausted_at, 3)
                                               if exhausted_at is not None else None),
                    "effective_window": (exhausted_at is None
                                         or exhausted_at >= args.min_effective_seconds),
                }
                # 处理能力用实测确认量 ÷ 窗口时长：窗口在积压耗尽处停止计时，
                # 因此这个估计在整个窗口内都成立。
                measured["capacity_fps_estimate"] = round(
                    confirmed / max(window_s, 1e-9), 2)
                measured["margin_fraction"] = (
                    measured["capacity_fps_estimate"] / input_fps - 1.0
                    if input_fps > 0 else 0.0)
                if rate >= 1.0:
                    rated = measured
                else:
                    degraded = measured

        # 排空前确保回放已恢复（幂等）：只预置积压、不跑追平阶段时，也必须让
        # 本地队列能真正排出去，否则排空阶段会一直等到超时。
        collector.resume_replay()
        drain_started = time.monotonic()
        # 真正停止输入的那一刻取快照：排空速率必须用"排空阶段自己的增量"，
        # 不能把追平阶段仍在生产时完成的帧算进来。
        drain_start_snapshot = snapshot("drain_start")
        checkpoint(drain_start_snapshot, force=True)
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
        checkpoint(final_snapshot, force=True)
        drained_confirmed = (final_snapshot["replayed_frames"]
                             - drain_start_snapshot["replayed_frames"])
        final_outbox = collector.outbox.stats()
        with collector._latest_condition:
            final_latest_pending = len(collector._latest_pending)
        collector.close()
        with sqlite3.connect(db_path) as db:
            sqlite_rows = sum(db.execute(f"SELECT COUNT(*) FROM {table}").fetchone()[0]
                              for table in ("collector_retry_outbox", "collector_retry_outbox_v2"))

        # ---- 数据正确性对账 ----
        problems: list[str] = []
        audit = audit_stream_frames(r, keys, devices, device_type, group,
                                    args.points, ticks, args.period_ms, base_ts,
                                    args.audit_stride)
        problems.extend(audit["problems"])

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
        # 预热取额定周期数的 10 倍或 2 秒的较大者：异步流水线需要时间填满在途窗口。
        warmup_s = max(2.0, args.period_ms / 1000.0 * 10)
        windows = window_analysis(samples, args.period_ms, args.devices, warmup_s=warmup_s)
        conservation = queue_conservation(
            [sample for sample in samples
             if sample.get("phase") in ("production", "production_end")])
        verdicts = build_verdicts(
            target_frames=report["target_frames"], production_submitted=production_submitted,
            production_s=production_end - production_started, target_seconds=args.seconds,
            committed=committed_total, replayed_during_production=replayed_during_production,
            windows=windows, drain_s=drain_s, final_outbox=final_outbox,
            correctness_problems=problems, target_input_fps=target_input_fps,
            seeded=seeded, rated=rated, degraded=degraded,
            min_effective_seconds=args.min_effective_seconds)
        verdicts["queue_conservation"] = {
            "status": conservation["status"],
            "detail": conservation.get("detail", "本地队列 A/D/Q 守恒对账"),
        }
        catchup_seconds_total = ((rated["seconds"] if rated else 0.0)
                                 + (degraded["seconds"] if degraded else 0.0))

        report.update({
            "verdicts": verdicts,
            # 总体状态是 pass|fail|not_tested 三态字符串：未测不再被折算成通过。
            "overall_status": overall_status(verdicts),
            # 本次实际尝试的判定项：没跑的档位不算不通过，但跑了没测出结果仍算未测。
            "measured_status": overall_status({
                **{name: verdicts[name] for name in attempted_verdict_names(
                    seeded=seeded is not None, rated=rated is not None,
                    degraded=degraded is not None)},
                "queue_conservation": verdicts["queue_conservation"],
            }),
            "stop_reason": stop_reason, "ticks": ticks,
            "submitted_frames": submitted,
            "production_submitted_frames": production_submitted,
            "production_seconds": round(production_end - production_started, 3),
            "catchup_seconds": round(catchup_seconds_total, 3),
            "seeded_backlog": seeded,
            "rated_catchup": rated,
            "degraded_recovery": degraded,
            "drain_seconds": round(drain_s, 3),
            "total_elapsed_seconds": round(
                (production_end - production_started)
                + catchup_seconds_total + drain_s, 3),
            # 实际接收速率用**实际提交量**；目标量单独输出，两者不能混用
            # （输入未达速时，用目标量当分子会高估实际接收速率）。
            "production_receive_fps_actual": round(
                production_submitted / max(production_end - production_started, 1e-9), 2),
            "production_receive_fps_target": round(
                (args.devices * (args.seconds * 1000 // args.period_ms))
                / max(args.seconds, 1e-9), 2),
            "committed_frames_total": committed_total,
            "replayed_frames_total": final_snapshot["replayed_frames"],
            "replayed_frames_during_production": replayed_during_production,
            "replayed_fps_during_production": round(
                replayed_during_production / max(production_end - production_started, 1e-9), 2),
            "steady_confirmed_fps": windows.get("steady_confirmed_fps"),
            # 排空速率只算**停止输入之后**完成的帧。追平阶段仍在生产，
            # 它的完成量必须扣除，否则会把那一段的吞吐算进排空。
            "drained_frames_after_input_stopped": drained_confirmed,
            "replayed_fps_after_production": round(
                drained_confirmed / max(drain_s, 1e-9), 2),
            "audited_frames": audit["audited"],
            "total_frames_in_stream": audit["total"],
            "duplicate_frames": audit["duplicates"],
            "missing_frames": audit["missing"],
            "invalid_identity_frames": audit["invalid"],
            "correctness_problems": problems,
            "windows": windows,
            # 队列守恒对账：只取生产阶段的窗口，用 A/D/Q 三个显式口径对账，
            # 任何不能被 ΔA、ΔD、Δ在途解释的差额都要列出来。
            "conservation": conservation,
            "batch_timeline": batch_timeline_analysis(collector.delivery_timeline()),
            "redis_cpu": _redis_cpu_summary(samples),
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
        append_progress(progress_path, {"token": token, "status": "complete",
                                        "time_ms": int(time.time() * 1000),
                                        "overall_status": report["overall_status"],
                                        "measured_status": report["measured_status"]})
        print(json.dumps({key: value for key, value in report.items()
                          if key not in ("samples", "windows", "profile_windows")},
                         ensure_ascii=False, indent=2), flush=True)
        return report
    except Exception as exc:
        report.update({"error": repr(exc), "submitted_frames": submitted, "ticks": ticks,
                       "samples": samples})
        save_report(Path(args.output), report)
        append_progress(progress_path, {"token": token, "status": "error",
                                        "time_ms": int(time.time() * 1000),
                                        "error": repr(exc)})
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
                        help="预置积压形成后，额定输入与回放同时启动的测量时长；"
                             "积压被处理完即提前结束计时")
    parser.add_argument("--catchup-rate", type=float, default=1.0,
                        help="该阶段的输入速率倍数（相对额定）。1.0（默认）=额定追平，"
                             "结果进 rated_catchup_margin；< 1 只作降速恢复描述，"
                             "单列 degraded_recovery，绝不顶替额定余量")
    parser.add_argument("--min-effective-seconds", type=float, default=5.0,
                        help="有效测量区间下限：积压在此时间内被处理完则报 not_tested，"
                             "不判为无余量")
    parser.add_argument("--seed-backlog-frames", type=int, default=0,
                        help="受控积压：回放**暂停**期间按正常持久接收路径预置这么多帧，"
                             "确认积压形成后再同时启动回放与额定输入，"
                             "避免用'正常流水线遗留的积压'充当处理余量证据")
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
    # 退出码看本次实际尝试的判定项：没跑的档位（如额定轮次里的降速档）不该让
    # 一轮合格运行变成失败；但跑了没测出结果仍是 not_tested，不算通过。
    raise SystemExit(0 if result.get("measured_status") == PASS else 1)
