"""重试机制"""

import asyncio
import time
from functools import wraps
from typing import Any, Callable, Optional, Type, Union
from enum import Enum

from core.exceptions import DeviceConnectionError, ReadError, StorageError


class ErrorType(Enum):
    """错误类型"""
    TEMPORARY = "temporary"  # 临时错误，可重试
    PERMANENT = "permanent"  # 永久错误，不应重试
    BACKOFF = "backoff"  # 需要退避的错误


class CircuitBreakerError(Exception):
    """熔断器打开异常"""
    pass


def classify_error(error: Exception) -> ErrorType:
    """分类错误类型"""
    if isinstance(error, (DeviceConnectionError, asyncio.TimeoutError)):
        return ErrorType.TEMPORARY
    elif isinstance(error, StorageError):
        # 数据库错误通常可能是临时的
        if "connection" in str(error).lower() or "timeout" in str(error).lower():
            return ErrorType.TEMPORARY
        return ErrorType.PERMANENT
    elif isinstance(error, ReadError):
        # PLC读取错误可能是临时的
        if "timeout" in str(error).lower() or "connection" in str(error).lower():
            return ErrorType.TEMPORARY
        return ErrorType.PERMANENT
    else:
        # 其他未知错误默认为临时错误
        return ErrorType.TEMPORARY


def with_retry(
    max_retries: int = 3,
    base_delay: float = 1.0,
    max_delay: float = 60.0,
    backoff_factor: float = 2.0,
    error_types: Optional[list] = None,
):
    """重试装饰器"""
    if error_types is None:
        error_types = [ErrorType.TEMPORARY]

    def decorator(func: Callable):
        @wraps(func)
        async def wrapper(*args, **kwargs):
            last_error = None
            for attempt in range(max_retries + 1):
                try:
                    return await func(*args, **kwargs)
                except Exception as e:
                    error_type = classify_error(e)

                    # 如果是永久错误，直接抛出
                    if error_type == ErrorType.PERMANENT:
                        raise

                    # 如果不是指定类型错误，直接抛出
                    if error_type not in error_types:
                        raise

                    last_error = e
                    if attempt < max_retries:
                        delay = min(
                            base_delay * (backoff_factor ** attempt),
                            max_delay
                        )
                        logger = args[0] if args and hasattr(args[0], 'logger') else None
                        if logger:
                            logger.warning(
                                f"Retry {attempt + 1}/{max_retries} after {delay}s. "
                                f"Error: {e}"
                            )
                        await asyncio.sleep(delay)

            # 所有重试都失败
            raise last_error if last_error else Exception("Max retries exceeded")

        return wrapper
    return decorator


class CircuitBreaker:
    """熔断器"""

    def __init__(
        self,
        failure_threshold: int = 5,
        recovery_timeout: float = 60.0,
        expected_exception: Optional[Type[Exception]] = None
    ):
        self.failure_threshold = failure_threshold
        self.recovery_timeout = recovery_timeout
        self.expected_exception = expected_exception or Exception

        self.failure_count = 0
        self.last_failure_time = None
        self.state = "closed"  # closed, open, half_open

    def __call__(self, func: Callable):
        @wraps(func)
        async def wrapper(*args, **kwargs):
            if self.state == "open":
                if self._should_attempt_reset():
                    self.state = "half_open"
                else:
                    raise CircuitBreakerError("Circuit breaker is open")

            try:
                result = await func(*args, **kwargs)
                self._on_success()
                return result
            except self.expected_exception as e:
                self._on_failure()
                raise

        return wrapper

    def _should_attempt_reset(self) -> bool:
        """判断是否应该尝试重置"""
        if self.last_failure_time is None:
            return True
        return time.time() - self.last_failure_time >= self.recovery_timeout

    def _on_success(self):
        """成功回调"""
        self.failure_count = 0
        self.state = "closed"

    def _on_failure(self):
        """失败回调"""
        self.failure_count += 1
        self.last_failure_time = time.time()

        if self.failure_count >= self.failure_threshold:
            self.state = "open"


class ConnectionPool:
    """连接池基类"""

    def __init__(self, max_size: int = 10, idle_timeout: float = 300.0):
        self.max_size = max_size
        self.idle_timeout = idle_timeout
        self._connections: list = []
        self._in_use: set = set()
        self._lock = asyncio.Lock()

    async def get_connection(self) -> Any:
        """获取连接"""
        async with self._lock:
            # 检查是否有空闲连接
            if self._connections:
                conn = self._connections.pop()
                self._in_use.add(conn)
                return conn

            # 没有空闲连接，检查是否可以创建新连接
            if len(self._in_use) < self.max_size:
                conn = await self._create_connection()
                self._in_use.add(conn)
                return conn

            # 连接池已满，等待
            raise Exception("Connection pool is full")

    async def return_connection(self, conn: Any):
        """归还连接"""
        async with self._lock:
            if conn in self._in_use:
                self._in_use.remove(conn)
                # 检查连接是否仍然有效
                if await self._is_connection_valid(conn):
                    self._connections.append(conn)
                else:
                    await self._close_connection(conn)

    async def _create_connection(self) -> Any:
        """创建新连接（子类实现）"""
        raise NotImplementedError

    async def _is_connection_valid(self, conn: Any) -> bool:
        """检查连接是否有效（子类实现）"""
        raise NotImplementedError

    async def _close_connection(self, conn: Any):
        """关闭连接（子类实现）"""
        raise NotImplementedError

    async def close_all(self):
        """关闭所有连接"""
        async with self._lock:
            for conn in self._connections + list(self._in_use):
                try:
                    await self._close_connection(conn)
                except Exception:
                    pass
            self._connections.clear()
            self._in_use.clear()

    @property
    def stats(self):
        """获取连接池统计"""
        return {
            "total": len(self._connections) + len(self._in_use),
            "idle": len(self._connections),
            "in_use": len(self._in_use),
            "max_size": self.max_size,
        }