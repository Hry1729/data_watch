from __future__ import annotations

import json
import sys
from typing import Any

import pytest
from typer.testing import CliRunner

import cli.main as main_module


runner = CliRunner()


class FakeCredentialStore:
    saved: dict[str, Any] | None = None

    def __init__(self) -> None:
        pass

    def save(self, profile: str, **values: Any) -> None:
        type(self).saved = {"profile": profile, **values}


class FakeBackendClient:
    def __init__(self, base_url: str, **_kwargs: Any) -> None:
        self.base_url = base_url
        self.token: str | None = None

    def __enter__(self) -> "FakeBackendClient":
        return self

    def __exit__(self, *_args: object) -> None:
        pass

    def login(self, username: str, password: str) -> dict[str, str]:
        assert username == "operator"
        assert password == "secret"
        return {"token": "Bearer hidden-token", "refreshToken": "hidden-refresh"}

    def get_user_info(self) -> dict[str, Any]:
        assert self.token == "Bearer hidden-token"
        return {
            "userName": "operator",
            "roles": ["R_USER"],
            "buttons": ["edit"],
            "permissions": ["system:menu:update"],
        }


def test_version_outputs_compact_json() -> None:
    result = runner.invoke(main_module.app, ["version"])

    assert result.exit_code == 0
    payload = json.loads(result.stdout)
    assert payload["ok"] is True
    assert payload["command"] == "version"
    assert payload["schemaVersion"] == main_module.OUTPUT_SCHEMA_VERSION
    assert payload["cliVersion"] == main_module.CLI_VERSION
    assert payload["requestId"]
    assert payload["data"] == {
        "version": main_module.CLI_VERSION,
        "outputSchemaVersion": main_module.OUTPUT_SCHEMA_VERSION,
    }


def test_config_show_uses_isolated_config_file(tmp_path, monkeypatch) -> None:
    path = tmp_path / "cli-config.json"
    monkeypatch.setenv("DATAWATCH_CONFIG_FILE", str(path))

    result = runner.invoke(main_module.app, ["config", "show"])

    assert result.exit_code == 0
    payload = json.loads(result.stdout)
    assert payload["data"]["runtime"]["baseUrl"] == "http://127.0.0.1:8000"
    assert payload["data"]["configPath"] == str(path)


def test_config_set_url_reports_unknown_profile_as_config_error(tmp_path, monkeypatch) -> None:
    monkeypatch.setenv("DATAWATCH_CONFIG_FILE", str(tmp_path / "cli-config.json"))

    result = runner.invoke(
        main_module.app,
        ["--profile", "missing", "config", "set-url", "https://backend.example.com"],
    )

    assert result.exit_code == 2
    payload = json.loads(result.stderr)
    assert payload["error"]["type"] == "config_error"
    assert payload["error"]["message"] == "Profile 不存在: missing"


def test_login_reads_password_from_stdin_and_never_outputs_tokens(
    tmp_path,
    monkeypatch,
) -> None:
    FakeCredentialStore.saved = None
    monkeypatch.setenv("DATAWATCH_CONFIG_FILE", str(tmp_path / "config.json"))
    monkeypatch.setattr(main_module, "BackendClient", FakeBackendClient)
    monkeypatch.setattr(main_module, "CredentialStore", FakeCredentialStore)

    result = runner.invoke(
        main_module.app,
        ["auth", "login", "--username", "operator", "--password-stdin"],
        input="secret\n",
    )

    assert result.exit_code == 0
    assert "hidden-token" not in result.stdout
    assert "hidden-refresh" not in result.stdout
    assert json.loads(result.stdout)["data"]["user"]["userName"] == "operator"
    assert FakeCredentialStore.saved is not None
    assert FakeCredentialStore.saved["token"] == "Bearer hidden-token"
    assert FakeCredentialStore.saved["base_url"] == "http://127.0.0.1:8000"

def test_production_entrypoint_outputs_json_for_usage_errors(monkeypatch, capsys) -> None:
    monkeypatch.setattr(sys, "argv", ["data-watch", "devices", "get"])

    with pytest.raises(SystemExit) as captured:
        main_module.main()

    assert captured.value.code == 2
    payload = json.loads(capsys.readouterr().err)
    assert payload["ok"] is False
    assert payload["schemaVersion"] == main_module.OUTPUT_SCHEMA_VERSION
    assert payload["error"]["type"] == "usage_error"
