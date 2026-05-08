"""数据缓冲队列"""

import asyncio
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional
from datetime import datetime

from core.logger import get_logger

logger = get_logger("buffer")


@dataclass
class DataPoint:
    """单条数据点"""
    device_id: str
    variable_id: str
    value: Any
    timestamp: datetime = field(default_factory=datetime.now)

    def to_tdengine_row(self) -> tuple:
        """转换为 TDengine 变量子表写入行 (timestamp, value)。"""
        return (self.timestamp, float(self.value))


class DataBuffer:
    """
    异步数据缓冲队列
    采集数据入队，聚合器出队
    """

    def __init__(self, max_size: int = 10000):
        self._queue: asyncio.Queue = asyncio.Queue(maxsize=max_size)
        self._max_size = max_size
        self._total_enqueued = 0
        self._total_dropped = 0

    async def put(self, point: DataPoint) -> bool:
        """
        数据点入队
        Returns:
            True if successful, False if queue is full
        """
        try:
            self._queue.put_nowait(point)
            self._total_enqueued += 1
            return True
        except asyncio.QueueFull:
            self._total_dropped += 1
            logger.warning(
                f"DataBuffer full ({self._max_size}), dropping point "
                f"device={point.device_id} var={point.variable_id}"
            )
            return False

    async def put_batch(self, points: List[DataPoint]) -> int:
        """
        批量入队
        Returns:
            number of successfully enqueued points
        """
        count = 0
        for point in points:
            if await self.put(point):
                count += 1
        return count

    async def get(self) -> DataPoint:
        """获取单个数据点（阻塞）"""
        return await self._queue.get()

    async def get_batch(self, max_count: int) -> List[DataPoint]:
        """
        批量获取数据点
        尽可能多地从队列中取出，最多max_count个
        """
        points = []
        while len(points) < max_count:
            try:
                point = self._queue.get_nowait()
                points.append(point)
            except asyncio.QueueEmpty:
                break
        return points

    def drain(self) -> List[DataPoint]:
        """
        清空队列并返回所有剩余点（同步版本，用于 shutdown）
        """
        points = []
        while True:
            try:
                point = self._queue.get_nowait()
                points.append(point)
            except asyncio.QueueEmpty:
                break
        return points

    def qsize(self) -> int:
        """当前队列大小"""
        return self._queue.qsize()

    def empty(self) -> bool:
        return self._queue.empty()

    @property
    def stats(self) -> Dict[str, int]:
        return {
            "size": self.qsize(),
            "total_enqueued": self._total_enqueued,
            "total_dropped": self._total_dropped,
            "max_size": self._max_size,
        }
