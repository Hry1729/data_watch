import sqlite3
from pathlib import Path

from edge.app.config_snapshot import ConfigSnapshotStore, snapshot_checksum
from edge.app.outbox import OutboxStore


def test_local_latest_frame_merges_across_scan_periods(tmp_path: Path):
    db_path = tmp_path / "edge.db"
    store = OutboxStore(db_path)
    store.configure_assignment("DEV1", 1, 100, 1)

    # 1. 模拟快周期采集更新 fast1, fast2
    store.write_local_latest(
        "DEV1",
        "GRP1",
        {
            "fast1": {"v": "10", "t": 1000, "q": 192},
            "fast2": {"v": "20", "t": 1000, "q": 192},
        },
    )

    latest = store.read_local_latest("DEV1", "GRP1")
    assert latest["fast1"]["v"] == "10"
    assert latest["fast2"]["v"] == "20"

    # 2. 模拟慢周期采集只更新 slow1
    store.write_local_latest(
        "DEV1",
        "GRP1",
        {
            "slow1": {"v": "99", "t": 1050, "q": 192},
        },
    )

    # 验证快周期点位未被冲掉，慢周期点位也已加入，各自时间戳独立
    latest = store.read_local_latest("DEV1", "GRP1")
    assert latest["fast1"]["v"] == "10"
    assert latest["fast1"]["t"] == 1000
    assert latest["fast2"]["v"] == "20"
    assert latest["fast2"]["t"] == 1000
    assert latest["slow1"]["v"] == "99"
    assert latest["slow1"]["t"] == 1050

    # 3. 再次写入快周期，慢周期依然保留
    store.write_local_latest(
        "DEV1",
        "GRP1",
        {
            "fast1": {"v": "11", "t": 2000, "q": 192},
        },
    )
    latest = store.read_local_latest("DEV1", "GRP1")
    assert latest["fast1"]["v"] == "11"
    assert latest["fast1"]["t"] == 2000
    assert latest["fast2"]["v"] == "20"
    assert latest["slow1"]["v"] == "99"
    assert latest["slow1"]["t"] == 1050


def test_local_latest_frame_purges_deleted_variables(tmp_path: Path):
    db_path = tmp_path / "edge.db"
    store = OutboxStore(db_path)
    snapshot_store = ConfigSnapshotStore(db_path)

    # 初始配置包含 var1 和 var2
    devices_v1 = [
        {
            "device_code": "DEV1",
            "device_type": "s7",
            "protocol": "s7",
            "endpoint": "s7://127.0.0.1",
            "scan_rate_ms": 100,
            "assignment_epoch": 1,
            "variables": [
                {
                    "variable_id": "var1",
                    "address": "DB1.DBD0",
                    "data_type": "REAL",
                    "scan_rate_ms": 100,
                    "is_storage": True,
                    "var_seq": 1,
                    "group_code": "GRP1",
                },
                {
                    "variable_id": "var2",
                    "address": "DB1.DBD4",
                    "data_type": "REAL",
                    "scan_rate_ms": 100,
                    "is_storage": True,
                    "var_seq": 2,
                    "group_code": "GRP1",
                },
            ],
        }
    ]
    snapshot_store.apply_snapshot(
        config_revision=1,
        checksum=snapshot_checksum(devices_v1),
        devices=devices_v1,
    )

    # 写入两个点位
    store.write_local_latest(
        "DEV1",
        "GRP1",
        {
            "var1": {"v": "1", "t": 1000, "q": 192},
            "var2": {"v": "2", "t": 1000, "q": 192},
        },
    )
    latest = store.read_local_latest("DEV1", "GRP1")
    assert "var1" in latest and "var2" in latest

    # 配置更新，删除了 var2，只保留 var1
    devices_v2 = [
        {
            "device_code": "DEV1",
            "device_type": "s7",
            "protocol": "s7",
            "endpoint": "s7://127.0.0.1",
            "scan_rate_ms": 100,
            "assignment_epoch": 2,
            "variables": [
                {
                    "variable_id": "var1",
                    "address": "DB1.DBD0",
                    "data_type": "REAL",
                    "scan_rate_ms": 100,
                    "is_storage": True,
                    "var_seq": 1,
                    "group_code": "GRP1",
                }
            ],
        }
    ]
    snapshot_store.apply_snapshot(
        config_revision=2,
        checksum=snapshot_checksum(devices_v2),
        devices=devices_v2,
    )

    assert "var2" not in store.read_local_latest("DEV1", "GRP1")

    # 下一次采集只写 var1，var2 应该被清除（而不是永久残留）
    store.write_local_latest(
        "DEV1",
        "GRP1",
        {
            "var1": {"v": "1.5", "t": 2000, "q": 192},
        },
    )
    latest = store.read_local_latest("DEV1", "GRP1")
    assert latest["var1"]["v"] == "1.5"
    assert "var2" not in latest


def test_removed_last_point_cleared_without_another_sample(tmp_path):
    path=tmp_path/"edge.db"
    store=OutboxStore(path)
    config=ConfigSnapshotStore(path)
    store.write_local_latest("D","g",{"v":{"v":"1","t":1}})
    config.apply_snapshot(config_revision=1,checksum=snapshot_checksum([]),devices=[])
    assert store.read_local_latest("D","g")=={}
    store.close();config.close()
