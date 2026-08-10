from __future__ import annotations

from unittest.mock import AsyncMock

import pytest

from app.services.auth import AuthService


@pytest.mark.asyncio
async def test_user_info_returns_auth_marks_and_permission_codes() -> None:
    service = AuthService(session=None)
    principal = {
        "user_id": 7,
        "user_name": "operator",
        "email": "operator@example.com",
        "avatar": "",
        "roles": ["R_ADMIN"],
    }
    service.resolve_principal = AsyncMock(return_value=principal)
    service._fetch_user_auth_marks = AsyncMock(return_value=["add", "edit"])
    service.fetch_user_permission_codes = AsyncMock(
        return_value=["system:menu:create", "system:menu:update"]
    )

    result = await service.get_user_info("Bearer token")

    assert result == {
        "userId": 7,
        "userName": "operator",
        "email": "operator@example.com",
        "avatar": "",
        "roles": ["R_ADMIN"],
        "buttons": ["add", "edit"],
        "permissions": ["system:menu:create", "system:menu:update"],
    }
    service._fetch_user_auth_marks.assert_awaited_once_with(7)
    service.fetch_user_permission_codes.assert_awaited_once_with(7)
