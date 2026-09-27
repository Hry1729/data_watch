"""验收规则与对账核心（Collector 容量脚本 / 完整链路脚本共用）。

为什么集中在这里
----------------
两个验证脚本此前各自定义 `PASS/FAIL/NOT_TESTED`、各自定义合成期望数据、
各自判断"通过"。同一批数据在两处可能得出不同结论，而且"未测"很容易被
当成"通过"。判定词表、总体口径、期望数据模型、稳态窗口与逐行对账统一放在
这里，作为唯一来源；脚本只能调用，不得再自己拼一个"通过"。

边界
----
* 这些函数只做**判定与对账**，不做 I/O。真实 Redis/TDengine 查询由脚本负责，
  因此本模块的规则可以离线用故障注入数据直接验证（见 scripts/test_pipeline_acceptance.py）。
* `not_tested` 是独立状态：没有测量就不等于通过，`overall_status()` 也不把它
  折算成 pass。
"""
from __future__ import annotations

import math
import zlib

PASS, FAIL, NOT_TESTED = "pass", "fail", "not_tested"

# 与 collector/core/quality_codes.py、writer/quality_codes.py 同值。
Q_GOOD = 0
Q_UNCERTAIN = 2
Q_BAD = 5

# 固定点位的特殊语义，用于覆盖 NULL / 布尔 / 负值 / 非存储 / STRING /
# 时有时无的 NULL（值表里必须有真实 NULL 单元格）/ 质量变化。
P_NULL = 1            # 值为 None：只进 latest（v=""），不进 stream
P_BOOL = 2            # bool：必须序列化成 "1"/"0"，不能是 "True"/"False"
P_NEGATIVE = 3        # 负值
P_QUALITY_VARY = 4    # 每个 tick 变化的质量码（GOOD / UNCERTAIN / BAD）
P_NON_STORAGE = 6     # is_storage=0：只进 latest 与 window，不进 stream
P_STRING_TYPE = 7     # 数据类型 STRING：只进 latest 与 window，不进 stream
P_SOMETIMES_NULL = 8  # 存储点，但每 5 个 tick 有 1 个 tick 值为 None：
                      # 列必须存在，且那些行在该列上必须是 NULL
FIXED_SPECIAL = (P_NULL, P_BOOL, P_NEGATIVE, P_QUALITY_VARY,
                 P_NON_STORAGE, P_STRING_TYPE, P_SOMETIMES_NULL)

# 永远不进历史 stream 的点（因此值超级表里不应出现对应列）。
NEVER_STORED = (P_NULL, P_NON_STORAGE, P_STRING_TYPE)


# ----------------------------------------------------------------------
# 判定词表
# ----------------------------------------------------------------------

def verdict(status: str, detail: str) -> dict:
    return {"status": status, "detail": detail}


def not_tested(detail: str) -> dict:
    """未测量：必须显式区别于通过。"""
    return verdict(NOT_TESTED, detail)


def overall_status(verdicts: dict) -> str:
    """总体口径：全部 pass 才 pass；任一 fail 即 fail；否则 not_tested。

    单独提出来是为了禁止各脚本用 `all(status == pass)` 之外的自定义口径
    （例如把 not_tested 混进 pass 数量）。
    """
    statuses = [item.get("status") for item in verdicts.values()]
    if not statuses:
        return NOT_TESTED
    if all(status == PASS for status in statuses):
        return PASS
    if any(status == FAIL for status in statuses):
        return FAIL
    return NOT_TESTED


def correctness_verdict(problems: list[str], *, checked: str) -> dict:
    if not problems:
        return verdict(PASS, f"{checked}：全部检查通过")
    return verdict(FAIL, f"{checked}：{len(problems)} 项不符："
                         + "; ".join(problems[:5]))


# ----------------------------------------------------------------------
# 延迟：固定阈值目标必须按"阈值内样本占比"判定
#
# 直方图分位数返回的是**桶上界**（TimingHistogram 的 p99 = 命中桶的上界，
# 见 collector/monitor/performance.py），它既可能高估也可能掩盖真实超标，
# 因此不能用它来判一个固定毫秒目标。这里统计阈值内的样本比例。
# ----------------------------------------------------------------------

# 三个延迟指标必须分开报，不能互相替代：
#   1. 实时显示新鲜度     采样发生 → 页面数值真正更新（本轮未测）
#   2. 历史入库确认延迟   采样发生 → TDengine 完整帧确认（本轮按测试预算判）
#   3. Redis 持久交付延迟 本地持久接收 → Redis 持久确认（AOF/WAITAOF 口径）
LATENCY_METRICS = {
    "realtime_display_freshness": "采样发生→页面数值真正更新（含后端推送与浏览器绘制）",
    "history_commit_latency": "采样发生→TDengine 完整帧确认",
    "redis_durable_delivery_latency": "本地持久接收→Redis 持久确认（WAITAOF/AOF）",
}

# 预算来源必须显式写出：现场问题是"页面显示超过 1 秒"，它**不是**
# "历史入库确认延迟 P99 < 1s"的产品 SLA。这里只作为复核测试预算使用。
LATENCY_BUDGET_ORIGIN = (
    "本轮复核测试预算：来自现场『页面显示超过 1 秒』的观察，"
    "尚未约定为『历史入库确认延迟』的产品 SLA；只适用于 history_commit_latency"
)


def latency_threshold_report(samples, budget_ms: float, *,
                             required_ratio: float = 0.99,
                             buckets=None) -> dict:
    """按阈值内样本占比判定固定延迟目标，并披露分位数所在桶区间。

    `required_ratio=0.99` 等价于原来的"P99 ≤ 预算"，但用的是真实样本计数，
    不是桶上界。样本量为 0 时为 `not_tested`（未测 ≠ 通过）。
    """
    values = [float(value) for value in samples]
    total = len(values)
    within = sum(1 for value in values if value <= budget_ms)
    over = total - within
    ratio = within / total if total else 0.0
    lower = upper = None
    if total and buckets:
        edges = sorted(float(edge) for edge in buckets)
        target = max(1, math.ceil(total * required_ratio))
        cumulative = 0
        previous = 0.0
        for edge in edges:
            cumulative += sum(1 for value in values if previous < value <= edge)
            if cumulative >= target:
                lower = previous
                upper = None if math.isinf(edge) else edge
                break
            previous = edge
    report = {
        "metric": "history_commit_latency",
        "budget_ms": budget_ms,
        "budget_origin": LATENCY_BUDGET_ORIGIN,
        "total_samples": total,
        "within_budget_samples": within,
        "over_budget_samples": over,
        "within_ratio": round(ratio, 6),
        "required_within_ratio": required_ratio,
        "p99_bucket_interval_ms": [lower, upper],
        "max_ms": round(max(values), 3) if values else None,
        "average_ms": round(sum(values) / total, 3) if total else None,
    }
    if not total:
        report["status"] = NOT_TESTED
        return report
    report["status"] = PASS if ratio >= required_ratio else FAIL
    return report


# ----------------------------------------------------------------------
# 回放批次时间线：判断"确认等待"是当前批的、还是上一批挡住了下一批
#
# 回放是串行的：领取 → 写 Redis → 等 WAITAOF → 删除并提交 → 再领取。
# 因此某一帧的"采样→Redis 持久确认"里同时包含：
#   * 它在本地队列里等下一次领取的时间（claim 时该批最老帧的等待）
#   * 它所在批次那次流水线往返（deliver_ms：脚本执行 + WAITAOF 等待，
#     客户端**无法**把这两者拆开，拆分只能靠 Redis 侧 commandstats/INFO cpu）
# 只有把这两项分开，才能回答"延迟是本批持久确认造成的，还是上一批阻塞了下一批投递"。
# ----------------------------------------------------------------------

def queue_conservation(windows) -> dict:
    """本地队列守恒对账：`Q_end − Q_start = ΔA − ΔD`，差额必须能被解释。

    口径（同一 Collector、同一时间窗口）：
      * A = 本地持久提交的逻辑帧数（cumulative accepted）
      * D = 已完成可靠交付**并提交本地清理**的逻辑帧数（cumulative confirmed）
      * Q = 仍在本地责任范围内的帧数 = A − D
      * Q_api = 队列 API 报的条数。**已领取未清理的行仍算在 Q_api 里**
        （本地 DELETE 与计数递减都发生在清理提交之后），所以对账式是
        `A − D == Q_api`，而 `claimed` 是 Q_api 里"此刻正在投递"的那一部分子集
        （`claimed ≤ Q_api`），不是相加项。

    对账式：`A − D == Q_api`，残差必须落在容差内；`claimed` 单独报告为在途子集。
    若某条路径删了行而没累加 D（或反过来），残差会立刻显形——差额不允许消失。

    `net_growth_fps` 是逐窗口"净增长帧数 ÷ 窗口时长"，因此不受窗口长度不齐影响。
    只有在长时间同窗口对账成立之后，最后一段的平均净增长才可以称为**持续**缺口；
    短期速率低于输入也可能是批量确认锯齿或窗口边界效应。
    """
    entries = []
    for window in windows:
        accepted = window.get("accepted")
        confirmed = window.get("confirmed")
        if accepted is None or confirmed is None:
            continue
        queue_count = window.get("queue_count")
        claimed = window.get("claimed_frames")
        outstanding = int(accepted) - int(confirmed)
        residual = None
        if queue_count is not None:
            residual = outstanding - int(queue_count)
        entries.append({
            "elapsed_s": window.get("elapsed_s"),
            "accepted": int(accepted),
            "confirmed": int(confirmed),
            "outstanding": outstanding,
            "queue_count": queue_count,
            "claimed_frames": claimed,
            "identity_residual": residual,
            "waiting_writes": window.get("waiting_writes"),
            "oldest_age_ms": window.get("oldest_age_ms"),
        })
    if len(entries) < 3:
        return {"status": NOT_TESTED, "windows": entries,
                "detail": "样本不足，无法做队列守恒对账"}

    first, last = entries[0], entries[-1]
    delta_accepted = last["accepted"] - first["accepted"]
    delta_confirmed = last["confirmed"] - first["confirmed"]
    delta_outstanding = last["outstanding"] - first["outstanding"]
    counter_residual = delta_outstanding - (delta_accepted - delta_confirmed)
    # 对账式：Δ(A−D) 是否等于 Δ队列条数（claimed 是队列条数里的在途子集，不相加）
    delta_queue = (None if first["queue_count"] is None or last["queue_count"] is None
                   else int(last["queue_count"]) - int(first["queue_count"]))
    identity_residual = (None if delta_queue is None
                         else delta_outstanding - delta_queue)
    # claimed 必须是 count 的子集：子集关系本身就是一条可证伪的检查
    subset_violations = [entry for entry in entries
                         if entry["claimed_frames"] is not None
                         and entry["queue_count"] is not None
                         and int(entry["claimed_frames"]) > int(entry["queue_count"])]

    per_window = []
    for previous, current in zip(entries, entries[1:]):
        span = (current["elapsed_s"] or 0) - (previous["elapsed_s"] or 0)
        if span <= 0:
            continue
        accepted_delta = current["accepted"] - previous["accepted"]
        confirmed_delta = current["confirmed"] - previous["confirmed"]
        net = accepted_delta - confirmed_delta
        per_window.append({
            "from_s": previous["elapsed_s"], "to_s": current["elapsed_s"],
            "span_s": round(span, 3),
            "accepted_delta": accepted_delta, "confirmed_delta": confirmed_delta,
            "net_growth_frames": net, "net_growth_fps": round(net / span, 3),
            "queue_count": current["queue_count"],
            "claimed_frames": current["claimed_frames"],
            "outstanding": current["outstanding"],
            "identity_residual": current["identity_residual"],
            "oldest_age_ms": current["oldest_age_ms"],
        })
    total_span = (last["elapsed_s"] or 0) - (first["elapsed_s"] or 0)
    net_total = delta_accepted - delta_confirmed
    quarter = max(1, len(per_window) // 4)
    tail = per_window[-quarter:]
    head = per_window[:quarter]
    tail_span = sum(item["span_s"] for item in tail)
    head_span = sum(item["span_s"] for item in head)
    tail_fps = (sum(item["net_growth_frames"] for item in tail) / tail_span
                if tail_span > 0 else None)
    head_fps = (sum(item["net_growth_frames"] for item in head) / head_span
                if head_span > 0 else None)
    if tail_fps is None:
        classification = "not_tested"
    elif tail_fps > 0:
        classification = "sustained_growth"
    else:
        classification = "sawtooth_or_converging"

    report = {
        "definition": ("A−D == 队列条数（已领取未清理的行仍在队列条数里，claimed 是它的"
                       "在途子集，不相加）；任何第三路径造成的残差都要单独列出"),
        "accepted_delta": delta_accepted,
        "confirmed_delta": delta_confirmed,
        "outstanding_start_frames": first["outstanding"],
        "outstanding_end_frames": last["outstanding"],
        "queue_count_start_frames": first["queue_count"],
        "queue_count_end_frames": last["queue_count"],
        "claimed_start_frames": first["claimed_frames"],
        "claimed_end_frames": last["claimed_frames"],
        "claimed_is_subset_of_queue": not subset_violations,
        "counter_residual_frames": counter_residual,
        "identity_residual_frames": identity_residual,
        "net_growth_frames": net_total,
        "net_growth_fps": round(net_total / total_span, 3) if total_span > 0 else None,
        "tail_quarter_mean_growth_fps": (round(tail_fps, 3) if tail_fps is not None else None),
        "head_quarter_mean_growth_fps": (round(head_fps, 3) if head_fps is not None else None),
        "classification": classification,
        "windows": per_window,
    }
    # 调用方在 outbox 锁内采集 A/D/Q；不能按累计输入放宽容差。
    tolerance = 0
    problems = []
    if counter_residual != 0:
        problems.append(f"计数口径不自洽：Q 变化 {delta_outstanding} ≠ ΔA−ΔD "
                        f"{delta_accepted - delta_confirmed}")
    if identity_residual is not None and abs(identity_residual) > tolerance:
        problems.append(f"守恒等式不闭合：Δ(A−D) {delta_outstanding} ≠ Δ队列条数 "
                        f"{delta_queue}，残差 {identity_residual} 帧（超出容差 {tolerance}）")
    inconsistent = [entry for entry in entries if entry["identity_residual"] != 0]
    if inconsistent:
        problems.append(f"{len(inconsistent)} 个采样点 A−D 与队列条数不一致或缺失")
    if identity_residual is None:
        problems.append("队列条数缺失，无法完成守恒对账")
    if subset_violations:
        problems.append(f"已领取未清理 {len(subset_violations)} 个采样点超过队列条数"
                        "（claimed 必须是 count 的子集）")
    report["nonzero_or_missing_residual_samples"] = len(inconsistent)
    report["tolerance_frames"] = tolerance
    report["status"] = FAIL if problems else PASS
    report["problems"] = problems
    report["note"] = ("净增长速率本身不直接判 fail：短期低于输入可能是批量确认锯齿或"
                      "窗口边界效应；只有长时间同窗口对账成立后，尾部平均净增长才可以"
                      "称为持续容量缺口")
    return report


def batch_timeline_analysis(records) -> dict:
    """把回放批次时间线压成可判读的分布（全部来自单调时钟的差值，单位 ms）。"""
    batches = []
    previous_confirmed = None
    for record in records:
        claim_at = record.get("claim_at")
        if claim_at is None:
            continue
        entry = {
            "frames": record.get("frames"),
            "bytes": record.get("bytes"),
            "queue_frames_at_claim": record.get("queue_frames_at_claim"),
            "queue_oldest_age_ms_at_claim": record.get("queue_oldest_age_ms_at_claim"),
            "claim_gap_ms": (None if previous_confirmed is None
                             else round((claim_at - previous_confirmed) * 1000, 3)),
            "cycle_ms": None,
        }
        if record.get("prev_claim_at") is not None:
            entry["cycle_ms"] = round((claim_at - record["prev_claim_at"]) * 1000, 3)
        for key in ("write_ms", "deliver_ms", "commit_ms"):
            if record.get(key) is not None:
                entry[key] = round(float(record[key]), 3)
        batches.append(entry)
        if record.get("aof_confirmed_at") is not None:
            previous_confirmed = record["aof_confirmed_at"]

    def quantile(key: str, fraction: float):
        values = sorted(entry[key] for entry in batches
                        if entry.get(key) is not None)
        if not values:
            return None
        index = min(len(values) - 1, max(0, math.ceil(len(values) * fraction) - 1))
        return values[index]

    analysis = {"batch_count": len(batches), "batches": batches[-64:]}
    # `deliver_ms` 是一次流水线往返：EVALSHA 执行与 WAITAOF 等待**不可从客户端拆分**，
    # 因此这里叫"确认往返"，不叫"AOF 等待"。两者的拆分只能靠 Redis 侧计数
    # （commandstats 的 evalsha/waitaof、INFO cpu 忙碌比），由脚本另行采集。
    for key in ("deliver_ms", "cycle_ms", "claim_gap_ms", "queue_oldest_age_ms_at_claim",
                "write_ms", "frames"):
        analysis[key] = {
            "p50": quantile(key, .50), "p95": quantile(key, .95),
            "max": (max((entry[key] for entry in batches if entry.get(key) is not None),
                        default=None)),
        }
    confirm_p50 = analysis["deliver_ms"]["p50"]
    cycle_p50 = analysis["cycle_ms"]["p50"]
    gap_p50 = analysis["claim_gap_ms"]["p50"]
    queue_p50 = analysis["queue_oldest_age_ms_at_claim"]["p50"]
    if confirm_p50 is None or cycle_p50 in (None, 0):
        analysis["dominant_wait"] = "not_tested"
        analysis["interpretation"] = "批次时间线样本不足，无法判断等待发生在哪一段"
        return analysis
    # 上一批确认返回后多久才领取下一批：接近 0 说明回放一直卡在"等确认"上，
    # 新帧只能在本地队列里排队 —— 即上一批的持久确认阻塞了下一批投递。
    blocked = gap_p50 is not None and gap_p50 <= 0.1 * cycle_p50
    if blocked and confirm_p50 >= 0.5 * cycle_p50:
        analysis["dominant_wait"] = "previous_batch_confirmation_blocks_next_claim"
    elif queue_p50 is not None and queue_p50 >= 0.5 * cycle_p50:
        analysis["dominant_wait"] = "frames_wait_a_full_cycle_in_local_queue"
    else:
        analysis["dominant_wait"] = "current_batch_confirmation"
    analysis["interpretation"] = (
        f"批次周期 p50 {cycle_p50}ms ≈ 确认往返 p50 {confirm_p50}ms + 领取间隔 p50 "
        f"{gap_p50}ms；批内最老帧在本地队列已等待 p50 {queue_p50}ms。"
        "同一进程串行回放时，上一批的持久确认会把新帧的投递推迟到下一次领取，"
        "这两段时间叠加在同一帧的端到端延迟里，不能只归因于当前批的确认等待；"
        "确认往返内部的『脚本执行 vs AOF 等待』拆分需看 Redis 侧计数")
    return analysis


# ----------------------------------------------------------------------
# 合成期望数据：由 (device, point, tick) 共同决定
#
# 期望值必须同时区分设备、点位与采样序号：不同设备生成相同数值序列时，
# 跨设备误归属根本看不出来。
# ----------------------------------------------------------------------

def device_seed(device: str) -> int:
    """设备标识 → 稳定的小整数，让不同设备的数值互相区分。

    不依赖设备名里的编号格式（历史实现取末三位数字，换一种命名就全变成 0，
    跨设备区分失效）。
    """
    return zlib.crc32(str(device).encode("utf-8")) % 997


def is_string_point(point: int) -> bool:
    return point == P_STRING_TYPE


def is_storage_point(point: int) -> bool:
    return point != P_NON_STORAGE


def expected_stream_value(device: str, point: int, tick: int) -> str | None:
    """stream 中该点位应有的字符串值；None 表示该点不进 stream。"""
    if point in NEVER_STORED:
        return None
    if point == P_SOMETIMES_NULL and tick % 5 == 4:
        return None
    if point == P_BOOL:
        return "1" if (tick + device_seed(device)) % 2 else "0"
    if point == P_NEGATIVE:
        return str(-(point * 1000 + tick))
    return str(point + tick + device_seed(device) * 7)


def expected_latest_value(device: str, point: int, tick: int) -> str:
    """latest 中该点位应有的字符串值（NULL 点为 ""，非存储点同样写入 latest）。"""
    if point in (P_NULL, P_SOMETIMES_NULL):
        # 非存储点/STRING 点仍写入自己的真实值；只有真正的 NULL 点是空串。
        if expected_stream_value(device, point, tick) is None:
            return ""
    if point == P_BOOL:
        return "1" if (tick + device_seed(device)) % 2 else "0"
    if point == P_NEGATIVE:
        return str(-(point * 1000 + tick))
    if point in NEVER_STORED:
        return f"text-{point}-{tick}"
    return str(point + tick + device_seed(device) * 7)


def expected_quality(point: int, tick: int) -> int:
    """该点位在该 tick 的质量码；覆盖 GOOD→变化→恢复 GOOD。"""
    if point == P_QUALITY_VARY:
        return (Q_GOOD, Q_UNCERTAIN, Q_BAD)[tick % 3]
    if point == P_QUALITY_VARY + 1:  # 第 5 点：一次坏值后恢复，验证质量码往返
        if tick % 20 == 7:
            return Q_BAD
        return Q_GOOD
    return Q_GOOD


def build_frame(device: str, points: int, tick: int, period_ms: int,
                base_ts: int) -> dict:
    """构造与 executor._write_to_redis 同形的 frame / stream / latest 三元组。

    注意：调用方直接构造 writer 入参，未经 collector.executor 的分组与过滤代码；
    executor 自身的过滤语义由 collector/tests/test_executor.py 覆盖。
    """
    ts = base_ts + tick * period_ms
    window_values = {str(p): expected_latest_value(device, p, tick)
                     for p in range(1, points + 1)}
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


def tdengine_value_column(point: int) -> str:
    """点位在值超级表里的列名（与脚本写入 MySQL 映射的口径一致）。"""
    return f"p{point}"


def expected_value_cell(device: str, point: int, tick: int) -> float | None:
    """值表该 (device, point, tick) 单元格应有的浮点值；None 表示必须是 NULL。

    非存储点 / STRING 点的 `expected_stream_value` 本身就是 None，因此它们
    在值表里只能是 NULL（且正常情况下超级表里连列都不该存在）。
    """
    raw = expected_stream_value(device, point, tick)
    return None if raw is None else float(raw)


def expected_quality_cell(device: str, point: int, tick: int) -> int | None:
    """质量窄表该 (device, point, tick) 应有的质量码；None 表示不该有该行。

    质量窄表只在点进入历史 stream 时才写入，因此与 stream 点集严格一致。
    """
    if expected_stream_value(device, point, tick) is None:
        return None
    return expected_quality(point, tick)


# ----------------------------------------------------------------------
# 逐行全字段对账（离线可测：喂进被破坏的数据即可验证校验器能抓到错误）
# ----------------------------------------------------------------------

def _close_enough(actual: object, expected: object) -> bool:
    if actual is None or expected is None:
        return actual is None and expected is None
    try:
        left = float(actual)
        right = float(expected)
    except (TypeError, ValueError):
        return actual == expected
    if math.isnan(left) or math.isnan(right):
        return False
    return math.isclose(left, right, rel_tol=1e-9, abs_tol=1e-9)


def reconcile_value_rows(actual_rows: list[dict], expectations: dict,
                         *, expected_columns: list[str], max_problems: int = 20) -> list[str]:
    """值表全字段对账：身份、重复、遗漏、每一列的每一个单元格。

    actual_rows: [{"device_code": str, "ts": int, "cells": {column: float|None}}]
    expectations: {(device_code, ts): {column: float|None}}
    expected_columns: 该超级表里必须存在的列（缺列要报，不能只看已有列）

    NULL 是有效期望值：期望为 None 而实际有数、或期望有数而实际为 NULL，
    都必须报出来。中间某个点位被改动过也必须报出来。
    """
    problems: list[str] = []
    seen: dict[tuple, int] = {}
    for row in actual_rows:
        key = (row["device_code"], int(row["ts"]))
        seen[key] = seen.get(key, 0) + 1
    duplicates = {key: count for key, count in seen.items() if count > 1}
    if duplicates:
        problems.append(f"重复行 {len(duplicates)} 组，例如 "
                        f"{sorted(duplicates.items())[:3]}")
    missing = [key for key in expectations if key not in seen]
    if missing:
        problems.append(f"遗漏 {len(missing)} 行，例如 {sorted(missing)[:3]}")
    extra = [key for key in seen if key not in expectations]
    if extra:
        problems.append(f"多出 {len(extra)} 行，例如 {sorted(extra)[:3]}")

    for row in actual_rows:
        key = (row["device_code"], int(row["ts"]))
        want = expectations.get(key)
        if want is None:
            continue
        cells = row.get("cells") or {}
        for column in expected_columns:
            if column not in cells:
                problems.append(f"{key} 缺列 {column}")
                if len(problems) >= max_problems:
                    return problems
        for column, wanted in want.items():
            if column not in cells:
                continue
            actual = cells[column]
            if not _close_enough(actual, wanted):
                problems.append(
                    f"{key} 列 {column}: 实际 {actual!r} 期望 {wanted!r}")
            if len(problems) >= max_problems:
                return problems
    return problems


def reconcile_quality_rows(actual_rows: list[dict], expectations: dict,
                           *, max_problems: int = 20) -> list[str]:
    """质量窄表对账：身份 (device, ts, var_id)、重复、遗漏、质量码值。

    质量恒定不变会被掩盖：期望值随 tick 变化，只有真的按点核对才发现。
    """
    problems: list[str] = []
    seen: dict[tuple, int] = {}
    for row in actual_rows:
        key = (row["device_code"], int(row["ts"]), int(row["var_id"]))
        seen[key] = seen.get(key, 0) + 1
    duplicates = {key: count for key, count in seen.items() if count > 1}
    if duplicates:
        problems.append(f"重复质量行 {len(duplicates)} 组，例如 "
                        f"{sorted(duplicates.items())[:3]}")
    missing = [key for key in expectations if key not in seen]
    if missing:
        problems.append(f"遗漏质量行 {len(missing)}，例如 {sorted(missing)[:3]}")
    extra = [key for key in seen if key not in expectations]
    if extra:
        problems.append(f"多出质量行 {len(extra)}，例如 {sorted(extra)[:3]}")
    for row in actual_rows:
        key = (row["device_code"], int(row["ts"]), int(row["var_id"]))
        wanted = expectations.get(key)
        if wanted is None:
            continue
        if not _close_enough(row.get("quality"), wanted):
            problems.append(f"质量行 {key}: 实际 {row['quality']!r} 期望 {wanted!r}")
        if len(problems) >= max_problems:
            return problems
    return problems


# ----------------------------------------------------------------------
# 连续窗口与趋势（容量脚本与完整链路脚本共用同一套稳态口径）
# ----------------------------------------------------------------------

def window_analysis(samples: list[dict], period_ms: int, devices: int,
                    min_span_s: float = 0.2, warmup_s: float = 0.0,
                    phase: str = "production") -> dict:
    """按相邻采样点构造连续窗口，给出速率、积压与趋势。

    过短的窗口会把采样抖动放大成天文数字速率，直接丢弃而不参与判定。
    `warmup_s` 之后才计入稳态速率：异步流水线在启动阶段有正常的填充期，
    把启动期算进"持续确认速率"会系统性低估处理能力。
    每个阶段单独计算（阶段内 elapsed_s 从该阶段起点计）。
    """
    windows = []
    for previous, current in zip(samples, samples[1:]):
        if previous.get("phase") != current.get("phase"):
            continue
        span = current["elapsed_s"] - previous["elapsed_s"]
        if span < min_span_s:
            continue
        outbox = current["outbox"]
        previous_outbox = previous["outbox"]
        windows.append({
            "phase": current.get("phase"),
            "from_s": previous["elapsed_s"],
            "to_s": current["elapsed_s"],
            "span_s": span,
            "input_delta": current["submitted"] - previous["submitted"],
            "confirmed_delta": current["replayed_frames"] - previous["replayed_frames"],
            "input_fps": round((current["submitted"] - previous["submitted"]) / span, 2),
            "replay_confirmed_fps": round(
                (current["replayed_frames"] - previous["replayed_frames"]) / span, 2),
            "outbox_frames": outbox["count"],
            "outbox_oldest_age_ms": outbox["oldest_age_ms"],
            "outbox_delta": outbox["count"] - previous_outbox["count"],
        })
    # 稳态窗口 = 预热之后**开始**的该阶段窗口。用 from_s 而不是 to_s：
    # 跨越预热边界的窗口会把填充期的低速确认算进稳态，系统性低估处理能力。
    # 速率与趋势共用同一个集合，避免两处门槛不一致。
    steady_windows = [window for window in windows
                      if window.get("phase") == phase
                      and window.get("from_s", 0.0) >= warmup_s]
    if not windows:
        return {"windows": [], "trend": "not_tested", "warmup_s": warmup_s,
                "steady_windows": 0}
    trend = "insufficient_windows"
    segments: list[float] = []
    if len(steady_windows) >= 4:
        # 判断"**持续**增长"必须看尾部两段，而不是首尾之差：
        # 运行中新增点位（TDengine 加列）会让积压台阶式抬升一次然后持平，
        # 那是能力足够的表现，不是"处理跟不上输入"。只看首尾差会把台阶误判成增长。
        size = len(steady_windows) / 4
        bounds = [round(size * index) for index in range(5)]
        segments = []
        for start, stop in zip(bounds, bounds[1:]):
            group = steady_windows[start:stop] or steady_windows[start:] or steady_windows[-1:]
            segments.append(round(sum(w["outbox_frames"] for w in group) / len(group), 2))
        previous, latest = segments[-2], segments[-1]
        if latest > previous * 1.05 and latest - previous > devices:
            trend = "growing"
        elif latest < previous * 0.95:
            trend = "shrinking"
        else:
            trend = "flat"
    elif len(steady_windows) >= 2:
        half = len(steady_windows) // 2
        first_avg = sum(w["outbox_frames"] for w in steady_windows[:half]) / half
        last_avg = (sum(w["outbox_frames"] for w in steady_windows[half:])
                    / (len(steady_windows) - half))
        if last_avg > first_avg * 1.05 and last_avg - first_avg > devices:
            trend = "growing"
        elif last_avg < first_avg * 0.95:
            trend = "shrinking"
        else:
            trend = "flat"
    # 聚合速率必须是"窗口总确认量 ÷ 窗口总时长"，不能对各窗口速率取算术平均：
    # 窗口长度由实际采样时间决定，长短不一，简单平均会被短窗口带偏。
    steady_confirmed_total = sum(window["confirmed_delta"] for window in steady_windows)
    steady_span_total = sum(window["span_s"] for window in steady_windows)
    steady_input_total = sum(window["input_delta"] for window in steady_windows)
    # 峰值类指标同样只在本阶段的窗口里取：把别的阶段（例如预置积压）算进来，
    # 会把"生产期最大积压"报成预置积压量，让稳态结论失去意义。
    phase_windows = [window for window in windows if window.get("phase") == phase] or windows
    return {
        "windows": windows,
        "trend": trend,
        "trend_segments": segments,
        "warmup_s": warmup_s,
        "steady_windows": len(steady_windows),
        "phase_windows": len(phase_windows),
        "max_outbox_frames": max(window["outbox_frames"] for window in phase_windows),
        "max_outbox_oldest_age_ms": max(window["outbox_oldest_age_ms"]
                                        for window in phase_windows),
        "max_input_fps": max(window["input_fps"] for window in phase_windows),
        "min_input_fps": min(window["input_fps"] for window in phase_windows),
        "max_replay_confirmed_fps": max(window["replay_confirmed_fps"]
                                        for window in phase_windows),
        # 稳态确认速率 = 预热后各窗口 Δ已确认逻辑帧之和 ÷ 各窗口 Δt 之和。
        # 异步流水线在停止输入瞬间还有正常在途尾批，用"生产期累计确认 ÷ 生产总时长"
        # 会把这份正常延迟误判成容量不足。
        "steady_confirmed_fps": (
            round(steady_confirmed_total / steady_span_total, 2)
            if steady_span_total > 0 else None),
        "steady_input_fps": (
            round(steady_input_total / steady_span_total, 2)
            if steady_span_total > 0 else None),
        "steady_span_s": round(steady_span_total, 3),
        "steady_outbox_max": (
            max(window["outbox_frames"] for window in steady_windows)
            if steady_windows else None),
    }
