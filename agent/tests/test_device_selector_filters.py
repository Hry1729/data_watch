from datetime import datetime
import sqlite3

from app.services.edge_local_store import EdgeLocalStore


def _seed_monitor(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    now = int(datetime.now().timestamp() * 1000)
    with sqlite3.connect(path) as connection:
        connection.executescript("""
            CREATE TABLE edge_device (device_pk INTEGER, device_code TEXT, device_type TEXT, protocol TEXT, enabled INTEGER);
            CREATE TABLE edge_device_status (device_code TEXT, status INTEGER, updated_at INTEGER);
            CREATE TABLE local_latest (device_code TEXT, updated_at INTEGER);
            CREATE TABLE edge_variable (variable_id INTEGER, device_code TEXT, var_seq INTEGER, address TEXT, data_type TEXT);
            INSERT INTO edge_device VALUES (101, 'QC01', 'QC', 's7', 1);
            INSERT INTO edge_variable VALUES (11, 'QC01', 1, 'D0', 'REAL');
        """)
        connection.execute("INSERT INTO edge_device_status VALUES ('QC01', 1, ?)", (now,))
        connection.execute("INSERT INTO local_latest VALUES ('QC01', ?)", (now,))


def test_monitor_scope_reads_all_assignments_without_runtime_tables(tmp_path):
    path = tmp_path / "scope.db"
    with sqlite3.connect(path) as connection:
        connection.execute("CREATE TABLE edge_device (device_pk INTEGER, device_code TEXT, device_type TEXT)")
        connection.executemany("INSERT INTO edge_device VALUES (?, ?, ?)", [(index, f"D{index}", "QC") for index in range(1, 6002)])
    scope = EdgeLocalStore(path).monitor_device_scope()
    assert len(scope) == 6001
    assert any(device["id"] == 6001 for device in scope)
    assert set(scope[0]) == {"id", "deviceCode", "deviceType"}


def test_monitor_device_filters_apply_before_local_pagination(tmp_path):
    path = tmp_path / "edge" / "edge.db"
    _seed_monitor(path)
    store = EdgeLocalStore(path)
    devices = store.monitor_devices(device_ids=[101], device_type_exact="QC", connect_status="online", page_size=1)
    assert devices["total"] == 1
    assert devices["list"][0]["id"] == 101
    assert store.monitor_devices(device_ids=[999])["total"] == 0
    assert store.monitor_devices(device_ids=[])["total"] == 0
    assert store.monitor_devices(device_type_exact="Q")["total"] == 0
    assert store.monitor_devices(device_code_exact="QC01")["total"] == 1
    assert store.monitor_devices(device_code_exact="QC0")["total"] == 0
    assert store.monitor_devices(device_status="disabled")["total"] == 0


def test_monitor_point_type_and_status_filters_preserve_totals_and_empty_scopes(tmp_path):
    path = tmp_path / "edge" / "edge.db"
    _seed_monitor(path)
    store = EdgeLocalStore(path)
    points = store.monitor_points(device_type="QC", connect_status="online", page_no=2, page_size=1)
    assert points["total"] == 1
    assert points["list"] == []
    assert store.monitor_points(device_type="missing")["total"] == 0
    assert store.monitor_points(device_type="QC", device_ids=[999])["total"] == 0
