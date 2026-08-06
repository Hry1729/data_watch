from __future__ import annotations

import asyncio
from urllib.parse import urlsplit

from fastapi import APIRouter, HTTPException

from app.core.config import load_config, save_config
from app.core.role import build_role
from app.core.security import dpapi_decrypt, dpapi_encrypt
from app.schemas.models import (
    BootstrapConfigRequest,
    BootstrapStatusResponse,
    EdgeBootstrapRegisterRequest,
    HealthCheckResponse,
    RuntimeConfigResponse,
    TestConnectionRequest,
)
from app.services.edge_sync import (
    CenterClient,
    CenterRequestError,
    EdgeIdentity,
    generate_identity,
    identity_public_key,
)
from app.services.health_checker import (
    check_backend,
    check_mysql,
    check_redis,
    check_tdengine,
)

router = APIRouter(prefix="/bootstrap", tags=["bootstrap"])


def _require_bootstrap_open() -> dict:
    cfg = load_config()
    if cfg["agent"].get("bootstrap_done", False):
        raise HTTPException(status_code=403, detail="Bootstrap already completed")
    return cfg


def _require_edge_role() -> None:
    if build_role() != "edge":
        raise HTTPException(status_code=404, detail="Not found")


def _center_url(value: str) -> str:
    normalized = value.strip().rstrip("/")
    parsed = urlsplit(normalized)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        raise HTTPException(status_code=400, detail="中心地址必须是有效的 HTTP(S) URL")
    return normalized


@router.get("/status", response_model=BootstrapStatusResponse)
async def bootstrap_status():
    cfg = load_config()
    return BootstrapStatusResponse(
        in_bootstrap=not cfg["agent"].get("bootstrap_done", False)
    )


@router.get("/runtime-config", response_model=RuntimeConfigResponse)
async def runtime_config():
    cfg = load_config()
    base_url = (cfg.get("backend") or {}).get("base_url", "")
    return RuntimeConfigResponse(backend_base_url=base_url)


@router.get("/edge/status")
async def edge_bootstrap_status():
    _require_edge_role()
    cfg = load_config()
    edge = cfg.get("edge") or {}
    return {
        "nodeUuid": str(edge.get("node_uuid") or ""),
        "nodeName": str(edge.get("node_name") or ""),
        "centerUrl": str(edge.get("center_url") or ""),
        "registered": bool(edge.get("registered")),
        "registrationStatus": str(
            edge.get("registration_status") or "not_registered"
        ),
        "lastSyncError": str(edge.get("last_sync_error") or ""),
    }


@router.post("/edge/register")
async def edge_register(body: EdgeBootstrapRegisterRequest):
    _require_edge_role()
    cfg = _require_bootstrap_open()
    center_url = _center_url(body.center_url)
    edge = cfg.setdefault("edge", {})
    private_key_pem = dpapi_decrypt(str(edge.get("private_key_enc") or ""))
    node_uuid = str(edge.get("node_uuid") or "")
    if private_key_pem and node_uuid:
        identity = EdgeIdentity(
            node_uuid=node_uuid,
            private_key_pem=private_key_pem,
            public_key_pem=identity_public_key(private_key_pem),
        )
    else:
        identity = generate_identity()

    edge.update(
        {
            "center_url": center_url,
            "node_uuid": identity.node_uuid,
            "node_name": body.node_name.strip(),
            "private_key_enc": dpapi_encrypt(identity.private_key_pem),
            "registered": False,
            "registration_status": "registering",
            "last_sync_error": "",
        }
    )
    # Persist identity before network I/O so commit_unknown retries use the same node/key.
    save_config(cfg)

    try:
        result = await CenterClient(center_url, identity).register(
            body.registration_code,
            body.node_name.strip(),
        )
    except CenterRequestError as exc:
        edge["registration_status"] = "register_error"
        edge["last_sync_error"] = str(exc)[:1000]
        save_config(cfg)
        status = exc.status_code if 400 <= exc.status_code < 600 else 400
        raise HTTPException(status_code=status, detail=str(exc)) from exc

    edge["registered"] = True
    edge["registration_status"] = str(result.get("status") or "pending")
    edge["last_sync_error"] = ""
    save_config(cfg)
    return {
        "nodeUuid": identity.node_uuid,
        "status": edge["registration_status"],
    }


def _apply_db_section(cfg: dict, section: str, data) -> None:
    target = cfg.setdefault(section, {})
    target["host"] = data.host
    target["port"] = data.port
    if data.user is not None:
        target["user"] = data.user
    if data.database is not None:
        target["database"] = data.database
    if data.password is not None:
        target["password_enc"] = dpapi_encrypt(data.password)
        target.pop("password", None)


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
    if build_role() == "edge" and not (cfg.get("edge") or {}).get("registered"):
        raise HTTPException(status_code=400, detail="Edge node must be registered first")
    cfg["agent"]["bootstrap_done"] = True
    save_config(cfg)
    return {}