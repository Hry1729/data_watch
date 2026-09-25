"""消费者端测试，验证刷新时的二分搜索降级功能。Consumer-side tests for §7.6 binary-search degrade wiring in _flush.

Verifies the per-row status contract from write_multi_table is honored:
LANDED → XACK + XDEL; BAD_DATA → DLQ then XACK + XDEL only on success;
RETRY (and DLQ-push failure) → kept buffered, never acked or deleted
(hard constraints ② and ④).
"""
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import consumer as consumer_mod
from consumer import StreamConsumer, _BufferedRow
from performance import WriterPerformanceMetrics
from tdengine_writer import BAD_DATA, FAILED_INCOMPLETE, LANDED, RETRY


def _config() -> dict:
    return {
        "writer": {
            "flush_interval_ms": 500,
            "batch_count": 100,
            "max_buffer_rows": 1000,
            "pending_min_idle_ms": 60000,
            "pending_max_retry": 3,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }


def _row(entry_id: str) -> _BufferedRow:
    return _BufferedRow(
        entry_id=entry_id,
        stream_key="rt:stream:qc:g001:0",
        device_code="qc100",
        device_type="qc",
        group_code="g001",
        child_table="qc100_1",
        stable="sdqc",
        ts=1000,
        columns={"qc1": 1.0},
        entry_data={"device_code": "qc100", "values": "{}"},
    )


def _entry_data(entry_id: str) -> dict:
    return {"device_code": "qc100", "device_type": "qc", "group_code": "g001",
            "ts": "1000", "config_version": "0", "values": '{"1": "1.5"}'}


def _consumer(writer, redis_client):
    return StreamConsumer(
        _config(), MagicMock(), writer, redis_client, metrics=WriterPerformanceMetrics()
    )


@pytest.mark.asyncio
async def test_bad_data_pushed_to_dlq_then_acked():
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], BAD_DATA, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    consumer._buffer = [_row("1-0")]

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._flush()

    dlq.assert_called_once()
    redis_client.xack.assert_called_once()
    redis_client.xdel.assert_called_once()
    assert consumer._buffer == []  # 坏行已隔离离开 buffer


@pytest.mark.asyncio
async def test_bad_data_dlq_failure_keeps_buffer_and_does_not_ack():
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], BAD_DATA, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")
    consumer._buffer = [row]

    with patch.object(consumer_mod, "push_dlq", return_value=False):
        await consumer._flush()

    redis_client.xack.assert_not_called()  # DLQ 失败 → 绝不 ACK
    redis_client.xdel.assert_not_called()
    assert consumer._buffer == [row]  # 留 buffer，不丢


@pytest.mark.asyncio
async def test_failed_incomplete_is_isolated_not_counted_as_success():
    """FAILED_INCOMPLETE 必须持久隔离、不计成功，且只在隔离可靠保存后才 ACK。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], FAILED_INCOMPLETE, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")
    consumer._buffer = [row]

    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._flush()

    dlq.assert_called_once()
    assert "failed incomplete" in dlq.call_args.kwargs["last_error"]
    # 结构化分段证据必须真的传出去：只有一段汇总说明不足以定位该修哪一段
    evidence = dlq.call_args.kwargs["segment_evidence"]
    assert evidence is not None, "FAILED_INCOMPLETE 必须带分段证据"
    assert evidence["overall_status"] == FAILED_INCOMPLETE
    assert evidence["logical_frame"]["entry_id"] == "1-0"
    assert "value_segment" in evidence and "quality_segment" in evidence
    # 关键：没有逐帧归属时，单帧状态必须是 unknown，
    # 绝不能拿批次级计数冒充"本帧已写入"（A 帧的成功会被算到 B 帧头上）。
    assert evidence["value_segment"]["status"] == "unknown"
    assert evidence["quality_segment"]["status"] == "unknown"
    # 批次统计只能放在明确标注作用域的字段里
    assert evidence["batch_execution_summary"]["scope"].startswith("entire write_multi_table batch")
    assert "不能当作本帧的证据" in evidence["note"]
    assert redis_client.xack.call_count == 1, "隔离记录保存成功后才允许 ACK"
    snapshot = consumer._metrics.snapshot()
    assert snapshot["gauges"]["buffer_rows"] == 0


@pytest.mark.asyncio
async def test_failed_incomplete_keeps_data_when_isolation_cannot_be_saved():
    """隔离记录保存不了时，绝不能 ACK、也不能丢原始消息。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], FAILED_INCOMPLETE, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")
    consumer._buffer = [row]

    with patch.object(consumer_mod, "push_dlq", return_value=False):
        await consumer._flush()

    redis_client.xack.assert_not_called()
    redis_client.xdel.assert_not_called()
    assert row in consumer._buffer, "隔离未可靠保存前必须保留原始帧"
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")
    consumer._buffer = [row]

    with patch.object(consumer_mod, "push_dlq") as dlq:
        await consumer._flush()

    dlq.assert_not_called()  # RETRY 绝不进 DLQ
    redis_client.xack.assert_not_called()
    redis_client.xdel.assert_not_called()
    assert consumer._buffer == [row]


@pytest.mark.asyncio
async def test_mixed_landed_bad_retry_split_correctly():
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(
        return_value=[([0], LANDED, 1.0), ([1], BAD_DATA, 1.0), ([2], RETRY, 1.0)]
    )
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    rows = [_row("1-0"), _row("2-0"), _row("3-0")]
    consumer._buffer = list(rows)

    with patch.object(consumer_mod, "push_dlq", return_value=True):
        await consumer._flush()

    # LANDED + 成功 DLQ 的坏行 → ACK；RETRY 行留 buffer
    acked = {eid for call in redis_client.xack.call_args_list for eid in call.args[2:]}
    assert acked == {"1-0", "2-0"}
    deleted = {eid for call in redis_client.xdel.call_args_list for eid in call.args[1:]}
    assert deleted == {"1-0", "2-0"}
    assert consumer._buffer == [rows[2]]


def test_xack_failure_never_deletes_and_retries_ack_first():
    writer = MagicMock()
    redis_client = MagicMock()
    redis_client.xack.side_effect = [RuntimeError("redis unavailable"), 1]
    consumer = _consumer(writer, redis_client)

    consumer._ack_ids("rt:stream:qc:g001:0", ["1-0"])

    redis_client.xdel.assert_not_called()
    assert consumer._ack_retry == {"rt:stream:qc:g001:0": {"1-0"}}

    consumer._retry_pending_acks()

    redis_client.xdel.assert_called_once_with("rt:stream:qc:g001:0", "1-0")
    assert consumer._ack_retry == {}


@pytest.mark.asyncio
async def test_isolated_payload_counts_against_the_total_byte_budget():
    """隔离区持有的载荷必须计入总字节预算，不能在字面账上消失。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    consumer._max_buffer_bytes = 10_000
    row = _row("1-0")
    row.payload_bytes = 4_000
    consumer._buffer = [row]

    for _ in range(consumer._max_row_retry):
        consumer._buffer = [row]
        await consumer._flush()

    assert consumer._isolated_bytes == 4_000
    assert consumer._remaining_byte_capacity() == 10_000 - 4_000, \
        "隔离区载荷必须从剩余额度里扣除"
    # 重新入队只改归属，不改变总占用
    consumer._readmit_isolated(now=row.next_retry_at + 1)
    assert consumer._isolated_bytes == 0
    assert consumer._buffer_bytes == 4_000
    assert consumer._remaining_byte_capacity() == 10_000 - 4_000, \
        "迁移状态只改归属，总占用不得变化"


@pytest.mark.asyncio
async def test_pending_reclaim_offloads_payload_parsing():
    """PEL 回收路径必须与正常读取共用同一套批量准备，不在事件循环上解码。

    原先回收路径不传 prepared，走 _handle_entry 里的 json.loads，
    于是"故障后大量 PEL 回收"会把所有 worker 共享的事件循环堵住。
    """
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(
        side_effect=lambda rows: [([i], LANDED, 1.0) for i in range(len(rows))])
    redis_client = MagicMock()
    redis_client.xautoclaim.return_value = (
        "0-0",
        [("1-0", _entry_data("1-0")), ("2-0", _entry_data("2-0"))],
        [],
    )
    consumer = _consumer(writer, redis_client)

    offloaded = []
    real_run_blocking = consumer._run_blocking

    async def spy(func, *args, **kwargs):
        if func is consumer_mod._prepare_payload_batch:
            offloaded.append(len(args[0]) if args else 0)
        return await real_run_blocking(func, *args, **kwargs)

    consumer._run_blocking = spy
    await consumer._drain_with_xautoclaim("rt:stream:qc:g001:0", "writer-1")

    assert offloaded, "回收路径必须把批量准备放到执行单元，而不是在事件循环里解码"
    assert offloaded[0] == 2, "一次回收 2 条时应一次性批量准备"


@pytest.mark.asyncio
async def test_segment_evidence_never_borrows_another_frames_success():
    """批次级计数不得用来证明某一帧已写入。

    构造：同一批次 A 帧数值成功、B 帧数值失败，批次成功行数=1。
    若用批次计数推导单帧状态，B 会被写成 confirmed_written —— A 的成功算到 B 头上。
    """
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], LANDED, 1.0)])
    # 批次级：尝试 2 行、成功 1 行（是 A 的成功）
    writer._last_sql_value_rows_attempted = 2
    writer._last_sql_value_rows_succeeded = 1
    writer._last_sql_quality_rows_attempted = 0
    writer._last_sql_quality_rows_succeeded = 0
    consumer = _consumer(writer, MagicMock())
    failing_frame = _row("B-0")

    evidence = consumer._segment_evidence_for(failing_frame, FAILED_INCOMPLETE)

    assert evidence["value_segment"]["status"] == "unknown", \
        "批次里另一帧的成功不得被当成本帧的证据"
    assert evidence["quality_segment"]["status"] == "unknown"
    assert evidence["batch_execution_summary"]["value_rows_succeeded"] == 1, \
        "批次统计仍可保留，但必须标明作用域"


def test_retry_backoff_never_overflows_for_absurd_attempt_counts():
    """退避必须对任意重试次数都返回有限合法延迟。

    原实现是 min(base * 2**n, maximum)：先算指数再取上限，
    n=1025 时 2**1025 无法转 float，故障处理路径自己抛 OverflowError。
    """
    consumer = _consumer(MagicMock(), MagicMock())
    maximum = consumer._retry_backoff_max_s
    for attempts in (0, 1, 2, 3, 100, 1024, 1025, 4096, 10 ** 9, 2 ** 70):
        delay = consumer._retry_delay_s(attempts)
        assert isinstance(delay, float)
        assert delay == delay, "不得为 NaN"
        assert delay != float("inf"), "不得为无穷"
        assert 0 < delay <= maximum, f"attempts={attempts} delay={delay}"


def test_retry_backoff_grows_then_saturates():
    consumer = _consumer(MagicMock(), MagicMock())
    delays = [consumer._retry_delay_s(n) for n in (1, 2, 3, 10, 10 ** 6)]
    assert delays[0] == consumer._retry_backoff_base_s
    assert delays[1] > delays[0], "退避应随次数增长"
    assert delays[-1] == consumer._retry_backoff_max_s, "最终饱和到上限"
    assert all(b >= a for a, b in zip(delays, delays[1:])), "必须单调不减"


def test_xdel_failure_is_retried_without_repeating_xack():
    writer = MagicMock()
    redis_client = MagicMock()
    redis_client.xdel.side_effect = [RuntimeError("redis unavailable"), 1]
    consumer = _consumer(writer, redis_client)

    consumer._ack_ids("rt:stream:qc:g001:0", ["1-0"])

    redis_client.xack.assert_called_once()
    assert consumer._delete_retry == {"rt:stream:qc:g001:0": {"1-0"}}

    consumer._retry_pending_deletes()

    redis_client.xack.assert_called_once()
    assert redis_client.xdel.call_count == 2
    assert consumer._delete_retry == {}


# ----------------------------------------------------------------------
# 持续 RETRY 的隔离：一条长期不恢复的行不得拖住同 worker 其它 stream
# ----------------------------------------------------------------------

@pytest.mark.asyncio
async def test_persistently_retrying_row_leaves_main_buffer_after_max_retries():
    """连续 RETRY 达上限后移出主缓冲，但绝不 ACK、绝不进 DLQ。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")
    consumer._buffer = [row]

    with patch.object(consumer_mod, "push_dlq") as dlq:
        for _ in range(consumer._max_row_retry):
            consumer._buffer = [row]
            await consumer._flush()

    dlq.assert_not_called()                       # RETRY 绝不进 DLQ
    redis_client.xack.assert_not_called()         # 也不能 ACK
    redis_client.xdel.assert_not_called()
    assert consumer._buffer == []                 # 不再占用主缓冲容量
    assert [r.entry_id for r in consumer._isolated] == ["1-0"]  # 但仍在本地跟踪
    assert row.retry_attempts >= consumer._max_row_retry


@pytest.mark.asyncio
async def test_isolated_row_does_not_block_healthy_rows_of_same_worker():
    """隔离发生后，同 worker 其它行仍能正常落库并 ACK，且读取容量恢复。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(
        return_value=[([0], RETRY, 1.0), ([1], LANDED, 1.0)]
    )
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    bad, healthy = _row("1-0"), _row("2-0")
    consumer._buffer = [bad, healthy]

    for _ in range(consumer._max_row_retry):
        consumer._buffer = [bad, healthy]
        await consumer._flush()

    acked = {eid for call in redis_client.xack.call_args_list for eid in call.args[2:]}
    assert acked == {"2-0"}, "健康行必须照常 ACK，不被坏行拖住"
    assert [r.entry_id for r in consumer._isolated] == ["1-0"]
    # 主缓冲不再被坏行占住：剩余容量回到满额（隔离区另计，仍受上限约束）。
    assert consumer._remaining_capacity() == consumer._max_buffer_rows - 1


@pytest.mark.asyncio
async def test_isolated_rows_are_readmitted_for_retry_not_abandoned():
    """隔离不是放弃：到期后放回主缓冲重试，且状态迁移必须一次做完。

    覆盖审查指出的状态维护缺陷：放回时如果漏掉身份集合与字节预算，
    容量核算会失真，且 PEL 再次领取同一消息时去重守卫会失效。
    """
    import time as _time

    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")

    for _ in range(consumer._max_row_retry):
        consumer._buffer = [row]
        await consumer._flush()
    assert [r.entry_id for r in consumer._isolated] == ["1-0"]
    assert consumer._buffer_bytes == 0
    assert consumer._isolated_keys == {("rt:stream:qc:g001:0", "1-0")}

    # 退避未到期时不得放回（重试节奏由 next_retry_at 决定，不是缓冲空了就放）
    consumer._readmit_isolated(now=row.next_retry_at - 1)
    assert consumer._isolated and not consumer._buffer

    # 到期后放回：列表、身份集合、字节预算、指标四项必须一致
    consumer._readmit_isolated(now=row.next_retry_at + 1)
    assert consumer._isolated == []
    assert [r.entry_id for r in consumer._buffer] == ["1-0"]
    assert consumer._isolated_keys == set()
    assert ("rt:stream:qc:g001:0", "1-0") in consumer._buffered_keys, \
        "放回后必须进入缓冲去重集合，否则 PEL 再次领取同一 ID 会重复入库"
    assert consumer._buffer_bytes == row.payload_bytes, \
        "放回后字节预算必须同步，否则容量核算失真"
    assert consumer._metrics.snapshot()["gauges"]["buffer_rows"] == 1


@pytest.mark.asyncio
async def test_readmitted_identity_is_deduplicated_when_pel_reclaims_same_id():
    """放回后若 PEL 再次领取同一个 Stream ID，去重守卫必须拦住，不重复入缓冲。"""
    import time as _time

    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    row = _row("1-0")

    for _ in range(consumer._max_row_retry):
        consumer._buffer = [row]
        await consumer._flush()
    consumer._readmit_isolated(now=row.next_retry_at + 1)
    assert len(consumer._buffer) == 1

    # 模拟 PEL 回收再次领取同一 entry_id
    await consumer._handle_entry("rt:stream:qc:g001:0", "1-0", _entry_data("1-0"), 2)
    assert len(consumer._buffer) == 1, "同一 Stream ID 不得被重复放入缓冲"


@pytest.mark.asyncio
async def test_isolated_set_is_bounded_and_restores_backpressure():
    """隔离区有上限：超出后新行留在主缓冲形成背压，而不是无界增长。"""
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[([0], RETRY, 1.0)])
    redis_client = MagicMock()
    consumer = _consumer(writer, redis_client)
    consumer._max_isolated_rows = 2
    rows = [_row(f"{i}-0") for i in range(1, 5)]

    for _ in range(consumer._max_row_retry):
        consumer._buffer = list(rows)
        # 每行单独成为一块，确保都走 RETRY 分支
        writer.write_multi_table = AsyncMock(
            side_effect=lambda batch: [([i], RETRY, 1.0) for i in range(len(batch))]
        )
        await consumer._flush()

    assert len(consumer._isolated) <= 2, "隔离区不得超过上限"
    assert consumer._buffer, "超限后必须有行留在主缓冲形成背压"
    assert len(consumer._isolated) + len(consumer._buffer) == 4, "不能丢行"
