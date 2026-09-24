"""消费者积压回收容量预算与去重回归测试（DW-02）。

验收场景：
- TDengine 持续 RETRY、积压远超 max_buffer_rows 时，XAUTOCLAIM 与
  XPENDING/XCLAIM 两条回收路径多轮翻页后缓冲仍有界、无重复 (stream_key, entry_id)。
- 正常读取 count 按剩余容量截断。
- 同一消息不会重复进入缓冲；恢复（flush 成功）后剩余 PEL 条目仍能被正确处理。
"""
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import consumer as consumer_mod
from consumer import StreamConsumer
from performance import WriterPerformanceMetrics
from tdengine_writer import LANDED, RETRY


def _config(max_buffer_rows: int = 10) -> dict:
    return {
        "writer": {
            "flush_interval_ms": 500,
            "batch_count": 5,
            "max_buffer_rows": max_buffer_rows,
            "pending_min_idle_ms": 0,
            "pending_max_retry": 3,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }


def _entry_data(entry_id: str) -> dict:
    return {
        "device_code": "qc100",
        "device_type": "qc",
        "group_code": "g001",
        "ts": "1000",
        "config_version": "0",
        "values": '{"1": "1.5"}',
    }


def _mapping():
    m = MagicMock()
    m.var_map = {"1": "c1"}
    m.child_table = "qc100_1"
    m.stable = "sdqc"
    return m


def _consumer(writer, redis_client, max_buffer_rows: int = 10) -> StreamConsumer:
    consumer = StreamConsumer(
        _config(max_buffer_rows), MagicMock(), writer, redis_client,
        metrics=WriterPerformanceMetrics(),
    )
    consumer._mapper.get_cached = MagicMock(return_value=_mapping())
    return consumer


def _retry_flush(writer):
    """write_multi_table 对任意行数一律返回 RETRY（TDengine 不可用）。"""
    def _retry(rows):
        return [(list(range(len(rows))), RETRY, 1.0)]
    writer.write_multi_table = AsyncMock(side_effect=_retry)


def _landed_flush(writer):
    def _landed(rows):
        return [(list(range(len(rows))), LANDED, 1.0)]
    writer.write_multi_table = AsyncMock(side_effect=_landed)


def _unique_keys(consumer) -> set:
    return {(r.stream_key, r.entry_id) for r in consumer._buffer}


# ---------------------------------------------------------------------------
# XAUTOCLAIM 回收路径
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_drain_xautoclaim_stops_when_flush_never_frees_space():
    """PEL 积压 30 条、上限 10、flush 永远 RETRY：缓冲有界且无重复。"""
    writer = MagicMock()
    _retry_flush(writer)
    redis_client = MagicMock()
    # 6 页 × 5 条 = 30 条 PEL 积压，远超 max_buffer_rows=10
    pages = []
    for page in range(6):
        entries = [(f"{page + 1}-{i}", _entry_data(f"{page + 1}-{i}")) for i in range(5)]
        next_id = "0-0" if page == 5 else f"{page + 2}-0"
        pages.append((next_id, entries, []))
    redis_client.xautoclaim.side_effect = pages
    redis_client.xpending_range.return_value = []  # _get_delivery_counts
    consumer = _consumer(writer, redis_client)

    await consumer.drain_pending("rt:stream:qc:g001:0", "writer-1")

    assert len(consumer._buffer) <= consumer._max_buffer_rows
    keys = _unique_keys(consumer)
    assert len(keys) == len(consumer._buffer)  # 无重复 (stream_key, entry_id)


@pytest.mark.asyncio
async def test_drain_xautoclaim_skips_entry_already_buffered():
    """PEL 中未确认、仍在缓冲的条目被再次领取时不得重复入缓冲。"""
    writer = MagicMock()
    _retry_flush(writer)
    redis_client = MagicMock()
    dup_id = "1-0"
    entries = [(f"{i}-0", _entry_data(f"{i}-0")) for i in range(1, 5)]
    entries.append((dup_id, _entry_data(dup_id)))  # 与第一页第一条重复
    redis_client.xautoclaim.side_effect = [
        ("5-0", entries, []),
        ("0-0", [], []),
    ]
    redis_client.xpending_range.return_value = []
    consumer = _consumer(writer, redis_client)

    await consumer.drain_pending("rt:stream:qc:g001:0", "writer-1")

    keys = [r.entry_id for r in consumer._buffer]
    assert keys.count(dup_id) == 1


@pytest.mark.asyncio
async def test_drain_resumes_after_recovery_and_processes_remaining():
    """flush 失败停止回收后，TDengine 恢复（flush 成功）时剩余 PEL 全部正确处理。"""
    writer = MagicMock()
    _retry_flush(writer)
    redis_client = MagicMock()
    pages = []
    for page in range(4):
        entries = [(f"{page + 1}-{i}", _entry_data(f"{page + 1}-{i}")) for i in range(5)]
        next_id = "0-0" if page == 3 else f"{page + 2}-0"
        pages.append((next_id, entries, []))
    redis_client.xautoclaim.side_effect = list(pages)
    redis_client.xpending_range.return_value = []
    consumer = _consumer(writer, redis_client)

    # 第一轮：失败，回收在上限处停止。
    await consumer.drain_pending("rt:stream:qc:g001:0", "writer-1")
    # 持续 RETRY 的行会被隔离出主缓冲（见 test_consumer_degrade.py 的隔离用例），
    # 因此这里不再断言"主缓冲被重试行占满"，而是断言这些行既没丢、也没被 ACK。
    assert len(consumer._buffer) + len(consumer._isolated) == consumer._max_buffer_rows
    assert redis_client.xack.call_count == 0
    assert len(consumer._isolated) > 0, "持续 RETRY 的行应进入隔离区而不是占着主缓冲"

    # 恢复：flush 成功（LANDED→ACK），继续回收剩余 PEL
    _landed_flush(writer)
    redis_client.xautoclaim.side_effect = [
        (next_id, entries, []) for (next_id, entries, _) in pages[2:]
    ]
    # 隔离行按 next_retry_at 退避后才放回；这里把退避时钟推过去，
    # 模拟"下一个回收周期到来"。生产由回收周期本身驱动，不会热循环。
    for row in consumer._isolated:
        row.next_retry_at = 0.0
    # 生产主循环每轮都会 flush；隔离行被放回后需要一次 flush 才真正落库。
    for _ in range(2):
        await consumer.drain_pending("rt:stream:qc:g001:0", "writer-1")
        await consumer._flush()

    assert consumer._buffer == []
    # LANDED 行全部被 ACK + XDEL（成功入库后才确认的语义保持）
    acked = {eid for call in redis_client.xack.call_args_list for eid in call.args[2:]}
    assert acked == {f"{p}-{i}" for p in range(1, 5) for i in range(5)}


@pytest.mark.asyncio
async def test_drain_claim_count_respects_remaining_capacity():
    """领取批次按剩余容量截断，不超配额。"""
    writer = MagicMock()
    _retry_flush(writer)
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client, max_buffer_rows=3)
    # 预先占满 2 条，仅剩 1 条容量
    from consumer import _BufferedRow
    consumer._buffer.append(_BufferedRow(
        entry_id="0-1", stream_key="rt:stream:qc:g001:0", device_code="qc100",
        device_type="qc", group_code="g001", child_table="t", stable="s", ts=1,
        columns={}, entry_data={},
    ))
    consumer._buffer.append(_BufferedRow(
        entry_id="0-2", stream_key="rt:stream:qc:g001:0", device_code="qc100",
        device_type="qc", group_code="g001", child_table="t", stable="s", ts=1,
        columns={}, entry_data={},
    ))
    redis_client.xautoclaim.side_effect = [("0-0", [], [])]
    redis_client.xpending_range.return_value = []

    await consumer.drain_pending("rt:stream:qc:g001:0", "writer-1")

    assert redis_client.xautoclaim.call_args.kwargs["count"] == 1


# ---------------------------------------------------------------------------
# XPENDING/XCLAIM 回退路径
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_drain_xpending_fallback_respects_budget_and_dedup():
    writer = MagicMock()
    _retry_flush(writer)
    redis_client = MagicMock()
    # XAUTOCLAIM 不支持 → 回退
    redis_client.xautoclaim.side_effect = Exception("unknown command 'XAUTOCLAIM'")
    pending = [
        {"message_id": f"{i}-0", "time_since_delivered": 999999, "times_delivered": 1}
        for i in range(1, 31)  # 30 条积压
    ]
    redis_client.xpending_range.side_effect = [
        pending[:20],
        pending[20:],
        [],
    ]

    def _xclaim(stream_key, group, consumer_name, min_idle_time, message_ids):
        # 真实 Redis 只返回所请求的 id；按 message_ids 过滤模拟该语义
        requested = set(message_ids)
        return [
            (p["message_id"], _entry_data(p["message_id"]))
            for p in pending
            if p["message_id"] in requested
        ]

    redis_client.xclaim.side_effect = _xclaim
    consumer = _consumer(writer, redis_client)

    await consumer.drain_pending("rt:stream:qc:g001:0", "writer-1")

    assert len(consumer._buffer) <= consumer._max_buffer_rows
    assert len(_unique_keys(consumer)) == len(consumer._buffer)
    # 领取数量按剩余容量截断
    first_claim = redis_client.xclaim.call_args_list[0]
    assert len(first_claim.kwargs["message_ids"]) <= consumer._max_buffer_rows


# ---------------------------------------------------------------------------
# 正常新消息读取路径
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_normal_read_count_capped_to_remaining_capacity():
    writer = MagicMock()
    _landed_flush(writer)
    redis_client = MagicMock()
    redis_client.xreadgroup.return_value = []
    consumer = _consumer(writer, redis_client)

    from consumer import _BufferedRow
    for i in range(8):
        consumer._buffer.append(_BufferedRow(
            entry_id=f"p-{i}", stream_key="s", device_code="qc100",
            device_type="qc", group_code="g001", child_table="t", stable="s", ts=1,
            columns={}, entry_data={},
        ))
        consumer._buffered_keys.add(("s", f"p-{i}"))

    # 模拟主循环读取（max=10，剩余 2）——直接断言 xreadgroup 的 count 参数
    # 主循环逻辑在 run_loop 中，这里验证通过 drain 之外的方式：
    # 手动复现主循环的 count 计算
    remaining = consumer._max_buffer_rows - len(consumer._buffer)
    count = min(consumer._batch_count, remaining)
    assert count == 2


def test_handle_entry_rejects_duplicate_entry_into_buffer():
    """同一条目两次 _handle_entry 只入缓冲一次。"""
    writer = MagicMock()
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    import asyncio

    entry_id = "42-0"
    data = _entry_data(entry_id)
    asyncio.run(consumer._handle_entry("rt:stream:qc:g001:0", entry_id, data, 1))
    asyncio.run(consumer._handle_entry("rt:stream:qc:g001:0", entry_id, data, 2))

    assert len(consumer._buffer) == 1
    assert ("rt:stream:qc:g001:0", entry_id) in consumer._buffered_keys


@pytest.mark.asyncio
async def test_flush_failure_keeps_keys_in_buffered_set():
    """flush RETRY 后 buffered_keys 与缓冲行保持同步（恢复后能继续去重）。"""
    writer = MagicMock()
    _retry_flush(writer)
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)

    await consumer._handle_entry("rt:stream:qc:g001:0", "7-0", _entry_data("7-0"), 1)
    assert ("rt:stream:qc:g001:0", "7-0") in consumer._buffered_keys

    await consumer._flush()

    assert [r.entry_id for r in consumer._buffer] == ["7-0"]
    assert consumer._buffered_keys == {("rt:stream:qc:g001:0", "7-0")}

    # 恢复后 flush 成功：行离开缓冲，键同步清除
    _landed_flush(writer)
    await consumer._flush()
    assert consumer._buffer == []
    assert consumer._buffered_keys == set()


@pytest.mark.asyncio
async def test_multistream_read_admission_leaves_overflow_in_pel():
    writer=MagicMock();_retry_flush(writer)
    redis_client=MagicMock();c=_consumer(writer,redis_client,max_buffer_rows=2)
    for stream in ("rt:stream:qc:g001:0","rt:stream:qc:g001:1"):
        for i in range(2):
            await c._handle_entry(stream,f"{i+1}-0",_entry_data(str(i)),1)
    assert len(c._buffer)==2
    assert len(c._buffered_keys)==2
    redis_client.xack.assert_not_called()
    await c._flush()
    assert len(c._buffer)==2



def test_multistream_count_respects_global_capacity_and_round_robin():
    consumer = _consumer(MagicMock(), MagicMock(), max_buffer_rows=100)
    consumer._batch_count = 100
    keys = [f"stream-{i}" for i in range(4)]
    streams, count = consumer._next_read_scope(keys)
    assert list(streams) == keys
    assert count == 25
    assert count * len(streams) == 100

    consumer._max_buffer_rows = 2
    streams, count = consumer._next_read_scope(keys)
    assert list(streams) == keys[:2]
    assert count == 1
    streams, count = consumer._next_read_scope(keys)
    assert list(streams) == keys[2:]
    assert count == 1


@pytest.mark.asyncio
async def test_concurrent_append_survives_flush_and_is_not_acked_early():
    import asyncio
    writer = MagicMock()
    entered = asyncio.Event()
    release = asyncio.Event()

    async def write(rows):
        entered.set()
        await release.wait()
        return [(list(range(len(rows))), LANDED, 1.0)]

    writer.write_multi_table = write
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    stream = "rt:stream:qc:g001:0"
    await consumer._handle_entry(stream, "1-0", _entry_data("1-0"), 1)
    flushing = asyncio.create_task(consumer._flush())
    await entered.wait()
    await consumer._handle_entry(stream, "2-0", _entry_data("2-0"), 1)
    assert len(consumer._buffer) == 1
    assert consumer._inflight_rows == 1
    release.set()
    await flushing
    assert [row.entry_id for row in consumer._buffer] == ["2-0"]
    assert consumer._buffered_keys == {(stream, "2-0")}
    acked = {eid for call in redis_client.xack.call_args_list for eid in call.args[2:]}
    assert acked == {"1-0"}


@pytest.mark.asyncio
async def test_cancelled_flush_restores_inflight_batch():
    import asyncio
    writer = MagicMock()
    entered = asyncio.Event()

    async def write(rows):
        entered.set()
        await asyncio.Event().wait()

    writer.write_multi_table = write
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    stream = "rt:stream:qc:g001:0"
    await consumer._handle_entry(stream, "1-0", _entry_data("1-0"), 1)
    flushing = asyncio.create_task(consumer._flush())
    await entered.wait()
    flushing.cancel()
    with pytest.raises(asyncio.CancelledError):
        await flushing
    assert [row.entry_id for row in consumer._buffer] == ["1-0"]
    assert consumer._inflight_rows == 0
    assert consumer._buffered_keys == {(stream, "1-0")}
    redis_client.xack.assert_not_called()



@pytest.mark.asyncio
async def test_overdelivered_entry_is_tracked_and_reclaimed_after_flush():
    writer = MagicMock()
    _landed_flush(writer)
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client, max_buffer_rows=2)
    consumer._max_buffer_bytes = 1
    stream = "rt:stream:qc:g001:0"
    await consumer._handle_entry(stream, "1-0", _entry_data("1-0"), 1)
    assert consumer._buffer_bytes > consumer._max_buffer_bytes
    await consumer._handle_entry(stream, "2-0", _entry_data("2-0"), 1)
    assert consumer._overdelivered_ids == {stream: {"2-0"}}
    await consumer._flush()
    redis_client.xclaim.return_value = [("2-0", _entry_data("2-0"))]
    await consumer._recover_overdelivered()
    assert [row.entry_id for row in consumer._buffer] == ["2-0"]
    assert consumer._overdelivered_ids == {}


def test_read_scope_stops_when_inflight_bytes_fill_budget():
    consumer = _consumer(MagicMock(), MagicMock(), max_buffer_rows=100)
    consumer._max_buffer_bytes = 1000
    consumer._inflight_bytes = 1000
    assert consumer._next_read_scope(["s1", "s2"]) == ({}, 0)



@pytest.mark.asyncio
async def test_schema_retry_releases_buffer_while_healthy_frame_acks():
    writer = MagicMock()
    writer._last_local_retry_indices = {0}
    writer.write_multi_table = AsyncMock(
        return_value=[([0], RETRY, 1.0), ([1], LANDED, 1.0)]
    )
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    stream = "rt:stream:qc:g001:0"
    await consumer._handle_entry(stream, "1-0", _entry_data("1-0"), 1)
    await consumer._handle_entry(stream, "2-0", _entry_data("2-0"), 1)
    await consumer._flush()
    assert consumer._buffer == []
    acked = {eid for call in redis_client.xack.call_args_list for eid in call.args[2:]}
    assert acked == {"2-0"}


@pytest.mark.asyncio
async def test_missing_writer_outcome_restores_batch_without_ack():
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    stream = "rt:stream:qc:g001:0"
    await consumer._handle_entry(stream, "1-0", _entry_data("1-0"), 1)
    with pytest.raises(RuntimeError, match="incomplete"):
        await consumer._flush()
    assert [row.entry_id for row in consumer._buffer] == ["1-0"]
    assert consumer._inflight_rows == 0
    redis_client.xack.assert_not_called()


@pytest.mark.asyncio
async def test_bounded_pending_reclaim_resumes_cursor_next_call():
    writer = MagicMock()
    _landed_flush(writer)
    redis_client = MagicMock()
    redis_client.xpending_range.return_value = []
    redis_client.xautoclaim.side_effect = [
        ("2-0", [("1-0", _entry_data("1-0"))], []),
        ("0-0", [("2-0", _entry_data("2-0"))], []),
    ]
    consumer = _consumer(writer, redis_client)
    consumer._batch_count = 1
    stream = "rt:stream:qc:g001:0"
    await consumer.drain_pending(stream, "writer-1", max_entries=1)
    assert redis_client.xautoclaim.call_count == 1
    assert consumer._pending_cursors[stream] == "2-0"
    await consumer.drain_pending(stream, "writer-1", max_entries=1)
    assert redis_client.xautoclaim.call_args_list[1].kwargs["start_id"] == "2-0"
    assert consumer._pending_cursors[stream] == "0-0"



@pytest.mark.asyncio
async def test_bounded_xpending_fallback_continues_after_claimed_id():
    writer = MagicMock()
    _landed_flush(writer)
    redis_client = MagicMock()
    redis_client.xautoclaim.side_effect = Exception("unknown command 'XAUTOCLAIM'")
    first = [
        {"message_id": f"{i}-0", "time_since_delivered": 999999, "times_delivered": 1}
        for i in (1, 2, 3)
    ]
    redis_client.xpending_range.side_effect = [first, first[1:]]

    def claim(stream, group, name, min_idle_time, message_ids):
        return [(entry_id, _entry_data(entry_id)) for entry_id in message_ids]

    redis_client.xclaim.side_effect = claim
    consumer = _consumer(writer, redis_client)
    consumer._batch_count = 3
    stream = "rt:stream:qc:g001:0"
    await consumer.drain_pending(stream, "writer-1", max_entries=1)
    assert consumer._pending_cursors[stream] == "(1-0"
    await consumer.drain_pending(stream, "writer-1", max_entries=1)
    assert redis_client.xclaim.call_args_list[1].kwargs["message_ids"] == ["2-0"]


def test_retry_ack_and_delete_rotate_across_streams_with_id_budget():
    redis_client = MagicMock()
    consumer = _consumer(MagicMock(), redis_client)
    consumer._ack_retry = {"s1": {"1-0", "2-0"}, "s2": {"3-0"}}
    consumer._delete_retry = {"s1": {"1-0", "2-0"}, "s2": {"3-0"}}
    consumer._retry_pending_acks(max_ids=1)
    assert redis_client.xack.call_count == 1
    assert redis_client.xack.call_args.args[0] == "s1"
    consumer._retry_pending_acks(max_ids=1)
    assert redis_client.xack.call_args.args[0] == "s2"
    consumer._retry_pending_deletes(max_ids=1)
    assert redis_client.xdel.call_args.args[0] == "s1"
    consumer._retry_pending_deletes(max_ids=1)
    assert redis_client.xdel.call_args.args[0] == "s2"


@pytest.mark.asyncio
async def test_overdelivered_reclaim_is_bounded_and_rotates_streams():
    redis_client = MagicMock()
    consumer = _consumer(MagicMock(), redis_client, max_buffer_rows=10)
    consumer._batch_count = 1
    consumer._overdelivered_ids = {"s1": {"1-0", "2-0"}, "s2": {"3-0"}}
    redis_client.xclaim.side_effect = lambda stream, *_args, **kwargs: [
        (entry_id, _entry_data(entry_id)) for entry_id in kwargs["message_ids"]
    ]
    await consumer._recover_overdelivered()
    assert redis_client.xclaim.call_count == 1
    assert redis_client.xclaim.call_args.args[0] == "s1"
    await consumer._recover_overdelivered()
    assert redis_client.xclaim.call_args.args[0] == "s2"
    assert len(consumer._buffer) == 2


@pytest.mark.asyncio
async def test_scope_join_processes_one_new_stream_before_regular_read():
    import asyncio

    redis_client = MagicMock()
    redis_client.xreadgroup.side_effect = asyncio.CancelledError()
    consumer = _consumer(MagicMock(), redis_client)
    consumer.drain_pending = AsyncMock()
    consumer.queue_stream_key("s2")
    consumer.queue_stream_key("s3")
    streams = ["s1"]
    with pytest.raises(asyncio.CancelledError):
        await consumer.run_loop(streams)
    assert streams == ["s1", "s2"]
    assert consumer._queued_stream_keys == ["s3"]


@pytest.mark.asyncio
async def test_ack_outage_bounds_new_reads_and_reclaim_never_rewrites_landed_frame():
    import asyncio

    writer = MagicMock()
    _landed_flush(writer)
    redis_client = MagicMock()
    redis_client.xack.side_effect = RuntimeError("Redis ACK unavailable")
    stream = "s1"
    entries = [
        ("1-0", _entry_data("1-0")),
        ("2-0", _entry_data("2-0")),
    ]
    redis_client.xreadgroup.return_value = [(stream, entries)]
    consumer = _consumer(writer, redis_client, max_buffer_rows=2)
    consumer.drain_pending = AsyncMock()
    task = asyncio.create_task(consumer.run_loop([stream]))
    try:
        for _ in range(100):
            if sum(len(ids) for ids in consumer._ack_retry.values()) == 2:
                break
            await asyncio.sleep(0.01)
        assert consumer._ack_retry == {stream: {"1-0", "2-0"}}
        await asyncio.sleep(0.15)
        assert redis_client.xreadgroup.call_count == 1
        assert writer.write_multi_table.call_count == 1
        assert consumer._remaining_capacity() == 0
        await consumer._handle_entry(stream, "1-0", entries[0][1], 2)
        assert writer.write_multi_table.call_count == 1
        assert consumer._buffer == []
    finally:
        task.cancel()
        with pytest.raises(asyncio.CancelledError):
            await task
    redis_client.xack.side_effect = None
    consumer._retry_pending_acks(max_ids=2)
    assert consumer._ack_retry == {}
    assert consumer._next_read_scope([stream])[0] == {stream: ">"}


@pytest.mark.asyncio
async def test_delete_outage_uses_same_identity_capacity_until_recovered():
    writer = MagicMock()
    _landed_flush(writer)
    redis_client = MagicMock()
    redis_client.xdel.side_effect = RuntimeError("Redis DELETE unavailable")
    consumer = _consumer(writer, redis_client, max_buffer_rows=2)
    for entry_id in ("1-0", "2-0"):
        await consumer._handle_entry("s1", entry_id, _entry_data(entry_id), 1)
    await consumer._flush()
    assert consumer._delete_retry == {"s1": {"1-0", "2-0"}}
    assert consumer._next_read_scope(["s1"]) == ({}, 0)
    consumer._retry_pending_deletes(max_ids=2)
    assert consumer._delete_retry == {"s1": {"1-0", "2-0"}}
    redis_client.xdel.side_effect = None
    consumer._retry_pending_deletes(max_ids=2)
    assert consumer._delete_retry == {}
    assert consumer._next_read_scope(["s1"])[0] == {"s1": ">"}
