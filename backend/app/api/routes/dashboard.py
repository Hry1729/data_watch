from __future__ import annotations

from typing import Any

from turboapi import APIRouter, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import require_permissions
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.tdengine import get_tdengine_client
from app.services.dashboard import DashboardService


router = APIRouter(tags=["dashboard"])
auth_scheme = HTTPBearer(auto_error=False)


@router.get("/api/device-monitor/dashboard/overview")
async def dashboard_overview(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:dashboard:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DashboardService(session=session, tdengine=get_tdengine_client())
        data = await service.get_overview()
        return success_response(data=data)


@router.get("/api/device-monitor/dashboard/device-type-distribution")
async def dashboard_device_type_distribution(
    topN: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:dashboard:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DashboardService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device_type_distribution(_compact_params({"topN": topN}))
        return success_response(data=data)


@router.get("/api/device-monitor/dashboard/recent-events")
async def dashboard_recent_events(
    limit: str | None = None,
    eventType: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:dashboard:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DashboardService(session=session, tdengine=get_tdengine_client())
        data = await service.get_recent_events(
            _compact_params(
                {
                    "limit": limit,
                    "eventType": eventType,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/device-monitor/dashboard/trend")
async def dashboard_trend(
    rangeType: str | None = None,
    granularity: str | None = None,
    metricKeys: list[str] | str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:dashboard:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DashboardService(session=session, tdengine=get_tdengine_client())
        data = await service.get_trend(
            _compact_params(
                {
                    "rangeType": rangeType,
                    "granularity": granularity,
                    "metricKeys": metricKeys,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/device-monitor/dashboard/device-connection-health")
async def dashboard_device_connection_health(
    statRange: str | None = None,
    topN: str | None = None,
    includeOffline: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:dashboard:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DashboardService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device_connection_health(
            _compact_params(
                {
                    "statRange": statRange,
                    "topN": topN,
                    "includeOffline": includeOffline,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/device-monitor/dashboard/alarm-distribution")
async def dashboard_alarm_distribution(
    statRange: str | None = None,
    groupBy: str | None = None,
    topN: str | None = None,
    includeOther: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:dashboard:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DashboardService(session=session, tdengine=get_tdengine_client())
        data = await service.get_alarm_distribution(
            _compact_params(
                {
                    "statRange": statRange,
                    "groupBy": groupBy,
                    "topN": topN,
                    "includeOther": includeOther,
                }
            )
        )
        return success_response(data=data)


def _compact_params(params: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in params.items() if value is not None}
