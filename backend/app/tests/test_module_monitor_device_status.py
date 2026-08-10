from __future__ import annotations

from datetime import timedelta

import pytest

from app.core.status_thresholds import UNSTABLE_MAX_AGE_SECONDS
from app.core.time_utils import now_local
from app.services.module_monitor import ModuleMonitorService


ROWS = [
    {
        "device_id": 1,
        "device_code": "PLC001",
        "device_name": "PLC 1",
        "enable": 1,
        "group_name": "Line A",
        "protocol": "S7",
    },
    {
        "device_id": 2,
        "device_code": "PLC002",
        "device_name": "PLC 2",
        "enable": 0,
        "group_name": "Line B",
        "protocol": "OPCUA",
    },
]


class FakeResult:
    def mappings(self) -> "FakeResult":
        return self

    def all(self) -> list[dict]:
        return ROWS


class FakeSession:
    async def execute(self, _statement) -> FakeResult:
        return FakeResult()


@pytest.mark.asyncio
async def test_device_statuses_separate_management_and_connection_state(monkeypatch):
    async def fake_latest_times(device_codes):
        assert list(device_codes) == ["PLC001", "PLC002"]
        return (
            {
                "PLC001": now_local(),
                "PLC002": now_local()
                - timedelta(seconds=UNSTABLE_MAX_AGE_SECONDS + 1),
            },
            True,
        )

    monkeypatch.setattr(
        "app.services.module_monitor.fetch_latest_collect_times",
        fake_latest_times,
    )

    result = await ModuleMonitorService(FakeSession()).get_device_statuses()

    assert result["statusSource"] == "collector_metrics"
    assert result["onlineDevices"] == 1
    assert result["offlineDevices"] == 1
    assert result["items"][0]["deviceStatus"] == "enabled"
    assert result["items"][0]["connectStatus"] == "online"
    assert result["items"][1]["deviceStatus"] == "disabled"
    assert result["items"][1]["connectStatus"] == "offline"


@pytest.mark.asyncio
async def test_device_statuses_report_unknown_when_activity_source_is_unavailable(
    monkeypatch,
):
    async def fake_latest_times(_device_codes):
        return {}, False

    monkeypatch.setattr(
        "app.services.module_monitor.fetch_latest_collect_times",
        fake_latest_times,
    )

    result = await ModuleMonitorService(FakeSession()).get_device_statuses()

    assert result["statusSource"] == "collector_metrics_unavailable"
    assert result["unknownDevices"] == 2
    assert {item["connectStatus"] for item in result["items"]} == {"unknown"}
