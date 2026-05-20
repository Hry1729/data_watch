from __future__ import annotations

import asyncio
from collections import OrderedDict
from dataclasses import dataclass
from datetime import datetime
import re
from typing import Any

from sqlalchemy import bindparam, text
from sqlalchemy.exc import IntegrityError
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError
from app.core.simple_xlsx import WorkbookSheet, build_workbook_bytes, read_workbook_bytes
from app.core.time_utils import now_local
from app.db.tdengine import TDengineClient


ONLINE_MAX_AGE_SECONDS = 120
UNSTABLE_MAX_AGE_SECONDS = 900
DEVICE_PREFIX_RE = re.compile(r"^[A-Za-z]+")
IDENTIFIER_SANITIZE_RE = re.compile(r"[^A-Za-z0-9_]+")
PROTOCOL_KEY_SANITIZE_RE = re.compile(r"[^A-Za-z0-9]+")
IMPORT_KEY_SANITIZE_RE = re.compile(r"[\s_\-()（）]+")

DEVICE_IMPORT_HEADERS = ["设备类型", "设备编号", "设备名称", "关联驱动", "设备状态", "描述"]
POINT_IMPORT_HEADERS = ["设备编号", "点位序号", "点位名称", "IO地址", "数据类型", "驱动名称", "是否只读", "描述", "采集周期(ms)", "是否存储"]
DEVICE_REQUIRED_HEADERS = {"设备类型", "设备编号", "设备名称", "关联驱动"}
POINT_REQUIRED_HEADERS = {"设备编号", "点位序号", "点位名称", "IO地址", "数据类型", "驱动名称"}
DEVICE_DATA_SHEET_NAME = "设备数据"
POINT_DATA_SHEET_NAME = "点位数据"
DEVICE_IMPORT_FIELD_ALIASES = {
    "设备类型": ("设备类型", "deviceType", "device_type"),
    "设备编号": ("设备编号", "deviceCode", "device_code"),
    "设备名称": ("设备名称", "deviceName", "device_name"),
    "关联驱动": ("关联驱动", "driverName", "driver_name"),
    "设备状态": ("设备状态", "deviceStatus", "device_status"),
    "描述": ("描述", "description"),
}
POINT_IMPORT_FIELD_ALIASES = {
    "设备编号": ("设备编号", "deviceCode", "device_code"),
    "点位序号": ("点位序号", "varSeq", "var_seq"),
    "点位名称": ("点位名称", "pointName", "point_name"),
    "IO地址": ("IO地址", "ioAddress", "io_address"),
    "数据类型": ("数据类型", "dataType", "data_type"),
    "驱动名称": ("驱动名称", "driverName", "driver_name"),
    "是否只读": ("是否只读", "isReadonly", "is_readonly"),
    "描述": ("描述", "description"),
    "采集周期(ms)": ("采集周期(ms)", "scanRate", "scan_rate"),
    "是否存储": ("是否存储", "isStorage", "is_storage"),
}


@dataclass(frozen=True)
class DeviceCatalogItem:
    device_id: int
    device_code: str
    device_name: str
    device_type: str
    device_type_code: str
    group_name: str
    group_code: str
    driver_name: str
    enable: int
    device_status_raw: int
    endpoint: str
    endpoint_bkp: str
    create_time: datetime | None
    update_time: datetime | None


@dataclass(frozen=True)
class PointCatalogItem:
    point_id: int
    device_id: int
    device_code: str
    device_name: str
    point_name: str
    io_address: str
    data_type: str
    description: str
    driver_name: str
    create_time: datetime | None
    update_time: datetime | None
    var_seq: int | None
    scan_rate: int | None
    is_alarm: bool
    is_storage: bool
    is_readonly: bool
    group_id: int | None


class CatalogService:
    def __init__(self, session: AsyncSession, tdengine: TDengineClient):
        self.session = session
        self.tdengine = tdengine

    async def list_drivers(self, params: dict[str, Any]) -> dict[str, Any]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        cp.id AS id,
                        cp.protocol AS driver_name,
                        COALESCE(cp.description, cp.protocol) AS driver_desc,
                        cp.create_time AS create_time,
                        COUNT(d.id) AS bound_device_count
                    FROM comm_protocol AS cp
                    LEFT JOIN device AS d ON d.protocol_id = cp.id
                    GROUP BY cp.id, cp.protocol, cp.description, cp.create_time
                    ORDER BY cp.id ASC
                    """
                )
            )
        ).mappings().all()

        keyword = self._normalize_text(params.get("keyword"))
        items = []
        available_count = 0
        for row in rows:
            item = {
                "id": int(row["id"]),
                "driverName": row["driver_name"] or "",
                "driverDesc": row["driver_desc"] or "",
                "readWriteType": self._resolve_read_write_type(row["driver_name"]),
                "status": "available" if int(row["bound_device_count"] or 0) > 0 else "unused",
                "boundDeviceCount": int(row["bound_device_count"] or 0),
                "updateTime": self._format_datetime(self._coerce_datetime(row["create_time"])),
            }
            if keyword and not self._matches_keyword(
                keyword,
                item["driverName"],
                item["driverDesc"],
            ):
                continue
            if item["status"] == "available":
                available_count += 1
            items.append(item)

        result = self._paginate(items, params)
        result["availableCount"] = available_count
        return result

    async def list_driver_options(self) -> list[dict[str, str]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        cp.protocol AS driver_name,
                        COALESCE(cp.description, cp.protocol) AS driver_desc
                    FROM comm_protocol AS cp
                    ORDER BY cp.id ASC
                    """
                )
            )
        ).mappings().all()

        return [
            {
                "driverName": row["driver_name"] or "",
                "driverDesc": row["driver_desc"] or row["driver_name"] or "",
            }
            for row in rows
        ]

    async def get_device_group_tree(self) -> list[dict[str, Any]]:
        rows = await self._fetch_device_group_rows()
        if not rows:
            return []

        groups: OrderedDict[int, dict[str, Any]] = OrderedDict()
        for row in rows:
            group_id = int(row["group_id"])
            group_name = row["group_name"] or row["group_code"] or f"Group {group_id}"
            group_node = groups.setdefault(
                group_id,
                {
                    "key": f"group-{group_id}",
                    "name": group_name,
                    "count": 0,
                    "nodeType": "group",
                    "children": [],
                },
            )

            if row["device_id"] is None:
                continue

            device = self._to_device_item(row)
            group_node["children"].append(
                {
                    "key": f"device-{device.device_id}",
                    "name": device.device_name,
                    "nodeType": "device",
                    "deviceId": device.device_id,
                    "deviceCode": device.device_code,
                    "deviceName": device.device_name,
                    "connectStatus": self._build_connect_status_from_raw(device.device_status_raw),
                }
            )
            group_node["count"] += 1

        return list(groups.values())

    async def list_devices(self, params: dict[str, Any]) -> dict[str, Any]:
        rows = await self._fetch_device_rows()
        devices = [self._to_device_item(row) for row in rows]
        filtered_devices = [device for device in devices if self._matches_basic_device_filters(device, params)]

        # 获取所有过滤后设备的最新采集时间，用于统计在线/离线数量
        all_latest_map = await self._fetch_latest_collect_times(filtered_devices)
        all_items = [
            self._serialize_device_item(device, all_latest_map.get(device.device_code))
            for device in filtered_devices
        ]

        # 统计在线和离线数量
        online_count = sum(1 for item in all_items if str(item.get("connectStatus", "")).lower() == "online")
        offline_count = len(all_items) - online_count

        connect_status = self._normalize_text(params.get("connectStatus"))
        if connect_status:
            filtered_items = [item for item in all_items if str(item["connectStatus"]).lower() == connect_status]
            result = self._paginate(filtered_items, params)
        else:
            page_devices, page_no, page_size = self._paginate_records(filtered_devices, params)
            page_items = [
                self._serialize_device_item(device, all_latest_map.get(device.device_code))
                for device in page_devices
            ]
            result = self._build_page_payload(page_items, len(filtered_devices), page_no, page_size)

        result["onlineCount"] = online_count
        result["offlineCount"] = offline_count
        return result

    async def get_device(self, device_id: int) -> dict[str, Any]:
        if device_id <= 0:
            raise ApiError(400, "deviceId must be a positive integer")

        row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(dt.type_name, dt.type_code, '') AS device_type,
                        COALESCE(dt.type_code, '') AS device_type_code,
                        COALESCE(dg.group_name, '') AS group_name,
                        COALESCE(dg.group_code, '') AS group_code,
                        COALESCE(cp.protocol, '') AS driver_name,
                        COALESCE(d.enable, 0) AS enable,
                        COALESCE(d.status, 0) AS device_status_raw,
                        COALESCE(d.endpoint, '') AS endpoint,
                        COALESCE(d.endpoint_bkp, '') AS endpoint_bkp,
                        d.create_time AS create_time,
                        d.update_time AS update_time
                    FROM device AS d
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    LEFT JOIN device_group AS dg ON dg.id = d.group_id
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    WHERE d.id = :device_id
                    LIMIT 1
                    """
                ),
                {"device_id": device_id},
            )
        ).mappings().first()

        if row is None:
            raise ApiError(404, "Device not found")

        device = self._to_device_item(row)
        latest_map = await self._fetch_latest_collect_times([device])
        return self._serialize_device_item(device, latest_map.get(device.device_code))

    async def export_devices_workbook(self, params: dict[str, Any], *, template: bool) -> bytes:
        rows = [self._build_device_instruction_row(header, detail) for header, detail in self._device_import_notes()]
        sheets = [WorkbookSheet(name="说明", rows=rows)]

        data_rows: list[list[Any]] = [DEVICE_IMPORT_HEADERS.copy()]
        if not template:
            devices = await self._fetch_export_devices(params)
            data_rows.extend(
                [
                    [
                        item["deviceType"],
                        item["deviceCode"],
                        item["deviceName"],
                        item["driverName"],
                        "启用" if item["deviceStatus"] == "enabled" else "停用",
                        item["description"],
                    ]
                    for item in devices
                ]
            )

        sheets.append(WorkbookSheet(name=DEVICE_DATA_SHEET_NAME, rows=data_rows))
        return build_workbook_bytes(sheets)

    async def import_devices_workbook(self, content: bytes, *, actor: str) -> dict[str, Any]:
        workbook = read_workbook_bytes(content)
        rows = self._extract_import_rows(workbook, expected_sheet_name=DEVICE_DATA_SHEET_NAME)
        records = self._map_import_rows(
            rows,
            expected_headers=DEVICE_IMPORT_HEADERS,
            required_headers=DEVICE_REQUIRED_HEADERS,
        )
        return await self._import_device_records(records, actor=actor)

    async def import_devices_rows(self, rows: Any, *, actor: str) -> dict[str, Any]:
        records = self._normalize_import_payload_rows(
            rows,
            expected_headers=DEVICE_IMPORT_HEADERS,
            required_headers=DEVICE_REQUIRED_HEADERS,
            field_aliases=DEVICE_IMPORT_FIELD_ALIASES,
        )
        return await self._import_device_records(records, actor=actor)

    async def _import_device_records(
        self,
        records: list[dict[str, Any]],
        *,
        actor: str,
    ) -> dict[str, Any]:
        if not records:
            raise ApiError(400, "导入数据为空")

        success = 0
        fail = 0
        errors: list[dict[str, Any]] = []
        seen_device_codes: set[str] = set()

        for record in records:
            try:
                payload = self._build_device_import_payload(record["values"], seen_device_codes)
                await self.create_device(payload, actor=actor)
                success += 1
            except ApiError as exc:
                fail += 1
                errors.append(
                    {
                        "rowNumber": record["row_number"],
                        "reason": str(exc),
                    }
                )

        result: dict[str, Any] = {"success": success, "fail": fail}
        if errors:
            result["errors"] = errors
        return result

    async def create_device(self, payload: dict[str, Any], *, actor: str) -> dict[str, int]:
        device_type = self._require_text(payload.get("deviceType"), field_name="deviceType", max_length=20)
        device_code = self._require_text(payload.get("deviceCode"), field_name="deviceCode", max_length=20)
        device_name = self._require_text(payload.get("deviceName"), field_name="deviceName", max_length=20)
        driver_name = self._require_text(payload.get("driverName"), field_name="driverName", max_length=50)
        device_status = self._normalize_device_status(payload.get("deviceStatus"))
        self._optional_text(payload.get("description"), max_length=255)

        existing = (
            await self.session.execute(
                text("SELECT id FROM device WHERE device_code = :device_code LIMIT 1"),
                {"device_code": device_code},
            )
        ).scalar_one_or_none()
        if existing is not None:
            raise ApiError(409, "deviceCode already exists")

        type_row = await self._resolve_device_type_row(device_type)
        protocol_row = await self._resolve_protocol_row(driver_name)
        reference_row = await self._fetch_device_reference_row(
            type_id=int(type_row["id"]),
            protocol_id=int(protocol_row["id"]),
        )
        group_id = await self._resolve_group_id(type_row=type_row, reference_row=reference_row)

        params = {
            "device_code": device_code,
            "device_name": device_name,
            "type_id": int(type_row["id"]),
            "group_id": group_id,
            "enable": 1 if device_status == "enabled" else 0,
            "status": 0,
            "protocol_id": int(protocol_row["id"]),
            "endpoint": self._build_placeholder_endpoint(device_code),
            "endpoint_bkp": None,
            "rack": int(reference_row["rack"]) if reference_row is not None else 0,
            "slot": int(reference_row["slot"]) if reference_row is not None else 1,
            "scan_cycle": int(reference_row["scan_cycle"]) if reference_row is not None else 1000,
            "actor": self._truncate_text(actor, 20),
        }

        try:
            result = await self.session.execute(
                text(
                    """
                    INSERT INTO device (
                        device_code,
                        device_name,
                        type_id,
                        group_id,
                        enable,
                        status,
                        protocol_id,
                        endpoint,
                        endpoint_bkp,
                        rack,
                        slot,
                        scan_cycle,
                        create_user,
                        create_time,
                        update_user,
                        update_time
                    ) VALUES (
                        :device_code,
                        :device_name,
                        :type_id,
                        :group_id,
                        :enable,
                        :status,
                        :protocol_id,
                        :endpoint,
                        :endpoint_bkp,
                        :rack,
                        :slot,
                        :scan_cycle,
                        :actor,
                        NOW(),
                        :actor,
                        NOW()
                    )
                    """
                ),
                params,
            )
            await self.session.commit()
        except IntegrityError as exc:
            await self.session.rollback()
            raise ApiError(409, "新增设备失败，设备编号可能已存在") from exc

        return {"id": int(result.lastrowid or 0)}

    async def update_device(self, device_id: int, payload: dict[str, Any], *, actor: str) -> dict[str, int]:
        if device_id <= 0:
            raise ApiError(400, "deviceId must be a positive integer")

        existing_row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id,
                        d.device_code,
                        COALESCE(d.endpoint, '') AS endpoint,
                        COALESCE(d.endpoint_bkp, '') AS endpoint_bkp,
                        d.rack,
                        d.slot,
                        d.scan_cycle,
                        d.status
                    FROM device AS d
                    WHERE d.id = :device_id
                    LIMIT 1
                    """
                ),
                {"device_id": device_id},
            )
        ).mappings().first()
        if existing_row is None:
            raise ApiError(404, "Device not found")

        device_type = self._require_text(payload.get("deviceType"), field_name="deviceType", max_length=20)
        device_code = self._require_text(payload.get("deviceCode"), field_name="deviceCode", max_length=20)
        device_name = self._require_text(payload.get("deviceName"), field_name="deviceName", max_length=20)
        driver_name = self._require_text(payload.get("driverName"), field_name="driverName", max_length=50)
        device_status = self._normalize_device_status(payload.get("deviceStatus"))
        self._optional_text(payload.get("description"), max_length=255)

        if device_code != str(existing_row["device_code"] or ""):
            raise ApiError(400, "deviceCode cannot be changed after creation")

        duplicate = (
            await self.session.execute(
                text(
                    """
                    SELECT id
                    FROM device
                    WHERE device_code = :device_code
                      AND id <> :device_id
                    LIMIT 1
                    """
                ),
                {"device_code": device_code, "device_id": device_id},
            )
        ).scalar_one_or_none()
        if duplicate is not None:
            raise ApiError(409, "deviceCode already exists")

        type_row = await self._resolve_device_type_row(device_type)
        protocol_row = await self._resolve_protocol_row(driver_name)
        reference_row = await self._fetch_device_reference_row(
            type_id=int(type_row["id"]),
            protocol_id=int(protocol_row["id"]),
        )
        group_id = await self._resolve_group_id(type_row=type_row, reference_row=reference_row)

        old_device_code = str(existing_row["device_code"] or "")
        endpoint = str(existing_row["endpoint"] or "")
        if not endpoint or endpoint == self._build_placeholder_endpoint(old_device_code):
            endpoint = self._build_placeholder_endpoint(device_code)

        params = {
            "device_id": device_id,
            "device_code": device_code,
            "device_name": device_name,
            "type_id": int(type_row["id"]),
            "group_id": group_id,
            "enable": 1 if device_status == "enabled" else 0,
            "protocol_id": int(protocol_row["id"]),
            "endpoint": endpoint,
            "rack": int(existing_row["rack"])
            if existing_row["rack"] is not None
            else int(reference_row["rack"]) if reference_row is not None else 0,
            "slot": int(existing_row["slot"])
            if existing_row["slot"] is not None
            else int(reference_row["slot"]) if reference_row is not None else 1,
            "scan_cycle": int(existing_row["scan_cycle"])
            if existing_row["scan_cycle"] is not None
            else int(reference_row["scan_cycle"]) if reference_row is not None else 1000,
            "actor": self._truncate_text(actor, 20),
        }

        try:
            await self.session.execute(
                text(
                    """
                    UPDATE device
                    SET
                        device_code = :device_code,
                        device_name = :device_name,
                        type_id = :type_id,
                        group_id = :group_id,
                        enable = :enable,
                        protocol_id = :protocol_id,
                        endpoint = :endpoint,
                        rack = :rack,
                        slot = :slot,
                        scan_cycle = :scan_cycle,
                        update_user = :actor,
                        update_time = NOW()
                    WHERE id = :device_id
                    """
                ),
                params,
            )
            await self.session.commit()
        except IntegrityError as exc:
            await self.session.rollback()
            raise ApiError(409, "编辑设备失败，设备编号可能已存在") from exc

        return {"id": device_id}

    async def delete_device(self, device_id: int, *, actor: str) -> dict[str, int]:
        del actor

        if device_id <= 0:
            raise ApiError(400, "deviceId must be a positive integer")

        existing_id = (
            await self.session.execute(
                text("SELECT id FROM device WHERE id = :device_id LIMIT 1"),
                {"device_id": device_id},
            )
        ).scalar_one_or_none()
        if existing_id is None:
            raise ApiError(404, "Device not found")

        try:
            await self.session.execute(
                text("DELETE FROM device_variable WHERE device_id = :device_id"),
                {"device_id": device_id},
            )
            await self.session.execute(
                text("DELETE FROM device WHERE id = :device_id"),
                {"device_id": device_id},
            )
            await self.session.commit()
        except IntegrityError as exc:
            await self.session.rollback()
            raise ApiError(409, "删除设备失败，请先清理关联数据") from exc

        return {"id": device_id}

    async def batch_delete_devices(self, ids: list[Any], *, actor: str) -> dict[str, int]:
        del actor

        normalized_ids = self._normalize_id_list(ids, field_name="ids")
        select_stmt = text("SELECT id FROM device WHERE id IN :ids").bindparams(bindparam("ids", expanding=True))
        existing_ids = [
            int(row[0])
            for row in (await self.session.execute(select_stmt, {"ids": normalized_ids})).all()
        ]
        if not existing_ids:
            raise ApiError(404, "No devices found")

        delete_points_stmt = text("DELETE FROM device_variable WHERE device_id IN :ids").bindparams(
            bindparam("ids", expanding=True)
        )
        delete_devices_stmt = text("DELETE FROM device WHERE id IN :ids").bindparams(bindparam("ids", expanding=True))

        try:
            await self.session.execute(delete_points_stmt, {"ids": existing_ids})
            await self.session.execute(delete_devices_stmt, {"ids": existing_ids})
            await self.session.commit()
        except IntegrityError as exc:
            await self.session.rollback()
            raise ApiError(409, "批量删除设备失败，请先清理关联数据") from exc

        return {"count": len(existing_ids)}

    async def get_point_device_tree(self) -> list[dict[str, Any]]:
        rows = await self._fetch_device_group_rows()
        if not rows:
            return []

        groups: OrderedDict[int, dict[str, Any]] = OrderedDict()
        for row in rows:
            group_id = int(row["group_id"])
            group_name = row["group_name"] or row["group_code"] or f"Group {group_id}"
            group_node = groups.setdefault(
                group_id,
                {
                    "key": f"group-{group_id}",
                    "name": group_name,
                    "count": 0,
                    "nodeType": "group",
                    "groupId": group_id,
                    "children": [],
                },
            )

            if row["device_id"] is None:
                continue

            group_node["children"].append(
                {
                    "key": f"device-{int(row['device_id'])}",
                    "name": row["device_name"] or row["device_code"] or "",
                    "nodeType": "device",
                    "deviceId": int(row["device_id"]),
                    "deviceCode": row["device_code"] or "",
                }
            )
            group_node["count"] += 1

        return list(groups.values())

    async def list_points(self, params: dict[str, Any]) -> dict[str, Any]:
        rows = await self._fetch_point_rows()
        points = [self._to_point_item(row) for row in rows]
        points = self._filter_point_items(points, params)

        # 先分页，只对当前页点位查询 TDengine 最新值
        page_no = self._to_positive_int(params.get("pageNo") or params.get("current"), default=1)
        page_size = self._to_positive_int(params.get("pageSize") or params.get("size"), default=20)
        total = len(points)
        start = (page_no - 1) * page_size
        end = start + page_size
        page_points = points[start:end]

        # 只查询当前页点位的最新值
        latest_value_map = await self._fetch_latest_point_value_map(page_points)

        items = [
            self._serialize_point_item(point, latest_value_map.get(point.point_id))
            for point in page_points
        ]
        result = self._build_page_payload(items, total, page_no, page_size)
        # recentUpdateCount 保留但不统计（因为只查询了当前页）
        result["recentUpdateCount"] = 0
        return result

    async def get_point(
        self,
        point_id: int,
        *,
        locator_device_id: Any = None,
        locator_var_seq: Any = None,
    ) -> dict[str, Any]:
        locator = await self._resolve_point_locator(
            point_id=point_id,
            device_id=locator_device_id,
            var_seq=locator_var_seq,
        )

        row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dv.id AS point_id,
                        dv.device_id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(dv.var_name, CAST(dv.var_seq AS CHAR), '') AS point_name,
                        COALESCE(dv.var_address, '') AS io_address,
                        UPPER(COALESCE(dv.var_type, 'STRING')) AS data_type,
                        COALESCE(dv.description, '') AS description,
                        COALESCE(cp.protocol, '') AS driver_name,
                        dv.create_time AS create_time,
                        dv.update_time AS update_time,
                        dv.var_seq AS var_seq,
                        dv.scan_rate AS scan_rate,
                        COALESCE(dv.is_alarm, 0) AS is_alarm,
                        COALESCE(dv.is_storage, 0) AS is_storage,
                        CASE
                            WHEN UPPER(COALESCE(dv.io_type, '')) IN ('OUTPUT', 'WRITE', 'RW')
                                THEN 0
                            ELSE 1
                        END AS is_readonly
                    FROM device_variable AS dv
                    JOIN device AS d ON d.id = dv.device_id
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    WHERE dv.id = :point_id
                      AND COALESCE(dv.deleted, 0) = 0
                    LIMIT 1
                    """
                ),
                {"point_id": locator["point_id"]},
            )
        ).mappings().first()

        if row is None:
            raise ApiError(404, "Point not found")

        point = self._to_point_item(row)
        latest_value_map = await self._fetch_latest_point_value_map([point])
        return self._serialize_point_item(point, latest_value_map.get(point.point_id))

    async def export_points_workbook(self, params: dict[str, Any], *, template: bool) -> bytes:
        rows = [self._build_device_instruction_row(header, detail) for header, detail in self._point_import_notes()]
        sheets = [WorkbookSheet(name="说明", rows=rows)]

        data_rows: list[list[Any]] = [POINT_IMPORT_HEADERS.copy()]
        if not template:
            point_items = await self._fetch_export_points(params)
            data_rows.extend(
                [
                    [
                        item.device_code,
                        item.var_seq if item.var_seq is not None else "",
                        item.point_name,
                        item.io_address,
                        item.data_type,
                        item.driver_name,
                        "是" if item.is_readonly else "否",
                        item.description,
                        item.scan_rate if item.scan_rate is not None else 1000,
                        "是" if item.is_storage else "否",
                    ]
                    for item in point_items
                ]
            )

        sheets.append(WorkbookSheet(name=POINT_DATA_SHEET_NAME, rows=data_rows))
        return build_workbook_bytes(sheets)

    async def import_points_workbook(self, content: bytes, *, actor: str) -> dict[str, Any]:
        workbook = read_workbook_bytes(content)
        rows = self._extract_import_rows(workbook, expected_sheet_name=POINT_DATA_SHEET_NAME)
        records = self._map_import_rows(
            rows,
            expected_headers=POINT_IMPORT_HEADERS,
            required_headers=POINT_REQUIRED_HEADERS,
        )
        return await self._import_point_records(records, actor=actor)

    async def import_points_rows(self, rows: Any, *, actor: str) -> dict[str, Any]:
        records = self._normalize_import_payload_rows(
            rows,
            expected_headers=POINT_IMPORT_HEADERS,
            required_headers=POINT_REQUIRED_HEADERS,
            field_aliases=POINT_IMPORT_FIELD_ALIASES,
        )
        return await self._import_point_records(records, actor=actor)

    async def import_points_from_template(self, device_id: Any, *, actor: str) -> dict[str, Any]:
        normalized_device_id = self._to_positive_int(device_id)
        device_row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        d.type_id AS device_type_id,
                        d.template_id AS template_id
                    FROM device AS d
                    WHERE d.id = :device_id
                    LIMIT 1
                    """
                ),
                {"device_id": normalized_device_id},
            )
        ).mappings().first()
        if device_row is None:
            raise ApiError(404, "Device not found")

        template_row = None
        template_id = self._to_positive_int(device_row["template_id"], required=False)
        if template_id is not None:
            template_row = (
                await self.session.execute(
                    text(
                        """
                        SELECT
                            vt.id AS template_id
                        FROM variable_template AS vt
                        WHERE vt.id = :template_id
                          AND vt.device_type_id = :device_type_id
                          AND COALESCE(vt.enabled, 0) = 1
                        LIMIT 1
                        """
                    ),
                    {
                        "template_id": template_id,
                        "device_type_id": int(device_row["device_type_id"]),
                    },
                )
            ).mappings().first()

        if template_row is None:
            template_row = (
                await self.session.execute(
                    text(
                        """
                        SELECT
                            vt.id AS template_id
                        FROM variable_template AS vt
                        WHERE vt.device_type_id = :device_type_id
                          AND COALESCE(vt.enabled, 0) = 1
                        ORDER BY vt.version_no DESC, vt.id DESC
                        LIMIT 1
                        """
                    ),
                    {"device_type_id": int(device_row["device_type_id"])},
                )
            ).mappings().first()

        if template_row is None:
            raise ApiError(404, "当前设备类型未找到可用模板")

        template_points = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        tv.var_seq AS var_seq,
                        COALESCE(tv.var_name, CAST(tv.var_seq AS CHAR), '') AS var_name,
                        UPPER(COALESCE(tv.var_type, 'STRING')) AS var_type,
                        COALESCE(tv.var_address, '') AS var_address,
                        COALESCE(tv.io_type, '') AS io_type,
                        tv.var_offset AS var_offset,
                        tv.var_bit AS var_bit,
                        COALESCE(tv.description, '') AS description,
                        tv.scan_rate AS scan_rate,
                        COALESCE(tv.is_alarm, 0) AS is_alarm,
                        COALESCE(tv.is_storage, 0) AS is_storage
                    FROM template_variable AS tv
                    WHERE tv.template_id = :template_id
                    ORDER BY tv.var_seq ASC, tv.id ASC
                    """
                ),
                {"template_id": int(template_row["template_id"])},
            )
        ).mappings().all()
        if not template_points:
            raise ApiError(404, "当前模板下没有可导入的点位")

        existing_rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dv.var_seq AS var_seq,
                        LOWER(COALESCE(dv.var_address, '')) AS io_address
                    FROM device_variable AS dv
                    WHERE dv.device_id = :device_id
                    """
                ),
                {"device_id": int(device_row["device_id"])},
            )
        ).mappings().all()
        existing_var_seqs = {
            int(row["var_seq"])
            for row in existing_rows
            if row["var_seq"] is not None
        }
        existing_addresses = {
            str(row["io_address"] or "").strip().lower()
            for row in existing_rows
            if str(row["io_address"] or "").strip()
        }

        seen_template_var_seqs: set[int] = set()
        seen_template_addresses: set[str] = set()
        success = 0
        fail = 0
        errors: list[dict[str, Any]] = []
        insert_sql = text(
            """
            INSERT INTO device_variable (
                device_id,
                var_seq,
                var_name,
                io_type,
                var_type,
                var_address,
                var_offset,
                var_bit,
                description,
                scan_rate,
                is_alarm,
                is_storage,
                create_user,
                create_time,
                update_user,
                update_time
            ) VALUES (
                :device_id,
                :var_seq,
                :var_name,
                :io_type,
                :var_type,
                :var_address,
                :var_offset,
                :var_bit,
                :description,
                :scan_rate,
                :is_alarm,
                :is_storage,
                :actor,
                NOW(),
                :actor,
                NOW()
            )
            """
        )

        for index, row in enumerate(template_points, start=1):
            try:
                var_seq = self._to_positive_int(row["var_seq"])
                io_address = self._require_text(row["var_address"], field_name="IO地址", max_length=20)
                normalized_address = io_address.lower()
                if var_seq in seen_template_var_seqs:
                    raise ApiError(400, "模板内存在重复点位序号")
                if normalized_address in seen_template_addresses:
                    raise ApiError(400, "模板内存在重复 IO地址")
                if var_seq in existing_var_seqs:
                    raise ApiError(409, "点位序号已存在于当前设备")
                if normalized_address in existing_addresses:
                    raise ApiError(409, "IO地址已存在于当前设备")

                await self.session.execute(
                    insert_sql,
                    {
                        "device_id": int(device_row["device_id"]),
                        "var_seq": var_seq,
                        "var_name": self._require_text(row["var_name"], field_name="点位名称", max_length=50),
                        "io_type": self._truncate_text(str(row["io_type"] or "").strip() or "INPUT", 10),
                        "var_type": self._normalize_requested_data_type(row["var_type"]),
                        "var_address": io_address,
                        "var_offset": row["var_offset"],
                        "var_bit": row["var_bit"],
                        "description": self._optional_text(row["description"], max_length=255),
                        "scan_rate": self._to_positive_int(row["scan_rate"], default=1000, required=False) or 1000,
                        "is_alarm": 1 if self._to_bool(row["is_alarm"], field_name="isAlarm", default=False) else 0,
                        "is_storage": 1 if self._to_bool(row["is_storage"], field_name="isStorage", default=True) else 0,
                        "actor": self._truncate_text(actor, 20),
                    },
                )
                await self.session.commit()
                existing_var_seqs.add(var_seq)
                existing_addresses.add(normalized_address)
                seen_template_var_seqs.add(var_seq)
                seen_template_addresses.add(normalized_address)
                success += 1
            except ApiError as exc:
                fail += 1
                errors.append(
                    {
                        "rowNumber": index,
                        "reason": str(exc),
                    }
                )
            except IntegrityError as exc:
                await self.session.rollback()
                fail += 1
                errors.append(
                    {
                        "rowNumber": index,
                        "reason": "模板点位导入失败，点位序号或 IO 地址可能已存在",
                    }
                )

        result: dict[str, Any] = {"success": success, "fail": fail}
        if errors:
            result["errors"] = errors
        return result

    async def _import_point_records(
        self,
        records: list[dict[str, Any]],
        *,
        actor: str,
    ) -> dict[str, Any]:
        if not records:
            raise ApiError(400, "导入数据为空")

        success = 0
        fail = 0
        errors: list[dict[str, Any]] = []
        seen_point_keys: set[str] = set()
        device_cache: dict[str, dict[str, Any]] = {}

        for record in records:
            try:
                payload = await self._build_point_import_payload(
                    record["values"],
                    seen_point_keys=seen_point_keys,
                    device_cache=device_cache,
                )
                await self.create_point(payload, actor=actor)
                success += 1
            except ApiError as exc:
                fail += 1
                errors.append(
                    {
                        "rowNumber": record["row_number"],
                        "reason": str(exc),
                    }
                )

        result: dict[str, Any] = {"success": success, "fail": fail}
        if errors:
            result["errors"] = errors
        return result

    async def create_point(self, payload: dict[str, Any], *, actor: str) -> dict[str, int]:
        device_id = self._to_positive_int(payload.get("deviceId"))
        device_code = self._require_text(payload.get("deviceCode"), field_name="deviceCode", max_length=20)
        var_seq = self._to_positive_int(payload.get("varSeq"))
        point_name = self._require_text(payload.get("pointName"), field_name="pointName", max_length=50)
        io_address = self._require_text(payload.get("ioAddress"), field_name="ioAddress", max_length=20)
        data_type = self._normalize_requested_data_type(payload.get("dataType"))
        description = self._optional_text(payload.get("description"), max_length=255)
        is_readonly = self._to_bool(payload.get("isReadonly"), field_name="isReadonly")
        driver_name = self._require_text(payload.get("driverName"), field_name="driverName", max_length=50)
        scan_rate = self._to_positive_int(payload.get("scanRate"), default=1000, required=False) or 1000
        is_storage = self._to_bool(payload.get("isStorage"), field_name="isStorage", default=True)

        device_row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(cp.protocol, '') AS driver_name
                    FROM device AS d
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    WHERE d.id = :device_id
                    LIMIT 1
                    """
                ),
                {"device_id": device_id},
            )
        ).mappings().first()
        if device_row is None:
            raise ApiError(404, "Device not found")
        if str(device_row["device_code"]).strip().lower() != device_code.lower():
            raise ApiError(400, "deviceCode does not match selected device")
        if self._normalize_protocol_key(device_row["driver_name"]) != self._normalize_protocol_key(driver_name):
            raise ApiError(400, "driverName does not match selected device")

        existing_address = (
            await self.session.execute(
                text(
                    """
                    SELECT id
                    FROM device_variable
                    WHERE device_id = :device_id
                      AND var_address = :io_address
                    LIMIT 1
                    """
                ),
                {"device_id": device_id, "io_address": io_address},
            )
        ).scalar_one_or_none()
        if existing_address is not None:
            raise ApiError(409, "ioAddress already exists for the selected device")

        existing_var_seq = (
            await self.session.execute(
                text(
                    """
                    SELECT id
                    FROM device_variable
                    WHERE device_id = :device_id
                      AND var_seq = :var_seq
                    LIMIT 1
                    """
                ),
                {"device_id": device_id, "var_seq": var_seq},
            )
        ).scalar_one_or_none()
        if existing_var_seq is not None:
            raise ApiError(409, "varSeq already exists for the selected device")

        params = {
            "device_id": device_id,
            "group_code": self._normalize_point_group_code(payload.get("groupCode") or payload.get("group_code")),
            "var_seq": var_seq,
            "var_name": point_name,
            "io_type": "INPUT" if is_readonly else "RW",
            "var_type": data_type,
            "var_address": io_address,
            "var_offset": None,
            "var_bit": None,
            "description": description,
            "scan_rate": scan_rate,
            "is_alarm": 0,
            "is_storage": 1 if is_storage else 0,
            "actor": self._truncate_text(actor, 20),
        }

        try:
            point_slot = await self._allocate_point_slot_for_update(
                device_id=device_id,
                group_code=params["group_code"],
                actor=actor,
            )
            result = await self.session.execute(
                text(
                    """
                    INSERT INTO device_variable (
                        device_id,
                        point_group_id,
                        group_index,
                        td_column_name,
                        enable,
                        deleted,
                        var_seq,
                        var_name,
                        io_type,
                        var_type,
                        var_address,
                        var_offset,
                        var_bit,
                        description,
                        scan_rate,
                        is_alarm,
                        is_storage,
                        create_user,
                        create_time,
                        update_user,
                        update_time
                    ) VALUES (
                        :device_id,
                        :point_group_id,
                        :group_index,
                        :td_column_name,
                        1,
                        0,
                        :var_seq,
                        :var_name,
                        :io_type,
                        :var_type,
                        :var_address,
                        :var_offset,
                        :var_bit,
                        :description,
                        :scan_rate,
                        :is_alarm,
                        :is_storage,
                        :actor,
                        NOW(),
                        :actor,
                        NOW()
                    )
                    """
                ),
                {
                    **params,
                    "point_group_id": point_slot["point_group_id"],
                    "group_index": point_slot["group_index"],
                    "td_column_name": point_slot["td_column_name"],
                },
            )
            await self._mark_point_created(
                point_group_id=point_slot["point_group_id"],
                device_id=device_id,
                actor=actor,
            )
            await self.session.commit()
        except IntegrityError as exc:
            await self.session.rollback()
            raise ApiError(409, "新增点位失败，点位序号或 IO 地址可能已存在") from exc

        return {"id": int(result.lastrowid or 0)}

    async def update_point(
        self,
        point_id: int,
        payload: dict[str, Any],
        *,
        actor: str,
        locator_device_id: Any = None,
        locator_var_seq: Any = None,
    ) -> dict[str, int]:
        locator = await self._resolve_point_locator(
            point_id=point_id,
            device_id=locator_device_id if locator_device_id is not None else payload.get("locatorDeviceId"),
            var_seq=locator_var_seq if locator_var_seq is not None else payload.get("locatorVarSeq"),
        )
        point_id = locator["point_id"]

        device_id = self._to_positive_int(payload.get("deviceId"))
        device_code = self._require_text(payload.get("deviceCode"), field_name="deviceCode", max_length=20)
        var_seq = self._to_positive_int(payload.get("varSeq"))
        point_name = self._require_text(payload.get("pointName"), field_name="pointName", max_length=50)
        io_address = self._require_text(payload.get("ioAddress"), field_name="ioAddress", max_length=20)
        data_type = self._normalize_requested_data_type(payload.get("dataType"))
        description = self._optional_text(payload.get("description"), max_length=255)
        is_readonly = self._to_bool(payload.get("isReadonly"), field_name="isReadonly")
        driver_name = self._require_text(payload.get("driverName"), field_name="driverName", max_length=50)
        scan_rate = self._to_positive_int(payload.get("scanRate"), default=1000, required=False) or 1000
        is_storage = self._to_bool(payload.get("isStorage"), field_name="isStorage", default=True)

        device_row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(cp.protocol, '') AS driver_name
                    FROM device AS d
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    WHERE d.id = :device_id
                    LIMIT 1
                    """
                ),
                {"device_id": device_id},
            )
        ).mappings().first()
        if device_row is None:
            raise ApiError(404, "Device not found")
        if str(device_row["device_code"]).strip().lower() != device_code.lower():
            raise ApiError(400, "deviceCode does not match selected device")
        if self._normalize_protocol_key(device_row["driver_name"]) != self._normalize_protocol_key(driver_name):
            raise ApiError(400, "driverName does not match selected device")

        existing_point = (
            await self.session.execute(
                text(
                    """
                    SELECT device_id, point_group_id
                    FROM device_variable
                    WHERE id = :point_id
                      AND COALESCE(deleted, 0) = 0
                    LIMIT 1
                    """
                ),
                {"point_id": point_id},
            )
        ).mappings().first()
        if existing_point is None:
            raise ApiError(404, "Point not found")
        if int(existing_point["device_id"]) != device_id:
            raise ApiError(400, "point device cannot be changed after creation")

        existing_address = (
            await self.session.execute(
                text(
                    """
                    SELECT id
                    FROM device_variable
                    WHERE device_id = :device_id
                      AND var_address = :io_address
                      AND id <> :point_id
                    LIMIT 1
                    """
                ),
                {"device_id": device_id, "io_address": io_address, "point_id": point_id},
            )
        ).scalar_one_or_none()
        if existing_address is not None:
            raise ApiError(409, "ioAddress already exists for the selected device")

        existing_var_seq = (
            await self.session.execute(
                text(
                    """
                    SELECT id
                    FROM device_variable
                    WHERE device_id = :device_id
                      AND var_seq = :var_seq
                      AND id <> :point_id
                    LIMIT 1
                    """
                ),
                {
                    "device_id": device_id,
                    "var_seq": var_seq,
                    "point_id": point_id,
                },
            )
        ).scalar_one_or_none()
        if existing_var_seq is not None:
            raise ApiError(409, "selected device already contains a conflicting point sequence")

        params = {
            "point_id": point_id,
            "device_id": device_id,
            "var_seq": var_seq,
            "var_name": point_name,
            "io_type": "INPUT" if is_readonly else "RW",
            "var_type": data_type,
            "var_address": io_address,
            "description": description,
            "scan_rate": scan_rate,
            "is_storage": 1 if is_storage else 0,
            "actor": self._truncate_text(actor, 20),
        }

        try:
            await self.session.execute(
                text(
                    """
                    UPDATE device_variable
                    SET
                        device_id = :device_id,
                        var_seq = :var_seq,
                        var_name = :var_name,
                        io_type = :io_type,
                        var_type = :var_type,
                        var_address = :var_address,
                        description = :description,
                        scan_rate = :scan_rate,
                        is_storage = :is_storage,
                        update_user = :actor,
                        update_time = NOW()
                    WHERE id = :point_id
                    """
                ),
                params,
            )
            point_group_id = existing_point["point_group_id"]
            if point_group_id is not None:
                await self._mark_point_config_changed(
                    point_group_id=int(point_group_id),
                    device_id=device_id,
                    actor=actor,
                )
            await self.session.commit()
        except IntegrityError as exc:
            await self.session.rollback()
            raise ApiError(409, "编辑点位失败，IO 地址或点位序号可能冲突") from exc

        return {"id": point_id}

    async def delete_point(
        self,
        point_id: int,
        *,
        actor: str,
        locator_device_id: Any = None,
        locator_var_seq: Any = None,
    ) -> dict[str, int]:
        locator = await self._resolve_point_locator(
            point_id=point_id,
            device_id=locator_device_id,
            var_seq=locator_var_seq,
        )
        point_id = locator["point_id"]

        row = await self._fetch_point_for_delete(point_id)
        if row is None:
            raise ApiError(404, "Point not found")

        try:
            await self._soft_delete_points([row], actor=actor)
            await self.session.commit()
        except IntegrityError as exc:
            await self.session.rollback()
            raise ApiError(409, "删除点位失败") from exc
        return {"id": point_id}

    async def batch_delete_points(self, ids: list[Any], *, actor: str) -> dict[str, int]:
        normalized_ids = self._normalize_id_list(ids, field_name="ids")
        select_stmt = text(
            """
            SELECT id, device_id, point_group_id, enable, deleted
            FROM device_variable
            WHERE id IN :ids
              AND COALESCE(deleted, 0) = 0
            """
        ).bindparams(
            bindparam("ids", expanding=True)
        )
        rows = [
            dict(row)
            for row in (await self.session.execute(select_stmt, {"ids": normalized_ids})).mappings().all()
        ]
        if not rows:
            raise ApiError(404, "No points found")

        try:
            await self._soft_delete_points(rows, actor=actor)
            await self.session.commit()
        except IntegrityError as exc:
            await self.session.rollback()
            raise ApiError(409, "批量删除点位失败") from exc
        return {"count": len(rows)}

    async def _resolve_point_locator(
        self,
        *,
        point_id: Any = None,
        device_id: Any = None,
        var_seq: Any = None,
    ) -> dict[str, int]:
        has_device_locator = device_id not in (None, "")
        has_var_seq_locator = var_seq not in (None, "")

        if has_device_locator or has_var_seq_locator:
            if not (has_device_locator and has_var_seq_locator):
                raise ApiError(400, "deviceId and varSeq must be provided together")

            normalized_device_id = self._to_positive_int(device_id)
            normalized_var_seq = self._to_positive_int(var_seq)
            row = (
                await self.session.execute(
                    text(
                        """
                        SELECT
                            id AS point_id,
                            device_id,
                            var_seq
                        FROM device_variable
                        WHERE device_id = :device_id
                          AND var_seq = :var_seq
                          AND COALESCE(deleted, 0) = 0
                        LIMIT 1
                        """
                    ),
                    {
                        "device_id": normalized_device_id,
                        "var_seq": normalized_var_seq,
                    },
                )
            ).mappings().first()
            if row is None:
                raise ApiError(404, "Point not found")
            return {
                "point_id": int(row["point_id"]),
                "device_id": int(row["device_id"]),
                "var_seq": int(row["var_seq"]) if row["var_seq"] is not None else 0,
            }

        normalized_point_id = self._to_positive_int(point_id)
        row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        id AS point_id,
                        device_id,
                        var_seq
                        FROM device_variable
                        WHERE id = :point_id
                          AND COALESCE(deleted, 0) = 0
                        LIMIT 1
                    """
                ),
                {"point_id": normalized_point_id},
            )
        ).mappings().first()
        if row is None:
            raise ApiError(404, "Point not found")
        return {
            "point_id": int(row["point_id"]),
            "device_id": int(row["device_id"]),
            "var_seq": int(row["var_seq"]) if row["var_seq"] is not None else 0,
        }

    def _normalize_point_group_code(self, value: Any) -> str:
        group_code = str(value or "g001").strip().lower()
        return group_code or "g001"

    def _point_group_no(self, group_code: str) -> int:
        match = re.search(r"(\d+)$", group_code)
        return int(match.group(1)) if match else 1

    async def _allocate_point_slot_for_update(
        self,
        *,
        device_id: int,
        group_code: str,
        actor: str,
    ) -> dict[str, Any]:
        target_code = self._normalize_point_group_code(group_code)
        actor_value = self._truncate_text(actor, 20)

        while True:
            row = (
                await self.session.execute(
                    text(
                        """
                        SELECT
                            id,
                            max_point_count,
                            used_column_count,
                            next_column_index
                        FROM device_point_group
                        WHERE device_id = :device_id
                          AND group_code = :group_code
                        LIMIT 1
                        FOR UPDATE
                        """
                    ),
                    {"device_id": device_id, "group_code": target_code},
                )
            ).mappings().first()

            if row is None:
                group_no = self._point_group_no(target_code)
                result = await self.session.execute(
                    text(
                        """
                        INSERT INTO device_point_group (
                            device_id,
                            group_no,
                            group_code,
                            group_name,
                            max_point_count,
                            active_point_count,
                            disabled_point_count,
                            deleted_point_count,
                            used_column_count,
                            next_column_index,
                            status,
                            config_version,
                            create_user,
                            update_user
                        ) VALUES (
                            :device_id,
                            :group_no,
                            :group_code,
                            :group_name,
                            3072,
                            0,
                            0,
                            0,
                            0,
                            1,
                            'active',
                            1,
                            :actor,
                            :actor
                        )
                        """
                    ),
                    {
                        "device_id": device_id,
                        "group_no": group_no,
                        "group_code": target_code,
                        "group_name": f"第{group_no}组",
                        "actor": actor_value,
                    },
                )
                row = {
                    "id": int(result.lastrowid or 0),
                    "max_point_count": 3072,
                    "used_column_count": 0,
                    "next_column_index": 1,
                }

            max_point_count = int(row["max_point_count"] or 3072)
            next_column_index = int(row["next_column_index"] or 1)
            used_column_count = int(row["used_column_count"] or 0)
            if used_column_count < max_point_count and next_column_index <= max_point_count:
                return {
                    "point_group_id": int(row["id"]),
                    "group_code": target_code,
                    "group_index": next_column_index,
                    "td_column_name": f"v{next_column_index:04d}",
                }

            await self.session.execute(
                text(
                    """
                    UPDATE device_point_group
                    SET status = 'full',
                        update_user = :actor,
                        update_time = NOW()
                    WHERE id = :group_id
                    """
                ),
                {"group_id": int(row["id"]), "actor": actor_value},
            )
            target_code = f"g{self._point_group_no(target_code) + 1:03d}"

    async def _mark_point_created(self, *, point_group_id: int, device_id: int, actor: str) -> None:
        actor_value = self._truncate_text(actor, 20)
        await self.session.execute(
            text(
                """
                UPDATE device_point_group
                SET active_point_count = active_point_count + 1,
                    used_column_count = used_column_count + 1,
                    next_column_index = next_column_index + 1,
                    status = CASE
                        WHEN used_column_count + 1 >= max_point_count THEN 'full'
                        ELSE 'active'
                    END,
                    config_version = config_version + 1,
                    update_user = :actor,
                    update_time = NOW()
                WHERE id = :point_group_id
                """
            ),
            {"point_group_id": point_group_id, "actor": actor_value},
        )
        await self._bump_device_variable_config(device_id)

    async def _mark_point_config_changed(self, *, point_group_id: int, device_id: int, actor: str) -> None:
        actor_value = self._truncate_text(actor, 20)
        await self.session.execute(
            text(
                """
                UPDATE device_point_group
                SET config_version = config_version + 1,
                    update_user = :actor,
                    update_time = NOW()
                WHERE id = :point_group_id
                """
            ),
            {"point_group_id": point_group_id, "actor": actor_value},
        )
        await self._bump_device_variable_config(device_id)

    async def _bump_device_variable_config(self, device_id: int) -> None:
        await self.session.execute(
            text(
                """
                UPDATE device
                SET variable_config_version = COALESCE(variable_config_version, 1) + 1,
                    update_time = NOW()
                WHERE id = :device_id
                """
            ),
            {"device_id": device_id},
        )

    async def _fetch_point_for_delete(self, point_id: int) -> dict[str, Any] | None:
        row = (
            await self.session.execute(
                text(
                    """
                    SELECT id, device_id, point_group_id, enable, deleted
                    FROM device_variable
                    WHERE id = :point_id
                      AND COALESCE(deleted, 0) = 0
                    LIMIT 1
                    """
                ),
                {"point_id": point_id},
            )
        ).mappings().first()
        return dict(row) if row is not None else None

    async def _soft_delete_points(self, rows: list[dict[str, Any]], *, actor: str) -> None:
        actor_value = self._truncate_text(actor, 20)
        ids = [int(row["id"]) for row in rows]
        update_stmt = text(
            """
            UPDATE device_variable
            SET deleted = 1,
                enable = 0,
                update_user = :actor,
                update_time = NOW()
            WHERE id IN :ids
              AND COALESCE(deleted, 0) = 0
            """
        ).bindparams(bindparam("ids", expanding=True))
        await self.session.execute(update_stmt, {"ids": ids, "actor": actor_value})

        by_group: dict[int, dict[str, Any]] = {}
        for row in rows:
            point_group_id = row.get("point_group_id")
            if point_group_id is None:
                continue
            group = by_group.setdefault(
                int(point_group_id),
                {"device_id": int(row["device_id"]), "active": 0, "disabled": 0},
            )
            if int(row.get("enable") or 0) == 1:
                group["active"] += 1
            else:
                group["disabled"] += 1

        for point_group_id, counts in by_group.items():
            await self.session.execute(
                text(
                    """
                    SELECT id
                    FROM device_point_group
                    WHERE id = :point_group_id
                    FOR UPDATE
                    """
                ),
                {"point_group_id": point_group_id},
            )
            await self.session.execute(
                text(
                    """
                    UPDATE device_point_group
                    SET active_point_count = GREATEST(active_point_count - :active_count, 0),
                        disabled_point_count = GREATEST(disabled_point_count - :disabled_count, 0),
                        deleted_point_count = deleted_point_count + :deleted_count,
                        config_version = config_version + 1,
                        update_user = :actor,
                        update_time = NOW()
                    WHERE id = :point_group_id
                    """
                ),
                {
                    "point_group_id": point_group_id,
                    "active_count": counts["active"],
                    "disabled_count": counts["disabled"],
                    "deleted_count": counts["active"] + counts["disabled"],
                    "actor": actor_value,
                },
            )
            await self._bump_device_variable_config(int(counts["device_id"]))

    async def _fetch_device_group_rows(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dg.id AS group_id,
                        COALESCE(dg.group_name, '') AS group_name,
                        COALESCE(dg.group_code, '') AS group_code,
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(dt.type_name, dt.type_code, '') AS device_type,
                        COALESCE(dt.type_code, '') AS device_type_code,
                        COALESCE(cp.protocol, '') AS driver_name,
                        COALESCE(d.enable, 0) AS enable,
                        COALESCE(d.status, 0) AS device_status_raw,
                        COALESCE(d.endpoint, '') AS endpoint,
                        COALESCE(d.endpoint_bkp, '') AS endpoint_bkp,
                        d.create_time AS create_time,
                        d.update_time AS update_time
                    FROM device_group AS dg
                    LEFT JOIN device AS d ON d.group_id = dg.id
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    ORDER BY dg.id ASC, d.id ASC
                    """
                )
            )
        ).mappings().all()
        return [dict(row) for row in rows]

    async def _fetch_device_rows(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(dt.type_name, dt.type_code, '') AS device_type,
                        COALESCE(dt.type_code, '') AS device_type_code,
                        COALESCE(dg.group_name, '') AS group_name,
                        COALESCE(dg.group_code, '') AS group_code,
                        COALESCE(cp.protocol, '') AS driver_name,
                        COALESCE(d.enable, 0) AS enable,
                        COALESCE(d.status, 0) AS device_status_raw,
                        COALESCE(d.endpoint, '') AS endpoint,
                        COALESCE(d.endpoint_bkp, '') AS endpoint_bkp,
                        d.create_time AS create_time,
                        d.update_time AS update_time
                    FROM device AS d
                    LEFT JOIN device_type AS dt ON dt.id = d.type_id
                    LEFT JOIN device_group AS dg ON dg.id = d.group_id
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    ORDER BY d.id ASC
                    """
                )
            )
        ).mappings().all()
        return [dict(row) for row in rows]

    async def _fetch_point_rows(self) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dv.id AS point_id,
                        dv.device_id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(d.device_name, d.device_code) AS device_name,
                        COALESCE(dv.var_name, CAST(dv.var_seq AS CHAR), '') AS point_name,
                        COALESCE(dv.var_address, '') AS io_address,
                        UPPER(COALESCE(dv.var_type, 'STRING')) AS data_type,
                        COALESCE(dv.description, '') AS description,
                        COALESCE(cp.protocol, '') AS driver_name,
                        dv.create_time AS create_time,
                        dv.update_time AS update_time,
                        dv.var_seq AS var_seq,
                        dv.scan_rate AS scan_rate,
                        COALESCE(dv.is_alarm, 0) AS is_alarm,
                        COALESCE(dv.is_storage, 0) AS is_storage,
                        CASE
                            WHEN UPPER(COALESCE(dv.io_type, '')) IN ('OUTPUT', 'WRITE', 'RW')
                                THEN 0
                            ELSE 1
                        END AS is_readonly,
                        d.group_id AS group_id
                    FROM device_variable AS dv
                    JOIN device AS d ON d.id = dv.device_id
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    WHERE COALESCE(dv.deleted, 0) = 0
                    ORDER BY dv.id ASC
                    """
                )
            )
        ).mappings().all()
        return [dict(row) for row in rows]

    def _to_device_item(self, row: dict[str, Any]) -> DeviceCatalogItem:
        return DeviceCatalogItem(
            device_id=int(row["device_id"]),
            device_code=str(row["device_code"]),
            device_name=str(row["device_name"] or row["device_code"]),
            device_type=str(row["device_type"] or row["device_type_code"] or ""),
            device_type_code=str(row["device_type_code"] or ""),
            group_name=str(row["group_name"] or ""),
            group_code=str(row["group_code"] or ""),
            driver_name=str(row["driver_name"] or ""),
            enable=int(row["enable"] or 0),
            device_status_raw=int(row["device_status_raw"] or 0),
            endpoint=str(row["endpoint"] or ""),
            endpoint_bkp=str(row["endpoint_bkp"] or ""),
            create_time=self._coerce_datetime(row["create_time"]),
            update_time=self._coerce_datetime(row["update_time"]),
        )

    def _to_point_item(self, row: dict[str, Any]) -> PointCatalogItem:
        return PointCatalogItem(
            point_id=int(row["point_id"]),
            device_id=int(row["device_id"]),
            device_code=str(row["device_code"]),
            device_name=str(row["device_name"] or row["device_code"]),
            point_name=str(row["point_name"] or row["var_seq"] or ""),
            io_address=str(row["io_address"] or ""),
            data_type=self._normalize_data_type(row["data_type"]),
            description=str(row["description"] or ""),
            driver_name=str(row["driver_name"] or ""),
            create_time=self._coerce_datetime(row["create_time"]),
            update_time=self._coerce_datetime(row["update_time"]),
            var_seq=int(row["var_seq"]) if row["var_seq"] is not None else None,
            scan_rate=int(row["scan_rate"]) if row["scan_rate"] is not None else None,
            is_alarm=bool(int(row["is_alarm"] or 0)),
            is_storage=bool(int(row["is_storage"] or 0)),
            is_readonly=bool(int(row["is_readonly"] or 0)),
            group_id=int(row["group_id"]) if row["group_id"] is not None else None,
        )

    def _serialize_device_item(
        self,
        device: DeviceCatalogItem,
        last_collect_time: datetime | None,
    ) -> dict[str, Any]:
        description = device.group_name or device.endpoint or device.endpoint_bkp or ""
        connect_status = (
            self._build_connect_status(last_collect_time)
            if last_collect_time is not None
            else self._build_connect_status_from_raw(device.device_status_raw)
        )
        data_status = self._build_data_status(last_collect_time) if last_collect_time is not None else "nodata"
        return {
            "id": device.device_id,
            "deviceType": device.device_type or device.device_type_code or device.group_name or "",
            "deviceCode": device.device_code,
            "deviceName": device.device_name,
            "driverName": device.driver_name,
            "deviceStatus": "enabled" if device.enable == 1 else "disabled",
            "connectStatus": connect_status,
            "connectUpdateTime": self._format_datetime(last_collect_time),
            "dataStatus": data_status,
            "dataUpdateTime": self._format_datetime(last_collect_time),
            "description": description,
            "groupName": device.group_name,
            "groupCode": device.group_code,
            "endpoint": device.endpoint,
            "endpointBackup": device.endpoint_bkp,
            "createTime": self._format_datetime(device.create_time),
            "updateTime": self._format_datetime(device.update_time),
        }

    def _serialize_point_item(
        self,
        point: PointCatalogItem,
        latest_value: dict[str, Any] | None,
    ) -> dict[str, Any]:
        return {
            "id": point.point_id,
            "deviceId": point.device_id,
            "deviceCode": point.device_code,
            "deviceName": point.device_name,
            "varSeq": point.var_seq,
            "pointName": point.point_name,
            "ioAddress": point.io_address,
            "dataType": point.data_type,
            "description": point.description,
            "isReadonly": point.is_readonly,
            "driverName": point.driver_name,
            "currentValue": self._normalize_point_value(point.data_type, latest_value["value"])
            if latest_value
            else "--",
            "createTime": self._format_datetime(point.create_time),
            "updateTime": self._format_datetime(latest_value["ts"] if latest_value else point.update_time),
            "scanRate": point.scan_rate,
            "isAlarm": point.is_alarm,
            "isStorage": point.is_storage,
        }

    def _matches_basic_device_filters(
        self,
        device: DeviceCatalogItem,
        params: dict[str, Any],
    ) -> bool:
        keyword = self._normalize_text(params.get("keyword"))
        device_type = self._normalize_text(params.get("deviceType"))
        device_code = self._normalize_text(params.get("deviceCode"))
        device_name = self._normalize_text(params.get("deviceName"))
        device_status = self._normalize_text(params.get("deviceStatus"))
        normalized_device_status = "enabled" if device.enable == 1 else "disabled"

        if keyword and not self._matches_keyword(
            keyword,
            device.device_code,
            device.device_name,
            device.driver_name,
            device.device_type,
            device.group_name,
            device.group_code,
            device.endpoint,
            device.endpoint_bkp,
        ):
            return False
        if device_type and not self._matches_keyword(
            device_type,
            device.device_type,
            device.device_type_code,
            device.group_name,
            device.group_code,
        ):
            return False
        if device_code and not self._matches_keyword(device_code, device.device_code):
            return False
        if device_name and not self._matches_keyword(device_name, device.device_name):
            return False
        if device_status and normalized_device_status != device_status:
            return False
        return True

    def _filter_point_items(
        self,
        items: list[PointCatalogItem],
        params: dict[str, Any],
    ) -> list[PointCatalogItem]:
        keyword = self._normalize_text(params.get("keyword"))
        device_id = self._to_positive_int(params.get("deviceId"), required=False)
        device_code = self._normalize_text(params.get("deviceCode"))
        group_id = self._to_positive_int(params.get("groupId"), required=False)
        var_seq = self._to_positive_int(params.get("varSeq"), required=False)
        point_name = self._normalize_text(params.get("pointName"))
        io_address = self._normalize_text(params.get("ioAddress"))

        filtered: list[PointCatalogItem] = []
        for item in items:
            if device_id is not None and item.device_id != device_id:
                continue
            if device_code and not self._matches_keyword(device_code, item.device_code):
                continue
            if group_id is not None and item.group_id != group_id:
                continue
            if var_seq is not None and item.var_seq != var_seq:
                continue
            if point_name and not self._matches_keyword(point_name, item.point_name):
                continue
            if io_address and not self._matches_keyword(io_address, item.io_address):
                continue
            if keyword and not self._matches_keyword(
                keyword,
                item.point_name,
                item.io_address,
                item.description,
                item.device_code,
                item.var_seq,
            ):
                continue
            filtered.append(item)
        return filtered

    async def _fetch_export_devices(self, params: dict[str, Any]) -> list[dict[str, Any]]:
        rows = await self._fetch_device_rows()
        devices = [self._to_device_item(row) for row in rows]
        filtered_devices = [device for device in devices if self._matches_basic_device_filters(device, params)]

        connect_status = self._normalize_text(params.get("connectStatus"))
        latest_map = await self._fetch_latest_collect_times(filtered_devices) if filtered_devices else {}
        serialized = [
            self._serialize_device_item(device, latest_map.get(device.device_code))
            for device in filtered_devices
        ]
        if connect_status:
            serialized = [
                item for item in serialized if str(item["connectStatus"]).strip().lower() == connect_status
            ]
        return serialized

    async def _fetch_export_points(self, params: dict[str, Any]) -> list[PointCatalogItem]:
        rows = await self._fetch_point_rows()
        points = [self._to_point_item(row) for row in rows]
        return self._filter_point_items(points, params)

    def _device_import_notes(self) -> list[tuple[str, str]]:
        return [
            ("文件格式", "仅支持 .xlsx；第一张为说明，第二张为设备数据。"),
            ("必填列", "设备类型、设备编号、设备名称、关联驱动。"),
            ("可选列", "设备状态、描述。设备状态留空时默认启用。"),
            ("设备状态", "支持 enabled / disabled / 启用 / 停用。"),
            ("导入策略", "按行校验，部分成功；重复设备编号或非法字典项会单行失败。"),
        ]

    def _point_import_notes(self) -> list[tuple[str, str]]:
        return [
            ("文件格式", "仅支持 .xlsx；第一张为说明，第二张为点位数据。"),
            ("必填列", "设备编号、点位序号、点位名称、IO地址、数据类型、驱动名称。"),
            ("可选列", "是否只读、描述、采集周期(ms)、是否存储。"),
            ("枚举值", "数据类型支持 BOOL/INT/DINT/REAL/STRING/WORD/DWORD；布尔列支持 true/false/1/0/是/否。"),
            ("导入策略", "按行校验，部分成功；同设备下点位序号重复、IO地址重复或驱动不匹配会单行失败。"),
        ]

    def _build_device_instruction_row(self, title: str, detail: str) -> list[str]:
        return [title, detail]

    def _extract_import_rows(
        self,
        workbook: dict[str, list[list[str]]],
        *,
        expected_sheet_name: str,
    ) -> list[list[str]]:
        if not workbook:
            raise ApiError(400, "导入文件为空或无法读取")

        if expected_sheet_name in workbook:
            return workbook[expected_sheet_name]

        ordered_sheets = list(workbook.items())
        if len(ordered_sheets) >= 2:
            return ordered_sheets[1][1]
        return ordered_sheets[0][1]

    def _map_import_rows(
        self,
        rows: list[list[str]],
        *,
        expected_headers: list[str],
        required_headers: set[str],
    ) -> list[dict[str, Any]]:
        if not rows:
            raise ApiError(400, "导入数据表为空")

        header_row = [str(value or "").strip() for value in rows[0]]
        header_map = {header: index for index, header in enumerate(header_row) if header}
        missing_headers = [header for header in required_headers if header not in header_map]
        if missing_headers:
            raise ApiError(400, f"缺少必填列: {', '.join(missing_headers)}")

        records: list[dict[str, Any]] = []
        for row_number, row in enumerate(rows[1:], start=2):
            values = {
                header: str(row[header_map[header]]).strip() if header_map[header] < len(row) else ""
                for header in expected_headers
                if header in header_map
            }
            if not any(values.values()):
                continue
            records.append({"row_number": row_number, "values": values})

        return records

    def _normalize_import_payload_rows(
        self,
        rows: Any,
        *,
        expected_headers: list[str],
        required_headers: set[str],
        field_aliases: dict[str, tuple[str, ...]] | None = None,
    ) -> list[dict[str, Any]]:
        if not isinstance(rows, list):
            raise ApiError(400, "rows must be a non-empty list")

        resolved_aliases = {
            header: tuple(
                dict.fromkeys(
                    self._normalize_import_field_key(alias)
                    for alias in (field_aliases or {}).get(header, (header,))
                    if self._normalize_import_field_key(alias)
                )
            )
            or (self._normalize_import_field_key(header),)
            for header in expected_headers
        }
        records: list[dict[str, Any]] = []
        for row_number, row in enumerate(rows, start=2):
            if not isinstance(row, dict):
                raise ApiError(400, "rows items must be objects")

            row_lookup = {
                self._normalize_import_field_key(key): value
                for key, value in row.items()
                if self._normalize_import_field_key(key)
            }
            if not any(str(value or "").strip() for value in row_lookup.values()):
                continue

            missing_headers: list[str] = []
            values: dict[str, str] = {}
            for header in expected_headers:
                resolved_value = None
                for alias in resolved_aliases[header]:
                    if alias in row_lookup:
                        resolved_value = row_lookup[alias]
                        break
                if header in required_headers and resolved_value is None:
                    missing_headers.append(header)
                values[header] = str(resolved_value or "").strip()

            if missing_headers:
                raise ApiError(400, f"缺少必填列: {', '.join(missing_headers)}")
            if not any(values.values()):
                continue
            records.append({"row_number": row_number, "values": values})

        return records

    def _normalize_import_field_key(self, value: Any) -> str:
        return IMPORT_KEY_SANITIZE_RE.sub("", str(value or "").strip()).lower()

    def _build_device_import_payload(
        self,
        values: dict[str, str],
        seen_device_codes: set[str],
    ) -> dict[str, Any]:
        device_code = self._require_text(values.get("设备编号"), field_name="设备编号", max_length=20)
        normalized_code = device_code.lower()
        if normalized_code in seen_device_codes:
            raise ApiError(400, "文件内存在重复设备编号")
        seen_device_codes.add(normalized_code)

        return {
            "deviceType": self._require_text(values.get("设备类型"), field_name="设备类型", max_length=20),
            "deviceCode": device_code,
            "deviceName": self._require_text(values.get("设备名称"), field_name="设备名称", max_length=20),
            "driverName": self._require_text(values.get("关联驱动"), field_name="关联驱动", max_length=50),
            "deviceStatus": self._normalize_import_device_status(values.get("设备状态")),
            "description": self._optional_text(values.get("描述"), max_length=255),
        }

    async def _build_point_import_payload(
        self,
        values: dict[str, str],
        *,
        seen_point_keys: set[str],
        device_cache: dict[str, dict[str, Any]],
    ) -> dict[str, Any]:
        device_code = self._require_text(values.get("设备编号"), field_name="设备编号", max_length=20)
        var_seq = self._to_positive_int(values.get("点位序号"))
        point_name = self._require_text(values.get("点位名称"), field_name="点位名称", max_length=50)
        io_address = self._require_text(values.get("IO地址"), field_name="IO地址", max_length=20)
        data_type = self._normalize_requested_data_type(values.get("数据类型"))
        driver_name = self._require_text(values.get("驱动名称"), field_name="驱动名称", max_length=50)
        is_readonly = self._to_bool(values.get("是否只读"), field_name="是否只读", default=True)
        description = self._optional_text(values.get("描述"), max_length=255)
        scan_rate = self._to_positive_int(values.get("采集周期(ms)"), default=1000, required=False) or 1000
        is_storage = self._to_bool(values.get("是否存储"), field_name="是否存储", default=True)

        device = await self._resolve_device_by_code(device_code, device_cache)
        if self._normalize_protocol_key(device["driver_name"]) != self._normalize_protocol_key(driver_name):
            raise ApiError(400, "驱动名称与设备当前驱动不一致")

        point_seq_key = f"{device_code.lower()}::{var_seq}"
        if point_seq_key in seen_point_keys:
            raise ApiError(400, "文件内存在重复 点位序号")
        seen_point_keys.add(point_seq_key)

        point_key = f"{device_code.lower()}::{io_address.lower()}"
        if point_key in seen_point_keys:
            raise ApiError(400, "文件内存在重复 IO地址")
        seen_point_keys.add(point_key)

        return {
            "deviceId": int(device["device_id"]),
            "deviceCode": str(device["device_code"]),
            "varSeq": var_seq,
            "pointName": point_name,
            "ioAddress": io_address,
            "dataType": data_type,
            "description": description,
            "isReadonly": is_readonly,
            "driverName": driver_name,
            "scanRate": scan_rate,
            "isStorage": is_storage,
        }

    async def _resolve_device_by_code(
        self,
        device_code: str,
        cache: dict[str, dict[str, Any]],
    ) -> dict[str, Any]:
        cache_key = device_code.strip().lower()
        if cache_key in cache:
            return cache[cache_key]

        row = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        d.id AS device_id,
                        d.device_code AS device_code,
                        COALESCE(cp.protocol, '') AS driver_name
                    FROM device AS d
                    LEFT JOIN comm_protocol AS cp ON cp.id = d.protocol_id
                    WHERE LOWER(d.device_code) = :device_code
                    LIMIT 1
                    """
                ),
                {"device_code": cache_key},
            )
        ).mappings().first()
        if row is None:
            raise ApiError(404, "设备编号不存在")

        cache[cache_key] = dict(row)
        return cache[cache_key]

    async def _fetch_latest_collect_times(
        self,
        devices: list[DeviceCatalogItem],
    ) -> dict[str, datetime]:
        if not devices:
            return {}

        tasks = [self._fetch_device_latest_collect_time(device) for device in devices]
        results = await asyncio.gather(*tasks)
        return {
            device.device_code: latest_time
            for device, latest_time in zip(devices, results, strict=False)
            if latest_time is not None
        }

    async def _fetch_device_latest_collect_time(
        self,
        device: DeviceCatalogItem,
    ) -> datetime | None:
        try:
            database_name = self._resolve_tdengine_database(
                device.device_code,
                device.device_type_code,
            )
            stable_name = self._normalize_identifier(device.device_code).lower()
            table_name = self.tdengine.qualify_table(database_name, stable_name)
            rows = await self.tdengine.fetch_all(
                f"SELECT LAST(ts) AS last_ts FROM {table_name}",
                database=database_name,
            )
        except Exception:
            return None

        if not rows:
            return None
        return self._coerce_datetime(rows[0][0])

    async def _fetch_latest_point_value_map(
        self,
        points: list[PointCatalogItem],
    ) -> dict[int, dict[str, Any]]:
        if not points:
            return {}

        tasks = [self._fetch_single_point_latest_row(point) for point in points]
        results = await asyncio.gather(*tasks, return_exceptions=True)
        return {
            point.point_id: result
            for point, result in zip(points, results, strict=False)
            if isinstance(result, dict)
        }

    async def _fetch_single_point_latest_row(
        self,
        point: PointCatalogItem,
    ) -> dict[str, Any] | None:
        try:
            database_name = self._resolve_tdengine_database(point.device_code)
            subtable_name = self.tdengine.qualify_table(
                database_name,
                self._resolve_point_subtable_name(point.device_code, point.var_seq, point.point_id),
            )

            try:
                rows = await self.tdengine.fetch_all(
                    f"SELECT ts, `value` FROM {subtable_name} ORDER BY ts DESC LIMIT 1",
                    database=database_name,
                )
            except Exception:
                rows = []

            if not rows:
                try:
                    stable_name = self.tdengine.qualify_table(
                        database_name,
                        self._normalize_identifier(point.device_code).lower(),
                    )
                    if point.var_seq is not None:
                        rows = await self.tdengine.fetch_all(
                            (
                                f"SELECT ts, `value` FROM {stable_name} "
                                f"WHERE var_seq = {int(point.var_seq)} ORDER BY ts DESC LIMIT 1"
                            ),
                            database=database_name,
                        )
                except Exception:
                    rows = []

            if not rows:
                try:
                    legacy_subtable_name = self.tdengine.qualify_table(
                        database_name,
                        self._resolve_point_subtable_name(point.device_code, None, point.point_id),
                    )
                    rows = await self.tdengine.fetch_all(
                        f"SELECT ts, `value` FROM {legacy_subtable_name} ORDER BY ts DESC LIMIT 1",
                        database=database_name,
                    )
                except Exception:
                    rows = []

            if not rows:
                try:
                    stable_name = self.tdengine.qualify_table(
                        database_name,
                        self._normalize_identifier(point.device_code).lower(),
                    )
                    rows = await self.tdengine.fetch_all(
                        (
                            f"SELECT ts, `value` FROM {stable_name} "
                            f"WHERE variable_id = {point.point_id} ORDER BY ts DESC LIMIT 1"
                        ),
                        database=database_name,
                    )
                except Exception:
                    return None

            if not rows:
                return None

            ts, value = rows[0]
            return {
                "ts": self._coerce_datetime(ts),
                "value": value,
            }
        except Exception:
            return None

    def _build_connect_status(self, last_collect_time: datetime | None) -> str:
        if last_collect_time is None:
            return "offline"

        age_seconds = max(0.0, (now_local() - last_collect_time).total_seconds())
        if age_seconds <= ONLINE_MAX_AGE_SECONDS:
            return "online"
        if age_seconds <= UNSTABLE_MAX_AGE_SECONDS:
            return "unstable"
        return "offline"

    def _build_connect_status_from_raw(self, raw_status: int) -> str:
        if raw_status == 1:
            return "online"
        if raw_status == 2:
            return "unstable"
        return "offline"

    def _build_data_status(self, last_collect_time: datetime | None) -> str:
        if last_collect_time is None:
            return "nodata"

        age_seconds = max(0.0, (now_local() - last_collect_time).total_seconds())
        if age_seconds <= ONLINE_MAX_AGE_SECONDS:
            return "normal"
        return "abnormal"

    def _resolve_read_write_type(self, driver_name: Any) -> str:
        normalized = str(driver_name or "").strip().upper()
        if normalized == "OPC-DA":
            return "只读"
        return "读写"

    def _resolve_tdengine_database(
        self,
        device_code: str,
        device_type_code: str | None = None,
    ) -> str:
        if device_type_code:
            normalized_type_code = self._normalize_identifier(device_type_code).lower()
            if normalized_type_code:
                return normalized_type_code

        normalized_device_code = self._normalize_identifier(device_code).lower()
        match = DEVICE_PREFIX_RE.match(normalized_device_code)
        if match is None:
            raise ValueError(f"Unable to resolve TDengine database from device code: {device_code}")
        return match.group(0)

    def _resolve_point_subtable_name(self, device_code: str, var_seq: int | None, point_id: int) -> str:
        normalized_device_code = self._normalize_identifier(device_code).lower()
        suffix = int(var_seq) if var_seq not in (None, 0) else int(point_id)
        return f"{normalized_device_code}_{suffix}"

    def _normalize_identifier(self, value: Any) -> str:
        normalized = IDENTIFIER_SANITIZE_RE.sub("_", str(value or "").strip())
        if not normalized:
            raise ValueError("TDengine identifier cannot be empty")
        return normalized

    def _normalize_data_type(self, value: Any) -> str:
        normalized = str(value or "STRING").strip().upper()
        if normalized in {"BOOL", "INT", "DINT", "REAL", "STRING", "WORD", "DWORD"}:
            return normalized
        return "STRING"

    def _normalize_requested_data_type(self, value: Any) -> str:
        normalized = str(value or "").strip().upper()
        if normalized not in {"BOOL", "INT", "DINT", "REAL", "STRING", "WORD", "DWORD"}:
            raise ApiError(400, "dataType is invalid")
        return normalized

    def _normalize_import_device_status(self, value: Any) -> str:
        normalized = str(value or "").strip().lower()
        if not normalized:
            return "enabled"
        alias_map = {
            "enabled": "enabled",
            "disabled": "disabled",
            "启用": "enabled",
            "停用": "disabled",
        }
        if normalized in alias_map:
            return alias_map[normalized]
        raise ApiError(400, "设备状态只支持 enabled / disabled / 启用 / 停用")

    def _normalize_device_status(self, value: Any) -> str:
        normalized = str(value or "").strip().lower()
        if normalized not in {"enabled", "disabled"}:
            raise ApiError(400, "deviceStatus is invalid")
        return normalized

    def _require_text(self, value: Any, *, field_name: str, max_length: int) -> str:
        normalized = str(value or "").strip()
        if not normalized:
            raise ApiError(400, f"{field_name} is required")
        if len(normalized) > max_length:
            raise ApiError(400, f"{field_name} length must be <= {max_length}")
        return normalized

    def _optional_text(self, value: Any, *, max_length: int) -> str:
        normalized = str(value or "").strip()
        if len(normalized) > max_length:
            raise ApiError(400, f"value length must be <= {max_length}")
        return normalized

    def _to_bool(self, value: Any, *, field_name: str, default: bool | None = None) -> bool:
        if value in (None, ""):
            if default is None:
                raise ApiError(400, f"{field_name} is required")
            return default
        if isinstance(value, bool):
            return value
        normalized = str(value).strip().lower()
        if normalized in {"1", "true", "yes", "on", "是"}:
            return True
        if normalized in {"0", "false", "no", "off", "否"}:
            return False
        raise ApiError(400, f"{field_name} is invalid")

    def _truncate_text(self, value: str, max_length: int) -> str:
        return value[:max_length]

    def _normalize_protocol_key(self, value: Any) -> str:
        return PROTOCOL_KEY_SANITIZE_RE.sub("", str(value or "").strip().upper())

    async def _resolve_device_type_row(self, raw_device_type: str) -> dict[str, Any]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT id, type_code, type_name
                    FROM device_type
                    ORDER BY id ASC
                    """
                )
            )
        ).mappings().all()
        if not rows:
            raise ApiError(400, "device_type dictionary is empty")

        candidates = {raw_device_type.strip().lower()}
        alias_map = {
            "桥吊": {"桥吊", "qc"},
            "轨道吊": {"轨道吊", "asc"},
            "agv": {"agv", "自动导引车"},
            "自动导引车": {"agv", "自动导引车"},
            "堆高机": {"堆高机"},
            "其他": {"其他"},
        }
        candidates.update(alias_map.get(raw_device_type.strip(), set()))
        candidates = {item.strip().lower() for item in candidates if item}

        for row in rows:
            row_tokens = {
                str(row["type_code"] or "").strip().lower(),
                str(row["type_name"] or "").strip().lower(),
            }
            if candidates.intersection(row_tokens):
                return dict(row)

        raise ApiError(400, "deviceType is not supported by current dictionary")

    async def _resolve_protocol_row(self, raw_driver_name: str) -> dict[str, Any]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT id, protocol
                    FROM comm_protocol
                    ORDER BY id ASC
                    """
                )
            )
        ).mappings().all()
        if not rows:
            raise ApiError(400, "comm_protocol dictionary is empty")

        requested_key = self._normalize_protocol_key(raw_driver_name)
        for row in rows:
            if self._normalize_protocol_key(row["protocol"]) == requested_key:
                return dict(row)
        raise ApiError(400, "driverName is not supported by current dictionary")

    async def _fetch_device_reference_row(self, *, type_id: int, protocol_id: int) -> dict[str, Any] | None:
        row = (
            await self.session.execute(
                text(
                    """
                    SELECT group_id, rack, slot, scan_cycle
                    FROM device
                    WHERE type_id = :type_id
                      AND protocol_id = :protocol_id
                    ORDER BY id ASC
                    LIMIT 1
                    """
                ),
                {"type_id": type_id, "protocol_id": protocol_id},
            )
        ).mappings().first()
        if row is not None:
            return dict(row)

        row = (
            await self.session.execute(
                text(
                    """
                    SELECT group_id, rack, slot, scan_cycle
                    FROM device
                    WHERE type_id = :type_id
                    ORDER BY id ASC
                    LIMIT 1
                    """
                ),
                {"type_id": type_id},
            )
        ).mappings().first()
        return dict(row) if row is not None else None

    async def _resolve_group_id(
        self,
        *,
        type_row: dict[str, Any],
        reference_row: dict[str, Any] | None,
    ) -> int:
        if reference_row is not None and reference_row.get("group_id") is not None:
            return int(reference_row["group_id"])

        row = (
            await self.session.execute(
                text(
                    """
                    SELECT id
                    FROM device_group
                    WHERE group_code = :group_code
                       OR group_name = :group_name
                    ORDER BY id ASC
                    LIMIT 1
                    """
                ),
                {
                    "group_code": type_row["type_code"],
                    "group_name": type_row["type_name"],
                },
            )
        ).mappings().first()
        if row is None:
            raise ApiError(400, "No matching device group found for deviceType")
        return int(row["id"])

    def _build_placeholder_endpoint(self, device_code: str) -> str:
        return self._truncate_text(device_code, 20)

    def _normalize_text(self, value: Any) -> str:
        return str(value or "").strip().lower()

    def _matches_keyword(self, keyword: str, *values: Any) -> bool:
        if not keyword:
            return True
        for value in values:
            if keyword in str(value or "").strip().lower():
                return True
        return False

    def _to_positive_int(self, value: Any, *, default: int | None = None, required: bool = True) -> int | None:
        if value in (None, ""):
            if required and default is None:
                raise ApiError(400, "value must be a positive integer")
            return default
        try:
            normalized = int(value)
        except (TypeError, ValueError) as exc:
            raise ApiError(400, "value must be a positive integer") from exc
        if normalized <= 0:
            raise ApiError(400, "value must be a positive integer")
        return normalized

    def _normalize_id_list(self, values: list[Any] | Any, *, field_name: str) -> list[int]:
        raw_values = values if isinstance(values, list) else [values]
        normalized: list[int] = []
        for item in raw_values:
            item_id = self._to_positive_int(item, required=True)
            if item_id is None or item_id in normalized:
                continue
            normalized.append(item_id)

        if not normalized:
            raise ApiError(400, f"{field_name} must contain at least one valid id")
        return normalized

    def _paginate(self, items: list[dict[str, Any]], params: dict[str, Any]) -> dict[str, Any]:
        page_no = self._to_positive_int(params.get("pageNo") or params.get("current"), default=1)
        page_size = self._to_positive_int(params.get("pageSize") or params.get("size"), default=20)
        total = len(items)
        start = (page_no - 1) * page_size
        end = start + page_size
        page_items = items[start:end]
        return self._build_page_payload(page_items, total, page_no, page_size)

    def _paginate_records(
        self,
        items: list[Any],
        params: dict[str, Any],
    ) -> tuple[list[Any], int, int]:
        page_no = self._to_positive_int(params.get("pageNo") or params.get("current"), default=1)
        page_size = self._to_positive_int(params.get("pageSize") or params.get("size"), default=20)
        start = (page_no - 1) * page_size
        end = start + page_size
        return items[start:end], page_no, page_size

    def _build_page_payload(
        self,
        items: list[dict[str, Any]],
        total: int,
        page_no: int,
        page_size: int,
    ) -> dict[str, Any]:
        return {
            "list": items,
            "records": items,
            "total": total,
            "pageNo": page_no,
            "pageSize": page_size,
            "current": page_no,
            "size": page_size,
            "pageNum": page_no,
        }

    def _coerce_datetime(self, value: Any) -> datetime | None:
        if value is None:
            return None
        if isinstance(value, datetime):
            if value.tzinfo is not None:
                return value.astimezone().replace(tzinfo=None)
            return value
        if isinstance(value, str):
            normalized = value.replace("Z", "+00:00")
            try:
                parsed = datetime.fromisoformat(normalized)
            except ValueError:
                return None
            if parsed.tzinfo is not None:
                return parsed.astimezone().replace(tzinfo=None)
            return parsed
        return None

    def _format_datetime(self, value: datetime | None) -> str:
        return value.strftime("%Y-%m-%d %H:%M:%S") if value else ""

    def _normalize_point_value(self, data_type: str | None, value: Any) -> str | int | float | bool:
        if value is None:
            return "--"

        normalized_type = (data_type or "").upper()
        if normalized_type == "BOOL":
            if isinstance(value, str):
                return value.strip().lower() in {"1", "true", "yes", "on"}
            return bool(int(float(value)))

        if normalized_type in {"INT", "DINT", "WORD", "DWORD"}:
            return int(float(value))

        if normalized_type == "REAL":
            return float(value)

        if isinstance(value, float) and value.is_integer():
            return int(value)
        return value

    async def get_device_point_groups(self, device_code: str) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dpg.group_code,
                        dpg.group_name,
                        dpg.active_point_count,
                        dpg.max_point_count,
                        dpg.status
                    FROM device_point_group AS dpg
                    JOIN device AS d ON d.id = dpg.device_id
                    WHERE d.device_code = :device_code
                    ORDER BY dpg.group_no
                    """
                ),
                {"device_code": device_code},
            )
        ).mappings().all()

        return [
            {
                "group_code": row["group_code"],
                "group_name": row["group_name"],
                "active_point_count": row["active_point_count"],
                "max_point_count": row["max_point_count"],
                "status": row["status"],
            }
            for row in rows
        ]

    async def get_device_variables_by_group(self, device_code: str, group_code: str) -> list[dict[str, Any]]:
        rows = (
            await self.session.execute(
                text(
                    """
                    SELECT
                        dv.id AS var_id,
                        COALESCE(dv.var_name, CAST(dv.var_seq AS CHAR), '') AS var_name,
                        dv.var_type,
                        dv.scan_rate,
                        dv.is_alarm,
                        dv.is_storage,
                        dv.group_index,
                        dv.td_column_name
                    FROM device_variable AS dv
                    JOIN device AS d ON d.id = dv.device_id
                    JOIN device_point_group AS dpg ON dpg.id = dv.point_group_id
                    WHERE d.device_code = :device_code
                      AND dpg.group_code = :group_code
                      AND dv.deleted = 0
                      AND dv.enable = 1
                    ORDER BY dv.group_index ASC
                    """
                ),
                {"device_code": device_code, "group_code": group_code},
            )
        ).mappings().all()

        return [
            {
                "id": int(row["var_id"]),
                "var_name": row["var_name"] or "",
                "var_type": row["var_type"] or "",
                "scan_rate": row["scan_rate"],
                "is_alarm": row["is_alarm"],
                "is_storage": row["is_storage"],
                "group_index": row["group_index"],
                "td_column_name": row["td_column_name"] or "",
            }
            for row in rows
        ]
