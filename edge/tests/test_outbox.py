from __future__ import annotations

from pathlib import Path

import pytest

from edge.app.outbox import OutboxStore


def append(store: OutboxStore, device: str = "QC100"):
    return store.append_envelope(
        device_code=device,
        device_type="qc",
        group_code="g001",
        scan_rate_ms=100,
        ts=1721800000000,
        config_version=1,
        values={"1": "12.5"},
        qualities={"1": 192},
    )


def configure(store: OutboxStore, device: str, epoch: int, rate: int) -> None:
    store.configure_assignment(device, epoch, rate, 1)


def test_atomic_sequence_starts_at_one_and_rolls_back_without_assignment(
    tmp_path: Path,
) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    with pytest.raises(ValueError, match="no active assignment"):
        append(store)
    assert store.snapshot_target_seq() == 0

    configure(store, "QC100", 5, 100)
    first = append(store)
    second = append(store)

    assert first.local_seq == 1
    assert second.local_seq == 2
    assert first.upload_session_id == second.upload_session_id
    store.close()


def test_upload_interval_tracks_fastest_enabled_assignment(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    assert store.upload_interval_ms() is None

    configure(store, "FAST", 1, 100)
    configure(store, "SLOW", 1, 400)
    assert store.upload_interval_ms() == 100

    store.configure_assignment("FAST", 1, 100, 2, enabled=False)
    assert store.upload_interval_ms() == 400
    store.close()


def test_claim_is_ordered_and_retry_wait_blocks_later_rows(tmp_path: Path) -> None:
    now = [1000]
    store = OutboxStore(tmp_path / "edge.db", clock_ms=lambda: now[0])
    configure(store, "QC100", 1, 100)
    records = [append(store) for _ in range(3)]

    claimed = store.claim_batch(
        target_seq=3,
        lease_owner="owner-1",
        lease_ms=5000,
        max_items=2,
        max_bytes=10000,
    )
    assert [record.local_seq for record in claimed] == [1, 2]

    store.mark_sent(claimed[0], "owner-1")
    assert store.operational_status()["sent_records_total"] == 1
    store.mark_retry(
        claimed[1],
        "owner-1",
        error="network",
        http_status=None,
        delay_ms=500,
    )
    blocked = store.claim_batch(
        target_seq=3,
        lease_owner="owner-1",
        lease_ms=5000,
        max_items=10,
        max_bytes=10000,
    )
    assert blocked == []

    now[0] = 1500
    retried = store.claim_batch(
        target_seq=3,
        lease_owner="owner-1",
        lease_ms=5000,
        max_items=10,
        max_bytes=10000,
    )
    assert [record.local_seq for record in retried] == [2, 3]
    assert records[2].local_seq == 3
    store.close()


def test_recover_inflight_and_session_switch(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    configure(store, "QC100", 1, 100)
    first = append(store)
    second = append(store)
    claimed = store.claim_batch(
        target_seq=2,
        lease_owner="old-owner",
        lease_ms=5000,
        max_items=10,
        max_bytes=10000,
    )
    store.mark_sent(claimed[0], "old-owner")

    assert store.recover_inflight("new-owner") == 1
    switch = store.prepare_session_switch()
    assert switch.old_session_id == first.upload_session_id
    assert switch.new_session_id != switch.old_session_id
    assert store.stats() == {"pending": 1, "sent": 1}
    assert store.pending_session_switch() == switch

    store.complete_session_switch(switch)
    assert store.pending_session_switch() is None
    new_first = append(store)
    assert new_first.local_seq == 2
    assert new_first.upload_session_id == switch.new_session_id
    assert second.local_seq == 2
    store.close()


def test_local_latest_is_available_offline(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    store.write_local_latest(
        "QC100",
        "g001",
        {"1": {"v": "12.5", "t": 1000, "q": 192}},
    )

    assert store.read_local_latest("QC100", "g001") == {
        "1": {"v": "12.5", "t": 1000, "q": 192}
    }
    store.close()


def test_local_latest_prefers_compact_frame_and_keeps_legacy_fallback(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    with store._lock:
        store._connection.execute(
            """
            INSERT INTO local_latest(device_code, group_code, var_id, value_json, updated_at)
            VALUES('QC100', 'g001', '1', '{"v":"legacy","t":1,"q":0}', 1)
            """
        )
    assert store.read_local_latest("QC100", "g001")["1"]["v"] == "legacy"

    store.write_local_latest(
        "QC100",
        "g001",
        {"1": {"v": "compact", "t": 2, "q": 192}},
    )
    assert store.read_local_latest("QC100", "g001")["1"]["v"] == "compact"
    with store._lock:
        assert store._connection.execute(
            "SELECT COUNT(*) FROM local_latest_frame"
        ).fetchone()[0] == 1
    store.close()


def test_overflow_marks_degraded_without_dropping_backlog(tmp_path: Path, monkeypatch) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    configure(store, "QC100", 1, 100)
    original_session = append(store).upload_session_id
    append(store)
    monkeypatch.setattr(
        store,
        "_disk_status",
        lambda: {
            "disk_total_bytes": 100,
            "disk_free_bytes": 4,
            "disk_used_percent": 96.0,
            "watermark": "overflow",
        },
    )

    result = store.maintenance(7)

    assert result["dropped_records"] == 0
    assert result["session_switched"] is False
    assert result["degraded"] is True
    assert result["degraded_reasons"] == ["disk_overflow"]
    assert result["backlog_count"] == 2
    assert store.stats() == {"pending": 2}
    assert store.pending_session_switch() is None
    assert store.current_session_id() == original_session
    assert store.snapshot_target_seq() == 2
    assert store.operational_status()["dropped_records"] == 0
    store.close()


def test_overflow_rejects_new_durable_frame_without_touching_existing_rows(
    tmp_path: Path, monkeypatch
) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    configure(store, "QC100", 1, 100)
    append(store)
    monkeypatch.setattr(
        store,
        "_disk_status",
        lambda: {"disk_total_bytes": 100, "disk_free_bytes": 4, "disk_used_percent": 96.0, "watermark": "overflow"},
    )

    with pytest.raises(RuntimeError, match="refusing new durable data"):
        append(store)

    assert store.stats() == {"pending": 1}
    store.close()


def test_maintenance_keeps_dead_for_retention_but_removes_sent_after_sixty_seconds(
    tmp_path: Path,
) -> None:
    now = [1_721_800_000_000]
    store = OutboxStore(tmp_path / "edge.db", clock_ms=lambda: now[0])
    configure(store, "QC100", 1, 100)
    append(store)
    append(store)
    claimed = store.claim_batch(
        target_seq=2,
        lease_owner="owner-1",
        lease_ms=5000,
        max_items=2,
        max_bytes=10000,
    )
    store.mark_sent(claimed[0], "owner-1")
    store.mark_dead(claimed[1], "owner-1", "invalid mapping")

    now[0] += 60_001
    result = store.maintenance(7)
    assert result["deleted_terminal"] == 1
    assert store.stats() == {"dead": 1}

    now[0] += 7 * 24 * 60 * 60 * 1000
    result = store.maintenance(7)
    assert result["deleted_terminal"] == 1
    assert store.stats() == {}
    store.close()


def test_new_database_enables_incremental_vacuum_and_reports_free_pages(
    tmp_path: Path,
) -> None:
    store = OutboxStore(tmp_path / "edge.db")

    status = store.operational_status()

    assert status["auto_vacuum_mode"] == 2
    assert status["database_live_bytes"] > 0
    assert status["database_reclaimable_bytes"] >= 0
    assert 0 <= status["database_free_page_ratio"] <= 1
    store.close()


def test_outbox_initialization_does_not_import_collector_config(
    tmp_path: Path,
    monkeypatch,
) -> None:
    original_import = __import__

    def guarded_import(name, *args, **kwargs):
        if name == "edge.app.config_snapshot":
            raise AssertionError("Outbox initialization loaded Collector-only config")
        return original_import(name, *args, **kwargs)

    monkeypatch.setattr("builtins.__import__", guarded_import)
    store = OutboxStore(tmp_path / "edge.db")
    store.close()
