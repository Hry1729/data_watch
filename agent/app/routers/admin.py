from __future__ import annotations

import asyncio
import base64
import urllib.parse
import logging
from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel, Field

from app.core.config import (
    DB_SECTIONS,
    apply_db_patch,
    data_root,
    load_config,
    module_paths,
    mutate_config,
)
from app.core.deps import (
    get_current_admin,
    get_current_user_or_driver_service,
    get_driver_service,
)
from app.core.security import dpapi_decrypt
from app.core.role import build_role, managed_module_names
from app.schemas.models import (
    ConnectionConfigIn,
    ConnectionConfigOut,
    HealthCheckRequest,
    HealthCheckResponse,
)
from app.services.driver_installer import DriverInstallError, DriverInstaller
from app.services.driver_zip import DriverZipError
from app.services.health_checker import check_mysql, check_redis, check_tdengine
from app.services.process_manager import ProcessManagerError

router = APIRouter(prefix="/admin", tags=["admin"])
logger = logging.getLogger(__name__)


class DriverInstallJsonBody(BaseModel):
    """JSON 方式提交驱动包（便于测试与无 multipart 客户端）。"""

    package_base64: str = Field(..., min_length=1)


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
    """中心式 Collector 用 MySQL/Redis；边缘 Collector 只用本地 SQLite。"""
    try:
        if build_role() == "edge":
            root = data_root()
            return {
                "DATAWATCH_DATA_ROOT": str(root),
                "DATAWATCH_EDGE_DB": str(root / "edge" / "edge.db"),
                "DATAWATCH_EDGE_STORAGE_URL": _edge_storage_url(),
            }
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

def _uploader_env() -> dict[str, str]:
    cfg = load_config()
    edge = cfg.get("edge") or {}
    private_key = dpapi_decrypt(str(edge.get("private_key_enc") or ""))
    encoded_key = base64.b64encode(private_key.encode("utf-8")).decode("ascii") if private_key else ""
    return {
        "DATAWATCH_DATA_ROOT": str(data_root()),
        "DATAWATCH_EDGE_DB": str(data_root() / "edge" / "edge.db"),
        "DATAWATCH_EDGE_STORAGE_URL": _edge_storage_url(),
        "DATAWATCH_CENTER_URL": str(edge.get("center_url") or ""),
        "DATAWATCH_NODE_UUID": str(edge.get("node_uuid") or ""),
        "DATAWATCH_UPLOADER_PRIVATE_KEY_B64": encoded_key,
        "DATAWATCH_MAX_OFFLINE_RETENTION_DAYS": str(
            edge.get("max_offline_retention_days") or 7
        ),
        "DATAWATCH_OUTBOX_MAINTENANCE_SECONDS": str(
            edge.get("outbox_maintenance_seconds") or 60
        ),
    }


def _edge_storage_url() -> str:
    cfg = load_config()
    storage = cfg.get("storage") or {}
    host = str(storage.get("host") or "127.0.0.1")
    port = int(storage.get("port") or 7073)
    return f"http://{host}:{port}"


def _storage_env() -> dict[str, str]:
    cfg = load_config()
    storage = cfg.get("storage") or {}
    runtime_dir = (cfg.get("agent") or {}).get("runtime_dir") or data_root() / "runtime"
    return {
        "DATAWATCH_DATA_ROOT": str(data_root()),
        "DATAWATCH_EDGE_DB": str(data_root() / "edge" / "edge.db"),
        "DATAWATCH_RUNTIME_DIR": str(runtime_dir),
        "DATAWATCH_EDGE_STORAGE_HOST": str(storage.get("host") or "127.0.0.1"),
        "DATAWATCH_EDGE_STORAGE_PORT": str(storage.get("port") or 7073),
        "DATAWATCH_EDGE_STORAGE_QUEUE_CAPACITY": str(storage.get("queue_capacity") or 2048),
    }


def _ingest_env() -> dict[str, str]:
    cfg = load_config()
    mysql = cfg.get("mysql") or {}
    redis = cfg.get("redis") or {}
    redis_password = dpapi_decrypt(str(redis.get("password_enc") or ""))
    auth = ":" + urllib.parse.quote(redis_password, safe="") + "@" if redis_password else ""
    return {
        "MYSQL_HOST": str(mysql.get("host") or ""),
        "MYSQL_PORT": str(mysql.get("port") or ""),
        "MYSQL_USER": str(mysql.get("user") or ""),
        "MYSQL_PASSWORD": dpapi_decrypt(str(mysql.get("password_enc") or "")),
        "MYSQL_DATABASE": str(mysql.get("database") or ""),
        "INGEST_REDIS_URL": f"redis://{auth}{redis.get('host') or '127.0.0.1'}:{redis.get('port') or 6379}/0",
        "INGEST_MAX_OFFLINE_RETENTION_DAYS": str(
            (cfg.get("ingest") or {}).get("max_offline_retention_days") or 7
        ),
    }


def _process_env(name: str) -> dict[str, str] | None:
    factories = {
        "collector": _collector_env,
        "writer": _writer_env,
        "uploader": _uploader_env,
        "storage": _storage_env,
        "ingest": _ingest_env,
    }
    factory = factories.get(name)
    return factory() if factory is not None else None


async def _ensure_edge_storage(request: Request) -> None:
    pm = request.app.state.pm
    if "storage" not in pm.modules:
        return
    if pm.status("storage").status != "running":
        await pm.start("storage", env=_storage_env())
        request.app.state.log_manager.attach(
            "storage",
            pm.log_file("storage"),
            asyncio.get_running_loop(),
        )
    await asyncio.to_thread(request.app.state.edge_storage.wait_ready)

@router.post("/processes/{name}/start")
async def start_process(name: str, request: Request, _user: str = Depends(get_current_admin)):
    installer = getattr(request.app.state, "driver_installer", None)
    if name in {"collector", "storage"} and installer is not None and (installer._lock.locked() or installer.recovery_blocked or installer.recovery_status()["incomplete"]):
        raise HTTPException(status_code=409, detail="驱动事务进行中或等待恢复，暂不能操作采集进程")
    pm = request.app.state.pm
    _require_known(pm, name)
    if pm.status(name).status == "running":
        raise HTTPException(status_code=409, detail=f"{name} is already running")

    if name in {"collector", "uploader"}:
        try:
            await _ensure_edge_storage(request)
        except ProcessManagerError as exc:
            raise HTTPException(status_code=400, detail=f"Edge Storage 启动失败: {exc}") from exc

    # writer 路径完全不碰 sidecar_manager；但两者都需 Agent 下发解密后的连接 env。
    extra_args = _collector_extra_args(request) if name == "collector" else None
    env = _process_env(name)

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
async def stop_process(name: str, request: Request, _user: str = Depends(get_current_admin)):
    installer = getattr(request.app.state, "driver_installer", None)
    if name in {"collector", "storage"} and installer is not None and (installer._lock.locked() or installer.recovery_blocked or installer.recovery_status()["incomplete"]):
        raise HTTPException(status_code=409, detail="驱动事务进行中或等待恢复，暂不能操作采集进程")
    pm = request.app.state.pm
    _require_known(pm, name)
    if name == "storage":
        for dependent in ("uploader", "collector"):
            if dependent in pm.modules and pm.status(dependent).status == "running":
                await pm.stop(dependent)
                await asyncio.to_thread(request.app.state.log_manager.detach, dependent)
                if dependent == "collector" and request.app.state.sidecar_manager is not None:
                    request.app.state.sidecar_manager.on_collector_stop()
    await pm.stop(name)
    await asyncio.to_thread(request.app.state.log_manager.detach, name)
    # 停模块 → 停其关联 proxy（FR-P17）；writer 不碰 sidecar。重启不走这里、proxy 保留。
    if name == "collector":
        request.app.state.sidecar_manager.on_collector_stop()
    return {}


@router.post("/processes/{name}/restart")
async def restart_process(name: str, request: Request, _user: str = Depends(get_current_admin)):
    installer = getattr(request.app.state, "driver_installer", None)
    if name in {"collector", "storage"} and installer is not None and (installer._lock.locked() or installer.recovery_blocked or installer.recovery_status()["incomplete"]):
        raise HTTPException(status_code=409, detail="驱动事务进行中或等待恢复，暂不能操作采集进程")
    pm = request.app.state.pm
    _require_known(pm, name)

    if name in {"collector", "uploader"}:
        try:
            await _ensure_edge_storage(request)
        except ProcessManagerError as exc:
            raise HTTPException(status_code=400, detail=f"Edge Storage 启动失败: {exc}") from exc

    # collector 走差量重启：prepare（起新/停删/留不变）后只 bounce collector；writer 不变。
    extra_args = _collector_extra_args(request) if name == "collector" else None
    env = _process_env(name)

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
async def get_config(_user: str = Depends(get_current_admin)):
    cfg = load_config()
    modules = set(managed_module_names())
    return ConnectionConfigOut(
        redis=_db_config_out(cfg, "redis"),
        mysql=_db_config_out(cfg, "mysql"),
        tdengine=_db_config_out(cfg, "tdengine"),
        collector=_process_config_out("collector") if "collector" in modules else None,
        writer=_process_config_out("writer") if "writer" in modules else None,
        ingest=_process_config_out("ingest") if "ingest" in modules else None,
    )


@router.put("/config")
async def update_config(payload: ConnectionConfigIn, _user: str = Depends(get_current_admin)):
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


# B11：凭据与这些目标标识绑定。任一项被请求体改写，都不得沿用已存储的凭据。
_CREDENTIAL_BOUND_FIELDS = ("host", "port", "user", "database")


def _has_stored_credential(section: dict) -> bool:
    """B11：**任何形态**的存储凭据都算（DPAPI 密文 `password_enc`，或手改配置留下的明文 `password`）。

    只看 `password_enc` 会漏掉明文形态：`_decrypt_password` 同样支持明文 `password`，
    否则改目标即可让明文口令跟随新目标（R1）。
    """
    if section.get("password_enc"):
        return True
    stored_password = section.get("password")
    return stored_password is not None and str(stored_password) != ""


@router.post("/health/check", response_model=HealthCheckResponse)
async def health_check(
    request: HealthCheckRequest,
    _user: str = Depends(get_current_admin),
) -> HealthCheckResponse:
    cfg = load_config()

    if not (cfg.get("agent") or {}).get("bootstrap_done", False):
        raise HTTPException(status_code=400, detail="Bootstrap must be completed first")

    target_cfg = dict(cfg.get(request.target) or {})
    overrides = (
        request.config.model_dump(exclude_unset=True) if request.config is not None else {}
    )

    changed = sorted(
        key
        for key in _CREDENTIAL_BOUND_FIELDS
        if overrides.get(key) is not None and target_cfg.get(key) != overrides.get(key)
    )
    if changed:
        if _has_stored_credential(target_cfg) and overrides.get("password") is None:
            # 审计只记字段名，不记值，避免把口令写进日志
            logger.warning(
                "health check denied: target=%s changed_fields=%s",
                request.target,
                changed,
            )
            raise HTTPException(
                status_code=400,
                detail=(
                    "目标已变更（" + "/".join(changed) + "），不得沿用已存储的凭据；"
                    "请在 config.password 显式提供该目标的临时口令"
                ),
            )
        # 明确的新目标：丢弃存储凭据（密文与明文两形态），只用本次显式提供的凭据
        target_cfg.pop("password_enc", None)
        target_cfg.pop("password", None)

    for key, value in overrides.items():
        if value is not None:
            target_cfg[key] = value

    checkers = {
        "redis": check_redis,
        "mysql": check_mysql,
        "tdengine": check_tdengine,
    }
    return await asyncio.to_thread(checkers[request.target], target_cfg)


def _get_installer(request: Request) -> DriverInstaller:
    installer = getattr(request.app.state, "driver_installer", None)
    if installer is None:
        installer = DriverInstaller(data_root() / "drivers", stop_collector=lambda: request.app.state.pm.stop("collector"), collector_pid=lambda: request.app.state.pm.status("collector").pid)
        request.app.state.driver_installer = installer
    return installer


async def _restart_collector_for_drivers(request: Request) -> None:
    """安装/卸载后冷加载：复用现有 collector restart 拼参与 env。"""
    pm = request.app.state.pm
    if "collector" not in pm.modules:
        raise ProcessManagerError("collector module not configured")
    extra_args = _collector_extra_args(request)
    env = _collector_env()
    await pm.restart("collector", extra_args=extra_args, env=env)
    request.app.state.log_manager.attach(
        "collector",
        pm.log_file("collector"),
        asyncio.get_running_loop(),
    )


async def _collector_is_alive(request: Request) -> bool:
    pm = request.app.state.pm
    try:
        return pm.status("collector").status == "running"
    except Exception:
        return False


def _raise_driver_error(exc: Exception) -> None:
    if isinstance(exc, (DriverZipError, DriverInstallError)):
        if exc.status_code >= 500:
            logger.exception("驱动操作内部错误")
            raise HTTPException(
                status_code=exc.status_code,
                detail="驱动操作失败，请查看 Agent 日志",
            ) from exc
        raise HTTPException(status_code=exc.status_code, detail=str(exc)) from exc
    raise exc


def _raise_driver_internal_error(exc: Exception) -> None:
    logger.exception("驱动操作内部错误")
    raise HTTPException(
        status_code=503,
        detail="驱动操作失败，请查看 Agent 日志",
    ) from exc


@router.get("/drivers")
async def list_drivers(
    request: Request,
    _user: str = Depends(get_current_user_or_driver_service),
):
    installer = _get_installer(request)
    status, pid = "unknown", None
    try:
        process = request.app.state.pm.status("collector")
        status, pid = process.status, process.pid
    except (AttributeError, KeyError, ProcessManagerError):
        pass
    drivers = await installer.list_drivers(collector_status=status, collector_pid=pid)
    return {
        "drivers": drivers,
        "recovery": installer.recovery_status(),
    }


@router.post("/drivers/recovery/retry")
async def retry_driver_recovery(
    request: Request,
    _user: str = Depends(get_current_admin),
):
    """显式重试崩溃恢复：磁盘已就位时再重启 collector 并验证代际。"""
    installer = _get_installer(request)

    async def _restart() -> None:
        await _restart_collector_for_drivers(request)

    async def _alive() -> bool:
        return await _collector_is_alive(request)

    try:
        results = await installer.recover_incomplete_operations(
            restart_collector=_restart,
            collector_alive=_alive,
        )
    except ProcessManagerError as exc:
        _raise_driver_internal_error(exc)
    request.app.state.driver_recovery_blocked = installer.recovery_blocked
    return {
        "results": results,
        "recovery": installer.recovery_status(),
    }


@router.post("/drivers/preview")
async def preview_driver(request: Request, user: str = Depends(get_current_admin)):
    body = await request.json()
    installer = _get_installer(request)
    try:
        if body.get("restore_id"):
            archive = await asyncio.to_thread(installer.restoration_archive, str(body["restore_id"]))
        else:
            try:
                archive = base64.b64decode(body.get("package_base64", ""), validate=True)
            except (ValueError, TypeError) as exc:
                raise HTTPException(status_code=400, detail="无效驱动 ZIP") from exc
        return {"driver": await installer.preview(archive)}
    except (DriverZipError, DriverInstallError) as exc:
        _raise_driver_error(exc)


@router.post("/drivers", status_code=201)
async def install_driver(
    request: Request,
    user: str = Depends(get_current_admin),
):
    """安装或升级外置驱动包。

    支持：
    - multipart 字段 ``package``（文件）
    - JSON ``{"package_base64":"..."}``
    - 原始 ``application/zip`` body

    幂等（同 ID/版本/SHA）返回 200。
    """
    zip_bytes: bytes | None = None
    content_type = (request.headers.get("content-type") or "").lower()

    if "multipart/form-data" in content_type:
        form = await request.form()
        upload = form.get("package")
        if upload is None:
            raise HTTPException(status_code=400, detail="缺少 multipart 字段 package")
        read = getattr(upload, "read", None)
        if read is None:
            raise HTTPException(status_code=400, detail="package 必须为文件")
        zip_bytes = await read()
    elif "application/json" in content_type:
        body = DriverInstallJsonBody.model_validate(await request.json())
        try:
            zip_bytes = base64.b64decode(body.package_base64, validate=True)
        except Exception as exc:
            raise HTTPException(status_code=400, detail=f"package_base64 无效: {exc}") from exc
    else:
        raw = await request.body()
        if raw:
            zip_bytes = raw

    if not zip_bytes:
        raise HTTPException(status_code=400, detail="缺少驱动 ZIP（package 或 package_base64）")

    installer = _get_installer(request)
    if installer.recovery_blocked:
        raise HTTPException(
            status_code=503,
            detail="driver_recovery_blocked: 请先 POST /admin/drivers/recovery/retry",
        )

    async def _restart() -> None:
        await _restart_collector_for_drivers(request)

    async def _alive() -> bool:
        return await _collector_is_alive(request)

    try:
        result = await installer.install(
            zip_bytes,
            restart_collector=_restart,
            collector_alive=_alive,
            operator=user,
        )
    except (DriverZipError, DriverInstallError) as exc:
        _raise_driver_error(exc)
        raise  # pragma: no cover
    except ProcessManagerError as exc:
        _raise_driver_internal_error(exc)

    if not result.created:
        return JSONResponse(status_code=200, content={"driver": result.driver})
    return {"driver": result.driver}


@router.delete("/drivers/{driver_id}")
async def uninstall_driver(
    driver_id: str,
    request: Request,
    user: str = Depends(get_driver_service),
):
    installer = _get_installer(request)
    if installer.recovery_blocked:
        raise HTTPException(
            status_code=503,
            detail="driver_recovery_blocked: 请先 POST /admin/drivers/recovery/retry",
        )

    async def _restart() -> None:
        await _restart_collector_for_drivers(request)

    async def _alive() -> bool:
        return await _collector_is_alive(request)

    # The center checks global references; an edge also checks its durable local config.
    from app.core.role import build_role
    if build_role() == "edge":
        from app.services.edge_local_store import EdgeLocalStore
        from data_watch_driver_catalog import OFFICIAL_ALIASES
        record = installer.get_driver(driver_id) or {}
        aliases = set(record.get("aliases") or OFFICIAL_ALIASES.get(driver_id, (record.get("protocol", driver_id),)))
        try:
            devices = await asyncio.to_thread(EdgeLocalStore(data_root() / "edge/edge.db").devices)
        except Exception as exc:
            raise HTTPException(status_code=503, detail="无法核实本节点设备引用，已取消卸载") from exc
        references = [str(item["device_code"]) for item in devices["devices"] if str(item.get("protocol", "")).lower() in aliases]
        if references:
            raise HTTPException(status_code=409, detail="驱动仍被本节点设备引用：" + ", ".join(references[:20]))

    try:
        result = await installer.uninstall(
            driver_id,
            restart_collector=_restart,
            collector_alive=_alive,
            operator=user,
        )
    except (DriverZipError, DriverInstallError) as exc:
        _raise_driver_error(exc)
        raise  # pragma: no cover
    except ProcessManagerError as exc:
        _raise_driver_internal_error(exc)

    return {"driver": result}


@router.post("/drivers/{driver_id}/restore")
async def restore_driver(driver_id: str, request: Request, user: str = Depends(get_current_admin)):
    installer = _get_installer(request)
    async def restart():
        await _restart_collector_for_drivers(request)
    async def alive():
        return await _collector_is_alive(request)
    try:
        archive = await asyncio.to_thread(installer.restoration_archive, driver_id)
        result = await installer.install(archive, restart_collector=restart, collector_alive=alive, operator=user)
        return {"driver": result.driver}
    except (DriverZipError, DriverInstallError) as exc:
        _raise_driver_error(exc)
