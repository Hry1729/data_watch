"""H3 / Edge 16：Writer 流发现必须与解码/Ingest 校验同源（point_storage_mapping），
Redis DB 与 shard 数可由 Agent 统一经环境变量下发。"""
import main
from main import _STREAM_SCOPE_SQL, _apply_env_overrides, _discover_stream_scopes


class _Cursor:
    def __init__(self, rows):
        self.rows = rows
        self.sql = None

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False

    def execute(self, sql):
        self.sql = sql

    def fetchall(self):
        return self.rows


class _Conn:
    def __init__(self, rows):
        self.cursor_obj = _Cursor(rows)

    def cursor(self, *_args):
        return self.cursor_obj


def test_scope_sql_reads_permanent_mapping_not_enabled_points() -> None:
    assert "FROM point_storage_mapping" in _STREAM_SCOPE_SQL
    # 点位禁用/软删后映射仍在、积压仍需消费：不得按 device_variable 的 enable/deleted 过滤
    assert "device_variable" not in _STREAM_SCOPE_SQL
    assert "enable" not in _STREAM_SCOPE_SQL
    assert "deleted" not in _STREAM_SCOPE_SQL


def test_discover_scopes_from_mapping_rows() -> None:
    conn = _Conn([
        {"device_type": "QC", "group_code": "g001"},
        {"device_type": "agv", "group_code": " g002 "},
        {"device_type": "", "group_code": "g003"},
    ])
    assert _discover_stream_scopes(conn) == [("qc", "g001"), ("agv", "g002")]
    assert conn.cursor_obj.sql == _STREAM_SCOPE_SQL


def test_env_overrides_redis_db_and_shard_count(monkeypatch) -> None:
    monkeypatch.setenv("DATAWATCH_REDIS_DB", "3")
    monkeypatch.setenv("DATAWATCH_REDIS_STREAM_SHARD_COUNT", "8")
    config = {"redis": {"db": 0, "stream_shard_count": 4}}
    _apply_env_overrides(config)
    assert config["redis"]["db"] == 3
    assert config["redis"]["stream_shard_count"] == 8


def test_env_overrides_absent_keeps_settings(monkeypatch) -> None:
    monkeypatch.delenv("DATAWATCH_REDIS_DB", raising=False)
    monkeypatch.delenv("DATAWATCH_REDIS_STREAM_SHARD_COUNT", raising=False)
    config = {"redis": {"db": 1, "stream_shard_count": 2}}
    _apply_env_overrides(config)
    assert config["redis"] == {"db": 1, "stream_shard_count": 2}
