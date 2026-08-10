from __future__ import annotations

import pytest

from app.api.routes import monitor
from app.services.point_resolver import PointResolverService


ROWS = [
    {
        "point_id": 11,
        "device_id": 1,
        "device_code": "QC100",
        "device_name": "QC100",
        "device_group_name": "桥吊作业区",
        "device_type_name": "桥吊",
        "var_seq": 101,
        "point_name": "CraneID",
        "io_address": "I101",
        "description": "桥吊编号",
        "point_group_code": "g002",
        "point_group_name": "基础信息",
        "point_enabled": 1,
        "device_enabled": 1,
    },
    {
        "point_id": 22,
        "device_id": 2,
        "device_code": "ASC701L",
        "device_name": "ASC701L",
        "device_group_name": "轨道吊作业区",
        "device_type_name": "轨道吊",
        "var_seq": 101,
        "point_name": "CraneIdentifier",
        "io_address": "I101",
        "description": "桥吊编号备用",
        "point_group_code": "g003",
        "point_group_name": "标识信息",
        "point_enabled": 1,
        "device_enabled": 1,
    },
]


class FakeResult:
    def mappings(self) -> "FakeResult":
        return self

    def all(self) -> list[dict]:
        return ROWS


class FakeSession:
    params: dict | None = None

    async def execute(self, _statement, params) -> FakeResult:
        type(self).params = params
        return FakeResult()


@pytest.mark.asyncio
async def test_exact_business_name_wins_over_contains_match() -> None:
    result = await PointResolverService(FakeSession()).search("Crane ID")

    assert result["bestMatchCount"] == 1
    assert result["items"][0]["pointId"] == 11
    assert result["items"][0]["groupCode"] == "g002"
    assert result["items"][0]["matchedFields"] == ["pointName"]
    assert FakeSession.params["query_key"] == "craneid"


@pytest.mark.asyncio
async def test_io_address_requires_device_hint_when_ambiguous() -> None:
    ambiguous = await PointResolverService(FakeSession()).search("I101")
    narrowed = await PointResolverService(FakeSession()).search(
        "I101", device_hint="asc701l"
    )

    assert ambiguous["bestMatchCount"] == 2
    assert narrowed["bestMatchCount"] == 1
    assert narrowed["items"][0]["deviceName"] == "ASC701L"
    assert narrowed["items"][0]["pointId"] == 22


@pytest.mark.asyncio
async def test_point_resolve_route_accepts_read_permissions(monkeypatch) -> None:
    class PermissionChecked(Exception):
        pass

    async def fake_require_permissions(_credentials, required):
        assert required == {
            "device_monitor:monitor:view",
            "device_monitor:realtime:view",
            "device_monitor:history:view",
            "device_monitor:point:view",
        }
        raise PermissionChecked

    monkeypatch.setattr(monitor, "require_permissions", fake_require_permissions)

    with pytest.raises(PermissionChecked):
        await monitor.monitor_point_resolve("I101", credentials=None)


@pytest.mark.asyncio
async def test_dispatch_route_coerces_typed_query_parameters() -> None:
    from types import SimpleNamespace

    from app.main import (
        CaseInsensitiveHeaders,
        CompatQueryParams,
        CompatRequest,
        EnhancedASGITurboAPI,
    )

    async def handler(limit: int = 20, enabled: bool = False):
        return {"limit": limit, "enabled": enabled}

    query_params = CompatQueryParams([("limit", "20"), ("enabled", "true")])
    request = CompatRequest(
        method="GET",
        path="/test",
        query_string="limit=20&enabled=true",
        headers=CaseInsensitiveHeaders({}),
        path_params={},
        query_params=query_params,
        body=b"",
    )

    result = await EnhancedASGITurboAPI._dispatch_route(
        object.__new__(EnhancedASGITurboAPI),
        route=SimpleNamespace(handler=handler, path_params=[]),
        path_params={},
        query_params=query_params,
        query_string="limit=20&enabled=true",
        headers=CaseInsensitiveHeaders({}),
        body=b"",
        request=request,
    )

    assert result == {"limit": 20, "enabled": True}


def test_response_encoder_serializes_datetime() -> None:
    import json
    from datetime import datetime

    from app.main import EnhancedASGITurboAPI

    encoded = EnhancedASGITurboAPI._encode_content(
        object.__new__(EnhancedASGITurboAPI),
        {"createdAt": datetime(2026, 8, 10, 17, 5, 12)},
    )

    assert json.loads(encoded) == {"createdAt": "2026-08-10T17:05:12"}
