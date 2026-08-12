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

def test_compatibility_is_anonymous_and_returns_contract() -> None:
    def handler(request: httpx.Request) -> httpx.Response:
        assert "Authorization" not in request.headers
        assert request.url.path == "/api/cli/compatibility"
        return httpx.Response(
            200,
            json={
                "code": 200,
                "msg": "ok",
                "data": {
                    "backendVersion": "0.1.0",
                    "apiVersion": "1.0",
                    "minCliVersion": "0.2.0",
                    "maxCliVersion": "0.x",
                },
            },
        )

    transport = httpx.MockTransport(handler)
    with BackendClient("http://backend.local", transport=transport) as client:
        assert client.compatibility()["apiVersion"] == "1.0"

def test_get_retries_transient_status_without_retrying_post() -> None:
    calls = {"get": 0, "post": 0}

    def handler(request: httpx.Request) -> httpx.Response:
        if request.method == "GET":
            calls["get"] += 1
            if calls["get"] == 1:
                return httpx.Response(503, json={"code": 503, "msg": "busy"})
            return httpx.Response(200, json={"code": 200, "data": {"ok": True}})
        calls["post"] += 1
        return httpx.Response(503, json={"code": 503, "msg": "busy"})

    with BackendClient(
        "http://backend.local",
        token="token",
        transport=httpx.MockTransport(handler),
        retry_backoff_seconds=0,
    ) as client:
        assert client.request("GET", "/items") == {"ok": True}
        with pytest.raises(CliError):
            client.request("POST", "/items", json_body={})

    assert calls == {"get": 2, "post": 1}


def test_unauthorized_request_refreshes_once_and_persists_rotated_tokens() -> None:
    requests: list[str] = []
    refreshed: list[tuple[str, str | None]] = []

    def handler(request: httpx.Request) -> httpx.Response:
        requests.append(request.url.path)
        if request.url.path == "/api/auth/refresh":
            assert json.loads(request.content) == {"refreshToken": "refresh-old"}
            return httpx.Response(
                200,
                json={
                    "code": 200,
                    "data": {
                        "token": "Bearer access-new",
                        "refreshToken": "refresh-new",
                    },
                },
            )
        if request.headers.get("Authorization") == "Bearer access-old":
            return httpx.Response(401, json={"code": 401, "msg": "expired"})
        return httpx.Response(200, json={"code": 200, "data": {"userName": "operator"}})

    with BackendClient(
        "http://backend.local",
        token="Bearer access-old",
        refresh_token="refresh-old",
        token_refreshed=lambda token, refresh: refreshed.append((token, refresh)),
        transport=httpx.MockTransport(handler),
    ) as client:
        assert client.get_user_info()["userName"] == "operator"

    assert requests == ["/api/user/info", "/api/auth/refresh", "/api/user/info"]
    assert refreshed == [("Bearer access-new", "refresh-new")]
