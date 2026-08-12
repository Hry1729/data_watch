"""设备管理器测试"""

import asyncio
import json
import sys
from pathlib import Path

import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from device.manager import (
    DeviceManager,
    EndpointPortMapper,
    PortMappingError,
    build_connection_config,
    _modbus_unit_id_from_slot,
)
from device.device import Device, DeviceStatus

_MODBUS_PKG_ROOT = Path(__file__).resolve().parents[2] / "packages" / "modbus_tcp"
if str(_MODBUS_PKG_ROOT) not in sys.path:
    sys.path.insert(0, str(_MODBUS_PKG_ROOT))


def test_modbus_unit_id_from_slot_range():
    assert _modbus_unit_id_from_slot(None) == 1
    assert _modbus_unit_id_from_slot(2) == 2
    assert _modbus_unit_id_from_slot(247) == 247
    for bad in (0, -1, 248):
        with pytest.raises(ValueError):
            _modbus_unit_id_from_slot(bad)


def test_build_connection_config_maps_slot_for_modbus_only():
    modbus = DeviceConfig(
        device_id="M1",
        device_type="PLC",
        protocol="modbus_tcp",
        ip="10.0.0.1",
        port=502,
        slot=2,
    )
    cfg = build_connection_config(modbus)
    assert cfg["unit_id"] == 2
    assert cfg["slot"] == 2

    s7 = DeviceConfig(
        device_id="S1",
        device_type="PLC",
        protocol="s7",
        ip="10.0.0.2",
        port=102,
        slot=1,
    )
    s7_cfg = build_connection_config(s7)
    assert "unit_id" not in s7_cfg
    assert s7_cfg["read_timeout_ms"] is None

    s7.read_timeout_ms = 850
    assert build_connection_config(s7)["read_timeout_ms"] == 850


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
async def test_invalid_modbus_config_does_not_block_other_devices_or_reconnect():
    from drivers.registry import DriverRegistry

    config = CollectorConfig(
        devices=[
            DeviceConfig("BAD", "PLC", "modbus_tcp", "10.0.0.1", port=502, slot=1),
            DeviceConfig("GOOD", "AGV", "opcda_sidecar", "127.0.0.1", port=8090),
        ],
        variables=[
            VariableConfig("bad_type", "BAD", "C0", "WORD", 1, True, 1),
        ],
    )
    registry = DriverRegistry.scan(_MODBUS_PKG_ROOT.parent)
    manager = DeviceManager(config, driver_registry=registry)

    manager.register_devices()
    bad = manager.devices["BAD"]
    assert bad.status == DeviceStatus.CONFIG_ERROR
    assert "只支持 BOOL" in bad.config_error
    assert bad.scan_tasks == []

    await manager.connect_all()

    assert manager.devices["GOOD"].status == DeviceStatus.ONLINE
    assert "BAD" not in manager._reconnect_tasks


def test_modbus_scan_task_attaches_decode_via_hooks():
    """外置 hooks 规划成功后，ScanTask 必须挂上 decode_read_results。"""
    from drivers.registry import DriverRegistry
    from device.device import Variable

    registry = DriverRegistry.discover(_MODBUS_PKG_ROOT.parent)
    hooks = registry.get("modbus_tcp")
    assert hooks is not None and hooks.build_read_plan is not None

    device = Device("M1", "PLC", "modbus_tcp", {"unit_id": 1})
    device.external_driver_hooks = hooks
    device.add_variable(Variable("v1", "C0", "BOOL", 1.0))
    device.build_scan_tasks()
    assert len(device.scan_tasks) == 1
    assert device.scan_tasks[0].decode_read_results is hooks.decode_read_results


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
async def test_connect_impl_disconnects_previous_driver_before_replace(collector_config):
    """重试/覆盖前必须断开旧 driver；每次新建失败实例也须各自 disconnect 一次。"""
    manager = DeviceManager(collector_config)
    manager.register_devices()
    device = manager.devices["TEST001"]

    old_driver = AsyncMock()
    old_driver.disconnect = AsyncMock()
    device.driver = old_driver

    created: list = []

    def _factory(_protocol: str):
        new_driver = AsyncMock()
        new_driver.connect = AsyncMock(side_effect=Exception("boom"))
        new_driver.disconnect = AsyncMock()
        created.append(new_driver)
        return new_driver

    with patch.object(manager, "_create_driver", side_effect=_factory):
        with patch.object(manager, "_report_device_status", new=AsyncMock()):
            with pytest.raises(Exception, match="boom"):
                await manager._connect_device_impl(device)

    old_driver.disconnect.assert_awaited_once()
    assert created
    for drv in created:
        drv.disconnect.assert_awaited_once()
    assert device.driver is None
    assert device.status == DeviceStatus.ERROR


@pytest.mark.asyncio
async def test_connect_impl_disconnect_timeout_does_not_block_replace(collector_config):
    """旧 driver disconnect 挂起时必须超时后继续覆盖，不得卡住重连。"""
    manager = DeviceManager(collector_config)
    manager.register_devices()
    device = manager.devices["TEST001"]

    hung = AsyncMock()

    async def _never_finish():
        await asyncio.sleep(3600)

    hung.disconnect = _never_finish
    device.driver = hung

    new_driver = AsyncMock()
    new_driver.connect = AsyncMock()
    new_driver.disconnect = AsyncMock()

    with patch("device.manager._DRIVER_DISCONNECT_TIMEOUT_SEC", 0.05):
        with patch.object(manager, "_create_driver", return_value=new_driver):
            with patch.object(manager, "_report_device_status", new=AsyncMock()):
                ok = await manager._connect_device_impl(device)

    assert ok is True
    assert device.driver is new_driver
    assert device.status == DeviceStatus.ONLINE
    new_driver.connect.assert_awaited_once()


@pytest.mark.asyncio
async def test_safe_disconnect_driver_is_idempotent(collector_config):
    """重复清理与空 driver 必须幂等，不得抛错。"""
    manager = DeviceManager(collector_config)
    driver = AsyncMock()
    driver.disconnect = AsyncMock()

    await manager._safe_disconnect_driver(driver, device_id="T1", reason="test")
    await manager._safe_disconnect_driver(driver, device_id="T1", reason="test")
    await manager._safe_disconnect_driver(None, device_id="T1", reason="test")

    assert driver.disconnect.await_count == 2


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
    old_generation = device.generation

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
    assert device.generation == old_generation


def test_rebuild_device_variables_advances_generation_on_success(collector_config):
    manager = DeviceManager(collector_config)
    manager.register_devices()
    device = manager.devices["TEST001"]
    old_generation = device.generation

    rebuilt = manager.rebuild_device_variables(
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

    assert rebuilt is device
    assert device.generation == old_generation + 1
    assert manager.is_current_generation(device.device_id, device.generation)


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


def test_authoritative_reload_reads_updated_mapping(tmp_path):
    path = _write_mapping(tmp_path, {"172.27.18.221": 9091})
    mapper = EndpointPortMapper(mapping_file=path)
    with pytest.raises(PortMappingError):
        mapper.get_port("10.0.0.99")

    (tmp_path / "port_mapping.json").write_text(
        json.dumps({"172.27.18.221": 9091, "10.0.0.99": 9092}),
        encoding="utf-8",
    )
    mapper.reload()

    assert mapper.get_port("10.0.0.99") == 9092


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


def test_device_manager_center_missing_endpoint_reloads_once():
    config = _center_config("10.0.0.99")
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    fake_mapper = MagicMock()
    fake_mapper.get_port.side_effect = [PortMappingError("missing"), 9092]
    manager._port_mapper = fake_mapper

    device = manager.add_device(config.devices[0], config.variables)

    assert device is not None
    assert device.connection_config["proxy_port"] == 9092
    fake_mapper.reload.assert_called_once()
    assert fake_mapper.get_port.call_count == 2


# ---------------------------------------------------------------------------
# A方-11(a) — center 设备的 use_circuit_breaker 透传
#
# 重连路径调 connect_device(device, use_circuit_breaker=False) 想绕过熔断强制重试；
# 此前 center 设备早 return 到 _connect_center_collection_device，总走熔断 → 熔断
# OPEN 期间重连空转。修复后 center 路径与普通设备路径对称地尊重该参数。
# ---------------------------------------------------------------------------


@pytest.mark.asyncio
async def test_center_reconnect_bypasses_circuit_breaker():
    """use_circuit_breaker=False（重连）→ 直接调 impl，不创建/触发熔断器。"""
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    device = MagicMock()
    device.device_id = "VM001"
    device.protocol = "opcda_center"
    device.status = DeviceStatus.ERROR

    with patch.object(
        manager,
        "_connect_center_collection_device_impl",
        new=AsyncMock(return_value=True),
    ) as impl:
        result = await manager.connect_device(device, use_circuit_breaker=False)

    assert result is True
    impl.assert_awaited_once_with(device)
    assert "VM001" not in manager._circuit_breakers  # 绕过：熔断器未创建


@pytest.mark.asyncio
async def test_connect_all_uses_device_snapshot(collector_config):
    """A方-5：connect_all 用同源快照；连接期间往 devices 注入新设备不致 zip 错配。"""
    manager = DeviceManager(collector_config)
    manager.register_devices()
    snapshot_size = len(manager.devices)

    async def connect_and_mutate(device, use_circuit_breaker=True):
        # 模拟连接期间热更新往 devices 注入新设备
        manager.devices["INJECTED_DURING_CONNECT"] = MagicMock()
        return True

    with patch.object(manager, "connect_device", side_effect=connect_and_mutate):
        with patch.object(manager, "schedule_reconnect", new=AsyncMock()) as sched:
            await manager.connect_all()  # 快照隔离注入项，不抛、不错配

    # 本轮只处理快照内设备：全部成功 → 无 reconnect 调度；注入项不在本轮范围
    assert snapshot_size >= 1
    sched.assert_not_awaited()
    assert "INJECTED_DURING_CONNECT" in manager.devices


@pytest.mark.asyncio
async def test_center_connect_with_circuit_breaker_creates_breaker():
    """对照：use_circuit_breaker=True（初始连接/默认）→ 仍走熔断器。"""
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    device = MagicMock()
    device.device_id = "VM001"
    device.protocol = "opcda_center"
    device.status = DeviceStatus.ERROR

    with patch.object(
        manager,
        "_connect_center_collection_device_impl",
        new=AsyncMock(return_value=True),
    ):
        result = await manager.connect_device(device, use_circuit_breaker=True)

    assert result is True
    assert "VM001" in manager._circuit_breakers  # 走熔断：熔断器已创建


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


def test_ge_connection_config_uses_global_payload_and_device_override():
    config = CollectorConfig(
        ge_max_payload_bytes=500,
        ge_parallel_channels=2,
        ge_batch_deadline_ms=300,
    )
    manager = DeviceManager(config)

    inherited = manager._build_connection_config(
        DeviceConfig("GE1", "PLC", "ge_srtp", "127.0.0.1")
    )
    overridden = manager._build_connection_config(
        DeviceConfig(
            "GE2",
            "PLC",
            "ge_srtp",
            "127.0.0.1",
            ge_max_payload_bytes=1000,
            ge_parallel_channels=1,
            ge_batch_deadline_ms=100,
        )
    )

    assert inherited["ge_max_payload_bytes"] == 500
    assert overridden["ge_max_payload_bytes"] == 1000
    assert inherited["ge_parallel_channels"] == 2
    assert overridden["ge_parallel_channels"] == 1
    assert inherited["ge_batch_deadline_ms"] == 300
    assert overridden["ge_batch_deadline_ms"] == 100

def test_device_manager_creates_bundled_aliases_through_unified_registry():
    from drivers.opcua_driver import OPCUADriver
    from drivers.s7_driver import S7Driver

    manager = DeviceManager(CollectorConfig())

    registry = manager._resolve_driver_registry()
    assert registry.get("s7").source == "bundled"
    assert registry.get("s7").protected is True
    assert isinstance(manager._create_driver("snap7"), S7Driver)
    assert isinstance(manager._create_driver("opc"), OPCUADriver)
