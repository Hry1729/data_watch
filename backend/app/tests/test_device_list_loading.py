from __future__ import annotations

import os
import sys
import asyncio
from dataclasses import replace
from datetime import datetime
from types import SimpleNamespace
from unittest.mock import AsyncMock, patch

import pytest

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services.catalog import CatalogService, DeviceCatalogItem


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
    service._fetch_latest_collect_times = AsyncMock(return_value={})
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
    }


@pytest.mark.asyncio
async def test_default_list_keeps_legacy_summary_contract():
    service, devices = make_service()
    result = await service.list_devices({"pageNo": 1, "pageSize": 1})
    service._fetch_latest_collect_times.assert_awaited_once_with(devices)
    assert result["onlineCount"] == 2
    assert result["offlineCount"] == 2


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
