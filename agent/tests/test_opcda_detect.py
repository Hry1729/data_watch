"""T007 — opcda_detect 单元测试（检测 / 粘性映射 / IO）。

对照 FR-P01/P02（检测+归一）、FR-P03/P04/P05（粘性、stale 不复用、持久化）。
检测用 monkeypatch 替身 pymysql.connect，不连真库。
"""

import sqlite3

import pytest

from app.services import opcda_detect


# ---------------------------------------------------------------------------
# pymysql 测试替身
# ---------------------------------------------------------------------------

class _FakeCursor:
    def __init__(self, rows):
        self._rows = rows

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False

    def execute(self, sql, *args):
        self._sql = sql

    def fetchall(self):
        return list(self._rows)


class _FakeConn:
    def __init__(self, rows):
        self._rows = rows
        self.closed = False

    def cursor(self):
        return _FakeCursor(self._rows)

    def close(self):
        self.closed = True


def _patch_rows(monkeypatch, rows):
    monkeypatch.setattr(
        opcda_detect.pymysql, "connect", lambda **kwargs: _FakeConn(rows)
    )


_MYSQL = {
    "host": "127.0.0.1",
    "port": 3306,
    "user": "root",
    "password": "plain",  # 明文 → 不走 DPAPI 解密
    "database": "device_watch",
}


# ---------------------------------------------------------------------------
# 检测（FR-P01 / FR-P02）
# ---------------------------------------------------------------------------

def test_detect_keeps_only_center_protocols(monkeypatch):
    _patch_rows(monkeypatch, [
        ("172.27.18.221", "opcda_center"),
        ("10.0.0.9", "s7"),            # 非 center → 排除
        ("172.27.18.222", "opcda_sidecar"),  # sidecar → 排除
    ])
    assert opcda_detect.detect_center_endpoints(_MYSQL) == ["172.27.18.221"]


def test_detect_dedupes_preserving_order(monkeypatch):
    _patch_rows(monkeypatch, [
        ("172.27.18.222", "opcda_center"),
        ("172.27.18.221", "opcda_center"),
        ("172.27.18.222", "opcda_center"),  # 重复
    ])
    assert opcda_detect.detect_center_endpoints(_MYSQL) == [
        "172.27.18.222", "172.27.18.221",
    ]


def test_detect_normalizes_protocol_aliases(monkeypatch):
    _patch_rows(monkeypatch, [("172.27.18.221", "opcda-center")])
    assert opcda_detect.detect_center_endpoints(_MYSQL) == ["172.27.18.221"]


def test_detect_empty_endpoint_becomes_localhost(monkeypatch):
    _patch_rows(monkeypatch, [("", "opcda_center")])
    assert opcda_detect.detect_center_endpoints(_MYSQL) == ["localhost"]


def test_detect_edge_endpoints_reads_local_snapshot(tmp_path):
    path = tmp_path / "edge.db"
    with sqlite3.connect(path) as connection:
        connection.execute(
            "CREATE TABLE edge_device(endpoint TEXT, protocol TEXT, enabled INTEGER)"
        )
        connection.executemany(
            "INSERT INTO edge_device VALUES(?, ?, ?)",
            [
                ("172.27.18.221", "opcda_center", 1),
                ("10.0.0.9", "s7", 1),
                ("172.27.18.222", "opcda-center", 0),
            ],
        )

    assert opcda_detect.detect_edge_endpoints(path) == ["172.27.18.221"]


def test_detect_edge_endpoints_without_snapshot_is_empty(tmp_path):
    assert opcda_detect.detect_edge_endpoints(tmp_path / "missing.db") == []


# ---------------------------------------------------------------------------
# 粘性映射（FR-P03 / FR-P04）
# ---------------------------------------------------------------------------

def test_sticky_keeps_existing_ports_no_reorder():
    existing = {"E1": 9090, "E2": 9091}
    result = opcda_detect.assign_sticky_ports(["E2", "E1"], existing)
    assert result["E1"] == 9090
    assert result["E2"] == 9091


def test_sticky_new_endpoint_takes_max_plus_one():
    result = opcda_detect.assign_sticky_ports(["E1", "E3"], {"E1": 9090})
    assert result["E3"] == 9091


def test_sticky_first_endpoint_uses_base_port():
    result = opcda_detect.assign_sticky_ports(["E1"], {})
    assert result["E1"] == 9090


def test_sticky_stale_kept_and_port_not_reused():
    # E1 被删（不在 endpoints 中），其条目须保留、端口 9090 不得复用给 E3
    existing = {"E1": 9090, "E2": 9091}
    result = opcda_detect.assign_sticky_ports(["E2", "E3"], existing)
    assert result["E1"] == 9090   # stale 保留
    assert result["E2"] == 9091
    assert result["E3"] == 9092   # 取 max+1，不复用 9090


# ---------------------------------------------------------------------------
# 持久化 IO（FR-P05）
# ---------------------------------------------------------------------------

def test_port_mapping_io_round_trip(tmp_path):
    path = tmp_path / "opcda_proxy" / "port_mapping.json"
    mapping = {"172.27.18.221": 9090, "172.27.18.222": 9091}
    opcda_detect.save_port_mapping(path, mapping)
    assert opcda_detect.load_port_mapping(path) == mapping


def test_load_port_mapping_missing_file_returns_empty(tmp_path):
    assert opcda_detect.load_port_mapping(tmp_path / "nope.json") == {}


def test_sticky_ignores_history_ports_below_base():
    # 历史端口 < 9090 时，新端点仍须从 9090 起（不漂到 1001）
    result = opcda_detect.assign_sticky_ports(["E1"], {"old": 1000})
    assert result["E1"] == 9090
    assert result["old"] == 1000  # stale 保留


def test_load_port_mapping_corrupt_non_dict_raises(tmp_path):
    bad = tmp_path / "bad.json"
    bad.write_text("[]", encoding="utf-8")
    with pytest.raises(ValueError):
        opcda_detect.load_port_mapping(bad)


def test_load_port_mapping_non_int_port_raises(tmp_path):
    bad = tmp_path / "bad.json"
    bad.write_text('{"E1": "9090"}', encoding="utf-8")
    with pytest.raises(ValueError):
        opcda_detect.load_port_mapping(bad)


def test_detect_handles_bytes_rows(monkeypatch):
    # pymysql 某些编码/字段配置下返回 bytes，不应漏检
    _patch_rows(monkeypatch, [(b"localhost", b"opcda-center")])
    assert opcda_detect.detect_center_endpoints(_MYSQL) == ["localhost"]


def test_detect_uses_consistent_timeouts(monkeypatch):
    captured = {}

    def fake_connect(**kwargs):
        captured.update(kwargs)
        return _FakeConn([])

    monkeypatch.setattr(opcda_detect.pymysql, "connect", fake_connect)
    opcda_detect.detect_center_endpoints(_MYSQL)
    assert captured["connect_timeout"] == captured["read_timeout"] == captured["write_timeout"]


def test_save_port_mapping_failure_keeps_previous_mapping_intact(tmp_path, monkeypatch):
    """B052：写到一半抛 OSError，旧映射仍完整可读，且不残留临时文件。"""
    path = tmp_path / "opcda_proxy" / "port_mapping.json"
    old = {"E1": 9090, "E2": 9091}
    opcda_detect.save_port_mapping(path, old)

    real_open = open

    class _Broken:
        def __init__(self, f):
            self._f = f

        def __enter__(self):
            return self

        def __exit__(self, *a):
            self._f.close()

        def write(self, text):
            self._f.write(text[: len(text) // 2])
            raise OSError("disk full")

    def broken_open(file, mode="r", *args, **kwargs):
        f = real_open(file, mode, *args, **kwargs)
        return _Broken(f) if "w" in mode else f

    monkeypatch.setattr("builtins.open", broken_open)
    with pytest.raises(OSError):
        opcda_detect.save_port_mapping(path, {"E1": 9090, "E2": 9091, "E3": 9092})
    monkeypatch.setattr("builtins.open", real_open)

    assert opcda_detect.load_port_mapping(path) == old
    assert sorted(p.name for p in path.parent.iterdir()) == [
        "port_mapping.json",
        "port_mapping.json.bak",
    ]


def test_load_port_mapping_falls_back_to_last_good_copy(tmp_path):
    """B052：主文件损坏（如旧版本非原子写中断）时回退 .bak；两者都坏仍 fail-fast。"""
    path = tmp_path / "port_mapping.json"
    good = {"E1": 9090}
    opcda_detect.save_port_mapping(path, good)
    path.write_text('{"E1": 90', encoding="utf-8")  # 截断的 JSON
    assert opcda_detect.load_port_mapping(path) == good

    (tmp_path / "port_mapping.json.bak").write_text("[]", encoding="utf-8")
    with pytest.raises(ValueError):
        opcda_detect.load_port_mapping(path)
