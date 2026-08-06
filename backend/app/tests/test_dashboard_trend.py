import os
import sys
import unittest
from datetime import datetime, timedelta
from unittest.mock import AsyncMock

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services.dashboard import DashboardService, DashboardTrendDevice


class TestDashboardTrend(unittest.IsolatedAsyncioTestCase):
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


if __name__ == "__main__":
    unittest.main()
