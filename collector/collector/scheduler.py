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
from typing import Callable, Coroutine, Dict, List, Optional

from core.logger import get_logger
from collector.task import CollectTask

logger = get_logger("scheduler")


class TaskScheduler:
    """
    基于asyncio的任务调度器
    按周期循环触发采集任务
    """

    def __init__(self):
        self.tasks: Dict[str, CollectTask] = {}
        self._running = False
        self._loop_tasks: List[asyncio.Task] = []
        self._device_loop_tasks: Dict[str, List[asyncio.Task]] = {}
        self._execute_func: Optional[Callable] = None

    def add_task(self, task: CollectTask) -> None:
        """添加采集任务"""
        self.tasks[task.task_name] = task
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

        # 为每个任务创建独立的循环
        for task in self.tasks.values():
            coro = self._run_loop(task, execute_func)
            loop_task = asyncio.create_task(coro)
            self._loop_tasks.append(loop_task)
            device_id = task.device.device_id
            self._device_loop_tasks.setdefault(device_id, []).append(loop_task)

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

        while self._running:
            start = time.monotonic()
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

            elapsed = time.monotonic() - start

            # 仅当执行时间小于间隔时才等待
            # 执行时间 >= 间隔时立即进入下一轮（不额外等待）
            if elapsed < interval:
                sleep_time = interval - elapsed
                await asyncio.sleep(sleep_time)
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

        # 移除旧 CollectTask 记录
        old_names = [n for n, ct in self.tasks.items() if ct.device.device_id == device_id]
        for name in old_names:
            del self.tasks[name]

        # 注册并启动新任务
        if self._execute_func is None:
            return
        for task in new_tasks:
            self.tasks[task.task_name] = task
            coro = self._run_loop(task, self._execute_func)
            loop_task = asyncio.create_task(coro)
            self._loop_tasks.append(loop_task)
            self._device_loop_tasks.setdefault(device_id, []).append(loop_task)

        logger.info(
            "Device %s reloaded: %d new collect tasks", device_id, len(new_tasks)
        )

    async def stop(self) -> None:
        """停止调度器"""
        self._running = False
        for task in self._loop_tasks:
            task.cancel()
            try:
                await task
            except asyncio.CancelledError:
                pass
        self._loop_tasks.clear()
        logger.info("Scheduler stopped")
