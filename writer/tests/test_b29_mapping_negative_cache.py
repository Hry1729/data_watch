"""B29 回归：持续缺映射时 MySQL 请求必须有界（短期负缓存 + 可快速失效）。

缺陷（已取证）：`MysqlMapper` 在映射缺失时返回 `None` 且**不缓存该未命中** ⇒ 同
`(device_code, group_code)` 的未知 var_id 会**每帧**触发一次 `reload`（consume 侧日志为
"leaving entry pending"，条目一直留在 PEL 等映射补齐），把 MySQL 请求放大成无界。

修复：缺失时写入**短期负缓存**（`NEGATIVE_CACHE_TTL_S`），窗口内直接返回 `None` 不查库；
成功加载即清除；并提供 `invalidate()` 供失效事件快速失效。**不改** "不 ACK、保留 pending"语义。
"""

from __future__ import annotations

import sys
import time
from pathlib import Path

WRITER_ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = WRITER_ROOT.parent
for _candidate in (WRITER_ROOT, REPO_ROOT):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from mysql_mapper import NEGATIVE_CACHE_TTL_S, MysqlMapper  # noqa: E402

_GROUP = ("QC100", "g001")


class FakeCursor:
    def __init__(self, owner: "FakeConnection") -> None:
        self._owner = owner

    def __enter__(self) -> "FakeCursor":
        return self

    def __exit__(self, exc_type, exc, tb) -> bool:
        return False

    def execute(self, query, params) -> None:
        self._owner.executions.append((query, params))

    def fetchall(self):
        return self._owner.rows


class FakeConnection:
    """rows 可随时替换，用来模拟"映射稍后被补齐"。"""

    def __init__(self, rows) -> None:
        self.rows = rows
        self.executions: list = []

    def ping(self, reconnect=True) -> None:
        return None

    def cursor(self, cursor_type):
        return FakeCursor(self)

    def query_count(self) -> int:
        return len(self.executions)


def mapping_row() -> dict:
    return {
        "var_id": 123,
        "td_column_name": "p0001",
        "var_type": "FLOAT",
        "group_code": "g001",
        "device_code": "QC100",
        "device_type_code": "qc",
        "stable_name": "sdqc",
        "child_table": "QC100",
    }


def test_repeated_misses_within_window_query_once() -> None:
    """核心：连续缺映射高频输入下，查询次数有界。"""
    conn = FakeConnection([])
    mapper = MysqlMapper(conn)

    for _ in range(100):
        assert mapper.reload(*_GROUP, 1) is None
        assert mapper.get_or_load(*_GROUP, 1) is None

    assert conn.query_count() == 1, f"窗口内应只查一次库，实际 {conn.query_count()}"


def test_window_expiry_self_heals_without_any_event() -> None:
    """窗口过期后必须重新查库（保证映射补齐后能自愈，不依赖失效事件）。"""
    conn = FakeConnection([])
    mapper = MysqlMapper(conn)
    mapper._negative_cache_ttl_s = 0.02

    assert mapper.reload(*_GROUP, 1) is None
    assert conn.query_count() == 1

    time.sleep(0.05)
    assert mapper.reload(*_GROUP, 1) is None
    assert conn.query_count() == 2, "窗口过期后未重新查库"


def test_newly_added_mapping_is_recovered() -> None:
    conn = FakeConnection([])
    mapper = MysqlMapper(conn)
    mapper._negative_cache_ttl_s = 0.02
    assert mapper.reload(*_GROUP, 1) is None

    conn.rows = [mapping_row()]  # 映射补齐
    time.sleep(0.05)

    assert mapper.reload(*_GROUP, 2) is not None, "补齐后未能恢复"


def test_invalidate_makes_recovery_immediate() -> None:
    """快速失效：失效事件后不必等窗口过期。"""
    conn = FakeConnection([])
    mapper = MysqlMapper(conn)
    assert mapper.reload(*_GROUP, 1) is None

    conn.rows = [mapping_row()]
    mapper.invalidate(*_GROUP)  # 失效事件

    assert mapper.reload(*_GROUP, 2) is not None, "失效后仍未立即恢复"


def test_negative_cache_does_not_leak_a_positive_entry() -> None:
    """负缓存只影响"是否再查库"，绝不能在正缓存里留下幻影条目。"""
    conn = FakeConnection([])
    mapper = MysqlMapper(conn)

    assert mapper.reload(*_GROUP, 1) is None

    assert mapper.get_cached(*_GROUP) is None


def test_missing_device_does_not_block_another_device() -> None:
    """其他设备必须继续推进：A 缺失不应影响 B 的加载。"""
    conn = FakeConnection([])
    mapper = MysqlMapper(conn)
    assert mapper.reload("MISSING", "g001", 1) is None

    conn.rows = [mapping_row()]
    assert mapper.reload("QC100", "g002", 1) is not None

    # A 仍在自己的负窗口内：不被 B 的成功影响
    assert conn.query_count() == 2


def test_default_window_is_short() -> None:
    """窗口必须足够短，避免"新点被陈旧负缓存永久屏蔽"。"""
    assert 0 < NEGATIVE_CACHE_TTL_S <= 30.0


def test_discriminating_power_disabled_window_queries_every_time() -> None:
    """判别力校验：把窗口设为 0（等价旧行为）时，调用多少次就查多少次库。"""
    conn = FakeConnection([])
    mapper = MysqlMapper(conn)
    mapper._negative_cache_ttl_s = 0.0

    for _ in range(50):
        assert mapper.reload(*_GROUP, 1) is None

    assert conn.query_count() == 50
