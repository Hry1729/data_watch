"""设备管理器测试"""

import json

import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from device.manager import DeviceManager, EndpointPortMapper, PortMappingError
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


def test_rebuild_device_variables_rolls_back_on_setup_failure(collector_config):
    manager = DeviceManager(collector_config)
    manager.register_devices()
    device = manager.devices["TEST001"]
    old_variables = device.variables
    old_scan_tasks = device.scan_tasks

    with patch.object(manager, "_setup_scan_tasks", side_effect=RuntimeError("boom")):
        with pytest.raises(RuntimeError, match="boom"):
            manager.rebuild_device_variables(
                "TEST001",
                [
                    VariableConfig(
                        variable_id="new_var",
                        device_id="TEST001",
                        address="DB100.DBW2",
                        data_type="INT",
                        scan_rate=1,
                        is_storage=True,
                        var_seq=2,
                    )
                ],
            )

    assert device.variables is old_variables
    assert device.scan_tasks is old_scan_tasks


# ---------------------------------------------------------------------------
# T016/T017 — EndpointPortMapper 显式 mapping_file 权威只读模式
#
# Agent 生成的 port_mapping.json 须被 collector 只读消费：endpoint 存在用文件端口、
# 缺失即报错、stale 多余项忽略、绝不动态探测/分配/写回（防端口漂移）。
# ---------------------------------------------------------------------------


def _write_mapping(tmp_path, data):
    f = tmp_path / "port_mapping.json"
    f.write_text(json.dumps(data), encoding="utf-8")
    return str(f)


def test_authoritative_uses_file_port(tmp_path):
    path = _write_mapping(tmp_path, {"172.27.18.221": 9091})
    mapper = EndpointPortMapper(mapping_file=path)
    assert mapper.get_port("172.27.18.221") == 9091


def test_authoritative_localhost_normalization(tmp_path):
    # 文件以 localhost 为键，127.0.0.1 / 空串应归一后命中同一端口。
    path = _write_mapping(tmp_path, {"localhost": 9090})
    mapper = EndpointPortMapper(mapping_file=path)
    assert mapper.get_port("127.0.0.1") == 9090
    assert mapper.get_port("") == 9090


def test_authoritative_missing_endpoint_raises(tmp_path):
    path = _write_mapping(tmp_path, {"172.27.18.221": 9091})
    mapper = EndpointPortMapper(mapping_file=path)
    with pytest.raises(PortMappingError) as excinfo:
        mapper.get_port("10.0.0.99")
    msg = str(excinfo.value)
    assert "10.0.0.99" in msg
    assert path in msg


def test_authoritative_ignores_stale_entries(tmp_path):
    # 文件含从未被请求的 stale endpoint，不应导致任何错误，命中项照常返回。
    path = _write_mapping(
        tmp_path, {"172.27.18.221": 9091, "stale.host.gone": 9099}
    )
    mapper = EndpointPortMapper(mapping_file=path)
    assert mapper.get_port("172.27.18.221") == 9091


def test_authoritative_does_not_allocate_or_write(tmp_path):
    # 缺失 endpoint 必须报错而非分配新端口；且文件内容不被改写。
    path = _write_mapping(tmp_path, {"172.27.18.221": 9091})
    before = (tmp_path / "port_mapping.json").read_text(encoding="utf-8")
    mapper = EndpointPortMapper(mapping_file=path)
    with pytest.raises(PortMappingError):
        mapper.get_port("unmapped.host")
    after = (tmp_path / "port_mapping.json").read_text(encoding="utf-8")
    assert before == after


def test_authoritative_missing_file_raises(tmp_path):
    # 文件不存在 → 构造期 fail-fast，避免静默退化成动态分配导致漂移。
    missing = str(tmp_path / "nope.json")
    with pytest.raises(PortMappingError):
        EndpointPortMapper(mapping_file=missing)


def _center_config(endpoint):
    return CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="VM001",
                device_type="QC",
                protocol="opcda_center",
                ip="127.0.0.1",
                endpoint=endpoint,
                port=0,
                scan_rate=1,
            )
        ],
        variables=[
            VariableConfig(
                variable_id="900001",
                device_id="VM001",
                address="ZPMC.Tag1",
                data_type="REAL",
                scan_rate=1,
                is_storage=True,
                var_seq=1,
            )
        ],
    )


def test_device_manager_authoritative_center_port(tmp_path):
    path = _write_mapping(tmp_path, {"172.27.18.221": 9091})
    manager = DeviceManager(_center_config("172.27.18.221"), port_mapping_file=path)
    manager.register_devices()
    assert manager.devices["VM001"].connection_config["proxy_port"] == 9091


def test_device_manager_authoritative_missing_center_endpoint_raises(tmp_path):
    path = _write_mapping(tmp_path, {"172.27.18.221": 9091})
    manager = DeviceManager(_center_config("10.0.0.99"), port_mapping_file=path)
    with pytest.raises(PortMappingError):
        manager.register_devices()


@pytest.mark.parametrize("bad", ["9091", True, 0, -1, 70000])
def test_authoritative_rejects_invalid_port_value(tmp_path, bad):
    # 损坏的端口值（字符串/布尔/越界/非正）须 fail-fast，而非被静默丢弃成"缺失"。
    path = _write_mapping(tmp_path, {"172.27.18.221": bad})
    with pytest.raises(PortMappingError):
        EndpointPortMapper(mapping_file=path)


@pytest.mark.asyncio
async def test_authoritative_async_uses_file_port(tmp_path):
    path = _write_mapping(tmp_path, {"172.27.18.221": 9091})
    mapper = EndpointPortMapper(mapping_file=path)
    assert await mapper.get_port_async("172.27.18.221") == 9091


@pytest.mark.asyncio
async def test_authoritative_async_missing_raises_no_allocate(tmp_path):
    # 异步版本在权威模式下同样只读：缺失即报错，绝不动态分配/写回。
    path = _write_mapping(tmp_path, {"172.27.18.221": 9091})
    before = (tmp_path / "port_mapping.json").read_text(encoding="utf-8")
    mapper = EndpointPortMapper(mapping_file=path)
    with pytest.raises(PortMappingError):
        await mapper.get_port_async("10.0.0.99")
    after = (tmp_path / "port_mapping.json").read_text(encoding="utf-8")
    assert before == after
