"""统一错误处理器"""

import asyncio
import inspect
import traceback
from datetime import datetime
from enum import Enum
from typing import Any, Callable, Dict, List, Optional
from functools import wraps
from dataclasses import dataclass

from core.logger import get_logger

logger = get_logger("error_handler")


class ErrorSeverity(Enum):
    """错误严重程度"""
    LOW = "low"        # 轻微错误，可自动恢复
    MEDIUM = "medium"  # 中等错误，需要关注
    HIGH = "high"      # 严重错误，影响功能
    CRITICAL = "critical"  # 致命错误，系统停机


class ErrorType(Enum):
    """错误类型"""
    CONNECTION = "connection"      # 连接错误
    CONFIGURATION = "config"      # 配置错误
    DATA = "data"                # 数据错误
    STORAGE = "storage"          # 存储错误
    VALIDATION = "validation"     # 验证错误
    UNKNOWN = "unknown"          # 未知错误


@dataclass
class ErrorContext:
    """错误上下文"""
    error_type: ErrorType
    severity: ErrorSeverity
    message: str
    exception: Optional[Exception] = None
    timestamp: Optional[datetime] = None
    component: str = ""
    details: Optional[Dict[str, Any]] = None

    def __post_init__(self):
        if self.timestamp is None:
            self.timestamp = datetime.now()
        if self.details is None:
            self.details = {}


class GlobalErrorHandler:
    """全局错误处理器"""

    def __init__(self):
        self._error_handlers: Dict[ErrorType, Callable] = {}
        self._error_callbacks: List[Callable] = []
        self._error_stats: Dict[str, int] = {}
        self._max_errors = 1000  # 最大错误记录数

    def register_handler(self, error_type: ErrorType, handler: Callable):
        """注册错误处理器"""
        self._error_handlers[error_type] = handler
        logger.info(f"Registered error handler for {error_type.value}")

    def register_callback(self, callback: Callable):
        """注册错误回调"""
        self._error_callbacks.append(callback)

    async def handle_error(self, context: ErrorContext) -> bool:
        """处理错误"""
        try:
            # 记录错误统计
            error_key = f"{context.component}.{context.error_type.value}"
            self._error_stats[error_key] = self._error_stats.get(error_key, 0) + 1

            # 记录错误日志
            self._log_error(context)

            # 调用特定错误处理器
            if context.error_type in self._error_handlers:
                try:
                    result = self._error_handlers[context.error_type](context)
                    if inspect.isawaitable(result):
                        await result
                except Exception as e:
                    logger.error(f"Error handler failed: {e}")

            # 调用所有错误回调
            for callback in self._error_callbacks:
                try:
                    if inspect.iscoroutinefunction(callback):
                        await callback(context)
                    else:
                        callback(context)
                except Exception as e:
                    logger.error(f"Error callback failed: {e}")

            # 清理旧的错误记录
            self._cleanup_stats()

            return True

        except Exception as e:
            logger.error(f"Global error handler failed: {e}")
            return False

    def _log_error(self, context: ErrorContext):
        """记录错误日志"""
        if context.severity == ErrorSeverity.CRITICAL:
            logger.critical(f"CRITICAL ERROR in {context.component}: {context.message}")
        elif context.severity == ErrorSeverity.HIGH:
            logger.error(f"HIGH ERROR in {context.component}: {context.message}")
        elif context.severity == ErrorSeverity.MEDIUM:
            logger.warning(f"MEDIUM ERROR in {context.component}: {context.message}")
        else:
            logger.info(f"LOW ERROR in {context.component}: {context.message}")

        if context.exception:
            logger.debug(f"Exception details:\n{traceback.format_exc()}")

    def _cleanup_stats(self):
        """清理错误统计"""
        if len(self._error_stats) > self._max_errors:
            # 保留最近出现的错误
            sorted_errors = sorted(self._error_stats.items(), key=lambda x: x[1], reverse=True)
            self._error_stats = dict(sorted_errors[:self._max_errors])

    def get_error_stats(self) -> Dict[str, int]:
        """获取错误统计"""
        return self._error_stats.copy()

    def clear_stats(self):
        """清空错误统计"""
        self._error_stats.clear()


# 全局错误处理器实例
_error_handler = GlobalErrorHandler()


def _submit_error_context(context: ErrorContext) -> None:
    """在同步代码中安全提交错误处理协程。"""
    try:
        loop = asyncio.get_running_loop()
    except RuntimeError:
        asyncio.run(_error_handler.handle_error(context))
    else:
        loop.create_task(_error_handler.handle_error(context))


def handle_exception(error_type: ErrorType, severity: ErrorSeverity = ErrorSeverity.MEDIUM):
    """异常处理装饰器"""
    def decorator(func):
        @wraps(func)
        async def async_wrapper(*args, **kwargs):
            try:
                return await func(*args, **kwargs)
            except Exception as e:
                context = ErrorContext(
                    error_type=error_type,
                    severity=severity,
                    message=str(e),
                    exception=e,
                    component=func.__name__,
                    details={
                        "function": func.__name__,
                        "args": str(args),
                        "kwargs": str(kwargs)
                    }
                )
                await _error_handler.handle_error(context)
                raise

        @wraps(func)
        def sync_wrapper(*args, **kwargs):
            try:
                return func(*args, **kwargs)
            except Exception as e:
                context = ErrorContext(
                    error_type=error_type,
                    severity=severity,
                    message=str(e),
                    exception=e,
                    component=func.__name__,
                    details={
                        "function": func.__name__,
                        "args": str(args),
                        "kwargs": str(kwargs)
                    }
                )
                _submit_error_context(context)
                raise

        return async_wrapper if inspect.iscoroutinefunction(func) else sync_wrapper
    return decorator


def log_error(error_type: ErrorType, severity: ErrorSeverity = ErrorSeverity.MEDIUM, component: str = ""):
    """手动记录错误"""
    def decorator(func):
        @wraps(func)
        def wrapper(*args, **kwargs):
            try:
                return func(*args, **kwargs)
            except Exception as e:
                context = ErrorContext(
                    error_type=error_type,
                    severity=severity,
                    message=str(e),
                    exception=e,
                    component=component or func.__name__,
                    details={
                        "function": func.__name__,
                        "args": str(args),
                        "kwargs": str(kwargs)
                    }
                )
                _submit_error_context(context)
                raise
        return wrapper
    return decorator


def get_error_handler() -> GlobalErrorHandler:
    """获取全局错误处理器"""
    return _error_handler


# 注册默认错误处理器
async def default_connection_error_handler(context: ErrorContext):
    """默认连接错误处理器"""
    logger.info(f"Handling connection error for {context.component}")
    # 可以在这里添加自动重连逻辑


async def default_storage_error_handler(context: ErrorContext):
    """默认存储错误处理器"""
    logger.info(f"Handling storage error for {context.component}")
    # 可以在这里添加重试逻辑


# 注册默认处理器
_error_handler.register_handler(ErrorType.CONNECTION, default_connection_error_handler)
_error_handler.register_handler(ErrorType.STORAGE, default_storage_error_handler)
