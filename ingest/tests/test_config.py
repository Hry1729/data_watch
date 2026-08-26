from __future__ import annotations

from types import SimpleNamespace

import pytest

from ingest.app.config import IngestSettings


def test_control_cache_and_access_log_environment(monkeypatch) -> None:
    monkeypatch.setenv("INGEST_CONTROL_CACHE_TTL_MS", "1250")
    monkeypatch.setenv("INGEST_ACCESS_LOG", "yes")
    monkeypatch.setenv("INGEST_WORKERS", "6")
    monkeypatch.setenv("INGEST_MYSQL_POOL_MAXSIZE", "7")

    settings = IngestSettings.from_env()

    assert settings.control_cache_ttl_ms == 1250
    assert settings.access_log is True
    assert settings.workers == 6
    assert settings.mysql_pool_maxsize == 7


@pytest.mark.parametrize(
    ("name", "value", "message"),
    [
        ("INGEST_CONTROL_CACHE_TTL_MS", "0", "control cache ttl"),
        ("INGEST_ACCESS_LOG", "sometimes", "must be a boolean"),
        ("INGEST_WORKERS", "0", "workers must be positive"),
        ("INGEST_MYSQL_POOL_MAXSIZE", "0", "mysql pool maxsize"),
    ],
)
def test_invalid_capacity_environment(monkeypatch, name, value, message) -> None:
    monkeypatch.setenv(name, value)

    with pytest.raises(ValueError, match=message):
        IngestSettings.from_env()

def test_capacity_defaults_use_physical_cpu_count(monkeypatch) -> None:
    monkeypatch.delenv("INGEST_WORKERS", raising=False)
    monkeypatch.setattr(
        "ingest.app.config.psutil",
        SimpleNamespace(cpu_count=lambda logical: 8 if logical is False else 16),
    )

    settings = IngestSettings.from_env()

    assert settings.workers == 4
    assert settings.control_cache_ttl_ms == 30_000
    assert settings.mysql_pool_maxsize == 5


def test_capacity_defaults_fall_back_to_logical_cpu_estimate(monkeypatch) -> None:
    monkeypatch.delenv("INGEST_WORKERS", raising=False)
    monkeypatch.setattr("ingest.app.config.psutil", None)
    monkeypatch.setattr("ingest.app.config.os.cpu_count", lambda: 14)

    settings = IngestSettings.from_env()

    assert settings.workers == 4
