from __future__ import annotations

import json
from pathlib import Path
import sqlite3
import sys


ROOT = Path(__file__).resolve().parents[1]
SCRIPTS = ROOT / "scripts"
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from simulate_edge_collector import (
    load_groups,
    record_state_counts,
    write_group,
)
from edge.app.config_snapshot import ConfigSnapshotStore, snapshot_checksum
from edge.app.outbox import OutboxStore


def _snapshot(device_code: str) -> dict:
    return {
        "device_code": device_code,
        "device_pk": 100 if device_code == "qc100" else 101,
        "device_type": "QC",
        "protocol": "s7",
        "endpoint": "127.0.0.1:102",
        "scan_rate_ms": 1000,
        "config_version": 1,
        "assignment_epoch": 1,
        "enabled": True,
        "variables": [
            {
                "variable_id": f"{device_code[-3:]}01",
                "address": "DB1.DBW0",
                "data_type": "INT",
                "scan_rate_ms": 100,
                "is_storage": True,
                "var_seq": 1,
                "group_code": "g001",
                "group_config_version": 3,
            },
            {
                "variable_id": f"{device_code[-3:]}02",
                "address": "DB1.DBX2.0",
                "data_type": "BOOL",
                "scan_rate_ms": 100,
                "is_storage": True,
                "var_seq": 2,
                "group_code": "g001",
                "group_config_version": 3,
            },
            {
                "variable_id": f"{device_code[-3:]}03",
                "address": "DB1.DBB3",
                "data_type": "STRING",
                "scan_rate_ms": 100,
                "is_storage": True,
                "var_seq": 3,
                "group_code": "g001",
                "group_config_version": 3,
            },
        ],
    }


def test_simulator_writes_real_outbox_and_local_latest(tmp_path: Path) -> None:
    path = tmp_path / "edge.db"
    devices = [_snapshot("qc100"), _snapshot("qc101")]
    OutboxStore(path).close()
    snapshot_store = ConfigSnapshotStore(path)
    snapshot_store.apply_snapshot(
        config_revision=4,
        checksum=snapshot_checksum(devices),
        devices=devices,
    )
    snapshot_store.close()

    groups = load_groups(path, ["qc100", "qc101"], point_limit=2)
    assert len(groups) == 2
    assert all(group.scan_rate_ms == 100 for group in groups)
    assert all(len(group.variables) == 2 for group in groups)

    outbox = OutboxStore(path)
    record = write_group(outbox, groups[0], cycle=1, timestamp_ms=1_700_000_000_000)
    assert record.local_seq == 1
    latest = outbox.read_local_latest(groups[0].device_code, "g001")
    assert set(latest) == {variable.variable_id for variable in groups[0].variables}
    assert all(value["q"] == 0 for value in latest.values())
    outbox.close()

    with sqlite3.connect(path) as connection:
        payload = json.loads(connection.execute("SELECT payload FROM outbox").fetchone()[0])
    assert payload["device_code"] == groups[0].device_code
    assert payload["scan_rate"] == 100
    assert payload["config_version"] == 3
    assert payload["contract_version"] == 2
    assert set(payload["values"]) == set(payload["qualities"])
    assert set(payload["qualities"].values()) == {0}
    assert record_state_counts(path, [record.batch_id]) == {"pending": 1}
