from __future__ import annotations

import logging

import pymysql
from fastapi import APIRouter, HTTPException

from app.core.config import load_config
from app.core.security import create_access_token, dpapi_decrypt, verify_password
from app.schemas.models import LoginRequest, LoginResponse

logger = logging.getLogger(__name__)

router = APIRouter(tags=["auth"])


def _get_mysql_conn(cfg: dict):
    mysql = cfg["mysql"]
    password = dpapi_decrypt(mysql.get("password_enc", ""))
    return pymysql.connect(
        host=mysql["host"],
        port=int(mysql["port"]),
        user=mysql["user"],
        password=password,
        database=mysql["database"],
        connect_timeout=5,
        cursorclass=pymysql.cursors.DictCursor,
    )


@router.post("/auth/login", response_model=LoginResponse)
async def login(body: LoginRequest):
    cfg = load_config()

    if not cfg["agent"].get("bootstrap_done", False):
        raise HTTPException(status_code=503, detail="Agent not initialized")

    try:
        conn = _get_mysql_conn(cfg)
    except Exception:
        raise HTTPException(status_code=503, detail="Database unavailable")

    try:
        with conn.cursor() as cur:
            cur.execute(
                "SELECT id, password_hash, status, deleted FROM sys_user"
                " WHERE user_name = %s LIMIT 1",
                (body.username,),
            )
            user = cur.fetchone()

        # 用户不存在或已删除 → 401（不区分"不存在"与"密码错误"）
        if user is None or user["deleted"]:
            raise HTTPException(status_code=401, detail="用户名或密码错误")

        # 密码验证 → 401（必须先于 status 判断，避免账号枚举侧信道）
        if not verify_password(body.password, user["password_hash"]):
            raise HTTPException(status_code=401, detail="用户名或密码错误")

        # 账号禁用 → 403
        if user["status"] != "1":
            raise HTTPException(status_code=403, detail="当前账号不可用，请联系管理员")

        # 角色检查：至少一个启用角色，并把角色编码写入 Agent JWT。
        with conn.cursor() as cur:
            cur.execute(
                "SELECT r.role_code FROM sys_user_role ur"
                " JOIN sys_role r ON r.id = ur.role_id"
                " WHERE ur.user_id = %s AND r.enabled = 1",
                (user["id"],),
            )
            roles = [str(row["role_code"]) for row in cur.fetchall()]
            if not roles:
                raise HTTPException(status_code=403, detail="当前账号未分配角色")

    finally:
        conn.close()

    token = create_access_token(
        subject=body.username,
        secret=cfg["agent"]["jwt_secret"],
        expire_hours=int(cfg["agent"].get("jwt_expire_hours", 8)),
        roles=roles,
    )
    logger.info("login success: user=%s", body.username)
    # 不 log token 值
    return LoginResponse(token=token)
