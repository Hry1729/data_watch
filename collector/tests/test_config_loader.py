import pytest

from core.config_loader import load_collector_config
from core.exceptions import ConfigError


def _write_config(path, password="toml-password"):
    path.write_text(
        f"""
[mysql]
host = "127.0.0.1"
port = 3306
user = "root"
password = "{password}"
database = "device_watch"

[redis]
host = "127.0.0.1"
port = 6379
""".strip(),
        encoding="utf-8",
    )


def test_mysql_password_can_be_overridden_by_env(tmp_path, monkeypatch):
    path = tmp_path / "settings.toml"
    _write_config(path)
    monkeypatch.setenv("DATAWATCH_MYSQL_PASSWORD", "env-password")

    config = load_collector_config(str(path))

    assert config.mysql.password == "env-password"


def test_mysql_password_uses_toml_when_env_missing(tmp_path, monkeypatch):
    path = tmp_path / "settings.toml"
    _write_config(path)
    monkeypatch.delenv("DATAWATCH_MYSQL_PASSWORD", raising=False)

    config = load_collector_config(str(path))

    assert config.mysql.password == "toml-password"


def test_connection_fields_overridden_by_env(tmp_path, monkeypatch):
    path = tmp_path / "settings.toml"
    _write_config(path)
    monkeypatch.setenv("DATAWATCH_MYSQL_HOST", "db-host")
    monkeypatch.setenv("DATAWATCH_MYSQL_PORT", "3307")
    monkeypatch.setenv("DATAWATCH_MYSQL_USER", "agentuser")
    monkeypatch.setenv("DATAWATCH_MYSQL_DATABASE", "dw")
    monkeypatch.setenv("DATAWATCH_MYSQL_PASSWORD", "mpw")
    monkeypatch.setenv("DATAWATCH_REDIS_HOST", "rds-host")
    monkeypatch.setenv("DATAWATCH_REDIS_PORT", "6380")
    monkeypatch.setenv("DATAWATCH_REDIS_PASSWORD", "rpw")

    config = load_collector_config(str(path))

    assert (config.mysql.host, config.mysql.port, config.mysql.user) == ("db-host", 3307, "agentuser")
    assert (config.mysql.database, config.mysql.password) == ("dw", "mpw")
    assert (config.redis.host, config.redis.port, config.redis.password) == ("rds-host", 6380, "rpw")


def test_invalid_int_env_raises_config_error(tmp_path, monkeypatch):
    path = tmp_path / "settings.toml"
    _write_config(path)
    monkeypatch.setenv("DATAWATCH_MYSQL_PORT", "not-a-number")

    with pytest.raises(ConfigError):
        load_collector_config(str(path))


def test_ge_max_payload_can_be_overridden_by_env(tmp_path, monkeypatch):
    path = tmp_path / "settings.toml"
    _write_config(path)
    monkeypatch.setenv("DATAWATCH_GE_MAX_PAYLOAD_BYTES", "1000")
    monkeypatch.setenv("DATAWATCH_GE_PARALLEL_CHANNELS", "2")
    monkeypatch.setenv("DATAWATCH_GE_BATCH_DEADLINE_MS", "300")

    config = load_collector_config(str(path))

    assert config.ge_max_payload_bytes == 1000
    assert config.ge_parallel_channels == 2
    assert config.ge_batch_deadline_ms == 300


def test_redis_db_and_shard_count_overridden_by_env(tmp_path, monkeypatch):
    """B013：集中式 Collector 与 Writer 同源读取 Redis DB / 分片数。"""
    path = tmp_path / "settings.toml"
    _write_config(path)
    with path.open("a", encoding="utf-8") as fh:
        fh.write("\ndb = 2\nstream_shard_count = 4\n")
    monkeypatch.setenv("DATAWATCH_REDIS_DB", "3")
    monkeypatch.setenv("DATAWATCH_REDIS_STREAM_SHARD_COUNT", "8")

    config = load_collector_config(str(path))

    assert (config.redis.db, config.redis.stream_shard_count) == (3, 8)


def test_redis_db_env_zero_overrides_toml(tmp_path, monkeypatch):
    """Agent 显式下发 DB=0 时 Collector 必须与 Writer 一致，而不是沿用 TOML 的 2。"""
    path = tmp_path / "settings.toml"
    _write_config(path)
    with path.open("a", encoding="utf-8") as fh:
        fh.write("\ndb = 2\n")
    monkeypatch.setenv("DATAWATCH_REDIS_DB", "0")
    monkeypatch.delenv("DATAWATCH_REDIS_STREAM_SHARD_COUNT", raising=False)

    config = load_collector_config(str(path))

    assert config.redis.db == 0
    assert config.redis.stream_shard_count == 4


def test_redis_db_env_missing_keeps_toml(tmp_path, monkeypatch):
    path = tmp_path / "settings.toml"
    _write_config(path)
    with path.open("a", encoding="utf-8") as fh:
        fh.write("\ndb = 2\n")
    monkeypatch.delenv("DATAWATCH_REDIS_DB", raising=False)
    monkeypatch.delenv("DATAWATCH_REDIS_STREAM_SHARD_COUNT", raising=False)

    assert load_collector_config(str(path)).redis.db == 2
