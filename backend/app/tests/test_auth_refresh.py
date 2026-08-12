from __future__ import annotations

from types import SimpleNamespace

import pytest

from app.core.security import create_signed_token, decode_signed_token
from app.services.auth import AuthService


@pytest.mark.asyncio
async def test_refresh_rotates_access_and_refresh_tokens(monkeypatch) -> None:
    service = AuthService(session=None)
    token = create_signed_token(
        {"uid": 8, "userName": "operator", "roles": ["R_USER"]},
        secret_key=service.settings.auth_secret_key,
        expires_in_seconds=60,
        token_type="refresh",
    )

    async def fetch_user(_user_id: int):
        return SimpleNamespace(id=8, user_name="operator", status="1", deleted=False)

    async def fetch_roles(_user_id: int):
        return ["R_USER"]

    monkeypatch.setattr(service, "_fetch_user_by_id", fetch_user)
    monkeypatch.setattr(service, "_fetch_user_roles", fetch_roles)

    result = await service.refresh(token)

    access = decode_signed_token(
        result["token"],
        secret_key=service.settings.auth_secret_key,
    )
    refreshed = decode_signed_token(
        result["refreshToken"],
        secret_key=service.settings.auth_secret_key,
    )
    assert access["type"] == "access"
    assert refreshed["type"] == "refresh"
    assert access["uid"] == 8
