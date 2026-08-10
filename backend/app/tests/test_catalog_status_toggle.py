from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest

from app.services.catalog import CatalogService


def _scalar_result(value):
    result = MagicMock()
    result.scalar_one_or_none.return_value = value
    return result


def _mapping_result(row):
    result = MagicMock()
    result.mappings.return_value.first.return_value = row
    return result


@pytest.mark.asyncio
async def test_device_status_change_bumps_assigned_edge_revision() -> None:
    update_result = MagicMock(rowcount=1)
    session = AsyncMock()
    session.execute = AsyncMock(side_effect=[_scalar_result(1), update_result])
    service = CatalogService(session=session, tdengine=MagicMock())
    service._bump_assigned_node_config_versions = AsyncMock()

    result = await service.update_device_status(7, enabled=False, actor="admin")

    assert result == {"id": 7}
    _, params = session.execute.await_args_list[1].args
    assert params == {"device_id": 7, "enabled": 0, "actor": "admin"}
    service._bump_assigned_node_config_versions.assert_awaited_once_with(7)
    session.commit.assert_awaited_once()


@pytest.mark.asyncio
async def test_device_status_same_value_is_idempotent() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(return_value=_scalar_result(0))
    service = CatalogService(session=session, tdengine=MagicMock())
    service._bump_assigned_node_config_versions = AsyncMock()

    assert await service.update_device_status(7, enabled=False, actor="admin") == {"id": 7}
    assert session.execute.await_count == 1
    service._bump_assigned_node_config_versions.assert_not_awaited()
    session.commit.assert_not_awaited()


@pytest.mark.asyncio
async def test_point_status_change_updates_group_and_revision() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(
        side_effect=[
            _mapping_result({"device_id": 7, "point_group_id": 3, "enabled": 1}),
            MagicMock(),
            MagicMock(),
        ]
    )
    service = CatalogService(session=session, tdengine=MagicMock())
    service._resolve_point_locator = AsyncMock(
        return_value={"point_id": 11, "device_id": 7, "var_seq": 1}
    )
    service._mark_point_config_changed = AsyncMock()

    result = await service.update_point_status(
        11,
        enabled=False,
        actor="admin",
        locator_device_id=7,
        locator_var_seq=1,
    )

    assert result == {"id": 11}
    _, params = session.execute.await_args_list[1].args
    assert params == {"point_id": 11, "enabled": 0, "actor": "admin"}
    service._mark_point_config_changed.assert_awaited_once_with(
        point_group_id=3,
        device_id=7,
        actor="admin",
    )
    session.commit.assert_awaited_once()


def test_point_item_serializes_enabled_status() -> None:
    service = CatalogService(session=MagicMock(), tdengine=MagicMock())
    row = {
        "point_id": 11,
        "device_id": 7,
        "device_code": "QC100",
        "device_name": "桥吊 100",
        "point_name": "起升速度",
        "io_address": "DB1.DBD0",
        "data_type": "REAL",
        "description": "",
        "driver_name": "s7",
        "create_time": None,
        "update_time": None,
        "var_seq": 1,
        "scan_rate": 100,
        "is_alarm": 0,
        "is_storage": 1,
        "is_readonly": 1,
        "group_id": 3,
        "td_column_name": "v1",
        "enabled": 0,
    }

    point = service._to_point_item(row)
    assert service._serialize_point_item(point, None)["pointStatus"] == "disabled"
