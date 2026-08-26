"""DLQ 容量上限与容量告警测试。"""

from unittest.mock import MagicMock

from consumer import StreamConsumer
from dlq import push_dlq
from performance import WriterPerformanceMetrics


class _Pipeline:
    def __init__(self, lpush_length: int):
        self.lpush_length = lpush_length
        self.lpush_args = None
        self.ltrim_args = None

    def lpush(self, *args):
        self.lpush_args = args

    def ltrim(self, *args):
        self.ltrim_args = args

    def execute(self):
        results = [self.lpush_length]
        if self.ltrim_args is not None:
            results.append(True)
        return results


def test_push_dlq_default_is_unbounded_and_warns_on_entry_threshold(caplog):
    redis_client = MagicMock()
    pipeline = _Pipeline(lpush_length=1000)
    redis_client.pipeline.return_value = pipeline

    with caplog.at_level("WARNING", logger="writer.dlq"):
        assert push_dlq(
            redis_client,
            device_type="qc",
            group_code="g001",
            entry_id="1-0",
            stream_key="rt:stream:qc:g001:0",
            data={"values": "{}"},
            retry_count=1,
            last_error="malformed",
            alert_threshold=0,
        )

    assert pipeline.ltrim_args is None
    assert any("unbounded queue" in record.message for record in caplog.records)


def test_push_dlq_uses_configured_limit_and_warns_near_capacity(caplog):
    redis_client = MagicMock()
    pipeline = _Pipeline(lpush_length=3)
    redis_client.pipeline.return_value = pipeline

    with caplog.at_level("WARNING", logger="writer.dlq"):
        assert push_dlq(
            redis_client,
            device_type="qc",
            group_code="g001",
            entry_id="1-0",
            stream_key="rt:stream:qc:g001:0",
            data={"values": "{}"},
            retry_count=1,
            last_error="malformed",
            max_size=3,
            alert_threshold=0.9,
        )

    assert pipeline.ltrim_args == ("rt:dlq:qc:g001", 0, 2)
    assert any("near capacity" in record.message for record in caplog.records)


def test_consumer_passes_dlq_settings_to_runtime():
    config = {
        "writer": {
            "flush_interval_ms": 500,
            "batch_count": 100,
            "max_buffer_rows": 1000,
            "pending_min_idle_ms": 60000,
            "pending_max_retry": 3,
            "dlq_max_size": 17,
            "dlq_alert_threshold": 0.75,
            "dlq_warn_entries": 23,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }
    consumer = StreamConsumer(
        config,
        MagicMock(),
        MagicMock(),
        MagicMock(),
        metrics=WriterPerformanceMetrics(),
    )

    assert consumer._dlq_max_size == 17
    assert consumer._dlq_alert_threshold == 0.75
    assert consumer._dlq_warn_entries == 23
