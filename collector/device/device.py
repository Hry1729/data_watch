"""设备类定义"""

import re
from dataclasses import dataclass, field
from enum import Enum
from typing import Any, Dict, List, Optional

from core.logger import get_logger
from core.exceptions import DeviceConnectionError

logger = get_logger("device")


class DeviceStatus(Enum):
    OFFLINE = "offline"
    CONNECTING = "connecting"
    ONLINE = "online"
    ERROR = "error"


@dataclass
class Variable:
    """变量定义"""
    variable_id: str
    address: str  # e.g., "DB398.DBD0", "%MD1304", "%R100"
    data_type: str = "INT"
    scan_rate: int = 1  # 采集周期（秒）
    is_storage: bool = True  # 是否存储到TDengine
    last_value: Optional[Any] = None
    last_storage_time: Optional[float] = None  # 上次入库时间戳


@dataclass
class ScanTask:
    """采集任务（同周期的变量合并）"""
    scan_rate: int  # 周期（秒）
    variables: List[Variable] = field(default_factory=list)
    last_read_time: float = 0.0
    # 批量读取块：连续地址会被合并
    read_blocks: List[Any] = field(default_factory=list)  # ReadBlock


class Device:
    """设备实例，维护一个PLC连接和变量列表"""

    _DB_ADDRESS_RE = re.compile(
        r"^DB(?P<db>\d+)\.(?P<access>DBX|DBB|DBW|DBD)(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
        re.IGNORECASE,
    )
    _S7_AREA_RE = re.compile(
        r"^%?(?P<area>I|Q|M)(?P<width>B|W|D)?(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
        re.IGNORECASE,
    )
    _GE_ADDRESS_RE = re.compile(r"^%(R|AI|AQ|I|Q|M)\d+$", re.IGNORECASE)

    def __init__(
        self,
        device_id: str,
        device_type: str,
        protocol: str,
        connection_config: Dict[str, Any],
        device_pk: Optional[int] = None,
    ):
        self.device_id = device_id
        self.device_type = device_type
        self.protocol = protocol
        self.connection_config = connection_config
        self.device_pk = device_pk  # MySQL device.id
        self.status = DeviceStatus.OFFLINE
        self.variables: List[Variable] = []
        self.scan_tasks: List[ScanTask] = []
        self.driver: Optional[Any] = None  # BaseDriver 实例
        self.last_error_time: Optional[float] = None
        self.error_count: int = 0
        self.max_errors: int = 5  # 最大错误次数

    def add_variable(self, variable: Variable) -> None:
        """添加变量到设备"""
        self.variables.append(variable)

    def build_scan_tasks(self) -> None:
        """
        按scan_rate分组构建采集任务
        同周期同设备的变量合并到一个任务
        """
        rate_map: Dict[int, List[Variable]] = {}
        for var in self.variables:
            rate_map.setdefault(var.scan_rate, []).append(var)

        self.scan_tasks = []
        for rate, variables in sorted(rate_map.items()):
            task = ScanTask(scan_rate=rate, variables=variables)
            self._build_read_blocks(task)
            self.scan_tasks.append(task)
            logger.debug(
                f"Device {self.device_id}: scan task rate={rate}s, "
                f"blocks={len(task.read_blocks)}, vars={len(variables)}"
            )

    def _build_read_blocks(self, task: ScanTask) -> None:
        """
        将变量按地址连续性合并为ReadBlock
        同设备+连续地址 -> 合并读取
        """
        from drivers.base import ReadBlock

        if not task.variables:
            return

        blocks = []
        for var in task.variables:
            # 先完成地址校验。S7 的 DB 地址需要保留 DBX/DBB/DBW/DBD 语义，
            # 不能再压缩成旧的 DB<num>.<offset> 形式，否则驱动无法正确读取。
            self._parse_address(var.address)
            block = ReadBlock(
                address=var.address,
                count=1,
                data_type=var.data_type,
            )
            blocks.append(block)

        task.read_blocks = blocks
        logger.debug(
            f"Device {self.device_id}: prepared {len(blocks)} read blocks from {len(task.variables)} variables"
        )

    def _parse_address(self, address: str) -> Dict[str, Any]:
        """解析地址字符串"""
        addr = address.upper()

        protocol = self.protocol.lower()

        if protocol in {"s7", "snap7", "siemens"}:
            return self._parse_s7_address(address)
        if protocol == "ge_srtp":
            return self._parse_ge_address(address)
        if protocol in {"opc", "opcua", "opc_ua"}:
            return {
                "area": "OPCUA",
                "raw": address,
            }
        if protocol in {"http_push", "opcda_sidecar"}:
            return {
                "area": "EXTERNAL_PUSH",
                "raw": address,
            }
        if protocol in {"opcda_center", "opcda"}:
            # OPC DA item_id 格式: OPD_Server.AGV801.ViewMain.Tag30041800
            return {
                "area": "OPCDA_ITEM",
                "raw": address,
            }

        # Unknown protocol: validate the common formats without forcing a
        # protocol-specific interpretation.
        if addr.startswith("DB") or self._S7_AREA_RE.match(addr):
            return self._parse_s7_address(address)
        if self._GE_ADDRESS_RE.match(addr):
            return self._parse_ge_address(address)
        return {
            "area": "UNKNOWN",
            "offset": 0,
        }

    def _parse_s7_address(self, address: str) -> Dict[str, Any]:
        """解析 S7 地址，拒绝旧式 DB<num>.<offset>。"""
        addr = address.upper()

        if addr.startswith("DB"):
            match = self._DB_ADDRESS_RE.match(addr)
            if not match:
                raise DeviceConnectionError(
                    f"Unsupported S7 DB address format: {address}. "
                    "Expected DB<num>.DBX<byte>.<bit>, DB<num>.DBB<byte>, "
                    "DB<num>.DBW<byte> or DB<num>.DBD<byte>."
                )

            access = match.group("access").upper()
            byte_offset = int(match.group("byte"))
            bit_text = match.group("bit")
            bit_offset = int(bit_text) if bit_text is not None else None

            if access == "DBX":
                if bit_offset is None or not 0 <= bit_offset <= 7:
                    raise DeviceConnectionError(f"Invalid S7 bit address: {address}")
            elif bit_offset is not None:
                raise DeviceConnectionError(f"Bit suffix is only allowed for DBX address: {address}")

            if access in {"DBW", "DBD"} and byte_offset % 2 != 0:
                raise DeviceConnectionError(f"S7 {access} address must use an even byte offset: {address}")

            return {
                "area": "DB",
                "db": int(match.group("db")),
                "access": access,
                "offset": byte_offset,
                "bit": bit_offset,
            }

        area_match = self._S7_AREA_RE.match(addr)
        if not area_match:
            raise DeviceConnectionError(
                f"Unsupported S7 address format: {address}. "
                "Expected DB<num>.DBX<byte>.<bit>, DB<num>.DBB<byte>, "
                "DB<num>.DBW<byte>, DB<num>.DBD<byte>, %I<byte>.<bit>, "
                "%Q<byte>.<bit>, %M<byte>.<bit>, %MB<byte>, %MW<byte> or %MD<byte>."
            )

        area = area_match.group("area").upper()
        width = (area_match.group("width") or "").upper()
        byte_offset = int(area_match.group("byte"))
        bit_text = area_match.group("bit")
        bit_offset = int(bit_text) if bit_text is not None else None

        if bit_offset is not None:
            if width:
                raise DeviceConnectionError(f"Bit suffix is not allowed for byte/word/dword address: {address}")
            if not 0 <= bit_offset <= 7:
                raise DeviceConnectionError(f"Invalid S7 bit address: {address}")
        elif not width:
            raise DeviceConnectionError(
                f"S7 area address must include bit suffix or B/W/D width: {address}"
            )

        return {
            "area": area,
            "width": width or None,
            "offset": byte_offset,
            "bit": bit_offset,
        }

    def _parse_ge_address(self, address: str) -> Dict[str, Any]:
        """解析 GE SRTP 地址。"""
        addr = address.upper()
        if not self._GE_ADDRESS_RE.match(addr):
            raise DeviceConnectionError(
                f"Unsupported GE SRTP address format: {address}. "
                "Expected %R<num>, %AI<num>, %AQ<num>, %I<num>, %Q<num> or %M<num>."
            )
        if addr.startswith("%AI") or addr.startswith("%AQ"):
            return {
                "area": addr[1:3],
                "offset": int(addr[3:]) - 1,
            }
        return {
            "area": addr[1],
            "offset": int(addr[2:]),
        }

    def __repr__(self):
        return f"Device({self.device_id}, {self.device_type}, {self.status.value})"
