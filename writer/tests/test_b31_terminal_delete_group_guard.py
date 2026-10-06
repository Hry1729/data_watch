"""B31 回归：终态删除保护必须覆盖**所有**删除路径，且未知拓扑 fail-closed。

本轮（工作单阶段一 · 问题 2）核实到两个真实缺口：
1. `_retry_pending_acks()` 用**默认** `allow_delete=True` 调 `_delete_ids()`，**绕过**多消费组判定；
2. `_may_delete_terminal()` 在**查询失败/形态不可读**时 `return True`（fail-open），即"未知拓扑仍做
   全局删除"。

修复后的语义（本文件锁定）：
- `XDEL` 是**全局删除**，只有**确认恰好一个消费组**才允许；**读到多组**或**无法确认**都**保留数据**；
- **未知结果不写缓存**（下一轮重探）；只有"确认单组"的结论才缓存 `_GROUP_TOPOLOGY_TTL_S`；
- **ACK 不受影响**：本组该做的 XACK 照常执行，删除只是可选清理；
- 所有删除路径（`_ack_ids` / `_ack_single` / `_retry_pending_acks` / `_retry_pending_deletes`）
  遵守**同一个**安全条件；
- `_delete_ids()` 自身仍**只**做 `xdel`（判定由调用方通过参数传入），以免扰动 xack→xdel 序列。
"""

from __future__ import annotations

import sys
from pathlib import Path
from unittest.mock import MagicMock

import pytest

WRITER_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = WRITER_ROOT.parent
for _candidate in (WRITER_ROOT, REPO_ROOT):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

import consumer as consumer_module  # noqa: E402
from consumer import StreamConsumer  # noqa: E402
from performance import WriterPerformanceMetrics  # noqa: E402

STREAM = "rt:stream:qc:g001:0"
MULTI = [{"name": "writer"}, {"name": "other"}]
SINGLE = [{"name": "writer"}]


def _config() -> dict:
    return {
        "writer": {
            "flush_interval_ms": 500,
            "batch_count": 5,
            "max_buffer_rows": 10,
            "pending_min_idle_ms": 0,
            "max_row_retry": 3,
        },
        "redis": {"group_name": "writer", "consumer_name": "writer-1"},
    }


def _consumer(groups) -> StreamConsumer:
    client = MagicMock()
    client.xinfo_groups.return_value = groups
    return StreamConsumer(
        _config(), MagicMock(), MagicMock(), client, metrics=WriterPerformanceMetrics()
    )


# ── 判定语义 ────────────────────────────────────────────────────────────────


def test_single_group_allows_terminal_delete() -> None:
    consumer = _consumer(SINGLE)
    assert consumer._may_delete_terminal(STREAM) is True
    consumer._delete_ids(STREAM, ["1-0"], allow_delete=True)
    consumer._r.xdel.assert_called_once_with(STREAM, "1-0")


def test_multi_group_refuses_and_records_metric() -> None:
    consumer = _consumer(MULTI)
    assert consumer._may_delete_terminal(STREAM) is False
    assert consumer._metrics._counters.get("retry_terminal_delete_multi_group_skipped_rows") == 1


def test_query_error_preserves_entries_fail_closed() -> None:
    """无法确认拓扑 ⇒ **保留**（旧实现这里是 fail-open，会做全局删除）。"""
    consumer = _consumer(SINGLE)
    consumer._r.xinfo_groups.side_effect = Exception("ERR unknown command")

    assert consumer._may_delete_terminal(STREAM) is False
    assert consumer._metrics._counters.get("retry_terminal_delete_topology_unknown_rows") == 1


def test_unreadable_topology_preserves_entries_fail_closed() -> None:
    consumer = _consumer("nonsense")

    assert consumer._may_delete_terminal(STREAM) is False
    assert consumer._metrics._counters.get("retry_terminal_delete_topology_unknown_rows") == 1


def test_unknown_topology_is_not_cached() -> None:
    """未知结果不得固化：下一轮必须重探（否则一次抖动会长时间停掉清理）。"""
    consumer = _consumer("nonsense")

    consumer._may_delete_terminal(STREAM)
    consumer._may_delete_terminal(STREAM)

    assert consumer._r.xinfo_groups.call_count == 2


def test_confirmed_single_group_is_cached_until_ttl(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """只有"确认单组"才缓存；TTL 过后必须重探（残差窗口即由此 TTL 界定）。"""
    monkeypatch.setattr(consumer_module, "_GROUP_TOPOLOGY_TTL_S", 0.0)
    consumer = _consumer(SINGLE)

    consumer._may_delete_terminal(STREAM)
    consumer._may_delete_terminal(STREAM)

    assert consumer._r.xinfo_groups.call_count == 2


# ── 所有删除路径遵守同一条件，且 ACK 不受影响 ───────────────────────────────


@pytest.mark.parametrize("path", ["ack_ids", "ack_single", "retry_pending_acks"])
def test_every_delete_path_preserves_on_multi_group_but_still_acks(path: str) -> None:
    consumer = _consumer(MULTI)

    if path == "ack_ids":
        consumer._ack_ids(STREAM, ["1-0"])
    elif path == "ack_single":
        consumer._ack_single(STREAM, "1-0")
    else:
        consumer._ack_retry = {STREAM: {"1-0"}}
        consumer._retry_pending_acks()

    consumer._r.xack.assert_called_once()  # 本组该做的 ACK 必须照常执行
    assert consumer._r.xdel.call_count == 0, "多消费组下不得全局删除"
    assert consumer._delete_retry == {}, "已 ACK 的条目不再占本地容量"


@pytest.mark.parametrize("path", ["ack_ids", "retry_pending_acks"])
def test_every_delete_path_deletes_on_confirmed_single_group(path: str) -> None:
    consumer = _consumer(SINGLE)

    if path == "ack_ids":
        consumer._ack_ids(STREAM, ["1-0"])
    else:
        consumer._ack_retry = {STREAM: {"1-0"}}
        consumer._retry_pending_acks()

    consumer._r.xack.assert_called_once()
    consumer._r.xdel.assert_called_once_with(STREAM, "1-0")


def test_retry_pending_deletes_skips_multi_group_and_releases_ids() -> None:
    consumer = _consumer(MULTI)
    consumer._delete_retry = {STREAM: {"1-0"}}

    consumer._retry_pending_deletes()

    assert consumer._r.xdel.call_count == 0
    assert consumer._delete_retry == {}


def test_retry_pending_deletes_still_deletes_single_group() -> None:
    consumer = _consumer(SINGLE)
    consumer._delete_retry = {STREAM: {"1-0"}}

    consumer._retry_pending_deletes()

    consumer._r.xdel.assert_called_once_with(STREAM, "1-0")
    assert consumer._delete_retry == {}


# ── 设计约束：_delete_ids 内不得有 redis 交互 ────────────────────────────────


def test_delete_ids_issues_no_redis_call_when_refused() -> None:
    consumer = _consumer(SINGLE)

    consumer._delete_ids(STREAM, ["1-0"], allow_delete=False)

    assert consumer._r.xdel.call_count == 0
    assert consumer._r.xinfo_groups.call_count == 0, "拓扑判定不得发生在 _delete_ids 内"
    assert consumer._delete_retry == {}


def test_discriminating_power_default_would_delete() -> None:
    """判别力：不传 `allow_delete`（调用方必须显式传入）时确实会删除，说明参数是安全闸门。"""
    consumer = _consumer(MULTI)

    consumer._delete_ids(STREAM, ["1-0"])  # 旧 `_retry_pending_acks` 就是这样调的

    consumer._r.xdel.assert_called_once_with(STREAM, "1-0")


def test_multi_group_ack_does_not_exhaust_capacity():
    consumer = _consumer(MULTI)
    capacity = consumer._remaining_capacity()
    for i in range(30):
        consumer._ack_single(STREAM, f"{i}-0")
    assert consumer._remaining_capacity() == capacity
    assert consumer._delete_retry == {}
    consumer._r.xdel.assert_not_called()
