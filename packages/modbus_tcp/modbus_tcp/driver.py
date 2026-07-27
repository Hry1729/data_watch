"""Modbus TCP 驱动实现（pymodbus 3.14.x，只读 FC1/FC3）。"""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from pymodbus.client import AsyncModbusTcpClient

from core.exceptions import DeviceConnectionError, ReadError
from core.logger import get_logger
from drivers.base import BaseDriver, ReadBlock, WritePoint
from drivers.context import DriverContext

from .planner import plan_read_blocks

logger = get_logger("modbus_tcp_driver")

_SAFE_ERROR_FIELDS = (
    "exception_code",
    "function_code",
    "device_id",
    "transaction_id",
)


def _format_error_diagnostics(response: Any) -> str:
    """只提取白名单整数协议字段，避免记录响应对象中的未知内容。"""
    details: List[str] = []
    for field in _SAFE_ERROR_FIELDS:
        try:
            value = getattr(response, field, None)
        except Exception:
            continue
        if isinstance(value, int) and not isinstance(value, bool):
            details.append(f"{field}={value}")
    return " ".join(details)


def _require_response_length(
    values: List[Any], *, fc: int, start: int, expected: int
) -> None:
    actual = len(values)
    if actual < expected:
        raise ReadError(
            f"Modbus FC{fc} 响应长度不足 start={start} "
            f"expected={expected} actual={actual}"
        )


def create_driver(context: DriverContext) -> BaseDriver:
    """外置驱动工厂入口（驱动 API v1：单参数 DriverContext）。"""
    return ModbusTcpDriver(read_executor=context.read_executor)


class ModbusTcpDriver(BaseDriver):
    """Modbus TCP 只读驱动。"""

    def __init__(self, read_executor=None):
        super().__init__()
        self._read_executor = read_executor
        self._client: Optional[AsyncModbusTcpClient] = None
        self._host = "127.0.0.1"
        self._port = 502
        self._unit_id = 1

    async def connect(self, config: Dict[str, Any]) -> None:
        self._host = config.get("ip") or config.get("host") or "127.0.0.1"
        self._port = int(config.get("port", 502))
        raw_unit = config.get("unit_id", config.get("unit", 1))
        self._unit_id = int(raw_unit)
        if not (1 <= self._unit_id <= 247):
            raise DeviceConnectionError(
                f"Modbus unit_id 必须在 1-247，当前为 {self._unit_id}"
            )

        self._client = AsyncModbusTcpClient(host=self._host, port=self._port)
        try:
            connected = await self._client.connect()
            if not connected or not self._client.connected:
                raise DeviceConnectionError(
                    f"Modbus TCP 连接失败: {self._host}:{self._port}"
                )
            self._connected = True
            logger.info(
                "Modbus TCP connected host=%s port=%s unit_id=%s",
                self._host,
                self._port,
                self._unit_id,
            )
        except Exception as exc:
            # 连接失败必须释放客户端，避免残留半开连接
            client = self._client
            self._client = None
            self._connected = False
            if client is not None:
                try:
                    client.close()
                except Exception as close_exc:
                    logger.warning("Modbus TCP connect cleanup close error: %s", close_exc)
            if isinstance(exc, DeviceConnectionError):
                raise
            raise DeviceConnectionError(f"Modbus TCP connect failed: {exc}") from exc

    async def disconnect(self) -> None:
        if self._client is not None:
            try:
                self._client.close()
            except Exception as exc:
                logger.warning("Modbus TCP disconnect error: %s", exc)
        self._client = None
        self._connected = False

    async def read(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        if not self._connected or self._client is None:
            raise ReadError("Modbus TCP 未连接")

        results: Dict[str, Any] = {}
        plan = plan_read_blocks(blocks, unit_id=self._unit_id)

        async with self._read_lock:
            for entry in plan:
                fc = entry["fc"]
                start = entry["start"]
                count = entry["count"]
                if fc == 1:
                    response = await self._client.read_coils(
                        start, count=count, device_id=self._unit_id
                    )
                    if response.isError():
                        details = _format_error_diagnostics(response)
                        suffix = f" {details}" if details else ""
                        raise ReadError(
                            f"Modbus FC1 读取失败 address={start} count={count}{suffix}"
                        )
                    payload = getattr(response, "bits", None)
                    try:
                        bits = list(payload)[:count]
                    except TypeError as exc:
                        raise ReadError(
                            f"Modbus FC1 响应无效：bits 不可迭代 "
                            f"address={start} count={count}"
                        ) from exc
                    _require_response_length(bits, fc=1, start=start, expected=count)
                    offset = 0
                    for item in entry["items"]:
                        # 结果键使用配置原始地址，供核心按 Variable.address 查找（决策 D）
                        item_count = item["count"]
                        values = [bool(bits[offset + idx]) for idx in range(item_count)]
                        results[item["address"]] = (
                            values[0] if item_count == 1 else values
                        )
                        offset += item_count
                elif fc == 3:
                    response = await self._client.read_holding_registers(
                        start, count=count, device_id=self._unit_id
                    )
                    if response.isError():
                        details = _format_error_diagnostics(response)
                        suffix = f" {details}" if details else ""
                        raise ReadError(
                            f"Modbus FC3 读取失败 address={start} count={count}{suffix}"
                        )
                    payload = getattr(response, "registers", None)
                    try:
                        registers = list(payload)[:count]
                    except TypeError as exc:
                        raise ReadError(
                            f"Modbus FC3 响应无效：registers 不可迭代 "
                            f"address={start} count={count}"
                        ) from exc
                    _require_response_length(registers, fc=3, start=start, expected=count)
                    offset = 0
                    for item in entry["items"]:
                        item_count = item["count"]
                        values = [registers[offset + idx] for idx in range(item_count)]
                        results[item["address"]] = (
                            values[0] if item_count == 1 else values
                        )
                        offset += item_count
                else:
                    raise ReadError(f"不支持的功能码 FC{fc}")

        return results

    async def write(self, points: List[WritePoint]) -> None:
        raise ReadError("Modbus TCP 驱动当前为只读")

    async def ping(self) -> bool:
        """仅检查已有 TCP 连接状态，不发起 HR0 探针读取。"""
        if not self._connected or self._client is None:
            return False
        return bool(self._client.connected)
