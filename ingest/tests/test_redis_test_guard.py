import pytest

from ingest.tests.redis_test_guard import (
    ALLOW_REMOTE_ENV,
    RedisTestGuardError,
    validate_test_redis_url,
)


def test_guard_rejects_db_zero():
    with pytest.raises(RedisTestGuardError, match="dedicated non-zero DB"):
        validate_test_redis_url("redis://127.0.0.1:6379/0")

    with pytest.raises(RedisTestGuardError, match="dedicated non-zero DB"):
        validate_test_redis_url("redis://127.0.0.1:6379")


def test_guard_rejects_invalid_scheme():
    with pytest.raises(RedisTestGuardError, match="scheme must be"):
        validate_test_redis_url("http://127.0.0.1:6379/1")


def test_guard_rejects_remote_without_flag(monkeypatch):
    monkeypatch.delenv(ALLOW_REMOTE_ENV, raising=False)
    with pytest.raises(RedisTestGuardError, match="is not loopback"):
        validate_test_redis_url("redis://192.168.1.100:6379/1")

    with pytest.raises(RedisTestGuardError, match="is not loopback"):
        validate_test_redis_url("redis://prod-redis.internal:6379/1")


def test_guard_allows_remote_with_flag(monkeypatch):
    monkeypatch.setenv(ALLOW_REMOTE_ENV, "1")
    url = "redis://192.168.1.100:6379/1"
    assert validate_test_redis_url(url, allow_remote=True) == url


def test_guard_allows_loopback_with_nonzero_db():
    assert (
        validate_test_redis_url("redis://127.0.0.1:6379/15")
        == "redis://127.0.0.1:6379/15"
    )
    assert (
        validate_test_redis_url("redis://localhost:6379/1")
        == "redis://localhost:6379/1"
    )
    assert (
        validate_test_redis_url("redis://[::1]:6379/2")
        == "redis://[::1]:6379/2"
    )
