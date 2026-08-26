"""Small ASGI helpers for bounded request-body reads."""

from __future__ import annotations

from collections.abc import Awaitable, Callable
from typing import Any


class RequestBodyTooLarge(ValueError):
    """Raised when an ASGI request exceeds the configured body budget."""

    status_code = 413


async def read_limited_body(
    receive: Callable[[], Awaitable[dict[str, Any]]],
    max_body_bytes: int,
) -> bytes:
    """Read ASGI chunks while keeping the accumulated body within a hard cap."""
    body_parts: list[bytes] = []
    body_size = 0
    while True:
        message = await receive()
        chunk = message.get("body", b"")
        body_size += len(chunk)
        if body_size > max_body_bytes:
            raise RequestBodyTooLarge()
        body_parts.append(chunk)
        if not message.get("more_body", False):
            break
    return b"".join(body_parts)
