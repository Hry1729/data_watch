from __future__ import annotations

import json

import httpx
import pytest

from cli.client import BackendClient
from cli.errors import CliError, EXIT_PERMISSION_DENIED, EXIT_TIMEOUT


def test_login_and_user_info_use_backend_envelope_and_bearer_token() -> None:
    requests: list[httpx.Request] = []

    def handler(request: httpx.Request) -> httpx.Response:
        requests.append(request)
        if request.url.path == "/api/auth/login":
            assert json.loads(request.content) == {
                "userName": "operator",
                "password": "secret",
            }
            return httpx.Response(
                200,
                json={
                    "code": 200,
                    "msg": "ok",
                    "data": {"token": "Bearer signed-token", "refreshToken": "refresh"},
                },
            )
        return httpx.Response(
            200,
            json={
                "code": 200,
                "msg": "ok",
                "data": {
                    "userName": "operator",
                    "buttons": ["edit"],
                    "permissions": ["system:menu:update"],
                },
            },
        )

    with BackendClient(
        "http://backend.local",
        transport=httpx.MockTransport(handler),
    ) as client:
        login = client.login("operator", "secret")
        client.token = login["token"]
        user_info = client.get_user_info()

    assert user_info["permissions"] == ["system:menu:update"]
    assert requests[1].headers["Authorization"] == "Bearer signed-token"


def test_raw_token_is_normalized_for_http_authorization() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        assert request.headers["Authorization"] == "Bearer raw-token"
        return httpx.Response(200, json={"code": 200, "msg": "ok", "data": {}})

    with BackendClient(
        "http://backend.local",
        token="raw-token",
        transport=httpx.MockTransport(handler),
    ) as client:
        client.get_user_info()


def test_forbidden_response_maps_to_permission_exit_code() -> None:
    transport = httpx.MockTransport(
        lambda _request: httpx.Response(
            403,
            json={"code": 403, "msg": "无权访问", "data": None},
        )
    )

    with BackendClient(
        "http://backend.local",
        token="Bearer token",
        transport=transport,
    ) as client:
        with pytest.raises(CliError) as captured:
            client.get_user_info()

    assert captured.value.exit_code == EXIT_PERMISSION_DENIED
    assert captured.value.status == 403


def test_timeout_maps_to_timeout_exit_code() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        raise httpx.ReadTimeout("timed out", request=request)

    with BackendClient(
        "http://backend.local",
        transport=httpx.MockTransport(handler),
    ) as client:
        with pytest.raises(CliError) as captured:
            client.health()

    assert captured.value.exit_code == EXIT_TIMEOUT


def test_health_returns_real_enveloped_payload() -> None:
    transport = httpx.MockTransport(
        lambda _request: httpx.Response(
            200,
            json={
                "code": 200,
                "msg": "ready",
                "data": {"service": "backend", "stage": "monitor-read-ready"},
            },
        )
    )

    with BackendClient("http://backend.local", transport=transport) as client:
        assert client.health()["service"] == "backend"
