"""Persistent driver lifecycle gate used by device create/update."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock

import pytest

from app.core.exceptions import ApiError
from app.services.catalog import CatalogService


def _mapping_result(rows):
    result = MagicMock()
    mappings = MagicMock()
    mappings.first.return_value = rows[0] if rows else None
    result.mappings.return_value = mappings
    return result


@pytest.mark.asyncio
async def test_binding_locks_protocol_row_and_accepts_active_driver():
    session = AsyncMock()
    session.execute = AsyncMock(
        side_effect=[_mapping_result([]), _mapping_result([{"lifecycle_status": "active"}])]
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    await service._lock_protocol_for_binding(protocol_id=3, protocol="modbus_tcp")

    assert "FOR UPDATE" in str(session.execute.await_args_list[0].args[0])


@pytest.mark.asyncio
async def test_binding_rejects_uninstalling_driver():
    session = AsyncMock()
    session.execute = AsyncMock(
        side_effect=[
            _mapping_result([]),
            _mapping_result([{"lifecycle_status": "uninstalling"}]),
        ]
    )
    service = CatalogService(session=session, tdengine=MagicMock())

    with pytest.raises(ApiError) as exc_info:
        await service._lock_protocol_for_binding(protocol_id=3, protocol="modbus_tcp")

    assert exc_info.value.code == 409
