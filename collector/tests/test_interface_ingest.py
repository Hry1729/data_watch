"""外部接口采集协议测试"""

from datetime import datetime
from unittest.mock import MagicMock

import pytest

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from data_queue.types import DataPoint
from monitor.ingest_server import (
    _IngestHTTPServer,
    _IngestRequestHandler,
    _map_opcda_quality,
    _normalize_payload,
    _point_from_dict,
)


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


def test_ingest_server_writes_points_to_redis_with_current_schema():
    redis_writer = MagicMock()
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="AGV801",
                device_type="AGV",
                protocol="opcda_sidecar",
                ip="127.0.0.1",
                config_version=7,
            )
        ],
        variables=[
            VariableConfig(
                variable_id="30041800",
                device_id="AGV801",
                address="OPD_Server.AGV801.ViewMain.Tag30041800",
                data_type="REAL",
                scan_rate=0.1,
                is_storage=True,
                var_seq=1,
                group_code="g001",
            )
        ],
    )
    server = _IngestHTTPServer(
        ("127.0.0.1", 0),
        _IngestRequestHandler,
        ingest_path="/ingest",
        api_key=None,
        max_batch_size=100,
        redis_writer=redis_writer,
        realtime_config=config,
    )
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "30041800", 12.5, quality=4)
        ])
    finally:
        server.server_close()

    assert accepted == 1
    assert errors == []
    redis_writer.write_latest.assert_called_once()
    assert redis_writer.write_latest.call_args.args[0:2] == ("AGV801", "g001")
    latest_fields = redis_writer.write_latest.call_args.args[2]
    assert latest_fields["30041800"]["v"] == "12.5"
    assert latest_fields["30041800"]["q"] == 4
    redis_writer.write_window.assert_called_once()
    redis_writer.write_stream.assert_called_once_with(
        "AGV801",
        "agv",
        "g001",
        100,
        latest_fields["30041800"]["t"],
        7,
        {"30041800": "12.5"},
    )
