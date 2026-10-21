"""审查批次 4 回归 B046：SystemLogWriter.stop 的总超时须覆盖满队列时的 sentinel 入队。"""

import asyncio
import time

import pytest

from core.system_log import SystemLogWriter


class _BlockingDB:
    def __init__(self) -> None:
        self.release = asyncio.Event()
        self.written = 0

    async def write_system_log(self, **_kwargs) -> None:
        await self.release.wait()
        self.written += 1


def _emit(writer: SystemLogWriter, n: int) -> int:
    return sum(
        writer.emit(level="INFO", log_type="t", source="s", content=f"c{i}") for i in range(n)
    )


@pytest.mark.asyncio
async def test_stop_returns_within_timeout_when_queue_full_and_worker_blocked():
    db = _BlockingDB()
    writer = SystemLogWriter(db, queue_max_size=3)
    await writer.start()
    _emit(writer, 1)
    await asyncio.sleep(0)  # worker 取走第一条并阻塞在 DB 写入
    _emit(writer, 3)  # 队列填满
    assert writer._queue.full()

    started = time.monotonic()
    await asyncio.wait_for(writer.stop(timeout=0.05), timeout=1.0)
    assert time.monotonic() - started < 0.5
    assert writer._worker_task is None
    db.release.set()


@pytest.mark.asyncio
async def test_stop_drains_normally_when_worker_healthy():
    db = _BlockingDB()
    db.release.set()
    writer = SystemLogWriter(db, queue_max_size=10)
    await writer.start()
    assert _emit(writer, 5) == 5
    await writer.stop(timeout=1.0)
    assert db.written == 5
    assert writer._worker_task is None
