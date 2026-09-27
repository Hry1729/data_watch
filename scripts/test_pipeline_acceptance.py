"""共享验收规则与完整链路校验器的离线验证（故障注入）。

要求：不能只证明"正常数据能通过"，必须证明校验器**能抓到错误**。
这里用确定性夹具直接构造被破坏的数据/期望：
中间点被改动、NULL 方向反了、错映射、重复行、遗漏行、多出设备、
质量被写成恒定值、未测状态被当成通过。
"""
from __future__ import annotations

import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[1]
# 只需要仓库根：把 collector/ 或 writer/ 插到 sys.path 会让同一次 pytest 会话里
# 另一侧的 `from main import ...` 解析错模块。
sys.path[:0] = [str(ROOT)]

from data_watch_shared import acceptance as acc  # noqa: E402

DEVICES = ["pcABCD1234d000", "pcABCD1234d001"]
POINTS = 8
TICKS = 6
PERIOD_MS = 100
BASE_TS = 1_700_000_000_000


def _expectations(devices=DEVICES, points=POINTS, ticks=TICKS):
    expected_columns = [acc.tdengine_value_column(p) for p in range(1, points + 1)
                        if p not in acc.NEVER_STORED]
    expectations = {}
    for device in devices:
        for tick in range(ticks):
            expectations[(device, BASE_TS + tick * PERIOD_MS)] = {
                column: acc.expected_value_cell(device, int(column[1:]), tick)
                for column in expected_columns
            }
    return expected_columns, expectations


def _rows_from(expectations):
    return [{"device_code": device, "ts": ts, "cells": dict(cells)}
            for (device, ts), cells in expectations.items()]


def _quality_expectations(devices=DEVICES, points=POINTS, ticks=TICKS):
    expected = {}
    for device in devices:
        for tick in range(ticks):
            for point in range(1, points + 1):
                cell = acc.expected_quality_cell(device, point, tick)
                if cell is not None:
                    expected[(device, BASE_TS + tick * PERIOD_MS, point)] = cell
    return expected


def _quality_rows(expected):
    return [{"device_code": device, "ts": ts, "var_id": var_id, "quality": quality}
            for (device, ts, var_id), quality in expected.items()]


# ----------------------------------------------------------------------
# 基线：正确数据必须通过（否则"能抓到错误"没有意义）
# ----------------------------------------------------------------------

def test_correct_data_has_no_problems():
    expected_columns, expectations = _expectations()
    assert acc.reconcile_value_rows(_rows_from(expectations), expectations,
                                    expected_columns=expected_columns) == []
    quality = _quality_expectations()
    assert acc.reconcile_quality_rows(_quality_rows(quality), quality) == []


def test_expectations_distinguish_device_point_and_tick():
    """同一 (point, tick) 在不同设备上必须不同值，否则跨设备误归属查不出来。"""
    values = {acc.expected_stream_value(device, 5, 3) for device in DEVICES}
    assert len(values) == len(DEVICES), f"设备间期望值撞车：{values}"
    assert acc.expected_stream_value(DEVICES[0], 5, 3) != acc.expected_stream_value(
        DEVICES[0], 5, 4), "采样序号必须体现在期望值里"
    assert acc.expected_stream_value(DEVICES[0], 5, 3) != acc.expected_stream_value(
        DEVICES[0], 6, 3), "点位必须体现在期望值里"


def test_never_stored_and_sometimes_null_semantics():
    """非存储/STRING 点不得落值表；时有时无的 NULL 点必须是存储点。"""
    expected_columns, _ = _expectations()
    for point in acc.NEVER_STORED:
        assert acc.expected_value_cell(DEVICES[0], point, 1) is None
        assert acc.expected_quality_cell(DEVICES[0], point, 1) is None
        assert acc.tdengine_value_column(point) not in expected_columns, \
            "非存储/STRING 点不该出现在值表列里"
    null_ticks = [tick for tick in range(10)
                  if acc.expected_stream_value(DEVICES[0], acc.P_SOMETIMES_NULL, tick) is None]
    assert null_ticks == [4, 9], "每 5 个 tick 有 1 个 NULL，列仍必须存在"
    assert acc.tdengine_value_column(acc.P_SOMETIMES_NULL) in expected_columns
    assert acc.expected_value_cell(DEVICES[0], acc.P_SOMETIMES_NULL, 0) is not None
    assert acc.expected_value_cell(DEVICES[0], acc.P_SOMETIMES_NULL, 4) is None


def test_build_frame_contract_matches_writer_expectation():
    """stream 与 qualities 键必须对齐（writer 侧 v2 契约），latest 覆盖全部点。"""
    frame = acc.build_frame(DEVICES[0], POINTS, 3, PERIOD_MS, BASE_TS)
    assert set(frame["stream_values"]) == set(frame["stream_qualities"])
    assert len(frame["latest"]) == POINTS
    for point in acc.NEVER_STORED:
        assert str(point) not in frame["stream_values"]
        assert str(point) in frame["latest"]
    null_point = str(acc.P_SOMETIMES_NULL)
    assert null_point in frame["stream_values"], "tick=3 是正常 tick，必须有值"
    frame_null = acc.build_frame(DEVICES[0], POINTS, 4, PERIOD_MS, BASE_TS)
    assert null_point not in frame_null["stream_values"], "tick=4 是 NULL tick"
    assert null_point not in frame_null["stream_qualities"], \
        "不进 stream 的点不该有质量行"
    assert frame_null["latest"][null_point]["v"] == ""


# ----------------------------------------------------------------------
# 故障注入：校验器必须抓到
# ----------------------------------------------------------------------

def test_corrupted_middle_point_is_caught():
    expected_columns, expectations = _expectations()
    rows = _rows_from(expectations)
    key = (DEVICES[0], BASE_TS + 3 * PERIOD_MS)
    middle = acc.tdengine_value_column(5)          # p5 在中间，不在首尾
    rows = [dict(row) for row in rows]
    for row in rows:
        if (row["device_code"], row["ts"]) == key:
            row["cells"][middle] = float(row["cells"][middle]) + 0.5
    problems = acc.reconcile_value_rows(rows, expectations,
                                        expected_columns=expected_columns)
    assert problems, "中间点被改动必须被发现"
    assert middle in problems[0] and str(key[1]) in problems[0]


def test_value_where_null_expected_and_null_where_value_expected():
    expected_columns, expectations = _expectations()
    rows = [dict(row) for row in _rows_from(expectations)]
    null_key = (DEVICES[0], BASE_TS + 4 * PERIOD_MS)
    null_column = acc.tdengine_value_column(acc.P_SOMETIMES_NULL)
    for row in rows:
        if (row["device_code"], row["ts"]) == null_key:
            assert row["cells"][null_column] is None
            row["cells"][null_column] = 42.0        # 期望 NULL，实际有值
    problems = acc.reconcile_value_rows(rows, expectations,
                                        expected_columns=expected_columns)
    assert any(null_column in problem for problem in problems), \
        f"期望 NULL 而实际有值必须被发现：{problems}"

    # 反方向：期望有值而实际 NULL
    expected_columns, expectations = _expectations()
    rows = [dict(row) for row in _rows_from(expectations)]
    value_key = (DEVICES[0], BASE_TS + 0 * PERIOD_MS)
    value_column = acc.tdengine_value_column(5)
    for row in rows:
        if (row["device_code"], row["ts"]) == value_key:
            row["cells"][value_column] = None
    problems = acc.reconcile_value_rows(rows, expectations,
                                        expected_columns=expected_columns)
    assert any(value_column in problem for problem in problems)


def test_wrong_mapping_is_caught():
    """点位与列错开一位：值本身都对，但归属错了，必须被发现。"""
    expected_columns, expectations = _expectations()
    shifted = {}
    for (device, ts), cells in expectations.items():
        shifted[(device, ts)] = {
            acc.tdengine_value_column(int(column[1:]) + 1): value
            for column, value in cells.items()
        }
    problems = acc.reconcile_value_rows(_rows_from(expectations), shifted,
                                        expected_columns=expected_columns)
    assert problems, "错映射必须被发现"


def test_cross_device_misattribution_is_caught():
    """把设备 A 的行挂到设备 B：身份必须能发现（A 少一行、B 多一行）。"""
    expected_columns, expectations = _expectations()
    rows = _rows_from(expectations)
    moved = [dict(row, device_code=(DEVICES[1] if row is rows[0] else row["device_code"]))
             for row in rows]
    problems = acc.reconcile_value_rows(moved, expectations,
                                        expected_columns=expected_columns)
    assert any("遗漏" in problem for problem in problems), \
        f"被搬走的设备行必须报遗漏：{problems}"
    assert any("重复" in problem for problem in problems), \
        f"接收方出现重复身份必须报重复：{problems}"


def test_duplicate_rows_are_caught():
    expected_columns, expectations = _expectations()
    rows = _rows_from(expectations)
    problems = acc.reconcile_value_rows(rows + [rows[0]], expectations,
                                        expected_columns=expected_columns)
    assert any("重复" in problem for problem in problems)


def test_missing_column_is_caught():
    expected_columns, expectations = _expectations()
    rows = [dict(row) for row in _rows_from(expectations)]
    dropped = acc.tdengine_value_column(3)
    for row in rows:
        row["cells"].pop(dropped, None)
    problems = acc.reconcile_value_rows(rows, expectations,
                                        expected_columns=expected_columns)
    assert any(dropped in problem for problem in problems)


def test_quality_constant_is_caught():
    """质量恒定 192 的老毛病：期望随点与 tick 变化，恒定值必须被发现。"""
    quality = _quality_expectations()
    assert len(set(quality.values())) > 1, "夹具本身必须覆盖多种质量码"
    problems = acc.reconcile_quality_rows(_quality_rows(quality),
                                          dict.fromkeys(quality, 192))
    assert problems, "恒定质量码必须被发现"


def test_quality_duplicate_and_missing_rows_are_caught():
    quality = _quality_expectations()
    rows = _quality_rows(quality)
    problems = acc.reconcile_quality_rows(rows + [rows[0]], quality)
    assert any("重复" in problem for problem in problems)
    problems = acc.reconcile_quality_rows(rows[:-1], quality)
    assert any("遗漏" in problem for problem in problems)


# ----------------------------------------------------------------------
# 判定口径
# ----------------------------------------------------------------------

def test_overall_status_never_folds_not_tested_into_pass():
    assert acc.overall_status({"a": acc.verdict(acc.PASS, ""),
                               "b": acc.verdict(acc.PASS, "")}) == acc.PASS
    assert acc.overall_status({"a": acc.verdict(acc.PASS, ""),
                               "b": acc.not_tested("没测")}) == acc.NOT_TESTED, \
        "未测不得被折算成通过"
    assert acc.overall_status({"a": acc.verdict(acc.PASS, ""),
                               "b": acc.verdict(acc.FAIL, "")}) == acc.FAIL
    assert acc.overall_status({}) == acc.NOT_TESTED


def test_correctness_verdict_reports_problems():
    assert acc.correctness_verdict([], checked="X")["status"] == acc.PASS
    item = acc.correctness_verdict(["a 不符", "b 不符"], checked="X")
    assert item["status"] == acc.FAIL and "2 项不符" in item["detail"]


def test_window_analysis_uses_total_delta_over_total_span():
    """聚合速率必须是总增量÷总时长，不能被短窗口算术平均带偏。"""
    samples = [
        {"phase": "production", "elapsed_s": 0.0, "submitted": 0, "replayed_frames": 0,
         "outbox": {"count": 5000, "oldest_age_ms": 10}},
        {"phase": "production", "elapsed_s": 0.5, "submitted": 0, "replayed_frames": 700,
         "outbox": {"count": 5000, "oldest_age_ms": 10}},
        {"phase": "production", "elapsed_s": 2.0, "submitted": 0, "replayed_frames": 1900,
         "outbox": {"count": 5000, "oldest_age_ms": 10}},
    ]
    windows = acc.window_analysis(samples, 100, 10, min_span_s=0.2)
    assert windows["steady_confirmed_fps"] == pytest.approx(1900 / 2.0, abs=0.01), \
        "不能用 (1400 + 800) / 2 = 1100 这种算术平均"


def _trend_samples(outbox_series, *, devices=10):
    return [{"phase": "production", "elapsed_s": float(index), "submitted": 100 * index,
             "replayed_frames": 1000 + 100 * index,
             "outbox": {"count": count, "oldest_age_ms": 10}}
            for index, count in enumerate(outbox_series)]


def test_trend_does_not_call_a_one_time_step_growth():
    """新增点位加列造成的台阶式抬升后持平，不是"持续增长"。"""
    series = [100] * 8 + [160] * 8
    windows = acc.window_analysis(_trend_samples(series), 100, 10)
    assert windows["trend"] == "flat", windows["trend_segments"]


def test_trend_detects_continuous_growth():
    series = [100 + 20 * index for index in range(16)]
    windows = acc.window_analysis(_trend_samples(series), 100, 10)
    assert windows["trend"] == "growing", windows["trend_segments"]


def test_trend_detects_shrinking_backlog():
    series = [500 - 25 * index for index in range(16)]
    windows = acc.window_analysis(_trend_samples(series), 100, 10)
    assert windows["trend"] == "shrinking", windows["trend_segments"]


def test_trend_uses_only_the_requested_phase():
    samples = _trend_samples([100] * 8)
    samples += [{"phase": "seed", "elapsed_s": float(index), "submitted": 0,
                 "replayed_frames": 0, "outbox": {"count": 6000, "oldest_age_ms": 10}}
                for index in range(8)]
    windows = acc.window_analysis(samples, 100, 10)
    assert windows["trend"] == "flat"
    assert windows["max_outbox_frames"] == 100, "预置积压阶段不得污染生产期峰值"


def test_window_analysis_separates_phases_and_warmup():
    samples = [
        {"phase": "production", "elapsed_s": 0.0, "submitted": 0, "replayed_frames": 0,
         "outbox": {"count": 10, "oldest_age_ms": 1}},
        {"phase": "production", "elapsed_s": 1.0, "submitted": 100, "replayed_frames": 50,
         "outbox": {"count": 12, "oldest_age_ms": 1}},
        {"phase": "production", "elapsed_s": 2.0, "submitted": 200, "replayed_frames": 90,
         "outbox": {"count": 12, "oldest_age_ms": 1}},
        {"phase": "drain", "elapsed_s": 0.0, "submitted": 200, "replayed_frames": 90,
         "outbox": {"count": 12, "oldest_age_ms": 1}},
        {"phase": "drain", "elapsed_s": 1.0, "submitted": 200, "replayed_frames": 130,
         "outbox": {"count": 0, "oldest_age_ms": 0}},
    ]
    windows = acc.window_analysis(samples, 100, 10, warmup_s=0.5)
    # 预热边界按窗口起点判定：0.0→1.0 的窗口跨过预热期，不计入稳态。
    assert windows["steady_windows"] == 1, "只应统计 production 阶段预热后开始的窗口"
    assert windows["steady_confirmed_fps"] == pytest.approx(40.0), \
        "只算生产阶段预热后的确认量，不能把排空阶段的确认算进来"
    drain_windows = [w for w in windows["windows"] if w["phase"] == "drain"]
    assert len(drain_windows) == 1 and drain_windows[0]["confirmed_delta"] == 40
    # 峰值只取本阶段：排空阶段积压归零，不能被当成"生产期最大积压"。
    assert windows["max_outbox_frames"] == 12
    assert windows["min_input_fps"] == 100.0, "排空阶段输入 0 不得计入生产期输入下限"


# ----------------------------------------------------------------------
# 固定延迟阈值：必须按阈值内样本占比判定，不能拿直方图桶上界当精确分位数
# ----------------------------------------------------------------------

def test_latency_threshold_uses_sample_counts_not_bucket_upper_bound():
    """阈值内占比是判据；分位数只报所在桶区间，不能拿桶上界当精确 P99。"""
    samples = [500.0] * 9900 + [1200.0] * 100      # 恰好 1% 超出 → 99% 阈值内
    report = acc.latency_threshold_report(samples, 1000.0,
                                          buckets=(500, 1000, 1500, float("inf")))
    assert report["total_samples"] == 10000
    assert report["within_budget_samples"] == 9900
    assert report["over_budget_samples"] == 100
    assert report["within_ratio"] == pytest.approx(0.99)
    assert report["status"] == acc.PASS
    # 9900 个样本 ≤500ms，p99 落在 [0,500] 桶：桶上界（500）与最大值（1200）
    # 都不是"精确 P99"，所以判定必须用样本计数。
    assert report["p99_bucket_interval_ms"] == [0, 500]
    assert report["max_ms"] == 1200.0


def test_latency_threshold_fails_when_more_than_one_percent_exceed():
    samples = [500.0] * 9800 + [1200.0] * 200      # 2% 超出
    report = acc.latency_threshold_report(samples, 1000.0,
                                          buckets=(500, 1000, 1500, float("inf")))
    assert report["over_budget_samples"] == 200
    assert report["status"] == acc.FAIL
    # 只报"P99 桶上界 1500ms"会把"其实 98% 在 1000ms 内"这件事说糊；
    # 阈值内占比 0.98 < 0.99 才是判据。
    assert report["within_ratio"] == pytest.approx(0.98)
    assert report["p99_bucket_interval_ms"] == [1000, 1500]


def test_latency_threshold_without_samples_is_not_tested():
    report = acc.latency_threshold_report([], 1000.0)
    assert report["status"] == acc.NOT_TESTED, "没有样本不得判通过"
    assert report["total_samples"] == 0


# ----------------------------------------------------------------------
# 队列守恒：A/D/Q 对账，差额必须能被解释
# ----------------------------------------------------------------------

def _conservation_windows(accepted, confirmed, queue, *, claimed=None, start=0.0, step=1.0):
    """claimed 是队列条数里"此刻正在投递"的在途子集（≤ queue），不是相加项。"""
    frames = []
    for index, (a, c, q) in enumerate(zip(accepted, confirmed, queue)):
        claim = 0 if claimed is None else claimed[index]
        frames.append({"elapsed_s": start + index * step, "accepted": a, "confirmed": c,
                       "queue_count": q, "claimed_frames": claim, "waiting_writes": 0,
                       "oldest_age_ms": 100})
    return frames


def test_conservation_matches_when_queue_change_equals_accepted_minus_confirmed():
    windows = _conservation_windows([0, 200, 400, 600], [0, 100, 200, 300],
                                    [0, 100, 200, 300], claimed=[0, 40, 60, 20])
    report = acc.queue_conservation(windows)
    assert report["accepted_delta"] == 600
    assert report["confirmed_delta"] == 300
    assert report["outstanding_end_frames"] == 300
    assert report["counter_residual_frames"] == 0
    assert report["identity_residual_frames"] == 0
    assert report["claimed_is_subset_of_queue"] is True
    assert report["status"] == acc.PASS
    assert report["net_growth_fps"] == pytest.approx(100.0), \
        "净增长 = (ΔA−ΔD)/总时长，必须用总增量÷总时长"


def test_conservation_accepts_in_flight_rows_inside_the_queue_count():
    """已领取未清理的行仍算在队列条数里：claimed 不能当相加项。

    线上 30 分钟长测的原始数据就是这样：outstanding == queue_count，
    而 claimed 是其中的在途子集；第一版实现把 claimed 当相加项，
    残差恒等于 −claimed（本轮据此修正）。
    """
    windows = _conservation_windows([0, 200, 400, 600], [0, 100, 200, 300],
                                    [0, 100, 200, 300], claimed=[0, 80, 120, 200])
    report = acc.queue_conservation(windows)
    assert report["outstanding_end_frames"] == 300
    assert report["claimed_end_frames"] == 200
    assert report["identity_residual_frames"] == 0
    assert report["status"] == acc.PASS


def test_conservation_fails_when_claimed_exceeds_queue_count():
    """claimed 必须是 count 的子集：超过就说明计数口径错了。"""
    windows = _conservation_windows([0, 200, 400, 600], [0, 100, 200, 300],
                                    [0, 100, 200, 300], claimed=[0, 150, 250, 400])
    report = acc.queue_conservation(windows)
    assert report["claimed_is_subset_of_queue"] is False
    assert report["status"] == acc.FAIL
    assert any("子集" in problem for problem in report["problems"])


def test_conservation_fails_when_third_path_hides_rows():
    """第三路径（既非接收也非清理）造成的差额不得消失。"""
    # Δ(A−D) = 300，但队列条数只增加 200 → 100 帧不知去向
    windows = _conservation_windows([0, 200, 400, 600], [0, 100, 200, 300],
                                    [0, 60, 130, 200], claimed=[0, 20, 40, 60])
    report = acc.queue_conservation(windows)
    assert report["identity_residual_frames"] == 100
    assert report["status"] == acc.FAIL
    assert report["problems"], "必须给出具体差额说明"


def test_conservation_without_queue_counts_is_flagged():
    """没有队列条数时不能假装对账通过。"""
    windows = [{"elapsed_s": float(index), "accepted": 100 * index,
                "confirmed": 50 * index}
               for index in range(5)]
    report = acc.queue_conservation(windows)
    assert report["identity_residual_frames"] is None
    assert report["status"] == acc.NOT_TESTED or report["status"] == acc.FAIL


def test_conservation_flags_sustained_growth_in_the_tail_only():
    """尾部仍在增长才叫持续；前段增长后段收敛应判锯齿/收敛。"""
    growing = _conservation_windows([0, 200, 400, 600, 800, 1000, 1200, 1400],
                                    [0, 100, 200, 300, 400, 490, 580, 670],
                                    [0, 100, 200, 300, 400, 510, 620, 730])
    assert acc.queue_conservation(growing)["classification"] == "sustained_growth"
    converging = _conservation_windows([0, 200, 400, 600, 800, 1000, 1200, 1400],
                                       [0, 100, 300, 500, 700, 900, 1100, 1300],
                                       [0, 100, 100, 100, 100, 100, 100, 100])
    assert acc.queue_conservation(converging)["classification"] == "sawtooth_or_converging"


# ----------------------------------------------------------------------
# 回放批次时间线：等待发生在当前批还是"上一批挡住下一批"
# ----------------------------------------------------------------------

def _timeline_record(index, *, claim, deliver_ms, gap_ms, queue_age, frames=200,
                     prev_claim=None, prev_confirmed=None):
    write_sent = claim + gap_ms / 1000.0
    confirmed = write_sent + deliver_ms / 1000.0
    return {"claim_at": claim, "write_sent_at": write_sent,
            "aof_confirmed_at": confirmed, "frames": frames, "bytes": 1024,
            "queue_frames_at_claim": 300,
            "queue_oldest_age_ms_at_claim": queue_age,
            "prev_claim_at": prev_claim, "prev_aof_confirmed_at": prev_confirmed,
            "deliver_ms": deliver_ms,
            "write_ms": (write_sent - claim) * 1000}


def test_batch_timeline_identifies_previous_confirmation_blocking_next_claim():
    """上一批确认一返回就立刻领取下一批（gap≈0）+ 确认往返占满周期 → 上一批阻塞下一批。"""
    records = []
    for index in range(6):
        claim = index * 1.0
        records.append(_timeline_record(
            index, claim=claim, deliver_ms=1000.0, gap_ms=0.0, queue_age=900,
            prev_claim=(claim - 1.0) if index else None,
            prev_confirmed=(claim - 0.001) if index else None))
    analysis = acc.batch_timeline_analysis(records)
    assert analysis["dominant_wait"] == "previous_batch_confirmation_blocks_next_claim"
    assert analysis["deliver_ms"]["p50"] == 1000.0
    assert analysis["claim_gap_ms"]["p50"] == pytest.approx(0.0)
    assert analysis["queue_oldest_age_ms_at_claim"]["p50"] == 900
    assert "不能只归因于当前批" in analysis["interpretation"]
    # 客户端无法把"脚本执行"与"AOF 等待"从一次往返里拆开，因此不报 aof_wait_ms。
    assert "aof_wait_ms" not in analysis


def test_batch_timeline_without_records_is_not_tested():
    analysis = acc.batch_timeline_analysis([])
    assert analysis["dominant_wait"] == "not_tested"
    assert analysis["batch_count"] == 0


def test_quality_null_and_fractional_codes_are_reported_as_mismatches():
    expected = {(DEVICES[0], BASE_TS, 4): 2}
    for invalid in (None, 2.5):
        rows = [{"device_code": DEVICES[0], "ts": BASE_TS,
                 "var_id": 4, "quality": invalid}]
        assert acc.reconcile_quality_rows(rows, expected)


def test_conservation_tail_rate_weights_unequal_windows():
    # 尾段两窗：9秒增长90帧、1秒减少20帧，净增长7帧/秒。
    # 直接平均窗口速度会得到-5，错误地宣称收敛。
    elapsed = [0, 1, 2, 3, 4, 5, 6, 15, 16]
    outstanding = [100, 100, 100, 100, 100, 100, 100, 190, 170]
    rows = [{"elapsed_s": t, "accepted": 1000 + t * 100,
             "confirmed": 1000 + t * 100 - q, "queue_count": q,
             "claimed_frames": 0} for t, q in zip(elapsed, outstanding)]
    result = acc.queue_conservation(rows)
    assert result["tail_quarter_mean_growth_fps"] == 7
    assert result["classification"] == "sustained_growth"


def test_conservation_does_not_hide_a_constant_residual_at_high_volume():
    rows = _conservation_windows([1000000, 2000000, 3000000],
                                 [999900, 1999900, 2999900], [90, 90, 90])
    result = acc.queue_conservation(rows)
    assert result["identity_residual_frames"] == 0
    assert result["status"] == acc.FAIL
    assert result["tolerance_frames"] == 0


def test_conservation_checks_middle_samples_not_only_endpoints():
    rows = _conservation_windows([100, 200, 300], [0, 100, 200], [100, 90, 100])
    result = acc.queue_conservation(rows)
    assert result["identity_residual_frames"] == 0
    assert result["status"] == acc.FAIL
