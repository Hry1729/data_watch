"""配置重载循环变更分类测试。"""

import asyncio
import logging
from unittest.mock import AsyncMock, MagicMock

import pytest

import main as collector_main
from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.config_db import DeviceSignature
from main import CollectorApp


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
    fake_db.app = app
    return app


async def _run_reload_loop(monkeypatch, app):
    async def _no_sleep(_seconds):
        return None

    monkeypatch.setattr(collector_main.asyncio, "sleep", _no_sleep)
    await app._config_reload_loop()


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

    app._remove_device.assert_awaited_once_with("PLC001")
    app._add_device.assert_awaited_once_with(new_device, variables)
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
    app._add_device.side_effect = [RuntimeError("boom"), None]

    await _run_reload_loop(monkeypatch, app)

    assert app._remove_device.await_count == 2
    assert app._add_device.await_count == 2


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
    app._remove_device.assert_awaited_once_with("PLC001")
    app._add_device.assert_awaited_once_with(new_device, variables)
