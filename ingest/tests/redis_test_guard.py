"""Guards for Redis integration tests (DW-09).

The integration tests call FLUSHDB against whatever INGEST_TEST_REDIS_URL
points at. A mistyped URL that lands on a business Redis must fail closed:
these helpers validate the target before any destructive command is issued.

Isolation contract:
- the URL must select a dedicated non-zero DB index (business data on
  self-hosted Redis conventionally lives in db 0);
- non-loopback hosts require an explicit INGEST_TEST_REDIS_ALLOW_REMOTE=1
  opt-in, so a prod hostname is rejected by default;
- the fixture proves write access to the exact target DB with a marker
  round-trip before flushing anything.
"""

from __future__ import annotations

from urllib.parse import urlsplit

LOOPBACK_HOSTS = {"127.0.0.1", "::1", "localhost"}
ALLOW_REMOTE_ENV = "INGEST_TEST_REDIS_ALLOW_REMOTE"
MARKER_KEY = "ingest:integration-test:marker"


class RedisTestGuardError(RuntimeError):
    """Raised when the configured Redis target fails the isolation guard."""


def validate_test_redis_url(url: str, *, allow_remote: bool = False) -> str:
    """Return the URL when it is safe for flushdb-based tests, else raise."""
    parsed = urlsplit(url)
    if parsed.scheme not in {"redis", "rediss", "unix"}:
        raise RedisTestGuardError(
            f"INGEST_TEST_REDIS_URL scheme must be redis/rediss/unix, got: {parsed.scheme!r}"
        )
    if parsed.scheme == "unix":
        return url

    host = parsed.hostname or ""
    if host not in LOOPBACK_HOSTS and not allow_remote:
        raise RedisTestGuardError(
            f"INGEST_TEST_REDIS_URL host {host!r} is not loopback; "
            f"set {ALLOW_REMOTE_ENV}=1 to explicitly allow a remote test instance"
        )
    db = parsed.path.lstrip("/")
    if not db or db == "0":
        raise RedisTestGuardError(
            "INGEST_TEST_REDIS_URL must select a dedicated non-zero DB "
            "(e.g. redis://127.0.0.1:6379/15); db 0 may hold business data"
        )
    return url
