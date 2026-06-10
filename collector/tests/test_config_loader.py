from core.config_loader import load_collector_config


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
