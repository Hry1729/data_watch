"""点位/设备表单补全：自动序号、驱动跟随设备、类型编码去空格、机架槽号校验。"""
from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest

from app.core.exceptions import ApiError
from app.services.catalog import CatalogService


def _scalar(value):
    result = MagicMock()
    result.scalar_one.return_value = value
    result.scalar_one_or_none.return_value = value
    return result


def _mappings_first(row):
    result = MagicMock()
    result.mappings.return_value.first.return_value = row
    return result


def _mappings_all(rows):
    result = MagicMock()
    result.mappings.return_value.all.return_value = rows
    return result


@pytest.mark.asyncio
async def test_next_point_seq_counts_soft_deleted_rows() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(return_value=_scalar(12))
    service = CatalogService(session=session, tdengine=MagicMock())

    assert await service.next_point_seq(63) == {"varSeq": 13}
    sql = str(session.execute.await_args.args[0])
    assert "MAX(var_seq)" in sql
    assert "deleted" not in sql


@pytest.mark.asyncio
async def test_next_point_seq_starts_at_one_for_empty_device() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(return_value=_scalar(None))
    service = CatalogService(session=session, tdengine=MagicMock())

    assert await service.next_point_seq(5) == {"varSeq": 1}


@pytest.mark.asyncio
async def test_device_type_options_trim_codes_and_skip_blank() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(
        return_value=_mappings_all(
            [
                {"type_code": " ASC", "type_name": "轨道吊"},
                {"type_code": "", "type_name": "空"},
                {"type_code": "QC", "type_name": None},
            ]
        )
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    assert await service.list_device_type_options() == [
        {"typeCode": "ASC", "typeName": "轨道吊"},
        {"typeCode": "QC", "typeName": "QC"},
    ]


@pytest.mark.asyncio
async def test_mapping_insert_strips_spaces_from_type_code() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(side_effect=[_scalar(1), MagicMock()])
    service = CatalogService(session=session, tdengine=MagicMock())

    await service._insert_point_storage_mapping(
        point_id=11,
        device_code="asc7181",
        device_type_code=" asc",
        point_slot={"group_code": "g001", "td_column_name": "asc1"},
        var_type="DINT",
    )

    _, params = session.execute.await_args_list[1].args
    assert params["device_type_code"] == "asc"
    assert params["stable_name"] == "sdasc"


@pytest.mark.asyncio
async def test_create_point_rejects_mismatched_driver_with_readable_message() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(
        return_value=_mappings_first(
            {"device_id": 63, "device_code": "asc7181", "driver_name": "S7", "type_code": "asc"}
        )
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    with pytest.raises(ApiError) as exc_info:
        await service.create_point(
            {
                "deviceId": 63,
                "deviceCode": "asc7181",
                "pointName": "大车位置",
                "ioAddress": "MD908",
                "dataType": "DINT",
                "driverName": "modbus_tcp",
            },
            actor="admin",
        )

    assert exc_info.value.code == 400
    assert "S7" in exc_info.value.message


@pytest.mark.asyncio
async def test_create_point_conflicting_seq_suggests_next_free_one() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(
        side_effect=[
            _mappings_first(
                {"device_id": 63, "device_code": "asc7181", "driver_name": "S7", "type_code": "asc"}
            ),
            _scalar(None),  # IO 地址未占用
            _scalar(99),  # 序号已占用
            _scalar(4),  # MAX(var_seq)
        ]
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    with pytest.raises(ApiError) as exc_info:
        await service.create_point(
            {
                "deviceId": 63,
                "deviceCode": "asc7181",
                "varSeq": 2,
                "pointName": "大车位置",
                "ioAddress": "MD908",
                "dataType": "DINT",
            },
            actor="admin",
        )

    assert exc_info.value.code == 409
    assert "可用序号 5" in exc_info.value.message
    address_sql = str(session.execute.await_args_list[1].args[0])
    assert "deleted" in address_sql


def test_device_link_overrides_validate_ranges() -> None:
    service = CatalogService(session=AsyncMock(), tdengine=MagicMock())

    assert service._device_link_overrides({"rack": "0", "slot": 2, "scanCycle": "500"}) == (0, 2, 500)
    assert service._device_link_overrides({}) == (None, None, None)
    with pytest.raises(ApiError):
        service._device_link_overrides({"slot": 40})
    with pytest.raises(ApiError):
        service._device_link_overrides({"scanCycle": "abc"})
