from __future__ import annotations

from typing import Any

from turboapi import APIRouter, Depends, Response
from turboapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.api.permissions import require_permissions
from app.api.routes.distributed_nodes import _publish_control_invalidation
from app.core.audit import audit_log
from app.core.exceptions import ApiError
from app.core.responses import success_response
from app.db.mysql import get_mysql_session_factory
from app.db.tdengine import get_tdengine_client
from app.services.catalog import CatalogService
from app.services.driver_lifecycle import DriverLifecycleService, is_uninstalling


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
    await require_permissions(credentials, "device_monitor:driver:view")
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
    await require_permissions(credentials, "device_monitor:driver:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.list_driver_options()
        # 卸载中的协议不得出现在设备绑定下拉
        data = [item for item in data if not is_uninstalling(item.get("driverName", ""))]
        return success_response(data=data)


@router.post("/api/device-monitor/driver/register")
@audit_log(
    module_name="driver",
    action_name="register",
    biz_type="driver",
    success_msg="驱动注册成功",
    failure_msg="驱动注册失败",
)
async def driver_register(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """Agent 安装成功后注册/更新 comm_protocol 目录。"""
    await require_permissions(credentials, "device_monitor:driver:manage")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DriverLifecycleService(session)
        data = await service.register_protocol(payload or {})
        return success_response(data=data)


@router.post("/api/device-monitor/driver/uninstall")
@audit_log(
    module_name="driver",
    action_name="uninstall",
    biz_type="driver",
    success_msg="驱动卸载成功",
    failure_msg="驱动卸载失败",
)
async def driver_uninstall(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    """卸载编排入口：绑定检查 → Agent DELETE → 删除目录行。

    body: ``{ protocol|driverName, driverId? }``；Agent 凭据取自服务端配置。
    """
    await require_permissions(credentials, "device_monitor:driver:manage")
    body = payload or {}
    protocol = str(body.get("protocol") or body.get("driverName") or "").strip()
    driver_id = str(body.get("driverId") or body.get("driver_id") or "").strip()
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DriverLifecycleService(session)
        data = await service.uninstall(
            protocol=protocol,
            driver_id=driver_id or None,
        )
        return success_response(data=data)


@router.get("/api/device-monitor/driver/uninstall-status")
async def driver_uninstall_status(
    protocol: str | None = None,
    driverName: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:driver:view")
    name = (protocol or driverName or "").strip()
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = DriverLifecycleService(session)
        data = await service.uninstall_status(name)
        return success_response(data=data)


@router.get("/api/device-monitor/device/group-tree")
async def device_group_tree(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:device:view")
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
    deviceTypeExact: str | None = None,
    deviceCodeExact: str | None = None,
    deviceIds: list[str] | str | None = None,
    deviceCode: str | None = None,
    deviceName: str | None = None,
    deviceStatus: str | None = None,
    connectStatus: str | None = None,
    includeSummary: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:device:view")
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
                    "deviceTypeExact": deviceTypeExact,
                    "deviceCodeExact": deviceCodeExact,
                    "deviceIds": deviceIds,
                    "deviceCode": deviceCode,
                    "deviceName": deviceName,
                    "deviceStatus": deviceStatus,
                    "connectStatus": connectStatus,
                    "includeSummary": includeSummary,
                }
            )
        )
        return success_response(data=data)


@router.post("/api/device-monitor/device/query")
async def query_devices(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:device:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        return success_response(data=await service.list_devices(_compact_params(payload)))


@router.get("/api/device-monitor/device/types")
async def device_types(
    deviceIds: list[str] | str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:device:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        return success_response(data=await service.list_device_types(_compact_params({"deviceIds": deviceIds})))


@router.get("/api/device-monitor/device/summary")
async def device_summary(
    keyword: str | None = None,
    deviceType: str | None = None,
    deviceCode: str | None = None,
    deviceName: str | None = None,
    deviceStatus: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:device:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device_summary(
            _compact_params({
                "keyword": keyword,
                "deviceType": deviceType,
                "deviceCode": deviceCode,
                "deviceName": deviceName,
                "deviceStatus": deviceStatus,
            })
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
    await require_permissions(credentials, "device_monitor:device:export")
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
    principal = await require_permissions(credentials, "device_monitor:device:import")

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.import_devices_rows(payload.get("rows"), actor=principal["user_name"])
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.get("/api/device-monitor/device/{device_id}")
async def device_detail(
    device_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:device:view")
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
    principal = await require_permissions(credentials, "device_monitor:device:create")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.create_device(payload, actor=principal["user_name"])
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.put("/api/device-monitor/device/{device_id}")
async def update_device(
    device_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:device:update")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.update_device(device_id, payload, actor=principal["user_name"])
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.patch("/api/device-monitor/device/{device_id}/status")
async def update_device_status(
    device_id: int,
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:device:update")
    if not isinstance(payload.get("enabled"), bool):
        raise ApiError(400, "enabled must be boolean")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.update_device_status(
            device_id,
            enabled=payload["enabled"],
            actor=principal["user_name"],
        )
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.delete("/api/device-monitor/device/batch")
async def batch_delete_devices(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:device:delete")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.batch_delete_devices(payload.get("ids"), actor=principal["user_name"])
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.delete("/api/device-monitor/device/{device_id}")
async def delete_device(
    device_id: int,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:device:delete")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.delete_device(device_id, actor=principal["user_name"])
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.get("/api/device-monitor/point/device-tree")
async def point_device_tree(
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:point:view")
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
    deviceIds: list[str] | str | None = None,
    deviceCode: str | None = None,
    deviceType: str | None = None,
    connectStatus: str | None = None,
    groupId: str | None = None,
    varSeq: str | None = None,
    pointName: str | None = None,
    ioAddress: str | None = None,
    dataType: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:point:view")
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
                    "deviceIds": deviceIds,
                    "deviceCode": deviceCode,
                    "deviceType": deviceType,
                    "connectStatus": connectStatus,
                    "groupId": groupId,
                    "varSeq": varSeq,
                    "pointName": pointName,
                    "ioAddress": ioAddress,
                    "dataType": dataType,
                }
            )
        )
        return success_response(data=data)


@router.post("/api/device-monitor/point/query")
async def query_points(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:point:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        return success_response(data=await service.list_points(_compact_params(payload)))


@router.get("/api/device-monitor/point/latest")
async def point_latest(
    pointIds: list[str] | str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    try:
        await require_permissions(credentials, "device_monitor:point:view")
    except ApiError as exc:
        raise ApiError(exc.code, exc.message, status_code=exc.code) from exc
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.get_point_latest_values(pointIds)
        return success_response(data=data)


@router.get("/api/device-monitor/point/export")
async def export_points(
    pageNo: str | None = None,
    pageSize: str | None = None,
    current: str | None = None,
    size: str | None = None,
    keyword: str | None = None,
    deviceId: str | None = None,
    deviceIds: list[str] | str | None = None,
    deviceCode: str | None = None,
    deviceType: str | None = None,
    connectStatus: str | None = None,
    groupId: str | None = None,
    varSeq: str | None = None,
    pointName: str | None = None,
    ioAddress: str | None = None,
    dataType: str | None = None,
    template: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:point:export")
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
                    # 与 /point/list 同一组筛选参数，导出范围不会大于列表。
                    "deviceIds": deviceIds,
                    "deviceCode": deviceCode,
                    # B058：导出也接收设备类型/连接状态筛选，与列表一致。
                    "deviceType": deviceType,
                    "connectStatus": connectStatus,
                    "groupId": groupId,
                    "varSeq": varSeq,
                    "pointName": pointName,
                    "ioAddress": ioAddress,
                    "dataType": dataType,
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
    principal = await require_permissions(credentials, "device_monitor:point:import")

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.import_points_rows(payload.get("rows"), actor=principal["user_name"])
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.post("/api/device-monitor/point/import/template")
async def import_points_from_template(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:point:import")

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.import_points_from_template(
            payload.get("deviceId"),
            actor=principal["user_name"],
        )
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.get("/api/device-monitor/point/{point_id}")
async def point_detail(
    point_id: int,
    deviceId: str | None = None,
    varSeq: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:point:view")
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
    principal = await require_permissions(credentials, "device_monitor:point:create")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.create_point(payload, actor=principal["user_name"])
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.put("/api/device-monitor/point/{point_id}")
async def update_point(
    point_id: int,
    payload: dict[str, Any],
    locatorDeviceId: str | None = None,
    locatorVarSeq: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:point:update")
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
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.patch("/api/device-monitor/point/{point_id}/status")
async def update_point_status(
    point_id: int,
    payload: dict[str, Any],
    locatorDeviceId: str | None = None,
    locatorVarSeq: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:point:update")
    if not isinstance(payload.get("enabled"), bool):
        raise ApiError(400, "enabled must be boolean")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.update_point_status(
            point_id,
            enabled=payload["enabled"],
            actor=principal["user_name"],
            locator_device_id=locatorDeviceId,
            locator_var_seq=locatorVarSeq,
        )
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.delete("/api/device-monitor/point/batch")
async def batch_delete_points(
    payload: dict[str, Any],
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:point:delete")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.batch_delete_points(payload.get("ids"), actor=principal["user_name"])
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.delete("/api/device-monitor/point/{point_id}")
async def delete_point(
    point_id: int,
    deviceId: str | None = None,
    varSeq: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    principal = await require_permissions(credentials, "device_monitor:point:delete")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.delete_point(
            point_id,
            actor=principal["user_name"],
            locator_device_id=deviceId,
            locator_var_seq=varSeq,
        )
        await _invalidate_ingest_control_caches()
        return success_response(data=data)


@router.get("/api/device-monitor/device/{device_code}/point-groups")
async def device_point_groups(
    device_code: str,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:device:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device_point_groups(device_code)
        return success_response(data=data)


@router.get("/api/device-monitor/device/{device_code}/variables")
async def device_variables_by_group(
    device_code: str,
    group_code: str | None = None,
    credentials: HTTPAuthorizationCredentials | None = Depends(auth_scheme),
):
    await require_permissions(credentials, "device_monitor:device:view")
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = CatalogService(session=session, tdengine=get_tdengine_client())
        data = await service.get_device_variables_by_group(device_code, group_code or "g001")
        return success_response(data=data)


async def _invalidate_ingest_control_caches() -> None:
    """控制面成功提交后统一发布 Ingest 控制缓存失效。

    点位/设备目录变更同时改变 `device_point_group.config_version`（Ingest 存在
    assignment 缓存里）与 `point_storage_mapping`（mapping 缓存），两者同一事务提交，
    因此两个 scope 一起发布。必须在服务层返回（事务已提交）之后调用，不可先通知后提交。
    """
    await _publish_control_invalidation({"scope": "assignments"})
    await _publish_control_invalidation({"scope": "mappings"})


def _compact_params(params: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in params.items() if value is not None}


def _parse_bool_param(value: str | None) -> bool:
    return str(value or "").strip().lower() in {"1", "true", "yes", "on"}
