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


    S7_DB_MAX_GAP_BYTES = 64


    S7_DB_MAX_BLOCK_BYTES = 8192


    def _build_s7_read_blocks(self, task: ScanTask) -> None:
        """构建 S7 读取块；按区域分别合并连续读取：I/Q/M 连续字节、Timer/Counter、DB 同号连续字节。"""
        from drivers.base import ReadBlock

        blocks = []
        task.result_map = {}
        merge_groups: Dict[str, List[tuple[int, int, Variable, Dict[str, Any]]]] = {}
        # Timer/Counter 只按区域分组、统一读原始 WORD，再由 decoder 按每变量 data_type 解释：
        # 若按 (area, data_type) 分组，%T1 TIMER_MS 与 T1 WORD 会生成同一 block_address，
        # 驱动结果按地址存字典，后读的块覆盖前者（B011）。
        timer_counter_groups: Dict[str, List[tuple[int, int, Variable, Dict[str, Any]]]] = {}
        db_groups: Dict[int, List[tuple[int, int, Variable, Dict[str, Any]]]] = {}

        for var in task.variables:
            parsed = self._parse_s7_address(var.address)
            if parsed.get("area") in {"I", "Q", "M"}:
                start = int(parsed["offset"])
                size = self._s7_variable_byte_size(parsed, var.data_type)
                merge_groups.setdefault(parsed["area"], []).append((start, start + size, var, parsed))
            elif parsed.get("area") in {"T", "C"}:
                area = str(parsed["area"]).upper()
                start = int(parsed["num"])
                timer_counter_groups.setdefault(area, []).append((start, start + 1, var, parsed))
            elif parsed.get("area") == "DB":
                size = self._s7_db_variable_byte_size(parsed, var.data_type)
                if size is None:
                    # 访问宽度与数据类型不一致（或类型不支持）：不合并，保持单点读，
                    # 交由驱动按原有校验处理（错配会被驱动如实报错并跳过该点）。
                    blocks.append(ReadBlock(var.address, count=1, data_type=var.data_type))
                else:
                    start = int(parsed["offset"])
                    db_groups.setdefault(int(parsed["db"]), []).append((start, start + size, var, parsed))
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

        for area, ranges in sorted(timer_counter_groups.items()):
            ranges.sort(key=lambda item: item[0])
            current: List[tuple[int, int, Variable, Dict[str, Any]]] = []
            current_start: Optional[int] = None
            current_end: Optional[int] = None

            for item in ranges:
                start, end, _, _ = item
                if current and current_end is not None and start > current_end:
                    block = self._create_s7_timer_counter_block(
                        area, current_start, current_end, current, task.result_map
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
                    area, current_start, current_end, current, task.result_map
                )
                blocks.append(block)

        for db_number, ranges in sorted(db_groups.items()):
            ranges.sort(key=lambda item: item[0])
            current = []
            current_start = None
            current_end = None

            for item in ranges:
                start, end, _, _ = item
                # 分段条件：与当前段间隔超过 max_gap，或并入后超过单块上限
                if current and current_start is not None and current_end is not None and (
                    start - current_end > self.S7_DB_MAX_GAP_BYTES
                    or end - current_start > self.S7_DB_MAX_BLOCK_BYTES
                ):
                    block = self._create_s7_db_byte_block(
                        db_number, current_start, current_end, current, task.result_map
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
                block = self._create_s7_db_byte_block(
                    db_number, current_start, current_end, current, task.result_map
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
        # 统一读原始 WORD；同编号不同类型的变量共享同一块，按各自 data_type 解码。
        block = ReadBlock(block_address, count=end - start, data_type="WORD")
        for item_start, _, var, _ in items:
            result_map[var.variable_id] = {
                "block_address": block_address,
                "area": area,
                "value_offset": item_start - start,
                "data_type": var.data_type,
                "raw_word": True,
            }
        return block


    def _create_s7_db_byte_block(
        self,
        db_number: int,
        start: Optional[int],
        end: Optional[int],
        items: List[tuple[int, int, Variable, Dict[str, Any]]],
        result_map: Dict[str, Dict[str, Any]],
    ):
        """创建合并后的 DB 字节读取块，并记录变量还原映射。"""
        from drivers.base import ReadBlock

        if start is None or end is None:
            raise DeviceConnectionError("Invalid S7 DB merge range")
        block_address = f"DB{db_number}.DBB{start}"
        block = ReadBlock(block_address, count=end - start, data_type="BYTE")
        for item_start, _, var, parsed in items:
            result_map[var.variable_id] = {
                "block_address": block_address,
                "byte_offset": item_start - start,
                "bit": parsed.get("bit"),
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
    def _s7_db_variable_byte_size(parsed: Dict[str, Any], data_type: str) -> Optional[int]:
        """计算 S7 DB 变量读取所需字节数（按 DBX/DBB/DBW/DBD 访问宽度）。

        若访问宽度与数据类型的解码宽度不一致，或数据类型不被还原逻辑支持，返回
        None，表示该点不应并入合并块（保持单点读，沿用驱动既有校验）。
        """
        access = str(parsed.get("access", "")).upper()
        access_size = {"DBX": 1, "DBB": 1, "DBW": 2, "DBD": 4}.get(access)
        decode_size = {
            "BOOL": 1, "BYTE": 1, "INT": 2, "WORD": 2,
            "REAL": 4, "FLOAT": 4, "DINT": 4, "DWORD": 4,
        }.get((data_type or "").upper())
        if access_size is None or decode_size is None or access_size != decode_size:
            return None
        return access_size


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


class Decoder:
    @staticmethod
    def _to_s7_bytearray(raw_values: Any) -> bytearray:
        """把合并块原始值转为 bytearray（已是 bytes/bytearray 则原样返回）。"""
        if isinstance(raw_values, (bytes, bytearray)):
            return raw_values
        values = raw_values if isinstance(raw_values, list) else [raw_values]
        return bytearray(int(v) & 0xFF for v in values)


    @staticmethod
    def _decode_s7_timer_counter_word(area: str, word: Any, data_type: str) -> Any:
        """按变量 data_type 解释 Timer/Counter 原始字（与驱动 _parse_s5time/_parse_counter_bcd 语义一致）。

        WORD → 原始字；Timer 其它类型 → S5TIME 毫秒（时基在字 bit13-12）；Counter 其它类型 → BCD 计数。
        """
        if word is None or data_type == "WORD":
            return word
        word = int(word) & 0xFFFF
        bcd_value = ((word >> 8) & 0x0F) * 100 + ((word >> 4) & 0x0F) * 10 + (word & 0x0F)
        if area == "T":
            time_base_ms = {0: 10, 1: 100, 2: 1000, 3: 10000}[(word >> 12) & 0x03]
            return bcd_value * time_base_ms
        return bcd_value


    @staticmethod
    def _extract_mapped_s7_value(raw_values: Any, mapping: Dict[str, Any], prepared: Any = None) -> Any:
        """从合并后的 S7 I/Q/M BYTE 块或 Timer/Counter 块中还原单个变量值。

        prepared: 调用方可传入按块预转换的 bytearray，避免每个变量重建整块（O(N²)→O(N)）。
        """
        if "value_offset" in mapping:
            values = raw_values if isinstance(raw_values, list) else [raw_values]
            value = values[int(mapping.get("value_offset", 0))]
            if mapping.get("raw_word"):
                return Decoder._decode_s7_timer_counter_word(
                    str(mapping.get("area", "")).upper(), value, str(mapping.get("data_type", "")).upper()
                )
            return value

        raw = prepared if prepared is not None else Decoder._to_s7_bytearray(raw_values)
        offset = int(mapping.get("byte_offset", 0))
        bit = mapping.get("bit")
        data_type = str(mapping.get("data_type", "BYTE")).upper()

        if bit is not None:
            return bool((raw[offset] >> int(bit)) & 1)
        if data_type == "BYTE":
            return raw[offset]
        if data_type == "INT":
            return struct.unpack(">h", raw[offset:offset + 2])[0]
        if data_type == "WORD":
            return struct.unpack(">H", raw[offset:offset + 2])[0]
        if data_type in {"REAL", "FLOAT"}:
            return struct.unpack(">f", raw[offset:offset + 4])[0]
        if data_type == "DINT":
            return struct.unpack(">i", raw[offset:offset + 4])[0]
        if data_type == "DWORD":
            return struct.unpack(">I", raw[offset:offset + 4])[0]
        return raw[offset]



def build_read_plan(variables, *, unit_id=1, connection_config=None):
    config = connection_config or {}
    task = ScanTask(scan_rate=0, variables=list(variables))
    Planner(config, config.get("device_id", ""))._build_s7_read_blocks(task)
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
        prepared = None
        if "value_offset" not in mapping:
            if address not in cache:
                cache[address] = Decoder._to_s7_bytearray(values)
            prepared = cache[address]
        decoded[variable_id] = Decoder._extract_mapped_s7_value(values, mapping, prepared)
    return decoded
