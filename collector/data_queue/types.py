"""数据队列公共数据结构。"""

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any


@dataclass
class DataPoint:
    """单条数据点"""
    device_id: str
    variable_id: str
    value: Any
    timestamp: datetime = field(default_factory=datetime.now)
    quality: int = 0
