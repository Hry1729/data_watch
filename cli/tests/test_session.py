from __future__ import annotations

from typing import Any

import pytest

import cli.session as session_module
from cli.config import RuntimeSettings
from cli.credentials import StoredCredentials
from cli.errors import CliError, EXIT_PERMISSION_DENIED
from cli.session import AuthorizedSession


class FakeCredentialStore:
    def load(
        self,
        profile: str,
        *,
        expected_base_url: str | None = None,
    ) -> StoredCredentials:
        assert profile == "development"
        assert expected_base_url == "http://backend.local"
        return StoredCredentials(
            token="Bearer token",
            base_url="http://backend.local",
        )


class FakeState:
    credential_store = FakeCredentialStore()
    dry_run = False
    idempotency_key = None
    all_pages = False
    max_items = 10000

    def runtime(self) -> RuntimeSettings:
        return RuntimeSettings("development", "http://backend.local", 5)


class FakeBackendClient:
    user_info_payload: dict[str, Any] = {}
    user_info_calls = 0
    requests: list[dict[str, Any]] = []

    def __init__(self, *_args, **_kwargs) -> None:
        pass

    def close(self) -> None:
        pass

    def get_user_info(self) -> dict[str, Any]:
        type(self).user_info_calls += 1
        return type(self).user_info_payload

    def request(self, method: str, path: str, **kwargs: Any) -> dict[str, Any]:
        type(self).requests.append({"method": method, "path": path, **kwargs})
        return {"sent": True}


def test_local_precheck_uses_permissions_not_buttons(monkeypatch) -> None:
    FakeBackendClient.user_info_payload = {
        "userName": "viewer",
        "roles": ["R_USER"],
        "buttons": ["edit"],
        "permissions": ["device_monitor:dashboard:view"],
    }
    FakeBackendClient.user_info_calls = 0
    monkeypatch.setattr(session_module, "BackendClient", FakeBackendClient)

    with AuthorizedSession(FakeState()) as session:
        session.authorize("dashboard overview")
        with pytest.raises(CliError) as captured:
            session.authorize("devices list")

    assert captured.value.exit_code == EXIT_PERMISSION_DENIED
    assert captured.value.details["requiredPermissionsAny"] == [
        "device_monitor:device:view"
    ]
    assert FakeBackendClient.user_info_calls == 1

def test_dry_run_does_not_send_write_and_redacts_secrets(monkeypatch) -> None:
    state = FakeState()
    state.dry_run = True
    state.idempotency_key = "ai-call-123"
    FakeBackendClient.requests = []
    monkeypatch.setattr(session_module, "BackendClient", FakeBackendClient)

    with AuthorizedSession(state) as session:
        result = session.request(
            "POST",
            "/api/user",
            json_body={"userName": "operator", "password": "hidden"},
        )

    assert FakeBackendClient.requests == []
    assert result["dryRun"] is True
    assert result["request"]["jsonBody"]["password"] == "***"
    assert result["request"]["idempotencyKey"] == "ai-call-123"


def test_idempotency_key_is_forwarded_only_for_writes(monkeypatch) -> None:
    state = FakeState()
    state.dry_run = False
    state.idempotency_key = "ai-call-123"
    FakeBackendClient.requests = []
    monkeypatch.setattr(session_module, "BackendClient", FakeBackendClient)

    with AuthorizedSession(state) as session:
        session.request("GET", "/api/items")
        session.request("POST", "/api/items", json_body={"name": "value"})

    assert FakeBackendClient.requests[0]["headers"] is None
    assert FakeBackendClient.requests[1]["headers"] == {
        "X-Idempotency-Key": "ai-call-123"
    }

def test_all_pages_aggregates_paginated_gets(monkeypatch) -> None:
    state = FakeState()
    state.all_pages = True
    state.max_items = 10
    FakeBackendClient.requests = []
    pages = iter(
        [
            {"list": [{"id": 1}], "records": [{"id": 1}], "total": 2},
            {"list": [{"id": 2}], "records": [{"id": 2}], "total": 2},
        ]
    )

    def request(_self, method: str, path: str, **kwargs: Any):
        FakeBackendClient.requests.append(
            {"method": method, "path": path, **kwargs}
        )
        return next(pages)

    monkeypatch.setattr(session_module, "BackendClient", FakeBackendClient)
    monkeypatch.setattr(FakeBackendClient, "request", request)

    with AuthorizedSession(state) as session:
        result = session.request(
            "GET",
            "/api/items",
            params={"pageNo": 1, "pageSize": 1},
        )

    assert result["list"] == [{"id": 1}, {"id": 2}]
    assert result["records"] == result["list"]
    assert result["allPages"] is True
    assert [call["params"]["pageNo"] for call in FakeBackendClient.requests] == [1, 2]
