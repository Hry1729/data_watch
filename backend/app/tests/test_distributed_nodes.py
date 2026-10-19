from __future__ import annotations

import pytest
from datetime import datetime, timedelta, timezone
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat

from app.core.exceptions import ApiError
from app.services.distributed_nodes import (
    DistributedNodeService,
    _assignment_schedule,
    _node_row,
    _protocol,
    _validated_public_key_pem,
)


class _NeverSession:
    async def execute(self, *_args, **_kwargs):
        raise AssertionError("invalid input must be rejected before SQL execution")


class _MappingsResult:
    def __init__(self, row):
        self._row = row

    def mappings(self):
        return self

    def first(self):
        return self._row


class _SequenceSession:
    def __init__(self, rows):
        self._rows = iter(rows)
        self.commits = 0

    async def execute(self, *_args, **_kwargs):
        return _MappingsResult(next(self._rows))

    async def commit(self):
        self.commits += 1


def test_registration_rejects_malformed_public_key() -> None:
    with pytest.raises(ApiError, match="节点公钥格式无效"):
        _validated_public_key_pem("not-a-public-key")


def test_registration_accepts_ed25519_public_key() -> None:
    pem = Ed25519PrivateKey.generate().public_key().public_bytes(
        Encoding.PEM,
        PublicFormat.SubjectPublicKeyInfo,
    ).decode("ascii")

    assert _validated_public_key_pem(pem) == pem.strip()

@pytest.mark.asyncio
async def test_registration_code_rejects_invalid_expiry() -> None:
    service = DistributedNodeService(_NeverSession())
    with pytest.raises(ApiError, match="expiresMinutes 必须是整数"):
        await service.create_registration_code(
            node_name="edge-1",
            actor="admin",
            expires_minutes="invalid",
        )


@pytest.mark.asyncio
async def test_assignment_rejects_invalid_device_id() -> None:
    service = DistributedNodeService(_NeverSession())
    with pytest.raises(ApiError, match="deviceIds 必须是整数"):
        await service.assign_devices(
            "3911959d-3e0d-45a4-880a-d3c7df3a2d65",
            ["invalid"],
        )


@pytest.mark.asyncio
async def test_heartbeat_rejects_invalid_backlog() -> None:
    service = DistributedNodeService(_NeverSession())
    with pytest.raises(ApiError, match="outboxBacklog 必须是整数"):
        await service.heartbeat(
            "3911959d-3e0d-45a4-880a-d3c7df3a2d65",
            {"outboxBacklog": "invalid"},
        )

@pytest.mark.asyncio
async def test_registration_retry_returns_existing_node_status() -> None:
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    pem = Ed25519PrivateKey.generate().public_key().public_bytes(
        Encoding.PEM,
        PublicFormat.SubjectPublicKeyInfo,
    ).decode("ascii")
    session = _SequenceSession(
        [
            {
                "node_name": "edge-1",
                "expires_at": None,
                "used_at": object(),
                "used_by_node_uuid": node_uuid,
            },
            {"status": "pending", "public_key_pem": pem},
        ]
    )

    result = await DistributedNodeService(session).register_node(
        {
            "registrationCode": "one-time-code",
            "nodeUuid": node_uuid,
            "nodeName": "edge-1",
            "publicKeyPem": pem,
        }
    )

    assert result == {"nodeUuid": node_uuid, "status": "pending"}
    assert session.commits == 0

def test_node_row_exposes_control_plane_metrics() -> None:
    now = datetime(2026, 8, 1, 12, 0, 0)
    result = _node_row(
        {
            "node_uuid": "3911959d-3e0d-45a4-880a-d3c7df3a2d65",
            "node_name": "edge-1",
            "status": "approved",
            "config_version": 8,
            "read_timeout_ms": None,
            "applied_revision": 7,
            "outbox_backlog": 12,
            "outbox_database_bytes": 2048,
            "outbox_disk_used_percent": 71.5,
            "outbox_watermark": "warning",
            "outbox_dropped_records": 3,
            "key_version": 2,
            "collector_status": "running",
            "uploader_status": "running",
            "clock_skew_ms": 31_000,
            "clock_anomaly": 1,
            "assignment_count": 4,
            "last_seen_at": now,
            "created_at": now,
            "updated_at": now,
        }
    )

    assert result["clockAnomaly"] is True
    assert result["clockSkewMs"] == 31_000
    assert result["assignmentCount"] == 4

class _ScriptedResult:
    def __init__(self, *, row=None, rows=None, scalar=None, rowcount=1):
        self._row = row
        self._rows = list(rows or [])
        self._scalar = scalar
        self.rowcount = rowcount

    def mappings(self):
        return self

    def first(self):
        return self._row

    def all(self):
        return self._rows

    def scalar_one(self):
        return self._scalar


class _ScriptedSession:
    def __init__(self, results):
        self._results = iter(results)
        self.executions = []
        self.commits = 0

    async def execute(self, statement, params=None):
        self.executions.append((str(statement), params or {}))
        return next(self._results)

    async def commit(self):
        self.commits += 1


@pytest.mark.asyncio
async def test_assignment_rejects_adding_device_to_clock_anomaly_target() -> None:
    # H2：时钟异常只拦"新增 / 迁入"。
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 1}),
            _ScriptedResult(rows=[{"device_id": 7, "assignment_epoch": 2}]),
        ]
    )

    with pytest.raises(ApiError, match="时钟异常"):
        await DistributedNodeService(session).assign_devices(
            "3911959d-3e0d-45a4-880a-d3c7df3a2d65",
            [7, 8],
        )
    assert session.commits == 0


@pytest.mark.asyncio
async def test_clock_anomaly_node_can_still_remove_devices() -> None:
    # H2：修复前任何变更（包括 unassign）都被 409 拒绝，设备会卡在时钟异常的节点上。
    # 原用例 test_assignment_rejects_clock_anomaly_target 固化的正是"空集合也拒绝"，已改为本用例。
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 1}),
            _ScriptedResult(rows=[{"device_id": 7, "assignment_epoch": 2}]),
            _ScriptedResult(),
            _ScriptedResult(),
        ]
    )

    result = await DistributedNodeService(session).assign_devices(node_uuid, [])

    assert result["removedDeviceIds"] == [7]
    assert session.commits == 1


@pytest.mark.asyncio
async def test_empty_target_set_schedules_current_assignment_retirement() -> None:
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(rows=[{"device_id": 7, "assignment_epoch": 2}]),
            _ScriptedResult(),
            _ScriptedResult(),
        ]
    )

    result = await DistributedNodeService(session).assign_devices(node_uuid, [])

    assert result["deviceIds"] == []
    assert result["removedDeviceIds"] == [7]
    assert result["cutoverAt"] is not None
    assert session.commits == 1
    retirement = next(
        params for sql, params in session.executions if "SET status='retired'" in sql
    )
    assert retirement["node_uuid"] == node_uuid
    assert retirement["grace_until"] > retirement["cutover_at"]


@pytest.mark.asyncio
async def test_existing_disabled_assignment_can_be_preserved() -> None:
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(
                rows=[
                    {"device_id": 7, "assignment_epoch": 2}
                ]
            ),
            _ScriptedResult(rows=[(7, 0)]),
            _ScriptedResult(rows=[]),
            _ScriptedResult(
                row={"node_uuid": node_uuid, "assignment_epoch": 2}
            ),
        ]
    )

    result = await DistributedNodeService(session).assign_devices(node_uuid, [7])

    assert result["deviceIds"] == [7]
    assert result["removedDeviceIds"] == []
    assert not any(
        "SET status='retired'" in sql for sql, _params in session.executions
    )
    assert session.commits == 1


@pytest.mark.asyncio
async def test_disabled_device_cannot_be_newly_assigned() -> None:
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(rows=[]),
            _ScriptedResult(rows=[(7, 0)]),
        ]
    )

    with pytest.raises(ApiError, match="包含不存在或已禁用的设备"):
        await DistributedNodeService(session).assign_devices(node_uuid, [7])


@pytest.mark.asyncio
async def test_assignment_occupancy_lists_current_owner() -> None:
    node_uuid = "7f510f16-dca6-4ed0-932f-6e8d6948a111"
    session = _ScriptedSession(
        [
            _ScriptedResult(
                rows=[
                    {"device_id": 7, "node_uuid": node_uuid, "node_name": "边缘02"}
                ]
            )
        ]
    )

    result = await DistributedNodeService(session).list_assignment_occupancy()

    assert result == [
        {"deviceId": 7, "nodeUuid": node_uuid, "nodeName": "边缘02"}
    ]


@pytest.mark.asyncio
async def test_cross_node_assignment_migrates_with_coordinated_cutover() -> None:
    # H1：原用例 test_cross_node_assignment_is_rejected 固化了"其他节点 active 即 409"，
    # 这使后面的迁移分支成为死代码，运维只能两步迁移、两边同时采集。现在其他节点上 active 的
    # 设备直接迁移：旧分配退役、新分配的 effective_at/cutover_at 与旧分配的 cutover_at 相同。
    target_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    source_uuid = "7f510f16-dca6-4ed0-932f-6e8d6948a111"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(rows=[]),
            _ScriptedResult(rows=[(7, 1)]),
            _ScriptedResult(rows=[]),
            _ScriptedResult(row={"node_uuid": source_uuid, "assignment_epoch": 1}),
            _ScriptedResult(),
            _ScriptedResult(scalar=2),
            _ScriptedResult(),
            _ScriptedResult(),
            _ScriptedResult(),
        ]
    )

    result = await DistributedNodeService(session).assign_devices(target_uuid, [7])

    assert result["migratedDeviceIds"] == [7]
    assert result["cutoverAt"] is not None
    assert session.commits == 1
    occupancy_sql = next(
        sql for sql, _params in session.executions if "device_id IN" in sql and "node_uuid<>:node_uuid" in sql
    )
    # 占用检查不再把其他节点上的 active 当作冲突（否则迁移分支永远走不到）
    assert "'active'" not in occupancy_sql
    retire = next(params for sql, params in session.executions if "SET status='retired'" in sql)
    insert = next(params for sql, params in session.executions if "INSERT INTO device_assignment" in sql)
    assert retire["node_uuid"] == source_uuid
    assert insert["node_uuid"] == target_uuid
    assert insert["effective_at"] == insert["cutover_at"] == retire["cutover_at"]
    bumped = {
        params["node_uuid"] for sql, params in session.executions if "config_version=config_version + 1" in sql
    }
    assert bumped == {source_uuid, target_uuid}
    # 新旧节点的采集窗口围绕同一切换点衔接（Ingest 按 cutover_at 严格切分）
    old_from, old_until = _assignment_schedule(
        {"cutover_at": retire["cutover_at"], "assignment_status": "retired", "has_successor": 1}
    )
    new_from, new_until = _assignment_schedule(
        {"cutover_at": insert["cutover_at"], "assignment_status": "active", "has_successor": 0}
    )
    assert old_from is None and new_until is None
    assert new_from < old_until and old_until - new_from == 2 * 30_000


@pytest.mark.asyncio
async def test_cross_node_preparing_assignment_is_still_rejected() -> None:
    target_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    source_uuid = "7f510f16-dca6-4ed0-932f-6e8d6948a111"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(rows=[]),
            _ScriptedResult(rows=[(7, 1)]),
            _ScriptedResult(rows=[{"device_id": 7, "node_uuid": source_uuid}]),
        ]
    )

    with pytest.raises(ApiError, match="准备中"):
        await DistributedNodeService(session).assign_devices(target_uuid, [7])

    assert session.commits == 0


@pytest.mark.asyncio
async def test_two_step_migration_inherits_pending_cutover() -> None:
    # H1：先在 A 上 unassign（A 退役、cutover 在将来），再在 B 上 assign。
    # 修复前 B 的新分配 cutover_at=NULL、立即采集，与 A 重叠约 60~90 秒。
    target_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    source_uuid = "7f510f16-dca6-4ed0-932f-6e8d6948a111"
    pending_cutover = datetime.now(timezone(timedelta(hours=8))).replace(tzinfo=None) + timedelta(seconds=45)
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(rows=[]),
            _ScriptedResult(rows=[(7, 1)]),
            _ScriptedResult(rows=[]),
            _ScriptedResult(row=None),
            _ScriptedResult(row={"node_uuid": source_uuid, "cutover_at": pending_cutover}),
            _ScriptedResult(scalar=3),
            _ScriptedResult(),
            _ScriptedResult(),
            _ScriptedResult(),
        ]
    )

    await DistributedNodeService(session).assign_devices(target_uuid, [7])

    insert = next(params for sql, params in session.executions if "INSERT INTO device_assignment" in sql)
    assert insert["effective_at"] == pending_cutover
    assert insert["cutover_at"] == pending_cutover
    bumped = {
        params["node_uuid"] for sql, params in session.executions if "config_version=config_version + 1" in sql
    }
    # A 的 collect_until 因出现后继而变化，也要重新下发
    assert bumped == {source_uuid, target_uuid}


@pytest.mark.asyncio
async def test_fresh_assignment_without_pending_handover_starts_immediately() -> None:
    target_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(rows=[]),
            _ScriptedResult(rows=[(7, 1)]),
            _ScriptedResult(rows=[]),
            _ScriptedResult(row=None),
            _ScriptedResult(row=None),
            _ScriptedResult(scalar=1),
            _ScriptedResult(),
            _ScriptedResult(),
        ]
    )

    await DistributedNodeService(session).assign_devices(target_uuid, [7])

    insert = next(params for sql, params in session.executions if "INSERT INTO device_assignment" in sql)
    assert insert["cutover_at"] is None


@pytest.mark.asyncio
async def test_incremental_modes_merge_with_locked_current_assignments() -> None:
    # M4：add/remove 在节点行锁（FOR UPDATE）之后基于当前分配合并，而不是由客户端读-改-写。
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    current = [{"device_id": 3, "assignment_epoch": 1}, {"device_id": 7, "assignment_epoch": 1}]
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(rows=current),
            _ScriptedResult(rows=[(3, 1)]),
            _ScriptedResult(rows=[]),
            _ScriptedResult(),
            _ScriptedResult(row={"node_uuid": node_uuid, "assignment_epoch": 1}),
            _ScriptedResult(),
        ]
    )
    result = await DistributedNodeService(session).assign_devices(node_uuid, [7], mode="remove")
    assert result["deviceIds"] == [3]
    assert result["removedDeviceIds"] == [7]

    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved", "clock_anomaly": 0}),
            _ScriptedResult(rows=current),
            _ScriptedResult(rows=[(3, 1), (7, 1), (9, 1)]),
            _ScriptedResult(rows=[]),
            _ScriptedResult(row={"node_uuid": node_uuid, "assignment_epoch": 1}),
            _ScriptedResult(row={"node_uuid": node_uuid, "assignment_epoch": 1}),
            _ScriptedResult(row=None),
            _ScriptedResult(row=None),
            _ScriptedResult(scalar=1),
            _ScriptedResult(),
            _ScriptedResult(),
        ]
    )
    result = await DistributedNodeService(session).assign_devices(node_uuid, [9], mode="add")
    assert result["deviceIds"] == [3, 7, 9]
    assert result["removedDeviceIds"] == []

def test_assignment_schedule_uses_strict_cutover_window() -> None:
    cutover = datetime(2026, 8, 1, 12, 0, 0)
    cutover_ms = int(
        cutover.replace(tzinfo=timezone(timedelta(hours=8))).timestamp() * 1000
    )

    assert _assignment_schedule(
        {"cutover_at": cutover, "assignment_status": "active", "has_successor": 0}
    ) == (cutover_ms - 30_000, None)
    assert _assignment_schedule(
        {"cutover_at": cutover, "assignment_status": "retired", "has_successor": 1}
    ) == (None, cutover_ms + 30_000)
    assert _assignment_schedule(
        {"cutover_at": cutover, "assignment_status": "retired", "has_successor": 0}
    ) == (None, cutover_ms)

@pytest.mark.asyncio
async def test_revocation_retires_assignments_in_same_transaction() -> None:
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"status": "approved"}),
            _ScriptedResult(rowcount=3),
            _ScriptedResult(),
        ]
    )

    result = await DistributedNodeService(session).set_node_status(node_uuid, "revoked")

    assert result["retiredAssignmentCount"] == 3
    assert session.commits == 1
    retirement_sql = next(sql for sql, _ in session.executions if "UPDATE device_assignment" in sql)
    assert "status IN ('active', 'preparing')" in retirement_sql


@pytest.mark.asyncio
async def test_previous_key_can_only_retry_committed_rotation() -> None:
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    pem = Ed25519PrivateKey.generate().public_key().public_bytes(
        Encoding.PEM,
        PublicFormat.SubjectPublicKeyInfo,
    ).decode("ascii")
    session = _ScriptedSession(
        [_ScriptedResult(row={"status": "approved", "public_key_pem": pem, "key_version": 2})]
    )

    result = await DistributedNodeService(session).rotate_public_key(
        node_uuid,
        pem,
        "previous",
    )

    assert result["status"] == "already_rotated"
    assert result["keyVersion"] == 2
    assert session.commits == 0

@pytest.mark.asyncio
async def test_node_read_timeout_is_optional_and_never_derived_from_scan_rate() -> None:
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession([_ScriptedResult()])

    result = await DistributedNodeService(session).update_node_settings(
        node_uuid,
        {"readTimeoutMs": None},
    )

    assert result["readTimeoutMs"] is None
    assert session.executions[0][1]["read_timeout_ms"] is None
    assert session.commits == 1

    with pytest.raises(ApiError, match="大于等于 1"):
        await DistributedNodeService(_NeverSession()).update_node_settings(
            node_uuid,
            {"readTimeoutMs": 0},
        )

@pytest.mark.asyncio
async def test_sync_device_statuses_only_bumps_approved_node_revisions() -> None:
    session = _ScriptedSession([_ScriptedResult(rowcount=2)])

    result = await DistributedNodeService(session).sync_device_statuses()

    assert result == {"updatedNodeCount": 2}
    assert "UPDATE sys_node" in session.executions[0][0]
    assert "UPDATE device_assignment" not in session.executions[0][0]
    assert session.commits == 1


@pytest.mark.asyncio
async def test_snapshot_keeps_disabled_assigned_device_with_enabled_false() -> None:
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(
                row={"config_version": 9, "read_timeout_ms": None}
            ),
            _ScriptedResult(
                rows=[
                    {
                        "device_pk": 7,
                        "device_code": "AGV801",
                        "endpoint": "127.0.0.1:102",
                        "rack": 0,
                        "slot": 1,
                        "enabled": 0,
                        "scan_cycle": 400,
                        "config_version": 3,
                        "device_type": "PLC",
                        "protocol": "s7",
                        "port": 102,
                        "opc_server": None,
                        "assignment_epoch": 2,
                        "assignment_status": "active",
                        "cutover_at": None,
                        "has_successor": 0,
                    }
                ]
            ),
            _ScriptedResult(
                rows=[
                    {
                        "device_code": "AGV801",
                        "variable_id": 11,
                        "address": "DB1.DBW0",
                        "point_name": "CraneID",
                        "data_type": "INT",
                        "scan_rate": None,
                        "device_scan_cycle": 400,
                        "is_storage": 1,
                        "var_seq": 1,
                        "group_code": "g001",
                        "group_config_version": 2,
                    }
                ]
            ),
        ]
    )

    result = await DistributedNodeService(session).config_snapshot(node_uuid, None)

    assert result["devices"][0]["device_code"] == "AGV801"
    assert result["devices"][0]["enabled"] is False
    assert result["devices"][0]["variables"][0]["variable_id"] == "11"
    assert result["devices"][0]["variables"][0]["scan_rate_ms"] == 400
    snapshot_sql = "\n".join(sql for sql, _params in session.executions[1:])
    assert "d.enable AS enabled" in snapshot_sql
    assert "d.enable=1" not in snapshot_sql


@pytest.mark.asyncio
async def test_snapshot_carries_point_name_without_changing_ids() -> None:
    """中心快照必须带上点位名称；variable_id / var_seq / IO 地址都不变。"""
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"config_version": 9, "read_timeout_ms": None}),
            _ScriptedResult(
                rows=[
                    {
                        "device_pk": 7,
                        "device_code": "AGV801",
                        "endpoint": "127.0.0.1:102",
                        "rack": 0,
                        "slot": 1,
                        "enabled": 1,
                        "scan_cycle": 400,
                        "config_version": 3,
                        "device_type": "AGV",
                        "protocol": "opcda_center",
                        "port": 102,
                        "opc_server": "OPC.SERVER",
                        "assignment_epoch": 2,
                        "assignment_status": "active",
                        "cutover_at": None,
                        "has_successor": 0,
                    }
                ]
            ),
            _ScriptedResult(
                rows=[
                    {
                        "device_code": "AGV801",
                        "variable_id": 52000,
                        "address": "OPD_Server.AGV801.Fault.Tag30007101",
                        "point_name": "FLT1001",
                        "data_type": "Boolean",
                        "scan_rate": None,
                        "device_scan_cycle": 400,
                        "is_storage": 1,
                        "var_seq": 306,
                        "group_code": "g001",
                        "group_config_version": 2,
                    }
                ]
            ),
        ]
    )

    result = await DistributedNodeService(session).config_snapshot(node_uuid, None)

    variable = result["devices"][0]["variables"][0]
    assert variable["point_name"] == "FLT1001"
    # 内部主键、点位序号、IO 地址都不因名称下发而改变
    assert variable["variable_id"] == "52000"
    assert variable["var_seq"] == 306
    assert variable["address"] == "OPD_Server.AGV801.Fault.Tag30007101"
    snapshot_sql = "\n".join(sql for sql, _params in session.executions[1:])
    assert "v.var_name AS point_name" in snapshot_sql
    assert "v.id AS variable_id" in snapshot_sql


@pytest.mark.asyncio
async def test_snapshot_keeps_blank_point_name_when_center_has_none() -> None:
    """中心没有 var_name 时下发空名称，边缘据此回退为「点位 {var_seq}」。"""
    node_uuid = "3911959d-3e0d-45a4-880a-d3c7df3a2d65"
    session = _ScriptedSession(
        [
            _ScriptedResult(row={"config_version": 9, "read_timeout_ms": None}),
            _ScriptedResult(
                rows=[
                    {
                        "device_pk": 7,
                        "device_code": "QC100",
                        "endpoint": "127.0.0.1:102",
                        "rack": 0,
                        "slot": 1,
                        "enabled": 1,
                        "scan_cycle": 400,
                        "config_version": 3,
                        "device_type": "QC",
                        "protocol": "s7",
                        "port": 102,
                        "opc_server": None,
                        "assignment_epoch": 2,
                        "assignment_status": "active",
                        "cutover_at": None,
                        "has_successor": 0,
                    }
                ]
            ),
            _ScriptedResult(
                rows=[
                    {
                        "device_code": "QC100",
                        "variable_id": 11,
                        "address": "%DB1702.DBW0",
                        "point_name": None,
                        "data_type": "INT",
                        "scan_rate": None,
                        "device_scan_cycle": 400,
                        "is_storage": 1,
                        "var_seq": 1,
                        "group_code": "g001",
                        "group_config_version": 2,
                    }
                ]
            ),
        ]
    )

    result = await DistributedNodeService(session).config_snapshot(node_uuid, None)

    assert result["devices"][0]["variables"][0]["point_name"] == ""


def test_distributed_snapshot_normalizes_opc_da_protocol_aliases() -> None:
    assert _protocol("opc-da-center") == "opcda_center"
    assert _protocol("opc da sidecar") == "opcda_sidecar"
    assert _protocol("GE SRTP") == "ge_srtp"
    assert _protocol("ge-srtp") == "ge_srtp"
