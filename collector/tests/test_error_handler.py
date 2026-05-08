"""错误处理器测试"""

import pytest
import asyncio
from unittest.mock import AsyncMock, MagicMock, patch

from core.error_handler import (
    GlobalErrorHandler,
    ErrorContext,
    ErrorType,
    ErrorSeverity,
    handle_exception,
    log_error,
    get_error_handler
)


class TestGlobalErrorHandler:
    """全局错误处理器测试"""

    def setup_method(self):
        """测试前设置"""
        self.error_handler = GlobalErrorHandler()
        self.context = ErrorContext(
            error_type=ErrorType.CONNECTION,
            severity=ErrorSeverity.HIGH,
            message="Test error message",
            component="test_component"
        )

    def test_error_handling(self):
        """测试基本错误处理"""
        # 设置回调
        callback = MagicMock()
        self.error_handler.register_callback(callback)

        # 处理错误
        asyncio.run(self.error_handler.handle_error(self.context))

        # 验证回调被调用
        callback.assert_called_once_with(self.context)

    def test_error_handler_registration(self):
        """测试错误处理器注册"""
        handler = MagicMock()
        self.error_handler.register_handler(ErrorType.CONNECTION, handler)

        # 处理错误
        asyncio.run(self.error_handler.handle_error(self.context))

        # 验证处理器被调用
        handler.assert_called_once_with(self.context)

    def test_error_stats(self):
        """测试错误统计"""
        # 处理几个错误
        for i in range(3):
            context = ErrorContext(
                error_type=ErrorType.CONNECTION,
                severity=ErrorSeverity.HIGH,
                message=f"Error {i}",
                component="test_component"
            )
            asyncio.run(self.error_handler.handle_error(context))

        stats = self.error_handler.get_error_stats()
        assert stats["test_component.connection"] == 3

    def test_stats_cleanup(self):
        """测试统计清理"""
        # 添加超过最大数量的错误
        for i in range(1500):
            context = ErrorContext(
                error_type=ErrorType.CONNECTION,
                severity=ErrorSeverity.HIGH,
                message=f"Error {i}",
                component="test_component"
            )
            asyncio.run(self.error_handler.handle_error(context))

        stats = self.error_handler.get_error_stats()
        assert len(stats) <= self.error_handler._max_errors


class TestErrorDecorators:
    """错误处理装饰器测试"""

    def test_handle_exception_decorator_async(self):
        """测试异步函数的错误处理装饰器"""
        @handle_exception(ErrorType.CONNECTION, ErrorSeverity.HIGH)
        async def async_function():
            raise Exception("Test exception")

        # 验证装饰器正常工作
        with pytest.raises(Exception, match="Test exception"):
            asyncio.run(async_function())

    def test_handle_exception_decorator_sync(self):
        """测试同步函数的错误处理装饰器"""
        @handle_exception(ErrorType.CONNECTION, ErrorSeverity.HIGH)
        def sync_function():
            raise Exception("Test exception")

        # 验证装饰器正常工作
        with pytest.raises(Exception, match="Test exception"):
            sync_function()

    def test_log_error_decorator(self):
        """测试错误日志装饰器"""
        error_handler = get_error_handler()
        error_handler.clear_stats()

        @log_error(ErrorType.CONNECTION, ErrorSeverity.HIGH, "test_component")
        def test_function():
            raise Exception("Test exception")

        # 验证装饰器正常工作
        with pytest.raises(Exception, match="Test exception"):
            test_function()

        # 验证错误被记录
        stats = error_handler.get_error_stats()
        assert "test_component.connection" in stats

    def test_get_error_handler(self):
        """测试获取全局错误处理器"""
        handler = get_error_handler()
        assert isinstance(handler, GlobalErrorHandler)


class TestErrorContext:
    """错误上下文测试"""

    def test_error_context_creation(self):
        """测试错误上下文创建"""
        context = ErrorContext(
            error_type=ErrorType.CONNECTION,
            severity=ErrorSeverity.HIGH,
            message="Test message"
        )

        assert context.error_type == ErrorType.CONNECTION
        assert context.severity == ErrorSeverity.HIGH
        assert context.message == "Test message"
        assert context.component == ""
        assert context.details == {}
        assert context.timestamp is not None
        assert context.exception is None

    def test_error_context_with_exception(self):
        """测试带有异常的错误上下文"""
        exception = Exception("Test exception")
        context = ErrorContext(
            error_type=ErrorType.CONNECTION,
            severity=ErrorSeverity.HIGH,
            message="Test message",
            exception=exception
        )

        assert context.exception == exception

    def test_error_context_with_details(self):
        """测试带有详细信息的错误上下文"""
        details = {"key": "value", "number": 123}
        context = ErrorContext(
            error_type=ErrorType.CONNECTION,
            severity=ErrorSeverity.HIGH,
            message="Test message",
            details=details
        )

        assert context.details == details
