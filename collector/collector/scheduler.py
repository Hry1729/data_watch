"""
任务调度器模块

本模块提供基于 asyncio 的任务调度功能：
- TaskScheduler: 按周期循环触发采集任务
- 支持多个任务的并发调度
- 支持任务的动态添加和移除

典型用法:
    scheduler = TaskScheduler()
    scheduler.add_tasks(tasks)
    await scheduler.start(execute_func)
    # ...
    await scheduler.stop()
"""

import asyncio
import time
import zlib
from collections import deque
from typing import Callable, Coroutine, Deque, Dict, List, Optional, Set

from core.logger import get_logger
from collector.task import CollectTask
from monitor.performance import PerformanceMetrics

logger = get_logger("scheduler")


class TaskScheduler:
    """
    基于asyncio的任务调度器
    按周期循环触发采集任务
    """

    def __init__(
        self,
        metrics: Optional[PerformanceMetrics] = None,
        deadline_miss_ratio: float = 0.1,
    ):
        self.tasks: Dict[str, CollectTask] = {}
        self._running = False
        # 正常停机：先停止新增采样，再给**已在途**的采样周期一段有界时间完成解析与存储。
        # 原实现直接 cancel 设备循环，取消点可能落在"读取成功、尚未写 Redis"的窗口里，
        # 导致正常停机丢样（复现证据：outputs/central-restore-20260928/shutdown-cancel-repro.json）。
        self._drain_timeout: float = 10.0
        self._cancel_timeout: float = 1.0
        self._stop_requested = False
        self._loop_tasks: List[asyncio.Task] = []
        self._device_loop_tasks: Dict[str, List[asyncio.Task]] = {}
        self._execute_func: Optional[Callable] = None
        self._metrics = metrics or PerformanceMetrics()
        self._metrics.increment("deadline_miss_count", 0)
        # B46.1：把"迟到开始"从 deadline_miss_count 里独立出来，三个计数互斥可对账：
        # - deadline_late_start_count：到期且**确实开始执行**、但滞后超阈值；
        # - device_inflight_skip_count：到期但因**设备仍在执行上一周期**而未启动新执行；
        # - deadline_miss_count：因上一周期超时而被跳过的**完整周期数**（漏周期）。
        self._metrics.increment("deadline_late_start_count", 0)
        self._metrics.increment("device_inflight_skip_count", 0)
        self._metrics.increment("scheduler_coalesced_group_count", 0)
        self._metrics.increment("scheduler_coalesced_task_count", 0)
        self._metrics.set_gauge("max_consecutive_deadline_misses", 0)
        self._metrics.set_gauge("inflight_jobs", 0)
        self._metrics.set_gauge("peak_inflight_jobs", 0)
        self._deadline_miss_ratio = deadline_miss_ratio
        self._max_consecutive_deadline_misses = 0
        self._active_executions = 0
        self._peak_active_executions = 0
        self._inflight_devices: Set[str] = set()
        self._device_waiters: Dict[str, Deque[str]] = {}
        self._clock = time.monotonic
        self._sleep = asyncio.sleep

    @staticmethod
    def _task_device_id(task: CollectTask) -> str:
        device = getattr(task, "device", None)
        return getattr(device, "device_id", task.task_name)

    def _try_begin_device_execution(self, task: CollectTask) -> bool:
        device_id = self._task_device_id(task)
        task_name = task.task_name
        waiters = self._device_waiters.setdefault(device_id, deque())

        if device_id in self._inflight_devices:
            if task_name not in waiters:
                waiters.append(task_name)
            return False

        if waiters:
            if waiters[0] != task_name:
                if task_name not in waiters:
                    waiters.append(task_name)
                return False
            waiters.popleft()

        if not waiters:
            self._device_waiters.pop(device_id, None)
        self._inflight_devices.add(device_id)
        return True

    def _finish_device_execution(self, task: CollectTask) -> None:
        self._inflight_devices.discard(self._task_device_id(task))

    def _clear_device_execution_state(self, device_id: str) -> None:
        self._inflight_devices.discard(device_id)
        self._device_waiters.pop(device_id, None)

    @staticmethod
    def _initial_phase_seconds(task: CollectTask) -> float:
        period_us = max(1, int(task.scan_rate * 1_000_000))
        device_id = TaskScheduler._task_device_id(task)
        phase_us = zlib.crc32(device_id.encode("utf-8")) % period_us
        return phase_us / 1_000_000

    async def _run_phased_loop(
        self, task: CollectTask, execute_func: Callable
    ) -> None:
        phase = self._initial_phase_seconds(task)
        if phase > 0:
            await self._sleep(phase)
        if self._running:
            await self._run_loop(task, execute_func)


    @staticmethod
    def _group_execute_func(execute_func: Callable):
        owner = getattr(execute_func, "__self__", None)
        group_func = getattr(owner, "execute_many", None)
        return group_func if callable(group_func) else None

    async def _execute_due_tasks(
        self,
        tasks: List[CollectTask],
        execute_func: Callable,
    ) -> None:
        group_func = self._group_execute_func(execute_func)
        if len(tasks) > 1 and group_func is not None:
            self._metrics.increment("scheduler_coalesced_group_count")
            self._metrics.increment("scheduler_coalesced_task_count", len(tasks))
            await group_func(tasks)
            return

        for task in tasks:
            try:
                await execute_func(task)
                task.error_count = 0
            except Exception as exc:
                task.error_count += 1
                logger.error(
                    "Task %s failed (errors=%s): %s",
                    task.task_name,
                    task.error_count,
                    exc,
                )

    async def _run_device_loop(
        self,
        tasks: List[CollectTask],
        execute_func: Callable,
    ) -> None:
        if not tasks:
            return
        if len(tasks) == 1:
            await self._run_phased_loop(tasks[0], execute_func)
            return

        lead_task = min(tasks, key=lambda item: item.scan_rate)
        phase = self._initial_phase_seconds(lead_task)
        if phase > 0:
            await self._sleep(phase)
        if not self._running:
            return

        next_due = {task.task_name: self._clock() for task in tasks}
        consecutive_misses = {task.task_name: 0 for task in tasks}
        device_id = self._task_device_id(lead_task)
        logger.info(
            "Device %s unified loop started, scan_rates=%s",
            device_id,
            sorted(task.scan_rate for task in tasks),
        )

        while self._running:
            now = self._clock()
            due = [
                task
                for task in tasks
                if next_due[task.task_name] <= now + 1e-6
            ]
            if not due:
                await self._sleep(max(0.0, min(next_due.values()) - now))
                continue

            if not self._try_begin_device_execution(lead_task):
                # B46.1：本周期因设备仍在执行上一周期而**未启动新执行**，只计"跳过"。
                # 原实现此前已把同一周期计入迟到（deadline_miss_count），且设备持续繁忙期间
                # 每个循环迭代都会为同一个过期周期重复累加——既双计又无界放大。
                self._metrics.increment("device_inflight_skip_count")
                await self._sleep(min(task.scan_rate for task in due))
                continue

            # B46.1：只有**确实开始了执行**才裁定"迟到"，与"跳过"互斥，并使用独立计数器。
            for task in due:
                lag_ms = max(0.0, (now - next_due[task.task_name]) * 1000)
                self._metrics.record_timing("deadline_lag_ms", lag_ms)
                if lag_ms > task.scan_rate * 1000 * self._deadline_miss_ratio:
                    self._metrics.increment("deadline_late_start_count")
                    consecutive_misses[task.task_name] += 1
                    self._max_consecutive_deadline_misses = max(
                        self._max_consecutive_deadline_misses,
                        consecutive_misses[task.task_name],
                    )
                    self._metrics.set_gauge(
                        "max_consecutive_deadline_misses",
                        self._max_consecutive_deadline_misses,
                    )
                else:
                    consecutive_misses[task.task_name] = 0

            started = self._clock()
            self._active_executions += 1
            self._peak_active_executions = max(
                self._peak_active_executions,
                self._active_executions,
            )
            self._metrics.set_gauge("inflight_jobs", self._active_executions)
            self._metrics.set_gauge("peak_inflight_jobs", self._peak_active_executions)
            try:
                await self._execute_due_tasks(due, execute_func)
            except Exception as exc:
                logger.error(
                    "Device %s coincident task group failed: %s",
                    device_id,
                    exc,
                )
            finally:
                self._finish_device_execution(lead_task)
                self._active_executions -= 1
                self._metrics.set_gauge("inflight_jobs", self._active_executions)

            finished = self._clock()
            self._metrics.record_timing("execution_ms", (finished - started) * 1000)
            self._metrics.increment("scheduler_executions")
            for task in due:
                deadline = next_due[task.task_name] + task.scan_rate
                while deadline <= finished:
                    deadline += task.scan_rate
                    # B46.1 语义定义：deadline_miss_count = 因上一周期超时而被跳过的**完整
                    # 周期数**（漏周期），与 deadline_late_start_count（迟到开始）、
                    # device_inflight_skip_count（因在途未启动）三者互不重叠、可对账。
                    self._metrics.increment("deadline_miss_count")
                next_due[task.task_name] = deadline

    def add_task(self, task: CollectTask) -> None:
        """添加采集任务"""
        self.tasks[task.task_name] = task
        self._metrics.set_gauge("scheduler_task_count", len(self.tasks))
        logger.debug("Added task: %s", task)

    def add_tasks(self, tasks: List[CollectTask]) -> None:
        """批量添加"""
        for task in tasks:
            self.add_task(task)

    async def start(self, execute_func: Callable) -> None:
        """
        启动调度器
        Args:
            execute_func: 协程函数，接收CollectTask参数，执行采集
        """
        self._running = True
        self._execute_func = execute_func
        logger.info("Scheduler starting with %s tasks", len(self.tasks))

        # 每台设备一个统一循环；同一时刻到期的不同扫描周期任务可合并读取。
        tasks_by_device: Dict[str, List[CollectTask]] = {}
        for task in self.tasks.values():
            tasks_by_device.setdefault(task.device.device_id, []).append(task)
        for device_id, device_tasks in tasks_by_device.items():
            coro = self._run_device_loop(device_tasks, execute_func)
            loop_task = asyncio.create_task(coro)
            self._loop_tasks.append(loop_task)
            self._device_loop_tasks[device_id] = [loop_task]
        self._metrics.set_gauge("scheduler_active_loops", len(self._loop_tasks))

    async def _run_loop(
        self, task: CollectTask, execute_func: Callable
    ) -> None:
        """
        单个任务的循环调度

        调度策略：
        - 如果执行时间 < interval：等待 (interval - elapsed) 后发送下一次请求
        - 如果执行时间 >= interval：立即发送下一次请求（不额外等待）

        这样可以保证：
        - 正常情况下按固定周期采集
        - 慢请求时不会造成请求堆积，也不会额外增加等待时间
        """
        interval = task.scan_rate
        logger.info("Task %s loop started, interval=%ss", task.task_name, interval)
        previous_start: Optional[float] = None
        planned_start: Optional[float] = None
        consecutive_deadline_misses = 0

        while self._running:
            start = self._clock()
            lag_ms: Optional[float] = None
            if previous_start is not None and planned_start is not None:
                start_to_start_ms = (start - previous_start) * 1000
                lag_ms = max(0.0, (start - planned_start) * 1000)
                self._metrics.record_timing("start_to_start_ms", start_to_start_ms)
                self._metrics.record_timing("deadline_lag_ms", lag_ms)
            previous_start = start
            planned_start = start + interval

            if not self._try_begin_device_execution(task):
                # B46.1：设备仍在执行上一周期 ⇒ 本周期**未启动新执行**，只计"跳过"。
                # 原实现在此处**又**计了一次 deadline_miss_count —— 同一事件被两个计数器
                # 重复登记，两者相加必然高估漏周期。
                self._metrics.increment("device_inflight_skip_count")
                consecutive_deadline_misses += 1
                self._max_consecutive_deadline_misses = max(
                    self._max_consecutive_deadline_misses,
                    consecutive_deadline_misses,
                )
                self._metrics.set_gauge(
                    "max_consecutive_deadline_misses",
                    self._max_consecutive_deadline_misses,
                )
                elapsed = self._clock() - start
                if elapsed < interval:
                    await self._sleep(interval - elapsed)
                continue

            # B46.1：只有**确实开始了执行**的周期才可能"迟到"，判定放在启动之后，与"跳过"
            # 互斥；迟到使用独立计数器，不再与跳过/漏周期挤在 deadline_miss_count 里。
            if lag_ms is not None:
                if lag_ms > interval * 1000 * self._deadline_miss_ratio:
                    self._metrics.increment("deadline_late_start_count")
                    consecutive_deadline_misses += 1
                    self._max_consecutive_deadline_misses = max(
                        self._max_consecutive_deadline_misses,
                        consecutive_deadline_misses,
                    )
                    self._metrics.set_gauge(
                        "max_consecutive_deadline_misses",
                        self._max_consecutive_deadline_misses,
                    )
                else:
                    consecutive_deadline_misses = 0

            self._active_executions += 1
            self._peak_active_executions = max(
                self._peak_active_executions,
                self._active_executions,
            )
            self._metrics.set_gauge("inflight_jobs", self._active_executions)
            self._metrics.set_gauge(
                "peak_inflight_jobs",
                self._peak_active_executions,
            )
            try:
                await execute_func(task)
                task.error_count = 0
            except Exception as e:
                task.error_count += 1
                logger.error(
                    "Task %s failed (errors=%s): %s",
                    task.task_name,
                    task.error_count,
                    e,
                )
            finally:
                self._finish_device_execution(task)
                self._active_executions -= 1
                self._metrics.set_gauge("inflight_jobs", self._active_executions)

            elapsed = self._clock() - start
            self._metrics.record_timing("execution_ms", elapsed * 1000)
            self._metrics.increment("scheduler_executions")

            # 仅当执行时间小于间隔时才等待
            # 执行时间 >= 间隔时立即进入下一轮（不额外等待）
            if elapsed < interval:
                sleep_time = interval - elapsed
                await self._sleep(sleep_time)
            # else: 执行超时，立即进入下一轮

        logger.info("Task %s loop stopped", task.task_name)

    async def reload_device_tasks(
        self, device_id: str, new_tasks: List[CollectTask]
    ) -> None:
        """取消指定设备的旧采集循环，用新任务重启。不影响其他设备。"""
        # 取消旧循环
        for loop_task in self._device_loop_tasks.pop(device_id, []):
            loop_task.cancel()
            try:
                await loop_task
            except asyncio.CancelledError:
                pass
            # 同步从扁平任务表移除，避免频繁热重载累积已取消任务引用
            if loop_task in self._loop_tasks:
                self._loop_tasks.remove(loop_task)

        self._clear_device_execution_state(device_id)

        # 移除旧 CollectTask 记录
        old_names = [n for n, ct in self.tasks.items() if ct.device.device_id == device_id]
        for name in old_names:
            del self.tasks[name]

        # 注册并启动新任务
        if self._execute_func is None:
            return
        for task in new_tasks:
            self.tasks[task.task_name] = task
        if new_tasks:
            coro = self._run_device_loop(new_tasks, self._execute_func)
            loop_task = asyncio.create_task(coro)
            self._loop_tasks.append(loop_task)
            self._device_loop_tasks[device_id] = [loop_task]

        self._metrics.set_gauge("scheduler_task_count", len(self.tasks))
        self._metrics.set_gauge("scheduler_active_loops", len(self._loop_tasks))

        logger.info(
            "Device %s reloaded: %d new collect tasks", device_id, len(new_tasks)
        )

    async def stop(self, *, drain_timeout: Optional[float] = None) -> dict:
        """停止调度器：**先停止新增采样，再让已在途的采样周期收尾**。

        与旧行为的区别（最小修复，不重构调度器）：
          * 旧：立刻 cancel 设备循环 → 取消点可能落在 execute_func 内部
            （读取已成功、解析/写 Redis 尚未完成）→ 正常停机丢样；
          * 新：置 _running=False，循环在下一轮开始前退出（不再新增采样），
            然后等这些循环任务自然结束（在途周期跑完解析与存储）；
            超过 drain_timeout 才取消，并留下明确诊断（日志 + 指标）。
        异常取消能力保留：超时路径仍然 cancel，不承诺强杀/断电不丢样。
        """
        self._stop_requested = True
        self._running = False
        timeout = self._drain_timeout if drain_timeout is None else drain_timeout
        loops = list(self._loop_tasks)
        timed_out: List[asyncio.Task] = []
        if loops:
            _, pending = await asyncio.wait(loops, timeout=timeout)
            timed_out = [task for task in pending]
            if timed_out:
                self._metrics.increment("scheduler_drain_timeout_count")
                logger.error(
                    "Scheduler drain timed out after %.2fs: %d loop(s) still busy; "
                    "cancelling them (in-flight cycle may be incomplete)",
                    timeout,
                    len(timed_out),
                )
                for task in timed_out:
                    task.cancel()
                _, still_pending = await asyncio.wait(
                    timed_out, timeout=self._cancel_timeout
                )
                if still_pending:
                    self._metrics.increment("scheduler_cancel_timeout_count")
                    logger.error(
                        "Scheduler cancellation timed out after %.2fs: %d loop(s) "
                        "still running; keeping storage and executor open",
                        self._cancel_timeout,
                        len(still_pending),
                    )
                    raise RuntimeError(
                        f"Scheduler stop failed: {len(still_pending)} loop(s) "
                        "still running after cancellation"
                    )
                await asyncio.gather(*timed_out, return_exceptions=True)
        self._device_loop_tasks.clear()
        self._loop_tasks.clear()
        self._metrics.set_gauge("scheduler_active_loops", 0)
        result = {"drained": not timed_out, "timed_out": len(timed_out),
                  "drain_timeout_s": timeout}
        logger.info("Scheduler stopped (drained=%s, timed_out=%d)",
                    result["drained"], result["timed_out"])
        return result

    @property
    def stats(self):
        return self._metrics.snapshot()
