from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest

from app.core.point_types import (
    CREATABLE_DATA_TYPES,
    data_type_sql_expression,
    normalize_data_type,
)
from app.services.catalog import CatalogService


@pytest.mark.parametrize(
    ("raw", "expected"),
    [
        ("BOOL", "BOOL"),
        ("BOOL", "BOOL"),
        ("Boolean", "BOOL"),
        ("BOOLEAN", "BOOL"),
        ("bool", "BOOL"),
        ("BYTE", "BYTE"),
        ("byte", "BYTE"),
        ("STRING", "STRING"),
        ("String", "STRING"),
        ("INT", "INT"),
        ("DINT", "DINT"),
        ("REAL", "REAL"),
        ("FLOAT", "REAL"),
        ("WORD", "WORD"),
        ("DWORD", "DWORD"),
        ("UDINT", "UDINT"),
        ("UINT", "UINT"),
        ("USINT", "USINT"),
        ("TIMER_MS", "TIMER_MS"),
        ("COUNTER", "COUNTER"),
    ],
)
def test_display_data_type_uses_one_caliber(raw: str, expected: str) -> None:
    """Boolean/BOOLEAN 与 BOOL 同口径；BYTE 不再被伪装成 STRING。"""
    assert normalize_data_type(raw) == expected


def test_unknown_data_type_is_not_silently_shown_as_string() -> None:
    """未识别类型原样显示，不能回落成 STRING 造成误导。"""
    assert normalize_data_type("SOMETHING") == "SOMETHING"
    assert normalize_data_type("") == "STRING"
    assert normalize_data_type(None) == "STRING"


def test_creatable_data_types_stay_unchanged() -> None:
    """显示口径放宽了，但"创建后不可修改点位物理类型"的白名单不能放宽。"""
    assert CREATABLE_DATA_TYPES == (
        "BOOL",
        "INT",
        "DINT",
        "REAL",
        "STRING",
        "WORD",
        "DWORD",
    )


def test_data_type_sql_expression_mirrors_the_display_caliber() -> None:
    expression = data_type_sql_expression("dv.var_type")
    assert "WHEN 'BOOLEAN' THEN 'BOOL'" in expression
    assert "WHEN 'FLOAT' THEN 'REAL'" in expression
    # 旧实现把所有非白名单类型 ELSE 成 STRING
    assert "ELSE 'STRING'" not in expression


def _count_result(total: int):
    result = MagicMock()
    result.scalar_one.return_value = total
    return result


def _page_result(rows: list[dict]):
    result = MagicMock()
    result.mappings.return_value.all.return_value = rows
    return result


@pytest.mark.asyncio
@pytest.mark.parametrize("data_type", ["BOOL", "Boolean", " boolean "])
async def test_point_list_type_filter_uses_the_display_caliber(data_type) -> None:
    """类型筛选必须和列表显示同一套口径，否则 BYTE/Boolean 会被当成 STRING。"""
    session = AsyncMock()
    session.execute.side_effect = [_count_result(0), _page_result([])]
    service = CatalogService(session=session, tdengine=MagicMock())
    service._fetch_latest_point_value_map = AsyncMock(return_value={})

    await service.list_points({"dataType": data_type})

    count_sql, count_params = session.execute.await_args_list[0].args
    assert "WHEN 'BOOLEAN' THEN 'BOOL'" in str(count_sql)
    assert "ELSE 'STRING'" not in str(count_sql)
    assert count_params["datatype"] == "BOOL"


@pytest.mark.asyncio
async def test_point_list_shows_boolean_and_byte_with_their_real_type() -> None:
    session = AsyncMock()
    session.execute.side_effect = [
        _count_result(2),
        _page_result(
            [
                {
                    "point_id": 52000,
                    "device_id": 8,
                    "device_code": "AGV801",
                    "device_name": "AGV801",
                    "point_name": "FLT1001",
                    "io_address": "OPD_Server.AGV801.Fault.Tag30007101",
                    "data_type": "BOOLEAN",
                    "description": "",
                    "driver_name": "opcda_center",
                    "create_time": None,
                    "update_time": None,
                    "var_seq": 306,
                    "scan_rate": 1000,
                    "is_alarm": 0,
                    "is_storage": 1,
                    "is_readonly": 1,
                    "group_id": 2,
                    "td_column_name": "flt1001",
                    "enabled": 1,
                },
                {
                    "point_id": 52001,
                    "device_id": 8,
                    "device_code": "QC100",
                    "device_name": "QC100",
                    "point_name": "rosNumber",
                    "io_address": "%DB1702.DBB2",
                    "data_type": "BYTE",
                    "description": "",
                    "driver_name": "s7",
                    "create_time": None,
                    "update_time": None,
                    "var_seq": 2,
                    "scan_rate": 1000,
                    "is_alarm": 0,
                    "is_storage": 1,
                    "is_readonly": 1,
                    "group_id": 2,
                    "td_column_name": "rosnumber",
                    "enabled": 1,
                },
            ]
        ),
    ]
    service = CatalogService(session=session, tdengine=MagicMock())
    service._fetch_latest_point_value_map = AsyncMock(return_value={})

    page = await service.list_points({})

    assert [item["dataType"] for item in page["records"]] == ["BOOL", "BYTE"]
    # 点位 ID 与 IO 地址不受显示口径影响
    assert page["records"][0]["id"] == 52000
    assert page["records"][0]["varSeq"] == 306
    assert page["records"][0]["ioAddress"] == "OPD_Server.AGV801.Fault.Tag30007101"
