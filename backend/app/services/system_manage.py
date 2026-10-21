from __future__ import annotations

from collections import defaultdict
from datetime import datetime
from typing import Any

from sqlalchemy import delete, func, select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError
from app.core.password_worker import PasswordWorkerBusy, run_password_op
from app.core.security import generate_temporary_password, hash_password
from app.models import (
    SysDataPermission,
    SysMenu,
    SysPermission,
    SysRole,
    SysRoleMenu,
    SysRolePermission,
    SysUser,
    SysUserRole,
)
from app.schemas.auth import MenuPayload, PasswordResetPayload, PermissionPayload, RoleGrantPayload, RolePayload, UserPayload
from app.services.auth import AuthService

# 内置角色编码：节点管理、导出文件访问等按编码授权（L6），编码本身不得被改名或被其他角色占用。
BUILTIN_ROLE_CODES = frozenset({"R_SUPER", "R_ADMIN", "R_USER"})


class SystemManageService:
    def __init__(self, session: AsyncSession):
        self.session = session
        self.auth_service = AuthService(session)

    async def list_users(self, params: dict[str, Any]) -> dict[str, Any]:
        current, size = _normalize_pagination(params)
        stmt = select(SysUser).where(SysUser.deleted.is_(False))

        if params.get("userName"):
            stmt = stmt.where(SysUser.user_name.like(f"%{str(params['userName']).strip()}%"))
        if params.get("userPhone"):
            stmt = stmt.where(SysUser.phone.like(f"%{str(params['userPhone']).strip()}%"))
        if params.get("userEmail"):
            stmt = stmt.where(SysUser.email.like(f"%{str(params['userEmail']).strip()}%"))
        if params.get("userGender"):
            stmt = stmt.where(SysUser.gender == str(params["userGender"]).strip())
        if params.get("status"):
            stmt = stmt.where(SysUser.status == str(params["status"]).strip())

        total = await self.session.scalar(select(func.count()).select_from(stmt.subquery())) or 0
        rows = (
            await self.session.execute(
                stmt.order_by(SysUser.id.desc()).offset((current - 1) * size).limit(size)
            )
        ).scalars().all()
        role_map = await self._load_user_role_map([row.id for row in rows])
        return {
            "records": [self._serialize_user(row, role_map.get(row.id, [])) for row in rows],
            "current": current,
            "size": size,
            "total": int(total),
        }

    async def get_user(self, user_id: int) -> dict[str, Any]:
        user = await self._get_user_or_raise(user_id)
        role_map = await self._load_user_role_map([user.id])
        return self._serialize_user(user, role_map.get(user.id, []))

    async def create_user(
        self,
        payload: UserPayload,
        *,
        actor: str,
        current_user_roles: list[str],
    ) -> dict[str, Any]:
        # B038：user_name 在库里全局唯一（含软删除行），软删用户的用户名同样不可复用，
        # 否则业务校验放行后 INSERT 撞唯一约束返回 500。
        existing = await self.session.scalar(select(SysUser).where(SysUser.user_name == payload.user_name))
        if existing is not None:
            raise ApiError(400, "用户名已存在（含已删除用户）" if existing.deleted else "用户名已存在")

        current_is_super = self._is_current_super(current_user_roles)
        role_ids = await self._resolve_role_ids(payload.user_roles)
        if not current_is_super:
            default_role_id = await self._get_required_role_id("R_USER")
            if role_ids and set(role_ids) != {default_role_id}:
                raise ApiError(403, "只有超级管理员可以指定用户角色")
            role_ids = [default_role_id]
        if await self._role_ids_include_super(role_ids) and not current_is_super:
            raise ApiError(403, "只有超级管理员可以分配超级管理员角色")
        temporary_password = generate_temporary_password()
        # PBKDF2 离开事件循环；先算出哈希再写库，worker 繁忙时不留下半完成用户。
        try:
            password_hash = await run_password_op(hash_password, temporary_password)
        except PasswordWorkerBusy as exc:
            raise ApiError(503, "认证服务繁忙，请稍后重试") from exc
        user = SysUser(
            user_name=payload.user_name,
            password_hash=password_hash,
            token_version=0,
            must_change_password=True,
            nick_name=payload.nick_name or payload.user_name,
            email=payload.user_email,
            phone=payload.user_phone,
            gender=payload.user_gender,
            avatar=payload.avatar,
            status=payload.status,
            create_by=actor,
            update_by=actor,
            deleted=False,
        )
        self.session.add(user)
        await self.session.flush()
        for role_id in role_ids:
            self.session.add(SysUserRole(user_id=user.id, role_id=role_id))
        await self.session.commit()
        return {
            "id": user.id,
            "userId": user.id,
            "temporaryPassword": temporary_password,
            "mustChangePassword": True,
            "tokenVersion": 0,
        }

    async def update_user(
        self,
        user_id: int,
        payload: UserPayload,
        *,
        actor: str,
        current_user_id: int,
        current_user_roles: list[str],
    ) -> dict[str, Any]:
        user = await self._get_user_or_raise(user_id)
        if payload.user_name and payload.user_name != user.user_name:
            duplicate = await self.session.scalar(select(SysUser).where(SysUser.user_name == payload.user_name))
            # B038：软删除行仍占用唯一约束，同样视为重名。
            if duplicate is not None and duplicate.id != user.id:
                raise ApiError(400, "用户名已存在（含已删除用户）" if duplicate.deleted else "用户名已存在")
            user.user_name = payload.user_name

        role_ids = await self._resolve_role_ids(payload.user_roles)
        current_role_ids = await self._load_user_role_ids(user_id)
        target_is_super = await self._user_has_role(user_id, "R_SUPER")
        keeps_super_role = await self._role_ids_include_super(role_ids)
        current_is_super = self._is_current_super(current_user_roles)
        if not current_is_super and set(role_ids) != current_role_ids:
            raise ApiError(403, "只有超级管理员可以修改用户角色")
        if (target_is_super or keeps_super_role) and not current_is_super:
            raise ApiError(403, "只有超级管理员可以管理超级管理员用户")
        if user_id == current_user_id and payload.status == "2":
            raise ApiError(400, "不能禁用当前登录用户")
        if target_is_super and payload.status != "1":
            raise ApiError(400, "超级管理员用户不能禁用")
        if target_is_super and user.user_name == "Super" and not keeps_super_role:
            raise ApiError(400, "内置超级管理员不能移除超级管理员角色")
        if target_is_super and user_id == current_user_id and not keeps_super_role:
            raise ApiError(400, "不能移除当前登录用户的超级管理员角色")

        user.nick_name = payload.nick_name or user.user_name
        user.email = payload.user_email
        user.phone = payload.user_phone
        user.gender = payload.user_gender
        user.avatar = payload.avatar
        user.status = payload.status
        user.update_by = actor
        await self.session.execute(delete(SysUserRole).where(SysUserRole.user_id == user.id))
        for role_id in role_ids:
            self.session.add(SysUserRole(user_id=user.id, role_id=role_id))
        await self.session.commit()
        return {"id": user.id}

    async def update_user_status(
        self,
        user_id: int,
        *,
        status: str,
        actor: str,
        current_user_id: int,
        current_user_roles: list[str],
    ) -> dict[str, int]:
        if user_id == current_user_id and status == "2":
            raise ApiError(400, "不能禁用当前登录用户")
        target_is_super = await self._user_has_role(user_id, "R_SUPER")
        if target_is_super and not self._is_current_super(current_user_roles):
            raise ApiError(403, "只有超级管理员可以管理超级管理员用户")
        if target_is_super and status != "1":
            raise ApiError(400, "超级管理员用户不能禁用")
        user = await self._get_user_or_raise(user_id)
        user.status = status
        user.update_by = actor
        await self.session.commit()
        return {"id": user.id}

    async def reset_user_password(
        self,
        user_id: int,
        payload: PasswordResetPayload,
        *,
        actor: str,
        current_user_roles: list[str],
    ) -> dict[str, Any]:
        if await self._user_has_role(user_id, "R_SUPER") and not self._is_current_super(current_user_roles):
            raise ApiError(403, "只有超级管理员可以重置超级管理员密码")
        user = await self._get_user_or_raise(user_id)
        temporary_password = generate_temporary_password()
        # 与新建用户相同：哈希完成后再改密码字段，超载时不提交。
        try:
            user.password_hash = await run_password_op(hash_password, temporary_password)
        except PasswordWorkerBusy as exc:
            raise ApiError(503, "认证服务繁忙，请稍后重试") from exc
        user.must_change_password = True
        user.token_version = int(getattr(user, "token_version", 0) or 0) + 1
        user.update_by = actor
        await self.session.commit()
        return {
            "id": user.id,
            "userId": user.id,
            "temporaryPassword": temporary_password,
            "mustChangePassword": True,
            "tokenVersion": user.token_version,
        }

    async def delete_user(
        self,
        user_id: int,
        *,
        actor: str,
        current_user_id: int,
        current_user_roles: list[str],
    ) -> dict[str, int]:
        if user_id == current_user_id:
            raise ApiError(400, "不能删除当前登录用户")
        user = await self._get_user_or_raise(user_id)
        if await self._user_has_role(user_id, "R_SUPER"):
            raise ApiError(400, "超级管理员用户不能删除")
        user.deleted = True
        user.status = "2"
        user.update_by = actor
        await self.session.execute(delete(SysUserRole).where(SysUserRole.user_id == user.id))
        await self.session.commit()
        return {"id": user.id}

    async def list_roles(self, params: dict[str, Any]) -> dict[str, Any]:
        current, size = _normalize_pagination(params)
        stmt = select(SysRole)
        if params.get("roleName"):
            stmt = stmt.where(SysRole.role_name.like(f"%{str(params['roleName']).strip()}%"))
        if params.get("roleCode"):
            stmt = stmt.where(SysRole.role_code.like(f"%{str(params['roleCode']).strip()}%"))
        if params.get("description"):
            stmt = stmt.where(SysRole.description.like(f"%{str(params['description']).strip()}%"))
        if params.get("enabled") not in (None, ""):
            stmt = stmt.where(SysRole.enabled.is_(bool(_coerce_bool(params.get("enabled")))))
        if params.get("startTime"):
            stmt = stmt.where(SysRole.create_time >= _parse_datetime(str(params["startTime"])))
        if params.get("endTime"):
            stmt = stmt.where(SysRole.create_time <= _parse_datetime(str(params["endTime"])))

        total = await self.session.scalar(select(func.count()).select_from(stmt.subquery())) or 0
        rows = (
            await self.session.execute(
                stmt.order_by(SysRole.sort.asc(), SysRole.id.asc()).offset((current - 1) * size).limit(size)
            )
        ).scalars().all()
        return {
            "records": [self._serialize_role(row) for row in rows],
            "current": current,
            "size": size,
            "total": int(total),
        }

    async def get_role(self, role_id: int) -> dict[str, Any]:
        role = await self._get_role_or_raise(role_id)
        return self._serialize_role(role)

    async def create_role(self, payload: RolePayload, *, current_user_roles: list[str]) -> dict[str, int]:
        if payload.role_code in BUILTIN_ROLE_CODES and not self._is_current_super(current_user_roles):
            raise ApiError(403, "只有超级管理员可以使用内置角色编码")
        duplicate = await self.session.scalar(select(SysRole).where(SysRole.role_code == payload.role_code))
        if duplicate is not None:
            raise ApiError(400, "角色编码已存在")
        role = SysRole(
            role_code=payload.role_code,
            role_name=payload.role_name,
            description=payload.description,
            enabled=payload.enabled,
            sort=100,
        )
        self.session.add(role)
        await self.session.commit()
        return {"id": role.id}

    async def update_role(
        self, role_id: int, payload: RolePayload, *, current_user_roles: list[str]
    ) -> dict[str, int]:
        role = await self._get_role_or_raise(role_id)
        await self._assert_can_manage_role(role, current_user_roles=current_user_roles)
        if payload.role_code != role.role_code:
            # 内置角色编码不允许改名（L6）：否则可以腾出 R_ADMIN 等编码再让自定义角色占用。
            if role.role_code in BUILTIN_ROLE_CODES:
                raise ApiError(403, "内置角色编码不能修改")
            # 不允许把别的角色改名成 R_SUPER（B21）：编码唯一，一旦成功即等于获得超级身份。
            if payload.role_code == "R_SUPER" and not self._is_current_super(current_user_roles):
                raise ApiError(403, "只有超级管理员可以授予超级管理员角色")
            if payload.role_code in BUILTIN_ROLE_CODES and not self._is_current_super(current_user_roles):
                raise ApiError(403, "只有超级管理员可以使用内置角色编码")
            duplicate = await self.session.scalar(select(SysRole).where(SysRole.role_code == payload.role_code))
            if duplicate is not None and duplicate.id != role.id:
                raise ApiError(400, "角色编码已存在")
        # B007：通用编辑与状态接口共用同一约束，R_SUPER 不能被禁用。
        _assert_role_can_be_disabled(role, payload.enabled)
        role.role_name = payload.role_name
        role.role_code = payload.role_code
        role.description = payload.description
        role.enabled = payload.enabled
        await self.session.commit()
        return {"id": role.id}

    async def update_role_status(
        self, role_id: int, *, enabled: bool, current_user_roles: list[str]
    ) -> dict[str, int]:
        role = await self._get_role_or_raise(role_id)
        await self._assert_can_manage_role(role, current_user_roles=current_user_roles)
        _assert_role_can_be_disabled(role, enabled)
        role.enabled = enabled
        await self.session.commit()
        return {"id": role.id}

    async def delete_role(self, role_id: int, *, current_user_roles: list[str]) -> dict[str, int]:
        role = await self._get_role_or_raise(role_id)
        await self._assert_can_manage_role(role, current_user_roles=current_user_roles)
        if role.role_code == "R_SUPER":
            raise ApiError(400, "超级管理员角色不能删除")
        in_use = await self.session.scalar(
            select(func.count()).select_from(SysUserRole).where(SysUserRole.role_id == role.id)
        )
        if in_use:
            raise ApiError(400, "该角色仍被用户占用，无法删除")
        await self.session.execute(delete(SysRoleMenu).where(SysRoleMenu.role_id == role.id))
        await self.session.execute(delete(SysRolePermission).where(SysRolePermission.role_id == role.id))
        await self.session.execute(delete(SysDataPermission).where(SysDataPermission.role_id == role.id))
        await self.session.delete(role)
        await self.session.commit()
        return {"id": role_id}

    async def list_role_options(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(select(SysRole).where(SysRole.enabled.is_(True)).order_by(SysRole.sort.asc(), SysRole.id.asc()))
        ).scalars().all()
        return [
            {
                "roleId": row.id,
                "roleName": row.role_name,
                "roleCode": row.role_code,
                "enabled": row.enabled,
            }
            for row in rows
        ]

    async def get_role_grant(self, role_id: int) -> dict[str, Any]:
        await self._get_role_or_raise(role_id)
        menu_ids = (
            await self.session.execute(select(SysRoleMenu.menu_id).where(SysRoleMenu.role_id == role_id))
        ).scalars().all()
        permission_ids = (
            await self.session.execute(
                select(SysRolePermission.permission_id).where(SysRolePermission.role_id == role_id)
            )
        ).scalars().all()
        data_scopes = (
            await self.session.execute(select(SysDataPermission).where(SysDataPermission.role_id == role_id))
        ).scalars().all()
        return {
            "menuIds": list(menu_ids),
            "permissionIds": list(permission_ids),
            "dataScopes": [
                {"id": item.id, "scopeType": item.scope_type, "scopeValue": item.scope_value, "enabled": item.enabled}
                for item in data_scopes
            ],
        }

    async def update_role_grant(
        self,
        role_id: int,
        payload: RoleGrantPayload,
        *,
        current_user_id: int,
        current_user_roles: list[str],
    ) -> dict[str, int]:
        role = await self._get_role_or_raise(role_id)
        await self._assert_can_manage_role(role, current_user_roles=current_user_roles)
        # 纵深防御：即使绕过 from_dict 直接构造 payload，也不允许无确认地清空全部授权。
        if not payload.menu_ids and not payload.permission_ids and not payload.clear_all:
            raise ApiError(400, "清空角色全部菜单和权限需要显式携带 clearAll: true")
        menu_ids = await self._expand_menu_ids(payload.menu_ids)
        explicit_permission_ids = await self._resolve_permission_ids(payload.permission_ids)
        page_permission_ids = await self._resolve_page_permission_ids(menu_ids)
        permission_ids = sorted(set(explicit_permission_ids).union(page_permission_ids))
        await self._assert_can_grant_permissions(
            permission_ids,
            current_user_id=current_user_id,
            current_user_roles=current_user_roles,
        )
        await self.session.execute(delete(SysRoleMenu).where(SysRoleMenu.role_id == role_id))
        await self.session.execute(delete(SysRolePermission).where(SysRolePermission.role_id == role_id))
        # B021：请求未携带 dataScopes 时保留现有数据范围，不再静默清空。
        if payload.data_scopes is not None:
            await self.session.execute(delete(SysDataPermission).where(SysDataPermission.role_id == role_id))
        for menu_id in sorted(set(menu_ids)):
            self.session.add(SysRoleMenu(role_id=role_id, menu_id=menu_id))
        for permission_id in sorted(set(permission_ids)):
            self.session.add(SysRolePermission(role_id=role_id, permission_id=permission_id))
        for scope in payload.data_scopes or []:
            # B22：数据范围目前**只有声明、没有服务端执行**（无 UI、无文档承诺，
            # 内置角色一律种为 all/*）。若静默接受收窄范围，管理员会以为该角色已被
            # 限制、实际却没有——这是把未实现写成已生效。因此只接受"不受限"形式，
            # 收窄形式显式拒绝，避免形成虚假的权限印象。
            scope_type = str(scope.get("scopeType") or scope.get("scope_type") or "all").strip()
            scope_value = str(scope.get("scopeValue") or scope.get("scope_value") or "*").strip()
            if scope_type != "all":
                raise ApiError(
                    501,
                    "数据范围收窄尚未实现：当前版本不会据此限制实时/历史读取，"
                    "因此拒绝接受非 all 的范围声明（以免误以为该角色已被限制）。"
                    "如需限制范围，请先确认产品需求。",
                )
            self.session.add(
                SysDataPermission(
                    role_id=role_id,
                    scope_type=scope_type,
                    scope_value=scope_value or "*",
                    enabled=bool(scope.get("enabled", True)),
                )
            )
        await self.session.commit()
        return {"id": role_id}

    async def get_menu_tree_for_current_user(self, authorization: str | None) -> list[dict[str, Any]]:
        principal = await self.auth_service.resolve_principal(authorization)
        menu_ids = (
            await self.session.execute(
                select(SysRoleMenu.menu_id)
                .join(SysUserRole, SysUserRole.role_id == SysRoleMenu.role_id)
                .join(SysRole, SysRole.id == SysUserRole.role_id)
                .where(SysUserRole.user_id == principal["user_id"])
                .where(SysRole.enabled.is_(True))
            )
        ).scalars().all()
        permission_rows = (
            await self.session.execute(
                select(SysPermission)
                .join(SysRolePermission, SysRolePermission.permission_id == SysPermission.id)
                .join(SysUserRole, SysUserRole.role_id == SysRolePermission.role_id)
                .join(SysRole, SysRole.id == SysUserRole.role_id)
                .where(SysUserRole.user_id == principal["user_id"])
                .where(SysPermission.enabled.is_(True))
                .where(SysRole.enabled.is_(True))
            )
        ).scalars().all()
        return await self._build_menu_tree(allowed_menu_ids=set(menu_ids), permissions=permission_rows)

    async def get_menu_tree_admin(self) -> list[dict[str, Any]]:
        """返回管理员菜单树，包含所有菜单（含禁用菜单）和所有权限"""
        permissions = (await self.session.execute(select(SysPermission))).scalars().all()
        return await self._build_menu_tree_admin(permissions=permissions)

    async def create_menu(self, payload: MenuPayload) -> dict[str, int]:
        duplicate = await self.session.scalar(select(SysMenu).where(SysMenu.name == payload.name))
        if duplicate is not None:
            raise ApiError(400, "菜单名称已存在")
        if payload.parent_id is not None:
            await self._get_menu_or_raise(payload.parent_id)
        menu = SysMenu(
            name=payload.name,
            parent_id=payload.parent_id,
            path=payload.path,
            component=payload.component,
            title_key=payload.title_key,
            icon=payload.icon,
            menu_type=payload.menu_type,
            link=payload.link,
            is_iframe=payload.is_iframe,
            keep_alive=payload.keep_alive,
            is_hide=payload.is_hide,
            is_hide_tab=payload.is_hide_tab,
            fixed_tab=payload.fixed_tab,
            active_path=payload.active_path,
            is_full_page=payload.is_full_page,
            sort=payload.sort,
            enabled=payload.enabled,
        )
        self.session.add(menu)
        await self.session.commit()
        return {"id": menu.id}

    async def update_menu(self, menu_id: int, payload: MenuPayload) -> dict[str, int]:
        menu = await self._get_menu_or_raise(menu_id)
        if payload.name != menu.name:
            duplicate = await self.session.scalar(select(SysMenu).where(SysMenu.name == payload.name))
            if duplicate is not None and duplicate.id != menu.id:
                raise ApiError(400, "菜单名称已存在")
        if payload.parent_id == menu.id:
            raise ApiError(400, "parentId cannot equal menu id")
        if payload.parent_id is not None:
            await self._get_menu_or_raise(payload.parent_id)
            await self._assert_no_menu_cycle(menu.id, payload.parent_id)
        menu.name = payload.name
        menu.parent_id = payload.parent_id
        menu.path = payload.path
        menu.component = payload.component
        menu.title_key = payload.title_key
        menu.icon = payload.icon
        menu.menu_type = payload.menu_type
        menu.link = payload.link
        menu.is_iframe = payload.is_iframe
        menu.keep_alive = payload.keep_alive
        menu.is_hide = payload.is_hide
        menu.is_hide_tab = payload.is_hide_tab
        menu.fixed_tab = payload.fixed_tab
        menu.active_path = payload.active_path
        menu.is_full_page = payload.is_full_page
        menu.sort = payload.sort
        menu.enabled = payload.enabled
        await self.session.commit()
        return {"id": menu.id}

    async def delete_menu(self, menu_id: int) -> dict[str, int]:
        menu = await self._get_menu_or_raise(menu_id)
        # 检查是否存在子菜单
        child_count = await self.session.scalar(
            select(func.count()).select_from(SysMenu).where(SysMenu.parent_id == menu.id)
        )
        if child_count and child_count > 0:
            raise ApiError(400, "请先删除子菜单")
        # 删除角色菜单关联
        await self.session.execute(delete(SysRoleMenu).where(SysRoleMenu.menu_id == menu.id))
        # 查找该菜单下的权限
        permission_ids = (
            await self.session.execute(
                select(SysPermission.id).where(SysPermission.menu_id == menu.id)
            )
        ).scalars().all()
        if permission_ids:
            # 删除角色权限关联
            await self.session.execute(
                delete(SysRolePermission).where(SysRolePermission.permission_id.in_(permission_ids))
            )
            # 删除权限
            await self.session.execute(delete(SysPermission).where(SysPermission.menu_id == menu.id))
        await self.session.delete(menu)
        await self.session.commit()
        return {"id": menu_id}

    async def list_menu_permissions(self, menu_id: int) -> list[dict[str, Any]]:
        await self._get_menu_or_raise(menu_id)
        rows = (
            await self.session.execute(
                select(SysPermission)
                .where(SysPermission.menu_id == menu_id)
                .order_by(SysPermission.sort.asc(), SysPermission.id.asc())
            )
        ).scalars().all()
        return [self._serialize_permission(row) for row in rows]

    async def create_menu_permission(self, menu_id: int, payload: PermissionPayload) -> dict[str, int]:
        await self._get_menu_or_raise(menu_id)
        duplicate = await self.session.scalar(
            select(SysPermission).where(SysPermission.permission_code == payload.permission_code)
        )
        if duplicate is not None:
            raise ApiError(400, "权限编码已存在")
        permission = SysPermission(
            permission_code=payload.permission_code,
            permission_name=payload.permission_name,
            permission_type=payload.permission_type,
            menu_id=menu_id,
            auth_mark=payload.auth_mark,
            description=payload.description,
            sort=payload.sort,
            enabled=payload.enabled,
        )
        self.session.add(permission)
        await self.session.commit()
        return {"id": permission.id}

    async def update_menu_permission(
        self,
        menu_id: int,
        permission_id: int,
        payload: PermissionPayload,
        *,
        current_user_id: int,
        current_user_roles: list[str],
    ) -> dict[str, int]:
        await self._get_menu_or_raise(menu_id)
        permission = await self._get_permission_or_raise(permission_id)
        if payload.permission_code != permission.permission_code:
            # L7：改权限编码等于改变"谁持有它"。非超级管理员必须同时持有改前、改后两个编码，
            # 否则可以把自己已持有的权限改名成任意高权限码，绕开 _assert_can_grant_permissions。
            if not self._is_current_super(current_user_roles):
                granted = set(await self.auth_service.fetch_user_permission_codes(current_user_id))
                missing = sorted(
                    code for code in (permission.permission_code, payload.permission_code) if code not in granted
                )
                if missing:
                    raise ApiError(403, "修改权限编码需要同时持有改前和改后的权限: " + ", ".join(missing))
            duplicate = await self.session.scalar(
                select(SysPermission).where(SysPermission.permission_code == payload.permission_code)
            )
            if duplicate is not None and duplicate.id != permission.id:
                raise ApiError(400, "权限编码已存在")
        permission.permission_code = payload.permission_code
        permission.permission_name = payload.permission_name
        permission.permission_type = payload.permission_type
        permission.menu_id = menu_id
        permission.auth_mark = payload.auth_mark
        permission.description = payload.description
        permission.sort = payload.sort
        permission.enabled = payload.enabled
        await self.session.commit()
        return {"id": permission.id}

    async def delete_menu_permission(self, permission_id: int) -> dict[str, int]:
        permission = await self._get_permission_or_raise(permission_id)
        await self.session.execute(delete(SysRolePermission).where(SysRolePermission.permission_id == permission.id))
        await self.session.delete(permission)
        await self.session.commit()
        return {"id": permission_id}

    async def _build_menu_tree(
        self,
        *,
        allowed_menu_ids: set[int] | None,
        permissions: list[SysPermission],
    ) -> list[dict[str, Any]]:
        menu_stmt = select(SysMenu).where(SysMenu.enabled.is_(True)).order_by(SysMenu.sort.asc(), SysMenu.id.asc())
        menus = (await self.session.execute(menu_stmt)).scalars().all()
        if allowed_menu_ids is not None:
            menus = [menu for menu in menus if menu.id in allowed_menu_ids]

        permission_map: dict[int, list[dict[str, Any]]] = defaultdict(list)
        for permission in permissions:
            if permission.menu_id is None or permission.permission_type != "action":
                continue
            if allowed_menu_ids is not None and permission.menu_id not in allowed_menu_ids:
                continue
            permission_map[permission.menu_id].append(
                {
                    "id": permission.id,
                    "title": permission.permission_name,
                    "authMark": permission.auth_mark,
                }
            )

        node_map: dict[int, dict[str, Any]] = {}
        roots: list[dict[str, Any]] = []
        for menu in menus:
            node_map[menu.id] = {
                "id": menu.id,
                "path": menu.path,
                "name": menu.name,
                "component": menu.component,
                "meta": {
                    "title": menu.title_key,
                    "icon": menu.icon,
                    "keepAlive": menu.keep_alive,
                    "isHide": menu.is_hide,
                    "isHideTab": menu.is_hide_tab,
                    "link": menu.link,
                    "isIframe": menu.is_iframe,
                    "fixedTab": menu.fixed_tab,
                    "activePath": menu.active_path,
                    "isFullPage": menu.is_full_page,
                    "authList": permission_map.get(menu.id, []),
                },
                "children": [],
            }

        for menu in menus:
            node = node_map[menu.id]
            if menu.parent_id and menu.parent_id in node_map:
                node_map[menu.parent_id]["children"].append(node)
            else:
                roots.append(node)

        for node in node_map.values():
            if not node["children"]:
                node.pop("children")
            if not node["meta"]["authList"]:
                node["meta"].pop("authList")
        return roots

    async def _build_menu_tree_admin(self, *, permissions: list[SysPermission]) -> list[dict[str, Any]]:
        """构建管理员菜单树，返回所有菜单（含禁用菜单）"""
        menus = (
            await self.session.execute(select(SysMenu).order_by(SysMenu.sort.asc(), SysMenu.id.asc()))
        ).scalars().all()

        permission_map: dict[int, list[dict[str, Any]]] = defaultdict(list)
        for permission in permissions:
            if permission.menu_id is None:
                continue
            permission_map[permission.menu_id].append(
                {
                    "id": permission.id,
                    "title": permission.permission_name,
                    "authMark": permission.auth_mark,
                    "enabled": permission.enabled,
                    "permissionCode": permission.permission_code,
                    "permissionType": permission.permission_type,
                    "sort": permission.sort,
                }
            )

        node_map: dict[int, dict[str, Any]] = {}
        roots: list[dict[str, Any]] = []
        for menu in menus:
            node_map[menu.id] = {
                "id": menu.id,
                "parentId": menu.parent_id,
                "path": menu.path,
                "name": menu.name,
                "component": menu.component,
                "menuType": menu.menu_type,
                "sort": menu.sort,
                "enabled": menu.enabled,
                "meta": {
                    "title": menu.title_key,
                    "icon": menu.icon,
                    "keepAlive": menu.keep_alive,
                    "isHide": menu.is_hide,
                    "isHideTab": menu.is_hide_tab,
                    "link": menu.link,
                    "isIframe": menu.is_iframe,
                    "fixedTab": menu.fixed_tab,
                    "activePath": menu.active_path,
                    "isFullPage": menu.is_full_page,
                    "authList": permission_map.get(menu.id, []),
                },
                "children": [],
            }

        for menu in menus:
            node = node_map[menu.id]
            if menu.parent_id and menu.parent_id in node_map:
                node_map[menu.parent_id]["children"].append(node)
            else:
                roots.append(node)

        for node in node_map.values():
            if not node["children"]:
                node.pop("children")
            if not node["meta"]["authList"]:
                node["meta"].pop("authList")
        return roots

    async def _load_user_role_map(self, user_ids: list[int]) -> dict[int, list[str]]:
        if not user_ids:
            return {}
        rows = (
            await self.session.execute(
                select(SysUserRole.user_id, SysRole.role_code)
                .join(SysRole, SysRole.id == SysUserRole.role_id)
                .where(SysUserRole.user_id.in_(user_ids))
                .order_by(SysRole.sort.asc(), SysRole.id.asc())
            )
        ).all()
        result: dict[int, list[str]] = defaultdict(list)
        for user_id, role_code in rows:
            result[int(user_id)].append(str(role_code))
        return result

    async def _resolve_role_ids(self, role_codes: list[str]) -> list[int]:
        if not role_codes:
            return []
        rows = (
            await self.session.execute(select(SysRole).where(SysRole.role_code.in_(role_codes)))
        ).scalars().all()
        found = {row.role_code: row.id for row in rows}
        missing = [role_code for role_code in role_codes if role_code not in found]
        if missing:
            raise ApiError(400, f"角色不存在: {', '.join(missing)}")
        return [found[role_code] for role_code in role_codes]

    async def _resolve_permission_ids(self, permission_ids: list[int]) -> list[int]:
        if not permission_ids:
            return []
        rows = (
            await self.session.execute(select(SysPermission.id).where(SysPermission.id.in_(permission_ids)))
        ).scalars().all()
        found = set(int(item) for item in rows)
        missing = [str(permission_id) for permission_id in permission_ids if permission_id not in found]
        if missing:
            raise ApiError(400, f"权限不存在: {', '.join(missing)}")
        return permission_ids

    async def _resolve_page_permission_ids(self, menu_ids: list[int]) -> list[int]:
        if not menu_ids:
            return []
        rows = (
            await self.session.execute(
                select(SysPermission.id)
                .where(SysPermission.menu_id.in_(menu_ids))
                .where(SysPermission.permission_type == "page")
                .where(SysPermission.enabled.is_(True))
            )
        ).scalars().all()
        return [int(item) for item in rows]

    async def _expand_menu_ids(self, menu_ids: list[int]) -> list[int]:
        if not menu_ids:
            return []
        menu_rows = (await self.session.execute(select(SysMenu))).scalars().all()
        by_id = {row.id: row for row in menu_rows}
        expanded = set(menu_ids)
        for menu_id in list(menu_ids):
            if menu_id not in by_id:
                raise ApiError(400, f"菜单不存在: {menu_id}")
            parent_id = by_id[menu_id].parent_id
            # B006：防御已有脏数据中的祖先环，避免同步死循环占住事件循环。
            visited = {menu_id}
            while parent_id is not None:
                if parent_id in visited:
                    raise ApiError(400, f"菜单层级存在循环: {menu_id}")
                visited.add(parent_id)
                expanded.add(parent_id)
                parent_id = by_id[parent_id].parent_id if parent_id in by_id else None
        return list(expanded)

    async def _assert_no_menu_cycle(self, menu_id: int, parent_id: int) -> None:
        """B006：parentId 不能是自身或自身后代，祖先链本身也不能已成环。"""
        menu_rows = (await self.session.execute(select(SysMenu))).scalars().all()
        parent_of = {row.id: row.parent_id for row in menu_rows}
        visited: set[int] = set()
        current: int | None = parent_id
        while current is not None:
            if current == menu_id:
                raise ApiError(400, "parentId 不能是当前菜单或其子菜单")
            if current in visited:
                raise ApiError(400, "菜单层级存在循环")
            visited.add(current)
            current = parent_of.get(current)

    async def _get_user_or_raise(self, user_id: int) -> SysUser:
        user = await self.session.scalar(select(SysUser).where(SysUser.id == user_id, SysUser.deleted.is_(False)))
        if user is None:
            raise ApiError(404, "用户不存在")
        return user

    async def _get_role_or_raise(self, role_id: int) -> SysRole:
        role = await self.session.scalar(select(SysRole).where(SysRole.id == role_id))
        if role is None:
            raise ApiError(404, "角色不存在")
        return role

    async def _get_menu_or_raise(self, menu_id: int) -> SysMenu:
        menu = await self.session.scalar(select(SysMenu).where(SysMenu.id == menu_id))
        if menu is None:
            raise ApiError(404, "菜单不存在")
        return menu

    async def _get_permission_or_raise(self, permission_id: int) -> SysPermission:
        permission = await self.session.scalar(select(SysPermission).where(SysPermission.id == permission_id))
        if permission is None:
            raise ApiError(404, "权限不存在")
        return permission

    async def _user_has_role(self, user_id: int, role_code: str) -> bool:
        role_count = await self.session.scalar(
            select(func.count())
            .select_from(SysUserRole)
            .join(SysRole, SysRole.id == SysUserRole.role_id)
            .where(SysUserRole.user_id == user_id)
            .where(SysRole.role_code == role_code)
        )
        return bool(role_count)

    async def _role_ids_include_super(self, role_ids: list[int]) -> bool:
        if not role_ids:
            return False
        super_role = await self.session.scalar(select(SysRole).where(SysRole.role_code == "R_SUPER"))
        return super_role is not None and super_role.id in role_ids

    async def _get_required_role_id(self, role_code: str) -> int:
        role = await self.session.scalar(select(SysRole).where(SysRole.role_code == role_code))
        if role is None:
            raise ApiError(500, f"系统角色不存在: {role_code}")
        return role.id

    async def _load_user_role_ids(self, user_id: int) -> set[int]:
        result = await self.session.execute(select(SysUserRole.role_id).where(SysUserRole.user_id == user_id))
        return {int(role_id) for role_id in result.scalars().all()}

    def _is_current_super(self, current_user_roles: list[str]) -> bool:
        return "R_SUPER" in set(current_user_roles)

    async def _assert_can_manage_role(self, role: SysRole, *, current_user_roles: list[str]) -> None:
        """非超级管理员不得管理内置超级角色（B21）。

        否则被授予角色管理权的用户可以改写/禁用/删除 R_SUPER，或清空它的授权，
        绕开"内置超级角色受保护"的既有约束。
        """
        if role.role_code == "R_SUPER" and not self._is_current_super(current_user_roles):
            raise ApiError(403, "只有超级管理员可以管理超级管理员角色")

    async def _assert_can_grant_permissions(
        self,
        permission_ids: list[int],
        *,
        current_user_id: int,
        current_user_roles: list[str],
    ) -> None:
        """非超级管理员只能授出**自己已持有**的操作权限（B21）。

        否则被授予角色管理权的用户可以把任意（甚至自己都没有的）权限授给自己的角色，
        形成权限提升。
        """
        if self._is_current_super(current_user_roles) or not permission_ids:
            return
        requested = set(
            (
                await self.session.execute(
                    select(SysPermission.permission_code).where(SysPermission.id.in_(permission_ids))
                )
            )
            .scalars()
            .all()
        )
        granted = set(await self.auth_service.fetch_user_permission_codes(current_user_id))
        excess = sorted(code for code in requested if code and code not in granted)
        if excess:
            raise ApiError(403, "不能授予超出你自身权限范围的操作权限: " + ", ".join(excess[:5]))

    def _serialize_user(self, user: SysUser, role_codes: list[str]) -> dict[str, Any]:
        return {
            "id": user.id,
            "avatar": user.avatar,
            "status": user.status,
            "userName": user.user_name,
            "userGender": user.gender,
            "nickName": user.nick_name,
            "userPhone": user.phone,
            "userEmail": user.email,
            "userRoles": role_codes,
            "createBy": user.create_by,
            "createTime": _format_datetime(user.create_time),
            "updateBy": user.update_by,
            "updateTime": _format_datetime(user.update_time),
        }

    def _serialize_role(self, role: SysRole) -> dict[str, Any]:
        return {
            "roleId": role.id,
            "roleName": role.role_name,
            "roleCode": role.role_code,
            "description": role.description,
            "enabled": role.enabled,
            "createTime": _format_datetime(role.create_time),
        }

    def _serialize_permission(self, permission: SysPermission) -> dict[str, Any]:
        return {
            "id": permission.id,
            "permissionCode": permission.permission_code,
            "permissionName": permission.permission_name,
            "permissionType": permission.permission_type,
            "authMark": permission.auth_mark,
            "description": permission.description,
            "sort": permission.sort,
            "enabled": permission.enabled,
            "menuId": permission.menu_id,
        }


def _assert_role_can_be_disabled(role: SysRole, enabled: bool) -> None:
    if role.role_code == "R_SUPER" and not enabled:
        raise ApiError(400, "超级管理员角色不能禁用")


def _normalize_pagination(params: dict[str, Any]) -> tuple[int, int]:
    current = int(params.get("current") or params.get("pageNo") or 1)
    size = int(params.get("size") or params.get("pageSize") or 20)
    current = max(current, 1)
    size = max(size, 1)
    return current, size


def _format_datetime(value: datetime | None) -> str:
    return value.strftime("%Y-%m-%d %H:%M:%S") if value else ""


def _parse_datetime(value: str) -> datetime:
    normalized = value.strip().replace("T", " ")
    for fmt in ("%Y-%m-%d %H:%M:%S", "%Y-%m-%d"):
        try:
            return datetime.strptime(normalized, fmt)
        except ValueError:
            continue
    raise ApiError(400, f"Invalid datetime value: {value}")


def _coerce_bool(value: Any) -> bool:
    if isinstance(value, bool):
        return value
    return str(value).strip().lower() in {"1", "true", "yes", "on"}
