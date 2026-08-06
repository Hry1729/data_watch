from __future__ import annotations

import json

import pytest

from data_watch_shared.stream_envelope import (
    build_latest_updates,
    build_stream_fields,
    decode_latest_value,
    latest_key,
    shard_number,
    stream_key,
    validate_envelope,
)


def test_keys_use_frozen_contract() -> None:
    shard = shard_number("qc100", 4)
    assert stream_key("qc100", "qc", "g001", 4) == f"rt:stream:qc:g001:{shard}"
    assert latest_key("qc100", "g001") == "rt:latest:qc100:g001"


def test_build_v1_stream_fields() -> None:
    fields = build_stream_fields(
        device_code="qc100", device_type="qc", group_code="g001",
        scan_rate_ms=100, ts=1721800000000, config_version=12,
        values={"1": "12.5"}, validate=True,
    )
    assert "contract_version" not in fields
    assert json.loads(fields["values"]) == {"1": "12.5"}


def test_build_v2_stream_fields() -> None:
    fields = build_stream_fields(
        device_code="qc100", device_type="qc", group_code="g001",
        scan_rate_ms=100, ts=1721800000000, config_version=12,
        values={"1": "12.5"}, qualities={"1": 0}, validate=True,
    )
    assert fields["contract_version"] == "2"
    assert json.loads(fields["qualities"]) == {"1": 0}


def test_v2_requires_matching_quality_keys() -> None:
    with pytest.raises(ValueError, match="keys must match"):
        validate_envelope(
            device_code="qc100", device_type="qc", group_code="g001",
            scan_rate_ms=100, ts=1, config_version=1, values={"1": "12.5"},
            contract_version=2, qualities={"2": 0},
        )


def test_latest_epoch_and_legacy_decode() -> None:
    updates = build_latest_updates(
        {"1": "12.5"}, ts=1721800000000, assignment_epoch=5, qualities={"1": 192}
    )
    assert updates == {"1": {"v": "12.5", "t": 1721800000000, "q": 192, "e": 5}}
    assert decode_latest_value(b'{"v":"old","t":1,"q":0}') == {
        "v": "old", "t": 1, "q": 0, "e": 0,
    }
