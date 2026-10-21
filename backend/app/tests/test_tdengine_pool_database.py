from __future__ import annotations

from app.db import tdengine


class _FakeResult:
    def __init__(self, rows):
        self._rows = rows

    def fetchall(self):
        return self._rows


class _FakeConn:
    def __init__(self, log):
        self.log = log

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False

    def exec_driver_sql(self, sql):
        self.log.append(sql)
        return _FakeResult([("t1",)])


class _FakeEngine:
    def __init__(self):
        self.log: list[str] = []

    def connect(self):
        return _FakeConn(self.log)


def test_pool_path_selects_requested_database_per_borrow(monkeypatch):
    """B005: 池路径必须为每次查询选定 database，而不是丢弃该参数。"""
    engine = _FakeEngine()
    monkeypatch.setattr(tdengine, "get_tdengine_engine", lambda _client=None: engine)
    client = tdengine.TDengineClient("localhost", 6041, "root", "", "device_data")

    assert client._fetch_all_sync("SHOW TABLES", "other_db") == [("t1",)]
    assert client._fetch_all_sync("SHOW STABLES") == [("t1",)]

    assert engine.log == [
        "USE `other_db`",
        "SHOW TABLES",
        "USE `device_data`",
        "SHOW STABLES",
    ]


def test_pool_path_rejects_invalid_database_identifier(monkeypatch):
    engine = _FakeEngine()
    monkeypatch.setattr(tdengine, "get_tdengine_engine", lambda _client=None: engine)
    client = tdengine.TDengineClient("localhost", 6041, "root", "", "device_data")
    try:
        client._fetch_all_sync("SHOW TABLES", "bad;db")
    except ValueError:
        pass
    else:
        raise AssertionError("invalid identifier must be rejected")
    assert engine.log == []
