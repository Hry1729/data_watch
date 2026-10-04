"""D24 回归：OPC DA center 的异常必须归一化，否则中心采集的熔断器永不 OPEN。

已核实的根因：
- 中心采集熔断器用 ``expected_exception=DeviceConnectionError``
  （collector/device/manager.py `_connect_center_collection_device`）；
- 而 OPC DA center 驱动抛的是**内置** `ConnectionError`
  （packages/opcda_center/opcda_center/driver.py）；
- `DeviceConnectionError` 继承 `CollectorError`，与内置 `ConnectionError`/`OSError`
  **无继承关系** ⇒ `except expected_exception` 拦不到 ⇒ 该设备**永远不会 OPEN**
  （普通驱动走 DeviceConnectionError 所以正常，故"整个熔断器不工作"不成立）。

修法：在 `_connect_center_collection_device_impl` 的失败分支把内置 `ConnectionError`/`OSError`
归一化为 `DeviceConnectionError`。重连路径仍以 `use_circuit_breaker=False` 调用、不接入熔断，
因此不会造成"设备无法恢复"。
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

from core.config import CollectorConfig, DeviceConfig, VariableConfig  # noqa: E402
from core.exceptions import DeviceConnectionError  # noqa: E402
from device.device import DeviceStatus  # noqa: E402
from device.manager import DeviceManager  # noqa: E402


class _FailingConnectDriver:
    """connect() 抛**内置** ConnectionError（与 OPC DA center 驱动一致）。"""

    def __init__(self, exc: BaseException) -> None:
        self._exc = exc
        self.is_connected = False

    async def connect(self, _config) -> None:
        raise self._exc

    async def disconnect(self) -> None:
        return None


def _center_manager(error: BaseException):
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager.register_devices()
    manager._report_device_status = AsyncMock()
    manager._safe_disconnect_driver = AsyncMock()
    device = manager.add_device(
        DeviceConfig("OPC01", "QC", "opcda_center", "10.0.0.5", port=0, slot=1),
        [VariableConfig("v1", "OPC01", "ITEM", "INT", 1, True, 1)],
    )
    device.connection_config = {
        "endpoint": "10.0.0.5",
        "proxy_port": 9090,
        "opc_server": "Kepware.KEPServerEX.V6",
    }
    manager._create_driver = lambda _protocol: _FailingConnectDriver(error)
    return manager, device


def test_device_connection_error_is_unrelated_to_builtin_connection_error() -> None:
    """把根因钉死为契约事实：两者无继承关系，所以 expected_exception 拦不到内置异常。"""
    assert not isinstance(ConnectionError("proxy down"), DeviceConnectionError)
    assert not issubclass(DeviceConnectionError, (ConnectionError, OSError))


@pytest.mark.asyncio
async def test_center_connect_normalizes_builtin_connection_error() -> None:
    manager, device = _center_manager(ConnectionError("proxy down"))

    with pytest.raises(DeviceConnectionError) as excinfo:
        await manager._connect_center_collection_device_impl(device)

    assert "proxy down" in str(excinfo.value)
    assert device.status == DeviceStatus.ERROR


@pytest.mark.asyncio
async def test_center_breaker_now_opens_on_repeated_driver_failures() -> None:
    """修复的核心效果：连续失败必须让**中心采集**设备的熔断器 OPEN。

    注意 connect_device(use_circuit_breaker=True) 的契约是**捕获异常并返回 False**（它自己记录
    日志），并不向外抛；因此这里断言返回值与熔断器状态，而不是 pytest.raises。
    """
    manager, device = _center_manager(ConnectionError("proxy down"))

    for _ in range(5):
        assert await manager.connect_device(device, use_circuit_breaker=True) is False

    breaker = manager._circuit_breakers[device.device_id]
    assert breaker.state == "open", f"中心设备熔断器未打开: state={breaker.state}"
    assert breaker.failure_count >= 5


@pytest.mark.asyncio
async def test_center_reconnect_path_does_not_open_the_breaker() -> None:
    """重连路径不得接入熔断——否则熔断 OPEN 期间设备永久无法恢复。

    **实测行为（与普通设备路径不对称，已登记）**：`use_circuit_breaker=False` 时中心路径
    直接 `return await ..._impl(device)`，异常会**抛出**给调用方，而普通设备路径会捕获并返回
    False。二者的共同点是**都不创建/不驱动熔断器**；此处按实测断言"未创建熔断器"，
    并把不对称单独记录，而不是把测试改成期望一个并不存在的行为。
    """
    manager, device = _center_manager(ConnectionError("proxy down"))

    for _ in range(6):
        with pytest.raises(DeviceConnectionError):
            await manager.connect_device(device, use_circuit_breaker=False)

    assert device.device_id not in manager._circuit_breakers, (
        "重连路径不应创建熔断器；若出现 OPEN，熔断期间设备将无法恢复"
    )


@pytest.mark.asyncio
async def test_normal_device_path_still_counts_failures() -> None:
    """对照：普通（非 center）设备的熔断行为不受本次改动影响。"""
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager.register_devices()
    manager._report_device_status = AsyncMock()
    manager._safe_disconnect_driver = AsyncMock()
    device = manager.add_device(
        DeviceConfig("S701", "QC", "s7", "192.168.1.10", port=102, slot=1),
        [VariableConfig("v1", "S701", "DB1.DBW0", "INT", 1, True, 1)],
    )

    manager._create_driver = lambda _protocol: _FailingConnectDriver(
        DeviceConnectionError("plc down")
    )

    for _ in range(5):
        assert await manager.connect_device(device, use_circuit_breaker=True) is False

    assert manager._circuit_breakers[device.device_id].state == "open"
