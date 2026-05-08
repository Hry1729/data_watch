"""连接池管理"""

import asyncio
import time
from abc import ABC, abstractmethod
from typing import Any, Dict, Optional, Type
from dataclasses import dataclass

from core.logger import get_logger
from core.exceptions import DeviceConnectionError

logger = get_logger("connection_pool")


@dataclass
class PoolConfig:
    """连接池配置"""
    max_size: int = 10
    min_size: int = 1
    idle_timeout: float = 300.0
    max_lifetime: float = 3600.0
    health_check_interval: float = 60.0
    acquire_timeout: float = 30.0


class Connection(ABC):
    """连接基类"""

    @abstractmethod
    async def close(self):
        """关闭连接"""
        pass

    @abstractmethod
    async def is_valid(self) -> bool:
        """检查连接是否有效"""
        pass


class BaseConnectionPool(ABC):
    """连接池基类"""

    def __init__(self, config: PoolConfig):
        self.config = config
        self._connections: Dict[str, Connection] = {}
        self._in_use: Dict[str, Connection] = {}
        self._available: asyncio.Queue = asyncio.Queue(maxsize=config.max_size)
        self._lock = asyncio.Lock()
        self._stats = {
            "total_created": 0,
            "total_closed": 0,
            "active": 0,
            "idle": 0,
            "failed": 0
        }

    @abstractmethod
    async def _create_connection(self) -> Connection:
        """创建新连接"""
        pass

    @abstractmethod
    async def _close_connection(self, connection: Connection):
        """关闭连接"""
        pass

    async def get_connection(self) -> Connection:
        """获取连接"""
        start_time = time.time()

        # 尝试从队列获取
        try:
            connection = await asyncio.wait_for(
                self._available.get(),
                timeout=self.config.acquire_timeout
            )
            logger.debug(f"Got connection from pool, acquired in {time.time() - start_time:.2f}s")
            return connection
        except asyncio.TimeoutError:
            # 队列为空，检查是否可以创建新连接
            async with self._lock:
                if len(self._in_use) < self.config.max_size:
                    connection = await self._create_connection()
                    self._stats["total_created"] += 1
                    self._stats["active"] += 1
                    logger.debug(f"Created new connection, total: {self._stats['total_created']}")
                    return connection
                else:
                    # 连接池已满
                    raise DeviceConnectionError("Connection pool exhausted")

    async def return_connection(self, connection: Connection):
        """归还连接"""
        async with self._lock:
            if connection in self._in_use:
                self._in_use.pop(connection)
                self._stats["active"] -= 1

                # 检查连接是否仍然有效
                if await connection.is_valid():
                    try:
                        await self._available.put(connection)
                        self._stats["idle"] += 1
                        logger.debug("Returned connection to pool")
                    except asyncio.QueueFull:
                        # 队列已满，关闭连接
                        await self._close_connection(connection)
                        self._stats["total_closed"] += 1
                        self._stats["failed"] += 1
                        logger.warning("Pool full, closing connection")
                else:
                    # 连接无效，关闭
                    await self._close_connection(connection)
                    self._stats["total_closed"] += 1
                    self._stats["failed"] += 1
                    logger.warning("Invalid connection, closing")

    async def close_all(self):
        """关闭所有连接"""
        async with self._lock:
            # 关闭空闲连接
            while not self._available.empty():
                connection = self._available.get_nowait()
                await self._close_connection(connection)
                self._stats["total_closed"] += 1

            # 关闭正在使用的连接
            for connection in list(self._in_use.values()):
                await self._close_connection(connection)
                self._stats["total_closed"] += 1

            self._stats["active"] = 0
            self._stats["idle"] = 0
            logger.info(f"Closed all connections. Total created: {self._stats['total_closed']}")

    @property
    def stats(self) -> Dict[str, Any]:
        """获取连接池统计"""
        return {
            **self._stats,
            "max_size": self.config.max_size,
            "min_size": self.config.min_size,
            "available": self._available.qsize(),
        }


class PlcConnectionPool(BaseConnectionPool):
    """PLC连接池示例实现"""

    def __init__(self, config: PoolConfig, driver_class: Type, **driver_kwargs):
        super().__init__(config)
        self.driver_class = driver_class
        self.driver_kwargs = driver_kwargs

    async def _create_connection(self) -> Connection:
        """创建PLC连接"""
        try:
            connection = self.driver_class(**self.driver_kwargs)
            await connection.connect()
            return connection
        except Exception as e:
            logger.error(f"Failed to create PLC connection: {e}")
            raise DeviceConnectionError(f"Failed to create PLC connection: {e}")

    async def _close_connection(self, connection: Connection):
        """关闭PLC连接"""
        try:
            await connection.close()
        except Exception as e:
            logger.warning(f"Error closing PLC connection: {e}")


class TdengineConnectionPool(BaseConnectionPool):
    """TDengine连接池"""

    def __init__(self, config: PoolConfig, tdengine_config):
        super().__init__(config)
        self.tdengine_config = tdengine_config

    async def _create_connection(self) -> Connection:
        """创建TDengine连接"""
        try:
            # 这里应该使用实际的TDengine连接创建逻辑
            # 简化实现
            class MockTdengineConnection:
                def __init__(self, config):
                    self.config = config
                    self.connected = True

                async def connect(self):
                    await asyncio.sleep(0.01)  # 模拟连接延迟

                async def close(self):
                    self.connected = False

                async def is_valid(self) -> bool:
                    return self.connected

                async def execute(self, sql):
                    return 1  # 模拟执行结果

            connection = MockTdengineConnection(self.tdengine_config)
            await connection.connect()
            return connection
        except Exception as e:
            logger.error(f"Failed to create TDengine connection: {e}")
            raise DeviceConnectionError(f"Failed to create TDengine connection: {e}")

    async def _close_connection(self, connection: Connection):
        """关闭TDengine连接"""
        try:
            await connection.close()
        except Exception as e:
            logger.warning(f"Error closing TDengine connection: {e}")


class PoolManager:
    """连接池管理器"""

    def __init__(self):
        self._pools: Dict[str, BaseConnectionPool] = {}

    def register_pool(self, name: str, pool: BaseConnectionPool):
        """注册连接池"""
        self._pools[name] = pool
        logger.info(f"Registered connection pool: {name}")

    def get_pool(self, name: str) -> Optional[BaseConnectionPool]:
        """获取连接池"""
        return self._pools.get(name)

    async def close_all(self):
        """关闭所有连接池"""
        for name, pool in self._pools.items():
            try:
                await pool.close_all()
                logger.info(f"Closed connection pool: {name}")
            except Exception as e:
                logger.error(f"Error closing connection pool {name}: {e}")


# 全局连接池管理器
_pool_manager = None


def get_pool_manager() -> PoolManager:
    """获取连接池管理器"""
    global _pool_manager
    if _pool_manager is None:
        _pool_manager = PoolManager()
    return _pool_manager


def init_pool_manager() -> PoolManager:
    """初始化连接池管理器"""
    global _pool_manager
    _pool_manager = PoolManager()
    return _pool_manager