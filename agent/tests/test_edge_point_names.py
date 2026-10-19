from __future__ import annotations

import sqlite3

import pytest

from app.services.edge_config_store import EdgeConfigStore, snapshot_checksum
from app.services.edge_local_store import (
    EdgeLocalStore,
    display_data_type,
    display_point_name,
)


def _snapshot(revision: int = 9, *, point_name: str | None = "FLT1001") -> dict:
    variable: dict = {
        "variable_id": "52000",
        "address": "OPD_Server.AGV801.Fault.Tag30007101",
        "data_type": "Boolean",
        "scan_rate_ms": 400,
        "is_storage": True,
        "var_seq": 306,
        "group_code": "g001",
        "group_config_version": 2,
    }
    if point_name is not None:
        variable["point_name"] = point_name
    devices = [
        {
            "device_code": "AGV801",
            "device_pk": 7,
            "device_type": "AGV",
            "protocol": "opcda_center",
            "endpoint": "10.0.0.7:102",
            "ip": "10.0.0.7",
            "port": 102,
            "rack": 0,
            "slot": 1,
            "scan_rate_ms": 400,
            "config_version": revision,
            "assignment_epoch": 2,
            "enabled": True,
            "variables": [variable],
        }
    ]
    return {
        "configRevision": revision,
        "checksum": snapshot_checksum(devices),
        "readTimeoutMs": None,
        "devices": devices,
    }


def _seed_collector_latest_tables(path) -> None:
    """实时接口要读 collector 建的 local_latest / local_latest_frame。"""
    with sqlite3.connect(path) as connection:
        connection.executescript(
            """
            CREATE TABLE IF NOT EXISTS local_latest (
                device_code TEXT NOT NULL,
                group_code TEXT NOT NULL,
                var_id TEXT NOT NULL,
                value_json BLOB,
                updated_at INTEGER
            );
            CREATE TABLE IF NOT EXISTS local_latest_frame (
                device_code TEXT NOT NULL,
                group_code TEXT NOT NULL,
                values_json BLOB,
                updated_at INTEGER
            );
            """
        )


def test_snapshot_persists_point_name_without_changing_ids(tmp_path) -> None:
    path = tmp_path / "edge.db"
    store = EdgeConfigStore(path)

    assert store.apply(_snapshot()) == 9

    with sqlite3.connect(path) as connection:
        row = connection.execute(
            "SELECT variable_id, point_name, address, data_type, var_seq "
            "FROM edge_variable"
        ).fetchone()
    assert row[1] == "FLT1001"
    # 点位 ID、序号、IO 地址都不因名称下发而改变
    assert row[0] == "52000"
    assert row[4] == 306
    assert row[2] == "OPD_Server.AGV801.Fault.Tag30007101"
    # 底层类型原样保存，边缘侧不改写解码规则
    assert row[3] == "BOOLEAN"


def test_existing_edge_db_is_migrated_in_place(tmp_path) -> None:
    """现场已有的 edge.db（没有点位名称列）必须原地补列，已有数据保留。"""
    path = tmp_path / "edge.db"
    store = EdgeConfigStore(path)
    store.apply(_snapshot())
    with sqlite3.connect(path) as connection:
        # 模拟升级前的现场库
        connection.execute("ALTER TABLE edge_variable DROP COLUMN point_name")
        connection.execute(
            "INSERT INTO edge_variable VALUES("
            "'101', 'QC01', 'DB1.DBD0', 'REAL', 100, 1, 1, 'g001', 8)"
        )
        connection.commit()

    # 再次初始化：补列且不丢数据
    again = EdgeConfigStore(path)
    with sqlite3.connect(path) as connection:
        columns = {row[1] for row in connection.execute("PRAGMA table_info(edge_variable)")}
        rows = connection.execute(
            "SELECT variable_id, point_name FROM edge_variable ORDER BY variable_id"
        ).fetchall()
    assert "point_name" in columns
    # 旧库本来就没有名称列，补列后已有行名称为空，行本身不丢
    assert rows == [("101", ""), ("52000", "")]

    # 重复初始化不报错
    EdgeConfigStore(path)
    assert again.revision() == 9


def test_local_api_returns_the_real_point_name(tmp_path) -> None:
    path = tmp_path / "edge.db"
    EdgeConfigStore(path).apply(_snapshot())

    result = EdgeLocalStore(path).monitor_points(page_no=1, page_size=100)

    assert result["total"] == 1
    point = result["list"][0]
    assert point["pointName"] == "FLT1001"
    assert point["varSeq"] == 306
    assert point["id"] == 52000
    assert point["ioAddress"] == "OPD_Server.AGV801.Fault.Tag30007101"


def test_local_api_falls_back_to_point_seq_when_name_is_missing(tmp_path) -> None:
    """旧快照没有名称时回退为「点位 {var_seq}」。"""
    path = tmp_path / "edge.db"
    EdgeConfigStore(path).apply(_snapshot(point_name=None))

    result = EdgeLocalStore(path).monitor_points(page_no=1, page_size=100)

    assert result["list"][0]["pointName"] == "点位 306"
    assert result["list"][0]["varSeq"] == 306


def test_local_api_falls_back_when_name_is_blank(tmp_path) -> None:
    path = tmp_path / "edge.db"
    EdgeConfigStore(path).apply(_snapshot(point_name="   "))

    result = EdgeLocalStore(path).monitor_points(page_no=1, page_size=100)

    assert result["list"][0]["pointName"] == "点位 306"


def test_local_api_returns_the_real_name_in_history_selectors(tmp_path) -> None:
    path = tmp_path / "edge.db"
    EdgeConfigStore(path).apply(_snapshot())
    store = EdgeLocalStore(path)

    points = store.history_points(device_id=7)
    assert points[0]["pointName"] == "FLT1001"
    assert points[0]["varSeq"] == 306


def test_local_api_realtime_uses_the_real_name(tmp_path) -> None:
    path = tmp_path / "edge.db"
    EdgeConfigStore(path).apply(_snapshot())
    _seed_collector_latest_tables(path)
    store = EdgeLocalStore(path)

    realtime = store.realtime(device_id=7, group_code="g001")

    assert realtime["data"][0]["pointName"] == "FLT1001"
    assert realtime["data"][0]["varSeq"] == 306
    assert realtime["data"][0]["dataType"] == "BOOL"


@pytest.mark.parametrize(
    ("raw", "expected"),
    [
        ("BOOL", "BOOL"),
        ("Boolean", "BOOL"),
        ("BOOLEAN", "BOOL"),
        ("BYTE", "BYTE"),
        ("STRING", "STRING"),
        ("INT", "INT"),
        ("REAL", "REAL"),
        ("UDINT", "UDINT"),
    ],
)
def test_display_data_type_uses_the_center_caliber(raw: str, expected: str) -> None:
    assert display_data_type(raw) == expected


def test_display_point_name_falls_back_only_when_blank() -> None:
    assert display_point_name("FLT1001", 306) == "FLT1001"
    assert display_point_name("", 306) == "点位 306"
    assert display_point_name(None, 306) == "点位 306"
    assert display_point_name("  ", 306) == "点位 306"


@pytest.mark.parametrize("data_type", ["BOOL", "Boolean", " boolean "])
def test_local_api_type_filter_matches_the_display_caliber(tmp_path, data_type) -> None:
    """按 BOOL 筛选能命中库里的 Boolean；按 BYTE 筛选不会命中 STRING。"""
    path = tmp_path / "edge.db"
    store = EdgeConfigStore(path)
    snapshot = _snapshot()
    snapshot["devices"][0]["variables"].append(
        {
            "variable_id": "52001",
            "address": "%DB1702.DBB2",
            "point_name": "rosNumber",
            "data_type": "BYTE",
            "scan_rate_ms": 400,
            "is_storage": True,
            "var_seq": 2,
            "group_code": "g001",
            "group_config_version": 2,
        }
    )
    snapshot["checksum"] = snapshot_checksum(snapshot["devices"])
    store.apply(snapshot)

    local = EdgeLocalStore(path)
    boolean = local.monitor_points(page_no=1, page_size=100, data_type=data_type)
    byte = local.monitor_points(page_no=1, page_size=100, data_type="BYTE")
    text = local.monitor_points(page_no=1, page_size=100, data_type="STRING")

    assert [item["pointName"] for item in boolean["list"]] == ["FLT1001"]
    assert [item["pointName"] for item in byte["list"]] == ["rosNumber"]
    assert text["list"] == []
    # 显示口径与筛选口径一致
    assert boolean["list"][0]["dataType"] == "BOOL"
    assert byte["list"][0]["dataType"] == "BYTE"


def test_local_store_tolerates_a_legacy_db_without_the_name_column(tmp_path) -> None:
    """现场旧库还没补列时，点位列表不能整体查不出来。"""
    path = tmp_path / "edge.db"
    store = EdgeConfigStore(path)
    store.apply(_snapshot())
    with sqlite3.connect(path) as connection:
        connection.execute("ALTER TABLE edge_variable DROP COLUMN point_name")
        connection.commit()

    result = EdgeLocalStore(path).monitor_points(page_no=1, page_size=100)

    assert result["total"] == 1
    assert result["list"][0]["pointName"] == "点位 306"
