"""H4：自动 bootstrap 的种子授权只执行一次，重启不得恢复管理员已撤销的授权。"""

from __future__ import annotations

import pytest
import pytest_asyncio
from sqlalchemy import BigInteger, delete, select
from sqlalchemy.ext.compiler import compiles

pytest.importorskip("aiosqlite")

from sqlalchemy.ext.asyncio import async_sessionmaker, create_async_engine  # noqa: E402

from app.models import (  # noqa: E402
    SysBootstrapState,
    SysPermission,
    SysRole,
    SysRolePermission,
    SysUser,
    SysUserRole,
)
from app.services import auth_bootstrap  # noqa: E402


@compiles(BigInteger, "sqlite")
def _sqlite_bigint(_type, _compiler, **_kw):  # SQLite 只有 INTEGER 主键才自增
    return "INTEGER"


@pytest_asyncio.fixture
async def sqlite_db(monkeypatch, tmp_path):
    engine = create_async_engine(f"sqlite+aiosqlite:///{tmp_path / 'auth.db'}")
    factory = async_sessionmaker(engine, expire_on_commit=False)
    monkeypatch.setattr(auth_bootstrap, "get_mysql_engine", lambda: engine)
    monkeypatch.setattr(auth_bootstrap, "get_mysql_session_factory", lambda: factory)
    # 只验证授权语义，跳过 PBKDF2
    monkeypatch.setattr(auth_bootstrap, "run_password_op", _fake_password_op)
    try:
        yield factory
    finally:
        await engine.dispose()


async def _fake_password_op(func, *args):
    if func is auth_bootstrap.hash_password:
        return "hashed"
    return False


async def _role_has_permission(factory, role_code: str, permission_code: str) -> bool:
    async with factory() as session:
        row = await session.scalar(
            select(SysRolePermission.id)
            .join(SysRole, SysRole.id == SysRolePermission.role_id)
            .join(SysPermission, SysPermission.id == SysRolePermission.permission_id)
            .where(SysRole.role_code == role_code, SysPermission.permission_code == permission_code)
        )
        return row is not None


async def _user_has_role(factory, user_name: str, role_code: str) -> bool:
    async with factory() as session:
        row = await session.scalar(
            select(SysUserRole.id)
            .join(SysUser, SysUser.id == SysUserRole.user_id)
            .join(SysRole, SysRole.id == SysUserRole.role_id)
            .where(SysUser.user_name == user_name, SysRole.role_code == role_code)
        )
        return row is not None


async def _revoke(factory, role_code: str, permission_code: str, user_name: str, user_role: str) -> None:
    async with factory() as session:
        role = await session.scalar(select(SysRole).where(SysRole.role_code == role_code))
        permission = await session.scalar(
            select(SysPermission).where(SysPermission.permission_code == permission_code)
        )
        await session.execute(
            delete(SysRolePermission).where(
                SysRolePermission.role_id == role.id,
                SysRolePermission.permission_id == permission.id,
            )
        )
        user = await session.scalar(select(SysUser).where(SysUser.user_name == user_name))
        other = await session.scalar(select(SysRole).where(SysRole.role_code == user_role))
        await session.execute(
            delete(SysUserRole).where(SysUserRole.user_id == user.id, SysUserRole.role_id == other.id)
        )
        await session.commit()


@pytest.mark.asyncio
async def test_restart_does_not_restore_revoked_seed_grants(sqlite_db) -> None:
    factory = sqlite_db
    await auth_bootstrap.bootstrap_auth_schema(app_env="development")
    assert await _role_has_permission(factory, "R_USER", "device_monitor:realtime:subscribe")
    assert await _user_has_role(factory, "Admin", "R_ADMIN")
    async with factory() as session:
        state = await session.get(SysBootstrapState, auth_bootstrap.SEED_GRANTS_STATE_KEY)
        assert state is not None and state.version == auth_bootstrap.SEED_GRANTS_VERSION

    await _revoke(factory, "R_USER", "device_monitor:realtime:subscribe", "Admin", "R_ADMIN")
    await auth_bootstrap.bootstrap_auth_schema(app_env="development")

    assert not await _role_has_permission(factory, "R_USER", "device_monitor:realtime:subscribe")
    assert not await _user_has_role(factory, "Admin", "R_ADMIN")


@pytest.mark.asyncio
async def test_legacy_install_without_state_is_not_regranted(sqlite_db) -> None:
    """旧版本已初始化过（没有状态记录）的库：升级后第一次启动也不补授权。"""
    factory = sqlite_db
    await auth_bootstrap.bootstrap_auth_schema(app_env="development")
    async with factory() as session:
        await session.execute(delete(SysBootstrapState))
        await session.commit()
    await _revoke(factory, "R_USER", "device_monitor:realtime:subscribe", "Admin", "R_ADMIN")

    await auth_bootstrap.bootstrap_auth_schema(app_env="development")

    assert not await _role_has_permission(factory, "R_USER", "device_monitor:realtime:subscribe")
    assert not await _user_has_role(factory, "Admin", "R_ADMIN")
    async with factory() as session:
        state = await session.get(SysBootstrapState, auth_bootstrap.SEED_GRANTS_STATE_KEY)
        assert state is not None and state.version == auth_bootstrap.SEED_GRANTS_VERSION


@pytest.mark.asyncio
async def test_newly_added_seed_permission_is_still_granted_once(sqlite_db) -> None:
    """后续版本新增的种子权限（库里还没有）仍会授予种子角色一次。"""
    factory = sqlite_db
    await auth_bootstrap.bootstrap_auth_schema(app_env="development")
    async with factory() as session:
        permission = await session.scalar(
            select(SysPermission).where(SysPermission.permission_code == "device_monitor:history:view")
        )
        await session.execute(
            delete(SysRolePermission).where(SysRolePermission.permission_id == permission.id)
        )
        await session.delete(permission)
        await session.commit()
    assert not await _role_has_permission(factory, "R_USER", "device_monitor:history:view")

    await auth_bootstrap.bootstrap_auth_schema(app_env="development")

    assert await _role_has_permission(factory, "R_USER", "device_monitor:history:view")
