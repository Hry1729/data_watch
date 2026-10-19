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


def _named_devices() -> list[dict]:
    devices = _devices()
    devices[0]["device_code"] = "AGV801"
    devices[0]["variables"] = [
        {
            "variable_id": "52000",
            "address": "OPD_Server.AGV801.Fault.Tag30007101",
            "point_name": "FLT1001",
            "data_type": "Boolean",
            "scan_rate_ms": 100,
            "is_storage": True,
            "var_seq": 306,
            "group_code": "g001",
            "group_config_version": 2,
        }
    ]
    return devices


def test_snapshot_persists_point_name_without_changing_ids(tmp_path):
    """快照下发点位名称并落库；点位 ID / 序号 / IO 地址都不变。"""
    path = tmp_path / "edge.db"
    outbox = OutboxStore(path)
    store = ConfigSnapshotStore(path)
    devices = _named_devices()

    store.apply_snapshot(
        config_revision=9,
        checksum=snapshot_checksum(devices),
        devices=devices,
    )

    with sqlite3.connect(path) as connection:
        connection.row_factory = sqlite3.Row
        row = connection.execute(
            "SELECT variable_id, point_name, address, data_type, var_seq "
            "FROM edge_variable"
        ).fetchone()
    assert row["point_name"] == "FLT1001"
    assert row["variable_id"] == "52000"
    assert row["var_seq"] == 306
    assert row["address"] == "OPD_Server.AGV801.Fault.Tag30007101"
    # 显示用的 data_type 原样保存，不在边缘侧改写底层类型
    assert row["data_type"] == "BOOLEAN"

    store.close()
    outbox.close()


def test_snapshot_without_point_name_stays_loadable(tmp_path):
    """旧中心不下发名称时，边缘仍能加载配置和采集。"""
    path = tmp_path / "edge.db"
    outbox = OutboxStore(path)
    store = ConfigSnapshotStore(path)
    devices = _devices()
    devices[0]["variables"][0].pop("point_name", None)

    store.apply_snapshot(
        config_revision=9,
        checksum=snapshot_checksum(devices),
        devices=devices,
    )

    manager = EdgeConfigManager(path)
    config = asyncio.run(manager.load_config())
    assert config.variables[0].variable_id == "101"
    assert config.variables[0].var_seq == 1

    with sqlite3.connect(path) as connection:
        point_name = connection.execute(
            "SELECT point_name FROM edge_variable"
        ).fetchone()[0]
    assert point_name == ""

    store.close()
    outbox.close()


def test_existing_edge_db_is_migrated_in_place(tmp_path):
    """现场已有的 edge.db（没有点位名称列）必须原地补列，已有数据保留。"""
    path = tmp_path / "edge.db"
    outbox = OutboxStore(path)
    outbox.close()
    with sqlite3.connect(path) as connection:
        # 模拟升级前的现场库：edge_variable 早于点位名称下发，没有该列
        connection.execute("ALTER TABLE edge_variable DROP COLUMN point_name")
        connection.execute(
            "INSERT INTO edge_device VALUES("
            "'QC01', 7, 'QC', 's7', '10.0.0.7:102', '10.0.0.7', 102, 0, 1, "
            "100, NULL, 4, 3, 1)"
        )
        connection.execute(
            "INSERT INTO edge_variable VALUES("
            "'101', 'QC01', 'DB1.DBD0', 'REAL', 100, 1, 1, 'g001', 8)"
        )
        connection.commit()

    # 第一次打开：补列并保留已有数据
    store = ConfigSnapshotStore(path)
    devices = _devices()
    devices[0]["variables"][0]["point_name"] = "CraneID"
    store.apply_snapshot(
        config_revision=9,
        checksum=snapshot_checksum(devices),
        devices=devices,
    )
    with sqlite3.connect(path) as connection:
        row = connection.execute(
            "SELECT variable_id, point_name FROM edge_variable"
        ).fetchone()
    assert row == ("101", "CraneID")
    store.close()

    # 重复启动：不再补列，也不报错
    again = ConfigSnapshotStore(path)
    with sqlite3.connect(path) as connection:
        row = connection.execute(
            "SELECT variable_id, point_name FROM edge_variable"
        ).fetchone()
    assert row == ("101", "CraneID")
    again.close()