"""审查批次1（Writer 侧）回归：B004 / B025 / B028 / B074。"""
from __future__ import annotations

import asyncio
import itertools
import queue
import threading
import time
from unittest.mock import AsyncMock, MagicMock

import pytest

import system_log as system_log_mod
from consumer import StreamConsumer, _BufferedRow
from performance import WriterPerformanceMetrics
from system_log import SystemLogWriter
from tdengine_writer import LANDED

STREAM = "s1"


def _config(**writer_overrides) -> dict:
    writer = {
        "flush_interval_ms": 20,
        "batch_count": 5,
        "max_buffer_rows": 50,
        "pending_min_idle_ms": 0,
        "max_row_retry": 3,
    }
    writer.update(writer_overrides)
    return {"writer": writer, "redis": {"group_name": "writer", "consumer_name": "writer-1"}}


def _entry_data() -> dict:
    return {
        "device_code": "qc100",
        "device_type": "qc",
        "group_code": "g001",
        "ts": "1000",
        "config_version": "0",
        "values": '{"1": "1.5"}',
    }


def _mapping():
    mapping = MagicMock()
    mapping.var_map = {"1": "c1"}
    mapping.child_table = "qc100_1"
    mapping.stable = "sdqc"
    return mapping


def _consumer(writer, redis_client, **overrides) -> StreamConsumer:
    consumer = StreamConsumer(
        _config(**overrides), MagicMock(), writer, redis_client,
        metrics=WriterPerformanceMetrics(),
    )
    consumer._mapper.get_cached = MagicMock(return_value=_mapping())
    return consumer


def _row(entry_id: str, payload_bytes: int = 0) -> _BufferedRow:
    return _BufferedRow(
        entry_id=entry_id, stream_key=STREAM, device_code="qc100", device_type="qc",
        group_code="g001", child_table="qc100_1", stable="sdqc", ts=1000,
        columns={"c1": 1.0}, entry_data=_entry_data(), payload_bytes=payload_bytes,
    )


def _landed(writer):
    writer.write_multi_table = AsyncMock(
        side_effect=lambda rows: [(list(range(len(rows))), LANDED, 1.0)]
    )


def _acked_ids(redis_client) -> set[str]:
    return {
        entry_id
        for call in redis_client.xack.call_args_list
        for entry_id in call.args[2:]
    }


# ── B004：持续健康流量下，到期隔离行仍按时重试 ───────────────────────────────


@pytest.mark.asyncio
async def test_b004_due_isolated_row_is_retried_under_continuous_traffic():
    writer = MagicMock()
    _landed(writer)
    redis_client = MagicMock()
    redis_client.xinfo_groups.return_value = [{"name": "writer"}]
    counter = itertools.count(1)
    # 每次读取都有新的健康数据：主缓冲在 readmit 检查点上永远不空
    redis_client.xreadgroup.side_effect = lambda **_kwargs: [
        (STREAM, [(f"{next(counter)}-1", _entry_data())])
    ]
    consumer = _consumer(writer, redis_client)
    consumer.drain_pending = AsyncMock()  # 只验证主循环自身的重试节奏
    isolated = _row("0-5", payload_bytes=10)
    consumer._isolate_row(isolated)
    isolated.next_retry_at = 0.0  # 已到期

    task = asyncio.create_task(consumer.run_loop([STREAM]))
    try:
        for _ in range(200):
            if "0-5" in _acked_ids(redis_client):
                break
            await asyncio.sleep(0.01)
    finally:
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task

    assert "0-5" in _acked_ids(redis_client)
    assert consumer._isolated == []
    assert len(_acked_ids(redis_client)) > 1  # 新数据同时照常写入


def test_b004_not_yet_due_rows_stay_isolated():
    consumer = _consumer(MagicMock(), MagicMock())
    row = _row("0-6")
    consumer._isolate_row(row)
    consumer._buffer.append(_row("9-1"))
    consumer._readmit_isolated(now=row.next_retry_at - 1)
    assert [r.entry_id for r in consumer._isolated] == ["0-6"]


# ── B074：缓冲字节指标包含隔离区 ─────────────────────────────────────────────


@pytest.mark.asyncio
async def test_b074_buffer_bytes_gauge_includes_isolated_rows():
    writer = MagicMock()
    _landed(writer)
    redis_client = MagicMock()
    redis_client.xinfo_groups.return_value = [{"name": "writer"}]
    consumer = _consumer(writer, redis_client)
    consumer._isolate_row(_row("0-7", payload_bytes=100))

    await consumer._handle_entry(STREAM, "1-1", _entry_data(), 1)
    gauge = consumer._metrics._gauges["buffer_bytes"]
    assert gauge == consumer._buffer_bytes + 100

    await consumer._flush()
    assert consumer._buffer == []
    assert consumer._metrics._gauges["buffer_bytes"] == 100
    assert consumer._remaining_byte_capacity() == consumer._max_buffer_bytes - 100


# ── B025：XACK 后 XDEL 失败再重启，遗留条目可被安全清扫 ──────────────────────


def _sweep_client(last_delivered: str, pending: dict, entries: list[str]):
    client = MagicMock()
    client.xinfo_groups.return_value = [
        {"name": "writer", "last-delivered-id": last_delivered}
    ]
    client.xpending.return_value = pending
    client.xrange.return_value = [(entry_id, {}) for entry_id in entries]
    return client


def test_b025_sweep_deletes_acked_entries_below_pel_after_restart():
    client = _sweep_client(
        "50-0", {"pending": 2, "min": "30-0", "max": "40-0", "consumers": []},
        ["10-0", "20-0"],
    )
    consumer = _consumer(MagicMock(), client)  # 新进程：_delete_retry 为空

    assert consumer._sweep_acked_entries(STREAM, 100) == 2
    client.xrange.assert_called_once_with(STREAM, "-", "(30-0", 100)
    client.xdel.assert_called_once_with(STREAM, "10-0", "20-0")


def test_b025_sweep_without_pending_is_bounded_by_last_delivered():
    client = _sweep_client("50-0", {"pending": 0, "min": None}, ["45-0"])
    consumer = _consumer(MagicMock(), client)
    consumer._delete_retry = {STREAM: {"45-0"}}

    assert consumer._sweep_acked_entries(STREAM, 10) == 1
    client.xrange.assert_called_once_with(STREAM, "-", "50-0", 10)
    assert consumer._delete_retry == {}


@pytest.mark.parametrize(
    "groups",
    [
        [{"name": "writer", "last-delivered-id": "50-0"}, {"name": "other", "last-delivered-id": "1-0"}],
        [{"name": "someone-else", "last-delivered-id": "50-0"}],
        [{"name": "writer", "last-delivered-id": "0-0"}],
        RuntimeError("NOPERM"),
    ],
    ids=["multi-group", "foreign-group", "nothing-delivered", "query-error"],
)
def test_b025_sweep_refuses_when_safety_is_unproven(groups):
    client = _sweep_client("50-0", {"pending": 0, "min": None}, ["1-0"])
    if isinstance(groups, Exception):
        client.xinfo_groups.side_effect = groups
    else:
        client.xinfo_groups.return_value = groups
    consumer = _consumer(MagicMock(), client)

    assert consumer._sweep_acked_entries(STREAM, 10) == 0
    client.xdel.assert_not_called()


# ── B028：满队列丢了停止哨兵，线程仍能在排空后退出 ───────────────────────────


def test_b028_full_queue_stop_still_terminates_worker(monkeypatch):
    blocker = threading.Event()
    written: list[str] = []

    def slow_write(self, event):
        blocker.wait(5)
        written.append(event["content"])

    monkeypatch.setattr(SystemLogWriter, "_write", slow_write)
    monkeypatch.setattr(system_log_mod, "_STOP_POLL_INTERVAL", 0.02)
    log = SystemLogWriter({}, queue_max_size=2)
    log.start()
    assert log.emit(level="INFO", log_type="t", source="s", content="a")
    time.sleep(0.05)  # 工作线程取走 a 并卡在写库
    assert log.emit(level="INFO", log_type="t", source="s", content="b")
    assert log.emit(level="INFO", log_type="t", source="s", content="c")
    assert log._queue.full()

    log.stop(timeout=0.05)  # 哨兵放不进满队列
    assert log._thread is not None and log._thread.is_alive()

    blocker.set()  # 写库恢复
    deadline = time.monotonic() + 5
    while log._thread.is_alive() and time.monotonic() < deadline:
        time.sleep(0.02)
    assert not log._thread.is_alive()
    assert written == ["a", "b", "c"]  # 停止前已排队的事件仍写完

    log.stop(timeout=1.0)  # 重复 stop 完成收尾
    assert log._thread is None
    with pytest.raises(queue.Empty):
        log._queue.get_nowait()
