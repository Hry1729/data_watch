"""History TDengine data-access probe tests."""

from __future__ import annotations

import os
import sys
import unittest
from unittest.mock import AsyncMock, MagicMock

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.core.exceptions import ApiError
from app.services.history import HistoryService


class TestHistoryTdengineProbe(unittest.IsolatedAsyncioTestCase):
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

    async def test_empty_database_is_not_reported_as_unavailable(self):
        tdengine = MagicMock()
        tdengine.fetch_all = AsyncMock(return_value=[])
        service = HistoryService(MagicMock(), tdengine)
