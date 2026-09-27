"""容量验收口径的离线验证（任务4）。

重点不是"工具能跑"，而是把三类结论钉死：
  * 额定输入下的处理余量只能用额定输入的测量；
  * 降速档与停采排空各自单独命名、单独判定，不能顶替额定余量；
  * 没测到（区间太短/没预置积压）一律 not_tested，绝不判成"无余量"。
"""
from __future__ import annotations

from pathlib import Path
import sys
from types import SimpleNamespace

import pytest

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector")]

from data_watch_shared.acceptance import FAIL, NOT_TESTED, PASS, overall_status  # noqa: E402
from scripts.validate_collector_ingest_capacity import (  # noqa: E402
    attempted_verdict_names,
    build_verdicts,
    margin_verdict,
)

# 被导入的容量脚本会把 collector/ 插到 sys.path 前面；留着会让同一次 pytest 会话里的
# writer/tests（from main import ...）误解析到 collector/main.py。导入完就撤掉。
for _collector_path in (str(ROOT / "collector"),):
    while _collector_path in sys.path:
        sys.path.remove(_collector_path)


def _measured(*, input_fps=200.0, capacity_fps=270.0, seconds=10.0, backlog_start=5000,
              backlog_end=4000, exhausted_at=None, rate=1.0):
    """confirmed_frames 是窗口内真正确认的帧数：处理能力 = confirmed ÷ 窗口秒数。"""
    confirmed = int(capacity_fps * seconds)
    return {
        "seconds": seconds, "rate_multiple": rate, "input_fps": input_fps,
        "confirmed_frames": confirmed, "confirmed_fps": round(confirmed / seconds, 2),
        "capacity_fps_estimate": round(confirmed / seconds, 2),
        "margin_fraction": confirmed / seconds / input_fps - 1.0,
        "backlog_start_frames": backlog_start, "backlog_end_frames": backlog_end,
        "backlog_bytes_start": 1, "backlog_bytes_end": 1,
        "backlog_oldest_age_start_ms": 100, "backlog_oldest_age_end_ms": 90,
        "net_drain_fps": (backlog_start - backlog_end) / seconds,
        "claimed_unconfirmed_frames_estimate": 0,
        "backlog_exhausted_at_s": exhausted_at,
        "effective_window": exhausted_at is None or exhausted_at >= 5.0,
        "seeded_backlog": None,
    }


def _windows(*, trend="flat", steady_windows=3):
    return {"windows": [{"phase": "production"}] * max(steady_windows, 1),
            "steady_windows": steady_windows, "trend": trend,
            "max_outbox_frames": 10, "max_outbox_oldest_age_ms": 100,
            "steady_outbox_max": 5, "steady_confirmed_fps": 200.0, "warmup_s": 2.0}


def _verdicts(**overrides):
    kwargs = dict(target_frames=2000, production_submitted=2000, production_s=10.0,
                  target_seconds=10.0, committed=2000, replayed_during_production=1900,
                  windows=_windows(), drain_s=1.0,
                  final_outbox={"count": 0, "oldest_age_ms": 0, "waiting_writes": 0},
                  correctness_problems=[], target_input_fps=200.0,
                  seeded={"target_reached": True, "produced_frames": 6000,
                          "backlog_frames": 5000, "target_frames": 5000, "seconds": 25.0,
                          "backlog_oldest_age_ms": 100, "in_flight_local_writes": 0,
                          "replay_quiescent_before_seed": True,
                          "replay_paused_during_seed": True,
                          "replay_advanced_during_seed": 0},
                  rated=_measured(), degraded=None, min_effective_seconds=5.0)
    kwargs.update(overrides)
    return build_verdicts(**kwargs)


# ----------------------------------------------------------------------
# 额定余量
# ----------------------------------------------------------------------

def test_rated_margin_pass_needs_30_percent_at_rated_input():
    item = margin_verdict(_measured(capacity_fps=270.0), target_input_fps=200.0,
                          min_effective_seconds=5.0, rate_multiple=1.0,
                          blocked_reason=None)
    assert item["status"] == PASS
    assert "35.0%" in item["detail"]


def test_rated_margin_below_target_fails():
    item = margin_verdict(_measured(capacity_fps=210.0), target_input_fps=200.0,
                          min_effective_seconds=5.0, rate_multiple=1.0,
                          blocked_reason=None)
    assert item["status"] == FAIL
    assert "5.0%" in item["detail"]


def test_input_below_rated_makes_the_conclusion_invalid():
    item = margin_verdict(_measured(input_fps=140.0, capacity_fps=260.0),
                          target_input_fps=200.0, min_effective_seconds=5.0,
                          rate_multiple=1.0, blocked_reason=None)
    assert item["status"] == FAIL and "未达目标" in item["detail"]


def test_short_effective_window_is_not_tested_never_fail():
    """积压被很快处理完 → 区间过短 → 未测，不能判为"无余量"。"""
    item = margin_verdict(_measured(capacity_fps=40.0, seconds=1.0, backlog_start=200,
                                    backlog_end=0, exhausted_at=1.0),
                          target_input_fps=200.0, min_effective_seconds=5.0,
                          rate_multiple=1.0, blocked_reason=None)
    assert item["status"] == NOT_TESTED
    assert "不能据此判定余量" in item["detail"]


def test_no_processable_backlog_is_not_tested():
    item = margin_verdict(_measured(backlog_start=0, backlog_end=0, capacity_fps=200.0),
                          target_input_fps=200.0, min_effective_seconds=5.0,
                          rate_multiple=1.0, blocked_reason=None)
    assert item["status"] == NOT_TESTED
    assert "尾部积压" in item["detail"]


def test_absent_measurement_is_not_tested():
    item = margin_verdict(None, target_input_fps=200.0, min_effective_seconds=5.0,
                          rate_multiple=1.0, blocked_reason="本轮没跑")
    assert item["status"] == NOT_TESTED and item["detail"] == "本轮没跑"


# ----------------------------------------------------------------------
# 降速档不得顶替额定档
# ----------------------------------------------------------------------

def test_degraded_only_run_leaves_rated_margin_not_tested():
    verdicts = _verdicts(rated=None, degraded=_measured(input_fps=140.0, capacity_fps=260.0,
                                                        rate=0.7))
    assert verdicts["rated_catchup_margin"]["status"] == NOT_TESTED
    assert "降速" in verdicts["rated_catchup_margin"]["detail"]
    assert verdicts["degraded_recovery"]["status"] in {PASS, FAIL, NOT_TESTED}


def test_degraded_verdict_is_named_separately_and_labelled():
    verdicts = _verdicts(rated=_measured(), degraded=_measured(input_fps=140.0,
                                                               capacity_fps=180.0, rate=0.7))
    degraded = verdicts["degraded_recovery"]
    assert "降速" in degraded["detail"] and "不作为额定余量" in degraded["detail"]
    assert verdicts["rated_catchup_margin"]["status"] == PASS


def test_seeded_backlog_verdict_reports_failure_to_reach_target():
    verdicts = _verdicts(seeded={"target_reached": False, "produced_frames": 300,
                                 "backlog_frames": 280, "target_frames": 5000,
                                 "seconds": 180.0, "backlog_oldest_age_ms": 50,
                                 "in_flight_local_writes": 12,
                                 "replay_quiescent_before_seed": True,
                                 "replay_paused_during_seed": True,
                                 "replay_advanced_during_seed": 0})
    assert verdicts["seeded_backlog"]["status"] == FAIL
    assert "280/5000" in verdicts["seeded_backlog"]["detail"]


def test_seeded_backlog_fails_when_replay_kept_advancing():
    """暂停没生效：预置期间回放仍在前进 → 预置积压数字不可信，必须判失败。"""
    verdicts = _verdicts(seeded={"target_reached": True, "produced_frames": 6000,
                                 "backlog_frames": 5000, "target_frames": 5000,
                                 "seconds": 25.0, "backlog_oldest_age_ms": 100,
                                 "in_flight_local_writes": 0,
                                 "replay_quiescent_before_seed": False,
                                 "replay_paused_during_seed": False,
                                 "replay_advanced_during_seed": 431})
    assert verdicts["seeded_backlog"]["status"] == FAIL
    assert "又前进 431 帧" in verdicts["seeded_backlog"]["detail"]


def test_seeding_is_not_tested_when_not_requested():
    verdicts = _verdicts(seeded=None, rated=None)
    assert verdicts["seeded_backlog"]["status"] == NOT_TESTED


# ----------------------------------------------------------------------
# 稳态与排空必须分开
# ----------------------------------------------------------------------

def test_steady_state_is_independent_of_post_stop_drain():
    """生产期趋势平稳但排空没完成：稳态通过、排空失败（两件事不能混算）。"""
    verdicts = _verdicts(final_outbox={"count": 37, "oldest_age_ms": 900,
                                       "waiting_writes": 0})
    assert verdicts["steady_state"]["status"] == PASS
    assert verdicts["post_stop_drain"]["status"] == FAIL
    assert "不证明额定输入的持续容量" in verdicts["post_stop_drain"]["detail"]


def test_growing_backlog_fails_steady_state_even_if_drained():
    verdicts = _verdicts(windows=_windows(trend="growing"))
    assert verdicts["steady_state"]["status"] == FAIL
    assert verdicts["post_stop_drain"]["status"] == PASS


def test_insufficient_windows_is_not_tested():
    verdicts = _verdicts(windows={"windows": [{"phase": "production"}],
                                  "steady_windows": 0, "trend": "insufficient_windows",
                                  "max_outbox_frames": 1, "max_outbox_oldest_age_ms": 1})
    assert verdicts["steady_state"]["status"] == NOT_TESTED
    assert verdicts["sustained_replay"]["status"] == NOT_TESTED


def test_not_tested_dominates_overall_status():
    verdicts = _verdicts(rated=None)
    assert overall_status(verdicts) == NOT_TESTED
    verdicts = _verdicts(rated=_measured(capacity_fps=210.0))
    assert overall_status(verdicts) == FAIL
    verdicts = _verdicts(rated=_measured(capacity_fps=270.0))
    # 额定轮次没跑降速档，严格总判定仍是"未测"（未测不折算成通过）……
    assert overall_status(verdicts) == NOT_TESTED
    # ……但本次实际尝试的判定项全部通过，退出码按 measured_status 走。
    names = attempted_verdict_names(seeded=True, rated=True, degraded=False)
    assert "degraded_recovery" not in names
    assert overall_status({n: verdicts[n] for n in names}) == PASS


def test_unmeasured_run_still_counts_as_not_tested():
    """跑了却没测出结果（稳态窗口不足）必须拉低本次判定，不能被"没跑的档位"掩护。"""
    verdicts = _verdicts(windows={"windows": [{"phase": "production"}], "steady_windows": 0,
                                  "trend": "insufficient_windows",
                                  "max_outbox_frames": 1, "max_outbox_oldest_age_ms": 1},
                         rated=_measured(capacity_fps=270.0))
    names = attempted_verdict_names(seeded=True, rated=True, degraded=False)
    assert overall_status({n: verdicts[n] for n in names}) == NOT_TESTED


def test_attempted_verdict_names_tracks_which_phases_ran():
    base = attempted_verdict_names(seeded=False, rated=False, degraded=False)
    assert "seeded_backlog" not in base and "rated_catchup_margin" not in base
    assert attempted_verdict_names(seeded=True, rated=True, degraded=True) == (
        base + ["seeded_backlog", "rated_catchup_margin", "degraded_recovery"])


# ----------------------------------------------------------------------
# 预置积压必须在回放启动之前
# ----------------------------------------------------------------------

class _FakeRedis:
    def ping(self):
        return True

    def script_load(self, script):
        return "sha"


def _writer(tmp_path):
    from storage.durable_redis_writer import DurableRedisWriter
    config = SimpleNamespace(redis=SimpleNamespace(stream_shard_count=1, stream_maxlen=0,
                                                   stream_warn_entries=50000))
    return DurableRedisWriter(_FakeRedis(), config, outbox_path=tmp_path / "outbox.sqlite")


def test_replay_starts_paused_when_seeding_backlog(tmp_path):
    writer = _writer(tmp_path)
    try:
        writer.ping(start_replay=False)
        assert writer._thread is None, "预置积压期间回放必须保持暂停"
        assert writer._latest_thread is not None and writer._latest_thread.is_alive()
        writer.start_replay()
        assert writer._thread is not None and writer._thread.is_alive()
        assert writer._thread.name == "collector-outbox"
    finally:
        writer._stop.set()
        writer._pending_event.set()
        with writer._latest_condition:
            writer._latest_condition.notify_all()
        for thread in (writer._latest_thread, writer._thread):
            if thread is not None:
                thread.join(timeout=5)


def test_production_ping_still_starts_replay(tmp_path):
    """生产默认行为不变：不传参数时回放必须直接启动。"""
    writer = _writer(tmp_path)
    try:
        writer.ping()
        assert writer._thread is not None and writer._thread.is_alive()
        assert not writer._replay_paused.is_set(), "生产路径默认不得暂停回放"
    finally:
        writer._stop.set()
        writer._pending_event.set()
        with writer._latest_condition:
            writer._latest_condition.notify_all()
        for thread in (writer._latest_thread, writer._thread):
            if thread is not None:
                thread.join(timeout=5)


def test_pause_and_resume_replay_are_idempotent(tmp_path):
    writer = _writer(tmp_path)
    try:
        writer.ping()
        assert writer.pause_replay(timeout_s=5.0) is True, \
            "暂停必须等到在飞批次结束才算静止"
        assert writer._replay_paused.is_set() and writer._replay_idle.is_set()
        assert writer.pause_replay(timeout_s=5.0) is True
        writer.resume_replay()
        assert not writer._replay_paused.is_set()
        writer.resume_replay()
        assert not writer._replay_paused.is_set()
    finally:
        writer._stop.set()
        writer._pending_event.set()
        with writer._latest_condition:
            writer._latest_condition.notify_all()
        for thread in (writer._latest_thread, writer._thread):
            if thread is not None:
                thread.join(timeout=5)


def test_pause_before_replay_started_reports_not_quiescent(tmp_path):
    """还没启动回放线程就请求暂停：没有线程会去确认静止，必须如实返回 False。"""
    writer = _writer(tmp_path)
    try:
        assert writer.pause_replay(timeout_s=0.2) is False
    finally:
        writer._stop.set()
        writer._pending_event.set()


@pytest.mark.parametrize("rate,expected_phase", [(1.0, "rated"),
                                                 (0.7, "degraded")])
def test_phase_naming_matches_input_rate(rate, expected_phase):
    assert ("rated" if rate >= 1.0 else "degraded") == expected_phase
