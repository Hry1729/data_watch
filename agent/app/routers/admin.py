from __future__ import annotations

import asyncio

from fastapi import APIRouter, Depends, HTTPException, Request

from app.core.config import (
    DB_SECTIONS,
    PROCESS_SECTIONS,
    apply_db_patch,
    apply_process_patch,
    load_config,
    mutate_config,
)
from app.core.deps import get_current_user
from app.schemas.models import (
    ConnectionConfigIn,
    ConnectionConfigOut,
    HealthCheckRequest,
    HealthCheckResponse,
)
from app.services.health_checker import check_mysql, check_redis, check_tdengine
from app.services.process_manager import ProcessManagerError

router = APIRouter(prefix="/admin", tags=["admin"])


def _require_known(pm, name: str) -> None:
    if name not in pm.modules:
        raise HTTPException(status_code=422, detail=f"unknown module: {name}")


def _db_config_out(cfg: dict, section: str) -> dict:
    source = cfg.get(section, {})
    return {
        "host": source.get("host", ""),
        "port": source.get("port", 0),
        "user": source.get("user"),
        "database": source.get("database"),
    }


def _process_config_out(cfg: dict, section: str) -> dict:
    source = cfg.get(section, {})
    return {
        "executable": source.get("executable", ""),
        "working_dir": source.get("working_dir", ""),
    }


def _collector_extra_args(request: Request) -> list[str]:
    """collector 启动/重启前的准备 + 拼参（start/restart 共用，避免一改一漏）。

    sidecar_manager 在 try 外取：未接好属接线 bug，让 AttributeError→500，不伪装 400。
    settings 未配置 / prepare 任何错误 → 400（detail 不吞 endpoint/port/日志路径）。
    无 OPC DA（port_mapping_path=None）→ 只拼 --config，不拼 --port-mapping。
    """
    sm = request.app.state.sidecar_manager
    settings_path = sm.collector_settings_path
    if settings_path is None:
        raise HTTPException(
            status_code=400, detail="collector settings path is not configured"
        )
    try:
        plan = sm.prepare_for_collector_start()
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    extra_args = ["--config", str(settings_path)]
    if plan.port_mapping_path is not None:
        extra_args += ["--port-mapping", str(plan.port_mapping_path)]
    return extra_args


@router.post("/processes/{name}/start")
async def start_process(name: str, request: Request, _user: str = Depends(get_current_user)):
    pm = request.app.state.pm
    _require_known(pm, name)
    if pm.status(name).status == "running":
        raise HTTPException(status_code=409, detail=f"{name} is already running")

    # writer 路径完全不碰 sidecar_manager。
    extra_args = _collector_extra_args(request) if name == "collector" else None

    try:
        await pm.start(name, extra_args=extra_args)
    except ProcessManagerError as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    request.app.state.log_manager.attach(
        name,
        pm.log_file(name),
        asyncio.get_running_loop(),
    )
    return {}


@router.post("/processes/{name}/stop")
async def stop_process(name: str, request: Request, _user: str = Depends(get_current_user)):
    pm = request.app.state.pm
    _require_known(pm, name)
    await pm.stop(name)
    await asyncio.to_thread(request.app.state.log_manager.detach, name)
    return {}


@router.post("/processes/{name}/restart")
async def restart_process(name: str, request: Request, _user: str = Depends(get_current_user)):
    pm = request.app.state.pm
    _require_known(pm, name)

    # collector 走差量重启：prepare（起新/停删/留不变）后只 bounce collector；writer 不变。
    extra_args = _collector_extra_args(request) if name == "collector" else None

    try:
        await pm.restart(name, extra_args=extra_args)
    except ProcessManagerError as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    request.app.state.log_manager.attach(
        name,
        pm.log_file(name),
        asyncio.get_running_loop(),
    )
    return {}


@router.get("/config", response_model=ConnectionConfigOut)
async def get_config(_user: str = Depends(get_current_user)):
    cfg = load_config()
    return ConnectionConfigOut(
        redis=_db_config_out(cfg, "redis"),
        mysql=_db_config_out(cfg, "mysql"),
        tdengine=_db_config_out(cfg, "tdengine"),
        collector=_process_config_out(cfg, "collector"),
        writer=_process_config_out(cfg, "writer"),
    )


@router.put("/config")
async def update_config(payload: ConnectionConfigIn, _user: str = Depends(get_current_user)):
    try:
        def apply_patches(cfg: dict) -> None:
            for section in DB_SECTIONS:
                section_patch = getattr(payload, section)
                if section_patch is not None:
                    apply_db_patch(cfg, section, section_patch.model_dump(exclude_unset=True))

            for section in PROCESS_SECTIONS:
                section_patch = getattr(payload, section)
                if section_patch is not None:
                    apply_process_patch(cfg, section, section_patch.model_dump(exclude_unset=True))

        mutate_config(apply_patches)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc))

    return {}


@router.post("/health/check", response_model=HealthCheckResponse)
async def health_check(
    request: HealthCheckRequest,
    _user: str = Depends(get_current_user),
) -> HealthCheckResponse:
    cfg = load_config()

    if not (cfg.get("agent") or {}).get("bootstrap_done", False):
        raise HTTPException(status_code=400, detail="Bootstrap must be completed first")

    target_cfg = dict(cfg.get(request.target) or {})
    if request.config is not None:
        for key, value in request.config.model_dump(exclude_unset=True).items():
            if value is not None:
                target_cfg[key] = value

    checkers = {
        "redis": check_redis,
        "mysql": check_mysql,
        "tdengine": check_tdengine,
    }
    return await asyncio.to_thread(checkers[request.target], target_cfg)
