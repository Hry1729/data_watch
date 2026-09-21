"""Protocol planning and decoding moved unchanged from Collector core."""
from __future__ import annotations
import re
import struct
import math
from types import SimpleNamespace
from typing import Any, Dict, List, Optional
from core.exceptions import DeviceConnectionError
from core.logger import get_logger
from device.device import ScanTask, Variable
from drivers.base import ReadBlock
logger = get_logger("driver_planner")

class Planner:
    def __init__(self, connection_config=None, device_id=""):
        self.connection_config = connection_config or {}
        self.device_id = device_id
    _GE_ADDRESS_RE = re.compile(
        r"^%(R|AI|AQ|I|Q|M|T|SA|SB|SC|S|G)(\d+)$",
        re.IGNORECASE,
    )


    GE_MAX_BLOCK_WORDS = 125


    GE_MAX_BLOCK_BYTES = 250


    GE_MIN_BLOCK_WORDS = 4


    GE_MIN_DISCRETE_BLOCK_BYTES = 8


    def _ge_max_payload_bytes(self) -> int:
        raw = self.connection_config.get(
            "ge_max_payload_bytes", self.GE_MAX_BLOCK_BYTES
        )
        try:
            value = int(raw)
        except (TypeError, ValueError) as exc:
            raise DeviceConnectionError(f"Invalid GE max payload: {raw!r}") from exc
        if not 8 <= value <= 1024:
            raise DeviceConnectionError(f"Invalid GE max payload: {value} bytes")
        return value


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
        - %I/%Q/%M/%T/%S/%SA/%SB/%SC/%G：byte 模式（第一版只支持 BOOL），
          块按字节界对齐，最大长度由设备 payload 配置决定
        - 允许跨空洞合并：空洞不超过 GE_MAX_GAP_WORDS/GE_MAX_GAP_BYTES 且总跨度不超过块上限，
          空洞位置的数据读回即弃（真机 bench：payload 免费，减少往返次数）
        """
        from drivers.base import ReadBlock

        blocks = []
        task.result_map = {}

        # 分组：按区域类型
        register_groups: Dict[str, List[tuple]] = {"R": [], "AI": [], "AQ": []}
        discrete_groups: Dict[str, List[tuple]] = {
            area: [] for area in ("I", "Q", "M", "T", "S", "SA", "SB", "SC", "G")
        }

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
        """合并 GE 寄存器区域（%R/%AI/%AQ）的地址（允许跨空洞）。

        前置条件：items 已由调用方按 start 升序排序。
        """
        from drivers.base import ReadBlock

        current_start: Optional[int] = None
        current_end: Optional[int] = None
        current_items: List[tuple] = []
        max_block_words = self._ge_max_payload_bytes() // 2

        for start, end, var, parsed, word_len in items:
            if current_start is None:
                current_start = start
                current_end = end
                current_items.append((start, end, var, parsed, word_len))
            elif (
                start - current_end <= max_block_words
                and end - current_start <= max_block_words
            ):
                # 跨洞和总跨度均受当前设备 payload 上限约束。
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

        # 小块向低地址扩齐，避免驱动 max(count,4) 垫长越过区域顶
        if end - start < self.GE_MIN_BLOCK_WORDS:
            start = max(0, end - self.GE_MIN_BLOCK_WORDS)

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
        """合并 GE 离散量区域的地址（byte 模式，允许跨空洞）。

        以字节为单位合并：每个 bit 点先换算为所属字节范围（byte_start = bit//8，
        byte_end = ceil(bit_end/8)），块按字节界对齐，gap/span 均以字节计。

        前置条件：items 已由调用方按 start 升序排序。
        """
        from drivers.base import ReadBlock

        current_byte_start: Optional[int] = None
        current_byte_end: Optional[int] = None
        current_items: List[tuple] = []

        max_block_bytes = self._ge_max_payload_bytes()
        for start, end, var, parsed, bit_len in items:
            byte_start = start // 8
            byte_end = (end + 7) // 8
            if current_byte_start is None:
                current_byte_start = byte_start
                current_byte_end = byte_end
                current_items.append((start, end, var, parsed, bit_len))
            elif (
                byte_start - current_byte_end <= max_block_bytes
                and byte_end - current_byte_start <= max_block_bytes
            ):
                # 跨洞和总跨度均受当前设备 payload 上限约束。
                current_byte_end = max(current_byte_end, byte_end)
                current_items.append((start, end, var, parsed, bit_len))
            else:
                # 不可合并：创建块
                self._create_ge_discrete_block(
                    area, current_byte_start, current_byte_end, current_items, result_map, blocks
                )
                current_byte_start = byte_start
                current_byte_end = byte_end
                current_items = [(start, end, var, parsed, bit_len)]

        if current_items:
            self._create_ge_discrete_block(
                area, current_byte_start, current_byte_end, current_items, result_map, blocks
            )


    def _create_ge_discrete_block(
        self,
        area: str,
        start: Optional[int],
        end: Optional[int],
        items: List[tuple],
        result_map: Dict[str, Dict[str, Any]],
        blocks: List,
    ) -> None:
        """创建合并后的 GE 离散量读取块（byte 模式，start/end 为字节偏移）。"""
        from drivers.base import ReadBlock

        if start is None or end is None:
            return

        # 小块向低地址扩齐，避免驱动 max(count,8) 垫长越过区域顶
        if end - start < self.GE_MIN_DISCRETE_BLOCK_BYTES:
            start = max(0, end - self.GE_MIN_DISCRETE_BLOCK_BYTES)

        # block_address 使用块首位的 PLC 逻辑编号（0 基 bit 索引 +1 转回；8 对齐），
        # driver 端 -1 还原 0 基后 //8 得字节偏移
        block_bit_base = start * 8
        block_address = f"%{area}{(block_bit_base + 1):05d}"
        byte_count = end - start

        block = ReadBlock(block_address, count=byte_count, data_type="BYTE")
        blocks.append(block)

        # 填充 result_map：bit_offset 相对块首位；packing 标记 byte 模式（LSB-first 拆位）
        for item_start, item_end, var, parsed, bit_len in items:
            bit_offset = item_start - block_bit_base
            result_map[var.variable_id] = {
                "protocol": "ge_srtp",
                "block_address": block_address,
                "area": area,
                "offset": bit_offset,
                "data_type": "BOOL",
                "bit_offset": bit_offset,
                "packing": "byte",
            }


    def _parse_ge_address(self, address: str) -> Dict[str, Any]:
        """
        解析 GE SRTP 地址，返回地址语义信息。

        返回：
            area: 区域类型 (R/AI/AQ/I/Q/M/T/S/SA/SB/SC/G)
            logical_number: 用户输入的 PLC 逻辑地址编号
            protocol_offset: GE 库需要的 0-based 偏移

        地址基准（已固化）：
            全区域 protocol_offset = logical_number - 1。
            对齐前辈 tmeic 项目实盘约定（readSysMemory 不分区域一律 -1）；
            2026-07-02 %Q2019 真机错位事故定案：离散区旧"暂不减 1"读的是相邻点。
        """
        addr = address.upper()
        match = self._GE_ADDRESS_RE.match(addr)
        if not match:
            raise DeviceConnectionError(
                f"Unsupported GE SRTP address format: {address}. "
                "Expected %R/%AI/%AQ/%I/%Q/%M/%T/%S/%SA/%SB/%SC/%G followed by a number."
            )

        area = match.group(1).upper()
        logical_number = int(match.group(2))

        # GE PLC 逻辑地址必须 >= 1
        if logical_number < 1:
            raise DeviceConnectionError(
                f"Invalid GE SRTP address: {address}. Logical address number must be >= 1."
            )

        # 地址基准转换：全区域一律 -1（%I1 = bit 0，与 %R1 = word 0 同构）
        protocol_offset = logical_number - 1

        return {
            "area": area,
            "logical_number": logical_number,
            "protocol_offset": protocol_offset,
        }


class Decoder:
    @staticmethod
    def _extract_mapped_ge_value(raw_values: Any, mapping: Dict[str, Any]) -> Any:
        """
        从合并后的 GE 块中还原单个变量值。

        寄存器区域 (%R/%AI/%AQ):
            word_offset 定位起始 word
            INT: 有符号 16 位
            WORD/UINT: 无符号 16 位
            DINT: 2 word 组合为有符号 32 位
            DWORD/UDINT: 2 word 组合为无符号 32 位
            REAL/FLOAT: 2 word 组合为 32 位浮点

        离散量区域 (%I/%Q/%M/%T/%S/%SA/%SB/%SC/%G):
            第一版只支持 BOOL
            packing="byte"（byte 模式块）：values 为字节列表，LSB-first 拆位
            （位序与 gesrtp extract_bit_values 一致，bit 模式下已现场验证）
            否则（bit 模式块）：bit_offset 直接从 bit list 中取值

        REAL/DINT/DWORD 字序：
            当前只支持 word_order="low_high"（低地址 word 在前，高地址 word 在后）
            与 GE PACSystems 文档和 gesrtp-py 实现一致，但仍需现场已知值校验后固化
        """
        values = raw_values if isinstance(raw_values, list) else [raw_values]
        area = str(mapping.get("area", ""))
        data_type = str(mapping.get("data_type", "INT")).upper()

        # 寄存器区域：word 偏移
        if area in ("R", "AI", "AQ"):
            word_offset = int(mapping.get("word_offset", 0))
            word_length = int(mapping.get("word_length", 1))
            # 获取 word_order 配置（当前只支持 low_high）
            word_order = str(mapping.get("word_order", "low_high"))

            # 单 word 类型
            if data_type == "INT":
                # 有符号 16 位：gesrtp-py 已用 <h 返回 signed，只需处理 unsigned 输入
                val = int(values[word_offset])
                if val > 0x7FFF:
                    return val - 0x10000  # unsigned 转 signed
                return val
            if data_type in ("WORD", "UINT"):
                return values[word_offset] & 0xFFFF
            if data_type == "USINT":
                return values[word_offset] & 0xFF

            # 2 word 类型：组合为 32 位值
            if word_length >= 2:
                if word_offset + 1 >= len(values):
                    raise ValueError(
                        f"GE 2-word type {data_type} needs words at offset "
                        f"{word_offset},{word_offset + 1} but only {len(values)} values available"
                    )
                if word_order != "low_high":
                    raise ValueError(f"Unsupported GE word_order: {word_order}. Current only supports low_high.")

                low = values[word_offset] & 0xFFFF
                high = values[word_offset + 1] & 0xFFFF

                if data_type in ("DINT",):
                    # 有符号 32 位（low_high word 序：low + high << 16）
                    val = low | (high << 16)
                    if val >= 0x80000000:
                        val -= 0x100000000
                    return val
                if data_type in ("DWORD", "UDINT"):
                    return low | (high << 16)
                if data_type in ("REAL", "FLOAT"):
                    # IEEE 754 浮点（low_high word 序）
                    raw_bytes = struct.pack('<HH', low, high)
                    return struct.unpack('<f', raw_bytes)[0]

            return values[word_offset]

        # 离散量区域：BOOL bit 偏移
        if area in ("I", "Q", "M", "T", "S", "SA", "SB", "SC", "G"):
            if data_type == "BOOL":
                bit_offset = int(mapping.get("bit_offset", 0))
                if str(mapping.get("packing", "")) == "byte":
                    # byte 模式块：LSB-first 拆位（byte k 含块内 bits [8k, 8k+8)）
                    byte_index = bit_offset // 8
                    if byte_index >= len(values):
                        raise ValueError(
                            f"GE byte-packed BOOL bit_offset {bit_offset} out of range "
                            f"for {len(values)}-byte block {mapping.get('block_address', '?')}"
                        )
                    return bool((int(values[byte_index]) >> (bit_offset % 8)) & 1)
                return bool(values[bit_offset])
            # 第一版不支持离散区的多字节类型
            logger.warning(
                "GE discrete area %s does not support %s in this version",
                area, data_type
            )
            return None

        return values[0]


    @staticmethod
    def _log_ge_nonfinite_value(
        device_id: str,
        variable: Any,
        raw_values: Any,
        mapping: Dict[str, Any],
        value: Any,
    ) -> None:
        """记录 GE REAL/FLOAT 解出 NaN/Inf 时的原始 word 与字序对照，不改变采集值。"""
        data_type = str(mapping.get("data_type", "")).upper()
        if data_type not in ("REAL", "FLOAT"):
            return
        if not isinstance(value, float) or math.isfinite(value):
            return

        values = raw_values if isinstance(raw_values, list) else [raw_values]
        word_offset = int(mapping.get("word_offset", 0))
        if word_offset + 1 >= len(values):
            return

        low_word = int(values[word_offset]) & 0xFFFF
        high_word = int(values[word_offset + 1]) & 0xFFFF
        low_high = struct.unpack("<f", struct.pack("<HH", low_word, high_word))[0]
        high_low = struct.unpack("<f", struct.pack("<HH", high_word, low_word))[0]
        logger.warning(
            "GE REAL/FLOAT decoded non-finite: device=%s variable=%s address=%s "
            "data_type=%s block=%s word_offset=%s raw_words=[0x%04X,0x%04X] "
            "low_high=%s high_low=%s",
            device_id,
            getattr(variable, "variable_id", "?"),
            getattr(variable, "address", "?"),
            data_type,
            mapping.get("block_address", "?"),
            word_offset,
            low_word,
            high_word,
            low_high,
            high_low,
        )



def build_read_plan(variables, *, unit_id=1, connection_config=None):
    config = connection_config or {}
    task = ScanTask(scan_rate=0, variables=list(variables))
    Planner(config, config.get("device_id", ""))._build_ge_read_blocks(task)
    return task.read_blocks, task.result_map


def decode_read_results(result, result_map):
    from drivers.base import MISSING_RESULT
    decoded = {}
    cache = {}
    for variable_id, mapping in result_map.items():
        address = mapping["block_address"]
        values = result.get(address)
        if values is None:
            decoded[variable_id] = MISSING_RESULT
            continue
        value = Decoder._extract_mapped_ge_value(values, mapping)
        Decoder._log_ge_nonfinite_value("", SimpleNamespace(variable_id=variable_id, address=address), values, mapping, value)
        decoded[variable_id] = value
    return decoded
