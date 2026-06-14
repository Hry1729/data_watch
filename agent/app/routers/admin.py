from __future__ import annotations

import asyncio
from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException, Request

from app.core.config import (
    DB_SECTIONS,
    apply_db_patch,
    load_config,
    module_paths,
    mutate_config,
)
from app.core.deps import get_current_user
from app.core.security import dpapi_decrypt
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


def _process_config_out(section: str) -> dict:
    """只读展示 factory 派生的实际路径；executable/working_dir 不再可由 PUT 配置。"""
    executable, working_dir = module_paths(section)
    return {"executable": str(executable), "working_dir": str(working_dir)}


def _collector_extra_args(request: Request) -> list[str]:
    """collector 启动/重启前的准备 + 拼参（start/restart 共用，避免一改一漏）。

    sidecar_manager 在 try 外取：未接好属接线 bug，让 AttributeError→500，不伪装 400。
    settings.toml 不存在(bootstrap 未跑) / prepare 任何错误 → 400（detail 不吞 endpoint/
    port/日志路径）。无 OPC DA（port_mapping_path=None）→ 只拼 --config，不拼 --port-mapping。
    """
    sm = request.app.state.sidecar_manager
    settings_path = sm.collector_settings_path
    if settings_path is None or not Path(settings_path).exists():
        raise HTTPException(
            status_code=400,
            detail="collector settings.toml not found; run bootstrap first",
        )
    try:
        plan = sm.prepare_for_collector_start()
    except Exception as exc:
        raise HTTPException(status_code=400, detail=str(exc))
    # 显式下发 runtime_dir：与 Agent 写 shutdown_flag 的目录(pm.runtime_dir)同源。
    # 否则 collector 自行推导默认目录（DATAWATCH_HOME/runtime 或 ProgramData\...），
    # 自定义数据根时两端分叉 → collector 收不到优雅停机 flag → 被强杀。
    pm = request.app.state.pm
    extra_args = ["--config", str(settings_path), "--runtime-dir", str(pm.runtime_dir)]
    if plan.port_mapping_path is not None:
        extra_args += ["--port-mapping", str(plan.port_mapping_path)]
    return extra_args


# env 注入契约：Agent 是连接凭据的唯一来源（DPAPI 加密存于 config.toml），启动
# collector/writer 前解密并经 DATAWATCH_<DB>_<FIELD> 下发；env 优先级高于各模块
# settings.toml（后者仅作 dev 本地回退）。launch() 合并而非替换 os.environ，故每个
# 受管字段都显式写出（缺失写空串）——空串覆盖父进程可能残留的同名变量，避免子进程
# 误继承旧值；子进程读到空串则回退自身 settings.toml。
_ENV_PREFIX = {"mysql": "MYSQL", "redis": "REDIS", "tdengine": "TDENGINE"}
# 各 section 实际拥有的非密码连接字段（redis 无 user/database）。
_ENV_CONN_FIELDS = {
    "mysql": ("host", "port", "user", "database"),
    "redis": ("host", "port"),
    "tdengine": ("host", "port", "user", "database"),
}


def _db_env(cfg: dict, section: str) -> dict[str, str]:
    """从一个 DB section 构造下发 env：所有受管字段（连接字段 + 解密后的 password）。

    缺失/None 写空串，使 Agent 成为权威来源（覆盖父进程残留、子进程据空串回退）。
    """
    src = cfg.get(section) or {}
    prefix = _ENV_PREFIX[section]
    env: dict[str, str] = {}
    for field in _ENV_CONN_FIELDS[section]:
        value = src.get(field)
        env[f"DATAWATCH_{prefix}_{field.upper()}"] = "" if value is None else str(value)
    env[f"DATAWATCH_{prefix}_PASSWORD"] = dpapi_decrypt(src.get("password_enc", "") or "")
    return env


def _collector_env() -> dict[str, str]:
    """collector 需要 mysql + redis 连接（注入失败回退空，由 collector 报连接错）。"""
    try:
        cfg = load_config()
        return {**_db_env(cfg, "mysql"), **_db_env(cfg, "redis")}
    except Exception:
        return {}


def _writer_env() -> dict[str, str]:
    """writer 需要 mysql + redis + tdengine 连接。"""
    try:
        cfg = load_config()
        return {
            **_db_env(cfg, "mysql"),
            **_db_env(cfg, "redis"),
            **_db_env(cfg, "tdengine"),
        }
    except Exception:
        return {}


@router.post("/processes/{name}/start")
async def start_process(name: str, request: Request, _user: str = Depends(get_current_user)):
    pm = request.app.state.pm
    _require_known(pm, name)
    if pm.status(name).status == "running":
        raise HTTPException(status_code=409, detail=f"{name} is already running")

    # writer 路径完全不碰 sidecar_manager；但两者都需 Agent 下发解密后的连接 env。
    extra_args = _collector_extra_args(request) if name == "collector" else None
    env = _collector_env() if name == "collector" else _writer_env() if name == "writer" else None

    try:
        await pm.start(name, extra_args=extra_args, env=env)
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
    # 停模块 → 停其关联 proxy（FR-P17）；writer 不碰 sidecar。重启不走这里、proxy 保留。
    if name == "collector":
        request.app.state.sidecar_manager.on_collector_stop()
    return {}


@router.post("/processes/{name}/restart")
async def restart_process(name: str, request: Request, _user: str = Depends(get_current_user)):
    pm = request.app.state.pm
    _require_known(pm, name)

    # collector 走差量重启：prepare（起新/停删/留不变）后只 bounce collector；writer 不变。
    extra_args = _collector_extra_args(request) if name == "collector" else None
    env = _collector_env() if name == "collector" else _writer_env() if name == "writer" else None

    try:
        await pm.restart(name, extra_args=extra_args, env=env)
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
        collector=_process_config_out("collector"),
        writer=_process_config_out("writer"),
    )


@router.put("/config")
async def update_config(payload: ConnectionConfigIn, _user: str = Depends(get_current_user)):
    try:
        # 进程段(collector/writer)路径由 config.module_paths 按安装根/数据根派生，
        # 只读不可配置；PUT 仅处理 DB 段。
        def apply_patches(cfg: dict) -> None:
            for section in DB_SECTIONS:
                section_patch = getattr(payload, section)
                if section_patch is not None:
                    apply_db_patch(cfg, section, section_patch.model_dump(exclude_unset=True))

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
