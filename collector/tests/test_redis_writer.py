"""RedisWriter（COL-2）单元测试。"""

from __future__ import annotations

import json
import zlib
from types import SimpleNamespace
from unittest.mock import MagicMock, call, patch

import pytest

from storage.redis_writer import RedisWriter
from monitor.performance import PerformanceMetrics


def _make_config(shard_count: int = 4, maxlen: int = 500000) -> SimpleNamespace:
    return SimpleNamespace(
        redis=SimpleNamespace(
            stream_shard_count=shard_count,
            stream_maxlen=maxlen,
        )
    )


@pytest.fixture
def mock_redis():
    r = MagicMock()
    pipe = MagicMock()
    pipe.execute = MagicMock()
    r.pipeline.return_value = pipe
    return r


# ---------------------------------------------------------------------------
# 写入最新值
# ---------------------------------------------------------------------------

class TestWriteLatest:
    def test_hset_called_with_correct_key(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_latest("qc100", "g001", {"1": {"v": "3.14", "t": 1000, "q": 1}})
        mock_redis.hset.assert_called_once()
        key = mock_redis.hset.call_args[0][0]
        assert key == "rt:latest:qc100:g001"

    def test_field_value_is_valid_json(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        fields = {"42": {"v": "1.5", "t": 12345, "q": 1}}
        writer.write_latest("qc100", "g001", fields)
        mapping = mock_redis.hset.call_args[1]["mapping"]
        assert isinstance(mapping["42"], bytes)
        parsed = json.loads(mapping["42"])
        assert parsed == {"v": "1.5", "t": 12345, "q": 1}

    def test_no_ttl_set(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_latest("qc100", "g001", {})
        mock_redis.expire.assert_not_called()


# ---------------------------------------------------------------------------
# 写入窗口
# ---------------------------------------------------------------------------

class TestWriteWindow:
    def test_lpush_and_ltrim_called(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_window("qc100", "g001", 100, {"ts": 1000, "values": {}})
        pipe = mock_redis.pipeline.return_value
        assert pipe.lpush.called
        encoded_frame = pipe.lpush.call_args[0][1]
        assert isinstance(encoded_frame, bytes)
        assert json.loads(encoded_frame) == {"ts": 1000, "values": {}}
        assert pipe.ltrim.called
        pipe.execute.assert_called_once()

    def test_ltrim_keeps_max_10(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_window("qc100", "g001", 100, {})
        pipe = mock_redis.pipeline.return_value
        ltrim_args = pipe.ltrim.call_args[0]
        # ltrim(key, 0, 9) 保留索引 0..9 = 10 个项目
        assert ltrim_args[1] == 0
        assert ltrim_args[2] == 9

    def test_key_includes_scan_rate(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_window("asc7011", "g001", 500, {})
        pipe = mock_redis.pipeline.return_value
        key = pipe.lpush.call_args[0][0]
        assert key == "rt:window:asc7011:g001:500"


# ---------------------------------------------------------------------------
# 写入流
# ---------------------------------------------------------------------------

class TestWriteStream:
    def test_xadd_called_with_shard_key(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config(shard_count=4))
        shard_no = zlib.crc32(b"qc100") % 4
        writer.write_stream("qc100", "qc", "g001", 100, 1700000000000, 1, {"1": "3.14"})
        mock_redis.xadd.assert_called_once()
        key = mock_redis.xadd.call_args[0][0]
        assert key == f"rt:stream:qc:g001:{shard_no}"

    def test_xadd_entry_contains_required_fields(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_stream("qc100", "qc", "g001", 100, 1700000000000, 2, {"5": "9.9"})
        entry = mock_redis.xadd.call_args[0][1]
        assert entry["device_code"] == "qc100"
        assert entry["device_type"] == "qc"
        assert entry["group_code"] == "g001"
        assert entry["config_version"] == "2"
        assert isinstance(entry["values"], bytes)
        parsed_values = json.loads(entry["values"])
        assert parsed_values == {"5": "9.9"}
        assert "contract_version" not in entry
        assert "qualities" not in entry

    def test_xadd_entry_contains_v2_quality_fields(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_stream(
            "qc100",
            "qc",
            "g001",
            100,
            1700000000000,
            2,
            {"5": "9.9"},
            {"5": 2},
        )

        entry = mock_redis.xadd.call_args[0][1]
        assert entry["contract_version"] == "2"
        assert json.loads(entry["qualities"]) == {"5": 2}

    def test_xadd_uses_configured_maxlen(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config(maxlen=200000))
        writer.write_stream("qc100", "qc", "g001", 100, 0, 1, {})
        kwargs = mock_redis.xadd.call_args[1]
        assert kwargs["maxlen"] == 200000
        assert kwargs["approximate"] is True


# ---------------------------------------------------------------------------
# write_window_stream（§7.5：window+stream 合并一个 pipeline）
# ---------------------------------------------------------------------------

class TestWriteWindowStream:
    def test_window_and_stream_in_single_pipeline(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config(shard_count=4))
        shard_no = zlib.crc32(b"qc100") % 4
        writer.write_window_stream(
            "qc100", "qc", "g001", 100, 1000, 2, {"ts": 1000}, {"1": "3.14"}
        )
        pipe = mock_redis.pipeline.return_value
        # 一个 pipeline、一次 execute 完成 window+stream（每设备往返 3→2 的关键）
        mock_redis.pipeline.assert_called_once()
        pipe.execute.assert_called_once()
        assert pipe.lpush.call_args[0][0] == "rt:window:qc100:g001:100"
        assert pipe.ltrim.called
        assert pipe.xadd.call_args[0][0] == f"rt:stream:qc:g001:{shard_no}"
        # latest 不在本方法内（latest-last 由调用方保证）
        mock_redis.hset.assert_not_called()

    def test_empty_stream_values_writes_window_only(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_window_stream("qc100", "qc", "g001", 100, 1000, 1, {"ts": 1000}, {})
        pipe = mock_redis.pipeline.return_value
        assert pipe.lpush.called
        assert pipe.ltrim.called
        pipe.xadd.assert_not_called()
        pipe.execute.assert_called_once()

    def test_xadd_entry_fields_and_maxlen(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config(maxlen=200000))
        writer.write_window_stream(
            "qc100", "qc", "g001", 100, 1700000000000, 7, {"ts": 1}, {"5": "9.9"}
        )
        pipe = mock_redis.pipeline.return_value
        entry = pipe.xadd.call_args[0][1]
        assert entry["config_version"] == "7"
        assert json.loads(entry["values"]) == {"5": "9.9"}
        assert pipe.xadd.call_args[1]["maxlen"] == 200000
        assert pipe.xadd.call_args[1]["approximate"] is True

    def test_pipeline_xadd_contains_v2_quality_fields(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_window_stream(
            "qc100",
            "qc",
            "g001",
            100,
            1700000000000,
            7,
            {"ts": 1},
            {"5": "9.9"},
            {"5": 5},
        )

        entry = mock_redis.pipeline.return_value.xadd.call_args[0][1]
        assert entry["contract_version"] == "2"
        assert json.loads(entry["qualities"]) == {"5": 5}


def test_writer_records_json_and_redis_io_timings(mock_redis):
    metrics = PerformanceMetrics()
    writer = RedisWriter(mock_redis, _make_config(), metrics=metrics)

    writer.write_latest("qc100", "g001", {"1": {"v": "3.14", "t": 1000, "q": 1}})
    writer.write_window("qc100", "g001", 100, {"ts": 1000, "values": {"1": "3.14"}})
    writer.write_stream("qc100", "qc", "g001", 100, 1000, 1, {"1": "3.14"})

    timings = metrics.snapshot()["timings"]
    assert timings["json_ms"]["count"] == 3
    assert timings["redis_io_ms"]["count"] == 3
