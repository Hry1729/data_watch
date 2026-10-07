import asyncio
from types import SimpleNamespace
from unittest.mock import AsyncMock
import pytest
from redis.exceptions import ConnectionError, RedisError
from app.core import login_limiter
from app.core.exceptions import ApiError
from app.core.login_limiter import LoginLimiter
from app.services.auth import AuthService


def test_key_encoding_preserves_original_string_distinctions():
    limiter = LoginLimiter(3, 60)
    assert limiter._key(" Op ", " ip ") == limiter._key("op", "ip")
    assert limiter._key("\U00010000", "ip") != limiter._key("\ud800\udc00", "ip")
    assert limiter._key("a\x00b", "c") != limiter._key("a", "b\x00c")


@pytest.mark.asyncio
async def test_random_username_flood_preserves_existing_block_and_partial_count(login_redis):
    limiter = LoginLimiter(3, 60)
    for _ in range(3):
        await limiter.record_failure("blocked", "attacker")
    await limiter.record_failure("partial", "attacker")
    for index in range(10010):
        await limiter.record_failure(str(index), "attacker")
    assert await limiter.is_blocked("blocked", "attacker")
    assert not await limiter.is_blocked("normal", "normal-ip")
    for _ in range(2):
        await limiter.record_failure("partial", "attacker")
    assert await limiter.is_blocked("partial", "attacker")
    for _ in range(3):
        await limiter.record_failure("normal", "normal-ip")
    assert await limiter.is_blocked("normal", "normal-ip")


@pytest.mark.asyncio
async def test_shared_instances_normalization_and_reset(login_redis):
    first, second = LoginLimiter(2, 60), LoginLimiter(2, 60)
    await first.record_failure(" Op ", " ip ")
    await second.record_failure("op", "ip")
    assert await first.is_blocked("OP", "ip")
    assert await second.is_blocked("op", "ip")
    assert not await second.is_blocked("op", "other-ip")
    assert first._key("a\x00b", "c") != first._key("a", "b\x00c")
    await second.reset(" OP ", " ip ")
    assert not await first.is_blocked("op", "ip")


@pytest.mark.asyncio
async def test_exact_window_cutoff_and_check_does_not_extend_ttl(login_redis, monkeypatch):
    script = login_limiter._SCRIPT
    monkeypatch.setattr(login_limiter, "_SCRIPT", script.replace("redis.call('TIME')", "{100, 0}"))
    limiter = LoginLimiter(1, 10)
    await limiter.record_failure("user", "ip")
    ttl_before = await login_redis.pttl(limiter._key("user", "ip"))
    monkeypatch.setattr(login_limiter, "_SCRIPT", script.replace("redis.call('TIME')", "{109, 999999}"))
    await asyncio.sleep(0.02)
    assert await limiter.is_blocked("user", "ip")
    assert await login_redis.pttl(limiter._key("user", "ip")) < ttl_before
    monkeypatch.setattr(login_limiter, "_SCRIPT", script.replace("redis.call('TIME')", "{110, 0}"))
    assert not await limiter.is_blocked("user", "ip")


@pytest.mark.asyncio
async def test_same_instant_concurrency_counts_and_bounds_each_key(login_redis, monkeypatch):
    monkeypatch.setattr(login_limiter, "_SCRIPT", login_limiter._SCRIPT.replace("redis.call('TIME')", "{100, 0}"))
    limiter = LoginLimiter(20, 60)
    await asyncio.gather(*(limiter.record_failure("user", "ip") for _ in range(100)))
    assert await limiter.is_blocked("user", "ip")
    assert await login_redis.zcard(limiter._key("user", "ip")) == 20
    assert 0 < await login_redis.pttl(limiter._key("user", "ip")) <= 60001


@pytest.mark.asyncio
@pytest.mark.parametrize("action", ["is_blocked", "record_failure", "reset"])
@pytest.mark.parametrize("error", [ConnectionError("down"), RedisError("unavailable"), TimeoutError("timeout")])
async def test_redis_operation_failure_is_service_unavailable(monkeypatch, action, error):
    client = SimpleNamespace(eval=AsyncMock(side_effect=error))
    monkeypatch.setattr(login_limiter, "get_redis_client", AsyncMock(return_value=client))
    with pytest.raises(ApiError) as caught:
        await getattr(LoginLimiter(3, 60), action)("user", "ip")
    assert caught.value.code == caught.value.status_code == 503
    assert "down" not in caught.value.message


@pytest.mark.asyncio
@pytest.mark.parametrize("stage", ["check", "record", "reset"])
async def test_login_redis_failure_never_commits_or_issues_token(monkeypatch, stage):
    failure = ConnectionError("down")
    client = SimpleNamespace(eval=AsyncMock(side_effect=[failure] if stage == "check" else [0, failure]))
    monkeypatch.setattr(login_limiter, "get_redis_client", AsyncMock(return_value=client))
    monkeypatch.setattr("app.services.auth.get_login_limiter", lambda: LoginLimiter(3, 60))
    monkeypatch.setattr("app.services.auth.run_password_op", AsyncMock(return_value=stage == "reset"))
    monkeypatch.setattr("app.services.auth.write_system_log", AsyncMock())
    token = AsyncMock()
    monkeypatch.setattr("app.services.auth.create_signed_token", token)
    commit = AsyncMock()
    service = AuthService(SimpleNamespace(commit=commit))
    service._fetch_user_by_name = AsyncMock(return_value=SimpleNamespace(id=1, deleted=False, status="1", password_hash="hash"))
    service._fetch_user_roles = AsyncMock(return_value=["R_USER"])
    with pytest.raises(ApiError) as caught:
        await service.login(user_name="op", password="test", client_ip="ip")
    assert caught.value.code == 503
    commit.assert_not_awaited()
    token.assert_not_called()
