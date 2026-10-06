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
