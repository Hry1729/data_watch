"""一次性驱动会话：创建、连接、读一块、断开、丢弃。"""

from __future__ import annotations

import asyncio
import contextlib
import threading
import time
from concurrent.futures import Future, ThreadPoolExecutor
from typing import Any, Callable, Coroutine

from direct_read.driver_loader import import_read_block
from direct_read.errors import DirectReadError
from direct_read.quality import (
    QUALITY_BAD,
    QUALITY_CONFIG_DECODE_ERROR,
    QUALITY_GOOD,
    QUALITY_NOT_CONNECTED,
    is_valid_quality,
)


_CONFIG_HINTS = ("data type", "address", "bool", "dbx", "decode", "config")

# 直连读专用线程池规模：connect/read 与 disconnect 可并行，避免断开排在卡住的 connect 之后。
_DIRECT_READ_WORKERS = 4


class _SettleTrackingExecutor(ThreadPoolExecutor):
    """记录在途的驱动线程调用；关闭后最后一个调用真正结束时回调 ``on_settled``（只一次）。

    超时后调用方立即返回，但仍在线程里阻塞的 PLC 调用必须继续占着全局并发名额
    （RatePermit），直到它真正结束，否则反复超时会绕过 MAX_CONCURRENCY（PR #27 Codex P1）。
    """

    def __init__(self, *, on_settled: Callable[[], None] | None, **kwargs: Any) -> None:
        super().__init__(**kwargs)
        self._settle_lock = threading.Lock()
        self._inflight = 0
        self._closing = False
        self._settled = False
        self._on_settled = on_settled

    def submit(self, fn, /, *args, **kwargs) -> Future:  # type: ignore[override]
        with self._settle_lock:
            self._inflight += 1
        try:
            future = super().submit(fn, *args, **kwargs)
        except BaseException:
            self._finish_one(None)
            raise
        future.add_done_callback(self._finish_one)
        return future

    def _finish_one(self, _future: Future | None) -> None:
        with self._settle_lock:
            self._inflight -= 1
            fire = self._closing and self._inflight == 0 and not self._settled
            if fire:
                self._settled = True
        if fire:
            self._fire()

    def close_and_settle(self) -> None:
        """不等待在途调用；未开始的排队任务直接取消。在途清零时（可能就是现在）回调。"""
        self.shutdown(wait=False, cancel_futures=True)
        with self._settle_lock:
            self._closing = True
            fire = self._inflight == 0 and not self._settled
            if fire:
                self._settled = True
        if fire:
            self._fire()

    def _fire(self) -> None:
        if self._on_settled is None:
            return
        with contextlib.suppress(Exception):
            self._on_settled()


def run_with_deadline(
    coro: Coroutine[Any, Any, Any],
    *,
    on_settled: Callable[[], None] | None = None,
) -> Any:
    """同步运行 ``coro``，收尾不等待仍在线程里阻塞的驱动调用（B043）。

    ``asyncio.run`` 退出时会 ``shutdown_default_executor()``，等待默认线程池里所有
    已开始的阻塞调用（如 snap7 connect/read）结束，``wait_for`` 的 deadline 因此约束不了
    同步 API 的真实墙钟。这里给事件循环一个专用线程池，结束时 ``shutdown(wait=False)``：
    超时结果按 deadline 返回，残留阻塞调用在后台线程里自行结束（受驱动自身
    Send/Recv 超时约束），不再拖住调用方。

    ``on_settled`` 在该线程池里所有调用都真正结束后才被调用（可能在后台线程里，
    也可能在本函数返回前）；调用方把并发名额的释放放在这里，超时残留的调用就
    仍计入并发上限。无论 ``coro`` 成功、失败还是超时，``on_settled`` 恰好调用一次。
    """
    executor = _SettleTrackingExecutor(
        on_settled=on_settled,
        max_workers=_DIRECT_READ_WORKERS,
        thread_name_prefix="direct-read",
    )
    try:
        loop = asyncio.new_event_loop()
    except BaseException:
        executor.close_and_settle()
        raise
    loop.set_default_executor(executor)
    try:
        asyncio.set_event_loop(loop)
        return loop.run_until_complete(coro)
    finally:
        try:
            pending = [task for task in asyncio.all_tasks(loop) if not task.done()]
            for task in pending:
                task.cancel()
            if pending:
                loop.run_until_complete(asyncio.gather(*pending, return_exceptions=True))
            loop.run_until_complete(loop.shutdown_asyncgens())
        finally:
            asyncio.set_event_loop(None)
            executor.close_and_settle()
            loop.close()


async def read_once(
    factory: Callable[[], Any],
    connection: dict[str, Any],
    *,
    address: str,
    data_type: str,
    deadline: float,
) -> tuple[Any, int, int]:
    """返回 (值, 质量码, 读耗时毫秒)。``deadline`` 是 ``time.monotonic`` 截止点，含断开。"""
    driver = factory()
    started = time.perf_counter()
    try:
        block_type = import_read_block()
        block = block_type(address, 1, data_type)
        try:
            left = deadline - time.monotonic()
            if left <= 0:
                raise TimeoutError
            await asyncio.wait_for(driver.connect(connection), left)
            left = deadline - time.monotonic()
            if left <= 0:
                raise TimeoutError
            raw = await asyncio.wait_for(driver.read([block]), left)
        except TimeoutError as exc:
            raise DirectReadError(
                "timeout",
                "直连读取超时",
                quality=QUALITY_NOT_CONNECTED,
            ) from exc
        except DirectReadError:
            raise
        except Exception as exc:
            raise _map_driver_error(exc) from exc
        value, quality = interpret_sample(raw, address)
        elapsed_ms = int((time.perf_counter() - started) * 1000)
        return value, quality, elapsed_ms
    finally:
        await _safe_disconnect(driver, deadline)


def interpret_sample(raw: Any, address: str) -> tuple[Any, int]:
    if not isinstance(raw, dict) or address not in raw:
        raise DirectReadError(
            "plc_read_error",
            "驱动没有返回该地址的样本",
            quality=QUALITY_BAD,
        )
    value, quality = _unwrap(raw[address])
    if isinstance(value, list):
        if len(value) != 1:
            raise DirectReadError(
                "plc_read_error",
                "单点读取返回了意外的块长度",
                quality=QUALITY_BAD,
            )
        value, nested_quality = _unwrap(value[0])
        if nested_quality is not None:
            quality = nested_quality
    if value is None:
        raise DirectReadError(
            "plc_read_error",
            "PLC 未返回有效值",
            quality=QUALITY_BAD,
        )
    if quality is None:
        quality = QUALITY_GOOD
    if not is_valid_quality(quality):
        raise DirectReadError(
            "plc_read_error",
            "驱动返回了无法识别的质量码",
            quality=QUALITY_BAD,
        )
    return _json_safe(value), int(quality)


def _unwrap(value: Any) -> tuple[Any, int | None]:
    if isinstance(value, (str, bytes, bytearray, dict, list, int, float, bool)):
        return value, None
    sample_value = getattr(value, "value", None)
    sample_quality = getattr(value, "quality", None)
    if sample_quality is None:
        return value, None
    try:
        quality = int(sample_quality)
    except (TypeError, ValueError):
        return value, None
    return sample_value, quality


def _json_safe(value: Any) -> Any:
    if isinstance(value, bool) or value is None:
        return value
    if isinstance(value, (str, int, float)):
        return value
    if isinstance(value, (bytes, bytearray)):
        return value.hex()
    return str(value)


def _map_driver_error(exc: Exception) -> DirectReadError:
    text = f"{type(exc).__name__} {exc}".lower()
    if "timeout" in text:
        return DirectReadError(
            "timeout",
            "直连读取超时",
            quality=QUALITY_NOT_CONNECTED,
        )
    if any(hint in text for hint in _CONFIG_HINTS):
        return DirectReadError(
            "config_error",
            "点位地址或类型与 S7 驱动不匹配",
            quality=QUALITY_CONFIG_DECODE_ERROR,
        )
    if type(exc).__name__ in {"DeviceConnectionError", "ConnectionError", "OSError"}:
        return DirectReadError(
            "network_error",
            "无法连接到 PLC",
            quality=QUALITY_NOT_CONNECTED,
        )
    return DirectReadError(
        "plc_read_error",
        "PLC 读取失败",
        quality=QUALITY_BAD,
    )


async def _safe_disconnect(driver: Any, deadline: float) -> None:
    """断开只消耗截止时间里剩下的部分，不再另加预算。"""
    disconnect = getattr(driver, "disconnect", None)
    if not callable(disconnect):
        return
    try:
        result = disconnect()
    except Exception:
        return
    if not asyncio.iscoroutine(result):
        return
    task = asyncio.ensure_future(result)
    try:
        # 让已经就绪的断开协程跑完一步，瞬时假驱动能记下 disconnect。
        await asyncio.sleep(0)
        if task.done():
            return
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            task.cancel()
            return
        await asyncio.wait_for(task, remaining)
    except Exception:
        if not task.done():
            task.cancel()
    finally:
        if not task.done():
            task.cancel()
        with contextlib.suppress(asyncio.CancelledError, Exception):
            await task
