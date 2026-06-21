"""Unit tests for TDengineWriter.write_multi_table (stage 5 / §7.6 step 1).

Covers cross-subtable packing into one statement, byte-budget chunking, column
union with NULL for missing columns, mixed stables, and chunk-level failure.
The taosws connection is mocked; execute() runs the captured SQL synchronously.
"""

from __future__ import annotations

from unittest.mock import MagicMock, patch

import pytest

import tdengine_writer
from tdengine_writer import TDengineWriter


def _make_writer(col_cache: dict[str, set[str]]) -> tuple[TDengineWriter, list[str]]:
    writer = TDengineWriter()
    writer._database = "device_data"
    writer._col_cache = col_cache  # primed → _ensure_columns issues no ALTER
    executed: list[str] = []
    conn = MagicMock()
    conn.execute = MagicMock(side_effect=lambda sql: executed.append(sql) or 1)
    writer._conn = conn
    return writer, executed


@pytest.mark.asyncio
async def test_multi_table_packs_many_subtables_into_one_statement():
    writer, executed = _make_writer({"sdqc": {"qc1", "qc2"}})
    rows = [
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1000, "columns": {"qc1": 1.0}},
        {"child_table": "qc101", "stable": "sdqc", "device_code": "qc101", "ts": 1000, "columns": {"qc2": 2.0}},
    ]
    results = await writer.write_multi_table(rows)

    assert len(executed) == 1  # 一条 INSERT 覆盖两个子表
    sql = executed[0]
    assert sql.startswith("INSERT INTO ")
    assert "`device_data`.`qc100`" in sql
    assert "`device_data`.`qc101`" in sql
    assert results == [([0, 1], True, results[0][2])]
    assert results[0][1] is True


@pytest.mark.asyncio
async def test_multi_table_mixes_stables_in_one_statement():
    writer, executed = _make_writer({"sdqc": {"qc1"}, "sdasc": {"a1"}})
    rows = [
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1, "columns": {"qc1": 1.0}},
        {"child_table": "asc7", "stable": "sdasc", "device_code": "asc7", "ts": 1, "columns": {"a1": 9.0}},
    ]
    await writer.write_multi_table(rows)
    sql = executed[0]
    assert "USING `device_data`.`sdqc`" in sql
    assert "USING `device_data`.`sdasc`" in sql


@pytest.mark.asyncio
async def test_multi_table_chunks_by_byte_budget():
    writer, executed = _make_writer({"sdqc": {"qc1"}})
    rows = [
        {"child_table": f"qc{i}", "stable": "sdqc", "device_code": f"qc{i}", "ts": 1000 + i, "columns": {"qc1": float(i)}}
        for i in range(6)
    ]
    with patch.object(tdengine_writer, "MAX_SQL_BYTES", 200):
        results = await writer.write_multi_table(rows)

    assert len(executed) >= 2  # 被字节预算切成多块
    for sql in executed:
        assert len(sql.encode("utf-8")) <= 200
    covered = sorted(i for idxs, _, _ in results for i in idxs)
    assert covered == list(range(6))  # 每行恰好被覆盖一次
    assert all(ok for _, ok, _ in results)


@pytest.mark.asyncio
async def test_multi_table_missing_column_becomes_null():
    writer, executed = _make_writer({"sdqc": {"qc1", "qc2"}})
    rows = [
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1000, "columns": {"qc1": 1.0, "qc2": 2.0}},
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1100, "columns": {"qc1": 3.0}},
    ]
    await writer.write_multi_table(rows)
    sql = executed[0]
    # 列并集 (ts, qc1, qc2)，第二行缺 qc2 → NULL
    assert "(1100, 3.0, NULL)" in sql


@pytest.mark.asyncio
async def test_multi_table_chunk_failure_marks_rows_failed():
    writer, _ = _make_writer({"sdqc": {"qc1"}})
    writer._conn.execute = MagicMock(side_effect=RuntimeError("[0x060B] Timestamp data out of range"))
    rows = [
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1000, "columns": {"qc1": 1.0}},
    ]
    results = await writer.write_multi_table(rows)
    assert len(results) == 1
    indices, ok, _ = results[0]
    assert indices == [0]
    assert ok is False
