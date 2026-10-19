from __future__ import annotations

import json
import sqlite3
import types
from datetime import datetime, timedelta

import pytest
from fastapi import HTTPException

from app.routers import local as local_router
from app.routers.local import (
    EdgeSettingsUpdate,
    local_collector,
    local_history_chart,
    local_history_matrix,
    local_monitor_devices,
    local_monitor_points,
    local_realtime,
    local_devices,
    local_outbox,
    local_settings,
    update_local_settings,
)
from app.services.edge_local_store import EdgeLocalStore
from app.services.process_manager import ProcessInfo


def _request():
    class PM:
        modules = {"collector": object(), "uploader": object()}

        def status(self, name):
            return ProcessInfo(
                name=name,
                status="running",
                pid=12,
                started_at="now",
                uptime_seconds=5,
            )

    return types.SimpleNamespace(
        app=types.SimpleNamespace(state=types.SimpleNamespace(pm=PM()))
    )


def _seed(path):
    path.parent.mkdir(parents=True)
    with sqlite3.connect(path) as connection:
        connection.executescript(
            """
            CREATE TABLE edge_config_meta (
                singleton INTEGER PRIMARY KEY,
                config_revision INTEGER,
                checksum TEXT,
                read_timeout_ms INTEGER,
                applied_at INTEGER
            );
            CREATE TABLE edge_device (
                device_code TEXT PRIMARY KEY,
                device_type TEXT,
                protocol TEXT,
                endpoint TEXT,
                scan_rate_ms INTEGER,
                config_version INTEGER,
                assignment_epoch INTEGER,
                enabled INTEGER
            );
            CREATE TABLE edge_device_status (
                device_code TEXT PRIMARY KEY,
                status INTEGER,
                message TEXT,
                updated_at INTEGER
            );
            CREATE TABLE edge_variable (
                device_code TEXT,
                scan_rate_ms INTEGER
            );
            CREATE TABLE local_latest (
                device_code TEXT,
                group_code TEXT,
                var_id TEXT,
                value_json BLOB,
                updated_at INTEGER
            );
            CREATE TABLE outbox (state TEXT, created_at INTEGER);
            CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT);
            CREATE TABLE device_assignment (
                device_code TEXT,
                scan_rate_ms INTEGER,
                enabled INTEGER
            );
            INSERT INTO edge_config_meta VALUES(1, 3, 'sum', NULL, 1000);
            INSERT INTO edge_device VALUES('QC01', 'QC', 's7', '10.0.0.1:102', 1000, 2, 1, 1);
            INSERT INTO edge_variable VALUES('QC01', 100);
            INSERT INTO metadata VALUES('upload_session_id', 'session-1');
            INSERT INTO metadata VALUES('last_seq', '0');
            INSERT INTO metadata VALUES('sent_records_total', '9');
            INSERT INTO device_assignment VALUES('QC01', 1000, 1);
            """
        )


@pytest.mark.asyncio
async def test_edge_local_api_reads_only_local_state(monkeypatch, tmp_path):
    monkeypatch.setenv("DATAWATCH_ROLE", "edge")
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    _seed(tmp_path / "edge" / "edge.db")

    device_result = await local_devices(_user="u")
    outbox_result = await local_outbox(_user="u")
    collector_result = await local_collector(_request(), _user="u")

    assert device_result["config"]["config_revision"] == 3
    assert device_result["devices"][0]["device_code"] == "QC01"
    assert device_result["devices"][0]["scan_rate_ms"] == 1000
    assert device_result["fastest_point_scan_rate_ms"] == 100
    assert outbox_result["upload_interval_ms"] == 100
    assert outbox_result["sent_records_total"] == 9
    assert collector_result["status"] == "running"


def test_devices_falls_back_before_collector_creates_latest_table(tmp_path):
    path = tmp_path / "edge" / "edge.db"
    _seed(path)
    with sqlite3.connect(path) as connection:
        connection.execute("DROP TABLE local_latest")

    result = EdgeLocalStore(path).devices()

    assert result["devices"][0]["device_code"] == "QC01"
    assert result["devices"][0]["status"] == 0


@pytest.mark.parametrize("keep_legacy", [True, False])
def test_device_status_uses_fresh_frames_after_status_event_expires(tmp_path, monkeypatch, keep_legacy):
    path = tmp_path / "edge" / "edge.db"
    _seed(path)
    monkeypatch.setattr("app.services.edge_local_store.time.time", lambda: 1000)
    with sqlite3.connect(path) as connection:
        connection.execute("ALTER TABLE edge_device ADD COLUMN device_pk INTEGER DEFAULT 101")
        connection.execute("INSERT INTO edge_device_status VALUES('QC01', 1, '', 1000)")
        connection.execute("CREATE TABLE local_latest_frame (device_code TEXT, updated_at INTEGER)")
        connection.execute("INSERT INTO local_latest_frame VALUES('QC01', 1000000)")
        if keep_legacy:
            connection.execute("INSERT INTO local_latest VALUES('QC01', 'g001', '11', '{}', 1000)")
        else:
            connection.execute("DROP TABLE local_latest")
    store = EdgeLocalStore(path)
    assert store.devices()["devices"][0]["status"] == 1
    assert store.monitor_devices()["list"][0]["connectStatus"] == "online"
    monkeypatch.setattr("app.services.edge_local_store.time.time", lambda: 1130)
    assert store.devices()["devices"][0]["status"] == 0
    assert store.monitor_devices()["list"][0]["connectStatus"] == "offline"


@pytest.mark.asyncio
async def test_local_api_is_hidden_from_non_edge_roles(monkeypatch):
    monkeypatch.setenv("DATAWATCH_ROLE", "centralized")

    with pytest.raises(HTTPException) as exc:
        await local_devices(_user="u")

    assert exc.value.status_code == 404


def test_local_store_returns_empty_outbox_before_collector_schema(tmp_path):
    path = tmp_path / "edge.db"
    with sqlite3.connect(path) as connection:
        connection.execute("CREATE TABLE edge_config_meta(singleton INTEGER PRIMARY KEY)")

    assert EdgeLocalStore(path).outbox_status()["backlog_count"] == 0

@pytest.mark.asyncio
async def test_edge_settings_only_updates_local_retention_values(monkeypatch):
    monkeypatch.setenv("DATAWATCH_ROLE", "edge")
    config = {
        "edge": {
            "node_uuid": "node-1",
            "node_name": "edge-a",
            "center_url": "http://127.0.0.1:8000",
            "registered": True,
            "max_offline_retention_days": 7,
            "outbox_maintenance_seconds": 60,
        }
    }

    monkeypatch.setattr(local_router, "load_config", lambda: config)

    def mutate(mutator):
        mutator(config)
        return config

    monkeypatch.setattr(local_router, "mutate_config", mutate)

    current = await local_settings(_user="admin")
    updated = await update_local_settings(
        EdgeSettingsUpdate(
            max_offline_retention_days=14,
            outbox_maintenance_seconds=120,
        ),
        _user="admin",
    )

    assert current["center_url"] == "http://127.0.0.1:8000"
    assert updated["max_offline_retention_days"] == 14
    assert updated["outbox_maintenance_seconds"] == 120
    assert config["edge"]["node_uuid"] == "node-1"


def _seed_monitor(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    now = int(datetime.now().timestamp() * 1000)
    payload = json.dumps(
        {"device_code": "QC01", "group_code": "g001", "ts": now, "values": {"11": "12.5"}}
    ).encode()
    latest = json.dumps({"v": "12.5", "t": now, "q": 0}).encode()
    with sqlite3.connect(path) as connection:
        connection.executescript(
            """
            CREATE TABLE edge_device (
                device_code TEXT PRIMARY KEY,
                device_pk INTEGER,
                device_type TEXT,
                protocol TEXT,
                endpoint TEXT,
                ip TEXT,
                port INTEGER,
                rack INTEGER,
                slot INTEGER,
                scan_rate_ms INTEGER,
                opc_server TEXT,
                config_version INTEGER,
                assignment_epoch INTEGER,
                enabled INTEGER
            );
            CREATE TABLE edge_variable (
                variable_id TEXT PRIMARY KEY,
                device_code TEXT,
                address TEXT,
                data_type TEXT,
                scan_rate_ms INTEGER,
                is_storage INTEGER,
                var_seq INTEGER,
                group_code TEXT,
                group_config_version INTEGER
            );
            CREATE TABLE edge_device_status (
                device_code TEXT PRIMARY KEY,
                status INTEGER,
                message TEXT,
                event_type TEXT,
                updated_at INTEGER
            );
            CREATE TABLE local_latest (
                device_code TEXT,
                group_code TEXT,
                var_id TEXT,
                value_json BLOB,
                updated_at INTEGER,
                PRIMARY KEY(device_code, group_code, var_id)
            );
            CREATE TABLE outbox (
                local_seq INTEGER,
                device_code TEXT,
                ts INTEGER,
                payload BLOB,
                state TEXT,
                created_at INTEGER
            );
            """
        )
        connection.execute(
            "INSERT INTO edge_device VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
            ("QC01", 101, "QC", "ge_srtp", "10.0.0.1:18245", "10.0.0.1", 18245, 0, 1, 100, None, 2, 1, 1),
        )
        connection.execute(
            "INSERT INTO edge_variable VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?)",
            ("11", "QC01", "%R00001", "REAL", 100, 1, 1, "g001", 2),
        )
        connection.execute(
            "INSERT INTO edge_device_status VALUES(?, ?, ?, ?, ?)",
            ("QC01", 1, "connected", "connected", now),
        )
        connection.execute(
            "INSERT INTO local_latest VALUES(?, ?, ?, ?, ?)",
            ("QC01", "g001", "11", latest, now),
        )
        connection.execute(
            "INSERT INTO outbox VALUES(?, ?, ?, ?, ?, ?)",
            (1, "QC01", now, payload, "sent", now),
        )
    return now


@pytest.mark.asyncio
async def test_local_monitor_reads_catalog_latest_and_retained_history(monkeypatch, tmp_path):
    monkeypatch.setenv("DATAWATCH_ROLE", "edge")
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    now = _seed_monitor(tmp_path / "edge" / "edge.db")

    devices = await local_monitor_devices(
        page_no=1, page_size=100, keyword="", device_ids=None, device_type="", device_type_exact=None,
        device_code_exact=None, connect_status="", device_status="", _user="u"
    )
    points = await local_monitor_points(
        page_no=1,
        page_size=100,
        device_id=101,
        device_ids=[101],
        device_code="",
        keyword="",
        data_type="",
        device_type="",
        connect_status="",
        _user="u",
    )
    realtime = await local_realtime(
        device_id=101, group_code="g001", _user="u"
    )
    query = local_router.LocalHistoryQuery(
        deviceId=101,
        varSeqs=[1],
        startTime=(datetime.fromtimestamp(now / 1000) - timedelta(minutes=1)).isoformat(),
        endTime=(datetime.fromtimestamp(now / 1000) + timedelta(minutes=1)).isoformat(),
    )
    chart = await local_history_chart(query, _user="u")
    matrix = await local_history_matrix(
        local_router.LocalHistoryMatrixQuery(**query.model_dump()),
        _user="u",
    )

    assert devices["list"][0]["deviceCode"] == "QC01"
    assert devices["list"][0]["connectStatus"] == "online"
    assert points["list"][0]["ioAddress"] == "%R00001"
    assert realtime["data"][0]["value"] == "12.5"
    assert chart["series"][0]["data"][0][1] == 12.5
    assert matrix["rows"][0]["v1"] == "12.5"


def test_local_monitor_prefers_compact_latest_frame(tmp_path):
    path = tmp_path / "edge" / "edge.db"
    _seed_monitor(path)
    compact = json.dumps({"11": {"v": "27.5", "t": 2000, "q": 192}}).encode()
    with sqlite3.connect(path) as connection:
        connection.execute(
            """
            CREATE TABLE local_latest_frame (
                device_code TEXT,
                group_code TEXT,
                values_json BLOB,
                updated_at INTEGER,
                PRIMARY KEY(device_code, group_code)
            )
            """
        )
        connection.execute(
            "INSERT INTO local_latest_frame VALUES(?, ?, ?, ?)",
            ("QC01", "g001", compact, 2000),
        )

    result = EdgeLocalStore(path).latest_values("QC01", "g001")

    assert result["values"]["11"]["v"] == "27.5"
