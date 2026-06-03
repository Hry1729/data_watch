"""运行期设备拓扑变更测试（#6）

覆盖 4 个 mock 场景 + 防重复：
1. 新增普通 PLC 设备：注册、连接、生成采集任务。
2. 删除已有设备：driver 断开、reconnect task 取消、从 devices/熔断器移除。
3. 新增 external-push 设备：不连接 PLC、不建轮询任务。
4. 新增 center 设备：调用端口映射、生成任务，不要求真实代理进程。
"""

from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from device.device import DeviceStatus
from device.manager import DeviceManager


def _empty_manager() -> DeviceManager:
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager.register_devices()  # 初始化 _port_mapper，devices 为空
    return manager


@pytest.mark.asyncio
async def test_add_device_normal_plc_connects_and_builds_tasks():
    """场景1：新增普通 PLC 设备 —— 注册、连接、生成采集任务。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("NEW_PLC", "QC", "s7", "192.168.1.50", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "NEW_PLC", "DB100.DBW0", "INT", 1, True, 1)]

    device = manager.add_device(dev_cfg, var_cfgs)

    assert device is not None
    assert "NEW_PLC" in manager.devices
    assert len(device.variables) == 1
    assert len(device.scan_tasks) == 1  # 普通 PLC 生成轮询任务

    with patch.object(manager, "_create_driver") as mock_create:
        mock_driver = AsyncMock()
        mock_driver.connect = AsyncMock()
        mock_driver.is_connected = True
        mock_create.return_value = mock_driver

        ok = await manager.connect_device(device)

    assert ok is True
    assert device.status == DeviceStatus.ONLINE


@pytest.mark.asyncio
async def test_remove_device_cancels_reconnect_and_disconnects():
    """场景2：删除已有设备 —— driver 断开、reconnect task 取消、移除。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("DEL_PLC", "QC", "s7", "192.168.1.51", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "DEL_PLC", "DB100.DBW0", "INT", 1, True, 1)]
    device = manager.add_device(dev_cfg, var_cfgs)

    # 模拟已连接并因故障进入重连
    device.driver = AsyncMock()
    device.driver.disconnect = AsyncMock()
    device.status = DeviceStatus.ERROR
    manager._circuit_breakers["DEL_PLC"] = MagicMock()
    await manager.schedule_reconnect(device)
    assert "DEL_PLC" in manager._reconnect_tasks

    await manager.remove_device("DEL_PLC")

    device.driver.disconnect.assert_awaited_once()
    assert "DEL_PLC" not in manager._reconnect_tasks
    assert "DEL_PLC" not in manager.devices
    assert "DEL_PLC" not in manager._circuit_breakers


@pytest.mark.asyncio
async def test_add_external_push_device_no_tasks_no_plc():
    """场景3：新增 external-push 设备 —— 不建轮询任务、不连接 PLC。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("EXT_PUSH", "AGV", "opcda_sidecar", "127.0.0.1")
    var_cfgs = [VariableConfig("v1", "EXT_PUSH", "OPD.Tag1", "REAL", 1, True, 1)]

    device = manager.add_device(dev_cfg, var_cfgs)

    assert device is not None
    assert device.scan_tasks == []  # 外部推送无轮询任务

    with patch.object(manager, "_create_driver") as mock_create:
        ok = await manager.connect_device(device)

    assert ok is True
    assert device.status == DeviceStatus.ONLINE
    assert device.driver is None  # 不创建 PLC driver
    mock_create.assert_not_called()


@pytest.mark.asyncio
async def test_add_center_device_allocates_port_and_builds_tasks():
    """场景4：新增 center 设备 —— 调用端口映射、生成任务路径正确。"""
    manager = _empty_manager()
    # 用 mock 端口映射器，避免真实绑定端口 / 写映射文件
    fake_mapper = MagicMock()
    fake_mapper.get_port.return_value = 9099
    manager._port_mapper = fake_mapper

    dev_cfg = DeviceConfig(
        "CENTER01", "AGV", "opcda_center", "10.0.0.5",
        endpoint="10.0.0.5", opc_server="OPD.Server.1",
    )
    var_cfgs = [VariableConfig("v1", "CENTER01", "OPD.Server.AGV.Tag1", "REAL", 1, True, 1)]

    device = manager.add_device(dev_cfg, var_cfgs)

    assert device is not None
    fake_mapper.get_port.assert_called_once_with("10.0.0.5")
    assert device.connection_config["proxy_port"] == 9099
    assert len(device.scan_tasks) == 1  # center 协议生成轮询任务


@pytest.mark.asyncio
async def test_add_device_rejects_duplicate():
    """约束3：device_id 已存在时拒绝，不覆盖已有 Device 对象。"""
    manager = _empty_manager()
    dev_cfg = DeviceConfig("DUP", "QC", "s7", "192.168.1.52", port=102, slot=1)
    var_cfgs = [VariableConfig("v1", "DUP", "DB100.DBW0", "INT", 1, True, 1)]

    first = manager.add_device(dev_cfg, var_cfgs)
    assert first is not None

    second = manager.add_device(dev_cfg, var_cfgs)
    assert second is None
    assert manager.devices["DUP"] is first  # 原对象未被覆盖
