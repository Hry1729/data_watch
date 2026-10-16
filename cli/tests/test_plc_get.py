"""``plc get`` 的包络、退出码和鉴权拒绝。"""

from __future__ import annotations

import json
from pathlib import Path

from typer.main import get_command
from typer.testing import CliRunner

import cli.main as main_module
from cli.credentials import StoredCredentials
from cli.tests.test_command_policy_coverage import _leaf_commands
from direct_read.runtime import mvp_runtime_supported


runner = CliRunner()


class _Authorized:
    def __init__(self, _state) -> None:
        self.credentials = None

    def __enter__(self):
        return self

    def __exit__(self, *_args) -> None:
        return None

    def authorize(self, command: str) -> dict:
        assert command == "plc get"
        return {
            "userName": "operator",
            "permissions": ["device_monitor:monitor:view"],
            "roles": ["R_USER"],
        }


def test_plc_command_has_no_write() -> None:
    commands = set(_leaf_commands(get_command(main_module.app)))
    assert "plc get" in commands
    assert "plc write" not in commands


def test_missing_target_is_usage_error() -> None:
    result = runner.invoke(main_module.app, ["plc", "get"])

    assert result.exit_code == 2
    payload = json.loads(result.stderr)
    assert payload["ok"] is False
    assert payload["command"] == "plc get"
    assert payload["error"]["type"] == "usage_error"
    assert payload["error"]["details"]["source"] == "direct_plc"
    assert payload["schemaVersion"] == main_module.OUTPUT_SCHEMA_VERSION


def test_success_envelope(monkeypatch) -> None:
    captured = {}

    def fake_read(device, point, **kwargs):
        captured["device"] = device
        captured["point"] = point
        captured["actor"] = kwargs["actor"]
        captured["timeout_ms"] = kwargs["timeout_ms"]
        return {
            "source": "direct_plc",
            "topologySource": "mysql",
            "quality": 0,
            "qualityName": "GOOD",
            "value": 7,
        }

    monkeypatch.setattr("cli.commands.plc.AuthorizedSession", _Authorized)
    monkeypatch.setattr("cli.commands.plc.read_point", fake_read)

    result = runner.invoke(
        main_module.app,
        ["plc", "get", "--device", "QC100", "--point", "CraneID", "--timeout", "1"],
    )

    assert result.exit_code == 0, result.stderr
    payload = json.loads(result.stdout)
    assert payload["ok"] is True
    assert payload["command"] == "plc get"
    assert payload["data"]["source"] == "direct_plc"
    assert payload["data"]["quality"] == 0
    assert payload["schemaVersion"] == main_module.OUTPUT_SCHEMA_VERSION
    assert captured == {
        "device": "QC100",
        "point": "CraneID",
        "actor": "operator",
        "timeout_ms": 1000,
    }


def test_auth_refused_without_login(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.setenv("DATAWATCH_MYSQL_PASSWORD", "super-secret-db-password")
    monkeypatch.delenv("DATAWATCH_TOKEN", raising=False)

    def _load(_self, _profile, expected_base_url=None):
        return None

    def _forbidden(*_args, **_kwargs):
        raise AssertionError("未登录不能读 PLC")

    monkeypatch.setattr("cli.credentials.CredentialStore.load", _load)
    monkeypatch.setattr("cli.commands.plc.read_point", _forbidden)

    result = runner.invoke(
        main_module.app,
        ["plc", "get", "--device", "QC100", "--point", "CraneID"],
    )

    assert result.exit_code == 3
    payload = json.loads(result.stderr)
    assert payload["ok"] is False
    assert payload["error"]["type"] == "auth_required"
    assert payload["error"]["details"]["source"] == "direct_plc"
    assert payload["error"]["details"]["quality"] == 7
    audit = (tmp_path / "direct_read" / "audit.log").read_text(encoding="utf-8")
    assert "auth_required" in audit
    assert "super-secret-db-password" not in audit
    assert "QC100" in audit


def test_permission_denied_does_not_read(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))

    class _Client:
        def __init__(self, *_args, **_kwargs) -> None:
            pass

        def close(self) -> None:
            return None

        def get_user_info(self) -> dict:
            return {"userName": "viewer", "permissions": [], "roles": ["R_USER"]}

    def _load(_self, _profile, expected_base_url=None):
        return StoredCredentials(
            token="planted-cli-token-value",
            refresh_token="planted-refresh-token-value",
            user_name="viewer",
            base_url=expected_base_url,
        )

    def _forbidden(*_args, **_kwargs):
        raise AssertionError("无权限不能读 PLC")

    monkeypatch.setattr("cli.session.BackendClient", _Client)
    monkeypatch.setattr("cli.credentials.CredentialStore.load", _load)
    monkeypatch.setattr("cli.commands.plc.read_point", _forbidden)

    result = runner.invoke(
        main_module.app,
        ["plc", "get", "--device", "QC100", "--point", "CraneID"],
    )

    assert result.exit_code == 4, result.stderr
    payload = json.loads(result.stderr)
    assert payload["error"]["type"] == "permission_denied"
    assert payload["error"]["details"]["source"] == "direct_plc"
    audit = (tmp_path / "direct_read" / "audit.log").read_text(encoding="utf-8")
    assert "viewer" in audit
    assert "planted-cli-token-value" not in audit
    assert "planted-refresh-token-value" not in audit


def test_explicit_read_on_non_mvp_runtime_is_refused(monkeypatch, tmp_path: Path) -> None:
    if mvp_runtime_supported():
        return
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.setattr("cli.commands.plc.AuthorizedSession", _Authorized)

    result = runner.invoke(
        main_module.app,
        [
            "plc",
            "get",
            "--device",
            "QC100",
            "--point",
            "CraneID",
            "--topology",
            "explicit",
            "--endpoint",
            "10.1.2.3",
            "--address",
            "DB1.DBW0",
        ],
    )

    assert result.exit_code == 6, result.stderr
    payload = json.loads(result.stderr)
    assert payload["error"]["type"] == "unsupported_runtime"
    assert payload["error"]["details"]["source"] == "direct_plc"
    assert payload["error"]["details"]["quality"] == 7
