"""缓存系统测试"""

import pytest
import asyncio
import time
from unittest.mock import patch

from core.cache import (
    CacheManager,
    CacheEntry,
    CachePolicy,
    cache_result,
    get_cache_manager,
    init_cache
)


class TestCacheManager:
    """缓存管理器测试"""

    @pytest.mark.asyncio
    async def test_basic_operations(self):
        """测试基本操作"""
        cache = CacheManager(max_size=10, default_ttl=60)

        # 测试设置和获取
        await cache.set("key1", "value1")
        assert await cache.get("key1") == "value1"

        # 测试不存在的键
        assert await cache.get("nonexistent") is None

    @pytest.mark.asyncio
    async def test_ttl_expiry(self):
        """测试TTL过期"""
        cache = CacheManager(max_size=10, default_ttl=1)  # 1秒过期

        await cache.set("key1", "value1")
        assert await cache.get("key1") == "value1"

        # 等待过期
        await asyncio.sleep(1.1)

        # 验证已过期
        assert await cache.get("key1") is None

    @pytest.mark.asyncio
    async def test_cache_eviction(self):
        """测试缓存淘汰"""
        cache = CacheManager(max_size=3)

        # 填满缓存
        await cache.set("key1", "value1")
        await cache.set("key2", "value2")
        await cache.set("key3", "value3")

        # 添加新键，应该淘汰最老的
        await cache.set("key4", "value4")

        # key1应该被淘汰
        assert await cache.get("key1") is None
        # 其他键应该还存在
        assert await cache.get("key2") == "value2"
        assert await cache.get("key3") == "value3"
        assert await cache.get("key4") == "value4"

    @pytest.mark.asyncio
    async def test_lru_eviction(self):
        """测试LRU淘汰策略"""
        cache = CacheManager(max_size=3)

        # 添加键
        await cache.set("key1", "value1")
        await cache.set("key2", "value2")
        await cache.set("key3", "value3")

        # 访问key1，让它成为最近使用的
        await cache.get("key1")

        # 添加新键，应该淘汰key2（最近最少使用的）
        await cache.set("key4", "value4", policy=CachePolicy.LRU)

        # 验证
        assert await cache.get("key1") == "value1"
        assert await cache.get("key2") is None
        assert await cache.get("key3") == "value3"
        assert await cache.get("key4") == "value4"

    @pytest.mark.asyncio
    async def test_cache_stats(self):
        """测试缓存统计"""
        cache = CacheManager()

        # 添加一些数据
        await cache.set("key1", "value1")
        await cache.get("key1")
        await cache.get("nonexistent")

        stats = cache.get_stats()
        assert stats["hits"] == 1
        assert stats["misses"] == 1
        assert stats["current_size"] == 1

    @pytest.mark.asyncio
    async def test_cleanup_task(self):
        """测试清理任务"""
        cache = CacheManager(default_ttl=1, cleanup_interval=0.5)

        # 启动清理任务
        await cache.start()

        # 添加会过期的数据
        await cache.set("key1", "value1")

        # 等待清理
        await asyncio.sleep(1.5)

        # 验证数据被清理
        assert await cache.get("key1") is None

        # 停止清理任务
        await cache.stop()

    @pytest.mark.asyncio
    async def test_clear_cache(self):
        """测试清空缓存"""
        cache = CacheManager()

        # 添加数据
        await cache.set("key1", "value1")
        await cache.set("key2", "value2")

        # 清空缓存
        await cache.clear()

        # 验证
        assert await cache.get("key1") is None
        assert await cache.get("key2") is None


class TestCacheDecorator:
    """缓存装饰器测试"""

    @pytest.mark.asyncio
    async def test_cache_result_decorator(self):
        """测试缓存结果装饰器"""
        cache = CacheManager(max_size=10)

        call_count = 0

        @cache_result(cache, "test_func", ttl=60)
        async def test_func(x, y):
            nonlocal call_count
            call_count += 1
            return x + y

        # 第一次调用
        result1 = await test_func(1, 2)
        assert result1 == 3
        assert call_count == 1

        # 第二次相同参数调用，应该从缓存获取
        result2 = await test_func(1, 2)
        assert result2 == 3
        assert call_count == 1  # 没有增加

        # 不同参数调用
        result3 = await test_func(2, 3)
        assert result3 == 5
        assert call_count == 2

    @pytest.mark.asyncio
    async def test_sync_cache_decorator(self):
        """测试同步缓存装饰器"""
        cache = CacheManager(max_size=10)

        call_count = 0

        @cache_result(cache, "sync_func", ttl=60)
        def sync_func(x):
            nonlocal call_count
            call_count += 1
            return x * 2

        # 第一次调用
        result1 = sync_func(5)
        assert result1 == 10
        assert call_count == 1

        # 第二次相同参数调用，应该从缓存获取
        result2 = sync_func(5)
        assert result2 == 10
        assert call_count == 1


class TestGlobalCache:
    """全局缓存测试"""

    @pytest.mark.asyncio
    async def test_get_cache_manager(self):
        """测试获取全局缓存管理器"""
        # 获取默认的
        manager1 = get_cache_manager()
        assert isinstance(manager1, CacheManager)

        # 获取两次应该是同一个
        manager2 = get_cache_manager()
        assert manager1 is manager2

    @pytest.mark.asyncio
    async def test_init_cache(self):
        """测试初始化缓存"""
        # 创建新的缓存管理器
        new_cache = await init_cache(max_size=100, default_ttl=120)

        assert new_cache.max_size == 100
        assert new_cache.default_ttl == 120

        # 验证全局管理器被更新
        global_manager = get_cache_manager()
        assert global_manager is new_cache