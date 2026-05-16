"""缓存系统"""

import asyncio
import inspect
import time
from dataclasses import dataclass, field
from functools import wraps
from typing import Any, Dict, Optional, Tuple, Callable
from enum import Enum

from core.logger import get_logger

logger = get_logger("cache")


class CachePolicy(Enum):
    """缓存策略"""
    TTL = "ttl"          # 生存时间
    LRU = "lru"         # 最近最少使用
    LFU = "lfu"         # 最不经常使用
    NONE = "none"       # 无缓存


@dataclass
class CacheEntry:
    """缓存条目"""
    value: Any
    created_at: float
    last_accessed: float
    access_count: int = 0
    expire_at: Optional[float] = None


class CacheManager:
    """缓存管理器"""

    def __init__(
        self,
        max_size: int = 1000,
        default_ttl: int = 300,  # 5分钟
        cleanup_interval: int = 60  # 1分钟
    ):
        self.max_size = max_size
        self.default_ttl = default_ttl
        self.cleanup_interval = cleanup_interval
        self._cache: Dict[str, CacheEntry] = {}
        self._stats = {
            "hits": 0,
            "misses": 0,
            "evictions": 0,
            "total_size": 0
        }
        self._lock = asyncio.Lock()
        self._cleanup_task: Optional[asyncio.Task] = None

    async def start(self):
        """启动清理任务"""
        if self._cleanup_task and not self._cleanup_task.done():
            return

        self._cleanup_task = asyncio.create_task(self._cleanup_loop())
        logger.info("Cache cleanup started")

    async def stop(self):
        """停止清理任务"""
        if self._cleanup_task and not self._cleanup_task.done():
            self._cleanup_task.cancel()
            try:
                await self._cleanup_task
            except asyncio.CancelledError:
                pass
        logger.info("Cache cleanup stopped")

    async def get(self, key: str) -> Optional[Any]:
        """获取缓存值"""
        async with self._lock:
            entry = self._cache.get(key)
            if not entry:
                self._stats["misses"] += 1
                return None

            # 检查是否过期
            if entry.expire_at and time.time() > entry.expire_at:
                del self._cache[key]
                self._stats["misses"] += 1
                return None

            # 更新访问信息
            entry.last_accessed = time.time()
            entry.access_count += 1
            self._stats["hits"] += 1

            return entry.value

    def get_sync(self, key: str) -> Optional[Any]:
        """获取缓存值（同步版本，用于 sync_wrapper）"""
        entry = self._cache.get(key)
        if not entry:
            self._stats["misses"] += 1
            return None

        # 检查是否过期
        if entry.expire_at and time.time() > entry.expire_at:
            del self._cache[key]
            self._stats["misses"] += 1
            return None

        # 更新访问信息
        entry.last_accessed = time.time()
        entry.access_count += 1
        self._stats["hits"] += 1

        return entry.value

    async def set(
        self,
        key: str,
        value: Any,
        ttl: Optional[int] = None,
        policy: CachePolicy = CachePolicy.TTL
    ) -> bool:
        """设置缓存值"""
        async with self._lock:
            # 检查大小限制
            if len(self._cache) >= self.max_size:
                await self._evict_entry(policy)

            # ttl=None 使用默认 TTL；ttl<=0 表示不过期。
            expire_at = None
            effective_ttl = self.default_ttl if ttl is None else ttl
            if effective_ttl > 0:
                expire_at = time.time() + effective_ttl

            # 创建条目
            entry = CacheEntry(
                value=value,
                created_at=time.time(),
                last_accessed=time.time(),
                expire_at=expire_at
            )

            self._cache[key] = entry
            self._stats["total_size"] = len(self._cache)

            return True

    def set_sync(
        self,
        key: str,
        value: Any,
        ttl: Optional[int] = None,
        policy: CachePolicy = CachePolicy.TTL
    ) -> bool:
        """设置缓存值（同步版本，用于 sync_wrapper）

        注意：同步版本不执行驱逐逻辑，可能超出 max_size。
        """
        # ttl=None 使用默认 TTL；ttl<=0 表示不过期。
        expire_at = None
        effective_ttl = self.default_ttl if ttl is None else ttl
        if effective_ttl > 0:
            expire_at = time.time() + effective_ttl

        # 创建条目
        entry = CacheEntry(
            value=value,
            created_at=time.time(),
            last_accessed=time.time(),
            expire_at=expire_at
        )

        self._cache[key] = entry
        self._stats["total_size"] = len(self._cache)

        return True

    async def delete(self, key: str) -> bool:
        """删除缓存值"""
        async with self._lock:
            if key in self._cache:
                del self._cache[key]
                self._stats["total_size"] = len(self._cache)
                return True
            return False

    async def clear(self):
        """清空缓存"""
        async with self._lock:
            self._cache.clear()
            self._stats["total_size"] = 0

    async def _evict_entry(self, policy: CachePolicy):
        """根据策略删除条目"""
        if not self._cache:
            return

        if policy == CachePolicy.LRU:
            # 删除最近最少使用的
            key = min(self._cache.keys(), key=lambda k: self._cache[k].last_accessed)
        elif policy == CachePolicy.LFU:
            # 删除最不经常使用的
            key = min(self._cache.keys(), key=lambda k: self._cache[k].access_count)
        else:
            # 默认删除第一个
            key = next(iter(self._cache))

        del self._cache[key]
        self._stats["evictions"] += 1
        self._stats["total_size"] = len(self._cache)

    async def _cleanup_loop(self):
        """清理过期条目"""
        while True:
            try:
                await asyncio.sleep(self.cleanup_interval)

                now = time.time()
                expired_keys = []

                async with self._lock:
                    for key, entry in self._cache.items():
                        if entry.expire_at and now > entry.expire_at:
                            expired_keys.append(key)

                    for key in expired_keys:
                        del self._cache[key]

                    if expired_keys:
                        logger.info(f"Cleaned up {len(expired_keys)} expired cache entries")
                        self._stats["total_size"] = len(self._cache)

            except asyncio.CancelledError:
                break
            except Exception as e:
                logger.error(f"Cache cleanup error: {e}")

    def get_stats(self) -> Dict[str, Any]:
        """获取缓存统计"""
        total = self._stats["hits"] + self._stats["misses"]
        hit_rate = (self._stats["hits"] / total * 100) if total > 0 else 0

        return {
            **self._stats,
            "hit_rate": f"{hit_rate:.2f}%",
            "current_size": len(self._cache),
            "max_size": self.max_size
        }

    def get_size(self) -> int:
        """获取当前缓存大小"""
        return len(self._cache)


# 装饰器：缓存函数结果
def cache_result(
    cache_manager: CacheManager,
    key_prefix: str = "",
    ttl: Optional[int] = None,
    policy: CachePolicy = CachePolicy.TTL
):
    """缓存函数结果的装饰器"""
    def decorator(func):
        @wraps(func)
        async def async_wrapper(*args, **kwargs):
            # 生成缓存键
            key = f"{key_prefix}_{func.__name__}_{hash(str(args) + str(kwargs))}"

            # 尝试从缓存获取
            cached = await cache_manager.get(key)
            if cached is not None:
                return cached

            # 执行函数并缓存结果
            result = await func(*args, **kwargs)
            await cache_manager.set(key, result, ttl, policy)
            return result

        @wraps(func)
        def sync_wrapper(*args, **kwargs):
            # 生成缓存键
            key = f"{key_prefix}_{func.__name__}_{hash(str(args) + str(kwargs))}"

            # 尝试从缓存获取（使用同步版本）
            cached = cache_manager.get_sync(key)
            if cached is not None:
                return cached

            # 执行函数并缓存结果（使用同步版本）
            result = func(*args, **kwargs)
            cache_manager.set_sync(key, result, ttl, policy)
            return result

        return async_wrapper if inspect.iscoroutinefunction(func) else sync_wrapper
    return decorator


# 全局缓存管理器
_cache_manager = CacheManager()


def get_cache_manager() -> CacheManager:
    """获取全局缓存管理器"""
    return _cache_manager


async def init_cache(max_size: int = 1000, default_ttl: int = 300):
    """初始化全局缓存"""
    global _cache_manager
    _cache_manager = CacheManager(max_size, default_ttl)
    await _cache_manager.start()
    return _cache_manager


async def cleanup_cache():
    """清理全局缓存"""
    if _cache_manager:
        await _cache_manager.stop()
        await _cache_manager.clear()
