"""外部接口采集协议测试

P1-2b：_IngestHTTPServer.write_points_to_redis 的多段 Redis 写入
（window / stream / latest）非原子问题。修复点：

1. 写顺序：window -> stream -> latest。latest 必须最后写，
   否则 stream/window 失败时 latest 已被刷新，客户端据此误判批次已落库。
2. 每段失败都通过返回值 errors 显式上抛，调用方据此降级 / 重试，
   不再被静默当整批成功。

本测试文件覆盖：
- 既有契约（成功路径、点位校验、热重载快照）。
- 失败语义：stream / window / latest 任一段失败时的写入次数与 errors 列表。
- 顺序回归：write_latest 必须在 write_window 与 write_stream 之后被调用。
"""

import json
import threading
from datetime import datetime, timezone
from http.client import HTTPConnection
from unittest.mock import MagicMock

import pytest

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from data_queue.types import DataPoint
from monitor.ingest_server import (
    _IngestHTTPServer,
    _IngestRequestHandler,
    _map_opcda_quality,
    _normalize_payload,
    _normalize_point_value,
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


@pytest.mark.parametrize("raw, expected", [(True, "1"), (False, "0")])
def test_external_boolean_value_is_normalized_for_stream(raw, expected):
    point = _point_from_dict({
        "device_id": "EXT001",
        "variable_id": "running",
        "value": raw,
    })

    assert point.value == expected
    assert _normalize_point_value(raw) == expected


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
            "quality": 256,
        })


def test_opcda_quality_maps_raw_quality_to_internal_code():
    assert _map_opcda_quality({"quality": 192}) == 0
    assert _map_opcda_quality({"quality": 64}) == 2
    assert _map_opcda_quality({"quality": 0}) == 5
    assert _map_opcda_quality({"quality": 128}) == 8
    # 高 8 位为 vendor-specific，只能按低字节主质量位判断。
    assert _map_opcda_quality({"quality": 0x0140}) == 2
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
                group_config_version=7,
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
        {"30041800": 4},
    )


def test_frame_timestamp_uses_max_point_timestamp_for_stream_and_window():
    """同一 frame 的宽行时间戳取点位时间戳最大值，latest 仍保留点位时间。"""
    redis_writer = MagicMock()
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="AGV801",
                device_type="AGV",
                protocol="opcda_sidecar",
                ip="127.0.0.1",
            )
        ],
        variables=[
            VariableConfig(
                variable_id="fast",
                device_id="AGV801",
                address="OPD_Server.AGV801.ViewMain.Fast",
                data_type="REAL",
                scan_rate=0.1,
                is_storage=True,
                var_seq=1,
                group_code="g001",
            ),
            VariableConfig(
                variable_id="slow",
                device_id="AGV801",
                address="OPD_Server.AGV801.ViewMain.Slow",
                data_type="REAL",
                scan_rate=0.1,
                is_storage=True,
                var_seq=2,
                group_code="g001",
            ),
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
    earlier = datetime(2026, 4, 5, 12, 0, 0, 100000, tzinfo=timezone.utc)
    later = datetime(2026, 4, 5, 12, 0, 0, 900000, tzinfo=timezone.utc)
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "fast", 1.0, timestamp=earlier),
            DataPoint("AGV801", "slow", 2.0, timestamp=later),
        ])
    finally:
        server.server_close()

    assert accepted == 2
    assert errors == []
    frame_ts = int(later.timestamp() * 1000)
    assert redis_writer.write_window.call_args.args[3]["ts"] == frame_ts
    assert redis_writer.write_stream.call_args.args[4] == frame_ts
    assert redis_writer.write_latest.call_args.args[2]["fast"]["t"] == int(earlier.timestamp() * 1000)
    assert redis_writer.write_latest.call_args.args[2]["slow"]["t"] == frame_ts


def test_ingest_server_update_metadata_refreshes_snapshot():
    """热重载后新增点位应被接受，旧快照不再拒绝它。"""
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
        # 重载前：新点位 99999 未配置，应被拒绝
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "99999", 1.0)
        ])
        assert accepted == 0
        assert errors and "not configured" in errors[0]

        # 模拟热重载：新增点位 99999
        new_variables = list(config.variables) + [
            VariableConfig(
                variable_id="99999",
                device_id="AGV801",
                address="OPD_Server.AGV801.ViewMain.Tag99999",
                data_type="REAL",
                scan_rate=0.1,
                is_storage=True,
                var_seq=2,
                group_code="g001",
            )
        ]
        server.update_metadata(config.devices, new_variables)

        # 重载后：新点位被接受
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "99999", 1.0)
        ])
        assert accepted == 1
        assert errors == []
    finally:
        server.server_close()


# ---------------------------------------------------------------------------
# P1-2b: write_points_to_redis 多段写入失败语义（与 HRY-54 / test_executor.py 对称）
# ---------------------------------------------------------------------------


def _make_ingest_server(redis_writer):
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
    return server


class _FakeOpcDaMapper:
    mapping_count = 1

    def get_variable_id(self, item_id):
        if item_id == "OPD_Server.AGV801.ViewMain.TagRunning":
            return "running"
        return None


def _post_opcda_payload(server, payload):
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    connection = HTTPConnection("127.0.0.1", server.server_address[1], timeout=2)
    try:
        body = json.dumps(payload).encode("utf-8")
        connection.request(
            "POST",
            "/ingest/opcda-tags",
            body=body,
            headers={"Content-Type": "application/json", "Content-Length": str(len(body))},
        )
        response = connection.getresponse()
        return response.status, json.loads(response.read().decode("utf-8"))
    finally:
        connection.close()
        server.shutdown()
        thread.join(timeout=2)
        server.server_close()


def _make_opcda_ingest_server(redis_writer):
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="AGV801",
                device_type="AGV",
                protocol="opcda_sidecar",
                ip="127.0.0.1",
            )
        ],
        variables=[
            VariableConfig(
                variable_id="running",
                device_id="AGV801",
                address="OPD_Server.AGV801.ViewMain.TagRunning",
                data_type="BOOL",
                scan_rate=0.1,
                is_storage=True,
                var_seq=1,
                group_code="g001",
            )
        ],
    )
    return _IngestHTTPServer(
        ("127.0.0.1", 0),
        _IngestRequestHandler,
        ingest_path="/ingest",
        api_key=None,
        max_batch_size=100,
        opcda_mapper=_FakeOpcDaMapper(),
        redis_writer=redis_writer,
        realtime_config=config,
    )


def test_opcda_response_reports_partial_failure_and_full_failure_status():
    redis_writer = MagicMock()
    server = _make_opcda_ingest_server(redis_writer)
    status, body = _post_opcda_payload(server, {
        "schema": "opcda_tag_batch_v1",
        "batch_id": "batch-1",
        "points": [
            {
                "item_id": "OPD_Server.AGV801.ViewMain.TagRunning",
                "device_id": "AGV801",
                "value": True,
                "quality": 192,
            },
            {
                "item_id": "OPD_Server.AGV801.ViewMain.TagMissing",
                "device_id": "AGV801",
                "value": False,
                "quality": 192,
            },
        ],
    })

    assert status == 200
    assert body["batch_id"] == "batch-1"
    assert body["accepted"] == 1
    assert body["rejected"] == 1
    assert body["rejected_indices"] == [1]
    assert body["errors"]
    stream_values = redis_writer.write_stream.call_args.args[6]
    assert stream_values == {"running": "1"}

    server = _make_opcda_ingest_server(MagicMock())
    status, body = _post_opcda_payload(server, {
        "schema": "opcda_tag_batch_v1",
        "batch_id": "batch-2",
        "points": [{
            "item_id": "OPD_Server.AGV801.ViewMain.TagMissing",
            "device_id": "AGV801",
            "value": False,
            "quality": 192,
        }],
    })

    assert status == 400
    assert body["batch_id"] == "batch-2"
    assert body["accepted"] == 0
    assert body["rejected"] == 1
    assert body["rejected_indices"] == [0]
    assert body["errors"]


def test_ingest_server_redis_stream_failure_does_not_refresh_latest():
    """stream 失败 → 错误计为一次，write_latest 未被调用（latest 不被假装推进）。"""
    redis_writer = MagicMock()
    redis_writer.write_stream.side_effect = RuntimeError("stream write failed")
    server = _make_ingest_server(redis_writer)
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "30041800", 12.5, quality=4)
        ])
    finally:
        server.server_close()

    assert accepted == 0
    assert len(errors) == 1
    assert "write_stream" in errors[0]
    redis_writer.write_window.assert_called_once()
    redis_writer.write_stream.assert_called_once()
    redis_writer.write_latest.assert_not_called()


def test_ingest_server_redis_window_failure_does_not_call_stream_or_latest():
    """window 失败 → 错误计为一次，stream / latest 都不会被调用。"""
    redis_writer = MagicMock()
    redis_writer.write_window.side_effect = RuntimeError("window write failed")
    server = _make_ingest_server(redis_writer)
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "30041800", 12.5, quality=4)
        ])
    finally:
        server.server_close()

    assert accepted == 0
    assert len(errors) == 1
    assert "write_window" in errors[0]
    redis_writer.write_window.assert_called_once()
    redis_writer.write_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()


def test_ingest_server_redis_latest_failure_reported_in_errors():
    """latest 失败（window/stream 已成功）→ 错误计为一次，window+stream 已落库。"""
    redis_writer = MagicMock()
    redis_writer.write_latest.side_effect = RuntimeError("latest write failed")
    server = _make_ingest_server(redis_writer)
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "30041800", 12.5, quality=4)
        ])
    finally:
        server.server_close()

    assert accepted == 0
    assert len(errors) == 1
    assert "write_latest" in errors[0]
    redis_writer.write_window.assert_called_once()
    redis_writer.write_stream.assert_called_once()
    redis_writer.write_latest.assert_called_once()


def test_ingest_server_redis_write_order_latest_last():
    """回归：write_latest 必须在 write_window 与 write_stream 之后被调用。"""
    redis_writer = MagicMock()
    parent = MagicMock()
    parent.attach_mock(redis_writer.write_window, "write_window")
    parent.attach_mock(redis_writer.write_stream, "write_stream")
    parent.attach_mock(redis_writer.write_latest, "write_latest")
    server = _make_ingest_server(redis_writer)
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "30041800", 12.5, quality=4)
        ])
    finally:
        server.server_close()

    assert accepted == 1
    assert errors == []
    method_names = [c[0] for c in parent.mock_calls]
    assert method_names.index("write_latest") > method_names.index("write_window")
    assert method_names.index("write_latest") > method_names.index("write_stream")


def test_ingest_server_redis_multi_scan_rate_frame_failure_isolates_latest():
    """同 (device, group) 不同 scan_rate 帧：失败帧的变量不应进入 latest。

    场景：一次 ingest 批次包含两个不同 scan_rate 的帧（A 帧 100ms、B 帧 500ms）。
    B 帧 stream 失败 → B 帧的变量不应被 A 帧的 latest 写入带进去。
    """
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
                variable_id="fast_var",
                device_id="AGV801",
                address="OPD_Server.AGV801.ViewMain.TagFast",
                data_type="REAL",
                scan_rate=0.1,
                is_storage=True,
                var_seq=1,
                group_code="g001",
            ),
            VariableConfig(
                variable_id="slow_var",
                device_id="AGV801",
                address="OPD_Server.AGV801.ViewMain.TagSlow",
                data_type="REAL",
                scan_rate=0.5,
                is_storage=True,
                var_seq=2,
                group_code="g001",
            ),
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

    def stream_side_effect(
        device_code,
        device_type,
        group_code,
        scan_rate_ms,
        ts,
        config_version,
        values,
        qualities,
    ):
        if scan_rate_ms == 500:
            raise RuntimeError("stream write failed for 500ms frame")

    redis_writer.write_stream.side_effect = stream_side_effect

    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "fast_var", 1.0, quality=4),
            DataPoint("AGV801", "slow_var", 2.0, quality=4),
        ])
    finally:
        server.server_close()

    assert accepted == 1
    assert len(errors) == 1
    assert "write_stream" in errors[0]
    assert "500ms" in errors[0]

    assert redis_writer.write_window.call_count == 2
    assert redis_writer.write_stream.call_count == 2
    redis_writer.write_latest.assert_called_once()

    latest_args = redis_writer.write_latest.call_args
    latest_fields = latest_args.args[2]
    assert "fast_var" in latest_fields
    assert "slow_var" not in latest_fields


# ---------------------------------------------------------------------------
# C9: 已配置变量的 device_id 一致性校验（消灭静默误路由）
# ---------------------------------------------------------------------------


def test_ingest_server_rejects_device_id_mismatch():
    """变量已配置且归属 AGV801，客户端传错 device_id → 该点被拒，不写 Redis。"""
    redis_writer = MagicMock()
    server = _make_ingest_server(redis_writer)
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("WRONG_DEV", "30041800", 12.5, quality=4)
        ])
    finally:
        server.server_close()

    assert accepted == 0
    assert len(errors) == 1
    assert "device_id mismatch" in errors[0]
    redis_writer.write_window.assert_not_called()
    redis_writer.write_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()


def test_ingest_server_empty_device_id_falls_back_to_variable_owner():
    """device_id 为空（如 OPC DA 端点）→ 按变量归属 AGV801 回退，正常落库。"""
    redis_writer = MagicMock()
    server = _make_ingest_server(redis_writer)
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("", "30041800", 12.5, quality=4)
        ])
    finally:
        server.server_close()

    assert accepted == 1
    assert errors == []
    redis_writer.write_latest.assert_called_once()
    assert redis_writer.write_latest.call_args.args[0] == "AGV801"


# ---------------------------------------------------------------------------
# C2: ingest 写 stream 的 config_version 用 group_config_version（对齐 writer），
# 不用 device 表的 config_version（与 executor 035a532 同源）
# ---------------------------------------------------------------------------


def test_ingest_stream_uses_group_config_version_not_device_version():
    """device.config_version=1 但 var.group_config_version=7 → stream 帧用 7。"""
    redis_writer = MagicMock()
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="AGV801",
                device_type="AGV",
                protocol="opcda_sidecar",
                ip="127.0.0.1",
                config_version=1,  # device 表 variable_config_version
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
                group_config_version=7,  # device_point_group.config_version
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
    redis_writer.write_stream.assert_called_once()
    # write_stream 第 6 个位置参数（index 5）是 config_version
    assert redis_writer.write_stream.call_args.args[5] == 7


def test_ingest_rejects_frame_from_stale_metadata_generation():
    class GenerationGate:
        def __init__(self):
            self.generation = 1

        def current_generation(self, _device_id):
            return self.generation

        def run_if_current_generation(self, _device_id, generation, action):
            if generation != self.generation:
                return False
            action()
            return True

    redis_writer = MagicMock()
    gate = GenerationGate()
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                device_id="AGV801",
                device_type="AGV",
                protocol="opcda_sidecar",
                ip="127.0.0.1",
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
        device_manager=gate,
    )

    # metadata 快照仍属于 generation=1，但设备已热更新到 generation=2。
    gate.generation = 2
    try:
        accepted, errors = server.write_points_to_redis([
            DataPoint("AGV801", "30041800", 12.5, quality=4)
        ])
    finally:
        server.server_close()

    assert accepted == 0
    assert len(errors) == 1
    assert "stale generation" in errors[0]
    redis_writer.write_window.assert_not_called()
    redis_writer.write_stream.assert_not_called()
    redis_writer.write_latest.assert_not_called()
