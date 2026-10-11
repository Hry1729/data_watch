"""TDengineWriter.write_multi_table 单元测试。Unit tests for TDengineWriter.write_multi_table (stage 5 / §7.6 step 1).

Covers cross-subtable packing into one statement, byte-budget chunking, column
actual column sets without synthesized NULL, mixed stables, and chunk-level failure.
The taosws connection is mocked; execute() runs the captured SQL synchronously.
"""

from __future__ import annotations

import math
from unittest.mock import MagicMock, patch

import pytest

import tdengine_writer
from tdengine_writer import BAD_DATA, FAILED_INCOMPLETE, LANDED, RETRY, TDengineWriter
from quality_schema import QualitySchemaError


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
    assert results == [([0, 1], LANDED, results[0][2])]
    assert results[0][1] == LANDED


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
    assert all(status == LANDED for _, status, _ in results)


@pytest.mark.asyncio
async def test_multi_table_missing_columns_are_omitted():
    writer, executed = _make_writer({"sdqc": {"qc1", "qc2"}})
    rows = [
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1000, "columns": {"qc1": 1.0, "qc2": 2.0}},
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1100, "columns": {"qc1": 3.0}},
    ]
    await writer.write_multi_table(rows)
    sql = executed[0]
    assert "(`ts`, `qc1`, `qc2`) VALUES (1000, 1.0, 2.0)" in sql
    assert "(`ts`, `qc1`) VALUES (1100, 3.0)" in sql
    assert "NULL" not in sql


def test_bisected_insert_preserves_each_rows_actual_columns():
    writer, _ = _make_writer({"sdqc": {"qc1", "qc2"}})
    rows = [
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1000, "columns": {"qc1": 1.0}},
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1000, "columns": {"qc2": 2.0}},
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1100, "columns": {"qc1": 3.0}},
    ]
    sql = writer._build_chunk_sql(rows, [0, 1, 2])
    assert "(`ts`, `qc1`) VALUES (1000, 1.0)" in sql
    assert "(`ts`, `qc1`) VALUES (1100, 3.0)" in sql
    assert "(`ts`, `qc2`) VALUES (1000, 2.0)" in sql
    assert "NULL" not in sql


@pytest.mark.parametrize("budget", [180, 3 * 1024 * 1024])
def test_column_runs_preserve_overlapping_timestamp_order(monkeypatch, budget):
    writer, _ = _make_writer({"sdqc": {"qc1", "qc2"}})
    rows = [
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1000, "columns": columns}
        for columns in ({"qc1": 1}, {"qc1": 2, "qc2": 3}, {"qc1": 4})
    ]
    monkeypatch.setattr(tdengine_writer, "MAX_SQL_BYTES", budget)
    chunks = list(writer._iter_chunks(rows))
    assert [i for indices, _, _ in chunks for i in indices] == [0, 1, 2]
    sql = writer._build_chunk_sql(rows, [0, 1, 2])
    assert sql.index("(1000, 1.0)") < sql.index("(1000, 2.0, 3.0)") < sql.index("(1000, 4.0)")


@pytest.mark.asyncio
async def test_column_grouping_keeps_explicit_null_and_column_order():
    writer, executed = _make_writer({"sdqc": {"qc1", "qc2"}})
    rows = [
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1000, "columns": {"qc1": 1.0, "qc2": 2.0}},
        {"child_table": "qc100", "stable": "sdqc", "device_code": "qc100", "ts": 1100, "columns": {"qc2": None, "qc1": 3.0}},
    ]
    results = await writer.write_multi_table(rows)
    assert len(executed) == 1
    assert executed[0].count("USING `device_data`.`sdqc`") == 1
    assert "(1100, 3.0, NULL)" in executed[0]
    assert results[0][0] == [0, 1]
    assert results[0][1] == LANDED


@pytest.mark.asyncio
async def test_multi_table_non_finite_value_becomes_null_without_poisoning_row():
    writer, executed = _make_writer({"sdqc": {"qc1", "qc2"}})
    rows = [
        {
            "child_table": "qc100",
            "stable": "sdqc",
            "device_code": "qc100",
            "ts": 1000,
            "columns": {"qc1": math.nan, "qc2": 2.0},
        },
    ]

    results = await writer.write_multi_table(rows)

    assert results[0][1] == LANDED
    sql = executed[0]
    assert "(1000, NULL, 2.0)" in sql
    assert "nan" not in sql.lower()


def _status_by_row(results) -> dict[int, str]:
    out: dict[int, str] = {}
    for idxs, status, _ in results:
        for i in idxs:
            out[i] = status
    return out


def _rows_distinct_tables(n: int) -> list[dict]:
    return [
        {"child_table": f"qc{i}", "stable": "sdqc", "device_code": f"qc{i}", "ts": 1000 + i, "columns": {"qc1": float(i)}}
        for i in range(n)
    ]


@pytest.mark.asyncio
async def test_data_error_bisects_to_isolate_bad_row():
    """0x060b 数据错 → 二分隔离：坏行 BAD_DATA、好行照常 LANDED。"""
    writer, executed = _make_writer({"sdqc": {"qc1"}})

    def _exec(sql):
        executed.append(sql)
        if "`qc2`" in sql:  # 只有覆盖坏子表的 SQL 失败
            raise RuntimeError("[0x060B] Timestamp data out of range")
        return 1

    writer._conn.execute = MagicMock(side_effect=_exec)
    results = await writer.write_multi_table(_rows_distinct_tables(4))

    status = _status_by_row(results)
    assert status[2] == BAD_DATA
    assert status[0] == status[1] == status[3] == LANDED


@pytest.mark.asyncio
async def test_connection_error_retries_whole_chunk_without_bisect():
    """连接错 → 整块 RETRY，绝不二分（execute 只调用一次）。"""
    writer, _ = _make_writer({"sdqc": {"qc1"}})
    writer._conn.execute = MagicMock(side_effect=ConnectionResetError("connection reset by peer"))
    results = await writer.write_multi_table(_rows_distinct_tables(2))

    assert writer._conn.execute.call_count == 1  # 没有二分递归
    status = _status_by_row(results)
    assert status[0] == status[1] == RETRY


@pytest.mark.asyncio
async def test_unknown_error_retries_whole_chunk_without_bisect():
    """未知错误 → 整块 RETRY，不进 DLQ、不二分。"""
    writer, _ = _make_writer({"sdqc": {"qc1"}})
    writer._conn.execute = MagicMock(side_effect=RuntimeError("some transient service hiccup"))
    results = await writer.write_multi_table(_rows_distinct_tables(2))

    assert writer._conn.execute.call_count == 1
    status = _status_by_row(results)
    assert status[0] == status[1] == RETRY


def test_format_value_fast_path_keeps_the_finite_check():
    """SQL 构建只跳过重复转换，不放松有限值检查，其它入口仍走完整校验。"""
    assert TDengineWriter._format_value(1.5) == "1.5"
    assert TDengineWriter._format_value(0.0) == "0.0"
    assert TDengineWriter._format_value(math.nan) == "NULL"
    assert TDengineWriter._format_value(math.inf) == "NULL"
    assert TDengineWriter._format_value(-math.inf) == "NULL"
    assert TDengineWriter._format_value("2.5") == "2.5"
    assert TDengineWriter._format_value(3) == "3.0"
    assert TDengineWriter._format_value(None) == "NULL"
    assert TDengineWriter._format_value("not-a-number") == "NULL"
    assert TDengineWriter._format_value(object()) == "NULL"


def test_union_cols_single_row_keeps_the_declared_column_order():
    """单行并集必须仍是该行自身的列集合与声明顺序。"""
    rows = [{"columns": {"qc2": 1.0, "qc1": 2.0}}, {"columns": {"qc1": 3.0}}]

    assert TDengineWriter._union_cols(rows, [0]) == ["qc2", "qc1"]
    assert TDengineWriter._union_cols(rows, [1]) == ["qc1"]
    assert TDengineWriter._union_cols(rows, [0, 1]) == ["qc2", "qc1"]


@pytest.mark.asyncio
async def test_single_row_subtable_emits_every_column_and_quality():
    """生产形态：每个子表自成一段时，仍写全列集合与全部质量点。"""
    columns = {f"qc{i}": float(i) for i in range(1, 21)}
    qualities = {str(i): 192 for i in range(1, 21)}
    writer, executed = _make_writer({"sdqc": set(columns)})
    writer._quality_stable_ready = {"sqqc"}  # sdqc 的质量窄表超级表
    rows = [{
        "child_table": "qc100", "stable": "sdqc", "device_code": "qc100",
        "ts": 1000, "columns": columns, "qualities": qualities,
    }]

    results = await writer.write_multi_table(rows)

    assert results[0][1] == LANDED and len(executed) == 1
    sql = executed[0]
    col_list = ", ".join(["`ts`"] + [f"`qc{i}`" for i in range(1, 21)])
    assert f"({col_list}) VALUES (1000, " in sql
    for i in range(1, 21):
        assert f"({1000}, {i}, 192)" in sql


@pytest.mark.asyncio
async def test_sql_too_long_bisects_into_smaller_chunks():
    """0x0219 SQL 过长 → 二分缩块（非直接判坏行），缩到能过则全 LANDED。"""
    writer, executed = _make_writer({"sdqc": {"qc1"}})

    def _exec(sql):
        executed.append(sql)
        if sql.count("USING") > 1:  # 含多个子表片段=过长，缩到单片段才放过
            raise RuntimeError("[0x0219] SQL statement too long")
        return 1

    writer._conn.execute = MagicMock(side_effect=_exec)
    results = await writer.write_multi_table(_rows_distinct_tables(4))

    assert len(executed) > 1  # 确实二分缩块了
    status = _status_by_row(results)
    assert all(s == LANDED for s in status.values())


@pytest.mark.asyncio
async def test_write_includes_quality_narrow_table_fragment():
    """v2 行带 qualities 时，同条 INSERT 附带质量窄表片段并确保 sq* 超级表。"""
    writer, executed = _make_writer({"sdqc": {"qc1", "qc2"}})
    writer._quality_stable_ready = set()

    async def _fake_ensure_quality(q_stable: str) -> None:
        writer._quality_stable_ready.add(q_stable)
        executed.append(f"ENSURE_Q:{q_stable}")

    writer._ensure_quality_stable = _fake_ensure_quality  # type: ignore[method-assign]

    rows = [
        {
            "child_table": "qc100",
            "stable": "sdqc",
            "device_code": "qc100",
            "ts": 1000,
            "columns": {"qc1": 1.0, "qc2": 2.0},
            "qualities": {"101": 0, "102": 2},
        }
    ]
    results = await writer.write_multi_table(rows)

    assert "ENSURE_Q:sqqc" in executed
    insert_sqls = [s for s in executed if s.startswith("INSERT INTO ")]
    assert len(insert_sqls) == 1
    sql = insert_sqls[0]
    assert "`qc100_q`" in sql
    assert "USING `device_data`.`sqqc`" in sql
    assert "`var_id`" in sql
    assert "`quality`" in sql
    assert "(1000, 101, 0)" in sql
    assert "(1000, 102, 2)" in sql
    assert results[0][1] == LANDED


@pytest.mark.asyncio
async def test_write_without_qualities_skips_narrow_fragment():
    """无 qualities 时不创建/写入质量窄表。"""
    writer, executed = _make_writer({"sdqc": {"qc1"}})
    rows = [
        {
            "child_table": "qc100",
            "stable": "sdqc",
            "device_code": "qc100",
            "ts": 1000,
            "columns": {"qc1": 1.0},
        }
    ]
    await writer.write_multi_table(rows)
    assert all("sqqc" not in s and "_q`" not in s for s in executed)


@pytest.mark.asyncio
async def test_ensure_quality_stable_creates_with_composite_key():
    """P0-1：建质量超级表 SQL 含 COMPOSITE KEY，且 DESCRIBE 通过后才标记 ready。"""
    writer, executed = _make_writer({})
    writer._quality_stable_ready = set()

    async def _fake_query(sql: str):
        executed.append(sql)
        return [
            ("ts", "TIMESTAMP", 8, ""),
            ("var_id", "BIGINT", 8, "COMPOSITE KEY"),
            ("quality", "TINYINT UNSIGNED", 1, ""),
            ("device_code", "VARCHAR", 32, "TAG"),
        ]

    writer._query = _fake_query  # type: ignore[method-assign]
    await writer._ensure_quality_stable("sqqc")

    create_sqls = [s for s in executed if s.startswith("CREATE STABLE")]
    assert len(create_sqls) == 1
    assert "COMPOSITE KEY" in create_sqls[0]
    assert "sqqc" in writer._quality_stable_ready


@pytest.mark.asyncio
async def test_ensure_quality_stable_rejects_missing_composite_key():
    """已有旧表缺 COMPOSITE KEY 时不得标记 ready，并留下迁移提示路径。"""
    writer, executed = _make_writer({})
    writer._quality_stable_ready = set()

    async def _fake_query(sql: str):
        return [
            ("ts", "TIMESTAMP", 8, ""),
            ("var_id", "BIGINT", 8, ""),
            ("quality", "TINYINT UNSIGNED", 1, ""),
        ]

    writer._query = _fake_query  # type: ignore[method-assign]
    with pytest.raises(QualitySchemaError):
        await writer._ensure_quality_stable("sqqc")
    assert "sqqc" not in writer._quality_stable_ready


@pytest.mark.asyncio
async def test_quality_fragment_allows_same_ts_multiple_var_ids():
    """同 ts 多 var_id 必须全部进入 INSERT（复合主键契约的写入侧断言）。"""
    writer, _ = _make_writer({"sdqc": {"qc1"}})
    rows = [
        {
            "child_table": "qc100",
            "stable": "sdqc",
            "device_code": "qc100",
            "ts": 1_720_000_000_000,
            "columns": {"qc1": 1.0},
            "qualities": {"1": 192, "2": 192, "3": 0},
        }
    ]
    frag = writer._quality_fragment_for_rows(rows, [0])
    assert frag is not None
    assert "(1720000000000, 1, 192)" in frag
    assert "(1720000000000, 2, 192)" in frag
    assert "(1720000000000, 3, 0)" in frag

@pytest.mark.asyncio
async def test_ensure_quality_stable_rejects_signed_quality_column():
    writer, _ = _make_writer({})
    writer._quality_stable_ready = set()

    async def _fake_query(sql: str):
        return [
            ("ts", "TIMESTAMP", 8, ""),
            ("var_id", "BIGINT", 8, "COMPOSITE KEY"),
            ("quality", "TINYINT", 1, ""),
            ("device_code", "VARCHAR", 32, "TAG"),
        ]

    writer._query = _fake_query  # type: ignore[method-assign]
    with pytest.raises(QualitySchemaError):
        await writer._ensure_quality_stable("sqqc")
    assert "sqqc" not in writer._quality_stable_ready


@pytest.mark.asyncio
async def test_large_quality_frame_splits_with_complete_ack_barrier():
    writer, executed = _make_writer({"sdqc": {"qc1"}})
    writer._quality_stable_ready.add("sqqc")
    rows = [{
        "child_table": "qc100", "stable": "sdqc", "device_code": "qc100",
        "ts": 1000, "columns": {"qc1": 1.0},
        "qualities": {str(i): 0 for i in range(1, 41)},
    }]
    with patch.object(tdengine_writer, "MAX_SQL_BYTES", 300):
        results = await writer.write_multi_table(rows)
    assert len(executed) > 1
    assert all(len(sql.encode("utf-8")) <= 300 for sql in executed)
    assert results[0][0] == [0]
    assert results[0][1] == LANDED


@pytest.mark.asyncio
async def test_large_quality_frame_partial_failure_is_failed_incomplete_not_retry():
    """一条逻辑帧分段执行时部分成功、另一部分不可恢复 → FAILED_INCOMPLETE。

    原先返回 RETRY。但 RETRY 不会回滚已经执行成功的 SQL：
    本用例本身就断言"值行执行成功、部分质量行执行成功"，
    所以"整数帧 RETRY"既不能保证原子性，也让这条帧永远补不齐、无限重试。
    现在改为显式的不完整失败状态：由调用方持久隔离、计入失败、不再占正常资源。
    """
    writer, executed = _make_writer({"sdqc": {"qc1"}})
    writer._quality_stable_ready.add("sqqc")
    rows = [{
        "child_table": "qc100", "stable": "sdqc", "device_code": "qc100",
        "ts": 1000, "columns": {"qc1": 1.0},
        "qualities": {str(i): 0 for i in range(1, 41)},
    }]

    def execute(sql):
        executed.append(sql)
        if "(1000, 25, 0)" in sql:
            raise RuntimeError("[0x0216] invalid quality")
        return 1

    writer._conn.execute = MagicMock(side_effect=execute)
    with patch.object(tdengine_writer, "MAX_SQL_BYTES", 300):
        results = await writer.write_multi_table(rows)
    assert all(len(sql.encode("utf-8")) <= 300 for sql in executed)
    assert results[0][0] == [0]
    assert results[0][1] == FAILED_INCOMPLETE
    assert writer._last_sql_value_rows_attempted >= 1
    assert writer._last_sql_value_rows_succeeded == 1
    assert writer._last_sql_quality_rows_attempted >= 40
    # 明确记录了"部分成功"这个事实：既不是全成功，也不是全失败
    assert 0 < writer._last_sql_quality_rows_succeeded < 40


@pytest.mark.asyncio
async def test_multi_part_all_landed_is_landed():
    writer, _ = _make_writer({"sdqc": {"qc1"}})
    writer._quality_stable_ready.add("sqqc")
    rows = [{
        "child_table": "qc100", "stable": "sdqc", "device_code": "qc100",
        "ts": 1000, "columns": {"qc1": 1.0},
        "qualities": {str(i): 0 for i in range(1, 41)},
    }]
    writer._conn.execute = MagicMock(return_value=1)
    with patch.object(tdengine_writer, "MAX_SQL_BYTES", 300):
        results = await writer.write_multi_table(rows)
    assert results[0][1] == LANDED


@pytest.mark.asyncio
async def test_multi_part_retry_segment_stays_retry():
    """任一分段结果未知/可恢复时仍必须 RETRY，不能当坏帧丢掉。"""
    writer, _ = _make_writer({"sdqc": {"qc1"}})
    rows = [{
        "child_table": "qc100", "stable": "sdqc", "device_code": "qc100",
        "ts": 1000, "columns": {"qc1": 1.0},
        "qualities": {},
    }]
    writer._conn.execute = MagicMock(side_effect=ConnectionResetError("reset"))
    results = await writer.write_multi_table(rows)
    assert results[0][1] == RETRY


@pytest.mark.asyncio
async def test_oversize_value_row_is_retained_without_sql_execution():
    writer, executed = _make_writer({"sdqc": {"qc1"}})
    rows = [{
        "child_table": "qc100", "stable": "sdqc", "device_code": "qc100",
        "ts": 1000, "columns": {"qc1": 1.0},
    }]
    with patch.object(tdengine_writer, "MAX_SQL_BYTES", 40):
        results = await writer.write_multi_table(rows)
    assert executed == []
    assert results[0][1] == RETRY



@pytest.mark.asyncio
async def test_unready_quality_schema_retries_only_affected_stable():
    writer, executed = _make_writer({"sdqc": {"qc1"}, "sdasc": {"a1"}})

    async def ensure_quality(stable):
        if stable == "sqqc":
            raise QualitySchemaError("old quality schema")
        writer._quality_stable_ready.add(stable)

    writer._ensure_quality_stable = ensure_quality
    rows = [
        {
            "child_table": "qc100", "stable": "sdqc", "device_code": "qc100",
            "ts": 1000, "columns": {"qc1": 1.0}, "qualities": {"1": 0},
        },
        {
            "child_table": "asc7", "stable": "sdasc", "device_code": "asc7",
            "ts": 1000, "columns": {"a1": 2.0}, "qualities": {"2": 0},
        },
    ]
    results = await writer.write_multi_table(rows)
    assert _status_by_row(results) == {0: RETRY, 1: LANDED}
    assert writer._last_local_retry_indices == {0}
    assert all("qc100" not in sql for sql in executed)
    assert any("asc7" in sql for sql in executed)


@pytest.mark.asyncio
async def test_execute_timeout_releases_worker_and_detaches_connection(monkeypatch):
    import asyncio
    import threading
    writer, _ = _make_writer({})
    release = threading.Event()
    writer._conn.execute.side_effect = lambda sql: release.wait(2)
    monkeypatch.setattr(tdengine_writer, "IO_TIMEOUT_SECONDS", 0.02)
    try:
        ok, error = await asyncio.wait_for(writer._execute_ex("SELECT 1"), timeout=0.5)
        assert not ok
        assert isinstance(error, TimeoutError)
        assert writer._conn is None
    finally:
        release.set()
