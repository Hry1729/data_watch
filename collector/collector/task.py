"""采集任务定义"""

from dataclasses import dataclass, field
from typing import Any, Dict, List

from core.logger import get_logger
from device.device import Device, ScanTask

logger = get_logger("task")


@dataclass
class CollectTask:
    """
    采集任务
    一个任务对应一个设备的一个采集周期
    """

    device: Device
    scan_task: ScanTask
    task_name: str = ""
    last_run: float = 0.0
    error_count: int = 0

    def __post_init__(self):
        if not self.task_name:
            self.task_name = (
                f"{self.device.device_id}_{self.scan_task.scan_rate}s"
            )

    @property
    def scan_rate(self) -> float:
        return self.scan_task.scan_rate

    def __repr__(self):
        return (
            f"CollectTask({self.task_name}, "
            f"blocks={len(self.scan_task.read_blocks)}, "
            f"vars={len(self.scan_task.variables)})"
        )
