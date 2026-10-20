"""审查批次3回归：history / dashboard / rate_sampler（B030-B036）。"""

from __future__ import annotations

import re
from datetime import datetime, timedelta
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock
from urllib.parse import parse_qsl

import pytest

from app.core.exceptions import ApiError
from app.services import history
from app.services.history import HistoryDevice, HistoryPoint, HistoryService


def _td_mock():
    tdengine = MagicMock(database="device_data")
    tdengine.qualify_table.side_effect = lambda db, table: f"`{db}`.`{table}`"
    tdengine.quote_identifier.side_effect = lambda name: f"`{name}`"
    return tdengine


# ---------------------------------------------------------------- B030
@pytest.mark.asyncio
@pytest.mark.parametrize("hours", [2, 24])
async def test_b030_chunked_export_does_not_duplicate_boundary_samples(tmp_path, monkeypatch, hours):
    start = datetime(2026, 1, 1)
    end = start + timedelta(hours=hours)
    # 每个整点边界都有样本，另加块内样本
    samples = [start + timedelta(hours=h) for h in range(hours + 1)]
    samples += [start + timedelta(hours=h, minutes=30) for h in range(hours)]
    tdengine = _td_mock()
    service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace(export_max_records=10_000))
    pattern = re.compile(r"ts >= (\d+) AND ts (<=|<) (\d+)")

    async def fetch(sql, **_kwargs):
        lo, op, hi = pattern.search(sql).groups()
        lo, hi = int(lo), int(hi)
        rows = []
        for ts in samples:
            ms = int(service._format_sql_timestamp(ts))
            if ms >= lo and (ms <= hi if op == "<=" else ms < hi):
                rows.append((ts, 1.0))
        return rows

    tdengine.fetch_all = fetch
    manager = MagicMock()
    monkeypatch.setattr(history, "get_export_task_manager", lambda: manager)
    monkeypatch.setattr(history, "get_repo_root", lambda: tmp_path)
    await service._process_export_task(
        "dup", HistoryDevice(1, "QC", "Device", "qc"),
        [HistoryPoint(1, "point", "a", "REAL", 1, None, td_column_name="v1")], start, end,
    )
    statuses = [call.kwargs.get("status") for call in manager.update_task.call_args_list]
    assert "completed" in statuses
    lines = (tmp_path / "data/exports/dup.csv").read_text(encoding="utf-8-sig").splitlines()[1:]
    times = [line.split(",")[0] for line in lines]
    assert len(times) == len(set(times)) == len(samples)


# ---------------------------------------------------------------- B031
@pytest.mark.asyncio
@pytest.mark.parametrize("var_type", ["INT", "DINT", "WORD", "DWORD"])
async def test_b031_avg_on_integer_points_keeps_fraction(var_type):
    service = HistoryService(MagicMock(), MagicMock())
    service.settings = SimpleNamespace(perspective_max_records_per_query=100)
    service._fetch_device = AsyncMock(return_value=HistoryDevice(1, "dev", "Device", "qc"))
    points = [HistoryPoint(1, "p1", "a1", var_type, 1, None), HistoryPoint(2, "p2", "a2", var_type, 2, None)]
    service._fetch_selected_points = AsyncMock(return_value=points)
    service._validate_query_points = MagicMock()
    service._validate_query_range = MagicMock(return_value=(datetime(2026, 1, 1), datetime(2026, 1, 2)))

    async def fetch(_device, point, *_args, limit):
        return [(datetime(2026, 1, 1), 1.5 if point.var_seq == 1 else -1.5)]

    service._fetch_downsampled_series = fetch
    result = await service.get_chart_data(1, [1, 2], "2026-01-01", "2026-01-02", aggregate="avg")
    assert [s["data"][0][1] for s in result["series"]] == [1.5, -1.5]

    # last 仍保持原始整数点位契约
    result = await service.get_chart_data(1, [1, 2], "2026-01-01", "2026-01-02", aggregate="last")
    assert [s["data"][0][1] for s in result["series"]] == [1, -1]


# ---------------------------------------------------------------- B032
@pytest.mark.asyncio
async def test_b032_downsampled_series_propagates_timeout():
    tdengine = _td_mock()
    tdengine.fetch_all = AsyncMock(side_effect=TimeoutError("query timed out"))
    service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace())
    service._raise_if_tdengine_unavailable = AsyncMock()
    with pytest.raises(ApiError) as exc:
        await service._fetch_downsampled_series(
            HistoryDevice(1, "QC", "Device", "qc"),
            HistoryPoint(1, "p", "a", "REAL", 1, None, td_column_name="v1"),
            datetime(2026, 1, 1), datetime(2026, 1, 2), "1m", "AVG",
        )
    assert exc.value.status_code == 503


@pytest.mark.asyncio
async def test_b032_downsampled_series_missing_schema_is_empty_when_db_healthy():
    tdengine = _td_mock()
    tdengine.fetch_all = AsyncMock(side_effect=RuntimeError("Table does not exist"))
    service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace())
    service._raise_if_tdengine_unavailable = AsyncMock()
    rows = await service._fetch_downsampled_series(
        HistoryDevice(1, "QC", "Device", "qc"),
        HistoryPoint(1, "p", "a", "REAL", 1, None, td_column_name="v1"),
        datetime(2026, 1, 1), datetime(2026, 1, 2), "1m", "AVG",
    )
    assert rows == []
    service._raise_if_tdengine_unavailable.assert_awaited()


@pytest.mark.asyncio
async def test_b032_bucket_data_propagates_permission_error():
    tdengine = _td_mock()
    tdengine.fetch_all = AsyncMock(side_effect=RuntimeError("permission denied"))
    service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace())
    service._raise_if_tdengine_unavailable = AsyncMock()
    with pytest.raises(ApiError):
        await service._fetch_point_bucket_data(
            HistoryDevice(1, "QC", "Device", "qc"),
            HistoryPoint(1, "p", "a", "REAL", 1, None, td_column_name="v1"),
            datetime(2026, 1, 1), datetime(2026, 1, 1, 1), "1s", 10,
        )


def _matrix_service(bucket_fetch):
    service = HistoryService(MagicMock(), MagicMock(), settings=SimpleNamespace())
    service._fetch_device = AsyncMock(return_value=HistoryDevice(1, "dev", "Device", "qc"))
    service._fetch_selected_points = AsyncMock(return_value=[
        HistoryPoint(1, "p1", "a1", "REAL", 1, None),
        HistoryPoint(2, "p2", "a2", "REAL", 2, None),
    ])
    service._validate_query_points = MagicMock()
    service._raise_if_tdengine_unavailable = AsyncMock()
    service._fetch_point_bucket_data = bucket_fetch
    return service


@pytest.mark.asyncio
async def test_b032_raw_matrix_one_point_failure_is_not_success():
    start = datetime(2026, 1, 1)

    async def fetch(_device, point, *_args):
        if point.var_seq == 2:
            raise ApiError(503, "History data source unavailable", status_code=503)
        return {start: 1.0}

    service = _matrix_service(fetch)
    service._validate_query_range = MagicMock(return_value=(start, start + timedelta(seconds=2)))
    with pytest.raises(ApiError) as exc:
        await service.get_raw_matrix_data(1, [1, 2], "a", "b")
    assert exc.value.status_code == 503


@pytest.mark.asyncio
async def test_b032_raw_matrix_all_empty_with_healthy_db_returns_empty():
    start = datetime(2026, 1, 1)
    service = _matrix_service(AsyncMock(return_value={}))
    service._validate_query_range = MagicMock(return_value=(start, start + timedelta(seconds=2)))
    result = await service.get_raw_matrix_data(1, [1, 2], "a", "b")
    assert result["rows"] == []


# ---------------------------------------------------------------- B034
@pytest.mark.asyncio
@pytest.mark.parametrize("granularity,step_ms", [("100ms", 100), ("500ms", 500), ("1s", 1000)])
async def test_b034_paging_keeps_bucket_at_exact_end_time(granularity, step_ms):
    start = datetime(2026, 1, 1)
    end = start + timedelta(milliseconds=2 * step_ms)
    buckets = {start + timedelta(milliseconds=i * step_ms): float(i) for i in range(3)}

    async def fetch(_device, point, lo, hi, _gran, limit):
        selected = {ts: v for ts, v in sorted(buckets.items()) if lo <= ts <= hi}
        return dict(list(selected.items())[:limit])

    service = _matrix_service(fetch)
    service._validate_query_range = MagicMock(return_value=(start, end))
    full = await service.get_raw_matrix_data(1, [1, 2], "a", "b", align_granularity=granularity, page_size=10)
    page1 = await service.get_raw_matrix_data(1, [1, 2], "a", "b", align_granularity=granularity, page_size=2)
    assert page1["nextCursor"]
    page2 = await service.get_raw_matrix_data(
        1, [1, 2], "a", "b", align_granularity=granularity, page_size=2,
        cursor_ts=page1["nextCursor"],
    )
    assert page1["rows"] + page2["rows"] == full["rows"]
    assert len(full["rows"]) == 3


# ---------------------------------------------------------------- B033
def _query_request(query: str):
    from app.main import CompatQueryParams

    return SimpleNamespace(query_params=CompatQueryParams(parse_qsl(query)))


@pytest.mark.parametrize("count", [6, 10])
def test_b033_axios_array_params_are_counted_once(count):
    from app.api.routes.history import _extract_var_seqs

    query = "&".join(f"varSeqs[]={i}" for i in range(1, count + 1))
    assert _extract_var_seqs(_query_request(query)) == list(range(1, count + 1))


def test_b033_mixed_alias_duplicates_count_once_and_eleven_distinct_still_counted():
    from app.api.routes.history import _extract_var_seqs

    assert _extract_var_seqs(_query_request("varSeqs=1&varSeqs[]=1&varSeqs[]=2")) == [1, 2]
    query = "&".join(f"varSeqs[]={i}" for i in range(1, 12))
    assert len(_extract_var_seqs(_query_request(query))) == 11


# ---------------------------------------------------------------- B035
def _dashboard(fact_tables, execute=None):
    from app.services.dashboard import DashboardService

    session = MagicMock()
    session.execute = execute or AsyncMock()
    service = DashboardService(session, MagicMock())
    service._get_fact_tables = AsyncMock(return_value=set(fact_tables))
    return service


@pytest.mark.asyncio
async def test_b035_missing_alarm_table_returns_zero_not_fake(monkeypatch):
    monkeypatch.delenv("DASHBOARD_SYNTHETIC_ALARMS", raising=False)
    service = _dashboard(set())
    dist = await service.get_alarm_distribution({})
    assert dist["totalAlarmCount"] == 0 and dist["groups"] == []
    events = await service.get_recent_events({"eventType": "all"})
    assert events == {"list": []}


@pytest.mark.asyncio
async def test_b035_empty_alarm_table_returns_zero():
    rows_result = MagicMock()
    rows_result.mappings.return_value.all.return_value = []
    count_result = MagicMock()
    count_result.scalar_one.return_value = 0
    execute = AsyncMock(side_effect=[rows_result, count_result, rows_result])
    service = _dashboard({"alarm_record"}, execute)
    dist = await service.get_alarm_distribution({})
    assert dist["totalAlarmCount"] == 0
    events = await service.get_recent_events({"eventType": "alarm"})
    assert events == {"list": []}


@pytest.mark.asyncio
async def test_b035_alarm_sql_failure_is_error_not_fake():
    service = _dashboard({"alarm_record"}, AsyncMock(side_effect=RuntimeError("db down")))
    with pytest.raises(ApiError) as exc:
        await service.get_alarm_distribution({})
    assert exc.value.status_code == 503
    with pytest.raises(ApiError):
        await service.get_recent_events({"eventType": "alarm"})


@pytest.mark.asyncio
async def test_b035_synthetic_alarms_require_explicit_dev_flag(monkeypatch):
    service = _dashboard(set())
    monkeypatch.setenv("DASHBOARD_SYNTHETIC_ALARMS", "1")
    monkeypatch.setenv("BACKEND_ENV", "production")
    assert (await service.get_recent_events({}))["list"] == []
    monkeypatch.setenv("BACKEND_ENV", "development")
    events = (await service.get_recent_events({}))["list"]
    assert events and all("联调假数据" in item["content"] for item in events)


# ---------------------------------------------------------------- B036
@pytest.mark.asyncio
async def test_b036_writer_landed_rate_no_spike_after_failed_read(monkeypatch):
    from app.services import rate_sampler as mod

    sampler = mod.RateSampler()
    clock = iter([0.0, 10.0, 20.0, 30.0])
    totals = iter([100, None, 120, 130])
    monkeypatch.setattr(mod, "time", SimpleNamespace(monotonic=lambda: next(clock)))
    sampler._read_cumulative = AsyncMock(return_value=({}, {}))

    async def counters():
        return None, next(totals)

    sampler._read_metrics_counters = counters
    for _ in range(4):
        await sampler.sample()
    values = [v for _t, v in sampler._writer_landed_points]
    # 真实速率 1 row/s = 60 rows/min；失败恢复轮不得出现 120 的假尖峰
    assert values == [60, 60]


@pytest.mark.asyncio
async def test_b036_collector_point_window_uses_real_60s_span(monkeypatch):
    from app.services import rate_sampler as mod

    sampler = mod.RateSampler()
    # 每 10s 采样，真实 60 点/min（1 点/s）；t=30 读取失败
    times = [float(t) for t in range(0, 130, 10)]
    clock = iter(times)
    monkeypatch.setattr(mod, "time", SimpleNamespace(monotonic=lambda: next(clock)))
    sampler._read_cumulative = AsyncMock(return_value=({}, {}))
    readings = iter([None if t == 30 else int(t) for t in times])

    async def counters():
        return next(readings), None

    sampler._read_metrics_counters = counters
    for _ in times:
        await sampler.sample()
    values = [v for _t, v in sampler._collector_point_points]
    assert values and all(v == 60 for v in values)
