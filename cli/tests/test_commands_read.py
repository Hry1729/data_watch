from __future__ import annotations

import json
from typing import Any

from typer.testing import CliRunner

import cli.commands.common as common_module
import cli.main as main_module


runner = CliRunner()


class FakeAuthorizedSession:
    requests: list[tuple[str, str, Any]] = []

    def __init__(self, _state) -> None:
        pass

    def __enter__(self) -> "FakeAuthorizedSession":
        return self

    def __exit__(self, *_args: object) -> None:
        pass

    def authorize(self, _command: str) -> dict[str, Any]:
        return {"permissions": []}

    def request(self, method: str, path: str, *, params=None, json_body=None):
        type(self).requests.append((method, path, params))
        if path.endswith("/overview"):
            return {"deviceTotal": 3, "systemAlertTotal": 1}
        if path.endswith("/device-status"):
            return {
                "statusSource": "collector_metrics",
                "items": [
                    {
                        "deviceCode": "QC100",
                        "deviceName": "QC100",
                        "groupName": "桥吊",
                        "deviceStatus": "enabled",
                        "connectStatus": "online",
                    }
                ],
            }
        return {"list": [], "total": 0}


def test_dashboard_overview_calls_real_route(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(main_module.app, ["dashboard", "overview"])

    assert result.exit_code == 0
    assert json.loads(result.stdout)["data"]["deviceTotal"] == 3
    assert FakeAuthorizedSession.requests == [
        ("GET", "/api/device-monitor/dashboard/overview", None)
    ]


def test_device_status_preserves_management_and_connection_fields(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        ["devices", "status", "--device", "桥吊100"],
    )

    payload = json.loads(result.stdout)
    assert result.exit_code == 0
    assert payload["data"]["item"]["deviceStatus"] == "enabled"
    assert payload["data"]["item"]["connectStatus"] == "online"


def test_device_list_normalizes_terminal_business_keyword(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        ["devices", "list", "--keyword", "轨道吊701海侧"],
    )

    assert result.exit_code == 0
    params = FakeAuthorizedSession.requests[0][2]
    assert params["keyword"] == "asc701s"


def test_invalid_connect_status_uses_json_usage_error(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        ["devices", "list", "--connect-status", "broken"],
    )

    assert result.exit_code == 2
    error = json.loads(result.stderr)
    assert error["error"]["type"] == "usage_error"
    assert FakeAuthorizedSession.requests == []
