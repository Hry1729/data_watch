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


# ── B025：XACK 后 XDEL 失败再重启，只清扫账本里登记过的已确认条目 ─────────────
#
# PR #26 审查（P1）：旧实现按 last-delivered-id + PEL 推断"已 ACK"，`--skip-to-latest`（`$`）
# 建组时会把从未投递的积压整段 XDEL 掉。以下用 fakeredis 的真实 Stream/消费组语义复现。

def _real_redis():
    fakeredis = pytest.importorskip("fakeredis")
    return fakeredis.FakeRedis(decode_responses=True)


def _xdel_fails_once(client):
    """包一层：第一次 XDEL 抛错（模拟 XACK 成功后 XDEL 失败），之后透传到真实实现。"""
    real_xdel = client.xdel
    state = {"failed": False}

    def xdel(*args):
        if not state["failed"]:
            state["failed"] = True
            raise ConnectionError("xdel boom")
        return real_xdel(*args)

    client.xdel = xdel
    return client


def _stream_ids(client) -> list[str]:
    return [entry_id for entry_id, _ in client.xrange(STREAM)]


def test_b025_skip_to_latest_backlog_is_never_swept():
    client = _real_redis()
    backlog = [client.xadd(STREAM, {"n": str(i)}) for i in range(5)]
    # 等价于 scripts/init_redis_consumer_groups.py --skip-to-latest
    client.xgroup_create(STREAM, "writer", id="$")
    assert client.xpending(STREAM, "writer")["pending"] == 0  # 旧实现会据此整段删除
    consumer = _consumer(MagicMock(), client)

    assert consumer._sweep_acked_entries(STREAM, 100) == 0
    assert _stream_ids(client) == backlog


def test_b025_setid_past_backlog_is_never_swept():
    client = _real_redis()
    client.xgroup_create(STREAM, "writer", id="0", mkstream=True)
    backlog = [client.xadd(STREAM, {"n": str(i)}) for i in range(3)]
    client.xgroup_setid(STREAM, "writer", id="$")
    consumer = _consumer(MagicMock(), client)

    assert consumer._sweep_acked_entries(STREAM, 100) == 0
    assert _stream_ids(client) == backlog


def test_b025_restart_sweeps_only_ids_this_writer_acked():
    client = _real_redis()
    backlog = [client.xadd(STREAM, {"n": str(i)}) for i in range(3)]
    client.xgroup_create(STREAM, "writer", id="$")  # 积压被跳过、从未投递
    delivered = [client.xadd(STREAM, {"n": f"d{i}"}) for i in range(2)]
    read = client.xreadgroup("writer", "writer-1", {STREAM: ">"}, count=10)
    assert [entry_id for entry_id, _ in read[0][1]] == delivered
    old = _consumer(MagicMock(), _xdel_fails_once(client))
    old._ack_ids(STREAM, delivered)  # XACK 成功，XDEL 失败
    assert client.xpending(STREAM, "writer")["pending"] == 0
    assert _stream_ids(client) == backlog + delivered

    fresh = _consumer(MagicMock(), client)  # 重启：内存里的 _delete_retry 已丢
    assert fresh._delete_retry == {}
    assert fresh._sweep_acked_entries(STREAM, 100) == 2
    assert _stream_ids(client) == backlog  # 积压完整保留
    assert fresh._sweep_acked_entries(STREAM, 100) == 0  # 账本已清空


def test_b025_unacked_pending_entries_are_never_swept():
    client = _real_redis()
    client.xgroup_create(STREAM, "writer", id="0", mkstream=True)
    ids = [client.xadd(STREAM, {"n": str(i)}) for i in range(3)]
    client.xreadgroup("writer", "writer-1", {STREAM: ">"}, count=10)  # 全部在 PEL，未 ACK
    consumer = _consumer(MagicMock(), client)

    assert consumer._sweep_acked_entries(STREAM, 100) == 0
    assert _stream_ids(client) == ids


def test_b025_multi_group_keeps_entries_and_ledger():
    client = _real_redis()
    client.xgroup_create(STREAM, "writer", id="0", mkstream=True)
    client.xgroup_create(STREAM, "other", id="0")
    ids = [client.xadd(STREAM, {"n": str(i)}) for i in range(2)]
    client.xreadgroup("writer", "writer-1", {STREAM: ">"}, count=10)
    client.xack(STREAM, "writer", *ids)
    # 即使账本里有本组确认过的 id，"other" 组尚未处理，全局 XDEL 会毁掉它的数据
    client.sadd("writer:acked_undeleted:writer:" + STREAM, *ids)
    consumer = _consumer(MagicMock(), client)

    assert consumer._sweep_acked_entries(STREAM, 100) == 0
    assert _stream_ids(client) == ids
    assert client.scard("writer:acked_undeleted:writer:" + STREAM) == 2
    other = client.xreadgroup("other", "c", {STREAM: ">"}, count=10)
    assert [entry_id for entry_id, _ in other[0][1]] == ids


def test_b025_ack_with_xdel_failure_in_multi_group_records_nothing():
    client = _real_redis()
    client.xgroup_create(STREAM, "writer", id="0", mkstream=True)
    client.xgroup_create(STREAM, "other", id="0")
    ids = [client.xadd(STREAM, {"n": "1"})]
    client.xreadgroup("writer", "writer-1", {STREAM: ">"}, count=10)
    consumer = _consumer(MagicMock(), _xdel_fails_once(client))
    consumer._ack_ids(STREAM, ids)  # 多组：不 XDEL，也就没有账本

    assert client.scard("writer:acked_undeleted:writer:" + STREAM) == 0
    assert _stream_ids(client) == ids


def test_b025_ledger_write_failure_only_leaks_never_deletes():
    client = _real_redis()
    client.xgroup_create(STREAM, "writer", id="0", mkstream=True)
    ids = [client.xadd(STREAM, {"n": "1"})]
    client.xreadgroup("writer", "writer-1", {STREAM: ">"}, count=10)
    _xdel_fails_once(client)
    client.sadd = MagicMock(side_effect=ConnectionError("sadd boom"))
    consumer = _consumer(MagicMock(), client)
    consumer._ack_ids(STREAM, ids)

    assert client.xpending(STREAM, "writer")["pending"] == 0
    assert _stream_ids(client) == ids  # 遗留（空间泄漏）而非误删
    assert consumer._delete_retry == {STREAM: set(ids)}  # 本进程内仍会重试 XDEL
    consumer._retry_pending_deletes()
    assert _stream_ids(client) == []


def test_b025_sweep_refuses_on_topology_query_error():
    client = MagicMock()
    client.srandmember.return_value = ["1-0"]
    client.xinfo_groups.side_effect = RuntimeError("NOPERM")
    consumer = _consumer(MagicMock(), client)

    assert consumer._sweep_acked_entries(STREAM, 10) == 0
    client.xdel.assert_not_called()
    client.srem.assert_not_called()


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
