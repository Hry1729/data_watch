from __future__ import annotations

from uuid import uuid4

import pytest

from ingest.app.schemas import RequestValidationError, UploadBatch


def base_payload() -> dict:
    return {
        "node_uuid": str(uuid4()),
        "request_id": str(uuid4()),
        "items": [
            {
                "batch_id": str(uuid4()),
                "upload_session_id": str(uuid4()),
                "local_seq": 1,
                "assignment_epoch": 1,
                "device_code": "QC100",
                "device_type": "qc",
                "group_code": "g001",
                "scan_rate": 100,
                "ts": 1721800000000,
                "config_version": 1,
                "values": {"1": "12.5"},
            }
        ],
    }


def test_identity_error_is_request_level_bad_request() -> None:
    payload = base_payload()
    payload["items"][0]["local_seq"] = 0
    with pytest.raises(RequestValidationError, match="local_seq"):
        UploadBatch.from_dict(payload)


def test_envelope_error_is_preserved_for_rejected_consumed() -> None:
    payload = base_payload()
    payload["items"][0]["scan_rate"] = "bad"
    batch = UploadBatch.from_dict(payload)
    assert "scan_rate" in (batch.items[0].envelope_error or "")


def test_batch_rejects_mixed_sessions() -> None:
    payload = base_payload()
    second = dict(payload["items"][0])
    second["batch_id"] = str(uuid4())
    second["upload_session_id"] = str(uuid4())
    second["local_seq"] = 2
    payload["items"].append(second)
    with pytest.raises(RequestValidationError, match="one upload_session"):
        UploadBatch.from_dict(payload)


def test_quality_only_latest_keys_are_accepted() -> None:
    """高-3：坏质量无值的点只在 latest_qualities/latest_timestamps 中出现。"""
    payload = base_payload()
    item = payload["items"][0]
    item["latest_values"] = {"1": "12.5"}
    item["latest_qualities"] = {"1": 0, "2": 8}
    item["latest_timestamps"] = {"1": 1721800000000, "2": 1721800000000}
    batch = UploadBatch.from_dict(payload)
    assert batch.items[0].envelope_error is None
    assert batch.items[0].latest_qualities == {"1": 0, "2": 8}


def test_latest_qualities_without_timestamps_are_accepted() -> None:
    """旧载荷：只有 latest_qualities，没有 latest_timestamps，仍然接受。"""
    payload = base_payload()
    item = payload["items"][0]
    item["latest_values"] = {"1": "12.5", "str_var": "ALARM"}
    item["latest_qualities"] = {"1": 192, "str_var": 192}
    batch = UploadBatch.from_dict(payload)
    parsed = batch.items[0]
    assert parsed.envelope_error is None
    assert parsed.latest_timestamps is None
    assert parsed.latest_qualities == {"1": 192, "str_var": 192}
    assert parsed.latest_values == {"1": "12.5", "str_var": "ALARM"}


def test_latest_values_must_be_covered_by_qualities() -> None:
    payload = base_payload()
    item = payload["items"][0]
    item["latest_values"] = {"1": "12.5", "3": "x"}
    item["latest_qualities"] = {"1": 0}
    item["latest_timestamps"] = {"1": 1721800000000}
    batch = UploadBatch.from_dict(payload)
    assert "latest_values" in (batch.items[0].envelope_error or "")
