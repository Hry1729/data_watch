from pathlib import Path
from uuid import uuid4

import msgspec
import pytest

from storage.edge_outbox_writer import EdgeOutboxWriter
from edge.app.outbox import OutboxStore
from ingest.app.schemas import UploadBatch, UploadItem


def test_edge_outbox_writer_preserves_realtime_fields_in_payload(tmp_path: Path):
    store = OutboxStore(tmp_path / "edge.db")
    store.configure_assignment("DEV1", 1, 100, 1)
    writer = EdgeOutboxWriter(store)

    # 场景：采集到 1 个存储数值点（var_num）、1 个非存储数值点（var_non_storage）、1 个字符串点（var_str）
    fields = {
        "var_num": {"v": "100.5", "t": 1000, "q": 192},
        "var_non_storage": {"v": "200.0", "t": 1000, "q": 192},
        "var_str": {"v": "RUNNING", "t": 1000, "q": 192},
    }
    # 历史 stream_values 仅含存储数值点
    stream_values = {"var_num": "100.5"}
    stream_qualities = {"var_num": 192}

    writer.write_window_stream(
        device_code="DEV1",
        device_type="s7",
        group_code="GRP1",
        scan_rate_ms=100,
        ts=1000,
        config_version=1,
        frame={},
        stream_values=stream_values,
        stream_qualities=stream_qualities,
    )
    writer.write_latest("DEV1", "GRP1", fields)

    records = store.claim_batch(
        target_seq=1,
        lease_owner="test",
        lease_ms=10000,
        max_items=10,
        max_bytes=1024 * 1024,
    )
    assert len(records) == 1
    envelope = msgspec.json.decode(records[0].payload)

    # 1. 历史 values 严格仅保留存储数值点
    assert envelope["values"] == {"var_num": "100.5"}

    # 2. 实时 latest_values 包含全部 3 个点（含非存储、字符串）
    assert "latest_values" in envelope
    assert envelope["latest_values"] == {
        "var_num": "100.5",
        "var_non_storage": "200.0",
        "var_str": "RUNNING",
    }


def test_edge_outbox_writer_pure_realtime_batch_creates_upload_record(tmp_path: Path):
    store = OutboxStore(tmp_path / "edge.db")
    store.configure_assignment("DEV1", 1, 100, 1)
    writer = EdgeOutboxWriter(store)

    # 场景：全都是非存储点或字符串点，stream_values 为空
    fields = {
        "var_str": {"v": "IDLE", "t": 1000, "q": 192},
    }
    writer.write_window_stream(
        device_code="DEV1",
        device_type="s7",
        group_code="GRP1",
        scan_rate_ms=100,
        ts=1000,
        config_version=1,
        frame={},
        stream_values={},
        stream_qualities={},
    )
    writer.write_latest("DEV1", "GRP1", fields)

    # 验证纯实时批次仍生成了上传记录
    records = store.claim_batch(
        target_seq=1,
        lease_owner="test",
        lease_ms=10000,
        max_items=10,
        max_bytes=1024 * 1024,
    )
    assert len(records) == 1
    envelope = msgspec.json.decode(records[0].payload)
    assert envelope["values"] == {}
    assert envelope["latest_values"] == {"var_str": "IDLE"}


def test_ingest_schema_deserializes_realtime_fields():
    raw_payload = {
        "batch_id": str(uuid4()),
        "upload_session_id": str(uuid4()),
        "local_seq": 1,
        "assignment_epoch": 1,
        "device_code": "DEV1",
        "device_type": "s7",
        "group_code": "GRP1",
        "scan_rate": 100,
        "ts": 1000,
        "config_version": 1,
        "values": {"1": "12.5"},
        "latest_values": {"1": "12.5", "str_var": "ALARM"},
        "latest_qualities": {"1": 192, "str_var": 192},
    }
    item = UploadItem.from_dict(raw_payload)
    assert item.values == {"1": "12.5"}
    assert item.latest_values == {"1": "12.5", "str_var": "ALARM"}
    assert item.latest_qualities == {"1": 192, "str_var": 192}
