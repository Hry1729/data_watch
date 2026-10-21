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


def _merged_latest(redis_writer):
    """模拟 Redis HSET：按调用顺序合并 write_latest 的 fields。"""
    merged = {}
    for call in redis_writer.write_latest.call_args_list:
        merged.update(call.args[2])
    return merged


def test_codex_example_each_value_keeps_its_own_timestamp():
    """PR #27 Codex P1：a@1,a@100,b@50,b@60 不得按序号拼帧改写时间（a@1→a@50、b@60→b@100）。"""
    redis_writer = MagicMock()
    server = _server(redis_writer, variables=[
        VariableConfig("a", "EXT001", "A.a", "REAL", 1, True, 1, group_code="g001"),
        VariableConfig("b", "EXT001", "A.b", "REAL", 1, True, 2, group_code="g001"),
    ])
    try:
        accepted, errors, failed = server.write_points_to_redis_detailed([
            DataPoint("EXT001", "a", 1, timestamp=_ts(1)),
            DataPoint("EXT001", "a", 100, timestamp=_ts(100 % 60)),
            DataPoint("EXT001", "b", 50, timestamp=_ts(50)),
            DataPoint("EXT001", "b", 60, timestamp=_ts(59)),
        ])
    finally:
        server.server_close()
    assert (accepted, errors, failed) == (4, [], [])
    # 每个不同时间戳一帧，帧 ts 即样本 ts，帧内只有该时间戳的值
    a100_ts = _ms(100 % 60)
    expected = sorted(
        [(_ms(1), {"a": "1"}), (a100_ts, {"a": "100"}), (_ms(50), {"b": "50"}), (_ms(59), {"b": "60"})]
    )
    assert _stream_writes(redis_writer) == expected
    for call in redis_writer.write_window_stream.call_args_list:
        frame = call.args[6]
        assert frame["ts"] == call.args[4]
        assert set(frame["values"]) == set(call.args[7])
    # 每次 latest 只带本帧样本，t 即样本自身时间戳
    for call, (ts_ms, values) in zip(redis_writer.write_latest.call_args_list, expected):
        assert {k: v["t"] for k, v in call.args[2].items()} == {k: ts_ms for k in values}
    merged = _merged_latest(redis_writer)
    assert merged["a"] == {"v": "100", "t": a100_ts, "q": 0}
    assert merged["b"] == {"v": "60", "t": _ms(59), "q": 0}


def test_codex_example_true_ordering_with_millisecond_timestamps():
    """同上，直接用 Codex 的毫秒时间戳 1/100/50/60。"""
    from datetime import timedelta

    base = datetime(2026, 1, 1, tzinfo=timezone.utc)
    base_ms = int(base.timestamp() * 1000)
    redis_writer = MagicMock()
    server = _server(redis_writer, variables=[
        VariableConfig("a", "EXT001", "A.a", "REAL", 1, True, 1, group_code="g001"),
        VariableConfig("b", "EXT001", "A.b", "REAL", 1, True, 2, group_code="g001"),
    ])
    try:
        accepted, errors, failed = server.write_points_to_redis_detailed([
            DataPoint("EXT001", "a", "a1", timestamp=base + timedelta(milliseconds=1)),
            DataPoint("EXT001", "a", "a100", timestamp=base + timedelta(milliseconds=100)),
            DataPoint("EXT001", "b", "b50", timestamp=base + timedelta(milliseconds=50)),
            DataPoint("EXT001", "b", "b60", timestamp=base + timedelta(milliseconds=60)),
        ])
    finally:
        server.server_close()
    assert (accepted, errors, failed) == (4, [], [])
    assert _stream_writes(redis_writer) == [
        (base_ms + 1, {"a": "a1"}),
        (base_ms + 50, {"b": "b50"}),
        (base_ms + 60, {"b": "b60"}),
        (base_ms + 100, {"a": "a100"}),
    ]
    merged = _merged_latest(redis_writer)
    assert merged["a"]["t"] == base_ms + 100 and merged["a"]["v"] == "a100"
    assert merged["b"]["t"] == base_ms + 60 and merged["b"]["v"] == "b60"


def test_latest_never_ahead_of_persisted_when_a_frame_fails():
    """b@50 的帧写失败：latest 里 b 不得出现（未落库），a 只到已落库的 a@1。"""
    redis_writer = MagicMock()
    # 帧顺序 ts=1(a), ts=50(b) 失败, ts=100(a) 失败
    redis_writer.write_window_stream.side_effect = [None, RuntimeError("boom"), RuntimeError("boom")]
    server = _server(redis_writer, variables=[
        VariableConfig("a", "EXT001", "A.a", "REAL", 1, True, 1, group_code="g001"),
        VariableConfig("b", "EXT001", "A.b", "REAL", 1, True, 2, group_code="g001"),
    ])
    try:
        accepted, errors, failed = server.write_points_to_redis_detailed([
            DataPoint("EXT001", "a", 1, timestamp=_ts(1)),
            DataPoint("EXT001", "a", 100, timestamp=_ts(40)),
            DataPoint("EXT001", "b", 50, timestamp=_ts(20)),
        ])
    finally:
        server.server_close()
    assert accepted == 1 and failed == [1, 2]
    assert _merged_latest(redis_writer) == {"a": {"v": "1", "t": _ms(1), "q": 0}}


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


# ---------------- PR #27 R2：OPC DA device_id 大小写不敏感 ----------------

def test_mapper_device_id_is_case_insensitive():
    mapper = OpcDaTagMapper(_two_device_vars(True))
    assert mapper.get_variable_id("Channel1.Device1.Tag1", "dev_a") == "A1"
    assert mapper.get_variable_id("Channel1.Device1.Tag1", " Dev_B ") == "B1"
    assert mapper.canonical_device_id("dev_a") == "DEV_A"
    assert mapper.get_device_variables("dev_a") == ["A1", "A2"]
    assert mapper.get_variable_id("Channel1.Device1.Tag1", "dev_c") is None


def test_mapper_device_ids_differing_only_by_case_require_exact_match():
    mapper = OpcDaTagMapper([
        VariableConfig("X1", "AGV1", "Ch.Tag", "REAL", 1, True, 1),
        VariableConfig("Y1", "agv1", "Ch.Tag", "REAL", 1, True, 1),
    ])
    assert mapper.get_variable_id("Ch.Tag", "AGV1") == "X1"
    assert mapper.get_variable_id("Ch.Tag", "agv1") == "Y1"
    # 折叠后冲突：不猜
    assert mapper.get_variable_id("Ch.Tag", "Agv1") is None
    assert mapper.canonical_device_id("Agv1") is None


def test_opcda_endpoint_accepts_device_id_with_different_case():
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
            {"device_id": "dev_a", "item_id": "Channel1.Device1.Tag1", "value": 1, "quality": 192},
            {"device_id": "Dev_B", "item_id": "Channel1.Device1.Tag1", "value": 2, "quality": 192},
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
    assert result["accepted"] == 2, result
    assert result["errors"] == []
    written = {c.args[0]: dict(c.args[7]) for c in redis_writer.write_window_stream.call_args_list}
    assert written == {"DEV_A": {"A1": "1"}, "DEV_B": {"B1": "2"}}
