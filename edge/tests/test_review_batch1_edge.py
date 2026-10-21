"""审查批次1（边缘侧）回归：B001 / B002 / B003 / B022 / B024 / B027 / B026（边缘对 retry_later 的处理）。"""

from __future__ import annotations

import asyncio
import json
import sqlite3
import threading
import time
from pathlib import Path
from uuid import uuid4

import httpx
import msgspec
import pytest

from edge.app.config_snapshot import ConfigSnapshotStore, EdgeConfigManager, snapshot_checksum
from edge.app.outbox import OutboxStore
from edge.app.storage_service import StorageEngine
from edge.app.uploader import Uploader, UploaderPaused, UploaderSettings

DAY_MS = 24 * 60 * 60 * 1000


class FakeSigner:
    def headers(self, method, path, payload):
        return {}


def _settings(tmp_path: Path) -> UploaderSettings:
    return UploaderSettings(
        center_url="https://center.test",
        node_uuid=str(uuid4()),
        private_key_path=tmp_path / "unused.pem",
        lock_path=tmp_path / "uploader.lock",
        retry_base_ms=500,
    )


def _append(store: OutboxStore, count: int = 1, **extra) -> None:
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
            qualities={"1": 0},
            **extra,
        )


def _seed_dead(store: OutboxStore, count: int = 1):
    _append(store, count)
    records = store.claim_batch(
        target_seq=store.snapshot_target_seq(),
        lease_owner="U1",
        lease_ms=60_000,
        max_items=10,
        max_bytes=10**6,
    )
    for record in records:
        store.mark_dead(record, "U1", "protocol_reject")
    return records


def _age_dead(store: OutboxStore, days: int) -> None:
    with store._lock:
        store._connection.execute(
            "UPDATE outbox SET dead_at = dead_at - ? WHERE state='dead'", (days * DAY_MS,)
        )
        store._connection.commit()


def _pin_disk(store: OutboxStore, monkeypatch, watermark: str = "normal") -> None:
    monkeypatch.setattr(
        store,
        "_disk_status",
        lambda: {
            "disk_total_bytes": 1000,
            "disk_free_bytes": 10,
            "disk_used_percent": 99.0,
            "watermark": watermark,
        },
    )


def _exported_at(store: OutboxStore, batch_id: str):
    with store._lock:
        return store._connection.execute(
            "SELECT exported_at FROM outbox WHERE batch_id=?", (batch_id,)
        ).fetchone()["exported_at"]


# ── B001：只有校验过的完整备份才授予清理凭证 ─────────────────────────────────


def test_b001_csv_export_is_not_a_backup_and_never_allows_purge(tmp_path, monkeypatch) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    _pin_disk(store, monkeypatch)
    record = _seed_dead(store)[0]

    result = store.export_dead_batch(
        store.path.parent / "dead-exports" / "triage.csv", fmt="csv"
    )
    _age_dead(store, 31)

    assert result["count"] == 1
    assert result["backup"] is False
    assert _exported_at(store, record.batch_id) is None
    assert store.maintenance(retention_days=7)["dead_purged"] == 0
    assert store.dead_backlog()["records"] == 1
    store.close()


def test_b001_single_export_needs_explicit_confirmation(tmp_path, monkeypatch) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    _pin_disk(store, monkeypatch)
    record = _seed_dead(store)[0]

    exported = store.export_dead(record.batch_id, record.upload_session_id, record.local_seq)
    _age_dead(store, 31)

    # 响应已经返回，但客户端是否保存成功未知：不得清理
    assert exported["exported_at"] is None
    assert store.maintenance(retention_days=7)["dead_purged"] == 0
    # 摘要不符或身份不全都不授予凭证
    assert not store.confirm_dead_export(
        record.batch_id, record.upload_session_id, record.local_seq, "0" * 64
    )
    assert not store.confirm_dead_export(
        record.batch_id, str(uuid4()), record.local_seq, exported["payload_sha256"]
    )
    assert _exported_at(store, record.batch_id) is None

    assert store.confirm_dead_export(
        record.batch_id, record.upload_session_id, record.local_seq, exported["payload_sha256"]
    )
    assert store.maintenance(retention_days=7)["dead_purged"] == 1
    store.close()


def test_b001_jsonl_export_that_fails_verification_grants_nothing(tmp_path, monkeypatch) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    record = _seed_dead(store)[0]
    real_publish = store._publish_export_file

    def publish_then_corrupt(temporary: Path, dest: Path) -> None:
        real_publish(temporary, dest)
        with open(dest, "ab") as handle:
            handle.write(b"torn")

    monkeypatch.setattr(store, "_publish_export_file", publish_then_corrupt)
    with pytest.raises(OSError, match="verification"):
        store.export_dead_batch(store.path.parent / "dead-exports" / "bad.jsonl", fmt="jsonl")
    assert _exported_at(store, record.batch_id) is None

    monkeypatch.setattr(store, "_publish_export_file", real_publish)
    good = store.export_dead_batch(store.path.parent / "dead-exports" / "good.jsonl", fmt="jsonl")
    assert good["backup"] is True and good["count"] == 1
    assert _exported_at(store, record.batch_id) is not None
    store.close()


# ── B002：自动导出失败不挡 sent 清理与上传 ───────────────────────────────────


def test_b002_auto_export_failure_still_cleans_sent_and_keeps_dead(tmp_path, monkeypatch) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    _pin_disk(store, monkeypatch, watermark="critical")
    _seed_dead(store)
    _append(store, 1)
    sent = store.claim_batch(
        target_seq=store.snapshot_target_seq(),
        lease_owner="U1",
        lease_ms=60_000,
        max_items=10,
        max_bytes=10**6,
    )
    store.mark_sent(sent[0], "U1")
    with store._lock:
        store._connection.execute("UPDATE outbox SET acked_at = acked_at - 10000 WHERE state='sent'")
        store._connection.commit()

    def refuse(*_args, **_kwargs):
        raise PermissionError("dead-exports not writable")

    monkeypatch.setattr(store, "_export_dead_rows", refuse)
    result = store.maintenance(retention_days=7, sent_retention_ms=1)

    assert result["dead_auto_export_failed"] is True
    assert "dead_auto_export_failed" in result["degraded_reasons"]
    assert result["deleted_terminal"] == 1
    assert store.stats() == {"dead": 1}
    store.close()


@pytest.mark.asyncio
async def test_b002_maintenance_failure_does_not_stop_uploads(tmp_path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    _append(store, 1)
    calls: list[str] = []

    def failing_maintenance(*_args, **_kwargs):
        calls.append("maintenance")
        raise OSError("No space left on device")

    store.maintenance = failing_maintenance  # type: ignore[method-assign]

    def handler(request: httpx.Request) -> httpx.Response:
        item = json.loads(request.content)["items"][0]
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
                ]
            },
        )

    client = httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler))
    uploader = Uploader(store, _settings(tmp_path), client=client, signer=FakeSigner())

    await uploader._maintain_if_due()
    assert calls == ["maintenance"]
    # 推迟重试，不是每秒重试
    assert uploader._next_maintenance_at > time.monotonic()
    await uploader.run_cycle()
    assert store.stats() == {"sent": 1}
    await client.aclose()
    store.close()


# ── B003：中心回滚到本机上一次的旧 session 时自愈，其他 session 仍拒绝 ─────────


def _switch_completed(store: OutboxStore):
    state = store.prepare_session_switch()
    store.complete_session_switch(state)
    return state


def test_b003_reopen_only_matches_this_nodes_previous_session(tmp_path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    state = _switch_completed(store)

    assert store.reopen_rolled_back_switch(str(uuid4())) is None
    assert store.reopen_rolled_back_switch(state.new_session_id) is None
    assert store.pending_session_switch() is None

    reopened = store.reopen_rolled_back_switch(state.old_session_id)
    assert reopened == state
    assert store.pending_session_switch() == state
    # 已有待完成切换时不重复打开
    assert store.reopen_rolled_back_switch(state.old_session_id) is None
    store.close()


@pytest.mark.asyncio
async def test_b003_center_rollback_is_healed_without_takeover(tmp_path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    state = _switch_completed(store)
    _append(store, 1)
    center = {"active": state.old_session_id}
    switches: list[dict] = []

    def handler(request: httpx.Request) -> httpx.Response:
        if request.method == "GET":
            return httpx.Response(200, json={"active_session_id": center["active"]})
        payload = json.loads(request.content)
        if request.url.path == "/ingest/session_switch":
            switches.append(payload)
            if center["active"] != payload["old_session_id"]:
                return httpx.Response(200, json={"status": "session_conflict"})
            center["active"] = payload["new_session_id"]
            return httpx.Response(200, json={"status": "switched"})
        item = payload["items"][0]
        status = "ack" if center["active"] == item["upload_session_id"] else "session_obsolete"
        return httpx.Response(
            200,
            json={
                "results": [
                    {
                        "batch_id": item["batch_id"],
                        "upload_session_id": item["upload_session_id"],
                        "local_seq": item["local_seq"],
                        "status": status,
                        "durability": "redis-aof",
                    }
                ]
            },
        )

    client = httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler))
    uploader = Uploader(store, _settings(tmp_path), client=client, signer=FakeSigner())

    await uploader.run_cycle()

    assert switches == [
        {
            "node_uuid": uploader._settings.node_uuid,
            "old_session_id": state.old_session_id,
            "new_session_id": state.new_session_id,
        }
    ]
    assert center["active"] == state.new_session_id
    assert store.current_session_id() == state.new_session_id
    assert store.pending_session_switch() is None

    # 中心恢复后下一轮直接上传成功
    with store._lock:
        store._connection.execute("UPDATE outbox SET next_retry_at=NULL")
        store._connection.commit()
    await uploader.run_cycle()
    assert store.stats() == {"sent": 1}
    await client.aclose()
    store.close()


@pytest.mark.asyncio
async def test_b003_foreign_session_still_pauses(tmp_path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    _switch_completed(store)
    _append(store, 1)
    foreign = str(uuid4())
    switches: list[dict] = []

    def handler(request: httpx.Request) -> httpx.Response:
        if request.method == "GET":
            return httpx.Response(200, json={"active_session_id": foreign})
        payload = json.loads(request.content)
        if request.url.path == "/ingest/session_switch":
            switches.append(payload)
            return httpx.Response(200, json={"status": "session_conflict"})
        item = payload["items"][0]
        return httpx.Response(
            200,
            json={
                "results": [
                    {
                        "batch_id": item["batch_id"],
                        "upload_session_id": item["upload_session_id"],
                        "local_seq": item["local_seq"],
                        "status": "session_obsolete",
                    }
                ]
            },
        )

    client = httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler))
    uploader = Uploader(store, _settings(tmp_path), client=client, signer=FakeSigner())

    with pytest.raises(UploaderPaused):
        await uploader.run_cycle()
    with pytest.raises(UploaderPaused):
        await uploader.run_cycle()
    assert switches == []
    assert store.pending_session_switch() is None
    await client.aclose()
    store.close()


# ── B026（边缘侧）：retry_later 在同一 session 退避重试，不判 dead ─────────────


@pytest.mark.asyncio
async def test_b026_retry_later_is_retried_not_dead(tmp_path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    _append(store, 2)
    session = store.current_session_id()

    def handler(request: httpx.Request) -> httpx.Response:
        item = json.loads(request.content)["items"][0]
        return httpx.Response(
            200,
            json={
                "results": [
                    {
                        "batch_id": item["batch_id"],
                        "upload_session_id": item["upload_session_id"],
                        "local_seq": item["local_seq"],
                        "status": "retry_later",
                        "reason": "malformed_envelope:unknown_var_id",
                    }
                ]
            },
        )

    client = httpx.AsyncClient(base_url="https://center.test", transport=httpx.MockTransport(handler))
    uploader = Uploader(store, _settings(tmp_path), client=client, signer=FakeSigner())
    await uploader.run_cycle()

    assert store.stats() == {"retry_wait": 2}
    assert store.current_session_id() == session
    with store._lock:
        budgets = [
            int(row["reject_count"])
            for row in store._connection.execute("SELECT reject_count FROM outbox")
        ]
    assert budgets == [0, 0]
    await client.aclose()
    store.close()


# ── B022（边缘侧）：缺值的坏质量点不伪造 latest 值 ───────────────────────────


def test_b022_latest_without_value_is_not_stringified(tmp_path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    _append(
        store,
        1,
        latest_fields={
            "1": {"v": "0", "t": 1721800000000, "q": 0},
            "2": {"v": None, "t": 1721800000000, "q": 5},
            "3": {"t": 1721800000000, "q": 7},
        },
    )
    records = store.claim_batch(
        target_seq=store.snapshot_target_seq(),
        lease_owner="U1",
        lease_ms=60_000,
        max_items=10,
        max_bytes=10**6,
    )
    payload = msgspec.json.decode(records[0].payload)
    assert payload["latest_values"] == {"1": "0"}
    assert payload["latest_qualities"] == {"1": 0, "2": 5, "3": 7}
    assert set(payload["latest_timestamps"]) == {"1", "2", "3"}
    store.close()


# ── B024：设备与点位来自同一读快照 ───────────────────────────────────────────


def _snapshot_devices(scan_rate_ms: int) -> list[dict]:
    return [
        {
            "device_code": "QC01",
            "device_pk": 7,
            "device_type": "QC",
            "protocol": "s7",
            "endpoint": "10.0.0.7:102",
            "ip": "10.0.0.7",
            "port": 102,
            "rack": 0,
            "slot": 1,
            "scan_rate_ms": scan_rate_ms,
            "config_version": scan_rate_ms,
            "assignment_epoch": 3,
            "variables": [
                {
                    "variable_id": "101",
                    "address": f"DB1.DBD{scan_rate_ms}",
                    "data_type": "REAL",
                    "scan_rate_ms": scan_rate_ms,
                    "is_storage": True,
                    "var_seq": 1,
                    "group_code": "g001",
                    "group_config_version": scan_rate_ms,
                }
            ],
        }
    ]


class _CommitBetweenSelects:
    """读完设备表后，由另一个连接提交一份新快照（模拟配置同步恰好落在两条 SELECT 之间）。"""

    def __init__(self, connection: sqlite3.Connection, on_devices) -> None:
        self._connection = connection
        self._on_devices = on_devices

    def execute(self, sql, *args):
        cursor = self._connection.execute(sql, *args)
        if "FROM edge_device WHERE enabled=1" in sql and "edge_variable" not in sql:
            rows = cursor.fetchall()
            self._on_devices()
            return _Rows(rows)
        return cursor

    def __enter__(self):
        self._connection.__enter__()
        return self

    def __exit__(self, *exc):
        return self._connection.__exit__(*exc)


class _Rows:
    def __init__(self, rows) -> None:
        self._rows = rows

    def fetchall(self):
        return self._rows


@pytest.mark.asyncio
async def test_b024_devices_and_variables_come_from_one_snapshot(tmp_path) -> None:
    path = tmp_path / "edge.db"
    outbox = OutboxStore(path)
    store = ConfigSnapshotStore(path)
    old = _snapshot_devices(100)
    store.apply_snapshot(config_revision=1, checksum=snapshot_checksum(old), devices=old)
    manager = EdgeConfigManager(path)
    real_connect = manager._connect
    new = _snapshot_devices(200)
    fired = []

    def commit_new() -> None:
        if not fired:
            fired.append(True)
            store.apply_snapshot(config_revision=2, checksum=snapshot_checksum(new), devices=new)

    manager._connect = lambda: _CommitBetweenSelects(real_connect(), commit_new)  # type: ignore[method-assign]

    devices, variables = await manager.load_devices_and_variables()

    assert fired
    assert [device.config_version for device in devices] == [100]
    assert [variable.address for variable in variables] == ["DB1.DBD100"]
    # 下一次读取拿到完整的新版本
    manager._connect = real_connect  # type: ignore[method-assign]
    devices, variables = await manager.load_devices_and_variables()
    assert [device.config_version for device in devices] == [200]
    assert [variable.address for variable in variables] == ["DB1.DBD200"]
    store.close()
    outbox.close()


# ── B027：慢导出不占用存储命令线程 ───────────────────────────────────────────


def test_b027_slow_dead_export_does_not_block_other_commands(tmp_path, monkeypatch) -> None:
    release = threading.Event()
    entered = threading.Event()
    real_export = OutboxStore.export_dead_batch

    def slow_export(self, *args, **kwargs):
        entered.set()
        release.wait(10)
        return real_export(self, *args, **kwargs)

    monkeypatch.setattr(OutboxStore, "export_dead_batch", slow_export)
    engine = StorageEngine(tmp_path / "edge.db", max_queue=32)
    engine.start()
    try:
        results: dict[str, object] = {}

        def run_export() -> None:
            try:
                results["export"] = engine.submit(
                    "outbox.dead_export_batch",
                    {"out": "slow.jsonl", "format": "jsonl"},
                    timeout_seconds=20,
                )
            except Exception as exc:  # pragma: no cover - 失败时给出诊断
                results["export"] = exc

        exporter = threading.Thread(target=run_export)
        exporter.start()
        assert entered.wait(5)

        started = time.monotonic()
        assert engine.submit("outbox.snapshot", timeout_seconds=2.0) == 0
        assert time.monotonic() - started < 2.0
        # 有界：同时只允许一个导出
        with pytest.raises(RuntimeError, match="already running"):
            engine.submit(
                "outbox.dead_export_batch",
                {"out": "second.jsonl", "format": "jsonl"},
                timeout_seconds=2.0,
            )

        release.set()
        exporter.join(10)
        assert isinstance(results["export"], dict)
        assert results["export"]["count"] == 0
    finally:
        release.set()
        engine.close()


# ── PR #26 审查：导出分页稳定，页间清理不再漏行；行数不符则失败 ─────────────


def _ordered_dead(store: OutboxStore, count: int, purgeable: int):
    """count 条 dead，按顺序排定 dead_at；前 purgeable 条已导出且超出保留期（可被清理）。"""
    records = _seed_dead(store, count)
    old = store._clock_ms() - 40 * DAY_MS
    with store._lock:
        for index, record in enumerate(records):
            store._connection.execute(
                "UPDATE outbox SET dead_at=?, exported_at=? WHERE batch_id=?",
                (old + index, old if index < purgeable else None, record.batch_id),
            )
        store._connection.commit()
    return records


def _export_lines(path: Path) -> list[dict]:
    lines = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines()]
    return [line for line in lines if line.get("kind") != "header"]


def _dead_ids(store: OutboxStore) -> set[str]:
    with store._lock:
        return {
            row["batch_id"]
            for row in store._connection.execute("SELECT batch_id FROM outbox WHERE state='dead'")
        }


def test_pr26_purge_between_export_pages_does_not_drop_rows(tmp_path, monkeypatch) -> None:
    import edge.app.outbox as outbox_module

    monkeypatch.setattr(outbox_module, "_DEAD_EXPORT_PAGE_SIZE", 2)
    store = OutboxStore(tmp_path / "edge.db")
    _pin_disk(store, monkeypatch)
    records = _ordered_dead(store, count=6, purgeable=2)
    real_page = store._snapshot_dead_page
    maintenance_results: list[dict] = []

    def page_then_maintain(*args, **kwargs):
        page = real_page(*args, **kwargs)
        if not maintenance_results:
            # 第一页读完后，命令线程跑一次 maintenance（与卸载出去的导出线程交错）
            worker = threading.Thread(
                target=lambda: maintenance_results.append(
                    store.maintenance(retention_days=7, dead_max_bytes=10**9)
                )
            )
            worker.start()
            worker.join(10)
        return page

    monkeypatch.setattr(store, "_snapshot_dead_page", page_then_maintain)
    out = store._export_root() / "all.jsonl"
    result = store.export_dead_batch(out, fmt="jsonl")

    written = [line["batch_id"] for line in _export_lines(out)]
    assert written == [record.batch_id for record in records]  # 一行不少、顺序不变
    assert result["count"] == 6
    assert maintenance_results[0]["dead_purged"] == 0
    assert maintenance_results[0]["dead_purge_deferred"] is True

    monkeypatch.setattr(store, "_snapshot_dead_page", real_page)
    after = store.maintenance(retention_days=7, dead_max_bytes=10**9)
    assert after["dead_purge_deferred"] is False
    assert after["dead_purged"] == 6  # 导出结束后照常清理（6 行现在都已备份且超期）
    store.close()


def test_pr26_rows_vanishing_mid_export_fail_the_export(tmp_path, monkeypatch) -> None:
    import edge.app.outbox as outbox_module

    monkeypatch.setattr(outbox_module, "_DEAD_EXPORT_PAGE_SIZE", 2)
    store = OutboxStore(tmp_path / "edge.db")
    records = _ordered_dead(store, count=5, purgeable=0)
    real_page = store._snapshot_dead_page
    calls = {"n": 0}

    def page_then_remove(*args, **kwargs):
        calls["n"] += 1
        if calls["n"] == 2:
            # 另一条删除路径（不经 maintenance）在页间删掉了后面页里的一行
            with store._lock:
                store._connection.execute(
                    "DELETE FROM outbox WHERE batch_id=?", (records[4].batch_id,)
                )
                store._connection.commit()
        return real_page(*args, **kwargs)

    monkeypatch.setattr(store, "_snapshot_dead_page", page_then_remove)
    out = store._export_root() / "partial.jsonl"
    with pytest.raises(RuntimeError, match="changed during export"):
        store.export_dead_batch(out, fmt="jsonl")

    assert not out.exists()
    assert list(store._export_root().iterdir()) == []  # 临时文件也已清理
    assert all(_exported_at(store, r.batch_id) is None for r in records[:4])
    assert store._dead_exports_active == 0
    store.close()


def test_pr26_offloaded_export_reports_failure_not_success(tmp_path, monkeypatch) -> None:
    import edge.app.outbox as outbox_module

    monkeypatch.setattr(outbox_module, "_DEAD_EXPORT_PAGE_SIZE", 1)
    real_page = OutboxStore._snapshot_dead_page
    calls = {"n": 0}

    def page_then_remove(self, where_sql, params, keys):
        calls["n"] += 1
        if calls["n"] == 2:
            with self._lock:
                self._connection.execute("DELETE FROM outbox WHERE batch_id=?", (keys[0],))
                self._connection.commit()
        return real_page(self, where_sql, params, keys)

    monkeypatch.setattr(OutboxStore, "_snapshot_dead_page", page_then_remove)
    seeder = OutboxStore(tmp_path / "edge.db")
    _seed_dead(seeder, 3)
    seeder.close()
    engine = StorageEngine(tmp_path / "edge.db", max_queue=32)
    engine.start()
    try:
        with pytest.raises(RuntimeError, match="changed during export"):
            engine.submit(
                "outbox.dead_export_batch",
                {"out": "offloaded.jsonl", "format": "jsonl"},
                timeout_seconds=20,
            )
    finally:
        engine.close()
