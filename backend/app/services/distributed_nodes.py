from __future__ import annotations

import hashlib
import json
import secrets
from datetime import datetime, timedelta, timezone
from typing import Any
from urllib.parse import urlparse
from uuid import UUID

from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey
from cryptography.hazmat.primitives.serialization import load_pem_public_key
from sqlalchemy import bindparam, text
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.exceptions import ApiError


_CENTER_TIMEZONE = timezone(timedelta(hours=8))
_MAX_CLOCK_SKEW = timedelta(seconds=30)
_CUTOVER_LEAD = _MAX_CLOCK_SKEW * 2
_CUTOVER_GRACE = timedelta(minutes=5)
_KEY_ROTATION_GRACE = timedelta(minutes=5)

class DistributedNodeService:
    def __init__(self, session: AsyncSession) -> None:
        self.session = session

    async def create_registration_code(
        self,
        *,
        node_name: str,
        actor: str,
        expires_minutes: Any = 30,
    ) -> dict[str, Any]:
        name = node_name.strip()
        if not name:
            raise ApiError(400, "节点名称不能为空")
        expires_minutes = _integer_field(
            expires_minutes,
            "expiresMinutes",
            minimum=1,
        )
        if expires_minutes < 1 or expires_minutes > 1440:
            raise ApiError(400, "注册码有效期必须在 1 到 1440 分钟之间")
        code = secrets.token_urlsafe(24)
        code_hash = _sha256(code)
        expires_at = datetime.now() + timedelta(minutes=expires_minutes)
        await self.session.execute(
            text(
                """
                INSERT INTO node_registration_code(
                    code_hash, node_name, expires_at, created_by
                ) VALUES(:code_hash, :node_name, :expires_at, :created_by)
                """
            ),
            {
                "code_hash": code_hash,
                "node_name": name,
                "expires_at": expires_at,
                "created_by": actor[:64],
            },
        )
        await self.session.commit()
        return {
            "registrationCode": code,
            "nodeName": name,
            "expiresAt": expires_at.isoformat(),
        }

    async def register_node(self, payload: dict[str, Any]) -> dict[str, Any]:
        code = str(payload.get("registrationCode") or "").strip()
        node_uuid = _uuid(payload.get("nodeUuid"))
        public_key = _validated_public_key_pem(payload.get("publicKeyPem"))
        requested_name = str(payload.get("nodeName") or "").strip()
        if not code:
            raise ApiError(400, "注册码和节点公钥不能为空")
        result = await self.session.execute(
            text(
                """
                SELECT node_name, expires_at, used_at, used_by_node_uuid
                FROM node_registration_code
                WHERE code_hash=:code_hash
                FOR UPDATE
                """
            ),
            {"code_hash": _sha256(code)},
        )
        row = result.mappings().first()
        if row is None:
            raise ApiError(400, "注册码无效或已使用")
        if row["used_at"] is not None:
            if str(row["used_by_node_uuid"] or "") != node_uuid:
                raise ApiError(400, "注册码无效或已使用")
            existing = await self.session.execute(
                text(
                    """
                    SELECT status, public_key_pem FROM sys_node
                    WHERE node_uuid=:node_uuid
                    """
                ),
                {"node_uuid": node_uuid},
            )
            existing_row = existing.mappings().first()
            if (
                existing_row is None
                or str(existing_row["public_key_pem"] or "").strip() != public_key
            ):
                raise ApiError(409, "节点身份与已提交注册不一致")
            return {"nodeUuid": node_uuid, "status": str(existing_row["status"])}
        if row["expires_at"] < datetime.now():
            raise ApiError(400, "注册码已过期")
        node_name = requested_name or str(row["node_name"])
        try:
            await self.session.execute(
                text(
                    """
                    INSERT INTO sys_node(
                        node_uuid, node_name, status, public_key_pem
                    ) VALUES(:node_uuid, :node_name, 'pending', :public_key)
                    """
                ),
                {
                    "node_uuid": node_uuid,
                    "node_name": node_name[:128],
                    "public_key": public_key,
                },
            )
            await self.session.execute(
                text(
                    """
                    UPDATE node_registration_code
                    SET used_at=CURRENT_TIMESTAMP(3), used_by_node_uuid=:node_uuid
                    WHERE code_hash=:code_hash AND used_at IS NULL
                    """
                ),
                {"node_uuid": node_uuid, "code_hash": _sha256(code)},
            )
            await self.session.commit()
        except Exception:
            await self.session.rollback()
            raise
        return {"nodeUuid": node_uuid, "status": "pending"}

    async def list_nodes(self) -> list[dict[str, Any]]:
        result = await self.session.execute(
            text(
                """
                SELECT node_uuid, node_name, status, config_version,
                       read_timeout_ms, applied_revision, outbox_backlog,
                       outbox_database_bytes, outbox_disk_used_percent,
                       outbox_watermark, outbox_dropped_records, key_version,
                       collector_status, uploader_status, last_seen_at,
                       clock_skew_ms, clock_anomaly,
                       (SELECT COUNT(*) FROM device_assignment AS da
                        WHERE da.node_uuid=sys_node.node_uuid
                          AND da.status='active') AS assignment_count,
                       created_at, updated_at
                FROM sys_node
                ORDER BY created_at DESC
                """
            )
        )
        return [_node_row(row) for row in result.mappings().all()]

    async def list_assignments(self, node_uuid: str) -> list[dict[str, Any]]:
        result = await self.session.execute(
            text(
                """
                SELECT da.device_id, d.device_code, d.device_name,
                       da.assignment_epoch, da.status, da.effective_at,
                       da.cutover_at, da.grace_until
                FROM device_assignment AS da
                JOIN device AS d ON d.id=da.device_id
                WHERE da.node_uuid=:node_uuid
                  AND da.status IN ('active', 'preparing')
                ORDER BY d.device_code
                """
            ),
            {"node_uuid": _uuid(node_uuid)},
        )
        return [
            {
                "deviceId": int(row["device_id"]),
                "deviceCode": str(row["device_code"]),
                "deviceName": str(row["device_name"] or ""),
                "assignmentEpoch": int(row["assignment_epoch"]),
                "status": str(row["status"]),
                "effectiveAt": _iso(row["effective_at"]),
                "cutoverAt": _iso(row["cutover_at"]),
                "graceUntil": _iso(row["grace_until"]),
            }
            for row in result.mappings().all()
        ]

    async def list_assignment_occupancy(self) -> list[dict[str, Any]]:
        result = await self.session.execute(
            text(
                """
                SELECT da.device_id, da.node_uuid, n.node_name
                FROM device_assignment AS da
                JOIN sys_node AS n ON n.node_uuid=da.node_uuid
                WHERE da.status IN ('active', 'preparing')
                ORDER BY da.device_id
                """
            )
        )
        return [
            {
                "deviceId": int(row["device_id"]),
                "nodeUuid": str(row["node_uuid"]),
                "nodeName": str(row["node_name"] or ""),
            }
            for row in result.mappings().all()
        ]

    async def sync_device_statuses(self) -> dict[str, int]:
        result = await self.session.execute(
            text(
                """
                UPDATE sys_node
                SET config_version=config_version + 1
                WHERE status='approved'
                """
            )
        )
        await self.session.commit()
        return {"updatedNodeCount": int(result.rowcount or 0)}

    async def set_node_status(self, node_uuid: str, status: str) -> dict[str, Any]:
        node_uuid = _uuid(node_uuid)
        if status not in {"approved", "revoked"}:
            raise ApiError(400, "节点状态只允许 approved 或 revoked")
        node = await self.session.execute(
            text("SELECT status FROM sys_node WHERE node_uuid=:node_uuid FOR UPDATE"),
            {"node_uuid": node_uuid},
        )
        if node.mappings().first() is None:
            await self.session.rollback()
            raise ApiError(404, "节点不存在")

        retired_count = 0
        if status == "revoked":
            now = datetime.now(_CENTER_TIMEZONE).replace(tzinfo=None)
            retired = await self.session.execute(
                text(
                    """
                    UPDATE device_assignment
                    SET status='retired', cutover_at=:now, grace_until=:now
                    WHERE node_uuid=:node_uuid AND status IN ('active', 'preparing')
                    """
                ),
                {"node_uuid": node_uuid, "now": now},
            )
            retired_count = int(retired.rowcount or 0)
            await self.session.execute(
                text(
                    """
                    UPDATE sys_node
                    SET status='revoked', config_version=config_version + 1,
                        previous_public_key_pem=NULL, previous_key_expires_at=NULL
                    WHERE node_uuid=:node_uuid
                    """
                ),
                {"node_uuid": node_uuid},
            )
        else:
            await self.session.execute(
                text("UPDATE sys_node SET status='approved' WHERE node_uuid=:node_uuid"),
                {"node_uuid": node_uuid},
            )
        await self.session.commit()
        return {
            "nodeUuid": node_uuid,
            "status": status,
            "retiredAssignmentCount": retired_count,
        }

    async def update_node_settings(
        self,
        node_uuid: str,
        payload: dict[str, Any],
    ) -> dict[str, Any]:
        node_uuid = _uuid(node_uuid)
        if "readTimeoutMs" not in payload:
            raise ApiError(400, "缺少 readTimeoutMs")
        raw_timeout = payload.get("readTimeoutMs")
        read_timeout_ms = (
            None
            if raw_timeout is None or raw_timeout == ""
            else _integer_field(raw_timeout, "readTimeoutMs", minimum=1)
        )
        result = await self.session.execute(
            text(
                """
                UPDATE sys_node
                SET read_timeout_ms=:read_timeout_ms,
                    config_version=config_version + 1
                WHERE node_uuid=:node_uuid
                """
            ),
            {"node_uuid": node_uuid, "read_timeout_ms": read_timeout_ms},
        )
        if result.rowcount != 1:
            await self.session.rollback()
            raise ApiError(404, "节点不存在")
        await self.session.commit()
        return {
            "nodeUuid": node_uuid,
            "readTimeoutMs": read_timeout_ms,
        }

    async def assign_devices(
        self,
        node_uuid: str,
        device_ids: list[int],
    ) -> dict[str, Any]:
        node_uuid = _uuid(node_uuid)
        unique_ids = sorted(
            {
                _integer_field(value, "deviceIds", minimum=1)
                for value in device_ids
            }
        )
        node = await self.session.execute(
            text(
                "SELECT status, clock_anomaly FROM sys_node "
                "WHERE node_uuid=:node_uuid FOR UPDATE"
            ),
            {"node_uuid": node_uuid},
        )
        node_row = node.mappings().first()
        if node_row is None:
            raise ApiError(404, "节点不存在")
        if node_row["status"] != "approved":
            raise ApiError(409, "仅已批准节点可分配设备")
        if bool(node_row.get("clock_anomaly")):
            raise ApiError(409, "目标节点时钟异常，禁止分配或迁移设备")

        now = datetime.now(_CENTER_TIMEZONE).replace(tzinfo=None)
        cutover_at = now + _CUTOVER_LEAD
        grace_until = cutover_at + _CUTOVER_GRACE
        changed_nodes: set[str] = set()
        migrated_ids: list[int] = []
        removed_ids: list[int] = []

        target_assignments = await self.session.execute(
            text(
                """
                SELECT device_id, assignment_epoch
                FROM device_assignment
                WHERE node_uuid=:node_uuid AND status='active'
                FOR UPDATE
                """
            ),
            {"node_uuid": node_uuid},
        )
        target_rows = target_assignments.mappings().all()
        target_ids = {int(row["device_id"]) for row in target_rows}

        if unique_ids:
            statement = text(
                """
                SELECT id, enable FROM device
                WHERE id IN :device_ids
                FOR UPDATE
                """
            ).bindparams(bindparam("device_ids", expanding=True))
            existing = await self.session.execute(statement, {"device_ids": unique_ids})
            found_devices = {int(row[0]): bool(row[1]) for row in existing.all()}
            if set(found_devices) != set(unique_ids):
                raise ApiError(400, "包含不存在或已禁用的设备")
            if any(
                not found_devices[device_id] and device_id not in target_ids
                for device_id in unique_ids
            ):
                raise ApiError(400, "包含不存在或已禁用的设备")

            occupancy_statement = text(
                """
                SELECT device_id, node_uuid
                FROM device_assignment
                WHERE device_id IN :device_ids
                  AND node_uuid<>:node_uuid
                  AND status IN ('active', 'preparing')
                FOR UPDATE
                """
            ).bindparams(bindparam("device_ids", expanding=True))
            occupied = await self.session.execute(
                occupancy_statement,
                {"device_ids": unique_ids, "node_uuid": node_uuid},
            )
            if occupied.mappings().all():
                raise ApiError(409, "设备已分配给其他边缘节点")

        for row in target_rows:
            device_id = int(row["device_id"])
            if device_id in unique_ids:
                continue
            await self.session.execute(
                text(
                    """
                    UPDATE device_assignment
                    SET status='retired', cutover_at=:cutover_at,
                        grace_until=:grace_until
                    WHERE device_id=:device_id
                      AND assignment_epoch=:assignment_epoch
                      AND node_uuid=:node_uuid AND status='active'
                    """
                ),
                {
                    "device_id": device_id,
                    "assignment_epoch": int(row["assignment_epoch"]),
                    "node_uuid": node_uuid,
                    "cutover_at": cutover_at,
                    "grace_until": grace_until,
                },
            )
            changed_nodes.add(node_uuid)
            removed_ids.append(device_id)

        for device_id in unique_ids:
            active = await self.session.execute(
                text(
                    """
                    SELECT node_uuid, assignment_epoch FROM device_assignment
                    WHERE device_id=:device_id AND status='active'
                    LIMIT 1 FOR UPDATE
                    """
                ),
                {"device_id": device_id},
            )
            current = active.mappings().first()
            if current is not None and str(current["node_uuid"]) == node_uuid:
                continue

            source_node_uuid = str(current["node_uuid"]) if current is not None else None
            if current is not None:
                await self.session.execute(
                    text(
                        """
                        UPDATE device_assignment
                        SET status='retired', cutover_at=:cutover_at,
                            grace_until=:grace_until
                        WHERE device_id=:device_id
                          AND assignment_epoch=:assignment_epoch
                          AND node_uuid=:node_uuid AND status='active'
                        """
                    ),
                    {
                        "device_id": device_id,
                        "assignment_epoch": int(current["assignment_epoch"]),
                        "node_uuid": source_node_uuid,
                        "cutover_at": cutover_at,
                        "grace_until": grace_until,
                    },
                )
                changed_nodes.add(source_node_uuid)
                migrated_ids.append(device_id)

            epoch_result = await self.session.execute(
                text(
                    """
                    SELECT COALESCE(MAX(assignment_epoch), 0) + 1
                    FROM device_assignment WHERE device_id=:device_id
                    """
                ),
                {"device_id": device_id},
            )
            epoch = int(epoch_result.scalar_one())
            await self.session.execute(
                text(
                    """
                    INSERT INTO device_assignment(
                        device_id, node_uuid, assignment_epoch,
                        effective_at, cutover_at, status
                    ) VALUES(
                        :device_id, :node_uuid, :epoch,
                        :effective_at, :cutover_at, 'active'
                    )
                    """
                ),
                {
                    "device_id": device_id,
                    "node_uuid": node_uuid,
                    "epoch": epoch,
                    "effective_at": cutover_at if source_node_uuid else now,
                    "cutover_at": cutover_at if source_node_uuid else None,
                },
            )
            changed_nodes.add(node_uuid)

        for changed_node_uuid in sorted(changed_nodes):
            await self.session.execute(
                text(
                    """
                    UPDATE sys_node
                    SET config_version=config_version + 1
                    WHERE node_uuid=:node_uuid
                    """
                ),
                {"node_uuid": changed_node_uuid},
            )
        await self.session.commit()
        return {
            "nodeUuid": node_uuid,
            "deviceIds": unique_ids,
            "migratedDeviceIds": migrated_ids,
            "removedDeviceIds": removed_ids,
            "cutoverAt": _iso(cutover_at) if migrated_ids or removed_ids else None,
        }

    async def heartbeat(
        self,
        node_uuid: str,
        payload: dict[str, Any],
    ) -> dict[str, Any]:
        node_uuid = _uuid(node_uuid)
        center_time_ms = int(datetime.now().timestamp() * 1000)
        edge_time_ms = _integer_field(
            payload.get("edgeTimeMs"),
            "edgeTimeMs",
            minimum=0,
            default=0,
        )
        clock_skew_ms = edge_time_ms - center_time_ms if edge_time_ms else 0
        result = await self.session.execute(
            text(
                """
                UPDATE sys_node SET
                    last_seen_at=CURRENT_TIMESTAMP(3),
                    applied_revision=:applied_revision,
                    outbox_backlog=:outbox_backlog,
                    outbox_database_bytes=:outbox_database_bytes,
                    outbox_disk_used_percent=:outbox_disk_used_percent,
                    outbox_watermark=:outbox_watermark,
                    outbox_dropped_records=:outbox_dropped_records,
                    collector_status=:collector_status,
                    uploader_status=:uploader_status,
                    edge_time_ms=:edge_time_ms,
                    clock_skew_ms=:clock_skew_ms,
                    clock_anomaly=:clock_anomaly,
                    last_error=:last_error
                WHERE node_uuid=:node_uuid AND status='approved'
                """
            ),
            {
                "node_uuid": node_uuid,
                "applied_revision": _integer_field(
                    payload.get("appliedRevision"),
                    "appliedRevision",
                    minimum=0,
                    default=0,
                ),
                "outbox_backlog": _integer_field(
                    payload.get("outboxBacklog"),
                    "outboxBacklog",
                    minimum=0,
                    default=0,
                ),
                "outbox_database_bytes": _integer_field(
                    payload.get("outboxDatabaseBytes"),
                    "outboxDatabaseBytes",
                    minimum=0,
                    default=0,
                ),
                "outbox_disk_used_percent": _percentage_field(
                    payload.get("outboxDiskUsedPercent"),
                    "outboxDiskUsedPercent",
                ),
                "outbox_watermark": _watermark(payload.get("outboxWatermark")),
                "outbox_dropped_records": _integer_field(
                    payload.get("outboxDroppedRecords"),
                    "outboxDroppedRecords",
                    minimum=0,
                    default=0,
                ),
                "collector_status": str(payload.get("collectorStatus") or "unknown")[:16],
                "uploader_status": str(payload.get("uploaderStatus") or "unknown")[:16],
                "edge_time_ms": edge_time_ms,
                "clock_skew_ms": clock_skew_ms,
                "clock_anomaly": int(abs(clock_skew_ms) > 30_000),
                "last_error": str(payload.get("lastError") or "")[:1000] or None,
            },
        )
        if result.rowcount != 1:
            await self.session.rollback()
            raise ApiError(403, "节点未批准或已吊销")
        revision = await self.session.execute(
            text("SELECT config_version FROM sys_node WHERE node_uuid=:node_uuid"),
            {"node_uuid": node_uuid},
        )
        await self.session.commit()
        return {
            "desiredRevision": int(revision.scalar_one()),
            "centerTimeMs": center_time_ms,
        }

    async def config_snapshot(
        self,
        node_uuid: str,
        current_revision: int | None,
    ) -> dict[str, Any]:
        node_uuid = _uuid(node_uuid)
        node_result = await self.session.execute(
            text(
                """
                SELECT config_version, read_timeout_ms
                FROM sys_node
                WHERE node_uuid=:node_uuid AND status='approved'
                """
            ),
            {"node_uuid": node_uuid},
        )
        node = node_result.mappings().first()
        if node is None:
            raise ApiError(403, "节点未批准或已吊销")
        revision = int(node["config_version"])
        if current_revision is not None and int(current_revision) == revision:
            return {"changed": False, "configRevision": revision}

        assignment_filter = """
            da.node_uuid=:node_uuid
            AND (
                da.status='active'
                OR (
                    da.status='retired'
                    AND (da.grace_until IS NULL OR da.grace_until >= CURRENT_TIMESTAMP(3))
                )
            )
            AND NOT EXISTS (
                SELECT 1 FROM device_assignment AS same_node_newer
                WHERE same_node_newer.device_id=da.device_id
                  AND same_node_newer.node_uuid=da.node_uuid
                  AND same_node_newer.assignment_epoch > da.assignment_epoch
                  AND same_node_newer.status IN ('active', 'retired')
            )
        """
        devices_result = await self.session.execute(
            text(
                f"""
                SELECT d.id AS device_pk, d.device_code, d.endpoint, d.rack, d.slot,
                       d.enable AS enabled,
                       d.scan_cycle, d.variable_config_version AS config_version,
                       dt.type_code AS device_type, cp.protocol, cp.port,
                       cp.server AS opc_server, da.assignment_epoch,
                       da.status AS assignment_status, da.cutover_at,
                       EXISTS(
                           SELECT 1 FROM device_assignment AS successor
                           WHERE successor.device_id=da.device_id
                             AND successor.assignment_epoch > da.assignment_epoch
                             AND successor.status='active'
                       ) AS has_successor
                FROM device_assignment AS da
                JOIN device AS d ON d.id=da.device_id
                LEFT JOIN device_type AS dt ON dt.id=d.type_id
                LEFT JOIN comm_protocol AS cp ON cp.id=d.protocol_id
                WHERE {assignment_filter}
                ORDER BY d.device_code
                """
            ),
            {"node_uuid": node_uuid},
        )
        device_rows = devices_result.mappings().all()
        variables_result = await self.session.execute(
            text(
                f"""
                SELECT d.device_code, v.id AS variable_id, v.var_address AS address,
                       v.var_type AS data_type, v.scan_rate, v.is_storage,
                       d.scan_cycle AS device_scan_cycle,
                       v.var_seq, g.group_code,
                       g.config_version AS group_config_version
                FROM device_assignment AS da
                JOIN device AS d ON d.id=da.device_id
                JOIN device_variable AS v
                  ON v.device_id=d.id AND v.enable=1 AND v.deleted=0
                LEFT JOIN device_point_group AS g ON g.id=v.point_group_id
                WHERE {assignment_filter}
                ORDER BY d.device_code, v.id
                """
            ),
            {"node_uuid": node_uuid},
        )
        variables: dict[str, list[dict[str, Any]]] = {}
        for row in variables_result.mappings().all():
            variables.setdefault(str(row["device_code"]), []).append(
                {
                    "variable_id": str(row["variable_id"]),
                    "address": str(row["address"]),
                    "data_type": str(row["data_type"] or "INT").upper(),
                    "scan_rate_ms": _scan_rate_ms(
                        row["scan_rate"],
                        fallback=row["device_scan_cycle"],
                    ),
                    "is_storage": bool(row["is_storage"]),
                    "var_seq": int(row["var_seq"] or 0),
                    "group_code": str(row["group_code"] or "g001"),
                    "group_config_version": int(row["group_config_version"] or 1),
                }
            )

        devices = []
        for row in device_rows:
            protocol = _protocol(row["protocol"])
            endpoint = str(row["endpoint"] or "")
            host, port = _endpoint(endpoint, int(row["port"] or _default_port(protocol)))
            collect_from_ms, collect_until_ms = _assignment_schedule(row)
            devices.append(
                {
                    "device_code": str(row["device_code"]),
                    "device_pk": int(row["device_pk"]),
                    "device_type": str(row["device_type"] or "UNKNOWN"),
                    "protocol": protocol,
                    "endpoint": endpoint,
                    "ip": host,
                    "port": port,
                    "rack": int(row["rack"] or 0),
                    "slot": int(row["slot"]) if row["slot"] is not None else 1,
                    "scan_rate_ms": _scan_rate_ms(row["scan_cycle"]),
                    "opc_server": row["opc_server"],
                    "config_version": int(row["config_version"] or 1),
                    "assignment_epoch": int(row["assignment_epoch"]),
                    "assignment_status": str(row["assignment_status"]),
                    "collect_from_ms": collect_from_ms,
                    "collect_until_ms": collect_until_ms,
                    "enabled": bool(row["enabled"]),
                    "variables": variables.get(str(row["device_code"]), []),
                }
            )
        checksum = hashlib.sha256(
            json.dumps(
                devices,
                ensure_ascii=True,
                sort_keys=True,
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest()
        return {
            "changed": True,
            "configRevision": revision,
            "checksum": checksum,
            "readTimeoutMs": (
                int(node["read_timeout_ms"])
                if node["read_timeout_ms"] is not None
                else None
            ),
            "devices": devices,
        }

    async def rotate_public_key(
        self,
        node_uuid: str,
        new_public_key_pem: Any,
        authenticated_key: str,
    ) -> dict[str, Any]:
        node_uuid = _uuid(node_uuid)
        new_public_key = _validated_public_key_pem(new_public_key_pem)
        result = await self.session.execute(
            text(
                """
                SELECT status, public_key_pem, key_version
                FROM sys_node WHERE node_uuid=:node_uuid FOR UPDATE
                """
            ),
            {"node_uuid": node_uuid},
        )
        node = result.mappings().first()
        if node is None or str(node["status"]) != "approved":
            raise ApiError(403, "节点未批准或已吊销")
        current = str(node["public_key_pem"] or "").strip()
        key_version = int(node["key_version"] or 1)
        if current == new_public_key:
            return {
                "nodeUuid": node_uuid,
                "keyVersion": key_version,
                "status": "already_rotated",
            }
        if authenticated_key != "current":
            raise ApiError(409, "旧密钥只允许重试已提交的轮换")

        expires_at = datetime.now(_CENTER_TIMEZONE).replace(tzinfo=None) + _KEY_ROTATION_GRACE
        await self.session.execute(
            text(
                """
                UPDATE sys_node
                SET previous_public_key_pem=public_key_pem,
                    previous_key_expires_at=:expires_at,
                    public_key_pem=:new_public_key,
                    key_version=key_version + 1
                WHERE node_uuid=:node_uuid AND status='approved'
                """
            ),
            {
                "node_uuid": node_uuid,
                "new_public_key": new_public_key,
                "expires_at": expires_at,
            },
        )
        await self.session.commit()
        return {
            "nodeUuid": node_uuid,
            "keyVersion": key_version + 1,
            "status": "rotated",
            "previousKeyExpiresAt": _iso(expires_at),
        }

    async def public_keys_for_approved_node(self, node_uuid: str) -> dict[str, str]:
        result = await self.session.execute(
            text(
                """
                SELECT public_key_pem, previous_public_key_pem,
                       previous_key_expires_at
                FROM sys_node
                WHERE node_uuid=:node_uuid AND status='approved'
                """
            ),
            {"node_uuid": _uuid(node_uuid)},
        )
        row = result.mappings().first()
        if row is None:
            return {}
        keys = {"current": str(row["public_key_pem"] or "").strip()}
        previous = str(row["previous_public_key_pem"] or "").strip()
        expires_at = row["previous_key_expires_at"]
        now = datetime.now(_CENTER_TIMEZONE).replace(tzinfo=None)
        if previous and expires_at is not None and expires_at >= now:
            keys["previous"] = previous
        return {name: value for name, value in keys.items() if value}


def _uuid(value: Any) -> str:
    try:
        return str(UUID(str(value or "")))
    except ValueError as exc:
        raise ApiError(400, "nodeUuid 格式无效") from exc


def _sha256(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def _validated_public_key_pem(value: Any) -> str:
    pem = str(value or "").strip()
    if not pem:
        raise ApiError(400, "注册码和节点公钥不能为空")
    try:
        key = load_pem_public_key(pem.encode("ascii"))
    except (TypeError, ValueError, UnicodeEncodeError) as exc:
        raise ApiError(400, "节点公钥格式无效") from exc
    if not isinstance(key, Ed25519PublicKey):
        raise ApiError(400, "节点公钥必须使用 Ed25519")
    return pem


def _percentage_field(value: Any, field: str) -> float:
    if value is None or value == "":
        return 0.0
    if isinstance(value, bool):
        raise ApiError(400, f"{field} 必须是 0 到 100 的数字")
    try:
        parsed = float(value)
    except (TypeError, ValueError) as exc:
        raise ApiError(400, f"{field} 必须是 0 到 100 的数字") from exc
    if parsed < 0 or parsed > 100:
        raise ApiError(400, f"{field} 必须在 0 到 100 之间")
    return round(parsed, 2)


def _watermark(value: Any) -> str:
    parsed = str(value or "normal")
    if parsed not in {"normal", "warning", "critical", "overflow"}:
        raise ApiError(400, "outboxWatermark 无效")
    return parsed


def _integer_field(
    value: Any,
    field: str,
    *,
    minimum: int,
    default: int | None = None,
) -> int:
    if value is None or value == "":
        if default is not None:
            return default
        raise ApiError(400, f"{field} 必须是整数")
    if isinstance(value, bool):
        raise ApiError(400, f"{field} 必须是整数")
    try:
        parsed = int(value)
    except (TypeError, ValueError) as exc:
        raise ApiError(400, f"{field} 必须是整数") from exc
    if parsed < minimum:
        raise ApiError(400, f"{field} 必须大于等于 {minimum}")
    return parsed


def _node_row(row) -> dict[str, Any]:
    return {
        "nodeUuid": str(row["node_uuid"]),
        "nodeName": str(row["node_name"]),
        "status": str(row["status"]),
        "configVersion": int(row["config_version"] or 0),
        "readTimeoutMs": (
            int(row["read_timeout_ms"]) if row["read_timeout_ms"] is not None else None
        ),
        "appliedRevision": int(row["applied_revision"] or 0),
        "outboxBacklog": int(row["outbox_backlog"] or 0),
        "outboxDatabaseBytes": int(row["outbox_database_bytes"] or 0),
        "outboxDiskUsedPercent": float(row["outbox_disk_used_percent"] or 0),
        "outboxWatermark": str(row["outbox_watermark"] or "normal"),
        "outboxDroppedRecords": int(row["outbox_dropped_records"] or 0),
        "keyVersion": int(row["key_version"] or 1),
        "collectorStatus": str(row["collector_status"] or "unknown"),
        "uploaderStatus": str(row["uploader_status"] or "unknown"),
        "clockSkewMs": int(row["clock_skew_ms"] or 0),
        "clockAnomaly": bool(row["clock_anomaly"]),
        "assignmentCount": int(row["assignment_count"] or 0),
        "lastSeenAt": row["last_seen_at"].isoformat() if row["last_seen_at"] else None,
        "createdAt": row["created_at"].isoformat() if row["created_at"] else None,
        "updatedAt": row["updated_at"].isoformat() if row["updated_at"] else None,
    }


def _iso(value: Any) -> str | None:
    return value.isoformat() if value is not None else None


def _assignment_schedule(row: Any) -> tuple[int | None, int | None]:
    cutover_at = row["cutover_at"]
    if cutover_at is None:
        return None, None
    if cutover_at.tzinfo is None:
        cutover_at = cutover_at.replace(tzinfo=_CENTER_TIMEZONE)
    cutover_ms = int(cutover_at.timestamp() * 1000)
    skew_ms = int(_MAX_CLOCK_SKEW.total_seconds() * 1000)
    if str(row["assignment_status"]) == "active":
        return cutover_ms - skew_ms, None
    if str(row["assignment_status"]) == "retired":
        return None, cutover_ms + skew_ms if bool(row["has_successor"]) else cutover_ms
    return None, cutover_ms


def _scan_rate_ms(value: Any, *, fallback: Any = None) -> int:
    candidate = value
    if candidate is None or float(candidate) <= 0:
        candidate = fallback
    if candidate is None:
        return 1000
    number = float(candidate)
    if number <= 0:
        return 1000
    return int(round(number if number >= 100 else number * 1000))


def _protocol(value: Any) -> str:
    raw = str(value or "s7").strip().lower()
    aliases = {
        "snap7": "s7",
        "siemens": "s7",
        "opc": "opcua",
        "opc_ua": "opcua",
        "opc-ua": "opcua",
        "opc ua": "opcua",
        "http push": "http_push",
        "http-push": "http_push",
        "gesrtp": "ge_srtp",
        "ge-srtp": "ge_srtp",
        "ge srtp": "ge_srtp",
        "opc_da_sidecar": "opcda_sidecar",
        "opc da sidecar": "opcda_sidecar",
        "opcda-sidecar": "opcda_sidecar",
        "opc-da-sidecar": "opcda_sidecar",
        "opc_da_center": "opcda_center",
        "opc da center": "opcda_center",
        "opcda-center": "opcda_center",
        "opc-da-center": "opcda_center",
    }
    return aliases.get(raw, raw)


def _default_port(protocol: str) -> int:
    return {"ge_srtp": 18245, "modbus_tcp": 502, "opcua": 4840}.get(protocol, 102)


def _endpoint(endpoint: str, default_port: int) -> tuple[str, int]:
    text_value = endpoint.strip()
    if not text_value:
        return "127.0.0.1", default_port
    parsed = urlparse(text_value if "://" in text_value else "//" + text_value)
    return parsed.hostname or text_value, parsed.port or default_port
