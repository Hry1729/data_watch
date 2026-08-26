"""Database-side history/export result budget tests."""

from __future__ import annotations

import os
import sys
import unittest
from datetime import datetime, timedelta
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.core.exceptions import ApiError
from app.services.history import HistoryDevice, HistoryPoint, HistoryService


class TestHistoryLimits(unittest.IsolatedAsyncioTestCase):
    def _service(self) -> HistoryService:
        service = HistoryService(MagicMock(), MagicMock())
        service.settings = SimpleNamespace(
            perspective_max_records_per_query=5,
            export_max_records=7,
        )
        return service

    @staticmethod
    def _points() -> list[HistoryPoint]:
        return [
            HistoryPoint(1, "p1", "a1", "REAL", 1, None),
            HistoryPoint(2, "p2", "a2", "REAL", 2, None),
        ]

    async def test_query_uses_remaining_budget_not_fixed_per_point_split(self):
        service = self._service()
        points = self._points()
        device = HistoryDevice(1, "dev001", "Device", "qc")
        service._fetch_device = AsyncMock(return_value=device)
        service._fetch_selected_points = AsyncMock(return_value=points)
        service._validate_query_points = MagicMock()
        service._validate_query_range = MagicMock(
            return_value=(datetime(2026, 1, 1), datetime(2026, 1, 2))
        )
        service._format_timestamp = MagicMock(side_effect=lambda value: value.isoformat())
        calls: list[tuple[int, int]] = []
        first_rows = [
            (datetime(2026, 1, 1) + timedelta(minutes=index), index)
            for index in range(5)
        ]

        async def fetch(_device, point, _start, _end, *, limit):
            calls.append((point.var_seq, limit))
            return first_rows if point.var_seq == 1 else []

        service._fetch_point_series = fetch
        result = await service.query_history(
            1,
            [1, 2],
            "2026-01-01",
            "2026-01-02",
        )

        self.assertEqual(calls, [(1, 6), (2, 1)])
        self.assertEqual(len(result["times"]), 5)

    async def test_query_rejects_sentinel_row_before_full_materialization(self):
        service = self._service()
        points = self._points()
        service._fetch_device = AsyncMock(
            return_value=HistoryDevice(1, "dev001", "Device", "qc")
        )
        service._fetch_selected_points = AsyncMock(return_value=points)
        service._validate_query_points = MagicMock()
        service._validate_query_range = MagicMock(
            return_value=(datetime(2026, 1, 1), datetime(2026, 1, 2))
        )
        rows = [(datetime(2026, 1, 1) + timedelta(minutes=i), i) for i in range(6)]
        service._fetch_point_series = AsyncMock(return_value=rows)

        with self.assertRaises(ApiError) as context:
            await service.query_history(1, [1, 2], "2026-01-01", "2026-01-02")

        self.assertEqual(context.exception.code, 400)
        self.assertIn("5 records", context.exception.message)
        self.assertEqual(service._fetch_point_series.await_args.kwargs["limit"], 6)

    async def test_chart_also_uses_remaining_budget(self):
        service = self._service()
        points = self._points()
        service._fetch_device = AsyncMock(
            return_value=HistoryDevice(1, "dev001", "Device", "qc")
        )
        service._fetch_selected_points = AsyncMock(return_value=points)
        service._validate_query_points = MagicMock()
        service._validate_query_range = MagicMock(
            return_value=(datetime(2026, 1, 1), datetime(2026, 1, 2))
        )
        rows = [(datetime(2026, 1, 1) + timedelta(minutes=i), i) for i in range(5)]
        calls: list[int] = []

        async def fetch(_device, _point, _start, _end, _interval, _agg, *, limit):
            calls.append(limit)
            return rows if len(calls) == 1 else []

        service._fetch_downsampled_series = fetch
        result = await service.get_chart_data(
            1,
            [1, 2],
            "2026-01-01",
            "2026-01-02",
            "1h",
            "avg",
        )

        self.assertEqual(calls, [6, 1])
        self.assertEqual(len(result["series"][0]["data"]), 5)

    def test_export_has_independent_budget(self):
        service = self._service()
        self.assertEqual(service._record_budget(export=True), 7)
        self.assertEqual(service._record_budget(), 5)

    async def test_point_query_pushes_remaining_budget_into_sql_limit(self):
        class FakeTDengine:
            database = "device_data"

            def __init__(self):
                self.queries: list[str] = []

            def qualify_table(self, database: str, table: str) -> str:
                return f"`{database}`.`{table}`"

            def quote_identifier(self, value: str) -> str:
                return f"`{value}`"

            async def fetch_all(self, query: str, *, database: str):
                self.queries.append(query)
                return []

        tdengine = FakeTDengine()
        service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace())
        await service._fetch_point_series(
            HistoryDevice(1, "dev001", "Device", "qc"),
            HistoryPoint(1, "p1", "a1", "REAL", 1, None, td_column_name="v1"),
            datetime(2026, 1, 1),
            datetime(2026, 1, 2),
            limit=6,
        )

        self.assertTrue(tdengine.queries)
        self.assertIn("ORDER BY ts ASC LIMIT 6", tdengine.queries[0])


if __name__ == "__main__":
    unittest.main()
