"""一次性驱动会话：创建、连接、读一块、断开、丢弃。"""

from __future__ import annotations

import asyncio
import contextlib
import time
from typing import Any, Callable

from direct_read.driver_loader import import_read_block
from direct_read.errors import DirectReadError
from direct_read.quality import (
    QUALITY_BAD,
    QUALITY_CONFIG_DECODE_ERROR,
    QUALITY_GOOD,
    QUALITY_NOT_CONNECTED,
    is_valid_quality,
)


_CONFIG_HINTS = ("data type", "address", "bool", "dbx", "decode", "config")


async def read_once(
    factory: Callable[[], Any],
    connection: dict[str, Any],
    *,
    address: str,
    data_type: str,
    deadline: float,
) -> tuple[Any, int, int]:
    """返回 (值, 质量码, 读耗时毫秒)。``deadline`` 是 ``time.monotonic`` 截止点，含断开。"""
    driver = factory()
    started = time.perf_counter()
    try:
        block_type = import_read_block()
        block = block_type(address, 1, data_type)
        try:
            left = deadline - time.monotonic()
            if left <= 0:
                raise TimeoutError
            await asyncio.wait_for(driver.connect(connection), left)
            left = deadline - time.monotonic()
            if left <= 0:
                raise TimeoutError
            raw = await asyncio.wait_for(driver.read([block]), left)
        except TimeoutError as exc:
            raise DirectReadError(
                "timeout",
                "直连读取超时",
                quality=QUALITY_NOT_CONNECTED,
            ) from exc
        except DirectReadError:
            raise
        except Exception as exc:
            raise _map_driver_error(exc) from exc
        value, quality = interpret_sample(raw, address)
        elapsed_ms = int((time.perf_counter() - started) * 1000)
        return value, quality, elapsed_ms
    finally:
        await _safe_disconnect(driver, deadline)


def interpret_sample(raw: Any, address: str) -> tuple[Any, int]:
    if not isinstance(raw, dict) or address not in raw:
        raise DirectReadError(
            "plc_read_error",
            "驱动没有返回该地址的样本",
            quality=QUALITY_BAD,
        )
    value, quality = _unwrap(raw[address])
    if isinstance(value, list):
        if len(value) != 1:
            raise DirectReadError(
                "plc_read_error",
                "单点读取返回了意外的块长度",
                quality=QUALITY_BAD,
            )
        value, nested_quality = _unwrap(value[0])
        if nested_quality is not None:
            quality = nested_quality
    if value is None:
        raise DirectReadError(
            "plc_read_error",
            "PLC 未返回有效值",
            quality=QUALITY_BAD,
        )
    if quality is None:
        quality = QUALITY_GOOD
    if not is_valid_quality(quality):
        raise DirectReadError(
            "plc_read_error",
            "驱动返回了无法识别的质量码",
            quality=QUALITY_BAD,
        )
    return _json_safe(value), int(quality)


def _unwrap(value: Any) -> tuple[Any, int | None]:
    if isinstance(value, (str, bytes, bytearray, dict, list, int, float, bool)):
        return value, None
    sample_value = getattr(value, "value", None)
    sample_quality = getattr(value, "quality", None)
    if sample_quality is None:
        return value, None
    try:
        quality = int(sample_quality)
    except (TypeError, ValueError):
        return value, None
    return sample_value, quality


def _json_safe(value: Any) -> Any:
    if isinstance(value, bool) or value is None:
        return value
    if isinstance(value, (str, int, float)):
        return value
    if isinstance(value, (bytes, bytearray)):
        return value.hex()
    return str(value)


def _map_driver_error(exc: Exception) -> DirectReadError:
    text = f"{type(exc).__name__} {exc}".lower()
    if "timeout" in text:
        return DirectReadError(
            "timeout",
            "直连读取超时",
            quality=QUALITY_NOT_CONNECTED,
        )
    if any(hint in text for hint in _CONFIG_HINTS):
        return DirectReadError(
            "config_error",
            "点位地址或类型与 S7 驱动不匹配",
            quality=QUALITY_CONFIG_DECODE_ERROR,
        )
    if type(exc).__name__ in {"DeviceConnectionError", "ConnectionError", "OSError"}:
        return DirectReadError(
            "network_error",
            "无法连接到 PLC",
            quality=QUALITY_NOT_CONNECTED,
        )
    return DirectReadError(
        "plc_read_error",
        "PLC 读取失败",
        quality=QUALITY_BAD,
    )


async def _safe_disconnect(driver: Any, deadline: float) -> None:
    """断开只消耗截止时间里剩下的部分，不再另加预算。"""
    disconnect = getattr(driver, "disconnect", None)
    if not callable(disconnect):
        return
    try:
        result = disconnect()
    except Exception:
        return
    if not asyncio.iscoroutine(result):
        return
    task = asyncio.ensure_future(result)
    try:
        # 让已经就绪的断开协程跑完一步，瞬时假驱动能记下 disconnect。
        await asyncio.sleep(0)
        if task.done():
            return
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            task.cancel()
            return
        await asyncio.wait_for(task, remaining)
    except Exception:
        if not task.done():
            task.cancel()
    finally:
        if not task.done():
            task.cancel()
        with contextlib.suppress(asyncio.CancelledError, Exception):
            await task
