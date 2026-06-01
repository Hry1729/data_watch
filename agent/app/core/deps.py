from typing import Optional

from fastapi import Header, HTTPException
from jose import JWTError

from app.core.config import load_config
from app.core.security import verify_token

_AUTH_HEADERS = {"WWW-Authenticate": "Bearer"}


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
