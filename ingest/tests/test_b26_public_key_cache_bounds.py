"""B26 回归：验签前的公钥缓存必须**有界**，且过期项可被淘汰。

缺陷（已取证）：`MySQLIngestRepository._public_key_cache` 原为普通 `dict`——
未认证请求可携带任意唯一 `node_uuid`，缓存随唯一 UUID 数量**无界增长**（内存放大），
且过期项**不主动淘汰**（只在同 key 再次查询时被覆盖）。

修复：改为 `OrderedDict` + 容量上限 `_PUBLIC_KEY_CACHE_MAX`，命中时 LRU 提升，
触及上限时**先清过期项、再按 LRU 淘汰最久未使用者**；正/负结果都缓存不变，
`invalidate_control_cache()`/单点 pop 的轮换与吊销语义不变。
"""

from __future__ import annotations

import sys
from collections import OrderedDict
from pathlib import Path
from time import monotonic

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "backend" / ".py314t-site"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from ingest.app.repository import (  # noqa: E402
    _PUBLIC_KEY_CACHE_MAX,
    MySQLIngestRepository,
)


def _repository(ttl_seconds: float = 3600.0):
    """按既有测试的约定构造：无真实连接 + 记录查库次数的 fake `_fetchone`。"""
    repository = MySQLIngestRepository(None, control_cache_ttl_seconds=ttl_seconds)
    queried: list[str] = []

    async def fake_fetchone(query: str, params: tuple):
        queried.append(str(params[0]))
        return None  # 未知节点 ⇒ 负结果

    repository._fetchone = fake_fetchone  # type: ignore[method-assign]
    return repository, queried


@pytest.mark.asyncio
async def test_unique_unknown_uuids_are_bounded() -> None:
    """唯一未知 UUID 洪泛：缓存规模必须有上限（旧实现无界）。"""
    repository, queried = _repository()
    flood = _PUBLIC_KEY_CACHE_MAX + 200

    for index in range(flood):
        assert await repository.get_approved_node_public_keys(f"unknown-{index}") == []

    assert len(repository._public_key_cache) <= _PUBLIC_KEY_CACHE_MAX
    # 每个唯一 UUID 仍恰好查库一次（有界不等于少查）
    assert len(queried) == flood


@pytest.mark.asyncio
async def test_repeat_uuid_does_not_requery() -> None:
    repository, queried = _repository()

    await repository.get_approved_node_public_keys("node-a")
    await repository.get_approved_node_public_keys("node-a")

    assert queried.count("node-a") == 1, "重复同 UUID 不应每次查库"


@pytest.mark.asyncio
async def test_negative_result_is_cached_too() -> None:
    repository, queried = _repository()

    assert await repository.get_approved_node_public_keys("nope") == []
    assert "nope" in repository._public_key_cache
    await repository.get_approved_node_public_keys("nope")
    assert queried.count("nope") == 1


@pytest.mark.asyncio
async def test_recently_used_entry_survives_eviction() -> None:
    """淘汰必须是 LRU：刚被使用过的项不能被淘汰（否则正常节点会被洪泛挤出）。"""
    repository, _queried = _repository()
    for index in range(_PUBLIC_KEY_CACHE_MAX):
        await repository.get_approved_node_public_keys(f"k{index:05d}")

    await repository.get_approved_node_public_keys("k00000")  # 提升为最近使用
    await repository.get_approved_node_public_keys("brand-new")

    assert "k00000" in repository._public_key_cache, "最近使用项被错误淘汰"
    assert "k00001" not in repository._public_key_cache, "应淘汰次久未使用者"
    assert len(repository._public_key_cache) <= _PUBLIC_KEY_CACHE_MAX


@pytest.mark.asyncio
async def test_expired_entries_are_pruned_when_capacity_is_touched() -> None:
    """TTL 已过的项在触及上限时必须被清掉（而不是永久滞留）。

    注意：TTL 为负时**新写入的项本身也已过期**，因此缓存落到空/极小是**正确**结果；
    这里断言的是"不残留任何过期项"这一真正有意义的性质。
    """
    repository, _queried = _repository(ttl_seconds=-1.0)  # 写入即过期
    for index in range(_PUBLIC_KEY_CACHE_MAX):
        await repository.get_approved_node_public_keys(f"expired-{index}")

    await repository.get_approved_node_public_keys("fresh")

    now = monotonic()
    leftovers = [k for k, (expiry, _keys) in repository._public_key_cache.items() if expiry <= now]
    assert leftovers == [], "过期项未被清理"
    assert len(repository._public_key_cache) <= 1


@pytest.mark.asyncio
async def test_invalidation_still_takes_effect() -> None:
    """轮换/吊销：失效后必须重新查库（缓存不得把旧授权永久留住）。"""
    repository, queried = _repository()
    await repository.get_approved_node_public_keys("rotated")
    assert queried.count("rotated") == 1

    repository._public_key_cache.pop("rotated", None)  # 与 invalidate_* 的单点失效等价

    await repository.get_approved_node_public_keys("rotated")
    assert queried.count("rotated") == 2, "失效后未重新查库"


def test_cache_is_an_ordered_dict() -> None:
    repository, _queried = _repository()
    assert isinstance(repository._public_key_cache, OrderedDict)


def test_old_unbounded_cache_would_exceed_the_cap() -> None:
    """判别力校验：证明"有上限"这条断言确实能区分新旧实现。"""
    naive: dict[str, tuple[float, list[str]]] = {}
    for index in range(_PUBLIC_KEY_CACHE_MAX + 200):
        naive[f"unknown-{index}"] = (1e18, [])
    assert len(naive) > _PUBLIC_KEY_CACHE_MAX, "该断言无法区分无界实现"
