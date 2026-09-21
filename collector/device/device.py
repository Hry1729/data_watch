"""设备类定义"""

import inspect
from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Callable, Dict, List, Optional

from core.logger import get_logger
from core.exceptions import DeviceConnectionError

logger = get_logger("device")


class DeviceStatus(Enum):
    OFFLINE = "offline"
    CONNECTING = "connecting"
    ONLINE = "online"
    ERROR = "error"
    CONFIG_ERROR = "config_error"


@dataclass
class Variable:
    """变量定义"""
    variable_id: str
    address: str  # e.g., "DB398.DBD0", "%MD1304", "%R100"
    data_type: str = "INT"
    scan_rate: float = 1.0  # 采集周期（秒）
    is_storage: bool = True  # 是否存储到TDengine
    group_code: str = "g001"  # 所属点位分组编码
    group_config_version: int = 1  # 所属点位组配置版本
    last_value: Optional[Any] = None
    last_storage_time: Optional[float] = None  # 上次入库时间戳


@dataclass
class ScanTask:
    """采集任务（同周期的变量合并）"""
    scan_rate: float  # 周期（秒）
    variables: List[Variable] = field(default_factory=list)
    last_read_time: float = 0.0
    # 批量读取块：连续地址会被合并
    read_blocks: List[Any] = field(default_factory=list)  # ReadBlock
    result_map: Dict[str, Dict[str, Any]] = field(default_factory=dict)
    # 外置驱动解码钩子（由 build_read_plan 成功后挂载，避免核心按协议名硬编码）
    decode_read_results: Optional[Callable[..., Any]] = None


class Device:
    """Device state and scheduling; protocol planning belongs to installed packages."""

    def __init__(
        self,
        device_id: str,
        device_type: str,
        protocol: str,
        connection_config: Dict[str, Any],
        device_pk: Optional[int] = None,
        config_version: int = 1,
    ):
        self.device_id = device_id
        self.device_type = device_type
        self.protocol = protocol
        self.connection_config = connection_config
        self.device_pk = device_pk  # MySQL device.id
        self.config_version = config_version
        self.generation: int = 0
        self.active: bool = False
        self.status = DeviceStatus.OFFLINE
        self.variables: List[Variable] = []
        self.scan_tasks: List[ScanTask] = []
        self.driver: Optional[Any] = None  # BaseDriver 实例
        self.last_error_time: Optional[float] = None
        self.error_count: int = 0
        self.max_errors: int = 5  # 最大错误次数
        self.config_error: Optional[str] = None


    def add_variable(self, variable: Variable) -> None:
        """添加变量到设备"""
        self.variables.append(variable)


    def build_scan_tasks(self) -> None:
        """
        按scan_rate分组构建采集任务
        同周期同设备的变量合并到一个任务
        """
        rate_map: Dict[float, List[Variable]] = {}
        for var in self.variables:
            rate_map.setdefault(var.scan_rate, []).append(var)

        scan_tasks: List[ScanTask] = []
        for rate, variables in sorted(rate_map.items()):
            task = ScanTask(scan_rate=rate, variables=variables)
            self._build_read_blocks(task)
            scan_tasks.append(task)
            logger.info(
                f"Device {self.device_id}: scan task rate={rate}s, "
                f"blocks={len(task.read_blocks)}, vars={len(variables)}"
            )
        self.scan_tasks = scan_tasks


    def __repr__(self):
        return f"Device({self.device_id}, {self.device_type}, {self.status.value})"


    def _build_read_blocks(self, task: ScanTask) -> None:
        from drivers.base import ReadBlock
        if not task.variables:
            return
        hooks = getattr(self, "external_driver_hooks", None)
        if hooks is None:
            raise DeviceConnectionError(f"驱动未安装或未加载: {self.protocol}")
        planner = getattr(hooks, "build_read_plan", None)
        if planner is not None:
            options = {"unit_id": self.connection_config.get("unit_id", 1)}
            if "connection_config" in inspect.signature(planner).parameters:
                options["connection_config"] = {**self.connection_config, "device_id": self.device_id}
            task.read_blocks, task.result_map = planner(task.variables, **options)
            task.decode_read_results = getattr(hooks, "decode_read_results", None)
        else:
            task.read_blocks = [ReadBlock(var.address, count=1, data_type=var.data_type) for var in task.variables]
