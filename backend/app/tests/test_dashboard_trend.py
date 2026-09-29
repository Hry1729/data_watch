import os
import sys
import asyncio
import unittest
from datetime import datetime, timedelta
from unittest.mock import AsyncMock, MagicMock, patch

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.core.time_utils import LOCAL_TZ
from app.core.exceptions import ApiError
from app.services import dashboard as dashboard_module
from app.services.dashboard import DashboardService, DashboardTrendDevice


class TestDashboardTrend(unittest.IsolatedAsyncioTestCase):
    def test_last_24_hours_bucket_labels_include_date(self):
        service = DashboardService(session=None, tdengine=None)
        range_start = datetime(2026, 8, 22, 14, 23)
        range_end = range_start + timedelta(days=1)

        first_label = service._format_bucket_label(
            bucket_start=datetime(2026, 8, 22, 14, 0),
            bucket_delta=timedelta(hours=1),
            range_start=range_start,
            range_end=range_end,
        )
        last_label = service._format_bucket_label(
            bucket_start=datetime(2026, 8, 23, 14, 0),
            bucket_delta=timedelta(hours=1),
            range_start=range_start,
            range_end=range_end,
        )

        self.assertEqual(first_label, "08-22 14:00")
        self.assertEqual(last_label, "08-23 14:00")
        self.assertNotEqual(first_label, last_label)

    def test_numeric_tdengine_bucket_timestamp_is_not_dropped(self):
        service = DashboardService(session=None, tdengine=None)
        bucket = datetime(2026, 8, 6, 10, 0, 0)
        epoch_ms = int(bucket.replace(tzinfo=LOCAL_TZ).timestamp() * 1000)

        self.assertEqual(service._normalize_bucket_rows([(epoch_ms, 12)], "1h"), {bucket: 12})

    async def test_online_device_count_uses_active_devices_per_bucket(self):
        service = DashboardService(session=None, tdengine=None)
        first_bucket = datetime(2026, 8, 6, 10, 0, 0)
        second_bucket = first_bucket + timedelta(hours=1)
        devices = [
            DashboardTrendDevice(device_id=1, device_code="QC100"),
            DashboardTrendDevice(device_id=2, device_code="AGV801"),
        ]

        service._resolve_time_range = lambda _range_type: (first_bucket, second_bucket)
        service._resolve_granularity = lambda _range_type, _granularity: "1h"
        service._build_bucket_starts = lambda *_args: [first_bucket, second_bucket]
        service._fetch_trend_devices = AsyncMock(return_value=devices)
        service._filter_trend_devices_with_existing_tables = AsyncMock(return_value=devices)
        service._fetch_device_bucket_counts = AsyncMock(
            side_effect=[
                {first_bucket: 10, second_bucket: 8},
                {first_bucket: 6},
            ]
        )
        service._build_alarm_series_from_logs = AsyncMock(return_value=[1, 2])

        result = await service.get_trend(
            {
                "rangeType": "last24h",
                "granularity": "1h",
                "metricKeys": ["collectCount", "alarmCount", "onlineDeviceCount"],
            }
        )

        series = {item["metricKey"]: item["data"] for item in result["series"]}
        self.assertEqual(series["collectCount"], [16, 8])
        self.assertEqual(series["alarmCount"], [1, 2])
        self.assertEqual(series["onlineDeviceCount"], [2, 1])

    async def test_trend_only_queries_devices_with_existing_data_tables(self):
        tdengine = MagicMock()
        tdengine.database = "device_data"
        tdengine.fetch_all = AsyncMock(
            return_value=[("qc100",), ("qc100_q",), ("unrelated",)]
        )
        service = DashboardService(session=None, tdengine=tdengine)
        devices = [
            DashboardTrendDevice(device_id=1, device_code="QC100"),
            DashboardTrendDevice(device_id=2, device_code="QC101"),
        ]

        result = await service._filter_trend_devices_with_existing_tables(devices)

        self.assertEqual(result, [devices[0]])
        tdengine.fetch_all.assert_awaited_once_with(
            "SHOW TABLES",
            database="device_data",
            timeout=dashboard_module._TREND_QUERY_TIMEOUT_SECONDS,
        )

    async def test_trend_table_probe_failure_does_not_query_every_device(self):
        tdengine = MagicMock()
        tdengine.database = "device_data"
        tdengine.fetch_all = AsyncMock(side_effect=RuntimeError("Authentication failure"))
        service = DashboardService(session=None, tdengine=tdengine)

        with self.assertRaises(ApiError) as failure:
            await service._filter_trend_devices_with_existing_tables(
                [
                    DashboardTrendDevice(device_id=1, device_code="QC100"),
                    DashboardTrendDevice(device_id=2, device_code="QC101"),
                ]
            )

        self.assertEqual(failure.exception.status_code, 503)
        self.assertEqual(tdengine.fetch_all.await_count, 1)

    async def test_dashboard_device_query_only_includes_enabled_devices(self):
        query_result = MagicMock()
        query_result.mappings.return_value.all.return_value = []
        session = MagicMock()
        session.execute = AsyncMock(return_value=query_result)
        service = DashboardService(session=session, tdengine=None)

        await service._fetch_dashboard_devices()

        statement = str(session.execute.await_args.args[0])
        self.assertIn("WHERE COALESCE(d.enable, 0) = 1", statement)

    async def test_latest_collect_times_only_queries_existing_stable_types(self):
        tdengine = MagicMock()
        tdengine.database = "device_data"
        tdengine.fetch_all = AsyncMock(
            return_value=[("sdqc",), ("sqqc",), ("unrelated",)]
        )
        service = DashboardService(session=None, tdengine=tdengine)

        result = await service._filter_existing_stable_types(
            "device_data",
            ["qc", "asc", "agv"],
        )

        self.assertEqual(result, ["qc"])
        tdengine.fetch_all.assert_awaited_once_with(
            "SHOW STABLES",
            database="device_data",
        )

    async def test_trend_reuses_successful_result(self):
        dashboard_module._trend_cache.clear()
        service = DashboardService(session=None, tdengine=None)
        service._build_trend = AsyncMock(return_value={"series": [1]})

        first = await service.get_trend({"rangeType": "today"})
        second = await service.get_trend({"rangeType": "today"})

        self.assertIs(first, second)
        self.assertEqual(service._build_trend.await_count, 1)

    async def test_trend_timeout_fails_and_is_not_cached(self):
        dashboard_module._trend_cache.clear()
        service = DashboardService(session=None, tdengine=None)

        async def never_finishes(*_args):
            await asyncio.Event().wait()

        service._build_trend = AsyncMock(side_effect=never_finishes)
        with patch.object(dashboard_module, "_TREND_TIMEOUT_SECONDS", 0.01):
            with self.assertRaises(ApiError) as failure:
                await service.get_trend({"rangeType": "today"})
        self.assertEqual(failure.exception.status_code, 504)

        service._build_trend = AsyncMock(return_value={"series": [2]})
        self.assertEqual(await service.get_trend({"rangeType": "today"}), {"series": [2]})
        self.assertEqual(service._build_trend.await_count, 1)

    async def test_trend_query_failure_is_not_cached_and_cancels_other_device_wrappers(self):
        dashboard_module._trend_cache.clear()
        service = DashboardService(session=None, tdengine=None)
        start = datetime(2026, 8, 6, 10)
        devices = [
            DashboardTrendDevice(device_id=1, device_code="QC301"),
            DashboardTrendDevice(device_id=2, device_code="QC302"),
        ]
        service._resolve_time_range = lambda _range_type: (start, start + timedelta(hours=1))
        service._build_bucket_starts = lambda *_args: [start]
        service._fetch_trend_devices = AsyncMock(return_value=devices)
        service._filter_trend_devices_with_existing_tables = AsyncMock(return_value=devices)
        second_cancelled = asyncio.Event()

        async def query(*, device, **_kwargs):
            if device.device_id == 1:
                await asyncio.sleep(0)
                raise ApiError(503, "TDengine unavailable", status_code=503)
            try:
                await asyncio.Event().wait()
            finally:
                second_cancelled.set()

        service._fetch_device_bucket_counts = AsyncMock(side_effect=query)
        with self.assertRaises(ApiError):
            await service.get_trend({"rangeType": "last7d"})
        await asyncio.wait_for(second_cancelled.wait(), timeout=1)
        self.assertFalse(dashboard_module._trend_cache)


if __name__ == "__main__":
    unittest.main()
