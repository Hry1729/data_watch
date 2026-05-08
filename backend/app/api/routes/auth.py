from __future__ import annotations

from turboapi import APIRouter, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.log_writer import write_system_log
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.schemas.auth import LoginPayload
from app.services.auth import AuthService


router = APIRouter(tags=["auth"])
auth_scheme = HTTPBearer(auto_error=False)


@router.post("/api/auth/login")
async def login(
    userName: str,
    password: str,
    x_forwarded_for: str | None = None,
    x_real_ip: str | None = None,
):
    login_payload = LoginPayload.from_dict({"userName": userName, "password": password})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = AuthService(session)
        data = await service.login(
            user_name=login_payload.user_name,
            password=login_payload.password,
            client_ip=_resolve_client_ip(x_forwarded_for, x_real_ip),
        )
        return success_response(data=data)


@router.post("/api/auth/logout")
async def logout(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
    x_forwarded_for: str | None = None,
    x_real_ip: str | None = None,
):
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = AuthService(session)
        client_ip = _resolve_client_ip(x_forwarded_for, x_real_ip)

        # 尝试获取用户信息以记录登出日志
        try:
            principal = await service.resolve_principal(_authorization_from_credentials(credentials))
            await write_system_log(
                level="INFO",
                log_type="auth",
                source="backend.logout",
                content=f"登出成功: {principal['user_name']}",
                detail={
                    "user_id": principal["user_id"],
                    "user_name": principal["user_name"],
                    "roles": principal["roles"],
                    "client_ip": client_ip,
                },
            )
        except Exception:
            # Token 无效时也记录登出，但用户信息为空
            await write_system_log(
                level="INFO",
                log_type="auth",
                source="backend.logout",
                content="登出（无有效登录状态）",
                detail={"client_ip": client_ip},
            )

        return success_response(data=True, msg="logout success")


@router.get("/api/user/info")
async def user_info(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = AuthService(session)
        data = await service.get_user_info(_authorization_from_credentials(credentials))
        return success_response(data=data)


def _resolve_client_ip(x_forwarded_for: str | None, x_real_ip: str | None) -> str:
    if x_forwarded_for:
        first_ip = x_forwarded_for.split(",", 1)[0].strip()
        if first_ip:
            return first_ip
    if x_real_ip and x_real_ip.strip():
        return x_real_ip.strip()
    return "0.0.0.0"


def _authorization_from_credentials(
    credentials: HTTPAuthorizationCredentials | None,
) -> str | None:
    if credentials is None:
        return None
    return f"{credentials.scheme} {credentials.credentials}".strip()
