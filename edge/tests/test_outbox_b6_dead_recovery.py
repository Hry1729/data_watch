"""B6 剩余项的回归：dead 载荷必须**先可恢复**（查看/导出/受控重放），再谈清理。

背景（取证）：`maintenance()` 会按 `retention_days` 直接 `DELETE ... state='dead' AND
dead_at < cutoff`，而此前**没有任何 IPC 动作**能查看或取回 dead 载荷 ⇒ 用户明确不接受的
"没有恢复入口，却按天自动删除唯一载荷"。

本回归锁定三件事：
1. 运维能列出、导出 dead 载荷（导出含完整 base64 载荷；列表**不含**载荷）；
2. 重放**幂等**（重复调用不改状态、不重复计数）、受**时间窗**限制、并**重置隔离预算**
   （reject_count/attempt_count 归零，使重放不继承历史协议拒绝计数）；
3. 审计留痕（dead_replay_count / last_dead_replayed_at）与容量视图（dead_backlog）。
"""

from __future__ import annotations

import base64
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from edge.app.outbox import OutboxStore  # noqa: E402


def _store(tmp_path) -> OutboxStore:
    return OutboxStore(tmp_path / "outbox.db")


def _seed_dead(store: OutboxStore, *, count: int = 1):
    store.configure_assignment("QC100", 7, 100, 1)
    for seq in range(count):
        store.append_envelope(
            device_code="QC100",
            device_type="qc",
            group_code="g001",
            scan_rate_ms=100,
            ts=1721800000000 + seq,
            config_version=12,
            values={"1": str(seq)},
            qualities={"1": 192},
        )
    records = store.claim_batch(
        target_seq=store.snapshot_target_seq(),
        lease_owner="U1",
        lease_ms=60_000,
        max_items=10,
        max_bytes=10**6,
    )
    assert records, "需要至少一条 inflight 记录才能标记 dead"
    for record in records:
        store.mark_dead(record, "U1", "protocol_reject")
    return records


def _states(store: OutboxStore) -> list[str]:
    with store._lock:
        rows = store._connection.execute("SELECT state FROM outbox ORDER BY local_seq").fetchall()
    return [str(row["state"]) for row in rows]


def test_list_dead_exposes_metadata_without_payload(tmp_path) -> None:
    store = _store(tmp_path)
    records = _seed_dead(store)

    listed = store.list_dead()

    assert len(listed) == 1
    entry = listed[0]
    assert entry["batch_id"] == records[0].batch_id
    assert entry["device_code"] == "QC100"
    assert entry["dead_at"] is not None
    assert entry["payload_bytes"] > 0
    assert "payload" not in entry and "payload_b64" not in entry, "列表不得回传载荷"


def test_export_dead_returns_full_payload_and_none_for_unknown(tmp_path) -> None:
    store = _store(tmp_path)
    records = _seed_dead(store)
    record = records[0]

    exported = store.export_dead(record.batch_id, record.upload_session_id, record.local_seq)

    assert exported is not None
    assert base64.b64decode(exported["payload_b64"]) == record.payload
    assert store.export_dead("nope", record.upload_session_id, record.local_seq) is None


def test_replay_dead_restores_row_and_resets_isolation_budget(tmp_path) -> None:
    store = _store(tmp_path)
    records = _seed_dead(store)
    record = records[0]
    assert store.dead_backlog()["records"] == 1

    result = store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    assert result["replayed"] is True
    assert result["state"] == "retry_wait"
    assert _states(store) == ["retry_wait"]
    assert store.dead_backlog()["records"] == 0
    with store._lock:
        row = store._connection.execute(
            "SELECT attempt_count, reject_count, next_retry_at, lease_owner "
            "FROM outbox WHERE local_seq=?",
            (record.local_seq,),
        ).fetchone()
    assert int(row["reject_count"]) == 0, "重放必须重置坏数据隔离预算"
    assert int(row["attempt_count"]) == 0
    assert row["next_retry_at"] is not None
    assert row["lease_owner"] is None


def test_replay_dead_is_idempotent(tmp_path) -> None:
    store = _store(tmp_path)
    records = _seed_dead(store)
    record = records[0]
    store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    again = store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    assert again["replayed"] is False
    assert again["reason"] == "not_dead"
    with store._lock:
        audit = store._connection.execute(
            "SELECT value FROM metadata WHERE key='dead_replay_count'"
        ).fetchone()
    assert int(audit["value"]) == 1, "幂等重放不得重复计数"


def test_replay_dead_respects_time_window(tmp_path) -> None:
    store = _store(tmp_path)
    records = _seed_dead(store)
    record = records[0]

    result = store.replay_dead(
        record.batch_id, record.upload_session_id, record.local_seq, window_ms=0
    )

    assert result["replayed"] is False
    assert result["reason"] == "outside_time_window"
    assert _states(store) == ["dead"], "超出时间窗不得改变状态（应改用导出）"


def test_replay_dead_reports_missing_identity(tmp_path) -> None:
    store = _store(tmp_path)
    _seed_dead(store)

    result = store.replay_dead("missing", "session", 1)

    assert result["replayed"] is False
    assert result["reason"] == "not_found"


def test_successful_replay_is_audited(tmp_path) -> None:
    store = _store(tmp_path)
    records = _seed_dead(store)
    record = records[0]

    store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    with store._lock:
        rows = {
            str(item["key"]): str(item["value"])
            for item in store._connection.execute(
                "SELECT key, value FROM metadata WHERE key LIKE 'dead_replay%' OR key LIKE 'last_dead%'"
            ).fetchall()
        }
    assert rows.get("dead_replay_count") == "1"
    assert rows.get("last_dead_replayed_at") is not None


def test_dead_replay_not_exposed_to_collector_or_agent() -> None:
    """权限：dead 载荷的查看/导出/重放只给 uploader 角色。"""
    from edge.app.storage_service import IPC_ROLE_ACTIONS

    for role in ("collector", "agent"):
        assert not [action for action in IPC_ROLE_ACTIONS[role] if "dead" in action]
    uploader_dead = sorted(a for a in IPC_ROLE_ACTIONS["uploader"] if "dead" in a)
    assert uploader_dead == [
        "outbox.dead_backlog",
        "outbox.dead_export",
        "outbox.dead_list",
        "outbox.dead_replay",
    ]



# ── 容量告警：必须在**载荷仍存在**时先给出 ────────────────────────────────────


def _age_dead(store: OutboxStore, *, days: int) -> None:
    """把 dead_at 改老，模拟"已超出保留窗口"（仅测试用，直接改库）。"""
    with store._lock:
        store._connection.execute(
            "UPDATE outbox SET dead_at = dead_at - ? WHERE state='dead'",
            (days * 24 * 60 * 60 * 1000,),
        )
        store._connection.commit()


def test_pending_dead_backlog_is_alerted_before_purge(tmp_path) -> None:
    """dead 仍在保留窗口内：必须**先**告警，且这次不得删除任何载荷。"""
    store = _store(tmp_path)
    _seed_dead(store)

    result = store.maintenance(retention_days=7)

    assert result["dead_records"] == 1
    assert result["dead_bytes"] > 0
    assert result["dead_purged"] == 0
    assert "dead_backlog_present_export_or_replay" in result["capacity_alerts"]
    # 载荷仍在：告警是可行动的，而不是事后通知
    assert _states(store) == ["dead"]
    assert store.dead_backlog()["records"] == 1


def test_expired_dead_purge_is_reported(tmp_path) -> None:
    store = _store(tmp_path)
    _seed_dead(store)
    _age_dead(store, days=30)

    result = store.maintenance(retention_days=7)

    assert result["dead_purged"] == 1
    assert "dead_records_purged_requires_export" in result["capacity_alerts"]
    assert _states(store) == []
    assert store.dead_backlog()["records"] == 0


def test_dead_alerts_do_not_hijack_the_degraded_flag(tmp_path) -> None:
    """容量告警与 degraded 是两件事：仅存在 dead 记录不得把整机拉成 degraded。"""
    store = _store(tmp_path)
    _seed_dead(store)

    result = store.maintenance(retention_days=7)

    assert result["capacity_alerts"], "应有容量告警"
    assert result["degraded"] is False
    assert "disk_overflow" not in result["degraded_reasons"]
