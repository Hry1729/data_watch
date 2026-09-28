"""正常停机不丢样：确定性同步点回归。

缺陷（基线也存在）：``TaskScheduler.stop()`` 直接 cancel 设备循环，取消点可能落在
``execute_func`` 内部 —— 读取已成功、解析/写 Redis 尚未完成 —— 导致正常停机丢样。
复现证据：``outputs/central-restore-20260928/shutdown-cancel-repro.json``
（``writer_calls: []`` 对比"等当前周期"的 ``["window_stream", "latest"]``）。

修复后的契约（本文件逐条钉住）：
  1. 正常 stop：先停止新增采样，在途周期完成必要写入后才退出；
  2. stop 之后不再新增采样；
  3. 阻塞任务超过 drain_timeout 时有界退出，并留下准确诊断；
  4. 重复 stop、execute 抛错后的清理都安全；
  5. 异常取消能力保留（超时才 cancel）。

同步点全部用 ``asyncio.Event`` 与可注入的 ``_sleep``，不靠 sleep 碰概率。
"""

from __future__ import annotations

import asyncio
import time
from types import SimpleNamespace

import pytest

from collector.scheduler import TaskScheduler
from monitor.performance import PerformanceMetrics


def _task(device_id: str = "dev1", scan_rate: float = 0.05) -> SimpleNamespace:
    return SimpleNamespace(
        device=SimpleNamespace(device_id=device_id, generation=1),
        scan_task=None,
        task_name=f"{device_id}_collect",
        scan_rate=scan_rate,
        error_count=0,
    )


async def _no_sleep(_seconds: float) -> None:
    """去掉真实等待：循环推进只由同步点驱动。"""
    await asyncio.sleep(0)


def _scheduler(metrics: PerformanceMetrics) -> TaskScheduler:
    scheduler = TaskScheduler(metrics=metrics)
    scheduler._sleep = _no_sleep
    return scheduler


@pytest.mark.asyncio
async def test_stop_finishes_inflight_write_without_losing_sample():
    """核心：卡在"读取成功、尚未写入"窗口时正常 stop，在途周期必须写完。"""
    metrics = PerformanceMetrics()
    scheduler = _scheduler(metrics)
    reads: list[int] = []
    writes: list[str] = []
    read_done = asyncio.Event()
    allow_write = asyncio.Event()

    async def execute(_task):
        reads.append(1)
        read_done.set()
        await allow_write.wait()          # ← 同步点：读取已完成，写入尚未发生
        writes.append("window_stream")
        writes.append("latest")

    scheduler.add_tasks([_task()])
    main_task = asyncio.create_task(scheduler.start(execute))
    await asyncio.wait_for(read_done.wait(), timeout=5.0)

    stop_task = asyncio.create_task(scheduler.stop(drain_timeout=5.0))
    await asyncio.sleep(0)                # 让 stop 进入"等在途循环"的状态
    assert not stop_task.done(), "stop 不应立刻返回（在途周期还没写完）"

    reads_before_release = len(reads)
    allow_write.set()                     # 放行在途周期的写入
    result = await asyncio.wait_for(stop_task, timeout=5.0)
    await asyncio.wait_for(main_task, timeout=5.0)

    assert writes == ["window_stream", "latest"], f"在途周期的写入被丢弃：{writes}"
    assert len(reads) == reads_before_release, "放行后不应再新增采样"
    assert result == {"drained": True, "timed_out": 0, "drain_timeout_s": 5.0}
    assert metrics.snapshot()["counters"].get("scheduler_drain_timeout_count", 0) == 0


@pytest.mark.asyncio
async def test_stop_stops_new_sampling():
    """stop 之后不得再新增采样周期。"""
    metrics = PerformanceMetrics()
    scheduler = _scheduler(metrics)
    cycles = 0
    gate = asyncio.Event()

    async def execute(_task):
        nonlocal cycles
        cycles += 1
        if cycles >= 2:
            gate.set()

    scheduler.add_tasks([_task()])
    main_task = asyncio.create_task(scheduler.start(execute))
    await asyncio.wait_for(gate.wait(), timeout=5.0)

    await asyncio.wait_for(scheduler.stop(drain_timeout=2.0), timeout=5.0)
    await asyncio.wait_for(main_task, timeout=5.0)
    cycles_at_stop = cycles

    # 给循环残留的唤醒机会：这期间不得再有新周期
    for _ in range(5):
        await asyncio.sleep(0)
    assert cycles == cycles_at_stop, f"stop 后仍新增采样：{cycles_at_stop} → {cycles}"
    assert scheduler._running is False
    assert scheduler._loop_tasks == []
    assert metrics.snapshot()["gauges"]["scheduler_active_loops"] == 0


@pytest.mark.asyncio
async def test_blocked_task_times_out_with_bounded_exit_and_diagnostic():
    """阻塞任务：有界退出 + 准确诊断（不能无限等待，也不能静默）。"""
    metrics = PerformanceMetrics()
    scheduler = _scheduler(metrics)
    started = asyncio.Event()
    never = asyncio.Event()

    async def execute(_task):
        started.set()
        await never.wait()                # 永不返回

    scheduler.add_tasks([_task()])
    main_task = asyncio.create_task(scheduler.start(execute))
    await asyncio.wait_for(started.wait(), timeout=5.0)

    began = time.monotonic()
    result = await asyncio.wait_for(scheduler.stop(drain_timeout=0.2), timeout=5.0)
    elapsed = time.monotonic() - began

    assert result["timed_out"] == 1 and result["drained"] is False
    assert elapsed < 2.0, f"超时退出不够有界：{elapsed:.2f}s"
    counters = metrics.snapshot()["counters"]
    assert counters.get("scheduler_drain_timeout_count") == 1, "超时必须留下诊断计数"
    main_task.cancel()
    await asyncio.gather(main_task, return_exceptions=True)


@pytest.mark.asyncio
async def test_cancel_suppressed_task_fails_boundedly_without_forgetting_live_loop():
    metrics = PerformanceMetrics()
    scheduler = _scheduler(metrics)
    scheduler._cancel_timeout = 0.05
    started = asyncio.Event()
    cancelled = asyncio.Event()
    release = asyncio.Event()

    async def execute(_task):
        started.set()
        try:
            await asyncio.Event().wait()
        except asyncio.CancelledError:
            cancelled.set()
            await release.wait()

    scheduler.add_tasks([_task()])
    await scheduler.start(execute)
    await asyncio.wait_for(started.wait(), timeout=5.0)

    try:
        with pytest.raises(RuntimeError, match="still running after cancellation"):
            await asyncio.wait_for(scheduler.stop(drain_timeout=0.05), timeout=2.0)
        assert cancelled.is_set()
        assert len(scheduler._loop_tasks) == 1
        assert not scheduler._loop_tasks[0].done()
        assert metrics.snapshot()["counters"]["scheduler_cancel_timeout_count"] == 1
    finally:
        release.set()
        await asyncio.wait_for(scheduler.stop(drain_timeout=1.0), timeout=2.0)


@pytest.mark.asyncio
async def test_stop_is_idempotent_and_safe_when_execute_fails():
    """execute 抛错后仍能正常停机；重复 stop 安全（不重复停两套 shutdown）。"""
    metrics = PerformanceMetrics()
    scheduler = _scheduler(metrics)
    attempts = 0
    failed = asyncio.Event()

    async def execute(_task):
        nonlocal attempts
        attempts += 1
        failed.set()
        raise RuntimeError("driver read failed")

    scheduler.add_tasks([_task()])
    main_task = asyncio.create_task(scheduler.start(execute))
    await asyncio.wait_for(failed.wait(), timeout=5.0)

    first = await asyncio.wait_for(scheduler.stop(drain_timeout=2.0), timeout=5.0)
    second = await asyncio.wait_for(scheduler.stop(drain_timeout=2.0), timeout=5.0)

    assert first["drained"] is True and second["drained"] is True
    assert scheduler._loop_tasks == [] and scheduler._device_loop_tasks == {}
    main_task.cancel()
    await asyncio.gather(main_task, return_exceptions=True)
