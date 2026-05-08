from __future__ import annotations

from datetime import datetime
from typing import Any

from turboapi import APIRouter, Depends
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.core.time_utils import parse_local_datetime
from app.db.mysql import get_mysql_session_factory
from app.services.auth import AuthService
from app.services.log import LogService


router = APIRouter(tags=["log"])
auth_scheme = HTTPBearer(auto_error=False)


@router.get("/api/device-monitor/log/list")
async def log_list(
    pageNo: str | None = None,
    pageSize: str | None = None,
    current: str | None = None,
    size: str | None = None,
    keyword: str | None = None,
    startTime: str | None = None,
    endTime: str | None = None,
    level: str | None = None,
    type: str | None = None,
    kind: str | None = None,
    source: str | None = None,
    serviceName: str | None = None,
    deviceCode: str | None = None,
    operatorName: str | None = None,
    requestId: str | None = None,
    traceId: str | None = None,
    resultStatus: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """日志列表查询"""
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = LogService(session=session)
        data = await service.list_logs(
            _compact_params(
                {
                    "pageNo": pageNo,
                    "pageSize": pageSize,
                    "current": current,
                    "size": size,
                    "keyword": keyword,
                    "startTime": startTime,
                    "endTime": endTime,
                    "level": level,
                    "type": type,
                    "kind": kind,
                    "source": source,
                    "serviceName": serviceName,
                    "deviceCode": deviceCode,
                    "operatorName": operatorName,
                    "requestId": requestId,
                    "traceId": traceId,
                    "resultStatus": resultStatus,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/device-monitor/log/stats")
async def log_stats(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """日志统计"""
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = LogService(session=session)
        data = await service.get_stats()
        return success_response(data=data)


@router.get("/api/device-monitor/log/policy")
async def get_log_policy(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """获取日志清理策略"""
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = LogService(session=session)
        policy = await service.get_policy()
        data = _serialize_policy(policy)
        return success_response(data=data)


@router.put("/api/device-monitor/log/policy")
async def update_log_policy(
    params: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """更新日志清理策略（仅 R_SUPER）"""
    principal = await _require_roles(credentials, {"R_SUPER"})
    operator_id = principal.get("user_id")
    operator_name = principal.get("user_name", "system")

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = LogService(session=session)
        policy = await service.update_policy(params, operator_id, operator_name)
        data = _serialize_policy(policy)
        return success_response(data=data)


@router.post("/api/device-monitor/log/debug")
async def toggle_log_debug(
    params: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """切换 DEBUG 日志落库开关（仅 R_SUPER）"""
    principal = await _require_roles(credentials, {"R_SUPER"})
    operator_id = principal.get("user_id")
    operator_name = principal.get("user_name", "system")

    enabled = params.get("enabled", False)
    duration_minutes = params.get("durationMinutes")
    reason = params.get("reason")

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = LogService(session=session)
        policy = await service.toggle_debug(
            enabled=bool(enabled),
            duration_minutes=int(duration_minutes) if duration_minutes else None,
            reason=str(reason) if reason else None,
            operator_id=operator_id,
            operator_name=operator_name,
        )
        data = _serialize_policy(policy)
        return success_response(data=data)


@router.post("/api/device-monitor/log/cleanup")
async def cleanup_logs(
    params: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """手动清理日志（仅 R_SUPER）"""
    principal = await _require_roles(credentials, {"R_SUPER"})
    operator_id = principal.get("user_id")
    operator_name = principal.get("user_name", "system")

    mode = params.get("mode", "policy")
    before_time = _coerce_datetime(params.get("beforeTime"))
    start_time = _coerce_datetime(params.get("startTime"))
    end_time = _coerce_datetime(params.get("endTime"))
    force = bool(params.get("force", False))

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = LogService(session=session)
        data = await service.cleanup_logs(
            mode=str(mode),
            before_time=before_time,
            start_time=start_time,
            end_time=end_time,
            force=force,
            operator_id=operator_id,
            operator_name=operator_name,
        )
        return success_response(data=data)


@router.post("/api/device-monitor/log/client")
async def report_client_log(
    params: dict[str, Any],
    x_forwarded_for: str | None = None,
    x_real_ip: str | None = None,
):
    """前端客户端日志上报（带限流）

    允许匿名上报，每个来源每分钟最多 30 条日志。
    """
    # 添加客户端 IP 到参数
    client_ip = _resolve_client_ip(x_forwarded_for, x_real_ip)
    params["clientIp"] = client_ip

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = LogService(session=session)
        result = await service.report_client_log(params)
        return success_response(data=result)


# ==================== 辅助函数 ====================


async def _require_roles(
    credentials: HTTPAuthorizationCredentials | None,
    required_roles: set[str],
) -> dict[str, Any]:
    """验证用户角色"""
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        principal = await AuthService(session).resolve_principal(
            _authorization_from_credentials(credentials)
        )
    if not required_roles.intersection(set(principal["roles"])):
        raise ApiError(403, "无权限访问该接口")
    return principal


def _authorization_from_credentials(
    credentials: HTTPAuthorizationCredentials | None,
) -> str | None:
    """从 credentials 构造 authorization 字串"""
    if credentials is None:
        return None
    return f"{credentials.scheme} {credentials.credentials}".strip()


def _compact_params(params: dict[str, Any]) -> dict[str, Any]:
    """移除空值参数"""
    return {key: value for key, value in params.items() if value is not None}


def _coerce_datetime(value: Any) -> datetime | None:
    """转换日期时间为项目本地时间。"""
    return parse_local_datetime(value)


def _serialize_policy(policy: Any) -> dict[str, Any]:
    """序列化策略配置"""
    return {
        "enabled": bool(policy.enabled),
        "retentionDays": policy.retention_days,
        "maxTotalMb": policy.max_total_mb,
        "cleanupTime": policy.cleanup_time,
        "capacityCheckIntervalMinutes": policy.capacity_check_interval_minutes,
        "batchSize": policy.batch_size,
        "debugDbEnabled": bool(policy.debug_db_enabled),
        "debugEnabledUntil": policy.debug_enabled_until.strftime("%Y-%m-%d %H:%M:%S") if policy.debug_enabled_until else None,
        "debugReason": policy.debug_reason,
        "debugEnabledBy": policy.debug_enabled_by_name,
        "lastCleanupTime": policy.last_cleanup_time.strftime("%Y-%m-%d %H:%M:%S") if policy.last_cleanup_time else None,
        "updateBy": policy.update_by_name,
        "updateTime": policy.update_time.strftime("%Y-%m-%d %H:%M:%S") if policy.update_time else None,
    }


def _resolve_client_ip(x_forwarded_for: str | None, x_real_ip: str | None) -> str:
    """解析客户端 IP"""
    if x_forwarded_for:
        first_ip = x_forwarded_for.split(",", 1)[0].strip()
        if first_ip:
            return first_ip
    if x_real_ip and x_real_ip.strip():
        return x_real_ip.strip()
    return "unknown"

