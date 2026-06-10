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


class Device:
    """设备实例，维护一个PLC连接和变量列表"""

    _DB_ADDRESS_RE = re.compile(
        r"^%?DB(?P<db>\d+)\.(?P<access>DBX|DBB|DBW|DBD)(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
        re.IGNORECASE,
    )
    _S7_AREA_RE = re.compile(
        r"^%?(?P<area>I|Q|M)(?P<width>B|W|D)?(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
        re.IGNORECASE,
    )
    _S7_TIMER_COUNTER_RE = re.compile(
        r"^%?(?P<type>T|C)(?P<num>\d+)$",
        re.IGNORECASE,
    )
    _GE_ADDRESS_RE = re.compile(
        r"^%(R|AI|AQ|I|Q|M)(\d+)$",
        re.IGNORECASE,
    )
    # GE 合并配置默认值
    GE_MAX_BLOCK_WORDS = 125
    GE_MAX_BLOCK_BITS = 512

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
        rate_map: Dict[float, List[Variable]] = {}
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

        if self.protocol.lower() in {"s7", "snap7", "siemens"}:
            self._build_s7_read_blocks(task)
            return

        if self.protocol.lower() == "ge_srtp":
            self._build_ge_read_blocks(task)
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

    def _build_s7_read_blocks(self, task: ScanTask) -> None:
        """构建 S7 读取块；只合并 I/Q/M 连续字节，DB 地址保持原样。"""
        from drivers.base import ReadBlock

        blocks = []
        task.result_map = {}
        merge_groups: Dict[str, List[tuple[int, int, Variable, Dict[str, Any]]]] = {}
        timer_counter_groups: Dict[tuple[str, str], List[tuple[int, int, Variable, Dict[str, Any]]]] = {}

        for var in task.variables:
            parsed = self._parse_s7_address(var.address)
            if parsed.get("area") in {"I", "Q", "M"}:
                start = int(parsed["offset"])
                size = self._s7_variable_byte_size(parsed, var.data_type)
                merge_groups.setdefault(parsed["area"], []).append((start, start + size, var, parsed))
            elif parsed.get("area") in {"T", "C"}:
                area = str(parsed["area"]).upper()
                data_type = (var.data_type or "").upper()
                start = int(parsed["num"])
                timer_counter_groups.setdefault((area, data_type), []).append((start, start + 1, var, parsed))
            else:
                blocks.append(ReadBlock(var.address, count=1, data_type=var.data_type))

        for area, ranges in sorted(merge_groups.items()):
            ranges.sort(key=lambda item: item[0])
            current: List[tuple[int, int, Variable, Dict[str, Any]]] = []
            current_start: Optional[int] = None
            current_end: Optional[int] = None

            for item in ranges:
                start, end, _, _ = item
                if current and current_end is not None and start > current_end:
                    block = self._create_s7_area_byte_block(area, current_start, current_end, current, task.result_map)
                    blocks.append(block)
                    current = []
                    current_start = None
                    current_end = None

                if not current:
                    current_start = start
                    current_end = end
                else:
                    current_end = max(current_end or end, end)
                current.append(item)

            if current:
                block = self._create_s7_area_byte_block(area, current_start, current_end, current, task.result_map)
                blocks.append(block)

        for (area, data_type), ranges in sorted(timer_counter_groups.items()):
            ranges.sort(key=lambda item: item[0])
            current: List[tuple[int, int, Variable, Dict[str, Any]]] = []
            current_start: Optional[int] = None
            current_end: Optional[int] = None

            for item in ranges:
                start, end, _, _ = item
                if current and current_end is not None and start > current_end:
                    block = self._create_s7_timer_counter_block(
                        area, data_type, current_start, current_end, current, task.result_map
                    )
                    blocks.append(block)
                    current = []
                    current_start = None
                    current_end = None

                if not current:
                    current_start = start
                    current_end = end
                else:
                    current_end = max(current_end or end, end)
                current.append(item)

            if current:
                block = self._create_s7_timer_counter_block(
                    area, data_type, current_start, current_end, current, task.result_map
                )
                blocks.append(block)

        task.read_blocks = blocks
        logger.debug(
            f"Device {self.device_id}: prepared {len(blocks)} S7 read blocks from {len(task.variables)} variables"
        )

    def _create_s7_area_byte_block(
        self,
        area: str,
        start: Optional[int],
        end: Optional[int],
        items: List[tuple[int, int, Variable, Dict[str, Any]]],
        result_map: Dict[str, Dict[str, Any]],
    ):
        """创建合并后的 I/Q/M 字节读取块，并记录变量还原映射。"""
        from drivers.base import ReadBlock

        if start is None or end is None:
            raise DeviceConnectionError("Invalid S7 area merge range")
        block_address = f"%{area}B{start}"
        block = ReadBlock(block_address, count=end - start, data_type="BYTE")
        for item_start, _, var, parsed in items:
            result_map[var.variable_id] = {
                "block_address": block_address,
                "byte_offset": item_start - start,
                "bit": parsed.get("bit"),
                "data_type": var.data_type,
            }
        return block

    def _create_s7_timer_counter_block(
        self,
        area: str,
        data_type: str,
        start: Optional[int],
        end: Optional[int],
        items: List[tuple[int, int, Variable, Dict[str, Any]]],
        result_map: Dict[str, Dict[str, Any]],
    ):
        """创建合并后的 S7 Timer/Counter 读取块，并记录单点还原偏移。"""
        from drivers.base import ReadBlock

        if start is None or end is None:
            raise DeviceConnectionError("Invalid S7 Timer/Counter merge range")

        block_address = f"%{area}{start}"
        block = ReadBlock(block_address, count=end - start, data_type=data_type)
        for item_start, _, var, _ in items:
            result_map[var.variable_id] = {
                "block_address": block_address,
                "area": area,
                "value_offset": item_start - start,
                "data_type": var.data_type,
            }
        return block

    @staticmethod
    def _s7_variable_byte_size(parsed: Dict[str, Any], data_type: str) -> int:
        """计算 S7 I/Q/M 变量读取所需字节数。"""
        if parsed.get("bit") is not None:
            return 1
        width = parsed.get("width")
        if width == "B":
            return 1
        if width == "W":
            return 2
        if width == "D":
            return 4
        sizes = {"BOOL": 1, "BYTE": 1, "INT": 2, "WORD": 2, "REAL": 4, "FLOAT": 4, "DINT": 4, "DWORD": 4}
        return sizes.get((data_type or "").upper(), 1)

    @staticmethod
    def _ge_word_size(data_type: str) -> int:
        """计算 GE 变量的 word 宽度。"""
        sizes = {
            "INT": 1, "WORD": 1, "UINT": 1, "USINT": 1,
            "DINT": 2, "DWORD": 2, "UDINT": 2,
            "REAL": 2, "FLOAT": 2,
        }
        return sizes.get((data_type or "").upper(), 1)

    def _build_ge_read_blocks(self, task: ScanTask) -> None:
        """
        构建 GE SRTP 读取块，按区域合并连续地址。

        合并规则：
        - %R/%AI/%AQ：按 word 范围合并，最大 125 words
        - %I/%Q/%M：按 bit 范围合并（第一版只支持 BOOL），最大 512 bits
        - 默认不跨空洞（max_gap=0）
        """
        from drivers.base import ReadBlock

        blocks = []
        task.result_map = {}

        # 分组：按区域类型
        register_groups: Dict[str, List[tuple]] = {"R": [], "AI": [], "AQ": []}
        discrete_groups: Dict[str, List[tuple]] = {"I": [], "Q": [], "M": []}

        for var in task.variables:
            parsed = self._parse_ge_address(var.address)
            area = parsed["area"]
            word_len = self._ge_word_size(var.data_type)

            if area in register_groups:
                # 寄存器区域：记录 (offset, end_offset, var, parsed, word_len)
                start = parsed["protocol_offset"]
                end = start + word_len
                register_groups[area].append((start, end, var, parsed, word_len))
            elif area in discrete_groups:
                # 离散量区域：第一版只支持 BOOL
                if var.data_type.upper() != "BOOL":
                    raise DeviceConnectionError(
                        f"GE SRTP discrete area {area} only supports BOOL in this version, got {var.data_type}"
                    )
                start = parsed["protocol_offset"]
                discrete_groups[area].append((start, start + 1, var, parsed, 1))
            else:
                raise DeviceConnectionError(f"Unsupported GE SRTP area: {area}")

        # 合并寄存器区域
        for area, items in register_groups.items():
            if not items:
                continue
            items.sort(key=lambda x: x[0])
            self._merge_ge_register_blocks(area, items, task.result_map, blocks)

        # 合并离散量区域
        for area, items in discrete_groups.items():
            if not items:
                continue
            items.sort(key=lambda x: x[0])
            self._merge_ge_discrete_blocks(area, items, task.result_map, blocks)

        task.read_blocks = blocks
        logger.debug(
            f"Device {self.device_id}: prepared {len(blocks)} GE read blocks from {len(task.variables)} variables"
        )

    def _merge_ge_register_blocks(
        self,
        area: str,
        items: List[tuple],
        result_map: Dict[str, Dict[str, Any]],
        blocks: List,
    ) -> None:
        """合并 GE 寄存器区域（%R/%AI/%AQ）的连续地址。"""
        from drivers.base import ReadBlock

        current_start: Optional[int] = None
        current_end: Optional[int] = None
        current_items: List[tuple] = []

        for start, end, var, parsed, word_len in items:
            if current_start is None:
                current_start = start
                current_end = end
                current_items.append((start, end, var, parsed, word_len))
            elif start <= current_end and end - current_start <= self.GE_MAX_BLOCK_WORDS:
                # 可合并：连续/重叠且不超限
                current_end = max(current_end, end)
                current_items.append((start, end, var, parsed, word_len))
            else:
                # 不可合并：创建块
                self._create_ge_register_block(area, current_start, current_end, current_items, result_map, blocks)
                current_start = start
                current_end = end
                current_items = [(start, end, var, parsed, word_len)]

        if current_items:
            self._create_ge_register_block(area, current_start, current_end, current_items, result_map, blocks)

    def _create_ge_register_block(
        self,
        area: str,
        start: Optional[int],
        end: Optional[int],
        items: List[tuple],
        result_map: Dict[str, Dict[str, Any]],
        blocks: List,
    ) -> None:
        """创建合并后的 GE 寄存器读取块。"""
        from drivers.base import ReadBlock

        if start is None or end is None:
            return

        # block_address 使用 PLC 逻辑地址字符串（用户输入形式 +1 转回）
        logical_number = start + 1
        block_address = f"%{area}{logical_number:05d}" if area == "R" else f"%{area}{logical_number:04d}"
        word_count = end - start

        block = ReadBlock(block_address, count=word_count, data_type="WORD")
        blocks.append(block)

        # 填充 result_map
        for item_start, item_end, var, parsed, word_len in items:
            word_offset = item_start - start
            result_map[var.variable_id] = {
                "protocol": "ge_srtp",
                "block_address": block_address,
                "area": area,
                "offset": word_offset,
                "data_type": var.data_type,
                "word_offset": word_offset,
                "word_length": word_len,
                "word_order": "low_high",  # 当前只支持 low_high，现场必须校验
            }

    def _merge_ge_discrete_blocks(
        self,
        area: str,
        items: List[tuple],
        result_map: Dict[str, Dict[str, Any]],
        blocks: List,
    ) -> None:
        """合并 GE 离散量区域（%I/%Q/%M）的连续地址。"""
        from drivers.base import ReadBlock

        current_start: Optional[int] = None
        current_end: Optional[int] = None
        current_items: List[tuple] = []

        for start, end, var, parsed, bit_len in items:
            if current_start is None:
                current_start = start
                current_end = end
                current_items.append((start, end, var, parsed, bit_len))
            elif start <= current_end and end - current_start <= self.GE_MAX_BLOCK_BITS:
                # 可合并：连续/重叠且不超限
                current_end = max(current_end, end)
                current_items.append((start, end, var, parsed, bit_len))
            else:
                # 不可合并：创建块
                self._create_ge_discrete_block(area, current_start, current_end, current_items, result_map, blocks)
                current_start = start
                current_end = end
                current_items = [(start, end, var, parsed, bit_len)]

        if current_items:
            self._create_ge_discrete_block(area, current_start, current_end, current_items, result_map, blocks)

    def _create_ge_discrete_block(
        self,
        area: str,
        start: Optional[int],
        end: Optional[int],
        items: List[tuple],
        result_map: Dict[str, Dict[str, Any]],
        blocks: List,
    ) -> None:
        """创建合并后的 GE 离散量读取块（bit 模式）。"""
        from drivers.base import ReadBlock

        if start is None or end is None:
            return

        # block_address 使用 PLC 逻辑地址字符串
        block_address = f"%{area}{start:05d}"
        bit_count = end - start

        block = ReadBlock(block_address, count=bit_count, data_type="BIT")
        blocks.append(block)

        # 填充 result_map
        for item_start, item_end, var, parsed, bit_len in items:
            bit_offset = item_start - start
            result_map[var.variable_id] = {
                "protocol": "ge_srtp",
                "block_address": block_address,
                "area": area,
                "offset": bit_offset,
                "data_type": "BOOL",
                "bit_offset": bit_offset,
            }

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
        if addr.startswith("DB") or addr.startswith("%DB") or self._S7_AREA_RE.match(addr):
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

        # 先检查 Timer/Counter
        tc_match = self._S7_TIMER_COUNTER_RE.match(addr)
        if tc_match:
            return {
                "area": tc_match.group("type"),
                "num": int(tc_match.group("num")),
                "width": None,
                "offset": 0,
                "bit": None,
            }

        if addr.startswith("DB") or addr.startswith("%DB"):
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
                "%Q<byte>.<bit>, %M<byte>.<bit>, %MB<byte>, %MW<byte>, %MD<byte>, "
                "%T<num> (Timer) or %C<num> (Counter)."
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
        """
        解析 GE SRTP 地址，返回地址语义信息。

        返回：
            area: 区域类型 (R/AI/AQ/I/Q/M)
            logical_number: 用户输入的 PLC 逻辑地址编号
            protocol_offset: GE 库需要的 0-based 偏移

        地址基准（需现场已知点校验后固化）：
            %R/%AI/%AQ: protocol_offset = logical_number - 1
            %I/%Q/%M: 保持当前库示例语义，暂不减 1
        """
        addr = address.upper()
        match = self._GE_ADDRESS_RE.match(addr)
        if not match:
            raise DeviceConnectionError(
                f"Unsupported GE SRTP address format: {address}. "
                "Expected %R<num>, %AI<num>, %AQ<num>, %I<num>, %Q<num> or %M<num>."
            )

        area = match.group(1).upper()
        logical_number = int(match.group(2))

        # GE PLC 逻辑地址必须 >= 1
        if logical_number < 1:
            raise DeviceConnectionError(
                f"Invalid GE SRTP address: {address}. Logical address number must be >= 1."
            )

        # 地址基准转换（需现场校验）
        if area in ("R", "AI", "AQ"):
            # 寄存器区域：logical_number - 1
            protocol_offset = logical_number - 1
        else:
            # 离散量区域：保持当前库示例语义
            protocol_offset = logical_number

        return {
            "area": area,
            "logical_number": logical_number,
            "protocol_offset": protocol_offset,
        }

    def __repr__(self):
        return f"Device({self.device_id}, {self.device_type}, {self.status.value})"
