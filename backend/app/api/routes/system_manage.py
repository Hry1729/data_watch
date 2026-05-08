from __future__ import annotations

from typing import Any

from turboapi import APIRouter, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.exceptions import ApiError
from app.core.log_writer import write_system_log
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
from app.services.auth import AuthService
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
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
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
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
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
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    user_payload = UserPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.create_user(user_payload, actor=principal["user_name"])
        return success_response(data=data)


@router.put("/api/user/{user_id}")
async def update_user(
    user_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    user_payload = UserPayload.from_dict(payload, require_user_name=False)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_user(user_id, user_payload, actor=principal["user_name"])
        return success_response(data=data)


@router.patch("/api/user/{user_id}/status")
async def update_user_status(
    user_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    status_payload = UserStatusPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.update_user_status(
            user_id,
            status=status_payload.status,
            actor=principal["user_name"],
        )
        return success_response(data=data)


@router.patch("/api/user/{user_id}/password/reset")
async def reset_user_password(
    user_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    password_payload = PasswordResetPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.reset_user_password(
            user_id,
            password_payload,
            actor=principal["user_name"],
        )
        return success_response(data=data)


@router.delete("/api/user/{user_id}")
async def delete_user(
    user_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.delete_user(user_id, actor=principal["user_name"])
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
            _authorization_from_credentials(credentials)
        )
        return success_response(data=data)


@router.get("/api/v3/system/menus/admin")
async def get_admin_menus(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
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
    await _require_roles(credentials, {"R_SUPER"})
    if menu_id <= 0:
        raise ApiError(400, "menu_id must be a positive integer")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = SystemManageService(session)
        data = await service.delete_menu_permission(permission_id)
        return success_response(data=data)


async def _require_roles(
    credentials: HTTPAuthorizationCredentials | None,
    required_roles: set[str],
) -> dict[str, Any]:
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        principal = await AuthService(session).resolve_principal(
            _authorization_from_credentials(credentials)
        )
    if not required_roles.intersection(set(principal["roles"])):
        # 记录权限拒绝日志
        await write_system_log(
            level="WARN",
            log_type="security",
            source="backend.permission",
            content=f"权限拒绝: 用户 {principal['user_name']} 尝试访问需要 {required_roles} 权限的接口",
            detail={
                "user_id": principal["user_id"],
                "user_name": principal["user_name"],
                "user_roles": principal["roles"],
                "required_roles": list(required_roles),
            },
        )
        raise ApiError(403, "无权限访问该接口")
    return principal


def _authorization_from_credentials(
    credentials: HTTPAuthorizationCredentials | None,
) -> str | None:
    if credentials is None:
        return None
    return f"{credentials.scheme} {credentials.credentials}".strip()


def _compact_params(params: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in params.items() if value is not None}
