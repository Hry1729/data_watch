from __future__ import annotations

import asyncio
from dataclasses import asdict
from typing import Literal

from fastapi import APIRouter, Depends, HTTPException, Query, Request
from pydantic import BaseModel, Field

from app.core.deps import get_current_admin, get_current_user
from app.core.config import data_root, load_config, mutate_config
from app.core.security import dpapi_decrypt, dpapi_encrypt
from app.core.role import build_role
from app.services.edge_local_store import EdgeLocalStore
from app.services.edge_sync import (
    CenterClient,
    CenterRequestError,
    EdgeIdentity,
    generate_identity,
    identity_public_key,
    identity_rotation_lock,
    recover_pending_rotation,
)
from app.services.process_manager import ProcessManagerError
from app.services.sidecar_manager import collector_lifecycle
from app.routers.admin import _uploader_env


router = APIRouter(prefix="/local", tags=["local"])
class LocalHistoryQuery(BaseModel):
    deviceId: int = Field(gt=0)
    varSeqs: list[int] = Field(default_factory=list, max_length=20)
    startTime: str
    endTime: str
    interval: str | None = None
    aggregate: str | None = None


class LocalHistoryMatrixQuery(LocalHistoryQuery):
    alignGranularity: Literal["100ms", "500ms", "1s"] = "1s"
    pageSize: int = Field(default=200, ge=1, le=500)
    cursorTs: str | None = None

class EdgeSettingsUpdate(BaseModel):
    max_offline_retention_days: int = Field(ge=1, le=365)
    outbox_maintenance_seconds: int = Field(ge=10, le=3600)


def _require_edge() -> None:
    if build_role() != "edge":
        raise HTTPException(status_code=404, detail="local edge API is not available")


def _store() -> EdgeLocalStore:
    return EdgeLocalStore(data_root() / "edge" / "edge.db")

def _process(request: Request, name: str) -> dict:
    pm = request.app.state.pm
    if name not in pm.modules:
        raise HTTPException(status_code=404, detail=f"module {name} is not installed")
    return asdict(pm.status(name))

def _settings_payload(edge: dict) -> dict:
    return {
        "node_uuid": str(edge.get("node_uuid") or ""),
        "node_name": str(edge.get("node_name") or ""),
        "center_url": str(edge.get("center_url") or ""),
        "registered": bool(edge.get("registered")),
        "registration_status": str(edge.get("registration_status") or "not_registered"),
        "last_sync_error": str(edge.get("last_sync_error") or ""),
        "applied_revision": int(edge.get("applied_revision") or 0),
        "max_offline_retention_days": int(edge.get("max_offline_retention_days") or 7),
        "outbox_maintenance_seconds": int(edge.get("outbox_maintenance_seconds") or 60),
    }


@router.get("/settings")
async def local_settings(_user: str = Depends(get_current_admin)):
    _require_edge()
    return _settings_payload(load_config().get("edge") or {})


@router.put("/settings")
async def update_local_settings(
    body: EdgeSettingsUpdate,
    _user: str = Depends(get_current_admin),
):
    _require_edge()

    def apply(config: dict) -> None:
        edge = config.setdefault("edge", {})
        edge["max_offline_retention_days"] = body.max_offline_retention_days
        edge["outbox_maintenance_seconds"] = body.outbox_maintenance_seconds

    config = mutate_config(apply)
    return _settings_payload(config.get("edge") or {})


@router.get("/devices")
async def local_devices(_user: str = Depends(get_current_user)):
    _require_edge()
    return _store().devices()



@router.get("/monitor/device-scope")
async def local_monitor_device_scope(_user: str = Depends(get_current_user)):
    _require_edge()
    return await asyncio.to_thread(_store().monitor_device_scope)


@router.get("/monitor/devices")
async def local_monitor_devices(
    page_no: int = Query(1, alias="pageNo", ge=1),
    page_size: int = Query(1000, alias="pageSize", ge=1, le=5000),
    keyword: str = Query(""),
    device_ids: list[int] | None = Query(None, alias="deviceIds"),
    device_type: str = Query("", alias="deviceType"),
    device_type_exact: str | None = Query(None, alias="deviceTypeExact"),
    device_code_exact: str | None = Query(None, alias="deviceCodeExact"),
    connect_status: str = Query("", alias="connectStatus"),
    device_status: str = Query("", alias="deviceStatus"),
    _user: str = Depends(get_current_user),
):
    _require_edge()
    return await asyncio.to_thread(
        _store().monitor_devices,
        page_no=page_no,
        page_size=page_size,
        keyword=keyword,
        device_ids=device_ids,
        device_type=device_type,
        device_type_exact=device_type_exact,
        device_code_exact=device_code_exact,
        connect_status=connect_status,
        device_status=device_status,
    )


@router.get("/monitor/points")
async def local_monitor_points(
    page_no: int = Query(1, alias="pageNo", ge=1),
    page_size: int = Query(1000, alias="pageSize", ge=1, le=5000),
    device_id: int | None = Query(None, alias="deviceId"),
    device_ids: list[int] | None = Query(None, alias="deviceIds"),
    device_code: str = Query("", alias="deviceCode"),
    keyword: str = Query(""),
    data_type: str = Query("", alias="dataType"),
    device_type: str = Query("", alias="deviceType"),
    connect_status: str = Query("", alias="connectStatus"),
    _user: str = Depends(get_current_user),
):
    _require_edge()
    return await asyncio.to_thread(
        _store().monitor_points,
        page_no=page_no,
        page_size=page_size,
        device_id=device_id,
        device_ids=device_ids,
        device_code=device_code,
        keyword=keyword,
        data_type=data_type,
        device_type=device_type,
        connect_status=connect_status,
    )


@router.get("/monitor/realtime/groups")
async def local_realtime_groups(
    device_code: str = Query(..., alias="deviceCode"),
    _user: str = Depends(get_current_user),
):
    _require_edge()
    return await asyncio.to_thread(_store().realtime_groups, device_code)


@router.get("/monitor/realtime")
async def local_realtime(
    device_id: int = Query(..., alias="deviceId", gt=0),
    group_code: str = Query("g001", alias="groupCode"),
    _user: str = Depends(get_current_user),
):
    _require_edge()
    return await asyncio.to_thread(_store().realtime, device_id, group_code)


@router.get("/monitor/realtime/latest")
async def local_realtime_latest(
    device_code: str = Query(..., alias="deviceCode"),
    group_code: str = Query("g001", alias="groupCode"),
    _user: str = Depends(get_current_user),
):
    _require_edge()
    return await asyncio.to_thread(
        _store().latest_values,
        device_code,
        group_code,
    )


@router.get("/monitor/history/devices")
async def local_history_devices(_user: str = Depends(get_current_user)):
    _require_edge()
    return await asyncio.to_thread(_store().history_devices)


@router.get("/monitor/history/device-types")
async def local_history_device_types(_user: str = Depends(get_current_user)):
    _require_edge()
    return await asyncio.to_thread(_store().history_device_types)


@router.get("/monitor/history/points")
async def local_history_points(
    device_id: int = Query(..., alias="deviceId", gt=0),
    _user: str = Depends(get_current_user),
):
    _require_edge()
    return await asyncio.to_thread(_store().history_points, device_id)


@router.post("/monitor/history/chart")
async def local_history_chart(
    body: LocalHistoryQuery,
    _user: str = Depends(get_current_user),
):
    _require_edge()
    try:
        return await asyncio.to_thread(_store().history_chart, body.model_dump())
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc


@router.post("/monitor/history/matrix")
async def local_history_matrix(
    body: LocalHistoryMatrixQuery,
    _user: str = Depends(get_current_user),
):
    _require_edge()
    try:
        return await asyncio.to_thread(_store().history_matrix, body.model_dump())
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc

@router.get("/collector")
async def local_collector(request: Request, _user: str = Depends(get_current_user)):
    _require_edge()
    return _process(request, "collector")


@router.get("/outbox")
async def local_outbox(_user: str = Depends(get_current_user)):
    _require_edge()
    return _store().outbox_status()


@router.get("/storage")
async def local_storage(request: Request, _user: str = Depends(get_current_user)):
    _require_edge()
    process = _process(request, "storage")
    try:
        process["storage"] = await asyncio.to_thread(request.app.state.edge_storage.status)
    except Exception as exc:
        process["storage"] = {"error": str(exc)}
    return process



@router.get("/uploader")
async def local_uploader(request: Request, _user: str = Depends(get_current_user)):
    _require_edge()
    process = _process(request, "uploader")
    process["outbox"] = _store().outbox_status()
    return process



@router.post("/identity/rotate")
async def rotate_identity(
    request: Request,
    _user: str = Depends(get_current_admin),
):
    _require_edge()
    # B016：与后台 pending 恢复串行化，避免并发轮换生成/提交多把 key。
    async with identity_rotation_lock:
        return await _rotate_identity(request)


async def _rotate_identity(request: Request):
    cfg = load_config()
    edge = cfg.get("edge") or {}
    node_uuid = str(edge.get("node_uuid") or "").strip()
    center_url = str(edge.get("center_url") or "").strip()
    private_key_pem = dpapi_decrypt(str(edge.get("private_key_enc") or ""))
    if not node_uuid or not center_url or not private_key_pem:
        raise HTTPException(status_code=409, detail="边缘节点尚未完成注册")

    current = EdgeIdentity(
        node_uuid=node_uuid,
        private_key_pem=private_key_pem,
        public_key_pem=identity_public_key(private_key_pem),
    )
    pending_private_key = dpapi_decrypt(
        str(edge.get("pending_private_key_enc") or "")
    )
    if pending_private_key:
        replacement = EdgeIdentity(
            node_uuid=node_uuid,
            private_key_pem=pending_private_key,
            public_key_pem=identity_public_key(pending_private_key),
        )
    else:
        generated = generate_identity()
        replacement = EdgeIdentity(
            node_uuid=node_uuid,
            private_key_pem=generated.private_key_pem,
            public_key_pem=generated.public_key_pem,
        )

        def save_pending(config: dict) -> None:
            config.setdefault("edge", {})["pending_private_key_enc"] = dpapi_encrypt(
                replacement.private_key_pem
            )

        mutate_config(save_pending)
    try:
        try:
            result = await CenterClient(center_url, current).rotate_key(
                replacement.public_key_pem
            )
        except CenterRequestError as exc:
            # B016：上次轮换中心已提交但响应丢失且旧 key grace 已过 → 旧 key 401；
            # 用 pending key 签名重放，中心返回 already_rotated 即可完成本地提升。
            if not pending_private_key or exc.status_code != 401:
                raise
            recovered = await recover_pending_rotation(
                center_url, node_uuid, pending_private_key
            )
            if recovered is None:
                raise exc
            result = recovered
    except Exception as exc:
        raise HTTPException(status_code=502, detail=f"中心密钥轮换失败: {exc}") from exc

    def apply(config: dict) -> None:
        target = config.setdefault("edge", {})
        target["private_key_enc"] = dpapi_encrypt(replacement.private_key_pem)
        target["key_version"] = int(result.get("keyVersion") or 1)
        target.pop("pending_private_key_enc", None)

    mutate_config(apply)
    restarted = False
    pm = request.app.state.pm
    # Edge 12：复用 admin 的统一 env 构造（含自定义 DATAWATCH_EDGE_STORAGE_URL）与同一生命周期锁。
    async with collector_lifecycle(pm):
        if "uploader" in pm.modules and pm.status("uploader").status == "running":
            try:
                await pm.restart("uploader", env=_uploader_env())
                restarted = True
            except ProcessManagerError as exc:
                return {
                    **result,
                    "uploaderRestarted": False,
                    "warning": str(exc),
                }
    return {**result, "uploaderRestarted": restarted}


@router.get("/logs")
async def local_logs(
    request: Request,
    module: Literal["collector", "uploader", "storage"] = Query(...),
    limit: int = Query(100, ge=1, le=500),
    _user: str = Depends(get_current_user),
):
    _require_edge()
    if module not in request.app.state.pm.modules:
        raise HTTPException(status_code=404, detail=f"module {module} is not installed")
    history = request.app.state.log_manager.get_history(module)
    return {"items": [item.model_dump() for item in history[-limit:]]}
