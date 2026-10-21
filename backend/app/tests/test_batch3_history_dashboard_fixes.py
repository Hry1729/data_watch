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


# ---------------------------------------------------------------- B032 R2：错误分类收紧
class _TaospyProgrammingError(Exception):
    """与 taospy taos/error.py 的 Error 同构（导入 taos 需要 libtaos.so，CI 上没有）。"""

    def __init__(self, msg=None, errno=0xFFFF):
        self.msg = msg
        self.errno = errno
        self._full_msg = "[0x%04x]: %s" % (self.errno & 0xFFFF, self.msg)

    def __str__(self):
        return self._full_msg


def _taospy_error(msg, code):
    return _TaospyProgrammingError(msg, code)


def _taosrest_error(msg, code):
    from taosrest.errors import ConnectError

    return ConnectError(msg, code)


def _sqlalchemy_wrapped(message, sql="SELECT ts, `v1` FROM `device_data`.`qc_table` WHERE ts >= 0"):
    from sqlalchemy.exc import DBAPIError

    class QueryError(Exception):  # 模拟 taosws.QueryError
        pass

    return DBAPIError(sql, None, QueryError(message))


SCHEMA_MISSING_ERRORS = [
    # taosws（WebSocket / SQLAlchemy 池）原文格式
    RuntimeError("[0x2603] Internal error: `Table does not exist`"),
    RuntimeError("[0x0388] Internal error: `Database not exist`"),  # 仓库 evidence/ 里的真实报错
    RuntimeError("[0x0362] Internal error: `STable does not exist`"),
    RuntimeError("[0x2602] Internal error: `Invalid column name: v99`"),
    # taospy 原生 / taosrest：errno 属性 + "[0x....]: msg"
    "taospy:0x80002603:Table does not exist",
    "taospy:-2147473917:Table does not exist",  # 0x80002603 的有符号形式
    "taosrest:9731:Table does not exist",
    "taosrest:904:Database does not exist",
    "taosrest:1318:Table column does not exist",  # 0x0526
    # SQLAlchemy 包装（str 里带 SQL 文本）
    "sqlalchemy:[0x2603] Internal error: `Table does not exist`",
    # 0x2662 只在原因是缺表时算缺表
    RuntimeError("[0x2662] Failed to get table info, error: Table does not exist"),
    # 无错误码时的精确原文
    RuntimeError("Table does not exist"),
    RuntimeError("Database does not exist"),
]

NOT_MISSING_ERRORS = [
    TimeoutError("TDengine query queue timed out"),
    # 近似误判：含 table / not found / does not exist 但不是缺表
    RuntimeError("Query timeout while scanning table device_data.qc: vnode not found in time"),
    RuntimeError("timeout: table scan did not finish, table does not exist check skipped"),
    RuntimeError("[0x0200] Query timeout: table does not exist check skipped"),
    RuntimeError("permission denied for table device_data.qc"),
    RuntimeError("[0x000B] Internal error: `Unable to establish connection`"),
    RuntimeError("[0x0357]: Authentication failure"),
    RuntimeError("[0x2662] Failed to get table info, error: Unable to establish connection"),
    RuntimeError("[0x2616] Internal error: `Database not specified`"),
    RuntimeError("Connection refused: table service not found"),
    FileNotFoundError(2, "No such file or directory: 'table'"),
    "taosrest:11:Unable to establish connection",
    "taospy:0x8000000B:Unable to establish connection",
    "sqlalchemy:[0x0200] Internal error: `Query timeout`",
]


def _build_error(spec):
    if isinstance(spec, BaseException):
        return spec
    kind, rest = spec.split(":", 1)
    if kind == "sqlalchemy":
        return _sqlalchemy_wrapped(rest)
    code, message = rest.split(":", 1)
    code_int = int(code, 0)
    return (_taospy_error if kind == "taospy" else _taosrest_error)(message, code_int)


@pytest.mark.parametrize("spec", SCHEMA_MISSING_ERRORS, ids=str)
def test_b032_r2_schema_missing_errors_are_recognised(spec):
    assert HistoryService._is_point_schema_missing_error(_build_error(spec)) is True


@pytest.mark.parametrize("spec", NOT_MISSING_ERRORS, ids=str)
def test_b032_r2_near_miss_errors_are_not_schema_missing(spec):
    exc = _build_error(spec)
    assert HistoryService._is_point_schema_missing_error(exc) is False
    assert HistoryService._is_quality_table_missing_error(exc) is False


def test_b032_r2_quality_table_missing_only_accepts_table_errors():
    assert HistoryService._is_quality_table_missing_error(
        RuntimeError("[0x2603] Internal error: `Table does not exist`")
    ) is True
    # 列/库不存在不是"质量窄表不存在"
    assert HistoryService._is_quality_table_missing_error(
        RuntimeError("[0x2602] Internal error: `Invalid column name: quality`")
    ) is False


NEAR_MISS_TIMEOUT = RuntimeError("Query timeout while scanning table device_data.qc: vnode not found in time")


@pytest.mark.asyncio
async def test_b032_r2_downsampled_series_near_miss_timeout_is_503():
    tdengine = _td_mock()
    tdengine.fetch_all = AsyncMock(side_effect=NEAR_MISS_TIMEOUT)
    service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace())
    service._raise_if_tdengine_unavailable = AsyncMock()  # 库探测健康也不能吞掉
    with pytest.raises(ApiError) as exc:
        await service._fetch_downsampled_series(
            HistoryDevice(1, "QC", "Device", "qc"),
            HistoryPoint(1, "p", "a", "REAL", 1, None, td_column_name="v1"),
            datetime(2026, 1, 1), datetime(2026, 1, 2), "1m", "AVG",
        )
    assert exc.value.status_code == 503


@pytest.mark.asyncio
async def test_b032_r2_point_series_near_miss_timeout_is_503():
    tdengine = _td_mock()
    tdengine.fetch_all = AsyncMock(side_effect=NEAR_MISS_TIMEOUT)
    service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace())
    service._raise_if_tdengine_unavailable = AsyncMock()
    with pytest.raises(ApiError) as exc:
        await service._fetch_point_series(
            HistoryDevice(1, "QC", "Device", "qc"),
            HistoryPoint(1, "p", "a", "REAL", 1, None, td_column_name="v1"),
            datetime(2026, 1, 1), datetime(2026, 1, 2),
        )
    assert exc.value.status_code == 503


@pytest.mark.asyncio
async def test_b032_r2_point_series_real_table_missing_is_empty():
    tdengine = _td_mock()
    tdengine.fetch_all = AsyncMock(
        side_effect=RuntimeError("[0x2603] Internal error: `Table does not exist`")
    )
    service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace())
    service._raise_if_tdengine_unavailable = AsyncMock()
    rows = await service._fetch_point_series(
        HistoryDevice(1, "QC", "Device", "qc"),
        HistoryPoint(1, "p", "a", "REAL", 1, None, td_column_name="v1"),
        datetime(2026, 1, 1), datetime(2026, 1, 2),
    )
    assert rows == []
    service._raise_if_tdengine_unavailable.assert_awaited()


@pytest.mark.asyncio
async def test_b032_r2_quality_map_near_miss_is_503_not_null_quality():
    tdengine = _td_mock()
    tdengine.fetch_all = AsyncMock(side_effect=NEAR_MISS_TIMEOUT)
    service = HistoryService(MagicMock(), tdengine, settings=SimpleNamespace())
    with pytest.raises(ApiError) as exc:
        await service._fetch_quality_map_for_raw(
            HistoryDevice(1, "QC", "Device", "qc"),
            HistoryPoint(1, "p", "a", "REAL", 1, None, td_column_name="v1"),
            "device_data", "ts >= 0", "1", 10, True,
        )
    assert exc.value.status_code == 503


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


@pytest.fixture
def isolated_settings(monkeypatch):
    """让 get_settings() 只看本测试给的进程环境/backend/.env，并在前后清缓存。"""
    from app.core import config

    env_file: dict[str, str] = {}
    monkeypatch.setattr(config, "_load_backend_env_defaults", lambda: dict(env_file))
    monkeypatch.delenv("DASHBOARD_SYNTHETIC_ALARMS", raising=False)
    monkeypatch.delenv("BACKEND_ENV", raising=False)
    config.get_settings.cache_clear()
    yield env_file
    config.get_settings.cache_clear()


@pytest.mark.asyncio
async def test_b035_missing_alarm_table_returns_zero_not_fake(isolated_settings):
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
async def test_b035_synthetic_alarms_require_explicit_dev_flag(monkeypatch, isolated_settings):
    from app.core.config import get_settings

    service = _dashboard(set())
    monkeypatch.setenv("DASHBOARD_SYNTHETIC_ALARMS", "1")
    monkeypatch.setenv("BACKEND_ENV", "production")
    get_settings.cache_clear()
    assert (await service.get_recent_events({}))["list"] == []
    monkeypatch.setenv("BACKEND_ENV", "development")
    get_settings.cache_clear()
    events = (await service.get_recent_events({}))["list"]
    assert events and all("联调假数据" in item["content"] for item in events)


@pytest.mark.asyncio
async def test_b035_synthetic_alarm_flag_is_read_from_backend_env_file(isolated_settings):
    """Codex P2：只写在 backend/.env 里（未导出到进程环境）的开关也必须生效。"""
    from app.core.config import get_settings

    service = _dashboard(set())
    isolated_settings.update({"DASHBOARD_SYNTHETIC_ALARMS": "true", "BACKEND_ENV": "development"})
    get_settings.cache_clear()
    assert get_settings().dashboard_synthetic_alarms is True
    events = (await service.get_recent_events({}))["list"]
    assert events and all("联调假数据" in item["content"] for item in events)

    isolated_settings.update({"BACKEND_ENV": "production"})
    get_settings.cache_clear()
    assert (await service.get_recent_events({}))["list"] == []


def test_b035_synthetic_alarm_flag_defaults_off(isolated_settings):
    from app.core.config import get_settings

    assert get_settings().dashboard_synthetic_alarms is False


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
