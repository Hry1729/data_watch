from __future__ import annotations

from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import threading
import time

import pytest

from edge.app.config_snapshot import snapshot_checksum
from edge.app.outbox import OutboxCompletion
from edge.app.storage_client import EdgeStorageClient
from edge.app.storage_service import StorageEngine, StorageHttpServer, StorageRequestHandler


def _snapshot() -> dict:
    devices = [
        {
            "device_code": "QC100",
            "device_pk": 1,
            "device_type": "qc",
            "protocol": "s7",
            "endpoint": "127.0.0.1:102",
            "scan_rate_ms": 100,
            "config_version": 1,
            "assignment_epoch": 1,
            "enabled": True,
            "variables": [
                {
                    "variable_id": "1",
                    "address": "DB1.DBD0",
                    "data_type": "REAL",
                    "scan_rate_ms": 100,
                    "is_storage": True,
                    "var_seq": 1,
                    "group_code": "g001",
                    "group_config_version": 1,
                }
            ],
        }
    ]
    return {
        "configRevision": 1,
        "checksum": snapshot_checksum(devices),
        "devices": devices,
    }


def test_storage_http_uses_keepalive_protocol() -> None:
    assert StorageRequestHandler.protocol_version == "HTTP/1.1"

def test_storage_serializes_concurrent_frames_and_batch_ack(
    tmp_path: Path, monkeypatch
) -> None:
    # S3: the IPC endpoint requires a per-role token now, and the token file must
    # stay inside tmp_path. Each client declares its identity explicitly instead
    # of relying on an implicit role.
    monkeypatch.setenv("DATAWATCH_EDGE_STORAGE_TOKEN_FILE", str(tmp_path / "ipc-tokens.json"))
    engine = StorageEngine(tmp_path / "edge.db", max_queue=256)
    engine.start()
    server = StorageHttpServer(("127.0.0.1", 0), engine)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    base_url = f"http://127.0.0.1:{server.server_port}"
    agent = EdgeStorageClient(base_url, role="agent")
    collector = EdgeStorageClient(base_url, role="collector")
    uploader = EdgeStorageClient(base_url, role="uploader")
    try:
        assert agent.apply(_snapshot()) == 1

        def commit(index: int):
            return collector.commit_frame(
                batch_id=f"frame-{index}",
                device_code="QC100",
                device_type="qc",
                group_code="g001",
                scan_rate_ms=100,
                ts=1721800000000 + index,
                config_version=1,
                values={"1": str(index)},
                qualities={"1": 192},
                latest_fields={
                    "1": {"v": str(index), "t": 1721800000000 + index, "q": 192}
                },
            )

        with ThreadPoolExecutor(max_workers=8) as executor:
            records = list(executor.map(commit, range(100)))

        assert sorted(record.local_seq for record in records) == list(range(1, 101))
        target = uploader.snapshot_target_seq()
        claimed = uploader.claim_batch(
            target_seq=target,
            lease_owner="test-uploader",
            lease_ms=5000,
            max_items=100,
            max_bytes=1024 * 1024,
        )
        claimed_again = uploader.claim_batch(
            target_seq=target,
            lease_owner="test-uploader",
            lease_ms=5000,
            max_items=100,
            max_bytes=1024 * 1024,
        )
        assert [row.local_seq for row in claimed_again] == [row.local_seq for row in claimed]
        uploader.complete_batch(
            [OutboxCompletion(record, "sent") for record in claimed],
            "test-uploader",
        )
        uploader.complete_batch(
            [OutboxCompletion(record, "sent") for record in claimed],
            "test-uploader",
        )
        uploader.maintenance(7, 24 * 60 * 60 * 1000)
        status = uploader.status()
        assert status["counts"] == {"sent": 100}
        assert status["sent_records_total"] == 100
        assert status["errors_total"] == 0
        assert status["writer_queue_depth"] == 0
    finally:
        agent.close()
        collector.close()
        uploader.close()
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)


def test_storage_close_reports_slow_writer_instead_of_silent_return(tmp_path: Path) -> None:
    engine = StorageEngine(
        tmp_path / "edge.db",
        max_queue=8,
        shutdown_timeout_seconds=0.01,
    )
    engine.start()
    dispatch_started = threading.Event()

    def slow_dispatch(_outbox, _config, _action, _payload):
        dispatch_started.set()
        time.sleep(0.1)
        return None

    engine._dispatch = slow_dispatch
    submit_thread = threading.Thread(target=lambda: engine.submit("slow"))
    submit_thread.start()
    assert dispatch_started.wait(timeout=1)

    with pytest.raises(TimeoutError, match="did not stop"):
        engine.close()

    submit_thread.join(timeout=1)
    assert not submit_thread.is_alive()
    # The second close observes the same worker and waits for the accepted
    # command/sentinel to finish instead of silently abandoning it.
    engine.close(timeout_seconds=1)


def test_storage_queue_saturation_backpressures_without_dropping_commands(
    tmp_path: Path,
) -> None:
    engine = StorageEngine(tmp_path / "edge.db", max_queue=1)
    engine.start()
    first_started = threading.Event()
    release = threading.Event()
    results: list[str] = []
    errors: list[Exception] = []

    def slow_dispatch(_outbox, _config, action, _payload):
        if action == "first":
            first_started.set()
            release.wait(timeout=2)
        return action

    def submit(action: str) -> None:
        try:
            results.append(engine.submit(action, timeout_seconds=2))
        except Exception as exc:
            errors.append(exc)

    engine._dispatch = slow_dispatch
    threads = [
        threading.Thread(target=submit, args=(action,))
        for action in ("first", "second", "third")
    ]
    try:
        threads[0].start()
        assert first_started.wait(timeout=1)
        threads[1].start()
        time.sleep(0.02)
        threads[2].start()
        time.sleep(0.05)
        assert threads[2].is_alive()

        release.set()
        for thread in threads:
            thread.join(timeout=2)

        assert errors == []
        assert sorted(results) == ["first", "second", "third"]
        assert all(not thread.is_alive() for thread in threads)
    finally:
        release.set()
        engine.close(timeout_seconds=2)
