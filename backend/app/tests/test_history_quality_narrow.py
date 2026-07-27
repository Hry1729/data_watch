"""历史 API 质量窄表读取与 legacy 兼容测试（Q1）。"""

from __future__ import annotations

import os
import sys
import unittest
from datetime import datetime
from unittest.mock import AsyncMock, MagicMock

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services.history import HistoryDevice, HistoryPoint, HistoryService


class TestHistoryQualityNarrow(unittest.IsolatedAsyncioTestCase):
    def _service(self) -> HistoryService:
        tdengine = MagicMock()
        tdengine.qualify_table = MagicMock(side_effect=lambda db, name: f"{db}.{name}")
        tdengine.quote_identifier = MagicMock(side_effect=lambda name: f"`{name}`")
        tdengine.database = "device_data"
        return HistoryService(MagicMock(), tdengine)

    async def test_legacy_raw_data_quality_is_null(self):
        """legacy 子表路径：不查窄表，quality 仍为 null。"""
        service = self._service()
        device = HistoryDevice(1, "dev001", "Device", "qc")
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
        service._resolve_current_tdengine_database = MagicMock(return_value="device_data")
        service._resolve_tdengine_database = MagicMock(return_value="legacy_db")
        service._format_sql_timestamp = MagicMock(return_value="1000")
        service._resolve_point_subtable_name = MagicMock(return_value="sub")
        service._normalize_identifier = MagicMock(return_value="dev001")
        service._coerce_datetime = MagicMock(return_value=datetime(2024, 1, 1, 12, 0, 0))
        service._format_timestamp = MagicMock(return_value="2024-01-01 12:00:00.000")
        service._normalize_point_value = MagicMock(return_value=1.5)
        service._raise_if_tdengine_unavailable = AsyncMock()
        service.tdengine.fetch_all = AsyncMock(
            return_value=[(datetime(2024, 1, 1, 12, 0, 0), 1.5)]
        )

        result = await service.get_raw_data(1, 1, "2024-01-01", "2024-01-02")
        self.assertEqual(len(result["list"]), 1)
        self.assertIsNone(result["list"][0]["quality"])
        # legacy 路径不应查询 *_q
        for call in service.tdengine.fetch_all.await_args_list:
            sql = call.args[0]
            self.assertNotIn("_q", sql)

    async def test_current_schema_reads_quality_from_narrow_table(self):
        """当前宽表有值且窄表有质量 → 返回平台质量码。"""
        service = self._service()
        device = HistoryDevice(1, "qc100", "QC100", "qc")
        point = HistoryPoint(
            point_id=101,
            point_name="p1",
            io_address="a1",
            var_type="REAL",
            var_seq=1,
            unit=None,
            td_column_name="qc1",
        )
        service._fetch_device = AsyncMock(return_value=device)
        service._fetch_selected_points = AsyncMock(return_value=[point])
        service._validate_query_points = MagicMock()
        service._validate_query_range = MagicMock(
            return_value=(datetime(2024, 1, 1), datetime(2024, 1, 2))
        )
        service._resolve_current_tdengine_database = MagicMock(return_value="device_data")
        service._resolve_tdengine_database = MagicMock(return_value="qc")
        service._format_sql_timestamp = MagicMock(return_value="1000")
        service._resolve_current_child_table_name = MagicMock(return_value="qc100")
        service._coerce_datetime = MagicMock(return_value=datetime(2024, 1, 1, 12, 0, 0))
        service._format_timestamp = MagicMock(return_value="2024-01-01 12:00:00.000")
        service._normalize_point_value = MagicMock(return_value=12.5)
        service._raise_if_tdengine_unavailable = AsyncMock()

        ts = datetime(2024, 1, 1, 12, 0, 0)

        async def _fetch(sql, database=None):
            if "`quality`" in sql and "qc100_q" in sql:
                self.assertIn("var_id = 101", sql)
                return [(ts, 2)]
            if "`qc1`" in sql:
                return [(ts, 12.5)]
            return []

        service.tdengine.fetch_all = AsyncMock(side_effect=_fetch)

        result = await service.get_raw_data(1, 1, "2024-01-01", "2024-01-02")
        self.assertEqual(result["list"][0]["value"], 12.5)
        self.assertEqual(result["list"][0]["quality"], 2)

    async def test_current_schema_missing_quality_row_returns_null(self):
        """宽表有值但窄表无对应行 → quality=null（不得臆造 Good）。"""
        service = self._service()
        device = HistoryDevice(1, "qc100", "QC100", "qc")
        point = HistoryPoint(
            point_id=101,
            point_name="p1",
            io_address="a1",
            var_type="REAL",
            var_seq=1,
            unit=None,
            td_column_name="qc1",
        )
        service._fetch_device = AsyncMock(return_value=device)
        service._fetch_selected_points = AsyncMock(return_value=[point])
        service._validate_query_points = MagicMock()
        service._validate_query_range = MagicMock(
            return_value=(datetime(2024, 1, 1), datetime(2024, 1, 2))
        )
        service._resolve_current_tdengine_database = MagicMock(return_value="device_data")
        service._resolve_tdengine_database = MagicMock(return_value="qc")
        service._format_sql_timestamp = MagicMock(return_value="1000")
        service._resolve_current_child_table_name = MagicMock(return_value="qc100")
        service._coerce_datetime = MagicMock(return_value=datetime(2024, 1, 1, 12, 0, 0))
        service._format_timestamp = MagicMock(return_value="2024-01-01 12:00:00.000")
        service._normalize_point_value = MagicMock(return_value=12.5)
        service._raise_if_tdengine_unavailable = AsyncMock()

        ts = datetime(2024, 1, 1, 12, 0, 0)

        async def _fetch(sql, database=None):
            if "`quality`" in sql:
                return []
            return [(ts, 12.5)]

        service.tdengine.fetch_all = AsyncMock(side_effect=_fetch)
        result = await service.get_raw_data(1, 1, "2024-01-01", "2024-01-02")
        self.assertIsNone(result["list"][0]["quality"])

    async def test_quality_table_missing_still_returns_values(self):
        """质量窄表不存在时降级：仍返回值，quality=null。"""
        service = self._service()
        device = HistoryDevice(1, "qc100", "QC100", "qc")
        point = HistoryPoint(
            point_id=101,
            point_name="p1",
            io_address="a1",
            var_type="REAL",
            var_seq=1,
            unit=None,
            td_column_name="qc1",
        )
        service._fetch_device = AsyncMock(return_value=device)
        service._fetch_selected_points = AsyncMock(return_value=[point])
        service._validate_query_points = MagicMock()
        service._validate_query_range = MagicMock(
            return_value=(datetime(2024, 1, 1), datetime(2024, 1, 2))
        )
        service._resolve_current_tdengine_database = MagicMock(return_value="device_data")
        service._resolve_tdengine_database = MagicMock(return_value="qc")
        service._format_sql_timestamp = MagicMock(return_value="1000")
        service._resolve_current_child_table_name = MagicMock(return_value="qc100")
        service._coerce_datetime = MagicMock(return_value=datetime(2024, 1, 1, 12, 0, 0))
        service._format_timestamp = MagicMock(return_value="2024-01-01 12:00:00.000")
        service._normalize_point_value = MagicMock(return_value=1.0)
        service._raise_if_tdengine_unavailable = AsyncMock()

        ts = datetime(2024, 1, 1, 12, 0, 0)

        async def _fetch(sql, database=None):
            if "`quality`" in sql:
                raise RuntimeError("Table does not exist")
            return [(ts, 1.0)]

        service.tdengine.fetch_all = AsyncMock(side_effect=_fetch)
        result = await service.get_raw_data(1, 1, "2024-01-01", "2024-01-02")
        self.assertEqual(result["list"][0]["value"], 1.0)
        self.assertIsNone(result["list"][0]["quality"])

    async def test_quality_query_db_fault_raises_api_error(self):
        """质量窄表非缺表故障：不得伪装成 null，应上抛 ApiError。"""
        from app.core.exceptions import ApiError

        service = self._service()
        device = HistoryDevice(1, "qc100", "QC100", "qc")
        point = HistoryPoint(
            point_id=101,
            point_name="p1",
            io_address="a1",
            var_type="REAL",
            var_seq=1,
            unit=None,
            td_column_name="qc1",
        )
        service._fetch_device = AsyncMock(return_value=device)
        service._fetch_selected_points = AsyncMock(return_value=[point])
        service._validate_query_points = MagicMock()
        service._validate_query_range = MagicMock(
            return_value=(datetime(2024, 1, 1), datetime(2024, 1, 2))
        )
        service._resolve_current_tdengine_database = MagicMock(return_value="device_data")
        service._resolve_tdengine_database = MagicMock(return_value="qc")
        service._format_sql_timestamp = MagicMock(return_value="1000")
        service._resolve_current_child_table_name = MagicMock(return_value="qc100")
        service._coerce_datetime = MagicMock(return_value=datetime(2024, 1, 1, 12, 0, 0))
        service._format_timestamp = MagicMock(return_value="2024-01-01 12:00:00.000")
        service._normalize_point_value = MagicMock(return_value=1.0)
        service._raise_if_tdengine_unavailable = AsyncMock()

        ts = datetime(2024, 1, 1, 12, 0, 0)

        async def _fetch(sql, database=None):
            if "`quality`" in sql:
                raise RuntimeError("connection refused")
            return [(ts, 1.0)]

        service.tdengine.fetch_all = AsyncMock(side_effect=_fetch)
        with self.assertRaises(ApiError) as ctx:
            await service.get_raw_data(1, 1, "2024-01-01", "2024-01-02")
        self.assertEqual(ctx.exception.code, 503)
        self.assertIn("质量", ctx.exception.message)