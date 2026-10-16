from __future__ import annotations

import asyncio
from datetime import datetime, timedelta
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest
import pytest_asyncio

from app.services import catalog, dashboard, device_activity


@pytest_asyncio.fixture(autouse=True)
async def reset_td_activity_cache():
    device_activity._td_cache.clear()
    device_activity._td_inflight.clear()
    yield
    tasks = list(device_activity._td_inflight.values())
    for task in tasks:
        task.cancel()
    await asyncio.gather(*tasks, return_exceptions=True)
    device_activity._td_cache.clear()


def make_tdengine(rows=()):
    return SimpleNamespace(
        host="activity-test", port=6041, user="test", password="test", use_ssl=False,
        database="test", qualify_table=lambda db, table: f"`{db}`.`{table}`",
        fetch_all=AsyncMock(return_value=list(rows)),
    )


@pytest.mark.asyncio
async def test_catalog_and_dashboard_share_td_query_and_short_cache(monkeypatch):
    timestamp = datetime(2026, 10, 15, 10)
    tdengine = make_tdengine([("qc1", timestamp)])
    clock = [100.0]
    monkeypatch.setattr(device_activity, "monotonic", lambda: clock[0])
    catalog_service = catalog.CatalogService(session=None, tdengine=tdengine)
    dashboard_service = dashboard.DashboardService(session=None, tdengine=tdengine)

    first, second = await asyncio.gather(
        catalog_service._fetch_type_latest_collect_times("test", "qc", ["qc1"]),
        dashboard_service._fetch_type_latest_collect_times("test", "qc", ["qc1"]),
    )
    assert first == second == {"qc1": timestamp}
    first.clear()
    assert await device_activity.fetch_type_latest_collect_times(tdengine, "test", "qc", ["qc1"]) == second
    assert tdengine.fetch_all.await_count == 1

    clock[0] += 3
    await device_activity.fetch_type_latest_collect_times(tdengine, "test", "qc", ["qc1"])
    assert tdengine.fetch_all.await_count == 2


@pytest.mark.asyncio
@pytest.mark.parametrize("service_module,service_class", [
    (catalog, catalog.CatalogService), (dashboard, dashboard.DashboardService),
])
async def test_fallback_queries_only_missing_device_tables(monkeypatch, service_module, service_class):
    timestamp = datetime(2026, 10, 15, 10)
    tdengine = make_tdengine([("qc2", timestamp)])
    service = service_class(session=None, tdengine=tdengine)
    if service_module is dashboard:
        service._filter_existing_stable_types = AsyncMock(return_value=["qc"])
    monkeypatch.setattr(service_module, "fetch_latest_collect_times", AsyncMock(return_value=({"QC1": timestamp}, True)))
    devices = [
        SimpleNamespace(device_code="QC1", device_type_code="QC"),
        SimpleNamespace(device_code="QC2", device_type_code="QC"),
        SimpleNamespace(device_code="D3", device_type_code=""),
    ]

    assert await service._fetch_latest_collect_times(devices) == {"QC1": timestamp, "QC2": timestamp}
    tdengine.fetch_all.assert_awaited_once_with(
        "SELECT tbname AS tbl, LAST(ts) AS last_ts FROM `test`.`sdqc` "
        "WHERE tbname IN ('qc2') GROUP BY tbname",
        database="test",
    )


@pytest.mark.asyncio
@pytest.mark.parametrize("service_module,service_class", [
    (catalog, catalog.CatalogService), (dashboard, dashboard.DashboardService),
])
async def test_td_failure_preserves_other_types_and_redis_without_trusting_raw_status(
    monkeypatch, service_module, service_class,
):
    timestamp = datetime(2026, 10, 15, 10)
    tdengine = make_tdengine()

    async def query(sql, **kwargs):
        if "`sdqc`" in sql:
            raise TimeoutError("unavailable")
        return [("agv1", timestamp)]

    tdengine.fetch_all.side_effect = query
    service = service_class(session=None, tdengine=tdengine)
    if service_module is dashboard:
        service._filter_existing_stable_types = AsyncMock(return_value=["qc", "agv"])
    monkeypatch.setattr(service_module, "fetch_latest_collect_times", AsyncMock(return_value=({"D1": timestamp}, False)))
    monkeypatch.setattr(service_module, "now_local", lambda: timestamp + timedelta(minutes=30))
    devices = [
        SimpleNamespace(device_code="D1", device_type_code="qc", raw_status=1),
        SimpleNamespace(device_code="QC2", device_type_code="qc", raw_status=1),
        SimpleNamespace(device_code="AGV1", device_type_code="agv", raw_status=1),
    ]

    expected = {"D1": timestamp, "AGV1": timestamp}
    assert await service._fetch_latest_collect_times(devices) == expected
    assert await service._fetch_latest_collect_times(devices) == expected
    assert tdengine.fetch_all.await_count == 3
    if service_module is catalog:
        assert service._build_connect_status(timestamp) == "offline"
        assert service._build_connect_status(None) == "offline"
    else:
        assert service._build_dynamic_connect_status(devices[0], timestamp) == "offline"
        assert service._build_dynamic_connect_status(devices[1], None) == "offline"


@pytest.mark.asyncio
async def test_failed_td_result_is_retried_and_cache_has_size_bound(monkeypatch):
    tdengine = make_tdengine()
    tdengine.fetch_all.side_effect = [ConnectionError("unavailable"), [], [], []]
    monkeypatch.setattr(device_activity, "_CACHE_MAX_ENTRIES", 2)
    with pytest.raises(ConnectionError):
        await device_activity.fetch_type_latest_collect_times(tdengine, "test", "qc", ["qc1"])
    assert not device_activity._td_cache

    for number in range(1, 4):
        assert await device_activity.fetch_type_latest_collect_times(tdengine, "test", "qc", [f"qc{number}"]) == {}
    assert tdengine.fetch_all.await_count == 4
    assert len(device_activity._td_cache) == 2


@pytest.mark.asyncio
async def test_td_cache_is_scoped_to_server_and_requested_tables():
    timestamp = datetime(2026, 10, 15, 10)
    first = make_tdengine([("qc1", timestamp)])
    second = make_tdengine([("qc1", timestamp + timedelta(seconds=1))])
    second.host = "other-activity-test"

    first_result = await device_activity.fetch_type_latest_collect_times(first, "test", "qc", ["qc1"])
    second_result = await device_activity.fetch_type_latest_collect_times(second, "test", "qc", ["qc1"])
    assert first_result != second_result
    await device_activity.fetch_type_latest_collect_times(first, "test", "qc", ["qc2"])
    assert first.fetch_all.await_count == 2
    assert second.fetch_all.await_count == 1
