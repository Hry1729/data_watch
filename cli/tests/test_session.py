from __future__ import annotations

from typing import Any

import pytest

import cli.session as session_module
from cli.config import RuntimeSettings
from cli.credentials import StoredCredentials
from cli.errors import CliError, EXIT_PERMISSION_DENIED
from cli.session import AuthorizedSession


class FakeCredentialStore:
    def load(self, profile: str) -> StoredCredentials:
        assert profile == "development"
        return StoredCredentials(token="Bearer token")


class FakeState:
    credential_store = FakeCredentialStore()

    def runtime(self) -> RuntimeSettings:
        return RuntimeSettings("development", "http://backend.local", 5)


class FakeBackendClient:
    user_info_payload: dict[str, Any] = {}
    user_info_calls = 0

    def __init__(self, *_args, **_kwargs) -> None:
        pass

    def close(self) -> None:
        pass

    def get_user_info(self) -> dict[str, Any]:
        type(self).user_info_calls += 1
        return type(self).user_info_payload


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
