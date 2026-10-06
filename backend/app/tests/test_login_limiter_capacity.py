from app.core.login_limiter import LoginLimiter


def test_random_username_flood_does_not_block_new_users():
    limiter = LoginLimiter(3, 60)
    for index in range(10010):
        limiter.record_failure(str(index), "attacker")
    assert len(limiter._failures) == 10000
    assert not limiter.is_blocked("normal", "normal-ip")
    for _ in range(3):
        limiter.record_failure("normal", "normal-ip")
    assert limiter.is_blocked("normal", "normal-ip")


def test_expiration_and_reset_preserve_window(monkeypatch):
    monkeypatch.setattr("app.core.login_limiter.time.monotonic", lambda: 100)
    limiter = LoginLimiter(1, 10)
    limiter.record_failure("user", "ip")
    assert limiter.is_blocked("user", "ip")
    monkeypatch.setattr("app.core.login_limiter.time.monotonic", lambda: 111)
    assert not limiter.is_blocked("user", "ip")
    limiter.record_failure("user", "ip")
    limiter.reset("user", "ip")
    assert not limiter.is_blocked("user", "ip")
