"""操作审计装饰器

用于自动记录 CRUD 操作审计日志。
"""
from __future__ import annotations

import functools
import time
from typing import Any, Callable

from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.log_writer import write_operation_log
from app.db.mysql import get_mysql_session_factory
from app.services.auth import AuthService

auth_scheme = HTTPBearer(auto_error=False)


def audit_log(
    *,
    module_name: str,
    action_name: str,
    biz_type: str = "",
    level: str = "INFO",
    success_msg: str | None = None,
    failure_msg: str | None = None,
):
    """操作审计装饰器

    用于记录 CRUD 操作审计日志。

    Args:
        module_name: 模块名（如 device, point, user, role）
        action_name: 动作名（如 create, update, delete）
        biz_type: 业务类型
        level: 日志级别
        success_msg: 成功消息模板
        failure_msg: 失败消息模板

    Usage:
        @audit_log(module_name="device", action_name="create")
        async def create_device(...):
            ...
    """

    def decorator(func: Callable) -> Callable:
        @functools.wraps(func)
        async def wrapper(*args, **kwargs) -> Any:
            start_time = time.time()
            result_status = "success"
            content = success_msg or f"{module_name}.{action_name}"
            detail: dict[str, Any] = {}
            operator_id = None
            operator_name = "system"
            request_id = ""

            # 尝试获取用户信息
            for arg in args:
                if isinstance(arg, HTTPAuthorizationCredentials):
                    try:
                        session_factory = get_mysql_session_factory()
                        async with session_factory() as session:
                            auth_service = AuthService(session)
                            auth_header = f"{arg.scheme} {arg.credentials}"
                            principal = await auth_service.resolve_principal(auth_header)
                            operator_id = principal["user_id"]
                            operator_name = principal["user_name"]
                    except Exception:
                        pass
                    break

            # 从 kwargs 中查找 credentials
            if operator_id is None:
                for key, value in kwargs.items():
                    if isinstance(value, HTTPAuthorizationCredentials):
                        try:
                            session_factory = get_mysql_session_factory()
                            async with session_factory() as session:
                                auth_service = AuthService(session)
                                auth_header = f"{value.scheme} {value.credentials}"
                                principal = await auth_service.resolve_principal(auth_header)
                                operator_id = principal["user_id"]
                                operator_name = principal["user_name"]
                        except Exception:
                            pass
                        break

            # 从 kwargs 中提取业务信息
            if "id" in kwargs:
                detail["id"] = kwargs["id"]
            if "ids" in kwargs:
                detail["ids"] = kwargs["ids"]
            if "payload" in kwargs:
                payload = kwargs["payload"]
                if hasattr(payload, "model_dump"):
                    detail["payload"] = payload.model_dump()
                elif isinstance(payload, dict):
                    detail["payload"] = payload

            try:
                result = await func(*args, **kwargs)

                # 尝试从返回值中提取业务ID
                if isinstance(result, dict):
                    if "data" in result:
                        data = result["data"]
                        if isinstance(data, dict) and "id" in data:
                            detail["created_id"] = data["id"]
                        elif isinstance(data, list):
                            detail["count"] = len(data)

                duration_ms = int((time.time() - start_time) * 1000)

                # ✅ 审计写入用 try-except 保护，失败不影响业务结果
                try:
                    await write_operation_log(
                        operator_id=operator_id,
                        operator_name=operator_name,
                        module_name=module_name,
                        action_name=action_name,
                        level=level,
                        content=content,
                        detail=detail,
                        request_id=request_id,
                        duration_ms=duration_ms,
                        result_status=result_status,
                        biz_type=biz_type,
                    )
                except Exception as audit_error:
                    # 审计失败只记录日志，不影响业务结果
                    import logging
                    logging.getLogger(__name__).warning(
                        f"审计日志写入失败 {module_name}.{action_name}: {audit_error}"
                    )

                return result

            except Exception as e:
                result_status = "failed"
                content = failure_msg or f"{module_name}.{action_name} 失败: {str(e)[:200]}"
                detail["error"] = str(e)[:500]

                duration_ms = int((time.time() - start_time) * 1000)

                # ✅ 业务失败的审计也用 try-except 保护
                try:
                    await write_operation_log(
                        operator_id=operator_id,
                        operator_name=operator_name,
                        module_name=module_name,
                        action_name=action_name,
                        level="ERROR",
                        content=content,
                        detail=detail,
                        request_id=request_id,
                        duration_ms=duration_ms,
                        result_status=result_status,
                        biz_type=biz_type,
                    )
                except Exception as audit_error:
                    import logging
                    logging.getLogger(__name__).warning(
                        f"审计日志写入失败 {module_name}.{action_name}: {audit_error}"
                    )

                # ✅ 重新抛出原始业务异常，不是审计异常
                raise

        return wrapper

    return decorator
