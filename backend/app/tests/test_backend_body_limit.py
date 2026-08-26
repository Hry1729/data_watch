"""ASGI request-body limits are enforced before route dispatch."""

from __future__ import annotations

import os
import sys

import pytest

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.http_body import RequestBodyTooLarge, read_limited_body


async def _run_body(messages: list[dict]) -> tuple[bytes | None, int, int | None]:
    received = 0
    sent: list[dict] = []

    async def receive() -> dict:
        nonlocal received
        received += 1
        return messages[received - 1]

    async def send(message: dict) -> None:
        sent.append(message)

    try:
        body = await read_limited_body(receive, 4)
        status = None
    except RequestBodyTooLarge as exc:
        body = None
        status = exc.status_code
    return body, received, status


@pytest.mark.asyncio
async def test_chunked_body_over_limit_raises_413_before_dispatch() -> None:
    body, received, status = await _run_body(
        [
            {"type": "http.request", "body": b"abc", "more_body": True},
            {"type": "http.request", "body": b"de", "more_body": False},
        ]
    )

    assert body is None
    assert status == 413
    assert received == 2


@pytest.mark.asyncio
async def test_body_within_limit_is_reassembled() -> None:
    body, received, status = await _run_body(
        [{"type": "http.request", "body": b"abcd", "more_body": False}]
    )

    assert body == b"abcd"
    assert status is None
    assert received == 1
