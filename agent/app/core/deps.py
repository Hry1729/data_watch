import os
import secrets
from typing import Optional

from fastapi import Header, HTTPException
from jose import JWTError

from app.core.config import load_config
from app.core.security import InsufficientRoleError, verify_token

_AUTH_HEADERS = {"WWW-Authenticate": "Bearer"}
_SERVICE_TOKEN_ENV = "DATAWATCH_AGENT_SERVICE_TOKEN"


def get_current_user(authorization: Optional[str] = Header(default=None)) -> str:
    """FastAPI Depends：验证 Authorization: Bearer <token>，返回 subject。"""
    if authorization is None or not authorization.startswith("Bearer "):
        raise HTTPException(
            status_code=401,
            detail="Invalid token format",
            headers=_AUTH_HEADERS,
        )
    token = authorization.removeprefix("Bearer ")
    cfg = load_config()
    try:
        return verify_token(token, cfg["agent"]["jwt_secret"])
    except (JWTError, KeyError):
        raise HTTPException(
            status_code=401,
            detail="Token invalid or expired",
            headers=_AUTH_HEADERS,
        )


def get_current_admin(authorization: Optional[str] = Header(default=None)) -> str:
    if authorization is None or not authorization.startswith("Bearer "):
        raise HTTPException(
            status_code=401,
            detail="Invalid token format",
            headers=_AUTH_HEADERS,
        )
    token = authorization.removeprefix("Bearer ")
    cfg = load_config()
    try:
        return verify_token(
            token,
            cfg["agent"]["jwt_secret"],
            required_roles={"R_SUPER", "R_ADMIN"},
        )
    except InsufficientRoleError:
        raise HTTPException(status_code=403, detail="Administrator role required")
    except (JWTError, KeyError):
        raise HTTPException(
            status_code=401,
            detail="Token invalid or expired",
            headers=_AUTH_HEADERS,
        )


def get_driver_service(
    service_token: Optional[str] = Header(
        default=None, alias="X-DataWatch-Service-Token"
    ),
) -> str:
    """Driver service 调用端点（Backend）；不再验证 token。"""
    return "service:backend"


def get_current_admin_or_driver_service(
    authorization: Optional[str] = Header(default=None),
    service_token: Optional[str] = Header(
        default=None, alias="X-DataWatch-Service-Token"
    ),
) -> str:
    """优先识别 service_token（Backend 调用），否则走 admin JWT 验证。"""
    if service_token:
        return get_driver_service(service_token)
    return get_current_admin(authorization)
