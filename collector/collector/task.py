"""采集任务定义"""

from dataclasses import dataclass, field
from types import MappingProxyType
from typing import Any, Mapping, Tuple, Union

from core.logger import get_logger
from device.device import Device, ScanTask, Variable

logger = get_logger("task")


@dataclass(frozen=True)
class VariableSnapshot:
    variable_id: str
    address: str
    data_type: str
    scan_rate: float
    is_storage: bool
    group_code: str
    group_config_version: int

    @classmethod
    def from_variable(cls, variable: Variable) -> "VariableSnapshot":
        return cls(
            variable_id=variable.variable_id,
            address=variable.address,
            data_type=variable.data_type,
            scan_rate=variable.scan_rate,
            is_storage=variable.is_storage,
            group_code=variable.group_code,
            group_config_version=variable.group_config_version,
        )


@dataclass(frozen=True)
class ReadBlockSnapshot:
    address: str
    count: int
    data_type: str


@dataclass(frozen=True)
class ScanTaskSnapshot:
    scan_rate: float
    variables: Tuple[VariableSnapshot, ...]
    read_blocks: Tuple[ReadBlockSnapshot, ...]
    result_map: Mapping[str, Mapping[str, Any]]

    @classmethod
    def from_scan_task(cls, scan_task: ScanTask) -> "ScanTaskSnapshot":
        result_map = MappingProxyType(
            {
                variable_id: MappingProxyType(dict(mapping))
                for variable_id, mapping in scan_task.result_map.items()
            }
        )
        return cls(
            scan_rate=scan_task.scan_rate,
            variables=tuple(
                VariableSnapshot.from_variable(variable)
                for variable in scan_task.variables
            ),
            read_blocks=tuple(
                ReadBlockSnapshot(
                    address=block.address,
                    count=block.count,
                    data_type=block.data_type,
                )
                for block in scan_task.read_blocks
            ),
            result_map=result_map,
        )


@dataclass
class CollectTask:
    """
    采集任务
    一个任务对应一个设备的一个采集周期
    """

    device: Device
    scan_task: Union[ScanTask, ScanTaskSnapshot]
    task_name: str = ""
    last_run: float = 0.0
    error_count: int = 0
    generation: int = field(init=False)

    def __post_init__(self):
        if isinstance(self.scan_task, ScanTask):
            self.scan_task = ScanTaskSnapshot.from_scan_task(self.scan_task)
        self.generation = self.device.generation
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
