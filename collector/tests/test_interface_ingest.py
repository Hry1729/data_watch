"""外部接口采集协议测试"""

from datetime import datetime

import pytest

from monitor.ingest_server import _map_opcda_quality, _normalize_payload, _point_from_dict


def test_normalize_payload_single_point():
    payload = {
        "device_id": "EXT001",
        "variable_id": "temperature",
        "value": 36.5,
    }
    points = _normalize_payload(payload, max_batch_size=10)
    assert len(points) == 1
    assert points[0]["device_id"] == "EXT001"


def test_normalize_payload_batch():
    payload = {
        "points": [
            {"device_id": "EXT001", "variable_id": "a", "value": 1},
            {"device_id": "EXT001", "variable_id": "b", "value": 2},
        ]
    }
    points = _normalize_payload(payload, max_batch_size=10)
    assert len(points) == 2


def test_normalize_payload_batch_size_limit():
    payload = [{"device_id": "EXT001", "variable_id": "a", "value": 1}] * 3
    with pytest.raises(ValueError):
        _normalize_payload(payload, max_batch_size=2)


def test_point_from_dict_with_iso_timestamp():
    item = {
        "device_id": "EXT001",
        "variable_id": "pressure",
        "value": 1.23,
        "timestamp": "2026-04-05T12:00:00+08:00",
    }
    point = _point_from_dict(item)
    assert point.device_id == "EXT001"
    assert point.variable_id == "pressure"
    assert point.value == 1.23
    assert point.quality == 0
    assert isinstance(point.timestamp, datetime)


def test_point_from_dict_accepts_quality_code():
    point = _point_from_dict({
        "device_id": "EXT001",
        "variable_id": "pressure",
        "value": 1.23,
        "quality": 4,
    })

    assert point.quality == 4


def test_point_from_dict_rejects_invalid_quality_code():
    with pytest.raises(ValueError, match="quality"):
        _point_from_dict({
            "device_id": "EXT001",
            "variable_id": "pressure",
            "value": 1.23,
            "quality": 192,
        })


def test_opcda_quality_maps_raw_quality_to_internal_code():
    assert _map_opcda_quality({"quality": 192}) == 0
    assert _map_opcda_quality({"quality_good": False, "quality": 192}) == 2


def test_point_from_dict_requires_fields():
    with pytest.raises(ValueError):
        _point_from_dict({"device_id": "EXT001", "value": 1})
