from __future__ import annotations

import asyncio
from datetime import datetime, timezone
import time

from fastapi import (
    APIRouter,
    Depends,
    HTTPException,
    Request,
    WebSocket,
    WebSocketDisconnect,
)

from app.core.config import load_config
from app.core.deps import get_current_user
from app.core.security import verify_token
from app.schemas.models import (
    ModuleStatusResponse,
    ProcessStatusResponse,
    ProxyLogItem,
    ServiceHealthItem,
    ViewerHealthResponse,
    ViewerStatusResponse,
)
from app.services.health_checker import check_mysql, check_redis, check_tdengine

router = APIRouter(tags=["viewer"])

WS_AUTH_TIMEOUT_SECONDS = 10.0


@router.get("/viewer/status", response_model=ViewerStatusResponse)
async def viewer_status():
    return ViewerStatusResponse()


@router.get("/viewer/health", response_model=ViewerHealthResponse)
async def viewer_health(_user: str = Depends(get_current_user)):
    cfg = load_config()
    if not (cfg.get("agent") or {}).get("bootstrap_done", False):
        item = ServiceHealthItem(
            status="unknown",
            message="Bootstrap must be completed first",
        )
        return ViewerHealthResponse(redis=item, mysql=item, tdengine=item)

    checkers = {
        "redis": check_redis,
        "mysql": check_mysql,
        "tdengine": check_tdengine,
    }

    async def run_check(target: str) -> ServiceHealthItem:
        checked_at = datetime.now(timezone.utc).isoformat()
        started = time.perf_counter()
        try:
            result = await asyncio.to_thread(checkers[target], dict(cfg.get(target) or {}))
        except Exception:
            return ServiceHealthItem(
                status="disconnected",
                message=f"{target} health check failed",
                checkedAt=checked_at,
            )

        latency_ms = int(round((time.perf_counter() - started) * 1000))
        return ServiceHealthItem(
            status="connected" if result.success else "disconnected",
            latencyMs=latency_ms,
            message=result.message,
            checkedAt=checked_at,
        )

    redis, mysql, tdengine = await asyncio.gather(
        run_check("redis"),
        run_check("mysql"),
        run_check("tdengine"),
    )
    return ViewerHealthResponse(redis=redis, mysql=mysql, tdengine=tdengine)


@router.get(
    "/viewer/processes/{name}/status",
    response_model=ProcessStatusResponse,
)
async def process_status(
    name: str,
    request: Request,
    _user: str = Depends(get_current_user),
):
    pm = request.app.state.pm
    if name not in pm.modules:
        raise HTTPException(status_code=422, detail=f"unknown module: {name}")
    info = pm.status(name)
    return ProcessStatusResponse(status=info.status)


@router.get(
    "/viewer/processes/{name}/metrics",
    response_model=ModuleStatusResponse,
)
async def process_metrics(
    name: str,
    request: Request,
    _user: str = Depends(get_current_user),
):
    pm = request.app.state.pm
    if name not in pm.modules:
        raise HTTPException(status_code=422, detail=f"unknown module: {name}")
    info = pm.status(name)
    # 可观测：暴露日志路径供现场排查。proxy_logs 仅 collector 有（writer 为 []）。
    proxy_logs: list[ProxyLogItem] = []
    if name == "collector":
        sm = getattr(request.app.state, "sidecar_manager", None)
        if sm is not None:
            proxy_logs = [
                ProxyLogItem(endpoint=ep, port=port, log_path=log_path)
                for ep, port, log_path in sm.proxy_log_snapshot()
            ]
    return ModuleStatusResponse(
        name=info.name,
        status=info.status,
        pid=info.pid,
        uptime_seconds=info.uptime_seconds,
        memory_mb=info.memory_mb,
        cpu_percent=info.cpu_percent,
        started_at=info.started_at,
        restart_count=info.restart_count,
        log_path=str(pm.log_file(name)),
        proxy_logs=proxy_logs,
    )


@router.websocket("/viewer/logs/{name}/tail")
async def tail_logs(name: str, websocket: WebSocket):
    if name not in {"collector", "writer"}:
        await websocket.close(code=1008)
        return

    await websocket.accept()

    # 首帧鉴权（10s 超时）
    try:
        frame = await asyncio.wait_for(
            websocket.receive_json(),
            timeout=WS_AUTH_TIMEOUT_SECONDS,
        )
    except asyncio.TimeoutError:
        await websocket.close(code=4001)
        return
    except WebSocketDisconnect:
        return

    if not isinstance(frame, dict) or "token" not in frame:
        await websocket.close(code=4001)
        return

    try:
        verify_token(frame.get("token", ""), load_config()["agent"]["jwt_secret"])
    except Exception:
        await websocket.close(code=4001)
        return

    log_manager = websocket.app.state.log_manager
    # 先 subscribe 再推历史，避免两步之间漏行（代价：突发并发下可能重复若干条；
    # 如需严格去重，LogLine 需带单调序号/offset）。
    q = log_manager.subscribe(name)
    try:
        for entry in log_manager.get_history(name):
            await websocket.send_text(entry.model_dump_json())
        while True:
            entry = await q.get()
            await websocket.send_text(entry.model_dump_json())
    except WebSocketDisconnect:
        return
    finally:
        log_manager.unsubscribe(name, q)
