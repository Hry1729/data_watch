"""GE SRTP协议驱动实现"""

import asyncio
import os
import re
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

    # GE 地址正则：匹配 %(R|AI|AQ|I|Q|M)<num>
    _GE_ADDRESS_RE = re.compile(r"^%(R|AI|AQ|I|Q|M)(\d+)$", re.IGNORECASE)

    def __init__(self, read_executor=None):
        super().__init__()
        # 专用 PLC 读线程池（§7.3）；None 时回退默认 executor，保持原行为与测试兼容。
        self._read_executor = read_executor
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
            await loop.run_in_executor(self._read_executor, self._driver.connect)
            self._connected = True
            logger.info(f"GE SRTP connected to {host}:{port} slot={slot}")
        except Exception as e:
            raise DeviceConnectionError(f"GE SRTP connect failed: {e}") from e

    async def disconnect(self) -> None:
        """断开GE PLC连接"""
        if self._driver:
            try:
                loop = asyncio.get_event_loop()
                await loop.run_in_executor(self._read_executor, self._driver.disconnect)
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
                        self._read_executor, self._read_block, block
                    )
                    result[block.address] = values
                except Exception as e:
                    logger.error(f"GE SRTP read block {block.address} failed: {e}")
                    result[block.address] = None

        return result

    def _read_block(self, block: ReadBlock) -> List[Any]:
        """
        在线程中执行单个 block 读取。

        ReadBlock.address 视作 PLC 逻辑地址字符串，如 "%R00001"、"%AI0001"。
        driver 内部根据地址格式解析区域类型和逻辑编号，转换为 GE 库需要的 protocol offset。

        地址基准（需现场已知点校验后固化）：
            %R/%AI/%AQ: protocol_offset = logical_number - 1
            %I/%Q/%M: 保持库示例语义，暂不减 1
        """
        addr = block.address.upper()
        count = block.count

        # 匹配 GE 地址格式：%(R|AI|AQ|I|Q|M)<num>
        match = self._GE_ADDRESS_RE.match(addr)
        if not match:
            raise ReadError(f"Unsupported GE address format: {addr}")

        area = match.group(1).upper()
        logical_number = int(match.group(2))

        # 地址基准转换
        if area in ("R", "AI", "AQ"):
            protocol_offset = logical_number - 1
        else:
            protocol_offset = logical_number

        # 根据区域类型调用对应的读取方法
        if area == "R":
            values = self._driver.read_register(protocol_offset, count)
        elif area == "AI":
            values = self._driver.read_analog_input(protocol_offset, count)
        elif area == "AQ":
            values = self._driver.read_analog_output(protocol_offset, count)
        elif area == "I":
            # 离散输入：bit 模式
            values = self._driver.read_discrete_input(protocol_offset, count, mode='bit')
        elif area == "Q":
            # 离散输出：bit 模式
            values = self._driver.read_discrete_output(protocol_offset, count, mode='bit')
        elif area == "M":
            # 内部离散量：bit 模式
            values = self._driver.read_discrete_internal(protocol_offset, count, mode='bit')
        else:
            raise ReadError(f"Unsupported GE area: {area}")

        # 确保返回 list
        return values if isinstance(values, list) else [values]

    async def write(self, points: List[WritePoint]) -> None:
        """GE SRTP驱动只读，不支持写入"""
        raise ReadError("GE SRTP driver is read-only")
