from datetime import datetime, timedelta
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock
import pytest
from app.services import history

@pytest.mark.asyncio
async def test_export_source_failure_cannot_be_completed(tmp_path, monkeypatch):
    service = history.HistoryService.__new__(history.HistoryService)
    service._record_budget = lambda **kw: 100
    service._fetch_point_series = AsyncMock(side_effect=ConnectionError("database down"))
    manager = MagicMock()
    monkeypatch.setattr(history, "get_export_task_manager", lambda: manager)
    monkeypatch.setattr(history, "get_repo_root", lambda: tmp_path)
    start = datetime(2026, 1, 1)
    await service._process_export_task("test_export", SimpleNamespace(device_code="QC"),
        [SimpleNamespace(point_name="point", var_seq=1)], start, start + timedelta(hours=1))
    assert any(call.kwargs.get("status") == "failed" for call in manager.update_task.call_args_list)
    assert not any(call.kwargs.get("status") == "completed" for call in manager.update_task.call_args_list)
    assert not (tmp_path / "data/exports/test_export.csv").exists()


@pytest.mark.asyncio
@pytest.mark.parametrize("failure", ["all", "after_data", None])
async def test_export_uses_real_point_query_failure_path(tmp_path, monkeypatch, failure):
    start = datetime(2026, 1, 1)
    tdengine = MagicMock(database="device_data")
    tdengine.qualify_table.side_effect = lambda db, table: f"`{db}`.`{table}`"
    tdengine.quote_identifier.side_effect = lambda name: f"`{name}`"
    if failure == "all":
        tdengine.fetch_all = AsyncMock(side_effect=ConnectionError("database down"))
    elif failure == "after_data":
        calls = 0
        async def fetch(*args, **kwargs):
            nonlocal calls
            calls += 1
            if calls == 1:
                return [(start, 1)]
            raise ConnectionError("database down")
        tdengine.fetch_all = fetch
    else:
        tdengine.fetch_all = AsyncMock(return_value=[])
    service = history.HistoryService(MagicMock(), tdengine,
        settings=SimpleNamespace(export_max_records=100))
    manager = MagicMock()
    monkeypatch.setattr(history, "get_export_task_manager", lambda: manager)
    monkeypatch.setattr(history, "get_repo_root", lambda: tmp_path)
    await service._process_export_task("real_query", history.HistoryDevice(1, "QC", "Device", "qc"),
        [history.HistoryPoint(1, "point", "a", "REAL", 1, None, td_column_name="v1")],
        start, start + timedelta(hours=2))
    statuses = [call.kwargs.get("status") for call in manager.update_task.call_args_list]
    if failure:
        assert "failed" in statuses and "completed" not in statuses
        assert not (tmp_path / "data/exports/real_query.csv").exists()
    else:
        assert "completed" in statuses and "failed" not in statuses
        assert (tmp_path / "data/exports/real_query.csv").exists()
