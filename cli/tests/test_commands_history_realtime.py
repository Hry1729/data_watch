from __future__ import annotations

import json
from typing import Any

from typer.testing import CliRunner

import cli.commands.common as common_module
import cli.main as main_module


runner = CliRunner()


class FakeResolver:
    def resolve_history_device(self, identifier: str) -> dict[str, Any]:
        assert identifier == "QC100"
        return {"id": 7, "deviceCode": "QC100"}

    def resolve_history_points(
        self,
        device_id: int,
        identifiers: list[str],
    ) -> list[dict[str, Any]]:
        assert device_id == 7
        return [
            {"varSeq": index + 1, "pointName": name}
            for index, name in enumerate(identifiers)
        ]

    def resolve_business_points(
        self, identifiers: list[str], *, device_hint: str | None = None
    ) -> tuple[dict[str, Any], list[dict[str, Any]]]:
        if device_hint is not None:
            assert device_hint == "桥吊100"
        points = [
            {
                "pointId": 11 + index,
                "deviceId": 7,
                "deviceCode": "QC100",
                "deviceName": "桥吊100",
                "varSeq": index + 1,
                "pointName": name,
                "ioAddress": f"DB1.DBW{index * 2}",
                "groupCode": "g002",
                "pointStatus": "enabled",
            }
            for index, name in enumerate(identifiers)
        ]
        return {"id": 7, "deviceId": 7, "deviceCode": "QC100", "deviceName": "桥吊100"}, points

    def resolve_monitor_device(self, identifier: str) -> dict[str, Any]:
        assert identifier == "QC100"
        return {"deviceId": 7, "deviceCode": "QC100"}


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
        type(self).requests.append((method, path, params))
        if path.endswith("/realtime-init"):
            return {
                "deviceId": 7,
                "deviceCode": "QC100",
                "data": [
                    {
                        "pointId": 11,
                        "varSeq": 1,
                        "pointName": "TEMP001",
                        "ioAddress": "DB1.DBW0",
                        "value": 23.5,
                    },
                    {
                        "pointId": 12,
                        "varSeq": 2,
                        "pointName": "PRESS001",
                        "ioAddress": "DB1.DBW2",
                        "value": 2.1,
                    },
                ],
            }
        return {"times": [], "points": []}


def test_history_query_sends_repeated_var_seqs(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        [
            "history",
            "query",
            "--point",
            "TEMP001",
            "--point",
            "PRESS001",
            "--start",
            "2026-08-10 00:00:00",
            "--end",
            "2026-08-10 01:00:00",
        ],
    )

    assert result.exit_code == 0
    params = FakeAuthorizedSession.requests[0][2]
    assert [value for key, value in params if key == "varSeqs"] == [1, 2]
    assert json.loads(result.stdout)["data"]["device"]["id"] == 7


def test_realtime_get_filters_one_point_without_changing_status_fields(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        [
            "realtime",
            "get",
            "--device",
            "桥吊100",
            "--point",
            "TEMP001",
        ],
    )

    assert result.exit_code == 0
    data = json.loads(result.stdout)["data"]
    assert data["deviceCode"] == "QC100"
    assert [point["pointName"] for point in data["data"]] == ["TEMP001"]
