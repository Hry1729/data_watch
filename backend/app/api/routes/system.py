from __future__ import annotations

from turboapi import APIRouter

from app.core.config import get_settings
from app.core.responses import success_response


router = APIRouter(tags=["system"])


@router.get("/healthz")
async def healthz():
    settings = get_settings()
    return success_response(
        data={
            "service": "backend",
            "stage": "monitor-read-ready",
            "apiPrefix": settings.api_prefix,
            "mysqlDatabase": settings.mysql_database,
            "tdengineDatabase": settings.tdengine_database,
        },
        msg="backend monitor read routes ready",
    )


@router.get("/api/device-monitor/system/ping")
async def device_monitor_ping():
    settings = get_settings()
    return success_response(
        data={
            "service": "device-monitor",
            "stage": "monitor-read-ready",
            "nextStep": "ws/realtime + local startup validation",
            "apiPrefix": settings.api_prefix,
        },
        msg="device-monitor backend reachable",
    )


@router.get("/api/cli/compatibility")
async def cli_compatibility():
    settings = get_settings()
    return success_response(
        data={
            "backendVersion": settings.app_version,
            "apiVersion": settings.cli_api_version,
            "minCliVersion": settings.cli_min_version,
            "maxCliVersion": settings.cli_max_version,
        }
    )
