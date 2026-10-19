"""严重-2（日志部分）与低-1 回归。

严重-2：边缘模式下写出目标是 Edge Storage，executor 却记 "Redis write failed ..."，
现场据此去查根本不存在的 Redis。日志必须写实际目标。

低-1：``int(scan_rate * 1000)`` 截断浮点误差，1.001 s → 1000 ms，window key /
stream scan_rate / 边缘 assignment 的 scan_rate_ms 对不上。改用 ``round``。
"""

from __future__ import annotations

import logging
from datetime import datetime
from types import SimpleNamespace
from unittest.mock import MagicMock

import pytest

from collector.executor import TaskExecutor
from core.config import CollectorConfig, DeviceConfig, VariableConfig
from data_queue.types import DataPoint
from monitor.ingest_server import _IngestHTTPServer, _IngestRequestHandler


def _variable(variable_id="v1", address="A1"):
    return SimpleNamespace(
        variable_id=variable_id,
        address=address,
        data_type="REAL",
        is_storage=True,
        group_code="g001",
        group_config_version=1,
    )


def _task(scan_rate=0.1):
    async def _read(_blocks):
        return {"A1": 1.5}

    driver = SimpleNamespace(is_connected=True, read=_read, native_quality=False)
    device = SimpleNamespace(
        device_id="DEV1", device_type="QC", driver=driver, connection_config={}
    )
    scan_task = SimpleNamespace(
        scan_rate=scan_rate,
        variables=[_variable()],
        read_blocks=[SimpleNamespace(address="A1", count=1, data_type="REAL")],
        result_map={},
    )
    return SimpleNamespace(device=device, scan_task=scan_task, task_name="DEV1_t", generation=0)


@pytest.mark.asyncio
@pytest.mark.parametrize(
    ("storage_path", "sink"),
    [("sqlite_outbox", "Edge Storage"), ("redis_only", "Redis"), ("sqlite_outbox_redis_aof", "Redis")],
)
async def test_write_failure_log_names_actual_sink(caplog, storage_path, sink):
    writer = MagicMock()
    writer.storage_path = storage_path
    writer.write_window_stream.side_effect = RuntimeError("boom")
    executor = TaskExecutor(redis_writer=writer)

    with caplog.at_level(logging.ERROR):
        await executor.execute(_task())

    messages = [r.getMessage() for r in caplog.records if r.levelno >= logging.ERROR]
    assert any(m.startswith(f"{sink} write failed after successful read") for m in messages), messages
    assert any(m.startswith(f"{sink} write failed for device DEV1") for m in messages), messages
    if sink != "Redis":
        assert not any("Redis" in m for m in messages), messages
    assert executor.stats["total_errors"] == 1


def test_executor_scan_rate_ms_is_rounded_not_truncated():
    writer = MagicMock()
    executor = TaskExecutor(redis_writer=writer)
    device = SimpleNamespace(device_id="DEV1", device_type="QC")
    scan_task = SimpleNamespace(scan_rate=1.001, variables=[_variable()])
    executor._write_to_redis(
        device, scan_task, [DataPoint("DEV1", "v1", 1.0, timestamp=datetime.now())]
    )
    args = writer.write_window_stream.call_args.args
    assert args[3] == 1001
    assert args[6]["scan_rate"] == 1001


def test_ingest_scan_rate_ms_is_rounded_not_truncated():
    writer = MagicMock()
    config = CollectorConfig(
        devices=[DeviceConfig("AGV801", "AGV", "opcda_sidecar", "127.0.0.1")],
        variables=[
            VariableConfig(
                variable_id="v1",
                device_id="AGV801",
                address="OPD_Server.AGV801.ViewMain.V1",
                data_type="REAL",
                scan_rate=1.001,
                is_storage=True,
                var_seq=1,
                group_code="g001",
            )
        ],
    )
    server = _IngestHTTPServer(
        ("127.0.0.1", 0),
        _IngestRequestHandler,
        ingest_path="/ingest",
        api_key=None,
        max_batch_size=100,
        redis_writer=writer,
        realtime_config=config,
    )
    try:
        accepted, errors = server.write_points_to_redis([DataPoint("AGV801", "v1", 1.0)])
    finally:
        server.server_close()

    assert errors == []
    assert accepted == 1
    # scan_rate_ms 在 write_window_stream / 旧 write_window 中都是第 4 个位置参数
    call = writer.write_window_stream.call_args or writer.write_window.call_args
    assert call.args[3] == 1001
