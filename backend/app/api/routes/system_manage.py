from __future__ import annotations

from typing import Any

from turboapi import APIRouter, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import authorization_from_credentials, require_permissions
from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.schemas.auth import (
    MenuPayload,
    PasswordResetPayload,
    PermissionPayload,
    RoleGrantPayload,
    RolePayload,
    RoleStatusPayload,
    UserPayload,
    UserStatusPayload,
)
from app.services.system_manage import SystemManageService


router = APIRouter(tags=["system-manage"])
auth_scheme = HTTPBearer(auto_error=False)


@router.get("/api/user/list")
async def user_list(
    current: str | None = None,
    size: str | None = None,
    pageNo: str | None = None,
    pageSize: str | None = None,
    userName: str | None = None,
    userPhone: str | None = None,
    userEmail: str | None = None,
    userGender: str | None = None,
    status: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:user:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.list_users(
            _compact_params(
                {
                    "current": current,
                    "size": size,
                    "pageNo": pageNo,
                    "pageSize": pageSize,
                    "userName": userName,
                    "userPhone": userPhone,
                    "userEmail": userEmail,
                    "userGender": userGender,
                    "status": status,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/user/{user_id}")
async def user_detail(
    user_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:user:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.get_user(user_id)
        return success_response(data=data)


@router.post("/api/user")
async def create_user(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "system:user:create")
    user_payload = UserPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.create_user(
            user_payload,
            actor=principal["user_name"],
            current_user_roles=principal["roles"],
        )
        return success_response(data=data)


@router.put("/api/user/{user_id}")
async def update_user(
    user_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "system:user:update")
    user_payload = UserPayload.from_dict(payload, require_user_name=False)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_user(
            user_id,
            user_payload,
            actor=principal["user_name"],
            current_user_id=principal["user_id"],
            current_user_roles=principal["roles"],
        )
        return success_response(data=data)


@router.patch("/api/user/{user_id}/status")
async def update_user_status(
    user_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "system:user:update")
    status_payload = UserStatusPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_user_status(
            user_id,
            status=status_payload.status,
            actor=principal["user_name"],
            current_user_id=principal["user_id"],
            current_user_roles=principal["roles"],
        )
        return success_response(data=data)


@router.patch("/api/user/{user_id}/password/reset")
async def reset_user_password(
    user_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "system:user:password_reset")
    password_payload = PasswordResetPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.reset_user_password(
            user_id,
            password_payload,
            actor=principal["user_name"],
            current_user_roles=principal["roles"],
        )
        return success_response(data=data)


@router.delete("/api/user/{user_id}")
async def delete_user(
    user_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "system:user:delete")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.delete_user(
            user_id,
            actor=principal["user_name"],
            current_user_id=principal["user_id"],
            current_user_roles=principal["roles"],
        )
        return success_response(data=data)


@router.get("/api/role/list")
async def role_list(
    current: str | None = None,
    size: str | None = None,
    pageNo: str | None = None,
    pageSize: str | None = None,
    roleName: str | None = None,
    roleCode: str | None = None,
    description: str | None = None,
    enabled: str | None = None,
    startTime: str | None = None,
    endTime: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"system:role:view", "system:role:grant"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.list_roles(
            _compact_params(
                {
                    "current": current,
                    "size": size,
                    "pageNo": pageNo,
                    "pageSize": pageSize,
                    "roleName": roleName,
                    "roleCode": roleCode,
                    "description": description,
                    "enabled": enabled,
                    "startTime": startTime,
                    "endTime": endTime,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/role/options")
async def role_options(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"system:role:view", "system:user:create", "system:user:update"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.list_role_options()
        return success_response(data=data)


@router.get("/api/role/{role_id}")
async def role_detail(
    role_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:role:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.get_role(role_id)
        return success_response(data=data)


@router.post("/api/role")
async def create_role(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:role:create")
    role_payload = RolePayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.create_role(role_payload)
        return success_response(data=data)


@router.put("/api/role/{role_id}")
async def update_role(
    role_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:role:update")
    role_payload = RolePayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_role(role_id, role_payload)
        return success_response(data=data)


@router.patch("/api/role/{role_id}/status")
async def update_role_status(
    role_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:role:update")
    status_payload = RoleStatusPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_role_status(role_id, enabled=status_payload.enabled)
        return success_response(data=data)


@router.delete("/api/role/{role_id}")
async def delete_role(
    role_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:role:delete")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.delete_role(role_id)
        return success_response(data=data)


@router.get("/api/role/{role_id}/grant")
async def get_role_grant(
    role_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:role:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.get_role_grant(role_id)
        return success_response(data=data)


@router.put("/api/role/{role_id}/grant")
async def update_role_grant(
    role_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:role:grant")
    grant_payload = RoleGrantPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_role_grant(role_id, grant_payload)
        return success_response(data=data)


@router.get("/api/v3/system/menus")
async def get_current_user_menus(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.get_menu_tree_for_current_user(
            authorization_from_credentials(credentials)
        )
        return success_response(data=data)


@router.get("/api/v3/system/menus/admin")
async def get_admin_menus(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"system:menu:view", "system:role:grant"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.get_menu_tree_admin()
        return success_response(data=data)


@router.post("/api/v3/system/menu")
async def create_menu(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:menu:create")
    menu_payload = MenuPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.create_menu(menu_payload)
        return success_response(data=data)


@router.put("/api/v3/system/menu/{menu_id}")
async def update_menu(
    menu_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:menu:update")
    menu_payload = MenuPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_menu(menu_id, menu_payload)
        return success_response(data=data)


@router.delete("/api/v3/system/menu/{menu_id}")
async def delete_menu(
    menu_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:menu:delete")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.delete_menu(menu_id)
        return success_response(data=data)


@router.get("/api/v3/system/menu/{menu_id}/permissions")
async def get_menu_permissions(
    menu_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:menu:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.list_menu_permissions(menu_id)
        return success_response(data=data)


@router.post("/api/v3/system/menu/{menu_id}/permissions")
async def create_menu_permission(
    menu_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:menu:create")
    permission_payload = PermissionPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.create_menu_permission(menu_id, permission_payload)
        return success_response(data=data)


@router.put("/api/v3/system/menu/{menu_id}/permissions/{permission_id}")
async def update_menu_permission(
    menu_id: int,
    permission_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:menu:update")
    permission_payload = PermissionPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_menu_permission(menu_id, permission_id, permission_payload)
        return success_response(data=data)


@router.delete("/api/v3/system/menu/{menu_id}/permissions/{permission_id}")
async def delete_menu_permission(
    menu_id: int,
    permission_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "system:menu:delete")
    if menu_id <= 0:
        raise ApiError(400, "menu_id must be a positive integer")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.delete_menu_permission(permission_id)
        return success_response(data=data)


def _compact_params(params: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in params.items() if value is not None}
