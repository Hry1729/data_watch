from __future__ import annotations

import sqlite3

import pytest

from app.services.edge_config_store import EdgeConfigStore, snapshot_checksum


def _snapshot(revision: int, rate: int) -> dict:
    devices = [
        {
            "device_code": "D1",
            "device_pk": 1,
            "device_type": "QC",
            "protocol": "s7",
            "endpoint": "10.0.0.1:102",
            "ip": "10.0.0.1",
            "port": 102,
            "rack": 0,
            "slot": 1,
            "scan_rate_ms": rate,
            "config_version": revision,
            "assignment_epoch": 2,
            "enabled": True,
            "variables": [
                {
                    "variable_id": "1",
                    "address": "DB1.DBW0",
                    "data_type": "INT",
                    "scan_rate_ms": rate,
                    "is_storage": True,
                    "var_seq": 1,
                    "group_code": "g001",
                    "group_config_version": revision,
                }
            ],
        }
    ]
    return {
        "configRevision": revision,
        "checksum": snapshot_checksum(devices),
        "readTimeoutMs": None,
        "devices": devices,
    }


def test_snapshot_applies_topology_and_assignment_atomically(tmp_path) -> None:
    path = tmp_path / "edge.db"
    store = EdgeConfigStore(path)

    assert store.apply(_snapshot(7, 400)) == 7
    assert store.revision() == 7

    with sqlite3.connect(path) as connection:
        device = connection.execute(
            "SELECT device_code, scan_rate_ms FROM edge_device"
        ).fetchone()
        assignment = connection.execute(
            "SELECT assignment_epoch, scan_rate_ms FROM device_assignment"
        ).fetchone()
    assert device == ("D1", 400)
    assert assignment == (2, 400)


def test_snapshot_checksum_mismatch_preserves_previous_revision(tmp_path) -> None:
    store = EdgeConfigStore(tmp_path / "edge.db")
    store.apply(_snapshot(7, 100))
    invalid = _snapshot(8, 400)
    invalid["checksum"] = "bad"

    with pytest.raises(ValueError, match="checksum"):
        store.apply(invalid)

    assert store.revision() == 7


def test_assignment_schedule_refreshes_enabled_state(tmp_path) -> None:
    path = tmp_path / "edge.db"
    store = EdgeConfigStore(path)
    snapshot = _snapshot(9, 100)
    snapshot["devices"][0]["collect_from_ms"] = 2_000
    snapshot["devices"][0]["collect_until_ms"] = 4_000
    snapshot["checksum"] = snapshot_checksum(snapshot["devices"])

    store.apply(snapshot)
    store.refresh_schedule(3_000)
    with sqlite3.connect(path) as connection:
        active = connection.execute(
            "SELECT d.enabled, a.enabled FROM edge_device d "
            "JOIN device_assignment a ON a.device_code=d.device_code"
        ).fetchone()
    assert active == (1, 1)

    store.refresh_schedule(4_000)
    with sqlite3.connect(path) as connection:
        stopped = connection.execute(
            "SELECT d.enabled, a.enabled FROM edge_device d "
            "JOIN device_assignment a ON a.device_code=d.device_code"
        ).fetchone()
    assert stopped == (0, 0)