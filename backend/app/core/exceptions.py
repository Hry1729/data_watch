from __future__ import annotations

import traceback
from turboapi import JSONResponse, Request

from app.core.log_writer import write_system_log_sync


class ApiError(Exception):
    def __init__(self, code: int, message: str, status_code: int = 200):
        self.code = code
        self.message = message
        self.status_code = status_code
        super().__init__(message)


class PermissionDeniedError(Exception):
    """权限拒绝异常"""
    def __init__(self, message: str = "权限不足"):
        self.message = message
        super().__init__(message)


def register_exception_handlers(app) -> None:
    @app.exception_handler(ApiError)
    async def api_error_handler(_: Request, exc: ApiError):
        return JSONResponse(
            status_code=exc.status_code,
            content={"code": exc.code, "msg": exc.message, "data": None},
        )

    @app.exception_handler(PermissionDeniedError)
    async def permission_denied_handler(_: Request, exc: PermissionDeniedError):
        # 记录权限拒绝日志
        write_system_log_sync(
            level="WARN",
            log_type="security",
            source="backend.permission",
            content=f"权限拒绝: {exc.message}",
            exception_stack=traceback.format_exc(),
        )
        return JSONResponse(
            status_code=200,
            content={"code": 403, "msg": exc.message, "data": None},
        )

    @app.exception_handler(Exception)
    async def unhandled_error_handler(request: Request, exc: Exception):
        # 记录未处理异常日志
        exception_stack = traceback.format_exc()
        write_system_log_sync(
            level="ERROR",
            log_type="system",
            source="backend.exception",
            content=f"未处理异常: {str(exc)[:500]}",
            exception_stack=exception_stack[:65535],
            detail={
                "path": getattr(request, "path", ""),
                "method": getattr(request, "method", ""),
                "exception_type": type(exc).__name__,
            },
        )
        # 不向客户端泄露内部异常细节（SQL/路径/驱动等）；详细异常已写入系统日志。
        return JSONResponse(
            status_code=500,
            content={"code": 500, "msg": "服务器内部错误", "data": None},
        )
