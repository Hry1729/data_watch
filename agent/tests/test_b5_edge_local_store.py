"""审查批次5：EdgeLocalStore B047（矩阵分页）/B048（图表抽样）/B049（实时在线口径）回归。"""
from __future__ import annotations

import json
import sqlite3
import time
from datetime import datetime, timedelta

from app.services.edge_local_store import EdgeLocalStore


def _seed(path, *, enabled=1, status=1, status_at=None, latest_at=None, devices=("QC01",)):
    path.parent.mkdir(parents=True, exist_ok=True)
    now = int(time.time() * 1000)
    with sqlite3.connect(path) as c:
        c.executescript(
            """
            CREATE TABLE edge_device (
                device_code TEXT PRIMARY KEY, device_pk INTEGER, device_type TEXT,
                protocol TEXT, endpoint TEXT, ip TEXT, port INTEGER, rack INTEGER,
                slot INTEGER, scan_rate_ms INTEGER, read_timeout_ms INTEGER,
                config_version INTEGER, assignment_epoch INTEGER, enabled INTEGER
            );
            CREATE TABLE edge_variable (
                variable_id TEXT, device_code TEXT, address TEXT, data_type TEXT,
                scan_rate_ms INTEGER, var_seq INTEGER, is_storage INTEGER,
                group_code TEXT, config_version INTEGER
            );
            CREATE TABLE edge_device_status (
                device_code TEXT PRIMARY KEY, status INTEGER, message TEXT,
                event_type TEXT, updated_at INTEGER
            );
            CREATE TABLE local_latest_frame (
                device_code TEXT, group_code TEXT, values_json BLOB, updated_at INTEGER,
                PRIMARY KEY(device_code, group_code)
            );
            CREATE TABLE outbox (
                local_seq INTEGER, device_code TEXT, group_code TEXT, ts INTEGER,
                payload BLOB, state TEXT, created_at INTEGER
            );
            """
        )
        for pk, code in enumerate(devices, start=101):
            c.execute(
                "INSERT INTO edge_device VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
                (code, pk, "QC", "s7", "e", "1.1.1.1", 102, 0, 1, 100, None, 1, 1, enabled),
            )
            for seq, group in ((1, "g001"), (2, "g002"), (3, "g003")):
                c.execute(
                    "INSERT INTO edge_variable VALUES(?,?,?,?,?,?,?,?,?)",
                    (f"{pk}{seq}", code, f"DB1.DBW{seq}", "REAL", 100, seq, 1, group, 1),
                )
        c.execute(
            "INSERT INTO edge_device_status VALUES(?,?,?,?,?)",
            ("QC01", status, "", "", status_at if status_at is not None else now),
        )
        if latest_at is not None:
            c.execute(
                "INSERT INTO local_latest_frame VALUES(?,?,?,?)",
                ("QC01", "g001", json.dumps({"1011": {"v": 1, "t": latest_at}}), latest_at),
            )
    return now


def _bounds(center_ms):
    center = datetime.fromtimestamp(center_ms / 1000)
    return {
        "startTime": (center - timedelta(hours=2)).strftime("%Y-%m-%d %H:%M:%S"),
        "endTime": (center + timedelta(hours=2)).strftime("%Y-%m-%d %H:%M:%S"),
    }


# ── B047 ──────────────────────────────────────────────────────────────


def test_matrix_pagination_keeps_all_groups_with_same_timestamp(tmp_path):
    path = tmp_path / "edge.db"
    now = _seed(path)
    t1 = now - now % 1000
    with sqlite3.connect(path) as c:
        seq = 0
        for ts in (t1, t1 + 1000):
            for n, group in ((1, "g001"), (2, "g002"), (3, "g003")):
                seq += 1
                c.execute(
                    "INSERT INTO outbox VALUES(?,?,?,?,?,?,?)",
                    (seq, "QC01", group, ts, json.dumps({"values": {f"101{n}": n * 10 + ts % 7}}), "sent", ts),
                )
    store = EdgeLocalStore(path)
    query = {"deviceId": 101, "varSeqs": [1, 2, 3], "pageSize": 2, "alignGranularity": "1s", **_bounds(now)}
    rows = []
    cursor = None
    for _ in range(10):
        page = store.history_matrix({**query, "cursorTs": cursor})
        rows.extend(page["rows"])
        cursor = page["nextCursor"]
        if cursor is None:
            break
    # 两个 bucket，各一行，且每行三组都在，无重复 bucket
    assert [r["ts"] for r in rows] == sorted({r["ts"] for r in rows})
    assert len(rows) == 2
    for row in rows:
        assert {"v1", "v2", "v3"} <= set(row)


# ── B048 ──────────────────────────────────────────────────────────────


def test_chart_sampling_is_not_emptied_by_global_seq_phase(tmp_path):
    path = tmp_path / "edge.db"
    now = _seed(path, devices=("QC01", "QC02"))
    base = now - 3_600_000
    with sqlite3.connect(path) as c:
        seq = 0
        for i in range(5001):
            for code in ("QC02", "QC01"):  # QC01 恰好只占偶数 → 1-based 下全为奇/偶相位
                seq += 1
                c.execute(
                    "INSERT INTO outbox VALUES(?,?,?,?,?,?,?)",
                    (seq * 2 - (1 if code == "QC01" else 0), code, "g001", base + i,
                     json.dumps({"values": {("1011" if code == "QC01" else "1021"): i}}), "sent", base + i),
                )
    store = EdgeLocalStore(path)
    chart = store.history_chart({"deviceId": 101, "varSeqs": [1], **_bounds(now)})
    data = chart["series"][0]["data"]
    assert chart["interval"] == "local-sampled"
    assert 0 < len(data) <= 5001
    values = [v for _ts, v in data]
    assert values[0] == 0 and values[-1] == 5000  # 保留首尾


def test_chart_sampling_keeps_every_group_when_groups_interleave(tmp_path):
    path = tmp_path / "edge.db"
    now = _seed(path)
    base = now - 3_600_000
    with sqlite3.connect(path) as c:
        seq = 0
        for i in range(5001):
            for n, group in ((1, "g001"), (2, "g002")):
                seq += 1
                c.execute(
                    "INSERT INTO outbox VALUES(?,?,?,?,?,?,?)",
                    (seq, "QC01", group, base + i, json.dumps({"values": {f"101{n}": i}}), "sent", base + i),
                )
    chart = EdgeLocalStore(path).history_chart({"deviceId": 101, "varSeqs": [1, 2], **_bounds(now)})
    lengths = [len(s["data"]) for s in chart["series"]]
    assert all(length > 0 for length in lengths)
    assert sum(lengths) <= 5000 + 4


# ── B049 ──────────────────────────────────────────────────────────────


def test_stale_cache_is_offline_in_realtime_and_latest_values(tmp_path):
    path = tmp_path / "edge.db"
    now = int(time.time() * 1000)
    old = now - 300_000
    _seed(path, status=1, status_at=old, latest_at=old)
    store = EdgeLocalStore(path)

    latest = store.latest_values("QC01", "g001")
    assert latest["values"]  # 旧值仍可展示
    assert latest["deviceOffline"] is True
    assert store.realtime(101, "g001")["connectStatus"] == "offline"
    assert store.monitor_devices()["list"][0]["connectStatus"] == "offline"


def test_disabled_device_never_online(tmp_path):
    path = tmp_path / "edge.db"
    now = int(time.time() * 1000)
    _seed(path, enabled=0, status=1, status_at=now, latest_at=now)
    store = EdgeLocalStore(path)
    assert store.latest_values("QC01", "g001")["deviceOffline"] is True
    assert store.realtime(101, "g001")["connectStatus"] == "offline"


def test_fresh_data_is_online_everywhere(tmp_path):
    path = tmp_path / "edge.db"
    now = int(time.time() * 1000)
    _seed(path, status=1, status_at=now, latest_at=now)
    store = EdgeLocalStore(path)
    assert store.latest_values("QC01", "g001")["deviceOffline"] is False
    assert store.realtime(101, "g001")["connectStatus"] == "online"
    assert store.monitor_devices()["list"][0]["connectStatus"] == "online"
