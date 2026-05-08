from __future__ import annotations

from typing import Any

from turboapi import APIRouter, Depends, Response
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.tdengine import get_tdengine_client
from app.services.auth import AuthService
from app.services.catalog import CatalogService


router = APIRouter(tags=["catalog"])
auth_scheme = HTTPBearer(auto_error=False)


@router.get("/api/device-monitor/driver/list")
async def driver_list(
    pageNo: str | None = None,
    pageSize: str | None = None,
    current: str | None = None,
    size: str | None = None,
    keyword: str | None = None,
    driverName: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.list_drivers(
            _compact_params(
                {
                    "pageNo": pageNo,
                    "pageSize": pageSize,
                    "current": current,
                    "size": size,
                    "keyword": keyword or driverName,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/device-monitor/driver/options")
async def driver_options(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.list_driver_options()
        return success_response(data=data)


@router.get("/api/device-monitor/device/group-tree")
async def device_group_tree(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device_group_tree()
        return success_response(data=data)


@router.get("/api/device-monitor/device/list")
async def device_list(
    pageNo: str | None = None,
    pageSize: str | None = None,
    current: str | None = None,
    size: str | None = None,
    keyword: str | None = None,
    deviceType: str | None = None,
    deviceCode: str | None = None,
    deviceName: str | None = None,
    deviceStatus: str | None = None,
    connectStatus: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.list_devices(
            _compact_params(
                {
                    "pageNo": pageNo,
                    "pageSize": pageSize,
                    "current": current,
                    "size": size,
                    "keyword": keyword,
                    "deviceType": deviceType,
                    "deviceCode": deviceCode,
                    "deviceName": deviceName,
                    "deviceStatus": deviceStatus,
                    "connectStatus": connectStatus,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/device-monitor/device/export")
async def export_devices(
    pageNo: str | None = None,
    pageSize: str | None = None,
    current: str | None = None,
    size: str | None = None,
    keyword: str | None = None,
    deviceType: str | None = None,
    deviceCode: str | None = None,
    deviceName: str | None = None,
    deviceStatus: str | None = None,
    connectStatus: str | None = None,
    template: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    template_flag = _parse_bool_param(template)
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        content = await service.export_devices_workbook(
            _compact_params(
                {
                    "pageNo": pageNo,
                    "pageSize": pageSize,
                    "current": current,
                    "size": size,
                    "keyword": keyword,
                    "deviceType": deviceType,
                    "deviceCode": deviceCode,
                    "deviceName": deviceName,
                    "deviceStatus": deviceStatus,
                    "connectStatus": connectStatus,
                }
            ),
            template=template_flag,
        )

    filename = "device-import-template.xlsx" if template_flag else "device-export.xlsx"
    return Response(
        content=content,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={
            "Content-Disposition": f'attachment; filename="{filename}"',
        },
    )


@router.post("/api/device-monitor/device/import")
async def import_devices(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.import_devices_rows(payload.get("rows"), actor=principal["user_name"])
        return success_response(data=data)


@router.get("/api/device-monitor/device/{device_id}")
async def device_detail(
    device_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device(device_id)
        return success_response(data=data)


@router.post("/api/device-monitor/device")
async def create_device(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.create_device(payload, actor=principal["user_name"])
        return success_response(data=data)


@router.put("/api/device-monitor/device/{device_id}")
async def update_device(
    device_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.update_device(device_id, payload, actor=principal["user_name"])
        return success_response(data=data)


@router.delete("/api/device-monitor/device/batch")
async def batch_delete_devices(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.batch_delete_devices(payload.get("ids"), actor=principal["user_name"])
        return success_response(data=data)


@router.delete("/api/device-monitor/device/{device_id}")
async def delete_device(
    device_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.delete_device(device_id, actor=principal["user_name"])
        return success_response(data=data)


@router.get("/api/device-monitor/point/device-tree")
async def point_device_tree(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.get_point_device_tree()
        return success_response(data=data)


@router.get("/api/device-monitor/point/list")
async def point_list(
    pageNo: str | None = None,
    pageSize: str | None = None,
    current: str | None = None,
    size: str | None = None,
    keyword: str | None = None,
    deviceId: str | None = None,
    deviceCode: str | None = None,
    groupId: str | None = None,
    varSeq: str | None = None,
    pointName: str | None = None,
    ioAddress: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.list_points(
            _compact_params(
                {
                    "pageNo": pageNo,
                    "pageSize": pageSize,
                    "current": current,
                    "size": size,
                    "keyword": keyword,
                    "deviceId": deviceId,
                    "deviceCode": deviceCode,
                    "groupId": groupId,
                    "varSeq": varSeq,
                    "pointName": pointName,
                    "ioAddress": ioAddress,
                }
            )
        )
        return success_response(data=data)


@router.get("/api/device-monitor/point/export")
async def export_points(
    pageNo: str | None = None,
    pageSize: str | None = None,
    current: str | None = None,
    size: str | None = None,
    keyword: str | None = None,
    deviceId: str | None = None,
    deviceCode: str | None = None,
    varSeq: str | None = None,
    pointName: str | None = None,
    ioAddress: str | None = None,
    template: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    template_flag = _parse_bool_param(template)
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        content = await service.export_points_workbook(
            _compact_params(
                {
                    "pageNo": pageNo,
                    "pageSize": pageSize,
                    "current": current,
                    "size": size,
                    "keyword": keyword,
                    "deviceId": deviceId,
                    "deviceCode": deviceCode,
                    "varSeq": varSeq,
                    "pointName": pointName,
                    "ioAddress": ioAddress,
                }
            ),
            template=template_flag,
        )

    filename = "point-import-template.xlsx" if template_flag else "point-export.xlsx"
    return Response(
        content=content,
        media_type="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        headers={
            "Content-Disposition": f'attachment; filename="{filename}"',
        },
    )


@router.post("/api/device-monitor/point/import")
async def import_points(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.import_points_rows(payload.get("rows"), actor=principal["user_name"])
        return success_response(data=data)


@router.post("/api/device-monitor/point/import/template")
async def import_points_from_template(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.import_points_from_template(
            payload.get("deviceId"),
            actor=principal["user_name"],
        )
        return success_response(data=data)


@router.get("/api/device-monitor/point/{point_id}")
async def point_detail(
    point_id: int,
    deviceId: str | None = None,
    varSeq: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.get_point(
            point_id,
            locator_device_id=deviceId,
            locator_var_seq=varSeq,
        )
        return success_response(data=data)


@router.post("/api/device-monitor/point")
async def create_point(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.create_point(payload, actor=principal["user_name"])
        return success_response(data=data)


@router.put("/api/device-monitor/point/{point_id}")
async def update_point(
    point_id: int,
    payload: dict[str, Any],
    locatorDeviceId: str | None = None,
    locatorVarSeq: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.update_point(
            point_id,
            payload,
            actor=principal["user_name"],
            locator_device_id=locatorDeviceId,
            locator_var_seq=locatorVarSeq,
        )
        return success_response(data=data)


@router.delete("/api/device-monitor/point/batch")
async def batch_delete_points(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.batch_delete_points(payload.get("ids"), actor=principal["user_name"])
        return success_response(data=data)


@router.delete("/api/device-monitor/point/{point_id}")
async def delete_point(
    point_id: int,
    deviceId: str | None = None,
    varSeq: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await _require_roles(credentials, {"R_SUPER", "R_ADMIN"})
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.delete_point(
            point_id,
            actor=principal["user_name"],
            locator_device_id=deviceId,
            locator_var_seq=varSeq,
        )
        return success_response(data=data)


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


def _compact_params(params: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in params.items() if value is not None}


def _parse_bool_param(value: str | None) -> bool:
    return str(value or "").strip().lower() in {"1", "true", "yes", "on"}
