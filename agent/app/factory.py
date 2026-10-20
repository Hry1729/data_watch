import asyncio
import logging
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException, Request
from fastapi.exceptions import RequestValidationError

from app.core.config import initialize_config_permissions, data_root, install_root, load_config, module_paths, mutate_config, resolve_runtime_dir
from app.core.exceptions import generic_exception_handler, validation_exception_handler
from app.core.role import build_role, managed_module_names
from app.core.security import dpapi_decrypt
from app.routers.admin import _collector_env, _edge_storage_url, _storage_env, _uploader_env, start_process
from app.services.driver_installer import DriverInstaller
from app.services.edge_sync import EdgeSyncService
from app.services.edge_storage_client import EdgeStorageClient
from app.services.log_manager import LogManager
from app.services.managed_process_runner import ManagedProcessRunner
from app.services.process_manager import ModuleSpec, ProcessManager
from app.services.sidecar_manager import (
    DEFAULT_CHANNEL,
    DEFAULT_SERVER_PROG_ID,
    SidecarManager,
    collector_lifecycle,
    wait_for_sidecar_operation,
    stop_collector_for_drivers,
)

logger = logging.getLogger(__name__)

_SHUTDOWN_TIMEOUT_SECONDS = 25.0


def build_sidecar_manager(cfg: dict, runtime_dir: Path) -> SidecarManager:
    """纯函数构建 SidecarManager（不依赖 app.state，便于单测）。

    约定：collector settings.toml = <collector 工作目录>/config/settings.toml，工作目录由
    config.module_paths 按数据根派生（与 collector 运行期默认、一致性校验同源），不再取自
    cfg。[opcda] 缺省走内置默认 progId/channel。install_root 见 config.install_root()。
    """
    _collector_exe, collector_working_dir = module_paths("collector")
    settings_path = collector_working_dir / "config" / "settings.toml"
    opcda = cfg.get("opcda", {})
    return SidecarManager(
        runtime_dir=runtime_dir,
        install_root=install_root(),
        runner=ManagedProcessRunner(runtime_dir),
        agent_mysql=cfg.get("mysql", {}),
        collector_settings_path=settings_path,
        server_prog_id=opcda.get("server_prog_id", DEFAULT_SERVER_PROG_ID),
        channel=opcda.get("channel", DEFAULT_CHANNEL),
    )


async def _stop_child_processes(pm: ProcessManager) -> None:
    async with collector_lifecycle(pm):
        ordered = [
            name
            for name in ("uploader", "collector", "ingest", "writer", "storage")
            if name in pm.modules
        ]
        for name in ordered:
            try:
                await pm.stop(name, preserve_desired=True)
            except Exception:
                logger.exception("Failed to stop module %s during Agent shutdown", name)

async def _detach_log_tails(pm: ProcessManager, log_manager: LogManager) -> None:
    for name in pm.modules:
        try:
            await asyncio.to_thread(log_manager.detach, name)
        except Exception:
            logger.exception("Failed to detach log tail for %s", name)


async def _recover_edge_storage(app: FastAPI) -> None:
    pm: ProcessManager = app.state.pm
    delay = 2.0
    stable_since = None
    while True:
        await asyncio.sleep(delay)
        try:
            async with collector_lifecycle(pm):
                if not pm.wants_running("storage", default=True):
                    app.state._storage_recovery_pending = False
                    continue
                status = pm.status("storage").status
                # B014：期望运行却无 PID（首启 spawn/PID 落盘失败）时 status 为 stopped，
                # 同样必须进入恢复，否则通用恢复循环跳过 storage 后再无人拉起。
                if status in {"crashed", "stopped"}:
                    app.state._storage_recovery_pending = True
                if not getattr(app.state, "_storage_recovery_pending", False):
                    now = asyncio.get_running_loop().time()
                    if stable_since is None:
                        stable_since = now
                    if now - stable_since >= 60:
                        delay = 2.0
                    continue
                stable_since = None
                delay = min(60.0, delay * 2)
                if status in {"crashed", "stopped"}:
                    await pm.restart("storage", env=_storage_env())
                await asyncio.to_thread(app.state.edge_storage.wait_ready)
                app.state._storage_recovery_pending = False
                app.state.log_manager.attach(
                    "storage",
                    pm.log_file("storage"),
                    asyncio.get_running_loop(),
                )
                logger.warning("Edge Storage crashed and was restarted")
        except asyncio.CancelledError:
            raise
        except HTTPException as exc:
            logger.error("Cannot recover Edge Storage: %s", exc.detail)
        except Exception:
            logger.exception("Failed to restart crashed Edge Storage")


async def _reconcile_sidecars_after_config_apply(app: FastAPI) -> None:
    """B015：边缘配置快照变更后，在统一生命周期锁内 reconcile OPC DA Sidecar。

    运行中的 Collector 热加载新增 OPC 设备时只会重读同一映射文件，故 Agent 须先
    差量起新端点代理、写映射并回收被删端点代理。若 Collector 启动时没有任何 OPC
    端点（未带 --port-mapping），则须重启 Collector 让其拿到映射文件。
    """
    pm: ProcessManager = app.state.pm
    sm = getattr(app.state, "sidecar_manager", None)
    if sm is None:
        return
    async with collector_lifecycle(pm):
        if pm.status("collector").status != "running":
            # 未运行：下一次启动会走 prepare_for_collector_start。
            return
        had_proxies = bool(sm._proxies)
        plan = await wait_for_sidecar_operation(sm.prepare_for_collector_start)
        if plan.port_mapping_path is None or had_proxies:
            return
        settings_path = sm.collector_settings_path
        if settings_path is None or not Path(settings_path).exists():
            raise RuntimeError("collector settings.toml not found; run bootstrap first")
        extra_args = [
            "--config",
            str(settings_path),
            "--runtime-dir",
            str(pm.runtime_dir),
            "--port-mapping",
            str(plan.port_mapping_path),
        ]
        await pm.restart("collector", extra_args=extra_args, env=_collector_env())


async def _restart_uploader_after_identity_rotation(app: FastAPI) -> None:
    """B016：后台用 pending key 恢复轮换后，Uploader 须以新 key 重启（同 /local/identity/rotate）。"""
    pm: ProcessManager = app.state.pm
    async with collector_lifecycle(pm):
        if "uploader" in pm.modules and pm.status("uploader").status == "running":
            await pm.restart("uploader", env=_uploader_env())


def _driver_gate_status(app: FastAPI) -> object:
    installer = getattr(app.state, "driver_installer", None)
    if installer is None:
        return None
    try:
        status = installer.recovery_status()
        return {
            "locked": installer._lock.locked(),
            "blocked": status.get("blocked"),
            "reasons": status.get("reasons"),
            "incomplete": len(status.get("incomplete") or []),
        }
    except Exception as exc:
        return f"unknown ({exc})"


async def _recover_processes(app: FastAPI, interval: float = 2.0) -> None:
    pm: ProcessManager = app.state.pm
    request = Request({"type": "http", "app": app, "datawatch_process_recovery": True})
    next_retry: dict[str, float] = {}
    delays: dict[str, float] = {}
    while True:
        try:
            ready = (load_config().get("agent") or {}).get("bootstrap_done", False)
        except Exception:
            logger.exception("Cannot read configuration for process recovery")
            await asyncio.sleep(interval)
            continue
        if ready:
            for name in pm.modules:
                if name == "storage":
                    continue  # Storage retains its existing readiness-aware recovery loop.
                try:
                    wanted = pm.wants_running(name)
                    info = pm.status(name)
                except Exception:
                    logger.exception("Cannot inspect module %s for recovery", name)
                    continue
                if not wanted:
                    next_retry.pop(name, None)
                    delays.pop(name, None)
                    continue
                if info.status == "running":
                    if info.uptime_seconds >= 60:
                        delays.pop(name, None)
                        next_retry.pop(name, None)
                    continue
                now = asyncio.get_running_loop().time()
                if now < next_retry.get(name, 0):
                    continue
                try:
                    # Reuse admin orchestration: fresh credentials, sidecars, driver
                    # transaction guard, Storage ready, and log attachment.
                    await start_process(name, request, _user="process-recovery")
                    if pm.status(name).status == "running":
                        pm._bump_restart_count(name)
                        logger.warning("Automatically recovered module %s", name)
                except asyncio.CancelledError:
                    raise
                except HTTPException as exc:
                    if exc.status_code == 409:
                        # D4：被驱动门禁拦下时必须留痕，否则界面只见 409、日志一片空白。
                        logger.warning(
                            "Recovery of module %s blocked: %s; driver recovery: %s",
                            name, exc.detail, _driver_gate_status(app),
                        )
                    else:
                        logger.error("Cannot recover module %s: %s", name, exc.detail)
                except Exception:
                    logger.exception("Cannot recover module %s", name)
                delay = delays.get(name, 2.0)
                next_retry[name] = asyncio.get_running_loop().time() + delay
                delays[name] = min(60.0, delay * 2)
        await asyncio.sleep(interval)


async def _recover_drivers_sync_collector(app: FastAPI) -> None:
    """磁盘恢复后统一重启 collector，并以 /drivers 验证运行代际。"""
    installer: DriverInstaller = app.state.driver_installer
    pm: ProcessManager = app.state.pm
    sm = app.state.sidecar_manager

    async def _restart() -> None:
        async with collector_lifecycle(pm):
            settings_path = sm.collector_settings_path
            if settings_path is None or not Path(settings_path).exists():
                raise RuntimeError("collector settings.toml not found; run bootstrap first")
            plan = await wait_for_sidecar_operation(sm.prepare_for_collector_start)
            extra_args = [
                "--config",
                str(settings_path),
                "--runtime-dir",
                str(pm.runtime_dir),
            ]
            if plan.port_mapping_path is not None:
                extra_args += ["--port-mapping", str(plan.port_mapping_path)]
            await pm.restart("collector", extra_args=extra_args, env=_collector_env())
            app.state.log_manager.attach(
                "collector",
                pm.log_file("collector"),
                asyncio.get_running_loop(),
            )

    async def _alive() -> bool:
        try:
            return pm.status("collector").status == "running"
        except Exception:
            return False

    try:
        results = await installer.recover_incomplete_operations(
            restart_collector=_restart,
            collector_alive=_alive,
        )
        if not installer.recovery_blocked:
            await installer.initialize_preinstalled(restart_collector=_restart, collector_alive=_alive)
        app.state.driver_recovery_blocked = installer.recovery_blocked
        for item in results:
            if item.get("verified"):
                continue
            logger.error(
                "驱动事务恢复未通过 collector 验证，已阻断驱动变更: %s",
                item,
            )
    except Exception:
        logger.exception("启动恢复驱动事务失败（不阻断 Agent 启动，但阻断驱动变更）")
        installer.mark_recovery_blocked("启动恢复异常")
        app.state.driver_recovery_blocked = True


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Stage 3 将在此处初始化 LogManager（日志 tail）
    await asyncio.to_thread(initialize_config_permissions)
    cfg = load_config()
    runtime_dir = resolve_runtime_dir(cfg)
    modules = {}
    for name in managed_module_names():
        exe, working_dir = module_paths(name)
        modules[name] = ModuleSpec(
            name=name,
            executable=str(exe),
            args=list(cfg.get(name, {}).get("args", [])),
            working_dir=str(working_dir),
            supports_shutdown_flag=name in {"collector", "writer", "storage"},
        )
    pm = ProcessManager(runtime_dir=runtime_dir, modules=modules)
    app.state.pm = pm
    app.state.log_manager = LogManager()
    app.state.sidecar_manager = (
        build_sidecar_manager(cfg, runtime_dir) if "collector" in modules else None
    )
    if app.state.sidecar_manager is not None:
        app.state.sidecar_manager.claim_existing_proxies()
    # 外置驱动安装根：DATAWATCH_DATA_ROOT/drivers
    app.state.driver_installer = DriverInstaller(data_root() / "drivers", stop_collector=lambda: stop_collector_for_drivers(pm), collector_pid=lambda: pm.status("collector").pid)
    app.state.driver_installer.ensure_layout()
    if build_role() == "edge":
        app.state.edge_storage = EdgeStorageClient(_edge_storage_url(), timeout_seconds=2.0)
        try:
            if pm.wants_running("storage", default=True):
                await pm.start("storage", env=_storage_env())
                await asyncio.to_thread(app.state.edge_storage.wait_ready)
                app.state.log_manager.attach(
                    "storage",
                    pm.log_file("storage"),
                    asyncio.get_running_loop(),
                )
        except Exception:
            # B014：首启失败保留恢复意图，交给 _recover_edge_storage 重试。
            app.state._storage_recovery_pending = True
            logger.exception("Edge Storage failed to start during Agent startup")
    # 崩溃补偿只属于包含 Collector 的角色。
    if "collector" in modules:
        await _recover_drivers_sync_collector(app)

    health_task = asyncio.create_task(pm.health_check_loop())
    process_recovery_task = asyncio.create_task(_recover_processes(app))
    edge_sync = None
    edge_sync_task = None
    storage_recovery_task = None
    if build_role() == "edge":
        storage_recovery_task = asyncio.create_task(_recover_edge_storage(app))

        def process_status(name: str) -> str:
            try:
                return pm.status(name).status
            except Exception:
                return "unknown"

        edge_sync = EdgeSyncService(
            data_root() / "edge" / "edge.db",
            load_config=load_config,
            mutate_config=mutate_config,
            decrypt=dpapi_decrypt,
            process_status=process_status,
            config_store=app.state.edge_storage,
            on_config_applied=lambda: _reconcile_sidecars_after_config_apply(app),
            on_identity_rotated=lambda: _restart_uploader_after_identity_rotation(app),
        )
        app.state.edge_sync = edge_sync
        edge_sync_task = asyncio.create_task(edge_sync.run_forever())
    try:
        yield
    finally:
        logger.info("Agent shutting down...")
        process_recovery_task.cancel()
        try:
            await process_recovery_task
        except asyncio.CancelledError:
            pass
        if edge_sync is not None and edge_sync_task is not None:
            edge_sync.stop()
            await edge_sync_task
        if storage_recovery_task is not None:
            storage_recovery_task.cancel()
            try:
                await storage_recovery_task
            except asyncio.CancelledError:
                pass
        try:
            await asyncio.wait_for(
                _stop_child_processes(app.state.pm),
                timeout=_SHUTDOWN_TIMEOUT_SECONDS,
            )
        except asyncio.TimeoutError:
            logger.error(
                "Timed out while stopping child processes after %.1f seconds",
                _SHUTDOWN_TIMEOUT_SECONDS,
            )

        await _detach_log_tails(app.state.pm, app.state.log_manager)

        health_task.cancel()
        try:
            await health_task
        except asyncio.CancelledError:
            pass


def create_app() -> FastAPI:
    app = FastAPI(
        title="DataWatch Agent",
        version="0.1.0",
        lifespan=lifespan,
    )

    app.add_exception_handler(RequestValidationError, validation_exception_handler)
    app.add_exception_handler(Exception, generic_exception_handler)

    from app.routers import bootstrap, auth, viewer, admin, local
    app.include_router(bootstrap.router)
    app.include_router(auth.router)
    app.include_router(viewer.router)
    app.include_router(admin.router)
    app.include_router(local.router)

    return app
