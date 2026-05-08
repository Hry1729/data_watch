"""数据聚合器测试"""

import asyncio

import pytest

from core.config import CollectorConfig
from data_queue.aggregator import DataAggregator
from data_queue.buffer import DataBuffer, DataPoint


@pytest.mark.asyncio
async def test_aggregator_dedupes_unchanged_values():
    config = CollectorConfig(batch_size=10, flush_interval=10, max_window_seconds=60)
    buffer = DataBuffer(max_size=100)
    aggregator = DataAggregator(config, buffer)

    aggregator.add(DataPoint("TEST001", "temp", 25.0))
    aggregator.add(DataPoint("TEST001", "temp", 25.0))
    aggregator.add(DataPoint("TEST001", "temp", 26.0))

    assert aggregator.cache_size == 2
    assert aggregator.stats["total_deduped"] == 1


@pytest.mark.asyncio
async def test_aggregator_flush_to_output_submits_batch():
    config = CollectorConfig(batch_size=2, flush_interval=10, max_window_seconds=60)
    buffer = DataBuffer(max_size=100)
    aggregator = DataAggregator(config, buffer)
    flushed_batches = []

    def flush_callback(points):
        flushed_batches.append(points)

    aggregator.set_output_callback(flush_callback)
    aggregator.add(DataPoint("TEST001", "temp", 25.0))
    aggregator.add(DataPoint("TEST001", "temp", 26.0))

    submitted = await aggregator.flush_to_output()

    assert submitted == 2
    assert aggregator.cache_size == 0
    assert len(flushed_batches) == 1
    assert len(flushed_batches[0]) == 2


@pytest.mark.asyncio
async def test_aggregator_run_flushes_by_interval():
    config = CollectorConfig(batch_size=100, flush_interval=1, max_window_seconds=60)
    buffer = DataBuffer(max_size=100)
    aggregator = DataAggregator(config, buffer)
    flushed = []

    def flush_callback(points):
        flushed.extend(points)

    aggregator.set_output_callback(flush_callback)
    await buffer.put(DataPoint("TEST001", "temp", 25.0))

    task = asyncio.create_task(aggregator.run())
    await asyncio.sleep(1.2)
    aggregator.stop()
    await task

    assert len(flushed) == 1
