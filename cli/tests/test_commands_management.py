from __future__ import annotations

import json
from typing import Any

from typer.testing import CliRunner

import cli.commands.common as common_module
import cli.main as main_module
from cli.permissions import get_command_policy


runner = CliRunner()


class FakeResolver:
    def resolve_catalog_device(self, identifier: str) -> dict[str, Any]:
        assert identifier in {"PLC001", "桥吊100"}
        return {"id": 7, "deviceCode": "QC100"}

    def resolve_history_device(self, identifier: str) -> dict[str, Any]:
        assert identifier == "PLC001"
        return {"id": 7, "deviceCode": "PLC001"}

    def resolve_history_points(
        self,
        device_id: int,
        identifiers: list[str],
    ) -> list[dict[str, Any]]:
        assert device_id == 7
        return [{"varSeq": 1, "pointName": identifiers[0]}]


    def resolve_business_points(
        self, identifiers: list[str], *, device_hint: str | None = None
    ) -> tuple[dict[str, Any], list[dict[str, Any]]]:
        assert device_hint == "PLC001"
        return (
            {"id": 7, "deviceId": 7, "deviceCode": "PLC001"},
            [{"pointId": 11, "deviceId": 7, "varSeq": 1, "pointName": identifiers[0]}],
        )


class FakeAuthorizedSession:
    requests: list[tuple[str, str, Any]] = []

    def __init__(self, _state) -> None:
        self.resolver = FakeResolver()

    def __enter__(self) -> "FakeAuthorizedSession":
        return self

    def __exit__(self, *_args: object) -> None:
        pass

    def authorize(self, _command: str) -> dict[str, Any]:
        return {"permissions": []}

    def request(self, method: str, path: str, *, params=None, json_body=None):
        type(self).requests.append((method, path, json_body))
        if path.endswith("/history/export"):
            return {"taskId": "task-1"}
        return {"deleted": True}

    def download(self, path: str, *, params=None):
        type(self).requests.append(("GET", path, params))
        return b"xlsx", "device-export.xlsx"


def test_wrong_device_confirmation_never_sends_delete(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        ["devices", "delete", "桥吊100", "--confirm", "qc100"],
    )

    assert result.exit_code == 2
    assert json.loads(result.stderr)["error"]["type"] == "confirmation_required"
    assert FakeAuthorizedSession.requests == []


def test_matching_device_confirmation_sends_delete(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        ["devices", "delete", "桥吊100", "--confirm", "QC100"],
    )

    assert result.exit_code == 0
    assert FakeAuthorizedSession.requests == [
        ("DELETE", "/api/device-monitor/device/7", None)
    ]


def test_history_export_uses_resolved_integer_contract(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        [
            "history",
            "export",
            "--device",
            "PLC001",
            "--point",
            "TEMP001",
            "--start",
            "2026-08-10 00:00:00",
            "--end",
            "2026-08-10 01:00:00",
        ],
    )

    assert result.exit_code == 0
    body = FakeAuthorizedSession.requests[0][2]
    assert body["deviceId"] == 7
    assert body["varSeqs"] == [1]


def test_device_export_writes_download_atomically(tmp_path, monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)
    output = tmp_path / "devices.xlsx"

    result = runner.invoke(
        main_module.app,
        ["devices", "export", "--output", str(output)],
    )

    assert result.exit_code == 0
    assert output.read_bytes() == b"xlsx"
    assert json.loads(result.stdout)["data"]["bytes"] == 4


def test_point_template_policy_matches_actual_export_route_permission() -> None:
    policy = get_command_policy("points template")
    assert policy is not None
    assert policy.permissions_any == frozenset({"device_monitor:point:export"})


def test_point_template_resolves_business_device_name(tmp_path, monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)
    output = tmp_path / "point-template.xlsx"

    result = runner.invoke(
        main_module.app,
        [
            "points",
            "template",
            "--device",
            "桥吊100",
            "--output",
            str(output),
        ],
    )

    assert result.exit_code == 0
    assert FakeAuthorizedSession.requests == [
        (
            "GET",
            "/api/device-monitor/point/export",
            {
                "deviceCode": "QC100",
                "pageNo": 1,
                "pageSize": 10000,
                "template": "true",
            },
        )
    ]
