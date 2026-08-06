from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest

from app.services.catalog import CatalogService


def _mapping_result(row):
    result = MagicMock()
    result.mappings.return_value.first.return_value = row
    return result


def _scalar_result(value):
    result = MagicMock()
    result.scalar_one.return_value = value
    result.scalar_one_or_none.return_value = value
    return result


@pytest.mark.asyncio
async def test_device_update_bumps_assigned_edge_revision() -> None:
    existing = {
        "id": 7,
        "device_code": "AGV801",
        "endpoint": "127.0.0.1",
        "endpoint_bkp": "",
        "rack": 0,
        "slot": 1,
        "scan_cycle": 1000,
        "status": 0,
        "type_id": 2,
    }
    session = AsyncMock()
    session.execute = AsyncMock(
        side_effect=[_mapping_result(existing), _scalar_result(None), MagicMock()]
    )
    service = CatalogService(session=session, tdengine=MagicMock())
    service._resolve_device_type_row = AsyncMock(return_value={"id": 2})
    service._resolve_protocol_row = AsyncMock(return_value={"id": 4, "protocol": "opcda_center"})
    service._lock_protocol_for_binding = AsyncMock()
    service._fetch_device_reference_row = AsyncMock(return_value=None)
    service._resolve_group_id = AsyncMock(return_value=3)
    service._bump_assigned_node_config_versions = AsyncMock()

    await service.update_device(
        7,
        {
            "deviceType": "AGV",
            "deviceCode": "AGV801",
            "deviceName": "AGV 801",
            "driverName": "opcda_center",
            "deviceStatus": "disabled",
            "endpoint": "192.168.1.10:102",
            "endpointBackup": "192.168.1.11:102",
            "description": "",
        },
        actor="admin",
    )

    _, update_params = session.execute.await_args_list[2].args
    assert update_params["endpoint"] == "192.168.1.10:102"
    assert update_params["endpoint_bkp"] == "192.168.1.11:102"
    service._bump_assigned_node_config_versions.assert_awaited_once_with(7)
    session.commit.assert_awaited_once()


@pytest.mark.asyncio
async def test_assigned_edge_revision_update_is_skipped_without_distributed_tables() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(return_value=_scalar_result(0))
    service = CatalogService(session=session, tdengine=MagicMock())

    await service._bump_assigned_node_config_versions(7)

    assert session.execute.await_count == 1


@pytest.mark.asyncio
async def test_assigned_edge_revision_updates_active_and_retired_nodes() -> None:
    session = AsyncMock()
    session.execute = AsyncMock(side_effect=[_scalar_result(2), MagicMock()])
    service = CatalogService(session=session, tdengine=MagicMock())

    await service._bump_assigned_node_config_versions(7)

    statement, params = session.execute.await_args_list[1].args
    sql = str(statement)
    assert "UPDATE sys_node" in sql
    assert "status IN ('active', 'retired')" in sql
    assert params == {"device_id": 7}