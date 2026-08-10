from __future__ import annotations

import httpx

from cli.client import BackendClient


def test_download_returns_binary_content_and_safe_server_filename() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.headers["Authorization"] == "Bearer token"
        return httpx.Response(
            200,
            content=b"workbook-bytes",
            headers={"Content-Disposition": 'attachment; filename="device-export.xlsx"'},
        )

    with BackendClient(
        "http://backend.local",
        token="Bearer token",
        transport=httpx.MockTransport(handler),
    ) as client:
        content, filename = client.download("/api/device-monitor/device/export")

    assert content == b"workbook-bytes"
    assert filename == "device-export.xlsx"


def test_download_strips_path_components_from_server_filename() -> None:
    transport = httpx.MockTransport(
        lambda _request: httpx.Response(
            200,
            content=b"csv",
            headers={"Content-Disposition": 'attachment; filename="../history.csv"'},
        )
    )

    with BackendClient(
        "http://backend.local",
        token="token",
        transport=transport,
    ) as client:
        _, filename = client.download("/download")

    assert filename == "history.csv"
