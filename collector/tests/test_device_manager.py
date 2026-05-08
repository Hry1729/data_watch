"""设备管理器测试"""

import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from device.manager import DeviceManager
from device.device import Device, DeviceStatus


@pytest.mark.asyncio
async def test_device_registration(collector_config):
    """测试设备注册"""
    manager = DeviceManager(collector_config)

    # 注册设备
    manager.register_devices()

    # 验证设备注册成功
    assert len(manager.devices) == 1
    assert "TEST001" in manager.devices

    device = manager.devices["TEST001"]
    assert device.device_id == "TEST001"
    assert device.device_type == "QC"
    assert device.protocol == "s7"
    assert len(device.variables) == 1


@pytest.mark.asyncio
async def test_device_connection(collector_config, mock_driver):
    """测试设备连接"""
    manager = DeviceManager(collector_config)
    manager.register_devices()

    # 模拟驱动创建
    with patch.object(manager, '_create_driver', return_value=mock_driver):
        # 连接设备
        result = await manager.connect_device(manager.devices["TEST001"])

        # 验证连接成功
        assert result is True
        assert manager.devices["TEST001"].status == DeviceStatus.ONLINE
        mock_driver.connect.assert_called_once()


@pytest.mark.asyncio
async def test_device_connection_failure(collector_config):
    """测试设备连接失败"""
    manager = DeviceManager(collector_config)
    manager.register_devices()

    # 模拟连接失败
    with patch.object(manager, '_create_driver') as mock_create:
        mock_driver = AsyncMock()
        mock_driver.connect.side_effect = Exception("Connection failed")
        mock_create.return_value = mock_driver

        # 连接设备
        result = await manager.connect_device(manager.devices["TEST001"])

        # 验证连接失败
        assert result is False
        assert manager.devices["TEST001"].status == DeviceStatus.ERROR


@pytest.mark.asyncio
async def test_concurrent_device_connection(collector_config, mock_driver):
    """测试并发设备连接"""
    # 创建多个设备
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id=f"TEST{i:03d}",
                device_type="QC",
                protocol="s7",
                ip="192.168.1.100",
                port=102,
                slot=1,
                scan_rate=1,
            )
            for i in range(5)
        ]
    )

    manager = DeviceManager(config)
    manager.register_devices()

    # 模拟驱动创建
    with patch.object(manager, '_create_driver', return_value=mock_driver):
        # 并发连接所有设备
        await manager.connect_all()

        # 验证所有设备都连接成功
        for device in manager.devices.values():
            assert device.status == DeviceStatus.ONLINE

        # 验证connect_all的日志输出
        mock_driver.connect.assert_called()


@pytest.mark.asyncio
async def test_external_push_device_does_not_create_driver_or_polling_tasks():
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="AGV801",
                device_type="AGV",
                protocol="opcda_sidecar",
                ip="127.0.0.1",
                port=8090,
                scan_rate=1,
            )
        ],
        variables=[
            VariableConfig(
                variable_id="123456",
                device_id="AGV801",
                address="ZPMC.AGV801.Speed",
                data_type="REAL",
                scan_rate=1,
                is_storage=True,
                var_seq=100,
            )
        ],
    )
    manager = DeviceManager(config)
    manager.register_devices()

    device = manager.devices["AGV801"]
    assert len(device.variables) == 1
    assert device.scan_tasks == []

    with patch.object(manager, "_create_driver") as mock_create:
        result = await manager.connect_device(device)

    assert result is True
    assert device.status == DeviceStatus.ONLINE
    assert device.driver is None
    mock_create.assert_not_called()
    assert await manager.check_device_health(device) is True
