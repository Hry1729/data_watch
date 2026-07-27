"""历史 API legacy 质量字段测试。"""

import os
import sys
import unittest
from datetime import datetime
from unittest.mock import AsyncMock, MagicMock

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services.history import HistoryDevice, HistoryPoint, HistoryService


class TestHistoryLegacyQuality(unittest.IsolatedAsyncioTestCase):
    async def test_legacy_raw_data_quality_is_null(self):
        """legacy 子表/超级表路径返回 quality=null，而非固定 1。"""
        tdengine = MagicMock()
        session = MagicMock()
        service = HistoryService(session, tdengine)

        device = HistoryDevice(1, "dev001", "Device", "type1")
        point = HistoryPoint(
            point_id=1,
            point_name="p1",
            io_address="a1",
            var_type="float",
            var_seq=1,
            unit=None,
            td_column_name=None,
        )

        service._fetch_device = AsyncMock(return_value=device)
        service._fetch_selected_points = AsyncMock(return_value=[point])
        service._validate_query_points = MagicMock()
        service._validate_query_range = MagicMock(
            return_value=(datetime(2024, 1, 1), datetime(2024, 1, 2))
        )
        service._resolve_current_tdengine_database = MagicMock(return_value="db")
        service._resolve_tdengine_database = MagicMock(return_value="legacy_db")
        service._format_sql_timestamp = MagicMock(return_value="'2024-01-01'")
        service._resolve_point_subtable_name = MagicMock(return_value="sub")
        service._normalize_identifier = MagicMock(return_value="dev001")
        service._coerce_datetime = MagicMock(return_value=datetime(2024, 1, 1, 12, 0, 0))
        service._format_timestamp = MagicMock(return_value="2024-01-01 12:00:00")
        service._normalize_point_value = MagicMock(return_value=1.5)
        service._raise_if_tdengine_unavailable = AsyncMock()

        tdengine.qualify_table = MagicMock(side_effect=lambda db, name: f"{db}.{name}")
        tdengine.fetch_all = AsyncMock(return_value=[(datetime(2024, 1, 1, 12, 0, 0), 1.5)])

        result = await service.get_raw_data(1, 1, "2024-01-01", "2024-01-02")

        self.assertEqual(len(result["list"]), 1)
        self.assertIsNone(result["list"][0]["quality"])
