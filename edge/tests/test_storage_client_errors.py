"""Edge 5 / Collector 严重-2：Storage 客户端保留服务端错误原因，400 不重试。"""

from __future__ import annotations

import httpx
import pytest

from edge.app.storage_client import (
    EdgeStorageClient,
    StorageRejected,
    StorageUnauthorized,
    StorageUnavailable,
)


def _client(handler) -> tuple[EdgeStorageClient, list[httpx.Request]]:
    calls: list[httpx.Request] = []

    def recording(request: httpx.Request) -> httpx.Response:
        calls.append(request)
        return handler(request)

    http = httpx.Client(base_url="http://storage.test", transport=httpx.MockTransport(recording))
    return EdgeStorageClient("http://storage.test", role="collector", token="t", client=http), calls


def test_400_raises_storage_rejected_with_server_reason_without_retry() -> None:
    client, calls = _client(
        lambda request: httpx.Response(
            400, json={"ok": False, "error": "no active assignment for device QC100"}
        )
    )
    with pytest.raises(StorageRejected, match="no active assignment for device QC100") as info:
        client.status()
    assert isinstance(info.value, StorageUnavailable)
    assert len(calls) == 1


def test_5xx_retries_once_and_keeps_server_reason() -> None:
    client, calls = _client(
        lambda request: httpx.Response(
            500, json={"ok": False, "error": "outbox terminal transition lost lease"}
        )
    )
    with pytest.raises(StorageUnavailable, match="outbox terminal transition lost lease") as info:
        client.status()
    assert not isinstance(info.value, StorageRejected)
    assert len(calls) == 2


def test_503_queue_full_reason_is_reported() -> None:
    client, calls = _client(
        lambda request: httpx.Response(
            503, json={"ok": False, "error": "write queue remained full"}
        )
    )
    with pytest.raises(StorageUnavailable, match="HTTP 503 status: write queue remained full"):
        client.status()
    assert len(calls) == 2


def test_403_is_still_unauthorized_without_retry() -> None:
    client, calls = _client(
        lambda request: httpx.Response(403, json={"ok": False, "error": "not permitted"})
    )
    with pytest.raises(StorageUnauthorized):
        client.status()
    assert len(calls) == 1
