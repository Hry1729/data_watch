from __future__ import annotations

from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from app.core.exceptions import ApiError
from app.core.security import create_signed_token
from app.schemas.auth import ChangePasswordPayload
from app.services import auth as auth_module
from app.services.auth import AuthService


@pytest.mark.asyncio
async def test_change_password_clears_force_change_and_invalidates_tokens(monkeypatch) -> None:
    service = AuthService(session=SimpleNamespace(commit=AsyncMock()))
    user = SimpleNamespace(
        id=7,
        user_name="operator",
        password_hash="old-hash",
        token_version=4,
        must_change_password=True,
        deleted=False,
    )
    service.resolve_principal = AsyncMock(return_value={"user_id": 7})
    service._fetch_user_by_id = AsyncMock(return_value=user)
    monkeypatch.setattr(auth_module, "verify_password", lambda value, _hash: value == "old")
    monkeypatch.setattr(auth_module, "hash_password", lambda value: f"hash:{value}")
    monkeypatch.setattr(auth_module, "write_system_log", AsyncMock())

    result = await service.change_password(
        "Bearer access-token",
        ChangePasswordPayload(old_password="old", new_password="new-password"),
    )

    assert result is True
    assert user.password_hash == "hash:new-password"
    assert user.must_change_password is False
    assert user.token_version == 5
    service.session.commit.assert_awaited_once()


@pytest.mark.asyncio
async def test_logout_invalidates_current_token_version() -> None:
    service = AuthService(session=SimpleNamespace(commit=AsyncMock()))
    user = SimpleNamespace(id=7, user_name="operator", token_version=2, deleted=False)
    principal = {"user_id": 7, "user_name": "operator", "roles": ["R_USER"]}
    service.resolve_principal = AsyncMock(return_value=principal)
    service._fetch_user_by_id = AsyncMock(return_value=user)

    assert await service.logout("Bearer access-token") == principal
    assert user.token_version == 3
    service.session.commit.assert_awaited_once()


@pytest.mark.asyncio
async def test_resolve_principal_rejects_stale_token_and_forced_change(monkeypatch) -> None:
    service = AuthService(session=None)
    user = SimpleNamespace(
        id=7,
        user_name="operator",
        email="",
        avatar="",
        status="1",
        deleted=False,
        token_version=3,
        must_change_password=True,
    )
    service._fetch_user_by_id = AsyncMock(return_value=user)
    service._fetch_user_roles = AsyncMock(return_value=["R_USER"])

    stale = create_signed_token(
        {"uid": 7, "token_version": 2},
        secret_key=service.settings.auth_secret_key,
        expires_in_seconds=60,
        token_type="access",
    )
    with pytest.raises(ApiError) as stale_error:
        await service.resolve_principal(stale, allow_password_change=True)
    assert stale_error.value.code == 401

    current = create_signed_token(
        {"uid": 7, "token_version": 3},
        secret_key=service.settings.auth_secret_key,
        expires_in_seconds=60,
        token_type="access",
    )
    with pytest.raises(ApiError) as force_error:
        await service.resolve_principal(current)
    assert force_error.value.code == 403

    principal = await service.resolve_principal(current, allow_password_change=True)
    assert principal["must_change_password"] is True
    assert principal["token_version"] == 3
