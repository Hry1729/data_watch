"""一次性驱动会话：创建、连接、读一块、断开、丢弃。"""

from __future__ import annotations

import asyncio
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
_DISCONNECT_BUDGET_S = 2.0


async def read_once(
    factory: Callable[[], Any],
    connection: dict[str, Any],
    *,
    address: str,
    data_type: str,
    timeout_s: float,
) -> tuple[Any, int, int]:
    """返回 (值, 质量码, 读耗时毫秒)。无论成败都断开连接。"""
    driver = factory()
    started = time.perf_counter()
    try:
        block_type = import_read_block()
        block = block_type(address, 1, data_type)
        loop = asyncio.get_running_loop()
        deadline = loop.time() + timeout_s
        try:
            await asyncio.wait_for(
                driver.connect(connection),
                max(0.001, deadline - loop.time()),
            )
            left = deadline - loop.time()
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
        await _safe_disconnect(driver)


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


async def _safe_disconnect(driver: Any) -> None:
    disconnect = getattr(driver, "disconnect", None)
    if not callable(disconnect):
        return
    try:
        result = disconnect()
        if asyncio.iscoroutine(result):
            await asyncio.wait_for(result, _DISCONNECT_BUDGET_S)
    except Exception:
        return
