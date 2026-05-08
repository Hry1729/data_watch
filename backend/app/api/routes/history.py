from __future__ import annotations

from datetime import datetime
from typing import Any

from turboapi import APIRouter, Request, Response

from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.tdengine import get_tdengine_client
from app.services.history import HistoryService


router = APIRouter(tags=["history"])


@router.get("/api/device-monitor/history/devices")
async def history_devices():
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        data = await service.get_devices()
        return success_response(data=data)


@router.get("/api/device-monitor/history/points")
async def history_points(deviceId: str | int):
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
):
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


@router.post("/api/device-monitor/history/export")
async def history_export(payload: dict[str, Any]):
    device_id = _require_positive_int(payload.get("deviceId"), "deviceId")
    var_seqs = _extract_body_var_seqs(payload)
    start_time = _require_string(payload.get("startTime"), "startTime")
    end_time = _require_string(payload.get("endTime"), "endTime")

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = HistoryService(session=session, tdengine=get_tdengine_client())
        content = await service.export_history_csv(
            device_id=device_id,
            var_seqs=var_seqs,
            start_time=start_time,
            end_time=end_time,
        )

    filename = f"history-data-{datetime.now().strftime('%Y%m%d-%H%M%S')}.csv"
    return Response(
        content=content,
        media_type="text/csv; charset=utf-8",
        headers={
            "Content-Disposition": f'attachment; filename="{filename}"',
        },
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
