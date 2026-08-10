from __future__ import annotations

from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import threading

from edge.app.config_snapshot import snapshot_checksum
from edge.app.outbox import OutboxCompletion
from edge.app.storage_client import EdgeStorageClient
from edge.app.storage_service import StorageEngine, StorageHttpServer


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


def test_storage_serializes_concurrent_frames_and_batch_ack(tmp_path: Path) -> None:
    engine = StorageEngine(tmp_path / "edge.db", max_queue=256)
    engine.start()
    server = StorageHttpServer(("127.0.0.1", 0), engine)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    client = EdgeStorageClient(f"http://127.0.0.1:{server.server_port}")
    try:
        assert client.apply(_snapshot()) == 1

        def commit(index: int):
            return client.commit_frame(
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
        target = client.snapshot_target_seq()
        claimed = client.claim_batch(
            target_seq=target,
            lease_owner="test-uploader",
            lease_ms=5000,
            max_items=100,
            max_bytes=1024 * 1024,
        )
        claimed_again = client.claim_batch(
            target_seq=target,
            lease_owner="test-uploader",
            lease_ms=5000,
            max_items=100,
            max_bytes=1024 * 1024,
        )
        assert [row.local_seq for row in claimed_again] == [row.local_seq for row in claimed]
        client.complete_batch(
            [OutboxCompletion(record, "sent") for record in claimed],
            "test-uploader",
        )
        client.complete_batch(
            [OutboxCompletion(record, "sent") for record in claimed],
            "test-uploader",
        )
        status = client.status()
        assert status["counts"] == {"sent": 100}
        assert status["sent_records_total"] == 100
        assert status["errors_total"] == 0
        assert status["writer_queue_depth"] == 0
    finally:
        client.close()
        server.shutdown()
        server.server_close()
        engine.close()
        thread.join(timeout=5)
