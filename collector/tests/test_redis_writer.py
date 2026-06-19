"""Unit tests for RedisWriter (COL-2)."""

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
# write_latest
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
        parsed = json.loads(mapping["42"])
        assert parsed == {"v": "1.5", "t": 12345, "q": 1}

    def test_no_ttl_set(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_latest("qc100", "g001", {})
        mock_redis.expire.assert_not_called()


# ---------------------------------------------------------------------------
# write_window
# ---------------------------------------------------------------------------

class TestWriteWindow:
    def test_lpush_and_ltrim_called(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_window("qc100", "g001", 100, {"ts": 1000, "values": {}})
        pipe = mock_redis.pipeline.return_value
        assert pipe.lpush.called
        assert pipe.ltrim.called
        pipe.execute.assert_called_once()

    def test_ltrim_keeps_max_10(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_window("qc100", "g001", 100, {})
        pipe = mock_redis.pipeline.return_value
        ltrim_args = pipe.ltrim.call_args[0]
        # ltrim(key, 0, 9) keeps indices 0..9 = 10 items
        assert ltrim_args[1] == 0
        assert ltrim_args[2] == 9

    def test_key_includes_scan_rate(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config())
        writer.write_window("asc7011", "g001", 500, {})
        pipe = mock_redis.pipeline.return_value
        key = pipe.lpush.call_args[0][0]
        assert key == "rt:window:asc7011:g001:500"


# ---------------------------------------------------------------------------
# write_stream
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
        parsed_values = json.loads(entry["values"])
        assert parsed_values == {"5": "9.9"}

    def test_xadd_uses_configured_maxlen(self, mock_redis):
        writer = RedisWriter(mock_redis, _make_config(maxlen=200000))
        writer.write_stream("qc100", "qc", "g001", 100, 0, 1, {})
        kwargs = mock_redis.xadd.call_args[1]
        assert kwargs["maxlen"] == 200000
        assert kwargs["approximate"] is True


def test_writer_records_json_and_redis_io_timings(mock_redis):
    metrics = PerformanceMetrics()
    writer = RedisWriter(mock_redis, _make_config(), metrics=metrics)

    writer.write_latest("qc100", "g001", {"1": {"v": "3.14", "t": 1000, "q": 1}})
    writer.write_window("qc100", "g001", 100, {"ts": 1000, "values": {"1": "3.14"}})
    writer.write_stream("qc100", "qc", "g001", 100, 1000, 1, {"1": "3.14"})

    timings = metrics.snapshot()["timings"]
    assert timings["json_ms"]["count"] == 3
    assert timings["redis_io_ms"]["count"] == 3
