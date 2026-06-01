from __future__ import annotations

from turboapi import APIRouter, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import require_permissions
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.services.module_monitor import ModuleMonitorService


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
