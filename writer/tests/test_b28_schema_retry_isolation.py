"""B28 回归：某 stable 补列失败不得扩大到该 stable 在本批的全部行。

修复前的行为：`write_multi_table` 只要某 stable 的 `_ensure_columns` 抛
`SchemaNotReadyError`，就把**该 stable 在本批的所有行**都放进
`_last_local_retry_indices`（按 stable 过滤），于是一整批里"只依赖既有列"的合法行也被扣住。

修复后：按**该行是否引用了缺失列**隔离（以 `_col_cache[stable]` 的已知可用列为准）。
被扣住的行状态必须是 **RETRY**（schema 未就绪）——**不得**按次数判为 BAD_DATA。
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

WRITER_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = WRITER_ROOT.parent
for _candidate in (WRITER_ROOT, REPO_ROOT):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from tdengine_writer import RETRY, SchemaNotReadyError, TDengineWriter  # noqa: E402


def _row(stable: str, child: str, columns: list[str], qualities: dict | None = None) -> dict:
    row = {
        "child_table": child,
        "stable": stable,
        "device_code": child.upper(),
        "ts": 1718000000000,
        "columns": columns,
    }
    if qualities:
        row["qualities"] = qualities
    return row


def _writer(col_cache: dict[str, set[str]]) -> TDengineWriter:
    writer = TDengineWriter.__new__(TDengineWriter)
    writer._database = "device_data"
    writer._metrics = None  # 未绑定观测指标：计时点只执行不记录
    writer._col_cache = col_cache
    writer._lock = __import__("asyncio").Lock()
    writer._last_local_retry_indices = set()
    writer._last_retry_reasons = {}
    for attr in (
        "_last_build_ms", "_last_build_wait_ms", "_last_execute_ms", "_last_sql_bytes",
        "_last_sql_value_rows_attempted", "_last_sql_value_rows_succeeded",
        "_last_sql_quality_rows_attempted", "_last_sql_quality_rows_succeeded",
        "_last_bisect_attempts",
    ):
        setattr(writer, attr, 0)
    writer._last_sql_statement_bytes = []
    # 不真正建连/执行 SQL：本次只验证"哪些行被扣住、以什么状态扣住"
    writer._iter_chunks = lambda rows, skip: iter(())
    return writer




def _retry_indices(results) -> set:
    """结果项形如 (indices: list[int], status, ms)。"""
    out: set = set()
    for indices, status, _ms in results:
        if status == RETRY:
            out.update(indices)
    return out

async def _ensure_columns_raises_for(writer: TDengineWriter, failing: str):
    async def _ensure(stable: str, columns: list[str]) -> None:
        if stable == failing:
            raise SchemaNotReadyError(f"column still missing: {stable}.vNEW")

    async def _ensure_q(q_stable: str) -> None:
        return None

    writer._ensure_columns = _ensure
    writer._ensure_quality_stable = _ensure_q


@pytest.mark.asyncio
async def test_only_rows_depending_on_missing_column_are_held_back() -> None:
    writer = _writer({"sda": {"v0001"}, "sdb": {"v0001"}})
    await _ensure_columns_raises_for(writer, "sda")

    rows = [
        _row("sda", "a1", ["v0001"]),            # 只用既有列 → 应可推进
        _row("sda", "a1", ["v0001", "vNEW"]),    # 依赖缺失列 → 应被扣住
        _row("sdb", "b1", ["v0001"]),            # 另一 stable，好行 → 应可推进
    ]

    results = await writer.write_multi_table(rows)

    held = _retry_indices(results)
    assert held == {1}, f"只有依赖缺失列的那一行应被扣住，实际={held}"
    assert writer._last_retry_reasons == {1: "schema"}
    # 合法行不在 retry 集合里（会进入分块/落库路径）
    assert 0 not in writer._last_local_retry_indices
    assert 2 not in writer._last_local_retry_indices


@pytest.mark.asyncio
async def test_schema_not_ready_is_retry_not_bad_data() -> None:
    """schema 未就绪必须保持 RETRY（可恢复），不得按次数判为坏数据。"""
    writer = _writer({"sda": {"v0001"}})
    await _ensure_columns_raises_for(writer, "sda")

    results = await writer.write_multi_table([_row("sda", "a1", ["vNEW"])])

    assert {status for _indices, status, _ms in results} == {RETRY}
    assert all(status == RETRY for _indices, status, _ms in results), "schema 未就绪必须是 RETRY"


@pytest.mark.asyncio
async def test_row_proceeds_after_the_missing_column_becomes_known() -> None:
    """列补齐后（缓存已含 vNEW），同一行不再被扣住。"""
    writer = _writer({"sda": {"v0001", "vnew"}})
    await _ensure_columns_raises_for(writer, "sda")

    results = await writer.write_multi_table([_row("sda", "a1", ["v0001", "vNEW"])])

    assert results == []
    assert writer._last_local_retry_indices == set()


@pytest.mark.asyncio
async def test_unknown_stable_still_holds_all_its_rows() -> None:
    """缓存整体缺失（无法确认任何列）时保持保守：该 stable 的行仍全部扣住。"""
    writer = _writer({})  # 从未成功 DESCRIBE
    await _ensure_columns_raises_for(writer, "sda")

    results = await writer.write_multi_table(
        [_row("sda", "a1", ["v0001"]), _row("sda", "a2", ["v0002"])]
    )

    assert _retry_indices(results) == {0, 1}

