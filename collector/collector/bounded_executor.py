"""有界线程执行器：限制已提交但尚未完成的工作数量。"""

from __future__ import annotations

import asyncio
import threading
from concurrent.futures import Future, ThreadPoolExecutor
from typing import Any, Callable, Optional, TypeVar

from monitor.performance import PerformanceMetrics


T = TypeVar("T")


class ExecutorSaturatedError(RuntimeError):
    """执行器容量已满，新任务未被提交。"""


class BoundedThreadPoolExecutor(ThreadPoolExecutor):
    """供 loop.run_in_executor 使用的非阻塞有界线程池。"""

    def __init__(
        self,
        *,
        name: str,
        max_workers: int,
        max_pending: int,
        metrics: Optional[PerformanceMetrics] = None,
    ) -> None:
        if max_workers <= 0 or max_pending <= 0:
            raise ValueError("max_workers and max_pending must be positive")
        super().__init__(
            max_workers=max_workers,
            thread_name_prefix=f"collector-{name}",
        )
        self.name = name
        self.max_pending = max_pending
        self._metrics = metrics
        self._slots = threading.BoundedSemaphore(max_pending)
        self._pending_lock = threading.Lock()
        self._pending = 0
        self._peak_pending = 0
        self._set_gauge("pending", 0)
        self._set_gauge("peak_pending", 0)
        self._increment("reject_count", 0)

    @property
    def pending(self) -> int:
        with self._pending_lock:
            return self._pending

    def submit(self, fn: Callable[..., T], /, *args: Any, **kwargs: Any) -> Future[T]:
        if not self._slots.acquire(blocking=False):
            self._increment("reject_count")
            raise ExecutorSaturatedError(
                f"{self.name} executor capacity reached: {self.max_pending}"
            )

        with self._pending_lock:
            self._pending += 1
            self._peak_pending = max(self._peak_pending, self._pending)
            self._set_gauge("pending", self._pending)
            self._set_gauge("peak_pending", self._peak_pending)
        try:
            future = super().submit(fn, *args, **kwargs)
        except Exception:
            self._release_slot()
            raise
        future.add_done_callback(lambda _future: self._release_slot())
        return future

    def _release_slot(self) -> None:
        with self._pending_lock:
            self._pending -= 1
            self._set_gauge("pending", self._pending)
        self._slots.release()

    def _increment(self, suffix: str, amount: int = 1) -> None:
        if self._metrics is not None:
            self._metrics.increment(f"{self.name}_executor_{suffix}", amount)

    def _set_gauge(self, suffix: str, value: int) -> None:
        if self._metrics is not None:
            self._metrics.set_gauge(f"{self.name}_executor_{suffix}", value)


class BoundedExecutor:
    def __init__(
        self,
        *,
        name: str,
        max_workers: int,
        max_pending: int,
        metrics: Optional[PerformanceMetrics] = None,
    ) -> None:
        if max_workers <= 0 or max_pending <= 0:
            raise ValueError("max_workers and max_pending must be positive")

        self.name = name
        self.max_pending = max_pending
        self._metrics = metrics
        self._executor = ThreadPoolExecutor(
            max_workers=max_workers,
            thread_name_prefix=f"collector-{name}",
        )
        self._slots = asyncio.Semaphore(max_pending)
        self._drained = asyncio.Event()
        self._drained.set()
        self._pending = 0
        self._peak_pending = 0
        self._closed = False
        self._set_gauge("pending", 0)
        self._set_gauge("peak_pending", 0)
        self._increment("reject_count", 0)

    @property
    def pending(self) -> int:
        return self._pending

    async def run(self, func: Callable[..., T], *args: Any) -> T:
        if self._closed:
            raise RuntimeError(f"{self.name} executor is closed")
        if self._slots.locked():
            self._increment("reject_count")
            raise ExecutorSaturatedError(
                f"{self.name} executor capacity reached: {self.max_pending}"
            )

        await self._slots.acquire()
        self._pending += 1
        self._peak_pending = max(self._peak_pending, self._pending)
        self._drained.clear()
        self._set_gauge("pending", self._pending)
        self._set_gauge("peak_pending", self._peak_pending)

        loop = asyncio.get_running_loop()
        try:
            future = loop.run_in_executor(self._executor, func, *args)
        except Exception:
            self._release_slot()
            raise

        future.add_done_callback(lambda _future: self._release_slot())
        return await asyncio.shield(future)

    async def shutdown(self, timeout: Optional[float] = None) -> int:
        """优雅关闭并返回未完成 job 数（0 表示已完全 drain）。

        timeout 为 None 时无限等待 drain（原行为）；给定 timeout 时最多等待该秒数，
        超时则放弃等待、取消尚未开始的任务并返回当前未完成数（§7.9-5）。
        """
        if self._closed:
            return 0
        self._closed = True
        if timeout is None:
            await self._drained.wait()
            await asyncio.to_thread(self._executor.shutdown, wait=True)
            return 0
        try:
            await asyncio.wait_for(self._drained.wait(), timeout)
        except asyncio.TimeoutError:
            unfinished = self._pending
            await asyncio.to_thread(
                self._executor.shutdown, wait=False, cancel_futures=True
            )
            return unfinished
        await asyncio.to_thread(self._executor.shutdown, wait=True)
        return 0

    def _release_slot(self) -> None:
        self._pending -= 1
        self._slots.release()
        self._set_gauge("pending", self._pending)
        if self._pending == 0:
            self._drained.set()

    def _increment(self, suffix: str, amount: int = 1) -> None:
        if self._metrics is not None:
            self._metrics.increment(f"{self.name}_executor_{suffix}", amount)

    def _set_gauge(self, suffix: str, value: int) -> None:
        if self._metrics is not None:
            self._metrics.set_gauge(f"{self.name}_executor_{suffix}", value)
