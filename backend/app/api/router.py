from turboapi import APIRouter

from app.api.routes.auth import router as auth_router
from app.api.routes.catalog import router as catalog_router
from app.api.routes.dashboard import router as dashboard_router
from app.api.routes.history import router as history_router
from app.api.routes.log import router as log_router
from app.api.routes.monitor import router as monitor_router
from app.api.routes.system_manage import router as system_manage_router
from app.api.routes.system import router as system_router


api_router = APIRouter()
api_router.include_router(auth_router)
api_router.include_router(system_router)
api_router.include_router(catalog_router)
api_router.include_router(dashboard_router)
api_router.include_router(log_router)
api_router.include_router(monitor_router)
api_router.include_router(history_router)
api_router.include_router(system_manage_router)
