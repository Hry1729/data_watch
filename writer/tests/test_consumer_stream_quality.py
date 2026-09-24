"""Stream v1/v2 质量消费测试（Q1）。"""

from __future__ import annotations

import json
from unittest.mock import MagicMock, patch

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
            "pending_max_retry": 3,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }


def _mapping() -> GroupMapping:
    return GroupMapping(
        config_version=0,
        child_table="qc100",
        stable="sdqc",
        var_map={"101": "qc1", "102": "qc2"},
    )


def _consumer():
    writer = MagicMock()
    redis_client = MagicMock()
    mapper = MagicMock()
    mapping = _mapping()
    mapper.get_cached.return_value = mapping
    mapper.get_or_load.return_value = mapping
    return (
        StreamConsumer(
            _config(), mapper, writer, redis_client, metrics=WriterPerformanceMetrics()
        ),
        redis_client,
    )


def _entry(
    values: dict,
    *,
    contract_version: int | None = None,
    qualities: dict | str | None = None,
) -> dict:
    data = {
        "device_code": "qc100",
        "device_type": "qc",
        "group_code": "g001",
        "ts": "1000",
        "config_version": "0",
        "values": json.dumps(values),
    }
    if contract_version is not None:
        data["contract_version"] = str(contract_version)
    if qualities is not None:
        data["qualities"] = (
            qualities if isinstance(qualities, str) else json.dumps(qualities)
        )
    return data


@pytest.mark.asyncio
async def test_v1_entry_buffers_without_qualities():
    """v1（无 contract_version）按 legacy unknown：不写质量窄表。"""
    consumer, _ = _consumer()
    with patch.object(consumer_mod, "push_dlq") as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0", "1-0", _entry({"101": "12.5", "102": "3"}), 1
        )
    dlq.assert_not_called()
    assert len(consumer._buffer) == 1
    row = consumer._buffer[0]
    assert row.columns == {"qc1": 12.5, "qc2": 3.0}
    assert row.qualities == {}


@pytest.mark.asyncio
async def test_v2_entry_buffers_qualities():
    consumer, _ = _consumer()
    with patch.object(consumer_mod, "push_dlq") as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "2-0",
            _entry(
                {"101": "1.0", "102": "2.0"},
                contract_version=2,
                qualities={"101": 0, "102": 2},
            ),
            1,
        )
    dlq.assert_not_called()
    row = consumer._buffer[0]
    assert row.qualities == {"101": 0, "102": 2}


@pytest.mark.asyncio
async def test_v1_and_v2_mixed_consumption():
    """同一 consumer 可混合缓冲 v1 与 v2。"""
    consumer, _ = _consumer()
    with patch.object(consumer_mod, "push_dlq") as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0", "1-0", _entry({"101": "1"}), 1
        )
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "2-0",
            _entry({"102": "2"}, contract_version=2, qualities={"102": 5}),
            1,
        )
    dlq.assert_not_called()
    assert len(consumer._buffer) == 2
    assert consumer._buffer[0].qualities == {}
    assert consumer._buffer[1].qualities == {"102": 5}


@pytest.mark.asyncio
async def test_v2_missing_quality_whole_entry_dlq():
    """v2 某点缺质量 → 整 entry DLQ（键集合必须对齐）。"""
    consumer, redis_client = _consumer()
    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "3-0",
            _entry(
                {"101": "1.0", "102": "2.0"},
                contract_version=2,
                qualities={"101": 0},
            ),
            1,
        )
    assert consumer._buffer == []
    assert "quality_key_mismatch" in dlq.call_args.kwargs["last_error"]
    redis_client.xack.assert_called_once()


@pytest.mark.asyncio
async def test_v2_extra_quality_whole_entry_dlq():
    """v2 多余质量键 → 整 entry DLQ。"""
    consumer, _ = _consumer()
    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "3-1",
            _entry(
                {"101": "1.0"},
                contract_version=2,
                qualities={"101": 0, "102": 0},
            ),
            1,
        )
    assert consumer._buffer == []
    assert "quality_key_mismatch" in dlq.call_args.kwargs["last_error"]


@pytest.mark.asyncio
async def test_unsupported_contract_version_dlq():
    """非法 contract_version 不得降级为 v1，应进 DLQ。"""
    consumer, _ = _consumer()
    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "3-2",
            _entry({"101": "1.0"}, contract_version=999, qualities={"101": 0}),
            1,
        )
    assert consumer._buffer == []
    assert "Unsupported contract_version" in dlq.call_args.kwargs["last_error"]


@pytest.mark.asyncio
async def test_v2_illegal_quality_whole_entry_dlq():
    consumer, redis_client = _consumer()
    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "4-0",
            _entry(
                {"101": "1.0"},
                contract_version=2,
                qualities={"101": 256},
            ),
            1,
        )
    assert consumer._buffer == []
    dlq.assert_called_once()
    assert "invalid_quality" in dlq.call_args.kwargs["last_error"]
    assert "101" in dlq.call_args.kwargs["last_error"]
    redis_client.xack.assert_called_once()


@pytest.mark.asyncio
async def test_v2_bool_quality_rejected():
    """bool 不是合法质量码（与 is_valid_quality 一致）。"""
    consumer, _ = _consumer()
    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry(
            "rt:stream:qc:g001:0",
            "5-0",
            _entry(
                {"101": "1.0"},
                contract_version=2,
                qualities={"101": True},
            ),
            1,
        )
    assert consumer._buffer == []
    assert "invalid_quality" in dlq.call_args.kwargs["last_error"]


@pytest.mark.asyncio
async def test_flush_passes_qualities_to_writer():
    consumer, _ = _consumer()
    await consumer._handle_entry(
        "rt:stream:qc:g001:0",
        "6-0",
        _entry(
            {"101": "1.5"},
            contract_version=2,
            qualities={"101": 0},
        ),
        1,
    )

    async def _write(rows):
        assert rows[0]["qualities"] == {"101": 0}
        return [([0], "landed", 1.0)]

    consumer._writer.write_multi_table = _write
    await consumer._flush()
    assert consumer._buffer == []


@pytest.mark.asyncio
async def test_prepared_batch_matches_v2_and_invalid_value_semantics():
    good = _entry(
        {"101": "1.0", "102": "2.0"}, contract_version=2,
        qualities={"101": 0, "102": 2},
    )
    bad = _entry(
        {"101": "not-a-number"}, contract_version=2, qualities={"101": 0}
    )
    prepared = consumer_mod._prepare_payload_batch([good, bad], 1024 * 1024)
    assert len(prepared) == 2
    assert prepared[0].raw_values == {"101": "1.0", "102": "2.0"}
    assert prepared[1].raw_values == {"101": "not-a-number"}
    consumer, redis_client = _consumer()
    with patch.object(consumer_mod, "push_dlq", return_value=True) as dlq:
        await consumer._handle_entry("s", "1-0", good, 1, prepared=prepared[0])
        await consumer._handle_entry("s", "2-0", bad, 1, prepared=prepared[1])
    assert len(consumer._buffer) == 1
    assert consumer._buffer[0].columns == {"qc1": 1.0, "qc2": 2.0}
    assert consumer._buffer[0].qualities == {"101": 0, "102": 2}
    assert dlq.call_count == 1
    assert "type_conversion" in dlq.call_args.kwargs["last_error"]
    redis_client.xack.assert_called_once()


@pytest.mark.asyncio
async def test_run_loop_prepares_one_bounded_batch_and_preserves_order():
    import asyncio
    import time
    from unittest.mock import AsyncMock

    consumer, redis_client = _consumer()
    consumer.drain_pending = AsyncMock()
    consumer._last_flush = time.monotonic()
    consumer._flush_interval_s = 1000
    redis_client.xreadgroup.side_effect = [
        [("s", [
            ("1-0", _entry({"101": "1"})),
            ("2-0", _entry({"102": "2"}, contract_version=2, qualities={"102": 0})),
        ])],
        asyncio.CancelledError(),
    ]
    consumer._batch_count = 2
    with patch.object(
        consumer_mod, "_prepare_payload_batch", wraps=consumer_mod._prepare_payload_batch
    ) as prepare:
        with pytest.raises(asyncio.CancelledError):
            await consumer.run_loop(["s"])
    assert prepare.call_count == 1
    assert [row.entry_id for row in consumer._buffer] == ["1-0", "2-0"]
    assert consumer._buffer[1].qualities == {"102": 0}


def test_batch_decoder_never_parses_raw_payload_beyond_32mib_budget():
    large_value = "x" * (1024 * 1024)
    entries = [{"values": large_value} for _ in range(33)]
    with patch.object(consumer_mod.json, "loads", return_value={"101": "1"}) as loads:
        prepared = consumer_mod._prepare_payload_batch(entries, 32 * 1024 * 1024)
    decoded_count = sum(item.decoded for item in prepared)
    assert 0 < decoded_count < len(entries)
    assert loads.call_count == decoded_count
    assert all(not item.decoded for item in prepared[decoded_count:])
