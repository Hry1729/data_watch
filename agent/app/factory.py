import asyncio
import logging
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError

from app.core.config import data_root, install_root, load_config, module_paths, mutate_config
from app.core.exceptions import generic_exception_handler, validation_exception_handler
from app.core.role import build_role, managed_module_names
from app.core.security import dpapi_decrypt
from app.routers.admin import _collector_env, _edge_storage_url, _storage_env
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
    ordered = [
        name
        for name in ("uploader", "collector", "ingest", "writer", "storage")
        if name in pm.modules
    ]
    for name in ordered:
        try:
            await pm.stop(name)
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
    while True:
        await asyncio.sleep(2.0)
        try:
            if pm.status("storage").status != "crashed":
                continue
            await pm.restart("storage", env=_storage_env())
            await asyncio.to_thread(app.state.edge_storage.wait_ready)
            app.state.log_manager.attach(
                "storage",
                pm.log_file("storage"),
                asyncio.get_running_loop(),
            )
            logger.warning("Edge Storage crashed and was restarted")
        except asyncio.CancelledError:
            raise
        except Exception:
            logger.exception("Failed to restart crashed Edge Storage")


async def _recover_drivers_sync_collector(app: FastAPI) -> None:
    """磁盘恢复后统一重启 collector，并以 /drivers 验证运行代际。"""
    installer: DriverInstaller = app.state.driver_installer
    pm: ProcessManager = app.state.pm
    sm = app.state.sidecar_manager

    async def _restart() -> None:
        settings_path = sm.collector_settings_path
        if settings_path is None or not Path(settings_path).exists():
            raise RuntimeError("collector settings.toml not found; run bootstrap first")
        plan = sm.prepare_for_collector_start()
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
    cfg = load_config()
    runtime_dir = Path(cfg["agent"].get("runtime_dir", r"C:\ProgramData\DataWatch\runtime"))
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
    app.state.driver_installer = DriverInstaller(data_root() / "drivers", stop_collector=lambda: pm.stop("collector"), collector_pid=lambda: pm.status("collector").pid)
    app.state.driver_installer.ensure_layout()
    if build_role() == "edge":
        app.state.edge_storage = EdgeStorageClient(_edge_storage_url(), timeout_seconds=2.0)
        try:
            await pm.start("storage", env=_storage_env())
            await asyncio.to_thread(app.state.edge_storage.wait_ready)
            app.state.log_manager.attach(
                "storage",
                pm.log_file("storage"),
                asyncio.get_running_loop(),
            )
        except Exception:
            logger.exception("Edge Storage failed to start during Agent startup")
    # 崩溃补偿只属于包含 Collector 的角色。
    if "collector" in modules:
        await _recover_drivers_sync_collector(app)

    health_task = asyncio.create_task(pm.health_check_loop())
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
        )
        app.state.edge_sync = edge_sync
        edge_sync_task = asyncio.create_task(edge_sync.run_forever())
    try:
        yield
    finally:
        logger.info("Agent shutting down...")
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
