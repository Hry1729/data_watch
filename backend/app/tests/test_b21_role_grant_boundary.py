"""B21 回归：角色管理权的可授予权限边界。

修复前：``update_role`` / ``update_role_grant`` **不接收当前主体**，被授予角色管理权的
用户可以：
- 把任意角色的 ``role_code`` 改成 ``R_SUPER``（内置超级角色编码唯一，改名即等于提升）；
- 改写/禁用/删除 ``R_SUPER`` 角色本身；
- 把任意（甚至自己都没有的）权限授给自己的角色。
"""

from __future__ import annotations

from dataclasses import dataclass

import pytest

from app.core.exceptions import ApiError
from app.schemas.auth import RolePayload
from app.services.system_manage import SystemManageService


@dataclass
class _Role:
    id: int
    role_code: str
    role_name: str = "role"
    description: str = ""
    enabled: bool = True


class _FakeResult:
    def __init__(self, rows):
        self._rows = list(rows)

    def scalars(self):
        return self

    def all(self):
        return list(self._rows)


class _FakeSession:
    """只服务本用例：execute 返回调用方指定的权限码；scalar/commit 可控。"""

    def __init__(self, permission_codes=(), scalar_value=None):
        self._permission_codes = list(permission_codes)
        self._scalar_value = scalar_value
        self.committed = 0

    async def execute(self, _statement):
        return _FakeResult(self._permission_codes)

    async def scalar(self, _statement):
        return self._scalar_value

    async def commit(self):
        self.committed += 1


class _FakeAuth:
    def __init__(self, codes):
        self._codes = list(codes)

    async def fetch_user_permission_codes(self, _user_id):
        return list(self._codes)


def _service(session: _FakeSession, actor_codes=()) -> SystemManageService:
    service = SystemManageService(session)
    service.auth_service = _FakeAuth(actor_codes)
    return service


# --- 内置超级角色的保护 -------------------------------------------------------


@pytest.mark.asyncio
async def test_non_super_cannot_manage_super_role() -> None:
    service = _service(_FakeSession())
    with pytest.raises(ApiError) as excinfo:
        await service._assert_can_manage_role(_Role(1, "R_SUPER"), current_user_roles=["R_ADMIN"])
    assert excinfo.value.code == 403


@pytest.mark.asyncio
async def test_super_may_manage_super_role_and_others() -> None:
    service = _service(_FakeSession())
    # 超级管理员不受限制
    await service._assert_can_manage_role(_Role(1, "R_SUPER"), current_user_roles=["R_SUPER"])
    # 普通角色对任何人都不受该守卫限制
    await service._assert_can_manage_role(_Role(2, "R_ADMIN"), current_user_roles=["R_ADMIN"])


@pytest.mark.asyncio
async def test_non_super_cannot_rename_role_into_super() -> None:
    session = _FakeSession(scalar_value=None)
    service = _service(session)
    service._get_role_or_raise = _async_return(_Role(2, "R_ADMIN"))
    payload = RolePayload.from_dict(
        {"roleName": "x", "roleCode": "R_SUPER", "description": "", "enabled": True}
    )
    with pytest.raises(ApiError) as excinfo:
        await service.update_role(2, payload, current_user_roles=["R_ADMIN"])
    assert excinfo.value.code == 403
    assert session.committed == 0


@pytest.mark.asyncio
async def test_non_super_cannot_disable_or_delete_super_role() -> None:
    service = _service(_FakeSession())
    service._get_role_or_raise = _async_return(_Role(1, "R_SUPER"))
    with pytest.raises(ApiError) as disable:
        await service.update_role_status(1, enabled=False, current_user_roles=["R_ADMIN"])
    assert disable.value.code == 403
    with pytest.raises(ApiError) as delete:
        await service.delete_role(1, current_user_roles=["R_ADMIN"])
    assert delete.value.code == 403


# --- 可授予权限的包含性 -------------------------------------------------------


@pytest.mark.asyncio
async def test_non_super_cannot_grant_permissions_they_lack() -> None:
    # 请求授予的权限码为 system:role:grant，而该管理员只持有 system:user:view
    session = _FakeSession(permission_codes=["system:role:grant", "system:user:delete"])
    service = _service(session, actor_codes=["system:user:view"])
    with pytest.raises(ApiError) as excinfo:
        await service._assert_can_grant_permissions(
            [1, 2], current_user_id=7, current_user_roles=["R_ADMIN"]
        )
    assert excinfo.value.code == 403
    message = str(excinfo.value)
    assert "system:role:grant" in message


@pytest.mark.asyncio
async def test_non_super_may_grant_permissions_they_hold() -> None:
    session = _FakeSession(permission_codes=["system:user:view"])
    service = _service(session, actor_codes=["system:user:view", "system:user:update"])
    # 不抛异常即通过
    await service._assert_can_grant_permissions(
        [1], current_user_id=7, current_user_roles=["R_ADMIN"]
    )


@pytest.mark.asyncio
async def test_super_may_grant_anything() -> None:
    session = _FakeSession(permission_codes=["system:role:grant"])
    service = _service(session, actor_codes=[])
    await service._assert_can_grant_permissions(
        [1], current_user_id=1, current_user_roles=["R_SUPER"]
    )


@pytest.mark.asyncio
async def test_empty_grant_needs_no_subset_check() -> None:
    session = _FakeSession(permission_codes=[])
    service = _service(session, actor_codes=[])
    await service._assert_can_grant_permissions(
        [], current_user_id=7, current_user_roles=["R_ADMIN"]
    )


# --- 路由必须把当前主体传进 service -------------------------------------------


def test_role_routes_pass_the_current_principal() -> None:
    import inspect

    from app.api.routes import system_manage as routes

    for route in (routes.update_role, routes.update_role_status, routes.delete_role):
        assert "current_user_roles" in inspect.getsource(route)
    assert "current_user_id" in inspect.getsource(routes.update_role_grant)


def _async_return(value):
    async def _call(*_args, **_kwargs):
        return value

    return _call

