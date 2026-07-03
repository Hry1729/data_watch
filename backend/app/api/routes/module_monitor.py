from __future__ import annotations

from typing import Any

from turboapi import APIRouter, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import require_permissions
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.schemas.collector import CollectorConfigPayload
from app.services.collector_config import CollectorConfigService
from app.services.module_monitor import ModuleMonitorService
from app.services.rate_sampler import get_rate_sampler


router = APIRouter(tags=["module-monitor"])
auth_scheme = HTTPBearer(auto_error=False)


@router.get("/api/device-monitor/module-monitor/collector")
async def module_monitor_collector(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = ModuleMonitorService(session=session)
        data = await service.get_collector_metrics()
        return success_response(data=data)


@router.get("/api/device-monitor/module-monitor/writer")
async def module_monitor_writer(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = ModuleMonitorService(session=session)
        data = await service.get_writer_metrics()
        return success_response(data=data)


@router.get("/api/device-monitor/module-monitor/rates")
async def module_monitor_rates(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    # 速率/趋势取自内存滚动缓冲（后台采样器维护），无需 DB/会话
    return success_response(data=get_rate_sampler().snapshot())


@router.get("/api/device-monitor/collector-config")
async def get_collector_config(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CollectorConfigService(session)
        data = await service.get_settings()
        return success_response(data=data)


@router.put("/api/device-monitor/collector-config")
async def update_collector_config(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    # TODO: 改配置应使用 edit 级权限；项目暂无 collector config 专用权限码，沿用 monitor:view
    await require_permissions(credentials, {"device_monitor:monitor:view", "device_monitor:realtime:view"})
    config_payload = CollectorConfigPayload.from_dict(payload)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CollectorConfigService(session)
        data = await service.update_stream_maxlen(config_payload.stream_maxlen)
        return success_response(data=data)
