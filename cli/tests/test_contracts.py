from __future__ import annotations

import io
import json

import pytest
from typer.main import get_command
from typer.testing import CliRunner

import cli.main as main_module
from cli.errors import CliError
from cli.files import load_json_object, require_confirmation, set_confirmation_bypass
from cli.schema import find_command_schema


runner = CliRunner()


def test_command_schema_contains_permission_and_parameters() -> None:
    schema = find_command_schema(get_command(main_module.app), "history query")

    assert schema is not None
    assert schema["access"]["permissionsAny"] == ["device_monitor:history:view"]
    device = next(item for item in schema["parameters"] if item["name"] == "device")
    assert device["kind"] == "option"
    assert "--device" in device["options"]


def test_commands_schema_is_available_as_json() -> None:
    result = runner.invoke(main_module.app, ["commands", "schema", "devices", "get"])

    assert result.exit_code == 0
    schema = json.loads(result.stdout)["data"]
    assert schema["command"] == "devices get"
    identifier = next(
        item for item in schema["parameters"] if item["name"] == "identifier"
    )
    assert identifier["kind"] == "argument"
    assert "options" not in identifier


def test_select_projects_success_data() -> None:
    result = runner.invoke(main_module.app, ["--select", "version", "version"])

    assert result.exit_code == 0
    assert json.loads(result.stdout)["data"] == main_module.CLI_VERSION


def test_json_file_can_be_read_from_stdin(monkeypatch) -> None:
    monkeypatch.setattr("sys.stdin", io.StringIO('{"deviceCode":"QC100"}'))

    assert load_json_object("-")["deviceCode"] == "QC100"


def test_global_confirmation_bypass_is_explicit() -> None:
    set_confirmation_bypass(True)
    require_confirmation("", "QC100")
    set_confirmation_bypass(False)

    with pytest.raises(CliError):
        require_confirmation("", "QC100")
