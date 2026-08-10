from __future__ import annotations

import json
from typing import Any

from typer.testing import CliRunner

import cli.commands.common as common_module
import cli.main as main_module


runner = CliRunner()


class FakeResolver:
    def search_business_points(
        self,
        query: str,
        *,
        device_hint: str | None = None,
        limit: int = 20,
    ) -> dict[str, Any]:
        assert query == "I101"
        assert device_hint == "轨道吊701陆侧"
        assert limit == 20
        return {
            "query": query,
            "deviceHint": device_hint,
            "items": [
                {
                    "deviceName": "ASC701L",
                    "pointName": "Ready",
                    "ioAddress": "I101",
                    "matchRank": 0,
                }
            ],
        }


class FakeAuthorizedSession:
    def __init__(self, _state) -> None:
        self.resolver = FakeResolver()

    def __enter__(self) -> "FakeAuthorizedSession":
        return self

    def __exit__(self, *_args: object) -> None:
        pass

    def authorize(self, command: str) -> dict[str, Any]:
        assert command == "points resolve"
        return {"permissions": ["device_monitor:realtime:view"]}


def test_points_resolve_accepts_io_address_and_business_device_name(monkeypatch) -> None:
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        ["points", "resolve", "I101", "--device", "轨道吊701陆侧"],
    )

    assert result.exit_code == 0
    data = json.loads(result.stdout)["data"]
    assert data["ambiguous"] is False
    assert data["selected"]["deviceName"] == "ASC701L"
    assert data["selected"]["ioAddress"] == "I101"
