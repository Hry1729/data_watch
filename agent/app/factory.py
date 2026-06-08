import asyncio
import logging
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError

from app.core.config import install_root, load_config, module_paths
from app.core.exceptions import generic_exception_handler, validation_exception_handler
from app.services.log_manager import LogManager
from app.services.managed_process_runner import ManagedProcessRunner
from app.services.process_manager import ModuleSpec, ProcessManager
from app.services.sidecar_manager import (
    DEFAULT_CHANNEL,
    DEFAULT_SERVER_PROG_ID,
    SidecarManager,
)

logger = logging.getLogger(__name__)

_MODULE_NAMES = ("collector", "writer")
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
    for name in _MODULE_NAMES:
        try:
            await pm.stop(name)
        except Exception:
            logger.exception("Failed to stop module %s during Agent shutdown", name)


async def _detach_log_tails(log_manager: LogManager) -> None:
    for name in _MODULE_NAMES:
        try:
            await asyncio.to_thread(log_manager.detach, name)
        except Exception:
            logger.exception("Failed to detach log tail for %s", name)


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Stage 3 将在此处初始化 LogManager（日志 tail）
    cfg = load_config()
    runtime_dir = Path(cfg["agent"].get("runtime_dir", r"C:\ProgramData\DataWatch\runtime"))
    modules = {}
    for name in ("collector", "writer"):
        exe, working_dir = module_paths(name)
        modules[name] = ModuleSpec(
            name=name,
            executable=str(exe),
            args=list(cfg.get(name, {}).get("args", [])),
            working_dir=str(working_dir),
        )
    pm = ProcessManager(runtime_dir=runtime_dir, modules=modules)
    app.state.pm = pm
    app.state.log_manager = LogManager()
    app.state.sidecar_manager = build_sidecar_manager(cfg, runtime_dir)
    app.state.sidecar_manager.claim_existing_proxies()  # 认领重启前仍存活的 proxy（T022/FR-P15）

    health_task = asyncio.create_task(pm.health_check_loop())
    try:
        yield
    finally:
        logger.info("Agent shutting down...")
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

        await _detach_log_tails(app.state.log_manager)

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

    from app.routers import bootstrap, auth, viewer, admin
    app.include_router(bootstrap.router)
    app.include_router(auth.router)
    app.include_router(viewer.router)
    app.include_router(admin.router)

    return app
