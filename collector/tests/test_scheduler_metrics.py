import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from collector.scheduler import TaskScheduler
from monitor.performance import PerformanceMetrics


@pytest.mark.asyncio
async def test_scheduler_records_execution_period_lag_and_deadline_miss():
    clock = iter((0.0, 0.004, 0.0105, 0.0145, 0.025, 0.029))

    async def no_sleep(_seconds):
        return None

    metrics = PerformanceMetrics()
    scheduler = TaskScheduler(metrics=metrics, deadline_miss_ratio=0.1)
    scheduler._clock = lambda: next(clock)
    scheduler._sleep = no_sleep
    task = SimpleNamespace(
        scan_rate=0.01,
        task_name="device_0.01s",
        error_count=0,
    )
    executions = 0

    async def execute(_task):
        nonlocal executions
        executions += 1
        if executions == 3:
            scheduler._running = False

    scheduler._running = True
    await scheduler._run_loop(task, execute)

    snapshot = metrics.snapshot()
    assert snapshot["timings"]["execution_ms"]["count"] == 3
    assert snapshot["timings"]["start_to_start_ms"]["count"] == 2
    assert snapshot["timings"]["deadline_lag_ms"]["count"] == 2
    assert snapshot["counters"]["deadline_miss_count"] == 1
    assert snapshot["gauges"]["max_consecutive_deadline_misses"] == 1
    assert snapshot["gauges"]["inflight_jobs"] == 0
    assert snapshot["gauges"]["peak_inflight_jobs"] == 1


def test_scheduler_exposes_task_count_gauge():
    metrics = PerformanceMetrics()
    scheduler = TaskScheduler(metrics=metrics)

    scheduler.add_task(
        SimpleNamespace(task_name="task-1", device=SimpleNamespace(device_id="d1"))
    )

    assert metrics.snapshot()["gauges"]["scheduler_task_count"] == 1


def test_scheduler_phase_is_stable_and_bounded_by_period():
    scheduler = TaskScheduler()
    task_a = SimpleNamespace(
        task_name="device-a_0.1s",
        scan_rate=0.1,
        device=SimpleNamespace(device_id="device-a"),
    )
    task_b = SimpleNamespace(
        task_name="device-b_0.1s",
        scan_rate=0.1,
        device=SimpleNamespace(device_id="device-b"),
    )

    phase_a = scheduler._initial_phase_seconds(task_a)

    assert phase_a == scheduler._initial_phase_seconds(task_a)
    assert 0 <= phase_a < task_a.scan_rate
    assert 0 <= scheduler._initial_phase_seconds(task_b) < task_b.scan_rate
    assert phase_a != scheduler._initial_phase_seconds(task_b)


def test_device_gate_serializes_and_rotates_skipped_tasks():
    scheduler = TaskScheduler()
    device = SimpleNamespace(device_id="d1")
    fast = SimpleNamespace(task_name="d1-fast", device=device)
    slow = SimpleNamespace(task_name="d1-slow", device=device)

    assert scheduler._try_begin_device_execution(fast) is True
    assert scheduler._try_begin_device_execution(slow) is False
    scheduler._finish_device_execution(fast)

    # slow 已经因 fast 忙而跳过，下一轮必须优先 slow，不能让 fast 永久抢占。
    assert scheduler._try_begin_device_execution(fast) is False
    assert scheduler._try_begin_device_execution(slow) is True
    scheduler._finish_device_execution(slow)

    assert scheduler._try_begin_device_execution(fast) is True
    scheduler._finish_device_execution(fast)


@pytest.mark.asyncio
async def test_busy_device_cycle_is_skipped_without_execution():
    metrics = PerformanceMetrics()
    scheduler = TaskScheduler(metrics=metrics)
    device = SimpleNamespace(device_id="d1")
    blocker = SimpleNamespace(task_name="d1-blocker", device=device)
    skipped = SimpleNamespace(
        scan_rate=0.1,
        task_name="d1-skipped",
        error_count=0,
        device=device,
    )
    executions = 0

    async def execute(_task):
        nonlocal executions
        executions += 1

    async def stop_after_skip(_seconds):
        scheduler._running = False

    assert scheduler._try_begin_device_execution(blocker) is True
    scheduler._sleep = stop_after_skip
    scheduler._running = True
    await scheduler._run_loop(skipped, execute)
    scheduler._finish_device_execution(blocker)

    snapshot = metrics.snapshot()
    assert executions == 0
    assert snapshot["counters"]["device_inflight_skip_count"] == 1
    assert snapshot["counters"]["deadline_miss_count"] == 1

@pytest.mark.asyncio
async def test_scheduler_dispatches_coincident_tasks_through_execute_many():
    metrics = PerformanceMetrics()
    scheduler = TaskScheduler(metrics=metrics)
    device = SimpleNamespace(device_id="d-coalesced")
    tasks = [
        SimpleNamespace(
            task_name=f"d-coalesced-{rate}",
            scan_rate=rate,
            error_count=0,
            device=device,
        )
        for rate in (0.1, 0.5)
    ]

    class GroupExecutor:
        def __init__(self):
            self.groups = []
            self.single_calls = []

        async def execute(self, task):
            self.single_calls.append(task)

        async def execute_many(self, due_tasks):
            self.groups.append(list(due_tasks))

    executor = GroupExecutor()
    await scheduler._execute_due_tasks(tasks, executor.execute)

    assert executor.groups == [tasks]
    assert executor.single_calls == []
    counters = metrics.snapshot()["counters"]
    assert counters["scheduler_coalesced_group_count"] == 1
    assert counters["scheduler_coalesced_task_count"] == 2


@pytest.mark.asyncio
async def test_scheduler_starts_one_unified_loop_per_device():
    scheduler = TaskScheduler()
    device = SimpleNamespace(device_id="d-one-loop")
    tasks = [
        SimpleNamespace(
            task_name=f"d-one-loop-{rate}",
            scan_rate=rate,
            error_count=0,
            device=device,
        )
        for rate in (0.1, 0.5)
    ]
    scheduler.add_tasks(tasks)
    scheduler._run_device_loop = AsyncMock()

    async def execute(_task):
        return None

    await scheduler.start(execute)
    await asyncio.sleep(0)

    assert scheduler._run_device_loop.await_count == 1
    grouped_tasks = scheduler._run_device_loop.await_args.args[0]
    assert grouped_tasks == tasks
    assert scheduler.stats["gauges"]["scheduler_active_loops"] == 1
    await scheduler.stop()
