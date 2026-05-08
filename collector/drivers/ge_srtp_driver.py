"""GE SRTP协议驱动实现"""

import asyncio
import os
import sys
from typing import Any, Dict, List

# 从本地目录 drivers/gesrtp-py-main 导入（与官方示例一致：gesrtp-py-main 在 path 上，使用包名 src）
_GESRTP_ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "gesrtp-py-main")
if _GESRTP_ROOT not in sys.path:
    sys.path.insert(0, _GESRTP_ROOT)

from src.driver import GE_SRTP_Driver  # type: ignore[import-untyped]  # 动态 path 后导入本地 gesrtp

from core.exceptions import DeviceConnectionError, ReadError
from core.logger import get_logger
from .base import BaseDriver, ReadBlock, WritePoint

logger = get_logger("ge_srtp_driver")


class GESRTPDriver(BaseDriver):
    """GE PLC驱动，基于gesrtp-py"""

    def __init__(self):
        super().__init__()
        self._driver = None

    async def connect(self, config: Dict[str, Any]) -> None:
        """
        连接到GE PLC
        config: {host, port, slot}
        """
        host = config.get("ip", "127.0.0.1")
        port = config.get("port", 18245)
        slot = config.get("slot", 1)

        self._driver = GE_SRTP_Driver(host=host, port=port, slot=slot)
        try:
            loop = asyncio.get_event_loop()
            await loop.run_in_executor(None, self._driver.connect)
            self._connected = True
            logger.info(f"GE SRTP connected to {host}:{port} slot={slot}")
        except Exception as e:
            raise DeviceConnectionError(f"GE SRTP connect failed: {e}") from e

    async def disconnect(self) -> None:
        """断开GE PLC连接"""
        if self._driver:
            try:
                loop = asyncio.get_event_loop()
                await loop.run_in_executor(None, self._driver.disconnect)
            except Exception as e:
                logger.warning(f"GE SRTP disconnect error: {e}")
            finally:
                self._connected = False
                self._driver = None
                logger.info("GE SRTP disconnected")

    async def read(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """
        批量读取GE PLC数据
        block.address 格式: "%R<num>", "%AI<num>", "%Q<num>" 等
        """
        if not self._connected or not self._driver:
            raise ReadError("GE SRTP driver not connected")

        result = {}
        loop = asyncio.get_event_loop()

        async with self._read_lock:
            for block in blocks:
                try:
                    values = await loop.run_in_executor(
                        None, self._read_block, block
                    )
                    result[block.address] = values
                except Exception as e:
                    logger.error(f"GE SRTP read block {block.address} failed: {e}")
                    result[block.address] = None

        return result

    def _read_block(self, block: ReadBlock) -> List[Any]:
        """在线程中执行单个block读取"""
        addr = block.address.upper()
        count = block.count

        if addr.startswith("%R"):
            address = int(addr[2:])
            values = self._driver.read_register(address, count)
            return values if isinstance(values, list) else [values]
        elif addr.startswith("%AI"):
            address = int(addr[3:]) - 1  # 1-based to 0-based
            values = self._driver.read_analog_input(address, count)
            return values if isinstance(values, list) else [values]
        elif addr.startswith("%AQ"):
            address = int(addr[3:]) - 1
            values = self._driver.read_analog_output(address, count)
            return values if isinstance(values, list) else [values]
        elif addr.startswith("%I"):
            address = int(addr[2:])
            values = self._driver.read_discrete_input(address, count)
            return values if isinstance(values, list) else [values]
        elif addr.startswith("%Q"):
            address = int(addr[2:])
            values = self._driver.read_discrete_output(address, count)
            return values if isinstance(values, list) else [values]
        elif addr.startswith("%M"):
            address = int(addr[2:])
            values = self._driver.read_discrete_internal(address, count)
            return values if isinstance(values, list) else [values]
        else:
            raise ReadError(f"Unsupported GE address format: {addr}")

    async def write(self, points: List[WritePoint]) -> None:
        """GE SRTP驱动只读，不支持写入"""
        raise ReadError("GE SRTP driver is read-only")
