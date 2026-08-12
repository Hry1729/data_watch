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
