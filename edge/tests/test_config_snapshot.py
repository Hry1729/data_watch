from __future__ import annotations

import asyncio
import sqlite3

import pytest

from edge.app.config_snapshot import (
    ConfigSnapshotStore,
    EdgeConfigManager,
    snapshot_checksum,
)
from edge.app.outbox import OutboxStore


def _devices(scan_rate_ms: int = 100) -> list[dict]:
    return [
        {
            "device_code": "QC01",
            "device_pk": 7,
            "device_type": "QC",
            "protocol": "s7",
            "endpoint": "10.0.0.7:102",
            "ip": "10.0.0.7",
            "port": 102,
            "rack": 0,
            "slot": 1,
            "scan_rate_ms": scan_rate_ms,
            "config_version": 4,
            "assignment_epoch": 3,
            "variables": [
                {
                    "variable_id": "101",
                    "address": "DB1.DBD0",
                    "data_type": "REAL",
                    "scan_rate_ms": scan_rate_ms,
                    "is_storage": True,
                    "var_seq": 1,
                    "group_code": "g001",
                    "group_config_version": 8,
                }
            ],
        }
    ]


def test_snapshot_is_atomic_and_drives_uploader_interval(tmp_path):
    path = tmp_path / "edge.db"
    outbox = OutboxStore(path)
    store = ConfigSnapshotStore(path, clock_ms=lambda: 1234)
    devices = _devices(1_000)
    devices[0]["variables"][0]["scan_rate_ms"] = 100

    info = store.apply_snapshot(
        config_revision=9,
        checksum=snapshot_checksum(devices),
        devices=devices,
        read_timeout_ms=900,
    )

    assert info.config_revision == 9
    assert store.snapshot_info() == info
    assert outbox.upload_interval_ms() == 100
    assert store.list_devices()[0]["device_code"] == "QC01"

    store.close()
    outbox.close()


def test_bad_checksum_preserves_previous_snapshot(tmp_path):
    path = tmp_path / "edge.db"
    outbox = OutboxStore(path)
    store = ConfigSnapshotStore(path)
    devices = _devices()
    store.apply_snapshot(
        config_revision=1,
        checksum=snapshot_checksum(devices),
        devices=devices,
    )

    with pytest.raises(ValueError, match="checksum"):
        store.apply_snapshot(
            config_revision=2,
            checksum="bad",
            devices=_devices(200),
        )

    assert store.snapshot_info().config_revision == 1
    assert outbox.upload_interval_ms() == 100
    store.close()
    outbox.close()


def test_collector_manager_reads_snapshot_and_updates_local_status(tmp_path):
    path = tmp_path / "edge.db"
    outbox = OutboxStore(path)
    store = ConfigSnapshotStore(path, clock_ms=lambda: 4567)
    devices = _devices()
    store.apply_snapshot(
        config_revision=2,
        checksum=snapshot_checksum(devices),
        devices=devices,
        read_timeout_ms=750,
    )
    manager = EdgeConfigManager(path, clock_ms=lambda: 9999)

    config = asyncio.run(manager.load_config())
    signatures = asyncio.run(manager.get_device_config_versions())
    asyncio.run(manager.update_device_status("QC01", 1, "connected"))

    assert config.devices[0].device_id == "QC01"
    assert config.devices[0].scan_rate == 0.1
    assert config.devices[0].read_timeout_ms == 750
    assert config.variables[0].group_config_version == 8
    assert signatures["QC01"].scan_cycle == 100
    assert signatures["QC01"].read_timeout_ms == 750
    assert store.list_devices()[0]["status"] == 1
    store.close()
    outbox.close()


@pytest.mark.parametrize("protocol", ["GE SRTP", "ge-srtp", "gesrtp"])
def test_snapshot_normalizes_ge_srtp_protocol_aliases(tmp_path, protocol):
    path = tmp_path / "edge.db"
    outbox = OutboxStore(path)
    store = ConfigSnapshotStore(path)
    devices = _devices()
    devices[0]["protocol"] = protocol
    store.apply_snapshot(
        config_revision=1,
        checksum=snapshot_checksum(devices),
        devices=devices,
    )
    manager = EdgeConfigManager(path)

    assert store.list_devices()[0]["protocol"] == "ge_srtp"
    assert asyncio.run(manager.load_config()).devices[0].protocol == "ge_srtp"

    with sqlite3.connect(path) as connection:
        connection.execute(
            "UPDATE edge_device SET protocol='ge srtp' WHERE device_code='QC01'"
        )
    assert asyncio.run(manager.load_config()).devices[0].protocol == "ge_srtp"
    outbox.close()
    store.close()


def test_assignment_schedule_drives_collector_and_uploader(tmp_path):
    path = tmp_path / "edge.db"
    now = {"value": 1_000}
    clock = lambda: now["value"]
    outbox = OutboxStore(path)
    store = ConfigSnapshotStore(path, clock_ms=clock)
    manager = EdgeConfigManager(path, clock_ms=clock)
    devices = _devices()
    devices[0]["collect_from_ms"] = 2_000
    devices[0]["collect_until_ms"] = 4_000

    store.apply_snapshot(
        config_revision=3,
        checksum=snapshot_checksum(devices),
        devices=devices,
    )
    assert asyncio.run(manager.get_device_config_versions()) == {}
    assert outbox.upload_interval_ms() is None

    now["value"] = 2_000
    assert "QC01" in asyncio.run(manager.get_device_config_versions())
    assert outbox.upload_interval_ms() == 100

    now["value"] = 4_000
    assert asyncio.run(manager.get_device_config_versions()) == {}
    assert outbox.upload_interval_ms() is None

    store.close()
    outbox.close()