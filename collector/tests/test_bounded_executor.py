from __future__ import annotations

import asyncio
import threading

import pytest

from collector.bounded_executor import (
    BoundedExecutor,
    BoundedThreadPoolExecutor,
    ExecutorSaturatedError,
)
from monitor.performance import PerformanceMetrics


@pytest.mark.asyncio
async def test_cancelled_waiter_does_not_release_capacity_early() -> None:
    started = threading.Event()
    release = threading.Event()
    metrics = PerformanceMetrics()
    executor = BoundedExecutor(
        name="test",
        max_workers=1,
        max_pending=1,
        metrics=metrics,
    )

    def blocking_work() -> str:
        started.set()
        release.wait(timeout=5)
        return "done"

    first = asyncio.create_task(executor.run(blocking_work))
    await asyncio.to_thread(started.wait, 2)
    first.cancel()
    with pytest.raises(asyncio.CancelledError):
        await first

    with pytest.raises(ExecutorSaturatedError):
        await executor.run(lambda: "must not run")

    release.set()
    for _ in range(100):
        if executor.pending == 0:
            break
        await asyncio.sleep(0.01)

    assert await executor.run(lambda: "next") == "next"
    await executor.shutdown()

    snapshot = metrics.snapshot()
    assert snapshot["counters"]["test_executor_reject_count"] == 1
    assert snapshot["gauges"]["test_executor_pending"] == 0
    assert snapshot["gauges"]["test_executor_peak_pending"] == 1


@pytest.mark.asyncio
async def test_shutdown_waits_for_submitted_work() -> None:
    release = threading.Event()
    executor = BoundedExecutor(name="drain", max_workers=1, max_pending=1)

    work = asyncio.create_task(executor.run(lambda: release.wait(timeout=5)))
    await asyncio.sleep(0)
    shutdown = asyncio.create_task(executor.shutdown())
    await asyncio.sleep(0.02)

    assert shutdown.done() is False

    release.set()
    await work
    await shutdown
    assert executor.pending == 0


def test_thread_pool_rejects_submit_beyond_capacity() -> None:
    started = threading.Event()
    release = threading.Event()
    executor = BoundedThreadPoolExecutor(
        name="io-test",
        max_workers=1,
        max_pending=1,
    )

    def blocking_work() -> str:
        started.set()
        release.wait(timeout=5)
        return "done"

    first = executor.submit(blocking_work)
    assert started.wait(timeout=2)

    with pytest.raises(ExecutorSaturatedError):
        executor.submit(lambda: "must not run")

    release.set()
    assert first.result(timeout=2) == "done"
    assert executor.submit(lambda: "next").result(timeout=2) == "next"
    executor.shutdown(wait=True)
