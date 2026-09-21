"""URL checks for explicitly provisioned, disposable Redis integration tests.

This guard is not proof of isolation. The fixture also requires an externally
provisioned marker and never executes FLUSHDB or FLUSHALL.
"""

from __future__ import annotations

from urllib.parse import urlsplit

LOOPBACK_HOSTS = {"127.0.0.1", "::1", "localhost"}
ALLOW_REMOTE_ENV = "INGEST_TEST_REDIS_ALLOW_REMOTE"
MARKER_KEY = "ingest:integration-test:marker"


class RedisTestGuardError(RuntimeError):
    """Raised when the configured Redis target fails the isolation guard."""


def validate_test_redis_url(url: str, *, allow_remote: bool = False) -> str:
    """Reject ambiguous or accidentally unscoped database URLs."""
    parsed = urlsplit(url)
    if parsed.scheme not in {"redis", "rediss"} or parsed.query or parsed.fragment:
        raise RedisTestGuardError("use a redis/rediss URL with an explicit DB path and no query overrides")
    host = parsed.hostname or ""
    if host not in LOOPBACK_HOSTS and not allow_remote:
        raise RedisTestGuardError("remote test instance requires explicit opt-in")
    db = parsed.path.lstrip("/")
    if not db.isdecimal() or int(db) <= 0:
        raise RedisTestGuardError("a dedicated non-zero DB is required")
    return url
