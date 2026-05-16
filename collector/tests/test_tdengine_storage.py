"""TDengine 新模型存储测试。"""

from datetime import datetime
from unittest.mock import AsyncMock

import pytest

from core.config import DeviceConfig, TDengineConfig, VariableConfig
from core.exceptions import StorageError
from data_queue.buffer import DataPoint
from storage.batch_writer import BatchWriter
from storage.tdengine import TDengineConnection


@pytest.mark.asyncio
async def test_init_storage_topology_builds_variable_targets():
    """建表拓扑应映射到设备类型数据库 + 设备超级表 + 变量子表。"""
    tdengine = TDengineConnection(TDengineConfig())
    tdengine.execute = AsyncMock(return_value=0)
    tdengine._stable_exists = AsyncMock(return_value=False)
    tdengine._table_exists = AsyncMock(return_value=False)

    devices = [
        DeviceConfig(
            device_id="qc100",
            device_type="QC",
            protocol="s7",
            ip="127.0.0.1",
            scan_rate=1,
            device_pk=991001,
        )
    ]
    variables = [
        VariableConfig(
            variable_id="992001",
            device_id="qc100",
            address="DB100.DBD0",
            data_type="REAL",
            scan_rate=1,
            is_storage=True,
            var_seq=30,
        )
    ]

    await tdengine.init_storage_topology(devices, variables)

    target = tdengine.get_storage_target("992001")
    assert target.database_name == "qc"
    assert target.stable_name == "qc100"
    assert target.table_name == "qc100_30"
    assert tdengine.execute.await_count == 3
    stable_sql = tdengine.execute.await_args_list[1].args[0]
    assert "`quality` TINYINT" in stable_sql
    assert "DEFAULT" not in stable_sql


@pytest.mark.asyncio
async def test_batch_writer_groups_by_variable_subtable_without_variable_id_column():
    """写入 SQL 应包含 ts/value/quality，并落到变量子表。"""
    tdengine = TDengineConnection(TDengineConfig())
    tdengine.execute = AsyncMock(return_value=1)
    tdengine._stable_exists = AsyncMock(return_value=False)
    tdengine._table_exists = AsyncMock(return_value=False)

    devices = [
        DeviceConfig(
            device_id="qc100",
            device_type="QC",
            protocol="s7",
            ip="127.0.0.1",
            scan_rate=1,
            device_pk=991001,
        )
    ]
    variables = [
        VariableConfig(
            variable_id="992001",
            device_id="qc100",
            address="DB100.DBD0",
            data_type="REAL",
            scan_rate=1,
            is_storage=True,
            var_seq=30,
        )
    ]
    await tdengine.init_storage_topology(devices, variables)
    tdengine.execute.reset_mock()

    writer = BatchWriter(tdengine)
    now = datetime(2026, 4, 18, 12, 0, 0, 123000)
    points = [
        DataPoint(device_id="qc100", variable_id="992001", value=1.5, timestamp=now),
        DataPoint(device_id="qc100", variable_id="992001", value=2.5, timestamp=now, quality=2),
    ]

    await writer.write(points)

    sql = tdengine.execute.await_args.args[0]
    assert "INSERT INTO `qc`.`qc100_30` (`ts`, `value`, `quality`)" in sql
    assert "variable_id" not in sql
    assert sql.count("('2026-04-18 12:00:00.123'") == 1
    assert "2.5, 2" in sql
    assert writer.stats["total_written"] == 1
    assert writer.stats["total_failed"] == 1


@pytest.mark.asyncio
async def test_stable_schema_requires_quality_column_and_var_seq_tag():
    tdengine = TDengineConnection(TDengineConfig())

    tdengine._fetch_all = AsyncMock(return_value=[
        {"field": "ts", "note": ""},
        {"field": "value", "note": ""},
        {"field": "quality", "note": ""},
        {"field": "var_seq", "note": "TAG"},
    ])
    assert await tdengine._stable_has_correct_schema("qc", "qc100") is True

    tdengine._fetch_all = AsyncMock(return_value=[
        {"field": "ts", "note": ""},
        {"field": "value", "note": ""},
        {"field": "var_seq", "note": "TAG"},
    ])
    assert await tdengine._stable_has_correct_schema("qc", "qc100") is False


@pytest.mark.asyncio
async def test_batch_writer_raises_on_partial_subtable_failure():
    """任一变量子表写入失败时，不应把批次误判为成功。"""
    tdengine = TDengineConnection(TDengineConfig())

    devices = [
        DeviceConfig("qc100", "QC", "s7", "127.0.0.1", scan_rate=1, device_pk=991001)
    ]
    variables = [
        VariableConfig("992001", "qc100", "DB100.DBD0", "REAL", 1, True, 30),
        VariableConfig("992002", "qc100", "DB104.DBD0", "REAL", 1, True, 31),
    ]
    tdengine.execute = AsyncMock(return_value=0)
    tdengine._stable_exists = AsyncMock(return_value=False)
    tdengine._table_exists = AsyncMock(return_value=False)
    await tdengine.init_storage_topology(devices, variables)

    async def execute_side_effect(sql):
        if "`qc100_31`" in sql:
            raise StorageError("simulated write failure")
        return 1

    tdengine.execute = AsyncMock(side_effect=execute_side_effect)
    writer = BatchWriter(tdengine, max_retries=1, retry_delay=0)
    now = datetime(2026, 4, 18, 12, 0, 0, 123000)
    points = [
        DataPoint(device_id="qc100", variable_id="992001", value=1.5, timestamp=now),
        DataPoint(device_id="qc100", variable_id="992002", value=2.5, timestamp=now),
    ]

    with pytest.raises(StorageError, match="partially failed"):
        await writer.write(points)

    assert writer.stats["total_written"] == 1
    assert writer.stats["total_failed"] == 1
