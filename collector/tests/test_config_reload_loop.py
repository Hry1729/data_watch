"""配置重载循环变更分类测试。"""

import asyncio
import logging
from unittest.mock import AsyncMock, MagicMock

import pytest

import main as collector_main
from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.config_db import DeviceSignature
from main import CollectorApp
from device.device import Device


def _device(
    *,
    device_id="PLC001",
    protocol="s7",
    endpoint="10.0.0.1",
    ip="10.0.0.1",
    scan_rate=1.0,
    config_version=1,
):
    return DeviceConfig(
        device_id=device_id,
        device_type="QC",
        protocol=protocol,
        ip=ip,
        endpoint=endpoint,
        port=102,
        rack=0,
        slot=1,
        scan_rate=scan_rate,
        config_version=config_version,
    )


def _variable(device_id="PLC001"):
    return VariableConfig(
        variable_id="v1",
        device_id=device_id,
        address="DB100.DBW0",
        data_type="INT",
        scan_rate=1,
        is_storage=True,
        var_seq=1,
    )


def _signature(
    *,
    config_version=1,
    endpoint="10.0.0.1",
    protocol_id=1,
    rack=0,
    slot=1,
    scan_cycle=1000,
):
    return DeviceSignature(
        config_version=config_version,
        endpoint=endpoint,
        protocol_id=protocol_id,
        rack=rack,
        slot=slot,
        scan_cycle=scan_cycle,
    )


class _FakeConfigDB:
    def __init__(self, signatures, devices, variables):
        self._signatures = list(signatures)
        self._devices = devices
        self._variables = variables
        self._calls = 0
        self.app = None

    async def get_device_config_versions(self):
        index = min(self._calls, len(self._signatures) - 1)
        value = self._signatures[index]
        self._calls += 1
        if self._calls >= len(self._signatures):
            self.app._shutdown.set()
        return value

    async def load_devices_and_variables(self):
        return self._devices, self._variables


def _make_app(fake_db, devices, variables):
    app = CollectorApp.__new__(CollectorApp)
    app.config = CollectorConfig(devices=devices, variables=variables)
    app.config_db_manager = fake_db
    app._shutdown = asyncio.Event()
    app.opcda_mapper = MagicMock()
    app.ingest_server = None
    app._apply_device_variables = AsyncMock()
    app._remove_device = AsyncMock()
    app._add_device = AsyncMock()
    app._replace_device_atomic = AsyncMock()
    app.device_manager = MagicMock()
    app.device_manager.devices = {device.device_id: MagicMock() for device in devices}
    fake_db.app = app
    return app


async def _run_reload_loop(monkeypatch, app):
    async def _no_sleep(_seconds):
        return None

    monkeypatch.setattr(collector_main.asyncio, "sleep", _no_sleep)
    await app._config_reload_loop()


@pytest.mark.asyncio
async def test_runtime_add_installs_tasks_before_slow_connection_finishes():
    app = CollectorApp.__new__(CollectorApp)
    device = MagicMock()
    device.scan_tasks = [MagicMock()]
    device.status.value = "offline"
    app.device_manager = MagicMock()
    app.device_manager.add_device.return_value = device
    app.device_manager.schedule_reconnect = AsyncMock()
    app.scheduler = MagicMock()
    app.scheduler.reload_device_tasks = AsyncMock()

    connection_started = asyncio.Event()
    release_connection = asyncio.Event()

    async def slow_connect(_device):
        connection_started.set()
        await release_connection.wait()
        return False

    app.device_manager.connect_device = AsyncMock(side_effect=slow_connect)
    add_task = asyncio.create_task(
        app._add_device(_device(device_id="NEW_PLC"), [_variable("NEW_PLC")])
    )
    await asyncio.wait_for(connection_started.wait(), timeout=1)
    try:
        app.scheduler.reload_device_tasks.assert_awaited_once()
    finally:
        release_connection.set()
    await add_task
    app.device_manager.schedule_reconnect.assert_awaited_once_with(device)

@pytest.mark.asyncio
async def test_config_reload_version_only_uses_variable_rebuild(monkeypatch):
    old_device = _device(config_version=1)
    new_device = _device(config_version=2)
    variables = [_variable()]
    fake_db = _FakeConfigDB(
        [
            {"PLC001": _signature(config_version=1)},
            {"PLC001": _signature(config_version=2)},
        ],
        [new_device],
        variables,
    )
    app = _make_app(fake_db, [old_device], variables)

    await _run_reload_loop(monkeypatch, app)

    app._apply_device_variables.assert_awaited_once_with("PLC001", new_device, variables)
    app._remove_device.assert_not_awaited()
    app._add_device.assert_not_awaited()
    app._replace_device_atomic.assert_not_awaited()


@pytest.mark.asyncio
async def test_config_reload_reconciles_runtime_device_removed_during_startup(monkeypatch):
    active_device = _device(device_id="PLC001")
    stale_device = _device(device_id="AGV801")
    variables = [_variable("PLC001")]
    current = {"PLC001": _signature()}
    fake_db = _FakeConfigDB([current, current], [active_device], variables)
    app = _make_app(fake_db, [active_device, stale_device], variables)

    await _run_reload_loop(monkeypatch, app)

    app._remove_device.assert_awaited_once_with("AGV801")
    app._add_device.assert_not_awaited()


@pytest.mark.asyncio
async def test_config_reload_device_field_change_rebuilds_device(monkeypatch):
    old_device = _device(endpoint="10.0.0.1", scan_rate=1.0)
    new_device = _device(endpoint="10.0.0.2", ip="10.0.0.2", scan_rate=0.5)
    variables = [_variable()]
    fake_db = _FakeConfigDB(
        [
            {"PLC001": _signature(endpoint="10.0.0.1", scan_cycle=1000)},
            {"PLC001": _signature(endpoint="10.0.0.2", scan_cycle=500)},
        ],
        [new_device],
        variables,
    )
    app = _make_app(fake_db, [old_device], variables)

    await _run_reload_loop(monkeypatch, app)

    app._replace_device_atomic.assert_awaited_once_with(
        "PLC001", new_device, variables
    )
    app._remove_device.assert_not_awaited()
    app._add_device.assert_not_awaited()
    app._apply_device_variables.assert_not_awaited()


@pytest.mark.asyncio
async def test_config_reload_device_rebuild_failure_retries_next_round(monkeypatch):
    old_device = _device(endpoint="10.0.0.1")
    new_device = _device(endpoint="10.0.0.2", ip="10.0.0.2")
    variables = [_variable()]
    fake_db = _FakeConfigDB(
        [
            {"PLC001": _signature(endpoint="10.0.0.1")},
            {"PLC001": _signature(endpoint="10.0.0.2")},
            {"PLC001": _signature(endpoint="10.0.0.2")},
        ],
        [new_device],
        variables,
    )
    app = _make_app(fake_db, [old_device], variables)
    app._replace_device_atomic.side_effect = [RuntimeError("boom"), None]
    app.device_manager.get_device.return_value = None

    await _run_reload_loop(monkeypatch, app)

    assert app._replace_device_atomic.await_count == 2
    app._remove_device.assert_not_awaited()
    app._add_device.assert_not_awaited()


@pytest.mark.asyncio
async def test_invalid_modbus_reload_keeps_previous_generation(monkeypatch):
    old_device = _device(protocol="modbus_tcp", endpoint="10.0.0.1")
    new_device = _device(protocol="modbus_tcp", endpoint="10.0.0.2", ip="10.0.0.2")
    variables = [
        VariableConfig("v1", "PLC001", "C0", "WORD", 1, True, 1),
    ]
    fake_db = _FakeConfigDB(
        [
            {"PLC001": _signature(endpoint="10.0.0.1")},
            {"PLC001": _signature(endpoint="10.0.0.2")},
        ],
        [new_device],
        variables,
    )
    app = _make_app(fake_db, [old_device], variables)
    previous = Device("PLC001", "QC", "modbus_tcp", {"ip": "10.0.0.1", "unit_id": 1})
    previous.scan_tasks = [MagicMock()]
    app.device_manager.validate_device_config.return_value = "Modbus coil 只支持 BOOL"
    app.device_manager.get_device.return_value = previous

    await _run_reload_loop(monkeypatch, app)

    app._replace_device_atomic.assert_not_awaited()
    app._remove_device.assert_not_awaited()
    app._add_device.assert_not_awaited()
    assert previous.scan_tasks
    assert previous.config_error == "Modbus coil 只支持 BOOL"


@pytest.mark.asyncio
async def test_config_reload_opcda_device_change_warns(monkeypatch, caplog):
    old_device = _device(protocol="opcda_center", endpoint="10.0.0.1")
    new_device = _device(
        protocol="opcda_center",
        endpoint="10.0.0.2",
        ip="10.0.0.2",
    )
    variables = [_variable()]
    fake_db = _FakeConfigDB(
        [
            {"PLC001": _signature(endpoint="10.0.0.1")},
            {"PLC001": _signature(endpoint="10.0.0.2")},
        ],
        [new_device],
        variables,
    )
    app = _make_app(fake_db, [old_device], variables)

    caplog.set_level(logging.WARNING)
    await _run_reload_loop(monkeypatch, app)

    assert "OPC DA device PLC001 config changed" in caplog.text
    app._replace_device_atomic.assert_awaited_once_with(
        "PLC001", new_device, variables
    )
    app._remove_device.assert_not_awaited()
    app._add_device.assert_not_awaited()


@pytest.mark.asyncio
async def test_replace_keeps_old_when_scheduler_install_fails(monkeypatch):
    """scheduler 安装新任务失败时，设备仍为旧代际（IP / config_version / slot）。"""
    from device.device import ScanTask, Variable
    from device.manager import DeviceManager

    old = Device(
        "PLC001",
        "QC",
        "s7",
        {"ip": "10.0.0.1", "endpoint": "10.0.0.1", "port": 102, "rack": 0, "slot": 0},
        config_version=1,
    )
    old.active = True
    old.generation = 1
    old.variables = [
        Variable("v1", "DB100.DBW0", "INT", 1.0),
    ]
    old.scan_tasks = [ScanTask(scan_rate=1.0, variables=old.variables)]
    old.driver = MagicMock()

    new_cfg = _device(
        endpoint="10.0.0.2",
        ip="10.0.0.2",
        config_version=2,
    )
    # 新配置显式 slot=1，与旧 slot=0 区分
    new_cfg.slot = 1
    variables = [_variable()]

    app = CollectorApp.__new__(CollectorApp)
    app.device_manager = DeviceManager.__new__(DeviceManager)
    app.device_manager.devices = {"PLC001": old}
    app.device_manager._device_generations = {"PLC001": 1}
    app.device_manager._generation_locks = {}
    app.device_manager._generation_registry_lock = __import__("threading").Lock()
    app.device_manager._port_mapper = None
    app.device_manager._port_mapping_file = None
    app.device_manager._driver_registry = None
    app.device_manager._resolve_driver_registry = MagicMock(return_value=None)
    app.device_manager._safe_disconnect_driver = AsyncMock()
    # B3：swap_device 现在会收尾旧代重连任务，替身需提供该属性。
    app.device_manager._reconnect_tasks = {}
    from tests.package_support import source_registry
    app.device_manager._attach_external_hooks = lambda device: setattr(device, "external_driver_hooks", source_registry().get(device.protocol))

    # 候选连接直接成功，不真正建驱动
    async def _fake_connect(device):
        device.driver = MagicMock(name="candidate_driver")
        return True

    app.device_manager.connect_device = _fake_connect
    app.scheduler = MagicMock()
    app.scheduler.reload_device_tasks = AsyncMock(
        side_effect=RuntimeError("install boom")
    )

    with pytest.raises(RuntimeError, match="install boom"):
        await CollectorApp._replace_device_atomic(app, "PLC001", new_cfg, variables)

    kept = app.device_manager.get_device("PLC001")
    assert kept is old
    assert kept.connection_config.get("ip") == "10.0.0.1"
    assert kept.config_version == 1
    assert kept.connection_config.get("slot") == 0
    # 回滚后应尝试重装旧任务
    assert app.scheduler.reload_device_tasks.await_count >= 2


@pytest.mark.asyncio
async def test_replace_rollback_reconnect_false_schedules_retry(monkeypatch):
    """回滚时先 connect；返回 False 则 schedule_reconnect，且发生在重装任务之前。"""
    from device.device import ScanTask, Variable
    from device.manager import DeviceManager

    old = Device(
        "PLC001",
        "QC",
        "s7",
        {"ip": "10.0.0.1", "endpoint": "10.0.0.1", "port": 102, "rack": 0, "slot": 0},
        config_version=1,
    )
    old.active = True
    old.generation = 1
    old.variables = [Variable("v1", "DB100.DBW0", "INT", 1.0)]
    old.scan_tasks = [ScanTask(scan_rate=1.0, variables=old.variables)]
    old.driver = MagicMock()

    new_cfg = _device(endpoint="10.0.0.2", ip="10.0.0.2", config_version=2)
    new_cfg.slot = 1
    variables = [_variable()]

    app = CollectorApp.__new__(CollectorApp)
    app.device_manager = DeviceManager.__new__(DeviceManager)
    app.device_manager.devices = {"PLC001": old}
    app.device_manager._device_generations = {"PLC001": 1}
    app.device_manager._generation_locks = {}
    app.device_manager._generation_registry_lock = __import__("threading").Lock()
    app.device_manager._port_mapper = None
    app.device_manager._port_mapping_file = None
    app.device_manager._driver_registry = None
    app.device_manager._resolve_driver_registry = MagicMock(return_value=None)
    app.device_manager._safe_disconnect_driver = AsyncMock()
    # B3：swap_device 现在会收尾旧代重连任务，替身需提供该属性。
    app.device_manager._reconnect_tasks = {}
    from tests.package_support import source_registry
    app.device_manager._attach_external_hooks = lambda device: setattr(device, "external_driver_hooks", source_registry().get(device.protocol))
    app.device_manager.schedule_reconnect = AsyncMock()

    call_order = []

    async def _fake_connect(device):
        call_order.append(("connect", device is old))
        if device is old:
            return False
        device.driver = MagicMock(name="candidate_driver")
        return True

    reload_count = 0

    async def _fake_reload(device_id, tasks):
        nonlocal reload_count
        reload_count += 1
        call_order.append(("reload", reload_count))
        if reload_count == 1:
            raise RuntimeError("install boom")

    app.device_manager.connect_device = _fake_connect
    app.scheduler = MagicMock()
    app.scheduler.reload_device_tasks = AsyncMock(side_effect=_fake_reload)

    with pytest.raises(RuntimeError, match="install boom"):
        await CollectorApp._replace_device_atomic(app, "PLC001", new_cfg, variables)

    assert app.device_manager.get_device("PLC001") is old
    app.device_manager.schedule_reconnect.assert_awaited_once_with(old)
    # 顺序：候选 connect → 新任务 reload(失败) → 旧 connect → schedule → 旧任务 reload
    assert ("connect", False) in call_order
    assert ("reload", 1) in call_order
    assert ("connect", True) in call_order
    assert ("reload", 2) in call_order
    assert call_order.index(("connect", True)) < call_order.index(("reload", 2))
