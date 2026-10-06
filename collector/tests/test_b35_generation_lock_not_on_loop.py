"""B35 回归：generation 锁不得让事件循环停摆。

修复前的事实（已核对 collector/device/manager.py）：
- ``run_if_current_generation`` 的 docstring 明写"在同设备 generation 锁内验证并执行"，
  而它的 ``action()`` 就是 ``_write_to_redis``（**Redis 网络 I/O**）⇒ 锁跨越网络 I/O 被持有；
- ``invalidate_device`` / ``swap_device`` 取**同一把** per-device RLock，且由**事件循环线程**
  （配置重载、设备下线）调用 ⇒ worker 线程做 Redis I/O 期间，事件循环**同步等待该锁**，
  所有设备的调度一起停摆。

修法：保持写侧"锁内 check→write"的原子性不变（不得把写移出锁），只把**事件循环侧**的
取锁放进工作线程（``asyncio.to_thread``），于是阻塞发生在工作线程，循环不再停摆。
"""

from __future__ import annotations

import asyncio
import sys
import threading
import time
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from core.config import CollectorConfig, DeviceConfig, VariableConfig  # noqa: E402
from device.manager import DeviceManager  # noqa: E402
from main import CollectorApp  # noqa: E402
from collector.executor import TaskExecutor
from collector.task import CollectTask


def _manager_with_device() -> tuple[DeviceManager, object]:
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager.register_devices()
    device = manager.add_device(
        DeviceConfig("LOCK_PLC", "QC", "s7", "192.168.1.99", port=102, slot=1),
        [VariableConfig("v1", "LOCK_PLC", "DB100.DBW0", "INT", 1, True, 1)],
    )
    return manager, device


def _app_with(manager: DeviceManager) -> CollectorApp:
    app = CollectorApp.__new__(CollectorApp)
    app.device_manager = manager
    app.scheduler = MagicMock()
    app.scheduler.reload_device_tasks = AsyncMock()
    return app


@pytest.mark.asyncio
async def test_remove_device_does_not_stall_event_loop_while_lock_is_held() -> None:
    """验收①：worker 线程持锁做慢 I/O 期间，事件循环必须继续推进。"""
    manager, device = _manager_with_device()
    lock = manager._generation_lock_for(device.device_id)

    started = threading.Event()
    release = threading.Event()

    def slow_write_holding_lock() -> None:
        # 模拟 worker 线程在锁内做慢 Redis 写
        with lock:
            started.set()
            release.wait(10)

    worker = asyncio.create_task(asyncio.to_thread(slow_write_holding_lock))
    assert await asyncio.to_thread(started.wait, 5), "worker 未能取得 generation 锁"

    ticks = 0

    async def heartbeat() -> None:
        nonlocal ticks
        while True:
            await asyncio.sleep(0.01)
            ticks += 1

    beat = asyncio.create_task(heartbeat())
    await asyncio.sleep(0.05)
    baseline = ticks
    assert baseline > 0, "心跳未启动"

    try:
        removing = asyncio.create_task(_app_with(manager)._remove_device(device.device_id))
        await asyncio.sleep(0.15)

        # 关键：失效操作在等锁（尚未完成），但**事件循环没有被卡住**——心跳仍在推进。
        assert not removing.done(), "锁被持有期间失效不应提前完成"
        assert ticks > baseline + 3, (
            f"事件循环被 generation 锁阻塞：期望心跳继续推进，实际 {baseline} -> {ticks}"
        )
    finally:
        release.set()
        await worker

    await removing
    assert "LOCK_PLC" not in manager.devices


@pytest.mark.asyncio
async def test_swap_device_does_not_stall_event_loop_while_lock_is_held() -> None:
    """验收①（热替换路径）：swap_device 同样不得阻塞事件循环。"""
    manager, device = _manager_with_device()
    lock = manager._generation_lock_for(device.device_id)

    started = threading.Event()
    release = threading.Event()

    def slow_write_holding_lock() -> None:
        with lock:
            started.set()
            release.wait(10)

    worker = asyncio.create_task(asyncio.to_thread(slow_write_holding_lock))
    assert await asyncio.to_thread(started.wait, 5), "worker 未能取得 generation 锁"

    ticks = 0

    async def heartbeat() -> None:
        nonlocal ticks
        while True:
            await asyncio.sleep(0.01)
            ticks += 1

    beat = asyncio.create_task(heartbeat())
    await asyncio.sleep(0.05)
    baseline = ticks

    candidate = device.__class__.__new__(device.__class__)
    candidate.__dict__.update(device.__dict__)
    candidate.driver = None
    manager._safe_disconnect_driver = AsyncMock()

    try:
        swapping = asyncio.create_task(manager.swap_device(device.device_id, candidate))
        await asyncio.sleep(0.15)
        assert not swapping.done(), "锁被持有期间热替换不应提前完成"
        assert ticks > baseline + 3, (
            f"事件循环被 generation 锁阻塞：期望心跳继续推进，实际 {baseline} -> {ticks}"
        )
    finally:
        release.set()
        await worker

    await swapping
    assert manager.devices[device.device_id] is candidate


@pytest.mark.asyncio
async def test_stale_generation_is_rejected_while_lock_held() -> None:
    """验收②：旧代不能在新代生效后提交——run_if_current_generation 必须返回 False 且不执行。"""
    manager, device = _manager_with_device()
    generation = device.generation

    manager.invalidate_device(device.device_id)

    calls: list[str] = []
    accepted = manager.run_if_current_generation(
        device.device_id, generation, lambda: calls.append("written")
    )

    assert accepted is False
    assert calls == [], "旧代不得提交写入"


@pytest.mark.asyncio
async def test_execute_generation_check_does_not_stall_loop():
    manager, device = _manager_with_device()
    started, release = threading.Event(), threading.Event()
    def hold():
        with manager._generation_lock_for(device.device_id):
            started.set()
            release.wait(3)
    worker = asyncio.create_task(asyncio.to_thread(hold))
    await asyncio.to_thread(started.wait, 2)
    executor = TaskExecutor(device_manager=manager, redis_writer=MagicMock())
    manager.schedule_reconnect = AsyncMock()
    executing = asyncio.create_task(executor.execute(CollectTask(device, device.scan_tasks[0])))
    try:
        await asyncio.sleep(0.05)
        assert not executing.done()
        assert not release.is_set()
    finally:
        release.set()
        await worker
        await executing
