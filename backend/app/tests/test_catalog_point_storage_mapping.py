from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest

from app.core.exceptions import ApiError
from app.services.catalog import CatalogService


def _scalar_result(*, one=None, one_or_none=None):
    result = MagicMock()
    result.scalar_one.return_value = one
    result.scalar_one_or_none.return_value = one_or_none
    return result


@pytest.mark.asyncio
async def test_mapping_insert_is_skipped_without_db6_table() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(return_value=_scalar_result(one=0))
    service = CatalogService(session=session, tdengine=MagicMock())

    await service._insert_point_storage_mapping(
        point_id=11,
        device_code="QC100",
        device_type_code="qc",
        point_slot={"group_code": "g001", "td_column_name": "qc1"},
        var_type="REAL",
    )

    assert session.execute.await_count == 1
    assert "information_schema.TABLES" in str(session.execute.await_args.args[0])


@pytest.mark.asyncio
async def test_mapping_insert_freezes_complete_physical_coordinates() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(
        side_effect=[_scalar_result(one=1), MagicMock()]
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    await service._insert_point_storage_mapping(
        point_id=11,
        device_code="QC100",
        device_type_code="qc",
        point_slot={"group_code": "g001", "td_column_name": "qc1"},
        var_type="REAL",
    )

    statement, params = session.execute.await_args_list[1].args
    assert "INSERT INTO point_storage_mapping" in str(statement)
    assert params == {
        "point_id": 11,
        "device_code": "QC100",
        "device_type_code": "qc",
        "stable_name": "sdqc",
        "child_table": "QC100",
        "group_code": "g001",
        "td_column_name": "qc1",
        "var_type": "REAL",
    }


@pytest.mark.asyncio
async def test_device_delete_is_blocked_when_permanent_mapping_exists() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(
        side_effect=[
            _scalar_result(one=1),
            _scalar_result(one_or_none=7),
        ]
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    with pytest.raises(ApiError) as exc_info:
        await service._assert_devices_have_no_permanent_mappings([7])

    assert exc_info.value.code == 409
    assert "disable it instead" in exc_info.value.message
