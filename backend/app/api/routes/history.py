from __future__ import annotations

import os
from datetime import datetime
from typing import Any

from turboapi.responses import FileResponse
from turboapi import APIRouter, Depends, Request, Response
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.tdengine import get_tdengine_client
from app.services.auth import AuthService
from app.services.history import HistoryService
from app.services.task_manager import get_export_task_manager


router = APIRouter(tags=["history"])
auth_scheme = HTTPBearer(auto_error=False)


async def _require_roles(
    credentials: HTTPAuthorizationCredentials | None,
    required_roles: set[str],
) -> dict[str, Any]:
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
    if credentials is None:
        return None
    return f"{credentials.scheme} {credentials.credentials}".strip()


@router.get("/api/device-monitor/history/devices")
async def history_devices(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        data = await service.get_devices()
        return success_response(data=data)


@router.get("/api/device-monitor/history/device-types")
async def history_device_types(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """获取设备类型列表（用于级联选择）"""
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device_types()
        return success_response(data=data)


@router.get("/api/device-monitor/history/points")
async def history_points(
    deviceId: str | int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    device_id = _require_positive_int(deviceId, "deviceId")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        data = await service.get_points(device_id=device_id)
        return success_response(data=data)


@router.get("/api/device-monitor/history/query")
async def history_query(
    request: Request,
    deviceId: str | int,
    startTime: str,
    endTime: str,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    device_id = _require_positive_int(deviceId, "deviceId")
    var_seqs = _extract_var_seqs(request)
    perspective_query_version = _parse_query_version(
        request.headers.get("X-Perspective-Query-Version")
    )

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        data = await service.query_history(
            device_id=device_id,
            var_seqs=var_seqs,
            start_time=startTime,
            end_time=endTime,
            perspective_session_id=_normalize_optional_header(
                request.headers.get("X-Perspective-Session-Id")
            ),
            perspective_query_version=perspective_query_version,
            authorization=request.headers.get("Authorization"),
        )
        return success_response(data=data)


@router.get("/api/device-monitor/history/chart")
async def history_chart(
    request: Request,
    deviceId: str | int,
    startTime: str,
    endTime: str,
    interval: str = "auto",
    aggregate: str = "avg",
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """获取降采样后的趋势数据"""
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    device_id = _require_positive_int(deviceId, "deviceId")
    var_seqs = _extract_var_seqs(request)

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        data = await service.get_chart_data(
            device_id=device_id,
            var_seqs=var_seqs,
            start_time=startTime,
            end_time=endTime,
            interval=interval,
            aggregate=aggregate,
        )
        return success_response(data=data)


@router.get("/api/device-monitor/history/raw")
async def history_raw(
    request: Request,
    deviceId: str | int,
    varSeq: str | int,
    startTime: str,
    endTime: str,
    pageSize: int = 1000,
    cursorTs: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    device_id = _require_positive_int(deviceId, "deviceId")
    var_seq = _require_positive_int(varSeq, "varSeq")
    # 强制转换并限制单页最大 5000 条
    page_size = min(_require_positive_int(pageSize, "pageSize"), 5000)

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        data = await service.get_raw_data(
            device_id=device_id,
            var_seq=var_seq,
            start_time=startTime,
            end_time=endTime,
            page_size=page_size,
            cursor_ts=cursorTs
        )
        return success_response(data=data)


@router.get("/api/device-monitor/history/raw-matrix")
async def history_raw_matrix(
    request: Request,
    deviceId: str | int,
    startTime: str,
    endTime: str,
    alignGranularity: str = "1s",
    pageSize: str | int = 200,
    cursorTs: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN", "R_USER"})
    device_id = _require_positive_int(deviceId, "deviceId")
    var_seqs = _extract_var_seqs(request)
    if len(var_seqs) > 10:
        raise ApiError(400, "varSeqs 最多 10 个点位")

    page_size = min(_require_positive_int(pageSize, "pageSize"), 500)

    if alignGranularity not in ("100ms", "500ms", "1s"):
        raise ApiError(400, f"不支持的 alignGranularity: {alignGranularity}")

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        data = await service.get_raw_matrix_data(
            device_id=device_id,
            var_seqs=var_seqs,
            start_time=startTime,
            end_time=endTime,
            align_granularity=alignGranularity,
            page_size=page_size,
            cursor_ts=cursorTs,
        )
        return success_response(data=data)


@router.post("/api/device-monitor/history/export")
async def history_export(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """创建异步导出任务（仅管理员）"""
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    device_id = _require_positive_int(payload.get("deviceId"), "deviceId")
    var_seqs = _extract_body_var_seqs(payload)
    start_time = _require_string(payload.get("startTime"), "startTime")
    end_time = _require_string(payload.get("endTime"), "endTime")

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        task_id = await service.create_export_task(
            device_id=device_id,
            var_seqs=var_seqs,
            start_time=start_time,
            end_time=end_time,
        )
        return success_response(data={"taskId": task_id})


@router.get("/api/device-monitor/history/export/{task_id}/status")
async def history_export_status(
    task_id: str,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """查询导出任务状态（仅管理员）"""
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    task_manager = get_export_task_manager()
    task = task_manager.get_task(task_id)
    if not task:
        raise ApiError(404, "Export task not found")

    return success_response(data={
        "status": task.status,
        "progress": task.progress,
        "error": task.error,
        "createdAt": task.created_at
    })


@router.get("/api/device-monitor/history/export/{task_id}/download")
async def history_export_download(
    task_id: str,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """下载导出结果文件（仅管理员）"""
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    task_manager = get_export_task_manager()
    task = task_manager.get_task(task_id)
    if not task:
        raise ApiError(404, "Export task not found")

    if task.status != "completed":
        raise ApiError(400, f"Task is not completed (status: {task.status})")

    if not task.file_path or not os.path.exists(task.file_path):
        raise ApiError(404, "Export file not found or expired")

    # 从元数据中获取设备名称作为文件名的一部分
    device_name = task.metadata.get("device_name", "device")
    filename = f"历史数据_{device_name}_{datetime.now().strftime('%Y%m%d%H%M%S')}.csv"

    return FileResponse(
        path=task.file_path,
        media_type="text/csv",
        filename=filename
    )



def _extract_var_seqs(request: Request) -> list[int]:
    query_params = request.query_params
    raw_values = [*query_params.getlist("varSeqs"), *query_params.getlist("varSeqs[]")]
    if not raw_values:
        raw_values = [*query_params.getlist("pointIds"), *query_params.getlist("pointIds[]")]
    var_seqs = _coerce_var_seqs(raw_values)
    if not var_seqs:
        raise ApiError(400, "varSeqs is required")
    return var_seqs


def _extract_body_var_seqs(payload: dict[str, Any]) -> list[int]:
    raw_values = payload.get("varSeqs")
    if raw_values is None:
        raw_values = payload.get("pointIds")
    var_seqs = _coerce_var_seqs(raw_values)
    if not var_seqs:
        raise ApiError(400, "varSeqs is required")
    return var_seqs


def _coerce_var_seqs(raw_values: Any) -> list[int]:
    if raw_values is None:
        return []
    if not isinstance(raw_values, list):
        raw_values = [raw_values]

    var_seqs: list[int] = []
    for raw_value in raw_values:
        if isinstance(raw_value, str) and "," in raw_value:
            fragments = [item.strip() for item in raw_value.split(",")]
        else:
            fragments = [raw_value]

        for fragment in fragments:
            text_value = str(fragment).strip()
            if not text_value:
                continue
            try:
                var_seqs.append(int(text_value))
            except ValueError as exc:
                raise ApiError(400, f"Invalid varSeq: {text_value}") from exc

    return var_seqs


def _parse_query_version(raw_value: str | None) -> int:
    if raw_value is None or not raw_value.strip():
        return 0
    try:
        return max(0, int(raw_value))
    except ValueError as exc:
        raise ApiError(400, "X-Perspective-Query-Version must be an integer") from exc


def _normalize_optional_header(raw_value: str | None) -> str | None:
    if raw_value is None:
        return None
    normalized = raw_value.strip()
    return normalized or None


def _require_positive_int(value: Any, field_name: str) -> int:
    try:
        parsed = int(value)
    except (TypeError, ValueError) as exc:
        raise ApiError(400, f"{field_name} must be a positive integer") from exc
    if parsed <= 0:
        raise ApiError(400, f"{field_name} must be a positive integer")
    return parsed


def _require_string(value: Any, field_name: str) -> str:
    normalized = str(value or "").strip()
    if not normalized:
        raise ApiError(400, f"{field_name} is required")
    return normalized
