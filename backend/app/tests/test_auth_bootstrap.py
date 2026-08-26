from __future__ import annotations

import pytest

from app.models import SysUser
from app.services import auth_bootstrap


class _Session:
    def __init__(self, users: dict[str, SysUser] | None = None) -> None:
        self.users = users or {}
        self.added: list[SysUser] = []

    async def scalar(self, statement):
        name = statement.whereclause.right.value
        return self.users.get(name)

    def add(self, value) -> None:
        self.added.append(value)

    async def flush(self) -> None:
        return None


@pytest.mark.asyncio
async def test_production_bootstrap_creates_only_super_from_explicit_password(monkeypatch) -> None:
    monkeypatch.setenv("AUTH_BOOTSTRAP_SUPER_PASSWORD", "Strong-Bootstrap-Password-2026!")
    session = _Session()

    credentials = await auth_bootstrap._seed_users(session, app_env="production")

    assert credentials == []
    assert [user.user_name for user in session.added] == ["Super"]
    assert session.added[0].must_change_password is True
    assert auth_bootstrap.verify_password(
        "Strong-Bootstrap-Password-2026!",
        session.added[0].password_hash,
    )


@pytest.mark.asyncio
async def test_production_bootstrap_fails_before_writing_without_password(monkeypatch) -> None:
    monkeypatch.delenv("AUTH_BOOTSTRAP_SUPER_PASSWORD", raising=False)
    session = _Session()

    with pytest.raises(RuntimeError, match="AUTH_BOOTSTRAP_SUPER_PASSWORD"):
        await auth_bootstrap._seed_users(session, app_env="production")

    assert session.added == []


@pytest.mark.asyncio
async def test_disabled_existing_super_is_not_revived(monkeypatch) -> None:
    monkeypatch.delenv("AUTH_BOOTSTRAP_SUPER_PASSWORD", raising=False)
    monkeypatch.setattr(auth_bootstrap, "verify_password", lambda *_args: False)
    super_user = SysUser(
        user_name="Super",
        password_hash="already-hashed",
        status="2",
        deleted=False,
    )
    session = _Session({"Super": super_user})

    await auth_bootstrap._seed_users(session, app_env="production")

    assert session.added == []
    assert super_user.status == "2"
    assert super_user.deleted is False
