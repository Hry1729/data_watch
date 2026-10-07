from __future__ import annotations

from datetime import datetime
from typing import Any

from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import get_settings
from app.core.exceptions import ApiError
from app.core.log_writer import write_system_log
from app.core.login_limiter import get_login_limiter
from app.core.password_worker import PasswordWorkerBusy, run_password_op
from app.core.time_utils import now_local
from app.core.security import (
    create_signed_token,
    decode_signed_token,
    hash_password,
    verify_password,
)
from app.models import SysPermission, SysRole, SysRolePermission, SysUser, SysUserRole
from app.schemas.auth import ChangePasswordPayload


class AuthService:
    def __init__(self, session: AsyncSession):
        self.session = session
        self.settings = get_settings()

    async def login(self, *, user_name: str, password: str, client_ip: str) -> dict[str, Any]:
        limiter = get_login_limiter()
        if await limiter.is_blocked(user_name, client_ip):
            raise ApiError(429, "登录失败次数过多，请稍后再试")

        user = await self._fetch_user_by_name(user_name)

        # 用户不存在或已删除
        if user is None or user.deleted:
            await write_system_log(
                level="WARN",
                log_type="auth",
                source="backend.login",
                content=f"登录失败: 用户不存在 [{user_name}]",
                detail={"user_name": user_name, "client_ip": client_ip, "reason": "user_not_found"},
            )
            await limiter.record_failure(user_name, client_ip)
            raise ApiError(401, "用户名或密码错误")

        # 账号不可用
        if user.status != "1":
            await write_system_log(
                level="WARN",
                log_type="auth",
                source="backend.login",
                content=f"登录失败: 账号已禁用 [{user_name}]",
                detail={"user_name": user_name, "user_id": user.id, "client_ip": client_ip, "reason": "account_disabled"},
            )
            raise ApiError(403, "当前账号不可用，请联系管理员")

        # 密码错误（PBKDF2 走有界线程池，超载立即 503）
        try:
            password_ok = await run_password_op(verify_password, password, user.password_hash)
        except PasswordWorkerBusy as exc:
            raise ApiError(503, "认证服务繁忙，请稍后重试") from exc
        if not password_ok:
            await limiter.record_failure(user_name, client_ip)
            await write_system_log(
                level="WARN",
                log_type="auth",
                source="backend.login",
                content=f"登录失败: 密码错误 [{user_name}]",
                detail={"user_name": user_name, "user_id": user.id, "client_ip": client_ip, "reason": "wrong_password"},
            )
            raise ApiError(401, "用户名或密码错误")

        roles = await self._fetch_user_roles(user.id)

        # 未分配角色
        if not roles:
            await write_system_log(
                level="WARN",
                log_type="auth",
                source="backend.login",
                content=f"登录失败: 未分配角色 [{user_name}]",
                detail={"user_name": user_name, "user_id": user.id, "client_ip": client_ip, "reason": "no_roles"},
            )
            raise ApiError(403, "当前账号未分配角色")

        # 登录成功
        await limiter.reset(user_name, client_ip)
        user.last_login_time = now_local()
        user.last_login_ip = client_ip
        user.update_by = user.user_name
        await self.session.commit()

        payload = {
            "uid": user.id,
            "userName": user.user_name,
            "roles": roles,
            "token_version": int(getattr(user, "token_version", 0) or 0),
            "must_change_password": bool(getattr(user, "must_change_password", False)),
        }
        access_token = create_signed_token(
            payload,
            secret_key=self.settings.auth_secret_key,
            expires_in_seconds=self.settings.auth_access_token_expire_seconds,
            token_type="access",
        )
        refresh_token = create_signed_token(
            payload,
            secret_key=self.settings.auth_secret_key,
            expires_in_seconds=self.settings.auth_refresh_token_expire_seconds,
            token_type="refresh",
        )

        # 记录登录成功日志
        await write_system_log(
            level="INFO",
            log_type="auth",
            source="backend.login",
            content=f"登录成功: {user_name}",
            detail={
                "user_id": user.id,
                "user_name": user_name,
                "roles": roles,
                "client_ip": client_ip,
            },
        )

        return {
            "token": access_token,
            "refreshToken": refresh_token,
            "userId": user.id,
            "mustChangePassword": bool(getattr(user, "must_change_password", False)),
            "tokenVersion": int(getattr(user, "token_version", 0) or 0),
        }

    async def refresh(self, refresh_token: str) -> dict[str, Any]:
        try:
            claims = decode_signed_token(
                refresh_token,
                secret_key=self.settings.auth_secret_key,
            )
        except ValueError as exc:
            raise ApiError(401, str(exc)) from exc
        if claims.get("type") != "refresh":
            raise ApiError(401, "Invalid refresh token")

        user = await self._fetch_user_by_id(int(claims.get("uid", 0)))
        if user is None or user.deleted:
            raise ApiError(401, "当前登录状态无效")
        if user.status != "1":
            raise ApiError(403, "当前账号不可用，请联系管理员")
        token_version = int(getattr(user, "token_version", 0) or 0)
        try:
            claimed_token_version = int(claims.get("token_version", 0) or 0)
        except (TypeError, ValueError) as exc:
            raise ApiError(401, "Invalid token version") from exc
        if claimed_token_version != token_version:
            raise ApiError(401, "当前登录状态已失效，请重新登录")
        roles = await self._fetch_user_roles(user.id)
        if not roles:
            raise ApiError(403, "当前账号未分配角色")

        payload = {
            "uid": user.id,
            "userName": user.user_name,
            "roles": roles,
            "token_version": token_version,
            "must_change_password": bool(getattr(user, "must_change_password", False)),
        }
        return {
            "token": create_signed_token(
                payload,
                secret_key=self.settings.auth_secret_key,
                expires_in_seconds=self.settings.auth_access_token_expire_seconds,
                token_type="access",
            ),
            "refreshToken": create_signed_token(
                payload,
                secret_key=self.settings.auth_secret_key,
                expires_in_seconds=self.settings.auth_refresh_token_expire_seconds,
                token_type="refresh",
            ),
            "userId": user.id,
            "mustChangePassword": bool(getattr(user, "must_change_password", False)),
            "tokenVersion": token_version,
        }

    async def get_user_info(self, authorization: str | None) -> dict[str, Any]:
        principal = await self.resolve_principal(authorization, allow_password_change=True)
        buttons = await self._fetch_user_auth_marks(principal["user_id"])
        permissions = await self.fetch_user_permission_codes(principal["user_id"])
        return {
            "userId": principal["user_id"],
            "userName": principal["user_name"],
            "email": principal["email"],
            "avatar": principal["avatar"],
            "roles": principal["roles"],
            "buttons": buttons,
            "permissions": permissions,
            "mustChangePassword": bool(principal.get("must_change_password", False)),
            "tokenVersion": int(principal.get("token_version", 0) or 0),
        }

    async def change_password(self, authorization: str | None, payload: ChangePasswordPayload) -> bool:
        principal = await self.resolve_principal(authorization, allow_password_change=True)
        user = await self._fetch_user_by_id(principal["user_id"])
        if user is None or user.deleted:
            raise ApiError(401, "当前登录状态无效")

        try:
            old_ok = await run_password_op(verify_password, payload.old_password, user.password_hash)
        except PasswordWorkerBusy as exc:
            raise ApiError(503, "认证服务繁忙，请稍后重试") from exc
        if not old_ok:
            await write_system_log(
                level="WARN",
                log_type="auth",
                source="backend.change_password",
                content=f"修改密码失败: 当前密码错误 [{user.user_name}]",
                detail={"user_id": user.id, "user_name": user.user_name, "reason": "wrong_old_password"},
            )
            raise ApiError(400, "当前密码不正确")

        try:
            same_as_old = await run_password_op(
                verify_password, payload.new_password, user.password_hash
            )
        except PasswordWorkerBusy as exc:
            raise ApiError(503, "认证服务繁忙，请稍后重试") from exc
        if same_as_old:
            raise ApiError(400, "新密码不能与当前密码相同")

        try:
            user.password_hash = await run_password_op(hash_password, payload.new_password)
        except PasswordWorkerBusy as exc:
            raise ApiError(503, "认证服务繁忙，请稍后重试") from exc
        user.must_change_password = False
        user.token_version = int(getattr(user, "token_version", 0) or 0) + 1
        user.update_by = user.user_name
        await self.session.commit()

        await write_system_log(
            level="INFO",
            log_type="auth",
            source="backend.change_password",
            content=f"修改密码成功: {user.user_name}",
            detail={"user_id": user.id, "user_name": user.user_name},
        )
        return True

    async def logout(self, authorization: str | None) -> dict[str, Any]:
        principal = await self.resolve_principal(authorization, allow_password_change=True)
        user = await self._fetch_user_by_id(principal["user_id"])
        if user is None or user.deleted:
            raise ApiError(401, "当前登录状态无效")
        user.token_version = int(getattr(user, "token_version", 0) or 0) + 1
        user.update_by = user.user_name
        await self.session.commit()
        return principal

    async def resolve_principal(
        self,
        authorization: str | None,
        *,
        allow_password_change: bool = False,
    ) -> dict[str, Any]:
        if authorization is None:
            raise ApiError(401, "未登录或登录已失效")
        try:
            claims = decode_signed_token(authorization, secret_key=self.settings.auth_secret_key)
        except ValueError as exc:
            raise ApiError(401, str(exc)) from exc

        if claims.get("type") != "access":
            raise ApiError(401, "Invalid access token")

        user_id = int(claims.get("uid", 0))
        user = await self._fetch_user_by_id(user_id)
        if user is None or user.deleted:
            raise ApiError(401, "当前登录状态无效")
        if user.status != "1":
            raise ApiError(403, "当前账号不可用，请联系管理员")

        current_token_version = int(getattr(user, "token_version", 0) or 0)
        try:
            claimed_token_version = int(claims.get("token_version", 0) or 0)
        except (TypeError, ValueError) as exc:
            raise ApiError(401, "Invalid token version") from exc
        if claimed_token_version != current_token_version:
            raise ApiError(401, "当前登录状态已失效，请重新登录")
        must_change_password = bool(getattr(user, "must_change_password", False))
        if must_change_password and not allow_password_change:
            raise ApiError(403, "首次登录必须修改密码")

        roles = await self._fetch_user_roles(user.id)
        try:
            token_exp = int(claims.get("exp", 0) or 0)
        except (TypeError, ValueError):
            token_exp = 0
        return {
            "user_id": user.id,
            "user_name": user.user_name,
            "email": user.email,
            "avatar": user.avatar,
            "roles": roles,
            "token_version": current_token_version,
            "must_change_password": must_change_password,
            "token_exp": token_exp,
        }

    async def _fetch_user_by_name(self, user_name: str) -> SysUser | None:
        stmt = select(SysUser).where(SysUser.user_name == user_name).limit(1)
        return await self.session.scalar(stmt)

    async def _fetch_user_by_id(self, user_id: int) -> SysUser | None:
        stmt = select(SysUser).where(SysUser.id == user_id).limit(1)
        return await self.session.scalar(stmt)

    async def _fetch_user_roles(self, user_id: int) -> list[str]:
        stmt = (
            select(SysRole.role_code)
            .join(SysUserRole, SysUserRole.role_id == SysRole.id)
            .where(SysUserRole.user_id == user_id)
            .where(SysRole.enabled.is_(True))
            .order_by(SysRole.sort.asc(), SysRole.id.asc())
        )
        rows = (await self.session.execute(stmt)).scalars().all()
        return list(dict.fromkeys(rows))

    async def _fetch_user_auth_marks(self, user_id: int) -> list[str]:
        stmt = (
            select(SysPermission.auth_mark)
            .join(SysRolePermission, SysRolePermission.permission_id == SysPermission.id)
            .join(SysUserRole, SysUserRole.role_id == SysRolePermission.role_id)
            .join(SysRole, SysRole.id == SysUserRole.role_id)
            .where(SysUserRole.user_id == user_id)
            .where(SysPermission.enabled.is_(True))
            .where(SysRole.enabled.is_(True))
            .order_by(SysPermission.sort.asc(), SysPermission.id.asc())
        )
        rows = (await self.session.execute(stmt)).scalars().all()
        return list(dict.fromkeys([row for row in rows if row]))

    async def fetch_user_permission_codes(self, user_id: int) -> list[str]:
        stmt = (
            select(SysPermission.permission_code)
            .join(SysRolePermission, SysRolePermission.permission_id == SysPermission.id)
            .join(SysUserRole, SysUserRole.role_id == SysRolePermission.role_id)
            .join(SysRole, SysRole.id == SysUserRole.role_id)
            .where(SysUserRole.user_id == user_id)
            .where(SysPermission.enabled.is_(True))
            .where(SysRole.enabled.is_(True))
            .order_by(SysPermission.sort.asc(), SysPermission.id.asc())
        )
        rows = (await self.session.execute(stmt)).scalars().all()
        return list(dict.fromkeys([row for row in rows if row]))
