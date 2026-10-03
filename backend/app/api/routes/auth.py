from __future__ import annotations

import ipaddress
from typing import Any

from turboapi import APIRouter, Depends, Request
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.config import get_settings
from app.core.log_writer import write_system_log
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.schemas.auth import ChangePasswordPayload, LoginPayload
from app.services.auth import AuthService


router = APIRouter(tags=["auth"])
auth_scheme = HTTPBearer(auto_error=False)


@router.post("/api/auth/login")
async def login(
    request: Request,
    userName: str,
    password: str,
):
    login_payload = LoginPayload.from_dict({"userName": userName, "password": password})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = AuthService(session)
        data = await service.login(
            user_name=login_payload.user_name,
            password=login_payload.password,
            client_ip=_resolve_client_ip(request),
        )
        return success_response(data=data)


@router.post("/api/auth/refresh")
async def refresh(payload: dict[str, Any]):
    refresh_token = str(payload.get("refreshToken") or "").strip()
    if not refresh_token:
        from app.core.exceptions import ApiError

        raise ApiError(400, "refreshToken is required")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        data = await AuthService(session).refresh(refresh_token)
        return success_response(data=data)


@router.post("/api/auth/logout")
async def logout(
    request: Request,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = AuthService(session)
        client_ip = _resolve_client_ip(request)

        # 尝试获取用户信息以记录登出日志
        try:
            principal = await service.logout(_authorization_from_credentials(credentials))
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


@router.post("/api/user/password/change")
async def change_password(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    change_payload = ChangePasswordPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = AuthService(session)
        data = await service.change_password(
            _authorization_from_credentials(credentials),
            change_payload,
        )
        return success_response(data=data)


def _trusted_proxy_networks() -> tuple[Any, ...]:
    configured = str(getattr(get_settings(), "auth_trusted_proxies", "") or "")
    networks = []
    for item in configured.split(","):
        candidate = item.strip()
        if not candidate:
            continue
        try:
            networks.append(ipaddress.ip_network(candidate, strict=False))
        except ValueError:
            # 配置项写错时不静默放宽：忽略该项，保持"不信任"的语义。
            continue
    return tuple(networks)


def _parse_address(value: str) -> Any | None:
    if not value:
        return None
    try:
        return ipaddress.ip_address(value)
    except ValueError:
        return None


def _address_in(networks: tuple[Any, ...], value: str) -> bool:
    address = _parse_address(value)
    if address is None:
        return False
    return any(address in network for network in networks if network.version == address.version)


def _resolve_client_ip(request: Request) -> str:
    """来源 IP：以连接对端为准，仅在可信代理链上才解析转发头（B18）。

    - **不信任最左项**：nginx 用 ``$proxy_add_x_forwarded_for`` **追加**对端，客户端自带
      的 ``X-Forwarded-For`` 会留在最左，所以必须**从右往左**、跳过可信跳取第一个不可信地址。
    - **默认不可伪造**：``AUTH_TRUSTED_PROXIES`` 为空时完全忽略转发头，来源即连接对端。
    - **不接受查询参数伪装的同名参数**：本函数只读连接对端与请求头。
    - **无法解析的项既不作来源也不作可信跳**：否则可用任意字符串伪造来源、绕过按来源限流
      并污染日志。
    """
    peer = str(getattr(getattr(request, "client", None), "host", "") or "").strip()
    networks = _trusted_proxy_networks()
    if not networks or not _address_in(networks, peer):
        return peer or "0.0.0.0"

    headers = getattr(request, "headers", None)
    forwarded = str(headers.get("X-Forwarded-For") or "") if headers is not None else ""
    for hop in reversed([item.strip() for item in forwarded.split(",") if item.strip()]):
        if _parse_address(hop) is None:
            continue
        if not _address_in(networks, hop):
            return hop

    real_ip = str(headers.get("X-Real-IP") or "").strip() if headers is not None else ""
    if _parse_address(real_ip) is not None and not _address_in(networks, real_ip):
        return real_ip
    return peer or "0.0.0.0"


def _authorization_from_credentials(
    credentials: HTTPAuthorizationCredentials | None,
) -> str | None:
    if credentials is None:
        return None
    return f"{credentials.scheme} {credentials.credentials}".strip()
