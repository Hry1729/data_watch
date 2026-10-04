"""B31 回归：多消费组下**拒绝**终态 XDEL，且判定不得扰动 xack→xdel 序列。

缺陷（已取证）：`XDEL` 是**全局删除**、不区分消费组。同一 Stream 上若还有别的消费组尚未处理
某条目，删除就等于**毁掉那一组的数据**；而旧实现的两处 XDEL 完全没有拓扑判断。

修法（第 4 次尝试，前三次均回退）：
- 拓扑判定 `_may_delete_terminal()` 只在**调用层**做一次，结果以 `allow_delete` 参数传给
  `_delete_ids()`；
- **`_delete_ids()` 内的 redis 交互必须只有 `xdel`** —— 它是紧跟 `xack` 之后、在同一个 `try` 内
  被调用的，任何就地插入的 redis 交互都会扰动既有用例精确锁定的 xack→xdel 序列（曾致 7~8 个
  既有用例失败）；
- 重试路径多组时**直接跳过**，且**不改动** `remaining`/游标核算（同样为避免扰动额度行为）；
- 语义刻意收窄：**确实读到多个组**才拒绝；查询失败/形态不可读 ⇒ 继续删除并计数，避免任何瞬时
  查询故障让终态清理全面停摆。
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

from consumer import StreamConsumer  # noqa: E402
from performance import WriterPerformanceMetrics  # noqa: E402

STREAM = "rt:stream:qc:g001:0"


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


def _consumer(redis_client: MagicMock) -> StreamConsumer:
    return StreamConsumer(
        _config(),
        MagicMock(),
        MagicMock(),
        redis_client,
        metrics=WriterPerformanceMetrics(),
    )


def _client(groups) -> MagicMock:
    client = MagicMock()
    client.xinfo_groups.return_value = groups
    return client


# ── 设计约束：_delete_ids 内不得有 redis 交互 ────────────────────────────────


def test_delete_ids_issues_no_redis_call_when_refused() -> None:
    """`allow_delete=False` 时 `_delete_ids` 必须**零 redis 调用**（判定在调用层，不在这里）。"""
    client = _client([])
    consumer = _consumer(client)

    consumer._delete_ids(STREAM, ["1-0"], allow_delete=False)

    assert client.xdel.call_count == 0
    assert client.xinfo_groups.call_count == 0, "拓扑判定不得发生在 _delete_ids 内"
    assert consumer._delete_retry == {STREAM: {"1-0"}}


def test_discriminating_power_default_would_delete() -> None:
    """判别力校验：不传 `allow_delete`（旧行为）时确实会执行 XDEL，证明守卫并非空转。"""
    client = _client([])
    consumer = _consumer(client)

    consumer._delete_ids(STREAM, ["1-0"])

    client.xdel.assert_called_once_with(STREAM, "1-0")


# ── 拓扑判定本身 ────────────────────────────────────────────────────────────


def test_multi_group_refuses_terminal_xdel() -> None:
    client = _client([{"name": "writer"}, {"name": "other"}])
    consumer = _consumer(client)

    allow = consumer._may_delete_terminal(STREAM)
    consumer._delete_ids(STREAM, ["1-0"], allow_delete=allow)

    assert allow is False
    assert client.xdel.call_count == 0, "多消费组下不得全局 XDEL"
    assert consumer._delete_retry == {STREAM: {"1-0"}}, "条目必须保留，不得丢失"


def test_single_group_allows_terminal_xdel() -> None:
    client = _client([{"name": "writer"}])
    consumer = _consumer(client)

    assert consumer._may_delete_terminal(STREAM) is True
    consumer._delete_ids(STREAM, ["1-0"], allow_delete=True)

    client.xdel.assert_called_once_with(STREAM, "1-0")


def test_unreadable_topology_fails_open() -> None:
    """形态不可读（例如替身返回非序列）⇒ 继续删除，但留下可见计数。"""
    client = _client("nonsense")
    consumer = _consumer(client)

    assert consumer._may_delete_terminal(STREAM) is True
    assert consumer._metrics._counters.get("retry_terminal_delete_topology_unknown_rows") == 1


def test_query_error_fails_open() -> None:
    client = MagicMock()
    client.xinfo_groups.side_effect = Exception("ERR unknown command")
    consumer = _consumer(client)

    assert consumer._may_delete_terminal(STREAM) is True
    assert consumer._metrics._counters.get("retry_terminal_delete_topology_unknown_rows") == 1


def test_multi_group_records_visible_metric() -> None:
    client = _client([{}, {}])
    consumer = _consumer(client)

    consumer._may_delete_terminal(STREAM)

    assert consumer._metrics._counters.get("retry_terminal_delete_multi_group_skipped_rows") == 1


def test_topology_query_is_cached() -> None:
    """拓扑极少变化：同一 Stream 在 TTL 内只查一次。"""
    client = _client([{"name": "writer"}])
    consumer = _consumer(client)

    consumer._may_delete_terminal(STREAM)
    consumer._may_delete_terminal(STREAM)

    assert client.xinfo_groups.call_count == 1


# ── 重试路径 ────────────────────────────────────────────────────────────────


def test_retry_pending_deletes_skips_multi_group_and_keeps_ids() -> None:
    client = _client([{}, {}])
    consumer = _consumer(client)
    consumer._delete_retry = {STREAM: {"1-0"}}

    consumer._retry_pending_deletes()

    assert client.xdel.call_count == 0
    assert consumer._delete_retry == {STREAM: {"1-0"}}


def test_retry_pending_deletes_still_deletes_single_group() -> None:
    client = _client([{}])
    consumer = _consumer(client)
    consumer._delete_retry = {STREAM: {"1-0"}}

    consumer._retry_pending_deletes()

    client.xdel.assert_called_once_with(STREAM, "1-0")
    assert consumer._delete_retry == {}

