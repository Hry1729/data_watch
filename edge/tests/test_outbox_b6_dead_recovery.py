"""B6：dead 载荷先可查看、导出和重新入队，未处置的行不会被 maintenance 删掉。

保留、批量导出、幂等重放、operator 权限和 dead_audit 都在本文件锁定。
"""

from __future__ import annotations

import base64
import hashlib
import json
import os
import sqlite3
import sys
import threading
from pathlib import Path

import httpx
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


def _export_path(store: OutboxStore, name: str) -> Path:
    return store.path.parent / "dead-exports" / name


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


def test_replay_reenqueues_without_resetting_the_dead_budget(tmp_path) -> None:
    """重新入队不改原 dead 行的隔离预算，新行携带原载荷。"""
    store = _store(tmp_path)
    records = _seed_dead(store)
    record = records[0]
    assert store.dead_backlog()["records"] == 1
    with store._lock:
        store._connection.execute(
            "UPDATE outbox SET attempt_count=5, reject_count=3 WHERE local_seq=?",
            (record.local_seq,),
        )
        store._connection.commit()

    result = store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    assert result["replayed"] is True
    assert result["reason"] == "reenqueued"
    assert sorted(_states(store)) == ["dead", "pending"]
    assert store.dead_backlog()["records"] == 1
    with store._lock:
        dead = store._connection.execute(
            "SELECT attempt_count, reject_count, next_retry_at, lease_owner, state "
            "FROM outbox WHERE batch_id=?",
            (record.batch_id,),
        ).fetchone()
        copied = store._connection.execute(
            "SELECT payload, state FROM outbox WHERE batch_id=?",
            (result["replay_ref"],),
        ).fetchone()
    assert dead["state"] == "dead"
    assert int(dead["reject_count"]) == 3, "重放不得改写原 dead 行的坏数据隔离预算"
    assert int(dead["attempt_count"]) == 5
    assert dead["next_retry_at"] is None
    assert dead["lease_owner"] is None
    assert copied["state"] == "pending"
    assert bytes(copied["payload"]) == record.payload


def test_replay_dead_is_idempotent(tmp_path) -> None:
    store = _store(tmp_path)
    records = _seed_dead(store)
    record = records[0]
    first = store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    again = store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    assert first["replayed"] is True
    assert again["replayed"] is False
    assert again["reason"] == "already_replayed"
    assert again["replay_ref"] == first["replay_ref"]
    with store._lock:
        rows = store._connection.execute("SELECT batch_id, state FROM outbox").fetchall()
        legacy = store._connection.execute(
            "SELECT value FROM metadata WHERE key='dead_replay_count'"
        ).fetchone()
    assert len(rows) == 2, "连续重放只能生成一条新 outbox 行"
    assert legacy is None


def test_replay_dead_respects_time_window(tmp_path) -> None:
    store = _store(tmp_path)
    records = _seed_dead(store)
    record = records[0]
    # window_ms=0 表示 dead_at 只要早于“现在”就只能导出。同一毫秒内两者相等，
    # 这里把 dead_at 拨早 1ms，避免卡在时钟粒度上。
    with store._lock:
        store._connection.execute(
            "UPDATE outbox SET dead_at = dead_at - 1 WHERE batch_id=?",
            (record.batch_id,),
        )
        store._connection.commit()

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
    with store._lock:
        audit = store._connection.execute(
            "SELECT action, batch_id, session, local_seq, result FROM dead_audit"
        ).fetchone()
    assert audit["action"] == "replay"
    assert audit["result"] == "not_found"
    assert audit["batch_id"] == "missing"
    assert audit["session"] == "session"
    assert audit["local_seq"] == 1


def test_refused_replay_does_not_record_success(tmp_path) -> None:
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
    assert rows.get("dead_replay_count") is None
    assert rows.get("last_dead_replayed_at") is None


def test_old_session_dead_replay_reenqueues_on_the_current_session(tmp_path):
    """旧 session 的 dead 不原地改状态；新行使用当前 session，原载荷仍可导出。"""
    store = _store(tmp_path)
    record = _seed_dead(store)[0]
    store.prepare_session_switch()
    assert store.current_session_id() != record.upload_session_id
    result = store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)
    assert result["replayed"] is True
    assert result["upload_session_id"] == store.current_session_id()
    assert (result["upload_session_id"], result["local_seq"]) != (
        record.upload_session_id,
        record.local_seq,
    )
    assert "dead" in _states(store)
    exported = store.export_dead(record.batch_id, record.upload_session_id, record.local_seq)
    assert base64.b64decode(exported["payload_b64"]) == record.payload


def test_dead_recovery_actions_belong_to_operator_not_uploader() -> None:
    """operator 只拿恢复动作；uploader 保留 dead_backlog 和 maintenance。"""
    from edge.app.storage_service import IPC_ROLE_ACTIONS

    assert IPC_ROLE_ACTIONS["operator"] == frozenset(
        {
            "outbox.dead_list",
            "outbox.dead_export",
            "outbox.dead_export_batch",
            "outbox.dead_replay",
        }
    )
    for role in ("collector", "agent"):
        assert not [action for action in IPC_ROLE_ACTIONS[role] if "dead" in action]
    uploader_dead = sorted(action for action in IPC_ROLE_ACTIONS["uploader"] if "dead" in action)
    assert uploader_dead == ["outbox.dead_backlog"]
    assert "maintenance" in IPC_ROLE_ACTIONS["uploader"]
    assert "maintenance" not in IPC_ROLE_ACTIONS["operator"]



# ── 容量告警：必须在**载荷仍存在**时先给出 ────────────────────────────────────


def _age_dead(store: OutboxStore, *, days: int) -> None:
    """把 dead_at 改老，模拟"已超出保留窗口"（仅测试用，直接改库）。"""
    with store._lock:
        store._connection.execute(
            "UPDATE outbox SET dead_at = dead_at - ? WHERE state='dead'",
            (days * 24 * 60 * 60 * 1000,),
        )
        store._connection.commit()


def _pin_disk(store: OutboxStore, monkeypatch: pytest.MonkeyPatch) -> None:
    """钉住正常磁盘水位，避免宿主机告警触发自动导出。"""
    monkeypatch.setattr(
        store,
        "_disk_status",
        lambda: {
            "disk_total_bytes": 1000,
            "disk_free_bytes": 800,
            "disk_used_percent": 20.0,
            "watermark": "normal",
        },
    )


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


def test_unexported_dead_past_cutoff_is_kept_until_exported(tmp_path, monkeypatch) -> None:
    """未导出的 dead 超过保留期不删；导出之后超过保留期才删，并留下清理告警。"""
    store = _store(tmp_path)
    _pin_disk(store, monkeypatch)
    record = _seed_dead(store)[0]
    _age_dead(store, days=31)

    kept = store.maintenance(retention_days=7)

    assert kept["dead_purged"] == 0
    assert "dead_backlog_present_export_or_replay" in kept["capacity_alerts"]
    assert _states(store) == ["dead"]
    assert store.dead_backlog()["records"] == 1
    with store._lock:
        exported_at = store._connection.execute(
            "SELECT exported_at FROM outbox WHERE batch_id=?",
            (record.batch_id,),
        ).fetchone()["exported_at"]
    assert exported_at is None

    store.export_dead(record.batch_id, record.upload_session_id, record.local_seq)
    purged = store.maintenance(retention_days=7)

    assert purged["dead_purged"] == 1
    assert "dead_records_purged_requires_export" in purged["capacity_alerts"]
    assert _states(store) == []
    assert store.dead_backlog()["records"] == 0
    with store._lock:
        purge = store._connection.execute(
            "SELECT action, result, sha256 FROM dead_audit WHERE action='purge'"
        ).fetchone()
        blob = " ".join(
            "" if value is None else str(value)
            for row in store._connection.execute("SELECT * FROM dead_audit").fetchall()
            for value in tuple(row)
        )
    assert purge["result"] == "deleted"
    assert purge["sha256"] == hashlib.sha256(record.payload).hexdigest()
    assert record.payload.decode("utf-8") not in blob


def test_dead_alerts_do_not_hijack_the_degraded_flag(tmp_path) -> None:
    """容量告警与 degraded 是两件事：仅存在 dead 记录不得把整机拉成 degraded。"""
    store = _store(tmp_path)
    _seed_dead(store)

    result = store.maintenance(retention_days=7)

    assert result["capacity_alerts"], "应有容量告警"
    assert result["degraded"] is False
    assert "disk_overflow" not in result["degraded_reasons"]


def test_pressure_auto_exports_but_keeps_exported_rows_inside_retention(tmp_path, monkeypatch) -> None:
    """字节上限或磁盘告警只自动导出。窗口内已导出的行，压力还在也不删。"""
    store = _store(tmp_path)
    _pin_disk(store, monkeypatch)
    record = _seed_dead(store)[0]

    first = store.maintenance(retention_days=7, dead_max_bytes=1)

    assert first["dead_auto_exported"] == 1
    assert first["dead_purged"] == 0
    assert store.dead_backlog()["records"] == 1
    path = Path(first["dead_auto_export_path"])
    assert path.name.startswith("auto-") and path.suffix == ".jsonl"
    lines = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
    assert lines[0]["schema_version"] == 1
    body = lines[1]
    assert base64.b64decode(body["payload_b64"]) == record.payload
    assert body["payload_sha256"] == hashlib.sha256(record.payload).hexdigest()

    second = store.maintenance(retention_days=7, dead_max_bytes=1)

    assert second["dead_purged"] == 0
    assert second["dead_auto_exported"] == 0
    assert store.dead_backlog()["records"] == 1
    assert _states(store) == ["dead"]
    assert path.read_bytes()

    monkeypatch.setattr(
        store,
        "_disk_status",
        lambda: {
            "disk_total_bytes": 1000,
            "disk_free_bytes": 200,
            "disk_used_percent": 80.0,
            "watermark": "warning",
        },
    )
    under_disk_alarm = store.maintenance(retention_days=7)

    assert under_disk_alarm["dead_purged"] == 0
    assert under_disk_alarm["dead_auto_exported"] == 0
    assert store.dead_backlog()["records"] == 1
    assert _states(store) == ["dead"]


def test_jsonl_round_trip_matches_payload_and_csv_omits_it(tmp_path, monkeypatch) -> None:
    store = _store(tmp_path)
    _pin_disk(store, monkeypatch)
    record = _seed_dead(store, count=1)[0]
    with store._lock:
        store._connection.execute(
            "UPDATE outbox SET last_error=? WHERE batch_id=?",
            ("keep-me", record.batch_id),
        )
        store._connection.commit()
    store.configure_assignment("QC200", 3, 100, 1)
    store.append_envelope(
        device_code="QC200",
        device_type="qc",
        group_code="g001",
        scan_rate_ms=100,
        ts=1721800000999,
        config_version=12,
        values={"1": "PAYLOADSENTINEL9f3a"},
        qualities={"1": 192},
    )
    extra = store.claim_batch(
        target_seq=store.snapshot_target_seq(),
        lease_owner="U1",
        lease_ms=60_000,
        max_items=10,
        max_bytes=10**6,
    )
    assert extra
    store.mark_dead(extra[0], "U1", "skip-me")

    jsonl_path = _export_path(store, "dead.jsonl")
    csv_path = _export_path(store, "dead.csv")
    exported = store.export_dead_batch(
        jsonl_path,
        fmt="jsonl",
        device_code="QC100",
        last_error="keep-me",
        node_uuid="node-under-test",
    )
    assert exported["count"] == 1
    lines = [json.loads(line) for line in jsonl_path.read_text(encoding="utf-8").splitlines()]
    assert lines[0] == {
        "kind": "header",
        "schema_version": 1,
        "node_uuid": "node-under-test",
    }
    restored = base64.b64decode(lines[1]["payload_b64"])
    assert restored == record.payload
    assert hashlib.sha256(restored).hexdigest() == lines[1]["payload_sha256"]
    with store._lock:
        kept = store._connection.execute(
            "SELECT exported_at FROM outbox WHERE batch_id=?",
            (record.batch_id,),
        ).fetchone()
        skipped = store._connection.execute(
            "SELECT exported_at FROM outbox WHERE batch_id=?",
            (extra[0].batch_id,),
        ).fetchone()
    assert kept["exported_at"] is not None
    assert skipped["exported_at"] is None

    csv_result = store.export_dead_batch(
        csv_path,
        fmt="csv",
        device_code="QC200",
        node_uuid="node-under-test",
    )
    csv_text = csv_path.read_text(encoding="utf-8")
    assert csv_result["count"] == 1
    assert "schema_version=1" in csv_text
    assert "node-under-test" in csv_text
    assert "payload_b64" not in csv_text
    assert b"PAYLOADSENTINEL9f3a" not in csv_path.read_bytes()
    assert hashlib.sha256(extra[0].payload).hexdigest() in csv_text


def _require_symlink_support(tmp_path: Path) -> None:
    """符号链接在 Windows 上需要管理员权限或开发者模式。

    本机没有权限时跳过；CI 设置 ``DATAWATCH_REQUIRE_SYMLINK_TESTS=1``，此时改为直接失败，
    避免"没有权限"把这条漏洞防护用例静默跳过。
    """
    probe = tmp_path / ".symlink-probe"
    target = tmp_path / ".symlink-target"
    target.write_text("probe", encoding="utf-8")
    try:
        probe.symlink_to(target)
    except (OSError, NotImplementedError) as exc:
        if os.environ.get("DATAWATCH_REQUIRE_SYMLINK_TESTS") == "1":
            pytest.fail(f"CI 要求符号链接用例真跑，但本机无法创建符号链接: {exc}")
        pytest.skip(f"本机无法创建符号链接（需管理员或开发者模式）: {exc}")
    else:
        probe.unlink(missing_ok=True)
    finally:
        target.unlink(missing_ok=True)


def test_export_path_stays_inside_dead_exports_and_refuses_existing_files(tmp_path) -> None:
    """目标必须是 dead-exports 下的新文件：穿越、符号链接和已有文件都拒绝。"""
    store = _store(tmp_path)
    _seed_dead(store)
    outside = tmp_path / "escape.jsonl"
    with pytest.raises(ValueError, match="dead-exports"):
        store.export_dead_batch(outside, fmt="jsonl")
    with pytest.raises(ValueError, match="dead-exports"):
        store.export_dead_batch(tmp_path / "dead-exports" / ".." / "escape.jsonl", fmt="jsonl")
    assert not outside.exists()

    exported = store.export_dead_batch(_export_path(store, "once.jsonl"), fmt="jsonl")
    assert exported["count"] == 1
    with pytest.raises(ValueError, match="already exists"):
        store.export_dead_batch(_export_path(store, "once.jsonl"), fmt="jsonl")

    _require_symlink_support(tmp_path)

    secret = tmp_path / "secret.txt"
    secret.write_text("keep", encoding="utf-8")
    link = _export_path(store, "via-link.jsonl")
    link.symlink_to(secret)
    with pytest.raises(ValueError, match="symlink"):
        store.export_dead_batch(link, fmt="jsonl")
    assert secret.read_text(encoding="utf-8") == "keep"

    jumped = tmp_path / "jumped"
    jumped.mkdir()
    alias = _export_path(store, "alias")
    alias.symlink_to(jumped, target_is_directory=True)
    with pytest.raises(ValueError, match="symlink"):
        store.export_dead_batch(alias / "out.jsonl", fmt="jsonl")
    assert not (jumped / "out.jsonl").exists()


def test_export_does_not_hold_the_write_lock_across_fsync(tmp_path, monkeypatch) -> None:
    import edge.app.outbox as outbox_module

    monkeypatch.setattr(outbox_module, "_DEAD_EXPORT_PAGE_SIZE", 1)
    store = _store(tmp_path)
    _seed_dead(store, count=2)
    released = threading.Event()

    def fsync_and_probe(handle) -> None:
        handle.flush()
        os.fsync(handle.fileno())

        def other() -> None:
            with store._transaction() as connection:
                connection.execute("SELECT 1")
            released.set()

        worker = threading.Thread(target=other)
        worker.start()
        worker.join(timeout=2)
        assert released.is_set()

    monkeypatch.setattr(store, "_fsync_export_handle", fsync_and_probe)
    result = store.export_dead_batch(_export_path(store, "paged.jsonl"), fmt="jsonl")

    assert result["count"] == 2
    assert released.is_set()


def test_export_does_not_mark_a_row_whose_payload_changed(tmp_path, monkeypatch) -> None:
    store = _store(tmp_path)
    record = _seed_dead(store)[0]
    real_mark = store._mark_exported_snapshot

    def change_then_mark(identities, **kwargs):
        with store._lock:
            store._connection.execute(
                "UPDATE outbox SET payload=? WHERE batch_id=?",
                (b"changed-payload", record.batch_id),
            )
            store._connection.commit()
        return real_mark(identities, **kwargs)

    monkeypatch.setattr(store, "_mark_exported_snapshot", change_then_mark)
    result = store.export_dead_batch(_export_path(store, "stale.jsonl"), fmt="jsonl")

    assert result["count"] == 0
    with store._lock:
        exported_at = store._connection.execute(
            "SELECT exported_at, state FROM outbox WHERE batch_id=?",
            (record.batch_id,),
        ).fetchone()
    assert exported_at["exported_at"] is None
    assert exported_at["state"] == "dead"


def test_replay_audits_not_dead(tmp_path) -> None:
    store = _store(tmp_path)
    store.configure_assignment("QC100", 7, 100, 1)
    pending = store.append_envelope(
        device_code="QC100",
        device_type="qc",
        group_code="g001",
        scan_rate_ms=100,
        ts=1721800000000,
        config_version=12,
        values={"1": "alive"},
        qualities={"1": 192},
    )

    result = store.replay_dead(pending.batch_id, pending.upload_session_id, pending.local_seq)

    assert result["reason"] == "not_dead"
    with store._lock:
        audit = store._connection.execute(
            "SELECT action, result, sha256 FROM dead_audit"
        ).fetchone()
        blob = " ".join(
            "" if value is None else str(value)
            for row in store._connection.execute("SELECT * FROM dead_audit").fetchall()
            for value in tuple(row)
        )
    assert audit["action"] == "replay"
    assert audit["result"] == "not_dead"
    assert audit["sha256"] == hashlib.sha256(pending.payload).hexdigest()
    assert pending.payload.decode("utf-8") not in blob


def test_assignment_change_refuses_replay_and_keeps_the_row(tmp_path) -> None:
    store = _store(tmp_path)
    record = _seed_dead(store)[0]
    store.configure_assignment("QC100", 9, 100, 2)

    result = store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    assert result["replayed"] is False
    assert result["reason"] == "assignment_changed"
    assert _states(store) == ["dead"]
    assert store.export_dead(record.batch_id, record.upload_session_id, record.local_seq) is not None


def test_export_and_replay_write_audit_without_payload(tmp_path) -> None:
    store = _store(tmp_path)
    record = _seed_dead(store)[0]
    store.export_dead(record.batch_id, record.upload_session_id, record.local_seq)
    store.replay_dead(record.batch_id, record.upload_session_id, record.local_seq)

    with store._lock:
        columns = [str(item[1]) for item in store._connection.execute("PRAGMA table_info(dead_audit)")]
        rows = store._connection.execute(
            "SELECT actor_role, action, batch_id, result, sha256, file_path FROM dead_audit ORDER BY id"
        ).fetchall()
        blob = " ".join(
            "" if value is None else str(value)
            for row in store._connection.execute("SELECT * FROM dead_audit").fetchall()
            for value in tuple(row)
        )
    assert "payload" not in columns
    assert record.payload.decode("utf-8") not in blob
    assert base64.b64encode(record.payload).decode("ascii") not in blob
    actions = [str(row["action"]) for row in rows]
    assert actions == ["export", "replay"]
    assert all(row["sha256"] == hashlib.sha256(record.payload).hexdigest() for row in rows)
    assert all(str(row["batch_id"]) == record.batch_id for row in rows)
    assert "token" not in blob


def test_migration_preserves_existing_dead_rows_without_export_marks(tmp_path, monkeypatch) -> None:
    """旧 edge.db 自动补列；已有 dead 的 exported_at 为 NULL，所以继续保留。"""
    path = tmp_path / "edge.db"
    legacy = sqlite3.connect(path)
    legacy.execute(
        """
        CREATE TABLE outbox (
            batch_id TEXT PRIMARY KEY,
            upload_session_id TEXT NOT NULL,
            local_seq INTEGER NOT NULL,
            device_code TEXT NOT NULL,
            group_code TEXT NOT NULL,
            assignment_epoch INTEGER NOT NULL,
            config_version INTEGER NOT NULL,
            ts INTEGER NOT NULL,
            payload BLOB NOT NULL,
            state TEXT NOT NULL DEFAULT 'pending',
            attempt_count INTEGER NOT NULL DEFAULT 0,
            reject_count INTEGER NOT NULL DEFAULT 0,
            next_retry_at INTEGER,
            last_error TEXT,
            http_status INTEGER,
            lease_owner TEXT,
            inflight_at INTEGER,
            lease_until INTEGER,
            acked_at INTEGER,
            dead_at INTEGER,
            created_at INTEGER NOT NULL,
            UNIQUE (upload_session_id, local_seq)
        )
        """
    )
    legacy.execute(
        """
        INSERT INTO outbox(
            batch_id, upload_session_id, local_seq, device_code, group_code,
            assignment_epoch, config_version, ts, payload, state, dead_at, created_at
        ) VALUES('legacy-dead', 'old-session', 1, 'QC100', 'g001', 7, 12, 1, ?, 'dead', 1, 1)
        """,
        (b'{"device_code":"QC100","config_version":12,"values":{"1":"legacy"}}',),
    )
    legacy.commit()
    legacy.close()

    store = OutboxStore(path)
    _pin_disk(store, monkeypatch)
    with store._lock:
        columns = {str(item[1]) for item in store._connection.execute("PRAGMA table_info(outbox)")}
        audit = store._connection.execute(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='dead_audit'"
        ).fetchone()
        row = store._connection.execute(
            "SELECT exported_at, replayed_at, payload FROM outbox WHERE batch_id='legacy-dead'"
        ).fetchone()
    assert {"exported_at", "replayed_at", "replay_ref"} <= columns
    assert audit is not None
    assert row["exported_at"] is None
    assert row["replayed_at"] is None

    result = store.maintenance(retention_days=7, dead_retention_days=1)
    assert result["dead_purged"] == 0
    assert store.dead_backlog()["records"] == 1
    assert store.export_dead("legacy-dead", "old-session", 1)["payload_b64"]


def test_operator_and_uploader_permissions_are_split(tmp_path, monkeypatch) -> None:
    """uploader、collector、agent 调 operator 动作返回 403；operator 调 maintenance 也返回 403。"""
    from edge.app.storage_client import IPC_TOKEN_HEADER, read_role_tokens
    from edge.app.storage_service import (
        IpcAuthenticator,
        StorageEngine,
        StorageHttpServer,
        ensure_token_file,
    )

    database = tmp_path / "edge.db"
    token_file = tmp_path / "ipc" / "storage-ipc-tokens.json"
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(token_file))
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_TOKEN", raising=False)
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_ROLE", raising=False)
    engine = StorageEngine(database, max_queue=32)
    engine.start()
    server = StorageHttpServer(("127.0.0.1", 0), engine, IpcAuthenticator.from_file(token_file))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    tokens = read_role_tokens(token_file)
    client = httpx.Client(base_url=f"http://127.0.0.1:{server.server_port}", timeout=10.0)
    operator_actions = (
        "outbox.dead_list",
        "outbox.dead_export",
        "outbox.dead_export_batch",
        "outbox.dead_replay",
    )
    try:
        for role in ("uploader", "collector", "agent"):
            for action in operator_actions:
                response = client.post(
                    "/v1/command",
                    json={"action": action, "payload": {}},
                    headers={IPC_TOKEN_HEADER: tokens[role]},
                )
                assert response.status_code == 403, (role, action, response.text)
        maintenance = client.post(
            "/v1/command",
            json={"action": "maintenance", "payload": {"retention_days": 7}},
            headers={IPC_TOKEN_HEADER: tokens["operator"]},
        )
        assert maintenance.status_code == 403
        listed = client.post(
            "/v1/command",
            json={"action": "outbox.dead_list", "payload": {}},
            headers={IPC_TOKEN_HEADER: tokens["operator"]},
        )
        assert listed.status_code == 200
        assert listed.json()["data"] == []
        backlog = client.post(
            "/v1/command",
            json={"action": "outbox.dead_backlog", "payload": {}},
            headers={IPC_TOKEN_HEADER: tokens["uploader"]},
        )
        assert backlog.status_code == 200
    finally:
        client.close()
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)


def test_existing_token_file_gains_operator_without_rotating_others(tmp_path) -> None:
    from edge.app.storage_client import read_role_tokens
    from edge.app.storage_service import ensure_token_file

    path = tmp_path / "storage-ipc-tokens.json"
    original = {
        "version": 1,
        "tokens": {"collector": "a" * 64, "uploader": "b" * 64, "agent": "c" * 64},
    }
    path.write_text(json.dumps(original), encoding="utf-8")

    ensure_token_file(path)
    tokens = read_role_tokens(path)

    assert tokens["collector"] == "a" * 64
    assert tokens["uploader"] == "b" * 64
    assert tokens["agent"] == "c" * 64
    assert len(tokens["operator"]) == 64
    assert tokens["operator"] not in original["tokens"].values()


def test_dead_admin_uses_the_operator_token(tmp_path, monkeypatch) -> None:
    from edge.app.storage_client import read_role_tokens
    from edge.app.storage_service import IpcAuthenticator, StorageEngine, StorageHttpServer, ensure_token_file
    from edge.dead_admin import main

    database = tmp_path / "edge.db"
    token_file = tmp_path / "ipc" / "storage-ipc-tokens.json"
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(token_file))
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_TOKEN", raising=False)
    monkeypatch.delenv("DATAWATCH_EDGE_STORAGE_ROLE", raising=False)
    ensure_token_file(token_file)
    assert "operator" in read_role_tokens(token_file)
    engine = StorageEngine(database, max_queue=32)
    engine.start()
    server = StorageHttpServer(("127.0.0.1", 0), engine, IpcAuthenticator.from_file(token_file))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_URL", f"http://127.0.0.1:{server.server_port}")
    destination = tmp_path / "dead-exports" / "all.jsonl"
    try:
        assert main(["list"]) == 0
        assert main(["replay", "--all", "--dry-run"]) == 0
        assert main(["export", "--format", "jsonl", "--out", str(destination), "--all"]) == 0
        header = json.loads(destination.read_text(encoding="utf-8").splitlines()[0])
        assert header["schema_version"] == 1
    finally:
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)


@pytest.mark.asyncio
async def test_fake_center_dead_frame_replays_as_a_new_sequence(tmp_path) -> None:
    """fake 中心把一帧打成 dead 后，replay 以新的 (session, seq) 再送一次，不和旧序号冲突。"""
    from uuid import uuid4

    from edge.app.uploader import Uploader, UploaderSettings

    class _Signer:
        def headers(self, method, path, payload):
            return {}

    store = _store(tmp_path)
    record = _seed_dead(store)[0]
    # _seed_dead 已经标成 dead。这里改回 pending，让 fake 中心自己把它打成 dead。
    with store._lock:
        store._connection.execute(
            "UPDATE outbox SET state='pending', dead_at=NULL, last_error=NULL, "
            "lease_owner=NULL, lease_until=NULL, inflight_at=NULL WHERE batch_id=?",
            (record.batch_id,),
        )
        store._connection.commit()
    seen: list[list[dict]] = []

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path != "/ingest":
            return httpx.Response(200, json={"status": "switched"})
        items = json.loads(request.content)["items"]
        seen.append(items)
        if len(seen) == 1:
            return httpx.Response(400, text="bad contract")
        return httpx.Response(
            200,
            json={
                "results": [
                    {
                        "batch_id": item["batch_id"],
                        "upload_session_id": item["upload_session_id"],
                        "local_seq": item["local_seq"],
                        "status": "ack",
                        "durability": "redis-aof",
                    }
                    for item in items
                ]
            },
        )

    settings = UploaderSettings(
        center_url="https://center.test",
        node_uuid=str(uuid4()),
        private_key_path=tmp_path / "unused.pem",
        lock_path=tmp_path / "uploader.lock",
        max_attempts=1,
        retry_base_ms=0,
    )
    client = httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler))
    uploader = Uploader(store, settings, client=client, signer=_Signer())
    try:
        await uploader.run_cycle()
        assert store.stats().get("dead") == 1
        dead = store.list_dead()[0]
        old_identity = (dead["upload_session_id"], int(dead["local_seq"]))
        replayed = store.replay_dead(dead["batch_id"], dead["upload_session_id"], int(dead["local_seq"]))
        assert replayed["replayed"] is True
        await uploader.run_cycle()
        assert len(seen) == 2
        new_item = seen[1][0]
        new_identity = (new_item["upload_session_id"], int(new_item["local_seq"]))
        assert new_identity != old_identity
        assert new_identity == (replayed["upload_session_id"], replayed["local_seq"])
        assert new_item["values"] == json.loads(record.payload)["values"]
        assert store.stats()["dead"] == 1
        assert store.stats()["sent"] == 1
    finally:
        await client.aclose()
        store.close()


def test_dead_retention_defaults_stay_separate_from_offline_backlog(monkeypatch, tmp_path) -> None:
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
    from cryptography.hazmat.primitives.serialization import Encoding, NoEncryption, PrivateFormat

    from edge.app.outbox import DEAD_MAX_BYTES_DEFAULT, DEAD_RETENTION_DAYS_DEFAULT
    from edge.uploader_main import settings_from_env

    pem = Ed25519PrivateKey.generate().private_bytes(Encoding.PEM, PrivateFormat.PKCS8, NoEncryption())
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.setenv("DATAWATCH_CENTER_URL", "https://center.test/")
    monkeypatch.setenv("DATAWATCH_NODE_UUID", "node-1")
    monkeypatch.setenv("DATAWATCH_UPLOADER_PRIVATE_KEY_B64", base64.b64encode(pem).decode("ascii"))
    monkeypatch.delenv("DATAWATCH_DEAD_RETENTION_DAYS", raising=False)
    monkeypatch.delenv("DATAWATCH_DEAD_MAX_BYTES", raising=False)
    monkeypatch.delenv("DATAWATCH_MAX_OFFLINE_RETENTION_DAYS", raising=False)

    settings, _signer = settings_from_env()

    assert settings.max_offline_retention_days == 7
    assert settings.dead_retention_days == DEAD_RETENTION_DAYS_DEFAULT == 30
    assert settings.dead_max_bytes == DEAD_MAX_BYTES_DEFAULT == 512 * 1024 * 1024

    monkeypatch.setenv("DATAWATCH_DEAD_RETENTION_DAYS", "14")
    monkeypatch.setenv("DATAWATCH_DEAD_MAX_BYTES", "1024")
    custom, _signer = settings_from_env()
    assert custom.max_offline_retention_days == 7
    assert custom.dead_retention_days == 14
    assert custom.dead_max_bytes == 1024
