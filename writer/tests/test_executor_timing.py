"""ExecutorTiming 三段拆分与阶段层级累计的单元测试。"""

from __future__ import annotations

import time
import pytest

from performance import STAGE_CONTAINMENT, ExecutorTiming, WriterPerformanceMetrics


def _record(metrics, name, work, repeats=1, queue_sleep=0.0, resume_gap=0.0):
    for _ in range(repeats):
        timing = ExecutorTiming()
        timing.queued_at = time.perf_counter()
        if queue_sleep:
            time.sleep(queue_sleep)
        timing.wrap(work)()
        timing.record(metrics, name, time.perf_counter() + resume_gap)


def test_executor_timing_splits_queue_call_and_resume():
    """排队 / 同步调用 / 事件循环恢复必须是三段，且互不重叠。"""
    metrics = WriterPerformanceMetrics()
    _record(metrics, "td_execute", lambda: time.sleep(0.01), queue_sleep=0.02, resume_gap=0.005)

    bucket = metrics.snapshot()["timings"]["executor_ms"]["td_execute"]
    assert bucket["calls"] == 1
    assert bucket["queue_ms"] >= 15.0
    assert bucket["call_ms"] >= 8.0
    assert bucket["resume_ms"] >= 3.0
    total = bucket["queue_ms"] + bucket["call_ms"] + bucket["resume_ms"]
    assert abs(bucket["total_ms"] - total) <= 0.01


def test_executor_timing_does_not_call_a_waiting_jump_cpu_time():
    """睡着的调用墙上时间不短、线程 CPU 时间接近零——两者必须可区分。"""
    metrics = WriterPerformanceMetrics()

    def sleepy():
        time.sleep(0.03)
        return 1

    _record(metrics, "td_execute_wait", sleepy)
    bucket = metrics.snapshot()["timings"]["executor_ms"]["td_execute_wait"]
    assert bucket["call_ms"] >= 20.0
    assert bucket["call_cpu_ms"] < bucket["call_ms"] * 0.5


def test_executor_timing_cpu_time_aggregates_over_many_calls():
    """Windows 线程 CPU 时钟粒度粗（约 15.6ms），单次不可信，按批累计才有意义。"""
    metrics = WriterPerformanceMetrics()

    def compute():
        return sum(range(50_000))

    _record(metrics, "build", compute, repeats=50)
    bucket = metrics.snapshot()["timings"]["executor_ms"]["build"]
    assert bucket["calls"] == 50
    assert bucket["call_cpu_ms"] > 0.0
    # CPU 时间属于墙上时间的子集，允许粒度带来的少量超出
    assert bucket["call_cpu_ms"] <= bucket["call_ms"] + 20.0


def test_call_that_finishes_after_the_await_is_marked_incomplete_only():
    """超时/取消后线程才结束：记为未完成、不进完成样本，也不串到下一批。"""
    import threading

    metrics = WriterPerformanceMetrics()
    release = threading.Event()

    timing = ExecutorTiming()
    timing.queued_at = time.perf_counter()
    thread = threading.Thread(target=timing.wrap(lambda: release.wait(5.0)))
    thread.start()
    time.sleep(0.05)  # 让线程真的开始调用
    timing.record(metrics, "td_execute", time.perf_counter())  # await 放弃的那一刻

    first = metrics.snapshot()["timings"]["executor_ms"]["td_execute"]
    assert first["calls"] == 0
    assert first["incomplete_calls"] == 1
    assert first["call_ms"] == 0.0
    assert first["incomplete_call_ms"] > 0.0

    release.set()
    thread.join(timeout=5)
    # 后台线程收尾后，已经记完的账不得再变
    assert metrics.snapshot()["timings"]["executor_ms"]["td_execute"] == first


def test_record_stage_accumulates_and_ignores_negative():
    metrics = WriterPerformanceMetrics()
    metrics.record_stage("read_claim_ms", 1.5)
    metrics.record_stage("read_claim_ms", 2.5)
    metrics.record_stage("read_claim_ms", -1.0)

    stages = metrics.snapshot()["timings"]["stages_ms"]
    assert stages["read_claim_ms"] == 4.0


def test_finish_after_cancel_return_is_not_a_completed_sample():
    metrics = WriterPerformanceMetrics()
    timing = ExecutorTiming()
    timing.queued_at = 1.0
    timing.started_at = 1.1
    timing.finished_at = 1.3
    timing.record(metrics, "td_execute", 1.2)
    bucket = metrics.snapshot()["timings"]["executor_ms"]["td_execute"]
    assert bucket["calls"] == 0
    assert bucket["incomplete_calls"] == 1
    assert abs(bucket["incomplete_call_ms"] - 100.0) < 0.001


def test_stage_containment_gives_every_stage_exactly_one_parent():
    """每个阶段只能挂在一个父阶段下，否则同层相加就会重复计数。"""
    owners: dict[str, str] = {}
    for parent, children in STAGE_CONTAINMENT.items():
        for child in children:
            assert child not in owners, f"{child} 同时挂在 {owners[child]} 与 {parent} 下"
            owners[child] = parent
    assert "loop_iteration_ms" not in owners
    # 已知的嵌套关系必须被声明出来
    assert owners["build_ms"] == "flush_outer_ms"
    assert owners["drain_claim_ms"] == "drain_own_ms"
    assert owners["drain_own_ms"] == "loop_iteration_ms"
    assert "drain_outer_ms" not in owners
    assert owners["td_execute_ms"] == "flush_outer_ms"
    assert owners["build_wait_ms"] == "flush_outer_ms"
    assert owners["prepare_ms"] == "read_block_ms"


def test_snapshot_exposes_stage_totals_and_containment():
    metrics = WriterPerformanceMetrics()
    metrics.record_stage("flush_outer_ms", 12.0)

    timings = metrics.snapshot()["timings"]
    assert timings["stages_ms"]["flush_outer_ms"] == 12.0
    assert timings["executor_ms"] == {}
    assert timings["stage_containment"]["flush_outer_ms"] == list(
        STAGE_CONTAINMENT["flush_outer_ms"]
    )


@pytest.mark.asyncio
async def test_scoped_reclaim_counts_claim_and_nested_flush_once(monkeypatch):
    import consumer as consumer_module
    from consumer import StreamConsumer

    instance = StreamConsumer.__new__(StreamConsumer)
    instance._metrics = WriterPerformanceMetrics()
    clock = iter((1.0, 1.1))
    monkeypatch.setattr(consumer_module.time, "perf_counter", lambda: next(clock, 1.1))

    async def reclaim():
        instance._metrics.record_stage("drain_claim_ms", 20.0)
        instance._metrics.record_stage("flush_outer_ms", 30.0)

    await instance._timed_scoped("drain_outer_ms", "drain_own_ms", reclaim)
    stages = instance._metrics.stage_totals()
    assert stages["drain_own_ms"] == pytest.approx(70.0)
    assert stages["drain_other_ms"] == pytest.approx(50.0)
    assert stages["drain_own_ms"] + stages["flush_outer_ms"] == pytest.approx(100.0)
