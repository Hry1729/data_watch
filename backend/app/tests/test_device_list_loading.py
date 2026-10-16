from __future__ import annotations

import os
import sys
import asyncio
from dataclasses import replace
from datetime import datetime, timedelta
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import pytest

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services.catalog import CatalogService, DeviceCatalogItem
from app.core.time_utils import now_local


def make_service():
    devices = [
        DeviceCatalogItem(
            device_id=index, device_code=f"D{index}", device_name=f"Device {index}",
            device_type="type", device_type_code="type", group_name="", group_code="",
            driver_name="driver", enable=0 if index == 4 else 1,
            device_status_raw=status, endpoint="", endpoint_bkp="", create_time=None, update_time=None,
        )
        for index, status in enumerate([1, 0, 2, 1], 1)
    ]
    service = CatalogService(session=None, tdengine=None)
    service._fetch_device_rows = AsyncMock(return_value=devices)
    service._to_device_item = lambda row: row
    timestamp = now_local()
    service._fetch_latest_collect_times = AsyncMock(return_value={
        "D1": timestamp - timedelta(seconds=10),
        "D3": timestamp - timedelta(seconds=90),
        "D4": timestamp - timedelta(seconds=10),
    })
    return service, devices


@pytest.mark.asyncio
async def test_list_only_fetches_current_page_activity_without_summary():
    service, devices = make_service()
    result = await service.list_devices({"pageNo": 2, "pageSize": 2, "includeSummary": "false"})
    service._fetch_latest_collect_times.assert_awaited_once_with(devices[2:])
    assert [item["id"] for item in result["list"]] == [3, 4]
    assert result["total"] == 4
    assert "onlineCount" not in result


@pytest.mark.asyncio
async def test_status_filter_still_checks_all_candidates_before_pagination():
    service, devices = make_service()
    result = await service.list_devices({"pageNo": 1, "pageSize": 1, "includeSummary": False, "connectStatus": "online"})
    service._fetch_latest_collect_times.assert_awaited_once_with(devices)
    assert result["total"] == 2
    assert result["list"][0]["id"] == 1


@pytest.mark.asyncio
async def test_summary_preserves_distinct_status_and_disabled_counts():
    service, _ = make_service()
    assert await service.get_device_summary({}) == {
        "total": 4, "onlineCount": 2, "offlineCount": 1, "unstableCount": 1, "disabledCount": 1,
        "enabledOnlineCount": 1, "enabledOfflineCount": 1,
    }


@pytest.mark.asyncio
async def test_default_list_keeps_legacy_summary_contract():
    service, devices = make_service()
    result = await service.list_devices({"pageNo": 1, "pageSize": 1})
    service._fetch_latest_collect_times.assert_awaited_once_with(devices)
    assert result["onlineCount"] == 2
    assert result["offlineCount"] == 2


@pytest.mark.asyncio
async def test_missing_collect_times_do_not_trust_historical_online_status():
    service, devices = make_service()
    service._fetch_latest_collect_times.return_value = {}

    result = await service.list_devices({"pageSize": 10})
    assert [item["connectStatus"] for item in result["list"]] == ["offline"] * len(devices)
    assert result["onlineCount"] == 0
    assert result["offlineCount"] == 4
    assert result["list"][-1]["deviceStatus"] == "disabled"
    assert all(item["dataStatus"] == "nodata" for item in result["list"])

    online = await service.list_devices({"connectStatus": "online", "includeSummary": False})
    assert online["total"] == 0
    assert online["list"] == []
    assert await service.get_device_summary({}) == {
        "total": 4, "onlineCount": 0, "offlineCount": 4, "unstableCount": 0,
        "disabledCount": 1, "enabledOnlineCount": 0, "enabledOfflineCount": 3,
    }


def test_recent_and_stale_collect_times_override_raw_status_without_enabling_devices():
    service, devices = make_service()
    timestamp = now_local()
    fresh = service._serialize_device_item(replace(devices[1], enable=0), timestamp - timedelta(seconds=10))
    stale = service._serialize_device_item(devices[0], timestamp - timedelta(minutes=10))
    assert fresh["connectStatus"] == "online"
    assert fresh["deviceStatus"] == "disabled"
    assert stale["connectStatus"] == "offline"


@pytest.mark.asyncio
async def test_group_tree_uses_collect_times_and_preserves_empty_groups():
    service, devices = make_service()
    devices = [replace(device, device_status_raw=1) for device in devices]
    timestamp = now_local()
    service._fetch_latest_collect_times.return_value = {
        "D1": timestamp - timedelta(seconds=10),
        "D3": timestamp - timedelta(seconds=90),
        "D4": timestamp - timedelta(minutes=10),
    }
    service._fetch_device_group_rows = AsyncMock(return_value=[
        {"group_id": 1, "group_name": "Group 1", "group_code": "g1", "device_id": device.device_id, "device": device}
        for device in devices
    ] + [{"group_id": 2, "group_name": "Empty", "group_code": "g2", "device_id": None}])
    service._to_device_item = lambda row: row["device"]

    groups = await service.get_device_group_tree()
    assert [item["connectStatus"] for item in groups[0]["children"]] == ["online", "offline", "unstable", "offline"]
    assert groups[0]["count"] == 4
    assert groups[1]["children"] == []
    assert groups[1]["count"] == 0
    service._fetch_latest_collect_times.assert_awaited_once_with(devices)


@pytest.mark.asyncio
async def test_empty_group_tree_does_not_query_activity():
    service, _ = make_service()
    service._fetch_device_group_rows = AsyncMock(return_value=[])
    assert await service.get_device_group_tree() == []
    service._fetch_latest_collect_times.assert_not_awaited()


@pytest.mark.asyncio
async def test_activity_keeps_redis_results_when_missing_devices_have_no_td_type():
    service, devices = make_service()
    del service._fetch_latest_collect_times
    service.tdengine = SimpleNamespace(database="test")
    timestamp = datetime(2026, 9, 29, 10)
    with patch("app.services.catalog.fetch_latest_collect_times", AsyncMock(return_value=({"D1": timestamp}, True))):
        result = await service._fetch_latest_collect_times([devices[0], replace(devices[1], device_type_code="")])
    assert result == {"D1": timestamp}


@pytest.mark.asyncio
async def test_slow_td_fallback_is_bounded_and_keeps_redis_results():
    service, devices = make_service()
    del service._fetch_latest_collect_times
    service.tdengine = SimpleNamespace(database="test")
    timestamp = datetime(2026, 9, 29, 10)
    cancelled = asyncio.Event()

    async def slow_query(*args):
        try:
            await asyncio.Event().wait()
        finally:
            cancelled.set()

    service._fetch_type_latest_collect_times = slow_query
    wait_for = asyncio.wait_for

    async def fast_deadline(awaitable, timeout):
        assert timeout == 5.0
        return await wait_for(awaitable, 0.01)

    with patch("app.services.catalog.fetch_latest_collect_times", AsyncMock(return_value=({"D1": timestamp}, True))):
        with patch("app.services.catalog.asyncio.wait_for", fast_deadline):
            result = await service._fetch_latest_collect_times(devices[:2])
    assert result == {"D1": timestamp}
    assert cancelled.is_set()
