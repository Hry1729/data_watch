"""History TDengine data-access probe tests."""

from __future__ import annotations

import os
import sys
import unittest
from datetime import datetime
from unittest.mock import AsyncMock, MagicMock

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.core.exceptions import ApiError
from app.core.time_utils import LOCAL_TZ
from app.services.history import HistoryDevice, HistoryPoint, HistoryService


class TestHistoryTdengineProbe(unittest.IsolatedAsyncioTestCase):
    async def test_chart_keeps_numeric_tdengine_timestamps(self):
        service = HistoryService(MagicMock(), MagicMock())
        service._fetch_device = AsyncMock(
            return_value=HistoryDevice(1, "qc100", "桥吊100", "qc")
        )
        service._fetch_selected_points = AsyncMock(
            return_value=[HistoryPoint(11, "运行", "%M0.0", "BOOL", 1, None, "qc1", "g001")]
        )
        epoch_ms = int(datetime(2026, 8, 6, 10, 0, tzinfo=LOCAL_TZ).timestamp() * 1000)
        service._fetch_downsampled_series = AsyncMock(return_value=[(epoch_ms, 1)])

        result = await service.get_chart_data(
            1,
            [1],
            "2026-08-06 09:59:00",
            "2026-08-06 10:01:00",
            "1s",
            "avg",
        )

        self.assertEqual(result["series"][0]["data"], [["2026-08-06 10:00:00.000", 1]])

    async def test_metadata_success_does_not_hide_expired_data_license(self):
        tdengine = MagicMock()
        tdengine.qualify_table.return_value = "`device_data`.`sqqc`"
        tdengine.fetch_all = AsyncMock(
            side_effect=[
                [("sqqc",)],
                RuntimeError("License expired [0x80000800]"),
            ]
        )
        service = HistoryService(MagicMock(), tdengine)
        service._resolve_current_tdengine_database = MagicMock(
            return_value="device_data"
        )

        with self.assertRaises(ApiError) as context:
            await service._raise_if_tdengine_unavailable(
                "chart_data",
                "qc100",
            )

        self.assertEqual(context.exception.code, 503)
        self.assertIn("许可证已过期", context.exception.message)
        self.assertEqual(tdengine.fetch_all.await_count, 2)
        tdengine.qualify_table.assert_called_once_with("device_data", "sqqc")
        self.assertEqual(
            tdengine.fetch_all.await_args_list[1].args[0],
            "SELECT ts FROM `device_data`.`sqqc` LIMIT 1",
        )

    async def test_authentication_failure_identifies_backend_credentials(self):
        tdengine = MagicMock()
        tdengine.fetch_all = AsyncMock(
            side_effect=RuntimeError("[0x0357]: Authentication failure")
        )
        service = HistoryService(MagicMock(), tdengine)
        service._resolve_current_tdengine_database = MagicMock(
            return_value="device_data"
        )

        with self.assertRaises(ApiError) as context:
            await service._raise_if_tdengine_unavailable("chart_data", "qc100")

        self.assertEqual(context.exception.code, 503)
        self.assertIn("TDENGINE_USER/TDENGINE_PASSWORD", context.exception.message)
        self.assertEqual(tdengine.fetch_all.await_count, 1)

    async def test_empty_database_is_not_reported_as_unavailable(self):
        tdengine = MagicMock()
        tdengine.fetch_all = AsyncMock(return_value=[])
        service = HistoryService(MagicMock(), tdengine)
