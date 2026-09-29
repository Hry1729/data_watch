from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest

from app.core.exceptions import ApiError
from app.services.catalog import CatalogService


def _count_result(total: int):
    result = MagicMock()
    result.scalar_one.return_value = total
    return result


def _page_result(rows: list[dict]):
    result = MagicMock()
    result.mappings.return_value.all.return_value = rows
    return result


@pytest.mark.asyncio
async def test_point_list_filters_and_pages_in_mysql_before_latest_values() -> None:
    session = AsyncMock()
    session.execute.side_effect = [
        _count_result(37),
        _page_result(
            [
                {
                    "point_id": 21,
                    "device_id": 8,
                    "device_code": "QC08",
                    "device_name": "QC08",
                    "point_name": "温度",
                    "io_address": "D0",
                    "data_type": "REAL",
                    "description": "",
                    "driver_name": "modbus",
                    "create_time": None,
                    "update_time": None,
                    "var_seq": 1,
                    "scan_rate": 1000,
                    "is_alarm": 0,
                    "is_storage": 1,
                    "is_readonly": 1,
                    "group_id": 2,
                    "td_column_name": "temp",
                    "enabled": 1,
                }
            ]
        ),
    ]
    service = CatalogService(session=session, tdengine=MagicMock())
    service._fetch_latest_point_value_map = AsyncMock(return_value={})

    page = await service.list_points(
        {
            "pageNo": "2",
            "pageSize": "20",
            "keyword": "温度",
            "deviceIds": ["8", "9"],
        }
    )

    assert page["total"] == 37
    assert len(page["records"]) == 1
    assert page["records"][0]["id"] == 21
    assert session.execute.await_count == 2
    count_sql, count_params = session.execute.await_args_list[0].args
    page_sql, page_params = session.execute.await_args_list[1].args
    assert "COUNT(*)" in str(count_sql)
    assert "device_id IN" in str(count_sql)
    assert "LIMIT :page_limit OFFSET :page_offset" in str(page_sql)
    assert count_params["keyword"] == "温度"
    assert count_params["device_ids"] == [8, 9]
    assert page_params["page_limit"] == 20
    assert page_params["page_offset"] == 20
    service._fetch_latest_point_value_map.assert_not_awaited()


@pytest.mark.asyncio
async def test_point_latest_reads_requested_ids_in_one_redis_batch(monkeypatch) -> None:
    session = AsyncMock()
    session.execute.return_value = _page_result(
        [
            {"point_id": 21, "device_code": "QC08", "group_code": "g001", "data_type": "REAL"},
            {"point_id": 22, "device_code": "QC08", "group_code": "g001", "data_type": "BOOL"},
        ]
    )
    pipeline = MagicMock()
    pipeline.execute = AsyncMock(return_value=[[b'{"v":"12.5","t":1700000000000}', None]])
    redis = MagicMock()
    redis.pipeline.return_value = pipeline
    monkeypatch.setattr("app.services.catalog.get_redis_client", AsyncMock(return_value=redis))
    service = CatalogService(session=session, tdengine=MagicMock())

    values = await service.get_point_latest_values(["21", "22"])

    assert values["21"]["currentValue"] == 12.5
    assert values["21"]["updateTime"]
    assert "22" not in values
    pipeline.hmget.assert_called_once_with("rt:latest:QC08:g001", ["21", "22"])
    pipeline.execute.assert_awaited_once()


@pytest.mark.asyncio
async def test_point_latest_rejects_missing_or_oversized_id_batches() -> None:
    session = AsyncMock()
    session.execute.return_value = _page_result(
        [{"point_id": 21, "device_code": "QC08", "group_code": "g001", "data_type": "REAL"}]
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    with pytest.raises(ApiError) as missing:
        await service.get_point_latest_values([21, 22])
    assert missing.value.code == 404
    assert missing.value.status_code == 404

    with pytest.raises(ApiError) as oversized:
        await service.get_point_latest_values(list(range(1, 102)))
    assert oversized.value.code == 400
    assert oversized.value.status_code == 400
    assert session.execute.await_count == 1


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("redis_error", "expected_status"),
    [(ConnectionError("unavailable"), 503), (TimeoutError(), 504)],
)
async def test_point_latest_reports_redis_failure_with_http_status(
    monkeypatch, redis_error: Exception, expected_status: int,
) -> None:
    session = AsyncMock()
    session.execute.return_value = _page_result(
        [{"point_id": 21, "device_code": "QC08", "group_code": "g001", "data_type": "REAL"}]
    )
    monkeypatch.setattr(
        "app.services.catalog.get_redis_client", AsyncMock(side_effect=redis_error)
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    with pytest.raises(ApiError) as error:
        await service.get_point_latest_values(["21"])
    assert error.value.code == expected_status
    assert error.value.status_code == expected_status


@pytest.mark.asyncio
async def test_point_latest_permission_error_uses_http_status(monkeypatch) -> None:
    from app.api.routes import catalog as routes

    monkeypatch.setattr(
        routes,
        "require_permissions",
        AsyncMock(side_effect=ApiError(403, "无权限访问该接口")),
    )

    with pytest.raises(ApiError) as error:
        await routes.point_latest(pointIds=["21"], credentials=None)
    assert error.value.code == 403
    assert error.value.status_code == 403
