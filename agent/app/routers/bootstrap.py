from __future__ import annotations

from fastapi import APIRouter, HTTPException

from app.core.config import load_config, save_config
from app.core.security import dpapi_encrypt
from app.schemas.models import (
    BootstrapConfigRequest,
    BootstrapStatusResponse,
    HealthCheckResponse,
    RuntimeConfigResponse,
    TestConnectionRequest,
)
from app.services.health_checker import (
    check_backend,
    check_mysql,
    check_redis,
    check_tdengine,
)

router = APIRouter(prefix="/bootstrap", tags=["bootstrap"])


def _require_bootstrap_open() -> dict:
    """config 中 bootstrap_done=true 时返回 403；否则返回当前 config。"""
    cfg = load_config()
    if cfg["agent"].get("bootstrap_done", False):
        raise HTTPException(status_code=403, detail="Bootstrap already completed")
    return cfg


@router.get("/status", response_model=BootstrapStatusResponse)
async def bootstrap_status():
    cfg = load_config()
    return BootstrapStatusResponse(
        in_bootstrap=not cfg["agent"].get("bootstrap_done", False)
    )


@router.get("/runtime-config", response_model=RuntimeConfigResponse)
async def runtime_config():
    """app 启动/登录前读取后端地址（无需鉴权、不区分 bootstrap 状态）。"""
    cfg = load_config()
    base_url = (cfg.get("backend") or {}).get("base_url", "")
    return RuntimeConfigResponse(backend_base_url=base_url)


def _apply_db_section(cfg: dict, section: str, data) -> None:
    """把引导页提交的某 DB 段写入 config：连接字段直存，password DPAPI 加密。"""
    target = cfg.setdefault(section, {})
    target["host"] = data.host
    target["port"] = data.port
    if data.user is not None:
        target["user"] = data.user
    if data.database is not None:
        target["database"] = data.database
    # password 为 None 表示本次不改；空串视为"无密码"，仍加密存储（dpapi 可加密空串）。
    if data.password is not None:
        target["password_enc"] = dpapi_encrypt(data.password)
        target.pop("password", None)  # 防止明文残留


@router.put("/config")
async def bootstrap_config(body: BootstrapConfigRequest):
    cfg = _require_bootstrap_open()
    if body.backend is not None:
        cfg.setdefault("backend", {})["base_url"] = body.backend.base_url
    if body.mysql is not None:
        _apply_db_section(cfg, "mysql", body.mysql)
    if body.redis is not None:
        _apply_db_section(cfg, "redis", body.redis)
    if body.tdengine is not None:
        _apply_db_section(cfg, "tdengine", body.tdengine)
    save_config(cfg)
    return {}


@router.post("/test-connection", response_model=HealthCheckResponse)
async def bootstrap_test_connection(body: TestConnectionRequest):
    _require_bootstrap_open()

    if body.target == "backend":
        return check_backend(body.base_url or "")

    # mysql/redis/tdengine：用内联的待测配置（password 明文，check_* 会优先用 password）
    conn_cfg = {
        "host": body.host,
        "port": body.port,
        "user": body.user,
        "password": body.password or "",
        "database": body.database,
    }
    checkers = {
        "mysql": check_mysql,
        "redis": check_redis,
        "tdengine": check_tdengine,
    }
    return checkers[body.target](conn_cfg)


@router.post("/finish")
async def bootstrap_finish():
    cfg = _require_bootstrap_open()
    cfg["agent"]["bootstrap_done"] = True
    save_config(cfg)
    return {}
