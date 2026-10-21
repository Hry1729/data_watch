"""Edge 17 / Agent M8：Agent 侧 SQLite —— URI 用 Path.as_uri()（路径含 # % ? 可用），
连接显式关闭，读 edge.db 使用只读连接。"""
from __future__ import annotations

import os
import sqlite3

import pytest

from app.services import edge_config_store, edge_local_store, opcda_detect
from app.services.edge_config_store import EdgeConfigStore
from app.services.edge_local_store import EdgeLocalStore


class _Tracker:
    def __init__(self):
        self.opened = []

    def install(self, monkeypatch, module):
        real_connect = sqlite3.connect
        tracker = self

        class TrackingConnection(sqlite3.Connection):
            def __init__(self, *args, **kwargs):
                super().__init__(*args, **kwargs)
                self.closed = False
                self.target = args[0] if args else kwargs.get("database")
                tracker.opened.append(self)

            def close(self):
                self.closed = True
                super().close()

        def connect(*args, **kwargs):
            kwargs.setdefault("factory", TrackingConnection)
            return real_connect(*args, **kwargs)

        monkeypatch.setattr(module.sqlite3, "connect", connect)
        return self


def _odd_dir(tmp_path):
    # Windows 文件名不允许 "?"，在 Windows 上仍覆盖 # 和 %
    name = "a#b%c d" if os.name == "nt" else "a#b%c?d"
    path = tmp_path / name / "edge"
    path.mkdir(parents=True)
    return path


def test_local_store_reads_path_with_special_chars_and_closes(tmp_path, monkeypatch):
    path = _odd_dir(tmp_path) / "edge.db"
    with sqlite3.connect(path) as setup:
        setup.execute("CREATE TABLE edge_config_meta(singleton INTEGER PRIMARY KEY)")
    setup.close()

    tracker = _Tracker().install(monkeypatch, edge_local_store)
    assert EdgeLocalStore(path).outbox_status()["backlog_count"] == 0
    assert tracker.opened, "expected at least one connection"
    assert all(conn.closed for conn in tracker.opened)
    assert all("mode=ro" in str(conn.target) for conn in tracker.opened)


def test_local_store_connection_is_read_only(tmp_path):
    path = _odd_dir(tmp_path) / "edge.db"
    with sqlite3.connect(path) as setup:
        setup.execute("CREATE TABLE t(x INTEGER)")
    setup.close()
    connection = EdgeLocalStore(path)._connect()
    try:
        # 路径含 # % ? 也必须能打开（旧 f"file:{as_posix()}" 会把 # 之后当作 fragment）
        assert connection.execute("SELECT count(*) FROM t").fetchone()[0] == 0
        with pytest.raises(sqlite3.OperationalError, match="readonly"):
            connection.execute("INSERT INTO t VALUES(1)")
    finally:
        connection.close()


def test_opcda_detect_uses_read_only_closed_connection(tmp_path, monkeypatch):
    path = _odd_dir(tmp_path) / "edge.db"
    with sqlite3.connect(path) as setup:
        setup.execute("CREATE TABLE edge_device(endpoint TEXT, protocol TEXT, enabled INTEGER)")
        setup.execute("INSERT INTO edge_device VALUES('172.27.18.221', 'opcda_center', 1)")
    setup.close()

    tracker = _Tracker().install(monkeypatch, opcda_detect)
    assert opcda_detect.detect_edge_endpoints(path) == ["172.27.18.221"]
    assert len(tracker.opened) == 1
    conn = tracker.opened[0]
    assert conn.closed
    assert str(conn.target).startswith("file:") and "mode=ro" in str(conn.target)


def test_config_store_closes_connections(tmp_path, monkeypatch):
    tracker = _Tracker().install(monkeypatch, edge_config_store)
    store = EdgeConfigStore(tmp_path / "edge.db")
    assert store.revision() == 0
    store.refresh_schedule(0)
    assert len(tracker.opened) == 3
    assert all(conn.closed for conn in tracker.opened)
