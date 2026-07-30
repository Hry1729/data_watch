"""驱动基类，定义统一接口"""

import asyncio
from abc import ABC, abstractmethod
from dataclasses import dataclass
from datetime import datetime
from typing import Any, Dict, List, Optional

from core.quality_codes import QUALITY_GOOD


class ReadBlock:
    """批量采集块定义：同设备连续地址合并读取"""

    def __init__(self, address: str, count: int = 1, data_type: str = "INT"):
        self.address = address
        self.count = count
        self.data_type = data_type

    def __repr__(self):
        return f"ReadBlock({self.address}, count={self.count}, type={self.data_type})"


class WritePoint:
    """写入点定义"""

    def __init__(self, address: str, value: Any, data_type: str = "INT"):
        self.address = address
        self.value = value
        self.data_type = data_type

    def __repr__(self):
        return f"WritePoint({self.address}, value={self.value})"


@dataclass(frozen=True, slots=True)
class DriverSample:
    """带质量的单点样本（驱动 → Executor 的统一标量契约）。

    仅声明 native_quality 能力的驱动需要返回本类型；未声明的驱动继续返回裸值，
    由 Executor 统一包装为 QUALITY_GOOD。

    Attributes:
        value: 点位值。None 表示本轮无样本，不进入历史。
        quality: 平台质量码，取值见 core.quality_codes（业务码 ∪ 保留码）。
        source_timestamp: 协议返回的源时间戳；None 表示采用采集时刻。
        protocol_quality: 协议原始质量码（如 OPC DA 的 quality bit field），
            作为诊断预留，不参与平台质量判定，当前不写入 Redis Stream。
    """

    value: Any
    quality: int = QUALITY_GOOD
    source_timestamp: Optional[datetime] = None
    protocol_quality: Optional[int] = None


class BaseDriver(ABC):
    """设备驱动基类，所有协议驱动需实现此接口"""

    native_quality = False

    def __init__(self):
        self._connected = False
        self._config: Optional[Dict[str, Any]] = None
        # PLC 客户端库通常不适用于并发操作
        # 同一连接上。驱动程序应为每个读取批次持有此锁。
        self._read_lock = asyncio.Lock()

    @property
    def is_connected(self) -> bool:
        return self._connected

    @abstractmethod
    async def connect(self, config: Dict[str, Any]) -> None:
        """连接到设备"""
        pass

    @abstractmethod
    async def disconnect(self) -> None:
        """断开连接"""
        pass

    @abstractmethod
    async def read(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """
        批量读取数据
        Args:
            blocks: 读取块列表，支持合并连续地址
        Returns:
            {address: value} 字典。value 的合法形式按驱动能力声明分流：

            1. 未声明 native_quality 的驱动：返回裸值（标量或 list），
               由 Executor 统一包装为 QUALITY_GOOD；
            2. 声明 native_quality 的驱动：成功样本必须返回 DriverSample
               （单点）或 list[DriverSample]（连续块），不得返回裸值——
               否则无法区分"故意 Good"与"漏传质量"。

            两种情况下，地址缺失或值为 None 均表示本轮无样本，不进入历史。
        """
        pass

    @abstractmethod
    async def write(self, points: List[WritePoint]) -> None:
        """批量写入数据"""
        pass

    async def ping(self) -> bool:
        """健康检查（可选实现）"""
        return True

    async def reconnect(self, config: Dict[str, Any]) -> None:
        """重连"""
        await self.disconnect()
        await self.connect(config)
