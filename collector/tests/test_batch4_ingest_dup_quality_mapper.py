"""审查批次 4 回归：ingest_server 重复样本（B041）、OPC 质量（B042）、跨设备 ItemID（B045）。"""

from datetime import datetime, timezone
from unittest.mock import MagicMock

import pytest

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.opcda_mapper import OpcDaTagMapper
from core.opcda_quality import map_opcda_quality
from core.quality_codes import QUALITY_BAD, QUALITY_GOOD, QUALITY_UNCERTAIN
from data_queue.types import DataPoint
from monitor.ingest_server import _IngestHTTPServer, _IngestRequestHandler, _map_opcda_quality


def _ts(seconds: int) -> datetime:
    return datetime(2026, 1, 1, 0, 0, seconds, tzinfo=timezone.utc)


def _ms(seconds: int) -> int:
    return int(_ts(seconds).timestamp() * 1000)


def _server(redis_writer, variables=None, devices=None):
    config = CollectorConfig(
        devices=devices or [DeviceConfig("EXT001", "EXT", "http", "127.0.0.1")],
        variables=variables or [
            VariableConfig("v", "EXT001", "A.v", "REAL", 1, True, 1, group_code="g001"),
            VariableConfig("w", "EXT001", "A.w", "REAL", 1, True, 2, group_code="g001"),
        ],
    )
    return _IngestHTTPServer(
        ("127.0.0.1", 0),
        _IngestRequestHandler,
        ingest_path="/ingest",
        api_key=None,
        max_batch_size=100,
        redis_writer=redis_writer,
        realtime_config=config,
    )


def _stream_writes(redis_writer):
    """[(frame_ts, stream_values)]，按调用顺序。"""
    return [(c.args[4], dict(c.args[7])) for c in redis_writer.write_window_stream.call_args_list]


# ---------------- B041 ----------------

def test_same_variable_different_timestamps_each_persisted_and_latest_is_newest():
    redis_writer = MagicMock()
    server = _server(redis_writer)
    try:
        accepted, errors, failed = server.write_points_to_redis_detailed([
            DataPoint("EXT001", "v", 1, timestamp=_ts(1)),
            DataPoint("EXT001", "v", 2, timestamp=_ts(2)),
        ])
    finally:
        server.server_close()
    assert (accepted, errors, failed) == (2, [], [])
    assert _stream_writes(redis_writer) == [(_ms(1), {"v": "1"}), (_ms(2), {"v": "2"})]
    final_latest = redis_writer.write_latest.call_args_list[-1].args[2]
    assert final_latest["v"] == {"v": "2", "t": _ms(2), "q": 0}


def test_out_of_order_samples_persist_in_time_order_and_latest_is_newest_ts():
    redis_writer = MagicMock()
    server = _server(redis_writer)
    try:
        accepted, errors, failed = server.write_points_to_redis_detailed([
            DataPoint("EXT001", "v", 3, timestamp=_ts(3)),
            DataPoint("EXT001", "w", 9, timestamp=_ts(3)),
            DataPoint("EXT001", "v", 1, timestamp=_ts(1)),
        ])
    finally:
        server.server_close()
    assert (accepted, errors, failed) == (3, [], [])
    # 每个样本都落了一次 stream；v 的样本按时间升序
    writes = _stream_writes(redis_writer)
    assert [values.get("v") for _, values in writes] == ["1", "3"]
    assert sum(len(values) for _, values in writes) == 3
    final_latest = redis_writer.write_latest.call_args_list[-1].args[2]
    assert final_latest["v"]["v"] == "3" and final_latest["v"]["t"] == _ms(3)
    assert final_latest["w"]["v"] == "9"


def test_same_variable_same_timestamp_duplicate_not_falsely_acked():
    redis_writer = MagicMock()
    server = _server(redis_writer)
    try:
        accepted, errors, failed = server.write_points_to_redis_detailed([
            DataPoint("EXT001", "v", 1, timestamp=_ts(1)),
            DataPoint("EXT001", "v", 2, timestamp=_ts(1)),
        ])
    finally:
        server.server_close()
    assert accepted == 1
    assert failed == [0]
    assert len(errors) == 1 and "points[0]" in errors[0] and "duplicate" in errors[0]
    assert _stream_writes(redis_writer) == [(_ms(1), {"v": "2"})]


def test_later_subframe_failure_does_not_ack_or_advance_latest():
    redis_writer = MagicMock()
    redis_writer.write_window_stream.side_effect = [None, RuntimeError("boom")]
    server = _server(redis_writer)
    try:
        accepted, errors, failed = server.write_points_to_redis_detailed([
            DataPoint("EXT001", "v", 1, timestamp=_ts(1)),
            DataPoint("EXT001", "v", 2, timestamp=_ts(2)),
        ])
    finally:
        server.server_close()
    assert accepted == 1 and failed == [1]
    assert redis_writer.write_latest.call_count == 1
    assert redis_writer.write_latest.call_args.args[2]["v"]["v"] == "1"


# ---------------- B042 ----------------

@pytest.mark.parametrize(
    "raw, quality_good",
    [(0x00, False), (0x40, False), (0x80, False), (0xC0, True), (0x0140, False)],
)
def test_sidecar_shape_quality_matches_ipc_mapping(raw, quality_good):
    """真实 sidecar 形状同时带 quality 与 quality_good：HTTP 结果须与 IPC map_opcda_quality 一致。"""
    assert _map_opcda_quality({"quality": raw, "quality_good": quality_good}) == map_opcda_quality(raw)


def test_bad_quality_stays_bad_even_if_quality_good_true():
    assert _map_opcda_quality({"quality": 0, "quality_good": True}) == QUALITY_BAD


def test_legacy_payload_without_raw_quality_falls_back_to_bool():
    assert _map_opcda_quality({"quality_good": True}) == QUALITY_GOOD
    assert _map_opcda_quality({"quality_good": False}) == QUALITY_UNCERTAIN
    assert _map_opcda_quality({}) == QUALITY_GOOD


def test_raw_quality_rejects_bool_and_float():
    with pytest.raises(ValueError):
        _map_opcda_quality({"quality": True})
    with pytest.raises(ValueError):
        _map_opcda_quality({"quality": 192.0})


# ---------------- B045 ----------------

def _two_device_vars(order):
    a = VariableConfig("A1", "DEV_A", "Channel1.Device1.Tag1", "REAL", 1, True, 1)
    b = VariableConfig("B1", "DEV_B", "Channel1.Device1.Tag1", "REAL", 1, True, 1)
    u = VariableConfig("A2", "DEV_A", "Channel1.Device1.Unique", "REAL", 1, True, 2)
    return [a, b, u] if order else [b, a, u]


@pytest.mark.parametrize("order", [True, False])
def test_mapper_same_item_id_on_two_devices_routes_per_device(order):
    mapper = OpcDaTagMapper(_two_device_vars(order))
    assert mapper.get_variable_id("Channel1.Device1.Tag1", "DEV_A") == "A1"
    assert mapper.get_variable_id("Channel1.Device1.Tag1", "DEV_B") == "B1"
    assert mapper.mapping_count == 3
    # 未指明设备：重名 ItemID 不得静默路由到任一设备
    assert mapper.get_variable_id("Channel1.Device1.Tag1") is None
    assert mapper.is_ambiguous("Channel1.Device1.Tag1")
    # 全局唯一 ItemID 仍兼容无 device_id 的旧客户端
    assert mapper.get_variable_id("Channel1.Device1.Unique") == "A2"
    assert not mapper.is_ambiguous("Channel1.Device1.Unique")


def test_opcda_endpoint_routes_duplicate_item_ids_per_device_and_rejects_ambiguous():
    import json
    import threading
    from http.client import HTTPConnection

    redis_writer = MagicMock()
    variables = _two_device_vars(True)
    devices = [
        DeviceConfig("DEV_A", "AGV", "opcda_sidecar", "127.0.0.1"),
        DeviceConfig("DEV_B", "AGV", "opcda_sidecar", "127.0.0.1"),
    ]
    server = _server(redis_writer, variables=variables, devices=devices)
    server.opcda_mapper = OpcDaTagMapper(variables)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        payload = {"schema": "opcda_tag_batch_v1", "points": [
            {"device_id": "DEV_A", "item_id": "Channel1.Device1.Tag1", "value": 1, "quality": 192},
            {"device_id": "DEV_B", "item_id": "Channel1.Device1.Tag1", "value": 2, "quality": 192},
            {"item_id": "Channel1.Device1.Tag1", "value": 3, "quality": 192},
        ]}
        body = json.dumps(payload).encode("utf-8")
        conn = HTTPConnection("127.0.0.1", server.server_address[1], timeout=2)
        conn.request("POST", "/ingest/opcda-tags", body=body,
                     headers={"Content-Type": "application/json", "Content-Length": str(len(body))})
        resp = conn.getresponse()
        result = json.loads(resp.read().decode("utf-8"))
        conn.close()
    finally:
        server.shutdown()
        server.server_close()
    assert resp.status == 200, result
    assert result["accepted"] == 2
    assert result["rejected_indices"] == [2]
    assert any("ambiguous" in e for e in result["errors"])
    written = {c.args[0]: dict(c.args[7]) for c in redis_writer.write_window_stream.call_args_list}
    assert written == {"DEV_A": {"A1": "1"}, "DEV_B": {"B1": "2"}}
