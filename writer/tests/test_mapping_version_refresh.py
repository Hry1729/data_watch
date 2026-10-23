"""配置版本递增后自动刷新映射、非法列名不连累同帧其它点、持续缺映射写 system_log。"""
from __future__ import annotations

import json
from unittest.mock import AsyncMock, MagicMock

import pytest

import consumer as consumer_mod
from consumer import StreamConsumer
from mysql_mapper import GroupMapping, MysqlMapper
from performance import WriterPerformanceMetrics


class _Cursor:
    def __init__(self, conn):
        self.conn = conn

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False

    def execute(self, query, params):
        self.conn.queries += 1

    def fetchall(self):
        return self.conn.rows


class _Conn:
    def __init__(self, rows):
        self.rows = rows
        self.queries = 0

    def ping(self, reconnect=True):
        pass

    def cursor(self, cursor_type):
        return _Cursor(self)


def _row(var_id, col, stable="sdasc", type_code="asc"):
    return {
        "var_id": var_id, "td_column_name": col, "var_type": "DINT",
        "group_code": "g001", "device_code": "asc7181", "device_type_code": type_code,
        "stable_name": stable, "child_table": "asc7181",
    }


def test_load_strips_spaces_and_isolates_invalid_columns():
    conn = _Conn([_row(1, "asc1"), _row(2, " asc2", stable="sd asc", type_code=" asc"), _row(3, "as-c3")])
    mapping = MysqlMapper(conn).load("asc7181", "g001")

    assert mapping is not None
    assert mapping.stable == "sdasc"
    assert mapping.var_map == {"1": "asc1", "2": "asc2"}
    assert mapping.invalid_columns == {"3": "as-c3"}


def test_newer_config_version_marks_cache_stale_once():
    conn = _Conn([_row(1, "asc1")])
    mapper = MysqlMapper(conn)
    assert mapper.get_or_load("asc7181", "g001", 5) is not None

    assert mapper.is_stale("asc7181", "g001", 5) is False
    assert mapper.is_stale("asc7181", "g001", 4) is False, "旧版本的 PEL 条目不触发重载"
    assert mapper.is_stale("asc7181", "g001", 6) is True
    assert mapper.is_stale("asc7181", "g001", 0) is False, "无版本信息不触发"

    conn.rows = [_row(1, "asc1"), _row(2, "asc2")]
    reloaded = mapper.reload("asc7181", "g001", 6)
    assert reloaded is not None and reloaded.var_map == {"1": "asc1", "2": "asc2"}
    assert mapper.is_stale("asc7181", "g001", 6) is False


def test_mark_version_seen_stops_retry_storm():
    mapper = MysqlMapper(_Conn([_row(1, "asc1")]))
    mapper.get_or_load("asc7181", "g001", 1)
    mapper.mark_version_seen("asc7181", "g001", 2)
    assert mapper.is_stale("asc7181", "g001", 2) is False


class _VersionedMapper:
    """缓存命中但版本过期时，reload 换上新列名。"""

    def __init__(self):
        self.mapping = GroupMapping(config_version=1, child_table="asc7181", stable="sdasc",
                                    var_map={"1": "old1"}, var_type={})
        self.reload_calls = 0

    def get_cached(self, device_code, group_code):
        return self.mapping

    def get_or_load(self, device_code, group_code, version):
        return self.mapping

    def is_stale(self, device_code, group_code, version):
        return version > self.mapping.config_version

    def mark_version_seen(self, *args):
        pass

    def reload(self, device_code, group_code, version, missing_points=False):
        self.reload_calls += 1
        self.mapping = GroupMapping(config_version=version, child_table="asc7181", stable="sdasc",
                                    var_map={"1": "asc1"}, var_type={})
        return self.mapping


def _consumer(mapper, syslog=None) -> StreamConsumer:
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[])
    config = {
        "writer": {"flush_interval_ms": 500, "batch_count": 100, "max_buffer_rows": 1000,
                   "pending_min_idle_ms": 60000},
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }
    return StreamConsumer(config, mapper, writer, MagicMock(), metrics=WriterPerformanceMetrics(),
                          syslog=syslog)


def _entry(values: dict, version: int) -> dict:
    return {"device_code": "asc7181", "device_type": "asc", "group_code": "g001",
            "ts": "1000", "config_version": str(version), "values": json.dumps(values)}


async def _handle(consumer, entry):
    await consumer._handle_entry(stream_key="rt:stream:asc:g001:0", entry_id="1-0",
                                 entry_data=entry, delivery_count=1, prepared=None)


@pytest.mark.asyncio
async def test_newer_stream_version_reloads_warm_cache():
    mapper = _VersionedMapper()
    consumer = _consumer(mapper)

    await _handle(consumer, _entry({"1": "7"}, version=2))

    assert mapper.reload_calls == 1
    assert consumer._buffer[0].columns == {"asc1": 7.0}


@pytest.mark.asyncio
async def test_invalid_column_does_not_block_valid_points():
    mapper = MagicMock()
    mapper.get_cached.return_value = GroupMapping(
        config_version=1, child_table="asc7181", stable="sdasc",
        var_map={"1": "asc1"}, var_type={}, invalid_columns={"2": " as c2"},
    )
    mapper.is_stale.return_value = False
    consumer = _consumer(mapper)

    await _handle(consumer, _entry({"1": "7", "2": "8"}, version=1))

    assert len(consumer._buffer) == 1
    assert consumer._buffer[0].columns == {"asc1": 7.0}


def test_persistent_mapping_problem_emits_system_log_once(monkeypatch):
    syslog = MagicMock()
    consumer = _consumer(MagicMock(), syslog=syslog)
    clock = iter([0.0, 10.0, 70.0, 80.0, 140.0])
    monkeypatch.setattr(consumer_mod.time, "monotonic", lambda: next(clock))

    for _ in range(5):
        consumer._warn_mapping("asc7181", "g001", "unknown_var_ids", "missing %s", "x")

    assert syslog.emit.call_count == 1
    kwargs = syslog.emit.call_args.kwargs
    assert kwargs["level"] == "ERROR"
    assert "points resync --device asc7181" in kwargs["content"]
