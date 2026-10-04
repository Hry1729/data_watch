"""B33 取证/回归：慢同步读期间热重载，旧代是否已被隔离。

背景：清单要求"取消后保留在途状态至真实读结束，**或关闭/替换连接并隔离旧代**"，且验收
要求"同一 socket 不出现两个底层读；迟到结果不覆盖新代"。

驱动侧事实（已核对，属受签名包）：GE 的 read 持 _read_lock 且在线程池执行阻塞 I/O；
disconnect 不取该锁 ⇒ 取消点之后仍可能有底层读在跑。**因此本测试只验证采集器侧是否已把
后果挡住**，用于判断"是否必须改驱动包"：

1. 迟到结果不得落库：旧代 task 的点必须被 generation 门挡掉；
2. 同一 socket 不得被两代共用：热重载后的候选必须持有**新的驱动实例**，旧设备对象不再持有驱动
   （于是回滚也只能新建连接，而不是复用可能已与 disconnect 竞态的旧连接）。
"""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from core.config import CollectorConfig, DeviceConfig, VariableConfig  # noqa: E402
from collector.executor import TaskExecutor  # noqa: E402
from device.device import DeviceStatus  # noqa: E402
from device.manager import DeviceManager  # noqa: E402


class _RecordingMetrics:
    def __init__(self) -> None:
        self.increments: dict[str, int] = {}

    def increment(self, name: str, amount: int = 1) -> None:
        self.increments[name] = self.increments.get(name, 0) + amount

    def set_gauge(self, *_args, **_kwargs) -> None:
        pass

    def record_timing(self, *_args, **_kwargs) -> None:
        pass


class _StubDriver:
    """带标记的驱动替身，用于分辨"是不是同一个连接实例"。"""

    def __init__(self, name: str) -> None:
        self.name = name
        self.is_connected = True
        self.disconnect = AsyncMock()
        self.ping = AsyncMock(return_value=True)


def _manager_with_device():
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager.register_devices()
    manager._metrics = _RecordingMetrics()
    device = manager.add_device(
        DeviceConfig("RL_PLC", "QC", "s7", "192.168.1.60", port=102, slot=1),
        [VariableConfig("v1", "RL_PLC", "DB100.DBW0", "INT", 1, True, 1)],
    )
    device.status = DeviceStatus.ONLINE
    return manager, device


@pytest.mark.asyncio
async def test_hot_reload_never_shares_a_connection_between_generations() -> None:
    """验收②：热重载后候选持有新驱动实例，旧设备对象不再持有驱动（回滚也只能新建连接）。"""
    manager, old = _manager_with_device()
    old_driver = _StubDriver("gen1")
    old.driver = old_driver
    old.status = DeviceStatus.ONLINE

    candidate = manager.prepare_candidate_device(
        DeviceConfig("RL_PLC", "QC", "s7", "192.168.1.61", port=102, slot=1),
        [VariableConfig("v1", "RL_PLC", "DB100.DBW0", "INT", 1, True, 1)],
    )
    candidate_driver = _StubDriver("gen2")
    candidate.driver = candidate_driver
    candidate.status = DeviceStatus.ONLINE

    await manager.swap_device("RL_PLC", candidate)

    assert manager.devices["RL_PLC"] is candidate
    assert candidate.driver is candidate_driver
    assert candidate_driver is not old_driver, "新旧代必须是不同的连接实例"
    assert old.driver is None, "旧设备对象不得继续持有旧驱动（回滚只能新建连接）"
    old_driver.disconnect.assert_awaited(), "旧驱动必须被断开"


@pytest.mark.asyncio
async def test_late_result_from_old_generation_is_rejected() -> None:
    """验收①：慢读跨过重载后返回的迟到结果，必须被 generation 门挡住，不覆盖新代 latest。"""
    manager, old = _manager_with_device()
    old.driver = _StubDriver("gen1")
    old.status = DeviceStatus.ONLINE
    stale_generation = old.generation

    executor = TaskExecutor.__new__(TaskExecutor)
    executor._metrics = _RecordingMetrics()
    executor.device_manager = manager
    written: list = []
    executor._write_to_redis = lambda device, scan_task, points: written.append(points)

    # 旧代 task：其 generation 在热重载后已过期
    candidate = manager.prepare_candidate_device(
        DeviceConfig("RL_PLC", "QC", "s7", "192.168.1.61", port=102, slot=1),
        [VariableConfig("v1", "RL_PLC", "DB100.DBW0", "INT", 1, True, 1)],
    )
    candidate.driver = _StubDriver("gen2")
    await manager.swap_device("RL_PLC", candidate)

    late_task = SimpleNamespace(
        device=old,
        generation=stale_generation,
        scan_task=SimpleNamespace(variables=[], read_blocks=[]),
    )
    accepted = executor._write_if_current(late_task, [object()])

    assert accepted is False, "旧代迟到结果必须被拒绝"
    assert written == [], "旧代迟到结果绝不能写入 latest"
    assert manager.is_current_generation("RL_PLC", stale_generation) is False
    assert manager.current_generation("RL_PLC") == candidate.generation
