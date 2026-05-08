"""日志写入助手模块

提供异步写入系统日志和操作日志的能力，用于：
- 后端启动/关闭
- 登录/登出/登录失败
- 权限拒绝
- 未处理异常和 500 错误
- 定时任务执行结果
"""
from __future__ import annotations

import asyncio
import traceback
from datetime import datetime
from typing import Any

from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker

from app.core.logging import get_logger
from app.core.time_utils import now_local
from app.db.mysql import get_mysql_session_factory
from app.models import OperationLog, SystemLog

logger = get_logger("backend.log_writer")


async def write_system_log(
    *,
    level: str,
    log_type: str,
    source: str,
    content: str,
    device_code: str = "",
    service_name: str = "backend",
    detail: dict[str, Any] | None = None,
    trace_id: str = "",
    exception_stack: str | None = None,
    elapsed_ms: int | None = None,
) -> None:
    """写入系统运行日志（异步，独立事务）

    Args:
        level: 日志级别 DEBUG/INFO/WARN/ERROR/CRITICAL
        log_type: 日志类型 system/auth/operation/collect/storage/frontend/security/alarm
        source: 日志来源
        content: 日志内容
        device_code: 设备编码
        service_name: 服务名
        detail: 结构化详情
        trace_id: 链路追踪ID
        exception_stack: 异常堆栈
        elapsed_ms: 耗时毫秒
    """
    try:
        session_factory = get_mysql_session_factory()
        async with session_factory() as session:
            log_entry = SystemLog(
                log_time=now_local(),
                level=level,
                log_type=log_type,
                source=source,
                device_code=device_code,
                host_name="",
                service_name=service_name,
                content=content[:500],
                detail_json=detail,
                trace_id=trace_id,
                exception_stack=exception_stack[:65535] if exception_stack else None,
                elapsed_ms=elapsed_ms,
            )
            session.add(log_entry)
            await session.commit()
    except Exception as e:
        logger.error(f"Failed to write system log: {e}")


async def write_operation_log(
    *,
    operator_id: int | None = None,
    operator_name: str = "system",
    module_name: str,
    action_name: str,
    level: str = "INFO",
    content: str,
    detail: dict[str, Any] | None = None,
    request_id: str = "",
    ip: str = "",
    user_agent: str = "",
    biz_type: str = "",
    biz_id: str = "",
    duration_ms: int | None = None,
    result_status: str = "success",
) -> None:
    """写入操作审计日志（异步，独立事务）

    Args:
        operator_id: 操作人ID
        operator_name: 操作人名称
        module_name: 模块名
        action_name: 动作名
        level: 日志级别
        content: 日志内容
        detail: 结构化详情
        request_id: 请求ID
        ip: 客户端IP
        user_agent: User-Agent
        biz_type: 业务类型
        biz_id: 业务ID
        duration_ms: 耗时毫秒
        result_status: 结果状态 success/failed/denied
    """
    try:
        session_factory = get_mysql_session_factory()
        async with session_factory() as session:
            log_entry = OperationLog(
                log_time=now_local(),
                operator_id=operator_id,
                operator_name=operator_name,
                module_name=module_name,
                action_name=action_name,
                level=level,
                content=content[:500],
                detail_json=detail,
                request_id=request_id,
                ip=ip,
                user_agent=user_agent,
                biz_type=biz_type,
                biz_id=biz_id,
                duration_ms=duration_ms,
                result_status=result_status,
            )
            session.add(log_entry)
            await session.commit()
    except Exception as e:
        logger.error(f"Failed to write operation log: {e}")


def write_system_log_sync(
    *,
    level: str,
    log_type: str,
    source: str,
    content: str,
    device_code: str = "",
    service_name: str = "backend",
    detail: dict[str, Any] | None = None,
    trace_id: str = "",
    exception_stack: str | None = None,
    elapsed_ms: int | None = None,
) -> None:
    """写入系统运行日志（同步版本，用于非异步上下文）

    在后台线程中执行异步写入，不会阻塞调用方。
    """
    try:
        loop = asyncio.get_running_loop()
        # 已经在事件循环中，创建任务
        loop.create_task(
            write_system_log(
                level=level,
                log_type=log_type,
                source=source,
                content=content,
                device_code=device_code,
                service_name=service_name,
                detail=detail,
                trace_id=trace_id,
                exception_stack=exception_stack,
                elapsed_ms=elapsed_ms,
            )
        )
    except RuntimeError:
        # 没有运行中的事件循环，创建新事件循环
        asyncio.run(
            write_system_log(
                level=level,
                log_type=log_type,
                source=source,
                content=content,
                device_code=device_code,
                service_name=service_name,
                detail=detail,
                trace_id=trace_id,
                exception_stack=exception_stack,
                elapsed_ms=elapsed_ms,
            )
        )


def write_operation_log_sync(
    *,
    operator_id: int | None = None,
    operator_name: str = "system",
    module_name: str,
    action_name: str,
    level: str = "INFO",
    content: str,
    detail: dict[str, Any] | None = None,
    request_id: str = "",
    ip: str = "",
    user_agent: str = "",
    biz_type: str = "",
    biz_id: str = "",
    duration_ms: int | None = None,
    result_status: str = "success",
) -> None:
    """写入操作审计日志（同步版本，用于非异步上下文）

    在后台线程中执行异步写入，不会阻塞调用方。
    """
    try:
        loop = asyncio.get_running_loop()
        loop.create_task(
            write_operation_log(
                operator_id=operator_id,
                operator_name=operator_name,
                module_name=module_name,
                action_name=action_name,
                level=level,
                content=content,
                detail=detail,
                request_id=request_id,
                ip=ip,
                user_agent=user_agent,
                biz_type=biz_type,
                biz_id=biz_id,
                duration_ms=duration_ms,
                result_status=result_status,
            )
        )
    except RuntimeError:
        asyncio.run(
            write_operation_log(
                operator_id=operator_id,
                operator_name=operator_name,
                module_name=module_name,
                action_name=action_name,
                level=level,
                content=content,
                detail=detail,
                request_id=request_id,
                ip=ip,
                user_agent=user_agent,
                biz_type=biz_type,
                biz_id=biz_id,
                duration_ms=duration_ms,
                result_status=result_status,
            )
        )

