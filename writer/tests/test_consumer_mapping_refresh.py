"""映射 reload 后，执行单元里预转换的结果必须失效重算。

e55251b 起的反例：`_prepare_payload_batch` 用**当时缓存的** var_map 生成
`prepared.converted_columns`，`_handle_entry` 在发现未知点、reload 映射之后
仍然只检查"不是 None"就复用，于是：
  * 同一帧里旧点+新增点 → 新增点被 `_convert_values` 跳过，重载后漏写；
  * 同一帧里只有新增点 → 预转换结果是空 dict，重载后直接走"无列可写"ACK，
    数据静默丢失；
  * 同批第一条触发 reload 后，后续条目拿到的 mapping 已是新的，但它们的
    prepared 仍是旧快照，同样漏写。

本文件用真实的 `_handle_entry` 路径 + 可控 mapper 构造这些反例，
并锁定"未 reload 时仍走快路径（不重复转换）"，避免修复把下沉收益一起删掉。
"""
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

import consumer as consumer_mod
from consumer import StreamConsumer
from mysql_mapper import GroupMapping
from performance import WriterPerformanceMetrics


def _config() -> dict:
    return {
        "writer": {
            "flush_interval_ms": 500,
            "batch_count": 100,
            "max_buffer_rows": 1000,
            "pending_min_idle_ms": 60000,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }


class _FakeMapper:
    """可控映射缓存：reload 换上一个**新的** GroupMapping（新 var_map 对象）。

    MySQL 版 MysqlMapper.load 每次都会重新构建 var_map dict，这里保持同样的
    语义，快照身份检查才有意义。
    """

    def __init__(self, var_map: dict, reload_to: dict | None = None) -> None:
        self.mapping = self._make(var_map)
        self._reload_var_map = reload_to
        self.reload_calls = 0

    @staticmethod
    def _make(var_map: dict) -> GroupMapping:
        return GroupMapping(config_version=0, child_table="qc100", stable="sdqc",
                            var_map=dict(var_map), var_type={})

    def get_cached(self, device_code, group_code):
        return self.mapping

    def get_or_load(self, device_code, group_code, stream_config_version):
        return self.mapping

    def reload(self, device_code, group_code, stream_config_version):
        self.reload_calls += 1
        if self._reload_var_map is not None:
            self.mapping = self._make(self._reload_var_map)
        return self.mapping


def _entry(entry_id: str, values: dict) -> dict:
    import json
    return {"device_code": "qc100", "device_type": "qc", "group_code": "g001",
            "ts": "1000", "config_version": "0",
            "values": json.dumps(values)}


def _consumer(mapper) -> StreamConsumer:
    writer = MagicMock()
    writer.write_multi_table = AsyncMock(return_value=[])
    redis_client = MagicMock()
    return StreamConsumer(
        _config(), mapper, writer, redis_client, metrics=WriterPerformanceMetrics()
    )


async def _dispatch_batch(consumer, entries: list[dict]) -> None:
    """复刻 run_loop 的批量形态：整批一次性准备，再逐条 handle。"""
    prepared = await consumer._run_blocking(
        consumer_mod._prepare_payload_batch,
        entries,
        consumer._remaining_byte_capacity(),
        consumer._var_map_for_entry,
    )
    for index, entry in enumerate(entries):
        await consumer._handle_entry(
            stream_key="rt:stream:qc:g001:0",
            entry_id=f"{index + 1}-0",
            entry_data=entry,
            delivery_count=1,
            prepared=prepared[index],
        )


@pytest.mark.asyncio
async def test_new_point_in_same_frame_is_not_lost_after_reload():
    """旧点+新增点同帧：reload 后必须补齐新增点的列。"""
    mapper = _FakeMapper({"1": "p1"}, reload_to={"1": "p1", "2": "p2"})
    consumer = _consumer(mapper)

    await _dispatch_batch(consumer, [_entry("1-0", {"1": "1.5", "2": "2.5"})])

    assert mapper.reload_calls >= 1, "未知点必须触发一次映射重载"
    assert len(consumer._buffer) == 1
    assert consumer._buffer[0].columns == {"p1": 1.5, "p2": 2.5}, \
        "reload 前的预转换结果不得继续使用，新增点不能被漏写"


@pytest.mark.asyncio
async def test_frame_with_only_new_point_is_not_acked_empty():
    """只有新增点的帧：预转换结果为空 dict，reload 后必须重算，不能空列 ACK。"""
    mapper = _FakeMapper({"1": "p1"}, reload_to={"1": "p1", "2": "p2"})
    consumer = _consumer(mapper)

    await _dispatch_batch(consumer, [_entry("1-0", {"2": "2.5"})])

    consumer._r.xack.assert_not_called()
    assert len(consumer._buffer) == 1, "只有新增点的帧不能被当成无可写列直接 ACK"
    assert consumer._buffer[0].columns == {"p2": 2.5}


@pytest.mark.asyncio
async def test_later_entries_of_same_batch_do_not_reuse_stale_conversion():
    """同批第一条触发 reload 后，后续条目持有的旧预转换结果也必须失效。"""
    mapper = _FakeMapper({"1": "p1"}, reload_to={"1": "p1", "2": "p2"})
    consumer = _consumer(mapper)
    entries = [
        _entry("1-0", {"1": "1.5", "2": "2.5"}),
        _entry("2-0", {"1": "3.5", "2": "4.5"}),
    ]

    await _dispatch_batch(consumer, entries)

    assert mapper.reload_calls == 1, "第二条命中的是新映射，不应再次 reload"
    assert len(consumer._buffer) == 2
    assert consumer._buffer[0].columns == {"p1": 1.5, "p2": 2.5}
    assert consumer._buffer[1].columns == {"p1": 3.5, "p2": 4.5}, \
        "同批后续条目不得复用 reload 前算出的列"


@pytest.mark.asyncio
async def test_illegal_new_point_value_still_isolated_after_reload():
    """新增点值非法：必须整帧隔离进 DLQ，不能被旧结果"看不见"而静默丢弃。"""
    mapper = _FakeMapper({"1": "p1"}, reload_to={"1": "p1", "2": "p2"})
    consumer = _consumer(mapper)
    consumer._push_dlq = MagicMock(return_value=True)

    await _dispatch_batch(consumer, [_entry("1-0", {"1": "1.5", "2": "oops"})])

    consumer._push_dlq.assert_called_once()
    assert "2" in consumer._push_dlq.call_args.kwargs["last_error"], \
        "非法的新增点必须出现在 type_conversion 证据里"
    consumer._r.xack.assert_called_once_with("rt:stream:qc:g001:0", "writer", "1-0")
    assert consumer._buffer == [], "非法值不得进入写库缓冲"


@pytest.mark.asyncio
async def test_point_missing_from_mapping_keeps_entry_pending():
    """reload 后仍未知的点：保留在 PEL，既不能 ACK 也不能写库。"""
    mapper = _FakeMapper({"1": "p1"}, reload_to={"1": "p1"})
    consumer = _consumer(mapper)

    await _dispatch_batch(consumer, [_entry("1-0", {"1": "1.5", "3": "3.5"})])

    consumer._r.xack.assert_not_called()
    assert consumer._buffer == [], "映射未命中时不得写入部分点"


@pytest.mark.asyncio
async def test_reload_must_recheck_old_points_before_buffering():
    """刷新后原有点消失、新增点出现时，整帧必须留在 PEL。"""
    mapper = _FakeMapper({"1": "p1"}, reload_to={"3": "p3"})
    consumer = _consumer(mapper)

    await _dispatch_batch(consumer, [_entry("1-0", {"1": "1.5", "3": "3.5"})])

    assert mapper.reload_calls == 1
    consumer._r.xack.assert_not_called()
    assert consumer._buffer == [], "刷新后消失的旧点不能被跳过并写入部分帧"


@pytest.mark.asyncio
async def test_unchanged_mapping_still_uses_converted_fast_path():
    """映射未变时必须继续复用执行单元的转换结果，不能每次都重算。"""
    mapper = _FakeMapper({"1": "p1", "2": "p2"})
    consumer = _consumer(mapper)

    with patch.object(consumer_mod, "_convert_values",
                      wraps=consumer_mod._convert_values) as spy:
        await _dispatch_batch(consumer, [_entry("1-0", {"1": "1.5", "2": "2.5"})])

    assert spy.call_count == 1, "只应在执行单元的批量准备里转换一次"
    assert consumer._buffer[0].columns == {"p1": 1.5, "p2": 2.5}
