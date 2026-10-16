from dataclasses import replace
from unittest.mock import AsyncMock

import pytest

from app.services.catalog import CatalogService, DeviceCatalogItem
from app.core.time_utils import now_local


def make_service():
    devices = [
        DeviceCatalogItem(
            device_id=index, device_code=f"D{index}", device_name=f"Device {index}",
            device_type="QC" if index % 2 else "AGV", device_type_code="type",
            group_name="QC group", group_code="group", driver_name="driver", enable=1,
            device_status_raw=status, endpoint="", endpoint_bkp="", create_time=None, update_time=None,
        ) for index, status in enumerate([1, 0, 2, 1], 1)
    ]
    service = CatalogService(session=None, tdengine=None)
    service._fetch_device_rows = AsyncMock(return_value=devices)
    service._to_device_item = lambda row: row
    service._fetch_latest_collect_times = AsyncMock(return_value={"D1": now_local().replace(tzinfo=None)})
    return service, devices


@pytest.mark.asyncio
async def test_device_types_are_complete_and_scoped_without_activity_reads():
    service, _ = make_service()
    assert await service.list_device_types({}) == ["AGV", "QC"]
    assert await service.list_device_types({"deviceIds": ["1", "3"]}) == ["QC"]
    service._fetch_latest_collect_times.assert_not_awaited()


@pytest.mark.asyncio
async def test_device_id_and_exact_type_filter_before_pagination():
    service, devices = make_service()
    result = await service.list_devices({"deviceIds": ["3", "4"], "deviceTypeExact": "QC", "includeSummary": False, "pageSize": 1})
    assert [device["id"] for device in result["list"]] == [3]
    assert result["total"] == 1
    service._fetch_latest_collect_times.assert_awaited_once_with([devices[2]])


@pytest.mark.asyncio
async def test_search_and_saved_selection_can_reach_devices_after_ten_thousand():
    service, devices = make_service()
    target = replace(devices[0], device_id=15001, device_code="D15001")
    service._fetch_device_rows.return_value = [*devices, target]
    search = await service.list_devices({"keyword": "D15001", "includeSummary": False, "pageSize": 50})
    restored = await service.list_devices({"deviceIds": [15001], "includeSummary": False, "pageSize": 1})
    assert search["list"][0]["id"] == restored["list"][0]["id"] == 15001


@pytest.mark.asyncio
async def test_point_type_and_status_filter_intersects_edge_scope_before_sql_pagination():
    service, devices = make_service()
    service._count_point_rows = AsyncMock(return_value=37)
    service._fetch_point_rows = AsyncMock(return_value=[])
    result = await service.list_points({"deviceIds": [1, 2, 4], "deviceType": "QC", "connectStatus": "online", "pageNo": 2, "pageSize": 20})
    assert result["total"] == 37
    service._fetch_latest_collect_times.assert_awaited_once_with([devices[0]])
    args = service._fetch_point_rows.await_args.kwargs
    assert args["query_params"]["device_ids"] == [1]
    assert args["offset"] == 20
    assert args["limit"] == 20


@pytest.mark.asyncio
async def test_empty_point_device_filter_returns_empty_without_an_unrestricted_query():
    service, _ = make_service()
    service._count_point_rows = AsyncMock()
    result = await service.list_points({"deviceType": "missing", "pageNo": 2, "pageSize": 20})
    assert result["list"] == []
    assert result["total"] == 0
    service._count_point_rows.assert_not_awaited()
    service._fetch_latest_collect_times.assert_not_awaited()
