"""B36 回归（A 半）：健康检查必须区分"执行资源拥塞"与"设备故障"。

已核实的事实：
- `main.py:167` 的有界 PLC 读池被注入驱动，而 S7 的 connect/disconnect/read/**ping**
  四类操作**共用该池**（packages/s7/s7/driver.py L72/L90/L126/L414-418）；
- `BoundedThreadPoolExecutor.submit` 在 pending 超限时抛 `ExecutorSaturatedError`；
- `health.py:51` 由 `check_device_health` 返回 False 直接 `schedule_reconnect`。

⇒ 池饱和时 ping 被立即拒绝，修复前会被当作设备故障 ⇒ **误报 + 重连风暴**。

本回归同时锁住两个方向：
1. 拥塞（`ExecutorSaturatedError`）**不得**被裁决为故障；
2. 真正的故障（其它异常 / ping 返回 False）**仍然**必须被检出——避免"为了不误报而放宽到
   什么都不报"。
"""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import AsyncMock

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from collector.bounded_executor import ExecutorSaturatedError  # noqa: E402
from core.config import CollectorConfig, DeviceConfig, VariableConfig  # noqa: E402
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


class _PingDriver:
    """只实现 ping 的替身驱动。"""

    def __init__(self, behaviour) -> None:
        self._behaviour = behaviour

    async def ping(self) -> bool:
        return self._behaviour()


def _manager_with_online_device(protocol: str = "s7"):
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager.register_devices()
    manager._metrics = _RecordingMetrics()
    manager._report_device_status = AsyncMock()
    device = manager.add_device(
        DeviceConfig("HC_PLC", "QC", protocol, "192.168.1.50", port=102, slot=1),
        [VariableConfig("v1", "HC_PLC", "DB100.DBW0", "INT", 1, True, 1)],
    )
    device.status = DeviceStatus.ONLINE
    return manager, device


def _behaviour_raises(exc: Exception):
    def _run():
        raise exc

    return _run


@pytest.mark.asyncio
async def test_saturation_is_not_reported_as_device_fault() -> None:
    """池满（拥塞）不得裁决为设备故障，也不得计入故障上报。"""
    manager, device = _manager_with_online_device()
    device.driver = _PingDriver(_behaviour_raises(ExecutorSaturatedError("pool full")))

    healthy = await manager.check_device_health(device)

    assert healthy is True, "拥塞不得被判为不健康"
    assert device.status == DeviceStatus.ONLINE, "拥塞不得把设备置为 ERROR"
    assert manager._metrics.increments.get("health_check_saturated_count") == 1, "拥塞必须可见"
    manager._report_device_status.assert_not_awaited()


@pytest.mark.asyncio
async def test_real_failure_is_still_detected() -> None:
    """对照：真正的故障仍必须被检出（不能为了不误报而放宽）。"""
    manager, device = _manager_with_online_device()
    device.driver = _PingDriver(_behaviour_raises(RuntimeError("plc unreachable")))

    healthy = await manager.check_device_health(device)

    assert healthy is False
    assert device.status == DeviceStatus.ERROR
    manager._report_device_status.assert_awaited()
    assert "health_check_saturated_count" not in manager._metrics.increments


@pytest.mark.asyncio
async def test_ping_false_is_still_a_fault() -> None:
    manager, device = _manager_with_online_device()
    device.driver = _PingDriver(lambda: False)

    healthy = await manager.check_device_health(device)

    assert healthy is False
    assert device.status == DeviceStatus.ERROR


@pytest.mark.asyncio
async def test_center_collection_proxy_ping_saturation_is_not_a_fault() -> None:
    """中心采集（代理 ping）路径同样必须区分拥塞与故障。"""
    manager, device = _manager_with_online_device(protocol="opcda_center")
    device.driver = _PingDriver(_behaviour_raises(ExecutorSaturatedError("pool full")))

    healthy = await manager.check_device_health(device)

    assert healthy is True
    assert device.status == DeviceStatus.ONLINE
    assert manager._metrics.increments.get("health_check_saturated_count") == 1
