"""驱动基类，定义统一接口"""

import asyncio
from abc import ABC, abstractmethod
from typing import Any, Dict, List, Optional


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


class BaseDriver(ABC):
    """设备驱动基类，所有协议驱动需实现此接口"""

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
            {address: value} 字典
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
