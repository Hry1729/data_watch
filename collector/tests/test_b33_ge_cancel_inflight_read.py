"""B33 回归：GE 热重载取消 await 后，底层共享连接读仍在执行。

修复前的两个缺口（已核对）：
1. ``read()`` 持 ``_read_lock`` 并在线程池执行**阻塞** socket I/O。收到 CancelledError 时
   ``async with`` 立即退出、锁被提前释放，而线程仍在读写同一 socket ⇒ 新代可在**同一 socket**
   上并发读，协议帧交错。
2. ``disconnect()`` 不取 ``_read_lock``，可在在途读进行中关闭/替换连接（"退出确认"缺失）。

现在：``_run_blocking`` 在取消后**先等底层真正结束**（有界 drain）再传播取消，因此锁一直持有
到底层 I/O 结束；drain 超时则同步关闭底层 socket 把线程逼出来；``disconnect`` 取同一把锁。
"""

from __future__ import annotations

import asyncio
import sys
import threading
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
GE_ROOT = REPO_ROOT / "packages" / "ge_srtp"
for _candidate in (GE_ROOT, GE_ROOT / "vendor", REPO_ROOT):
    if _candidate.is_dir() and str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))


def _ge_driver_class():
    try:
        from ge_srtp.driver import GESRTPDriver
    except Exception as exc:  # pragma: no cover - 取决于 GE 驱动依赖是否就绪
        pytest.skip(f"ge_srtp driver unavailable: {exc}")
    return GESRTPDriver


def _read_block():
    from drivers.base import ReadBlock

    return ReadBlock("%R1", 1, "INT")


class _FakeSocket:
    def __init__(self) -> None:
        self.closed = False

    def close(self) -> None:
        self.closed = True


class _BlockingChannel:
    """替身通道：读阻塞在事件上，并记录底层**并发重入**次数。"""

    def __init__(self) -> None:
        self.sock = _FakeSocket()
        self.entered = threading.Event()
        self.release = threading.Event()
        self.active = 0
        self.max_active = 0
        self._guard = threading.Lock()

    def read_register(self, offset, count):
        with self._guard:
            self.active += 1
            self.max_active = max(self.max_active, self.active)
        self.entered.set()
        try:
            self.release.wait(10)
            return [offset] * count
        finally:
            with self._guard:
                self.active -= 1

    def disconnect(self):
        return None


def _prepare(GESRTPDriver, executor):
    driver = GESRTPDriver(read_executor=executor)
    channel = _BlockingChannel()
    driver._driver = channel
    driver._drivers = []
    driver._connected = True
    return driver, channel


async def _spin(rounds: int = 30) -> None:
    for _ in range(rounds):
        await asyncio.sleep(0)


@pytest.mark.asyncio
async def test_cancel_closes_and_isolates_the_shared_connection() -> None:
    """取消后必须关闭并隔离连接：同一 socket 不会再被新代读，且旧线程结果被丢弃。"""
    GESRTPDriver = _ge_driver_class()
    executor = ThreadPoolExecutor(max_workers=2)
    driver, channel = _prepare(GESRTPDriver, executor)

    first = asyncio.create_task(driver.read([_read_block()]))
    assert await asyncio.to_thread(channel.entered.wait, 5), "底层读未进入"
    assert channel.active == 1

    first.cancel()
    with pytest.raises(asyncio.CancelledError):
        await first
    await _spin()

    # 关闭 + 标记不可用：旧 socket 被隔离，后续读取必须先重连换新连接。
    assert channel.sock.closed is True, "取消后必须关闭底层 socket"
    assert driver._connected is False, "取消后连接必须被标记为不可用"

    # 在途读未结束的窗口内，新代读不得落到同一个 socket 上。
    from core.exceptions import ReadError

    with pytest.raises(ReadError):
        await driver.read([_read_block()])
    assert channel.max_active == 1, "同一 socket 出现了并发底层读"

    channel.release.set()
    await _spin()
    executor.shutdown(wait=True)


@pytest.mark.asyncio
async def test_disconnect_serializes_with_in_flight_read() -> None:
    GESRTPDriver = _ge_driver_class()
    executor = ThreadPoolExecutor(max_workers=2)
    driver, channel = _prepare(GESRTPDriver, executor)

    reading = asyncio.create_task(driver.read([_read_block()]))
    assert await asyncio.to_thread(channel.entered.wait, 5), "底层读未进入"

    disconnecting = asyncio.create_task(driver.disconnect())
    await _spin()
    assert not disconnecting.done(), "disconnect 不得在在途读进行中完成"

    channel.release.set()
    await reading
    await disconnecting

    assert driver._connected is False
    assert driver._driver is None
    executor.shutdown(wait=True)

