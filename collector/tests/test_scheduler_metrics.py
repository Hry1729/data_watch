from types import SimpleNamespace

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


def test_scheduler_exposes_task_count_gauge():
    metrics = PerformanceMetrics()
    scheduler = TaskScheduler(metrics=metrics)

    scheduler.add_task(
        SimpleNamespace(task_name="task-1", device=SimpleNamespace(device_id="d1"))
    )

    assert metrics.snapshot()["gauges"]["scheduler_task_count"] == 1
