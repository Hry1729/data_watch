from pathlib import Path
from unittest.mock import MagicMock

import pytest

from storage.collector_outbox import CollectorRetryOutbox


def test_collector_retry_outbox_records_and_replays_with_original_timestamp(
    tmp_path: Path,
):
    db_path = tmp_path / "retry.db"
    outbox = CollectorRetryOutbox(db_path, max_records=100)

    # 模拟写入一条失败记录
    ts = 1720000000123
    outbox.record_failure(
        device_code="DEV1",
        group_code="GRP1",
        scan_rate_ms=100,
        ts_ms=ts,
        config_version=1,
        device_type="s7",
        frame={"device_code": "DEV1", "values": {"var1": "10"}},
        stream_values={"var1": "10"},
        stream_qualities={"var1": 192},
        latest_fields={"var1": {"v": "10", "t": ts, "q": 192}},
    )
    assert outbox.count() == 1

    # 模拟 RedisWriter 恢复
    mock_writer = MagicMock()
    replayed = outbox.replay_pending(mock_writer)

    assert replayed == 1
    assert outbox.count() == 0

    # 验证重放使用的是原始采样时间戳与数据
    mock_writer.write_window_stream.assert_called_once()
    args, _ = mock_writer.write_window_stream.call_args
    # device_code, device_type, group_code, scan_rate_ms, ts_ms, config_version, ...
    assert args[0] == "DEV1"
    assert args[1] == "s7"
    assert args[2] == "GRP1"
    assert args[3] == 100
    assert args[4] == ts  # 原始时间戳保留

    mock_writer.write_latest.assert_called_once()
    latest_args, _ = mock_writer.write_latest.call_args
    assert latest_args[0] == "DEV1"
    assert latest_args[1] == "GRP1"
    assert latest_args[2]["var1"]["t"] == ts


def test_collector_retry_outbox_saturation_purges_oldest(tmp_path: Path):
    db_path = tmp_path / "retry_sat.db"
    outbox = CollectorRetryOutbox(db_path, max_records=5)

    for i in range(5):
        outbox.record_failure(
            device_code="DEV1",
            group_code="GRP1",
            scan_rate_ms=100,
            ts_ms=1000 + i,
            config_version=1,
            device_type="s7",
            frame={},
            stream_values={"var1": str(i)},
            stream_qualities=None,
            latest_fields={},
        )
    assert outbox.count() == 5

    # 写入第 6 条，触发容量淘汰
    outbox.record_failure(
        device_code="DEV1",
        group_code="GRP1",
        scan_rate_ms=100,
        ts_ms=2000,
        config_version=1,
        device_type="s7",
        frame={},
        stream_values={"var1": "99"},
        stream_qualities=None,
        latest_fields={},
    )
    # 应淘汰部分最老记录并容纳新记录
    assert outbox.count() <= 5
