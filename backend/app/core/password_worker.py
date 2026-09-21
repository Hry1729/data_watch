"""有界密码运算线程池（DW-08）。

PBKDF2 60 万次迭代保留在 security.hash/verify 中；本模块只负责把同步计算
移出事件循环，并用「工作线程 + 排队」硬上限拒绝超载，避免无界等待。
"""
from __future__ import annotations

import asyncio
import functools
import threading
from collections.abc import Callable
from concurrent.futures import ThreadPoolExecutor
from typing import Any, TypeVar

T = TypeVar("T")


class PasswordWorkerBusy(Exception):
    """工作线程与排队均已满，立即拒绝。"""


class PasswordWorker:
    def __init__(self, max_workers: int, max_queue: int) -> None:
        workers = max(1, int(max_workers))
        queue = max(0, int(max_queue))
        self._max_in_flight = workers + queue
        self._in_flight = 0
        self._lock = threading.Lock()
        self._executor = ThreadPoolExecutor(
            max_workers=workers,
            thread_name_prefix="pw-hash",
        )

    async def run(self, func: Callable[..., T], *args: Any, **kwargs: Any) -> T:
        with self._lock:
            if self._in_flight >= self._max_in_flight:
                raise PasswordWorkerBusy()
            self._in_flight += 1

        cfut = self._executor.submit(functools.partial(func, *args, **kwargs))

        def _release(_fut: object = None) -> None:
            with self._lock:
                self._in_flight -= 1

        try:
            return await asyncio.wrap_future(cfut)
        finally:
            if cfut.done():
                _release()
            else:
                cfut.add_done_callback(_release)

    def shutdown(self, wait: bool = True) -> None:
        self._executor.shutdown(wait=wait, cancel_futures=False)


_worker: PasswordWorker | None = None
_worker_lock = threading.Lock()


def get_password_worker() -> PasswordWorker:
    global _worker
    if _worker is None:
        with _worker_lock:
            if _worker is None:
                from app.core.config import get_settings

                settings = get_settings()
                _worker = PasswordWorker(
                    max_workers=settings.password_worker_max_workers,
                    max_queue=settings.password_worker_max_queue,
                )
    return _worker


async def run_password_op(func: Callable[..., T], *args: Any, **kwargs: Any) -> T:
    return await get_password_worker().run(func, *args, **kwargs)
