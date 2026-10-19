"""L6 / L7 回归：改角色编码、改权限编码不能绕过授权。

L6：节点管理、他人导出文件访问按角色编码 R_ADMIN/R_SUPER 判断。持有 system:role:update
    的非超级管理员可以先把内置 R_ADMIN 改名腾出编码，再把自己的角色改名为 R_ADMIN。
L7：持有 system:menu:update 的用户可以把自己已持有的权限的 permission_code 改成任意
    高权限码，绕开 _assert_can_grant_permissions。
"""

from __future__ import annotations

import inspect
from dataclasses import dataclass

import pytest

from app.core.exceptions import ApiError
from app.schemas.auth import PermissionPayload, RolePayload
from app.services.system_manage import SystemManageService


@dataclass
class _Role:
    id: int
    role_code: str
    role_name: str = "role"
    description: str = ""
    enabled: bool = True


@dataclass
class _Permission:
    id: int
    permission_code: str
    permission_name: str = "p"
    permission_type: str = "action"
    menu_id: int | None = 1
    auth_mark: str = ""
    description: str = ""
    sort: int = 1
    enabled: bool = True


class _Session:
    def __init__(self):
        self.committed = 0
        self.added = []

    async def scalar(self, _statement):
        return None

    def add(self, value):
        self.added.append(value)

    async def commit(self):
        self.committed += 1


class _FakeAuth:
    def __init__(self, codes):
        self._codes = list(codes)

    async def fetch_user_permission_codes(self, _user_id):
        return list(self._codes)


def _service(actor_codes=()):
    session = _Session()
    service = SystemManageService(session)
    service.auth_service = _FakeAuth(actor_codes)
    return service, session


def _async_return(value):
    async def _call(*_args, **_kwargs):
        return value

    return _call


def _role_payload(code: str) -> RolePayload:
    return RolePayload.from_dict({"roleName": "x", "roleCode": code, "description": "", "enabled": True})


def _permission_payload(code: str) -> PermissionPayload:
    return PermissionPayload.from_dict(
        {"permissionCode": code, "permissionName": "p", "permissionType": "action", "authMark": "", "sort": 1}
    )


# --- L6 内置角色编码 ---------------------------------------------------------


@pytest.mark.asyncio
@pytest.mark.parametrize("actor_roles", [["R_ADMIN"], ["R_SUPER"]])
async def test_builtin_role_code_cannot_be_renamed(actor_roles) -> None:
    service, session = _service()
    role = _Role(2, "R_ADMIN")
    service._get_role_or_raise = _async_return(role)
    with pytest.raises(ApiError) as excinfo:
        await service.update_role(2, _role_payload("R_ADMIN_OLD"), current_user_roles=actor_roles)
    assert excinfo.value.code == 403
    assert role.role_code == "R_ADMIN"
    assert session.committed == 0


@pytest.mark.asyncio
async def test_non_super_cannot_rename_custom_role_into_builtin_code() -> None:
    service, session = _service()
    service._get_role_or_raise = _async_return(_Role(9, "R_OPS"))
    with pytest.raises(ApiError) as excinfo:
        await service.update_role(9, _role_payload("R_ADMIN"), current_user_roles=["R_OPS"])
    assert excinfo.value.code == 403
    assert session.committed == 0


@pytest.mark.asyncio
async def test_non_super_cannot_create_role_with_builtin_code() -> None:
    service, session = _service()
    with pytest.raises(ApiError) as excinfo:
        await service.create_role(_role_payload("R_ADMIN"), current_user_roles=["R_OPS"])
    assert excinfo.value.code == 403
    assert session.added == []


@pytest.mark.asyncio
async def test_builtin_role_name_still_editable_and_custom_rename_allowed() -> None:
    service, session = _service()
    role = _Role(2, "R_ADMIN")
    service._get_role_or_raise = _async_return(role)
    payload = RolePayload.from_dict({"roleName": "新名称", "roleCode": "R_ADMIN", "description": "", "enabled": True})
    await service.update_role(2, payload, current_user_roles=["R_ADMIN"])
    assert role.role_name == "新名称"

    custom = _Role(9, "R_OPS")
    service._get_role_or_raise = _async_return(custom)
    await service.update_role(9, _role_payload("R_OPS2"), current_user_roles=["R_ADMIN"])
    assert custom.role_code == "R_OPS2"
    assert session.committed == 2


def test_create_role_route_passes_current_principal() -> None:
    from app.api.routes import system_manage as routes

    assert "current_user_roles" in inspect.getsource(routes.create_role)
    assert "current_user_id" in inspect.getsource(routes.update_menu_permission)


# --- L7 权限编码改名 ---------------------------------------------------------


@pytest.mark.asyncio
async def test_non_super_cannot_rename_permission_code_into_one_they_lack() -> None:
    service, session = _service(actor_codes=["device_monitor:history:view"])
    permission = _Permission(5, "device_monitor:history:view")
    service._get_menu_or_raise = _async_return(object())
    service._get_permission_or_raise = _async_return(permission)
    with pytest.raises(ApiError) as excinfo:
        await service.update_menu_permission(
            1, 5, _permission_payload("system:role:grant"), current_user_id=7, current_user_roles=["R_ADMIN"]
        )
    assert excinfo.value.code == 403
    assert permission.permission_code == "device_monitor:history:view"
    assert session.committed == 0


@pytest.mark.asyncio
async def test_non_super_cannot_rename_permission_code_they_do_not_hold() -> None:
    """先把高权限改名腾出编码这一步同样要求持有旧编码。"""
    service, session = _service(actor_codes=["system:menu:update"])
    permission = _Permission(6, "system:role:grant")
    service._get_menu_or_raise = _async_return(object())
    service._get_permission_or_raise = _async_return(permission)
    with pytest.raises(ApiError) as excinfo:
        await service.update_menu_permission(
            1, 6, _permission_payload("system:menu:update"), current_user_id=7, current_user_roles=["R_ADMIN"]
        )
    assert excinfo.value.code == 403
    assert session.committed == 0


@pytest.mark.asyncio
async def test_permission_code_rename_allowed_when_holding_both_or_super() -> None:
    service, session = _service(actor_codes=["a:b:c", "a:b:d"])
    permission = _Permission(7, "a:b:c")
    service._get_menu_or_raise = _async_return(object())
    service._get_permission_or_raise = _async_return(permission)
    await service.update_menu_permission(
        1, 7, _permission_payload("a:b:d"), current_user_id=7, current_user_roles=["R_ADMIN"]
    )
    assert permission.permission_code == "a:b:d"

    service2, _ = _service(actor_codes=[])
    permission2 = _Permission(8, "x:y:z")
    service2._get_menu_or_raise = _async_return(object())
    service2._get_permission_or_raise = _async_return(permission2)
    await service2.update_menu_permission(
        1, 8, _permission_payload("system:role:grant"), current_user_id=1, current_user_roles=["R_SUPER"]
    )
    assert permission2.permission_code == "system:role:grant"
