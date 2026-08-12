from __future__ import annotations

import pytest

from cli.credentials import CredentialStore, KEYRING_SERVICE
from cli.errors import CliError


class FakeKeyring:
    def __init__(self) -> None:
        self.values: dict[tuple[str, str], str] = {}

    def get_password(self, service: str, username: str) -> str | None:
        return self.values.get((service, username))

    def set_password(self, service: str, username: str, password: str) -> None:
        self.values[(service, username)] = password

    def delete_password(self, service: str, username: str) -> None:
        del self.values[(service, username)]


def test_keyring_credentials_round_trip_without_exposing_token() -> None:
    keyring = FakeKeyring()
    store = CredentialStore(environ={}, keyring_backend=keyring)

    store.save(
        "development",
        token="Bearer access-token",
        refresh_token="refresh-token",
        user_name="admin",
        base_url="http://backend.local:80/",
    )
    credentials = store.load(
        "development",
        expected_base_url="http://BACKEND.local",
    )

    assert credentials is not None
    assert credentials.token == "Bearer access-token"
    assert credentials.refresh_token == "refresh-token"
    assert credentials.base_url == "http://backend.local"
    assert credentials.source == "keyring"
    assert (KEYRING_SERVICE, "development") in keyring.values
    assert store.clear("development") is True
    assert store.load("development") is None


def test_keyring_credentials_cannot_cross_backend_scope() -> None:
    keyring = FakeKeyring()
    store = CredentialStore(environ={}, keyring_backend=keyring)
    store.save(
        "development",
        token="Bearer access-token",
        refresh_token=None,
        user_name="admin",
        base_url="http://backend-a.local",
    )

    with pytest.raises(CliError) as captured:
        store.load(
            "development",
            expected_base_url="http://backend-b.local",
        )

    assert captured.value.error_type == "credential_scope_mismatch"


def test_environment_token_takes_precedence_over_keyring() -> None:
    keyring = FakeKeyring()
    keyring.set_password(KEYRING_SERVICE, "development", "not valid json")
    store = CredentialStore(
        environ={
            "DATAWATCH_TOKEN": "Bearer environment-token",
            "DATAWATCH_REFRESH_TOKEN": "environment-refresh",
        },
        keyring_backend=keyring,
    )

    credentials = store.load("development")

    assert credentials is not None
    assert credentials.token == "Bearer environment-token"
    assert credentials.refresh_token == "environment-refresh"
    assert credentials.source == "environment"
