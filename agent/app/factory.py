import asyncio
import logging
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError

from app.core.config import load_config
from app.core.exceptions import generic_exception_handler, validation_exception_handler
from app.services.log_manager import LogManager
from app.services.process_manager import ModuleSpec, ProcessManager

logger = logging.getLogger(__name__)

_MODULE_NAMES = ("collector", "writer")
_SHUTDOWN_TIMEOUT_SECONDS = 25.0


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
    modules = {
        name: ModuleSpec(
            name=name,
            executable=cfg.get(name, {}).get("executable", ""),
            args=list(cfg.get(name, {}).get("args", [])),
            working_dir=cfg.get(name, {}).get("working_dir", ""),
        )
        for name in ("collector", "writer")
    }
    pm = ProcessManager(runtime_dir=runtime_dir, modules=modules)
    app.state.pm = pm
    app.state.log_manager = LogManager()

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
