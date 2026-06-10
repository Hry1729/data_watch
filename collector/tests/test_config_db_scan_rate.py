"""ConfigDB scan_rate normalization tests."""

from contextlib import asynccontextmanager
from types import SimpleNamespace

import pytest

from core.config_db import ConfigDBManager, DeviceSignature


@pytest.mark.parametrize(
    ("raw_value", "expected"),
    [
        (100, 0.1),
        (500, 0.5),
        (1000, 1.0),
        (1500, 1.5),
        (1, 1.0),
    ],
)
def test_normalize_scan_rate_keeps_millisecond_precision(raw_value, expected):
    assert ConfigDBManager._normalize_scan_rate(raw_value) == pytest.approx(expected)


def test_normalize_scan_rate_uses_default_for_missing_or_invalid_values():
    assert ConfigDBManager._normalize_scan_rate(None, default_seconds=0.5) == pytest.approx(0.5)
    assert ConfigDBManager._normalize_scan_rate(0, default_seconds=0.5) == pytest.approx(0.5)


def test_device_signature_device_fields_ignore_config_version():
    old = DeviceSignature(1, "10.0.0.1", 1, 0, 1, 1000)
    new_version = DeviceSignature(2, "10.0.0.1", 1, 0, 1, 1000)
    new_endpoint = DeviceSignature(2, "10.0.0.2", 1, 0, 1, 1000)

    assert old.device_fields_changed(new_version) is False
    assert old.device_fields_changed(new_endpoint) is True


@pytest.mark.asyncio
async def test_get_device_config_versions_returns_device_signatures():
    rows = [
        SimpleNamespace(
            device_code="PLC001",
            variable_config_version=7,
            endpoint="10.0.0.1",
            protocol_id=3,
            rack=0,
            slot=2,
            scan_cycle=500,
        ),
        SimpleNamespace(
            device_code="PLC002",
            variable_config_version=None,
            endpoint=None,
            protocol_id=None,
            rack=None,
            slot=None,
            scan_cycle=None,
        ),
    ]

    class _Result:
        def all(self):
            return rows

    class _Session:
        async def execute(self, _stmt):
            return _Result()

    @asynccontextmanager
    async def _session():
        yield _Session()

    manager = ConfigDBManager.__new__(ConfigDBManager)
    manager.get_session = _session

    versions = await manager.get_device_config_versions()

    assert versions["PLC001"] == DeviceSignature(7, "10.0.0.1", 3, 0, 2, 500)
    assert versions["PLC002"] == DeviceSignature(1, "", 0, 0, 1, 1000)
