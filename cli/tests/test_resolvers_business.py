from __future__ import annotations

from typing import Any

import pytest

from cli.errors import CliError
from cli.resolvers import Resolver


def point(
    point_id: int,
    *,
    device_id: int,
    device_name: str,
    point_name: str,
    io_address: str,
    device_code: str | None = None,
    rank: int = 0,
) -> dict[str, Any]:
    return {
        "pointId": point_id,
        "deviceId": device_id,
        "deviceCode": device_code or f"QC{99 + device_id}",
        "deviceName": device_name,
        "deviceGroupName": "桥吊作业区",
        "deviceTypeName": "桥吊",
        "varSeq": point_id,
        "pointName": point_name,
        "ioAddress": io_address,
        "description": "",
        "groupCode": "g002",
        "groupName": "业务点",
        "matchRank": rank,
    }


class FakeSession:
    def __init__(self, responses: dict[str, list[dict[str, Any]]]) -> None:
        self.responses = responses
        self.requests: list[tuple[str, str, Any]] = []

    def request(self, method: str, path: str, *, params=None, json_body=None):
        self.requests.append((method, path, params))
        query = str(params["query"])
        return {"items": self.responses.get(query, [])}


def test_business_point_resolution_is_cached_and_infers_device_group() -> None:
    session = FakeSession(
        {
            "Crane ID": [
                point(
                    11,
                    device_id=1,
                    device_name="QC100",
                    point_name="CraneID",
                    io_address="DB1702.DBW0",
                )
            ]
        }
    )
    resolver = Resolver(session)

    first_device, first_points = resolver.resolve_business_points(["Crane ID"])
    second_device, second_points = resolver.resolve_business_points(["Crane ID"])

    assert first_device["deviceName"] == "QC100"
    assert first_points[0]["groupCode"] == "g002"
    assert second_device == first_device
    assert second_points == first_points
    assert len(session.requests) == 1


def test_business_point_device_hint_uses_terminal_device_alias() -> None:
    session = FakeSession(
        {
            "I101": [
                point(
                    11,
                    device_id=1,
                    device_name="ASC701L",
                    point_name="Ready",
                    device_code="ASC701L",
                    io_address="I101",
                )
            ]
        }
    )

    Resolver(session).resolve_business_points(
        ["I101"], device_hint="轨道吊701陆侧"
    )

    assert session.requests[0][2]["deviceHint"] == "asc701l"


def test_ambiguous_io_address_returns_business_candidates() -> None:
    resolver = Resolver(
        FakeSession(
            {
                "I101": [
                    point(11, device_id=1, device_name="QC100", point_name="Ready", io_address="I101"),
                    point(22, device_id=2, device_name="QC101", point_name="Ready", io_address="I101"),
                ]
            }
        )
    )

    with pytest.raises(CliError) as captured:
        resolver.resolve_business_points(["I101"])

    assert captured.value.status == 409
    assert [item["deviceName"] for item in captured.value.details["candidates"]] == [
        "QC100",
        "QC101",
    ]


def test_multi_point_lookup_rejects_cross_device_selection() -> None:
    resolver = Resolver(
        FakeSession(
            {
                "CraneID": [
                    point(11, device_id=1, device_name="QC100", point_name="CraneID", io_address="I101")
                ],
                "CraneMode": [
                    point(22, device_id=2, device_name="QC101", point_name="CraneMode", io_address="I102")
                ],
            }
        )
    )

    with pytest.raises(CliError) as captured:
        resolver.resolve_business_points(["CraneID", "CraneMode"])

    assert captured.value.status == 409
    assert "不同设备" in captured.value.message
