from __future__ import annotations

from fastapi import APIRouter, HTTPException

from app.core.config import load_config, save_config
from app.core.security import dpapi_encrypt
from app.schemas.models import (
    BootstrapConfigRequest,
    BootstrapStatusResponse,
    HealthCheckResponse,
    TestConnectionRequest,
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


@router.put("/config")
async def bootstrap_config(body: BootstrapConfigRequest):
    cfg = _require_bootstrap_open()
    cfg["mysql"]["host"] = body.host
    cfg["mysql"]["port"] = body.port
    cfg["mysql"]["user"] = body.user
    cfg["mysql"]["database"] = body.database
    cfg["mysql"]["password_enc"] = dpapi_encrypt(body.password)
    cfg["mysql"].pop("password", None)  # 防止明文密码残留
    save_config(cfg)
    return {}


@router.post("/test-connection", response_model=HealthCheckResponse)
async def bootstrap_test_connection(body: TestConnectionRequest):
    cfg = _require_bootstrap_open()
    from app.core.security import dpapi_decrypt
    mysql = cfg["mysql"]
    try:
        import pymysql
        password = dpapi_decrypt(mysql.get("password_enc", ""))
        conn = pymysql.connect(
            host=mysql["host"],
            port=int(mysql["port"]),
            user=mysql["user"],
            password=password,
            database=mysql["database"],
            connect_timeout=5,
        )
        conn.close()
        return HealthCheckResponse(success=True)
    except Exception:
        return HealthCheckResponse(success=False, message="Connection failed")


@router.post("/finish")
async def bootstrap_finish():
    cfg = _require_bootstrap_open()
    cfg["agent"]["bootstrap_done"] = True
    save_config(cfg)
    return {}
