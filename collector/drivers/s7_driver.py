"""S7协议驱动实现（基于python-snap7）"""

import asyncio
import re
from typing import Any, Dict, List

from core.exceptions import DeviceConnectionError, ReadError
from core.logger import get_logger
from .base import BaseDriver, ReadBlock, WritePoint

logger = get_logger("s7_driver")


class S7Driver(BaseDriver):
    """西门子S7 PLC驱动，基于python-snap7"""

    _AREA_CODES = {
        "I": 0x81,  # process inputs
        "Q": 0x82,  # process outputs
        "M": 0x83,  # merkers / memory
    }
    _DB_ADDRESS_RE = re.compile(
        r"^DB(?P<db>\d+)\.(?P<access>DBX|DBB|DBW|DBD)(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
        re.IGNORECASE,
    )
    _AREA_ADDRESS_RE = re.compile(
        r"^%?(?P<area>I|Q|M)(?P<width>B|W|D)?(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
        re.IGNORECASE,
    )

    def __init__(self):
        super().__init__()
        self._client = None
        self._ip = None
        self._rack = None
        self._slot = None

    async def connect(self, config: Dict[str, Any]) -> None:
        """
        连接到S7 PLC
        config: {ip, rack, slot, port(optional)}
        """
        import snap7

        self._ip = config.get("ip", "127.0.0.1")
        self._rack = config.get("rack", 0)
        self._slot = config.get("slot", 1)
        port = config.get("port", 102)

        self._client = snap7.client.Client()
        try:
            # snap7 是阻塞调用，用线程池执行
            loop = asyncio.get_event_loop()
            await loop.run_in_executor(
                None, self._client.connect, self._ip, self._rack, self._slot, port
            )
            self._connected = True
            logger.info(f"S7 connected to {self._ip}:{port} rack={self._rack} slot={self._slot}")
        except Exception as e:
            raise DeviceConnectionError(f"S7 connect failed: {e}") from e

    async def disconnect(self) -> None:
        """断开S7连接"""
        if self._client:
            try:
                loop = asyncio.get_event_loop()
                await loop.run_in_executor(None, self._client.disconnect)
            except Exception as e:
                logger.warning(f"S7 disconnect error: {e}")
            finally:
                self._connected = False
                self._client = None
                logger.info("S7 disconnected")

    async def read(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """
        批量读取S7 PLC数据
        block.address 格式:
        - "DB1.DBX0.0"
        - "DB100.DBB10"
        - "DB50.DBW20"
        - "DB30.DBD100"
        - "%I101.0", "%Q101.0", "%M101.0"
        - "%MB1304", "%MW1304", "%MD1304"
        """
        if not self._connected or not self._client:
            raise ReadError("S7 driver not connected")

        if not blocks:
            return {}

        # 同一个 PLC client 串行读取，避免多周期任务并发访问同一连接。
        async with self._read_lock:
            loop = asyncio.get_event_loop()
            return await loop.run_in_executor(None, self._read_blocks, blocks)

    def _read_blocks(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """在线程中批量读取所有block"""
        result = {}
        for block in blocks:
            try:
                values = self._read_single_block(block)
                result[block.address] = values
            except Exception as e:
                logger.error(f"S7 read block {block.address} failed: {e}")
                result[block.address] = None
        return result

    def _read_single_block(self, block: ReadBlock) -> List[Any]:
        """读取单个地址块"""
        addr = block.address
        count = block.count
        data_type = block.data_type

        if addr.upper().startswith("DB"):
            return self._read_db_block(addr, data_type, count)
        return self._read_area_block(addr, data_type, count)

    def _read_db_block(self, address: str, data_type: str, count: int) -> List[Any]:
        """读取 DB 地址，兼容 DBX / DBB / DBW / DBD 标准语法。"""
        match = self._DB_ADDRESS_RE.match(address.upper())
        if match:
            db_number = int(match.group("db"))
            access = match.group("access").upper()
            byte_offset = int(match.group("byte"))
            bit_text = match.group("bit")
            bit_offset = int(bit_text) if bit_text is not None else None

            if access == "DBX":
                if data_type.upper() != "BOOL":
                    raise ReadError(f"DBX address must use BOOL data type: {address} ({data_type})")
                if bit_offset is None or not 0 <= bit_offset <= 7:
                    raise ReadError(f"Invalid DBX bit address: {address}")
                raw = self._client.db_read(db_number, byte_offset, 1)
                return [bool((raw[0] >> bit_offset) & 1)]

            if bit_offset is not None:
                raise ReadError(f"Bit suffix is only supported for DBX address: {address}")

            if data_type.upper() == "BOOL":
                raise ReadError(f"BOOL values must use DBX bit address syntax: {address}")

            expected_size = {"DBB": 1, "DBW": 2, "DBD": 4}[access]
            actual_size = self._calc_byte_size(data_type, count)
            if actual_size != expected_size:
                raise ReadError(
                    f"S7 address {address} expects {expected_size} bytes, got data_type={data_type} count={count}"
                )
            if access in {"DBW", "DBD"} and byte_offset % 2 != 0:
                raise ReadError(f"S7 {access} address must use an even byte offset: {address}")

            raw = self._client.db_read(db_number, byte_offset, expected_size)
            return self._parse_bytes(raw, data_type, count)

        raise ReadError(f"Unsupported S7 DB address format: {address}")

    def _read_area_block(self, address: str, data_type: str, count: int) -> List[Any]:
        """读取 I/Q/M 区地址，支持位、字节、字、双字寻址。"""
        match = self._AREA_ADDRESS_RE.match(address.upper())
        if not match:
            raise ReadError(f"Unsupported S7 address format: {address}")

        area = match.group("area").upper()
        width = (match.group("width") or "").upper()
        byte_offset = int(match.group("byte"))
        bit_text = match.group("bit")
        bit_offset = int(bit_text) if bit_text is not None else None

        if bit_offset is not None:
            if data_type.upper() != "BOOL":
                raise ReadError(f"Bit address must use BOOL data type: {address} ({data_type})")
            if width:
                raise ReadError(f"Bit suffix is not allowed for byte/word/dword address: {address}")
            if not 0 <= bit_offset <= 7:
                raise ReadError(f"Invalid bit address: {address}")
            raw = self._client.read_area(self._AREA_CODES[area], 0, byte_offset, 1)
            return [bool((raw[0] >> bit_offset) & 1)]

        if not width:
            raise ReadError(f"S7 area address must include bit suffix or B/W/D width: {address}")
        if data_type.upper() == "BOOL":
            raise ReadError(f"BOOL values must use bit address syntax: {address}")

        expected_size = {"B": 1, "W": 2, "D": 4}.get(width)
        actual_size = self._calc_byte_size(data_type, count)
        if expected_size is not None and actual_size != expected_size:
            raise ReadError(
                f"S7 address {address} expects {expected_size} bytes, got data_type={data_type} count={count}"
            )

        byte_size = expected_size if expected_size is not None else actual_size
        raw = self._client.read_area(self._AREA_CODES[area], 0, byte_offset, byte_size)
        return self._parse_bytes(raw, data_type, count)

    def _calc_byte_size(self, data_type: str, count: int) -> int:
        """计算需要的字节数"""
        type_sizes = {"BOOL": 1, "BYTE": 1, "INT": 2, "WORD": 2, "REAL": 4, "FLOAT": 4, "DINT": 4, "DWORD": 4}
        return type_sizes.get(data_type.upper(), 2) * count

    def _parse_bytes(self, data: bytearray, data_type: str, count: int) -> List[Any]:
        """解析原始字节为Python值"""
        import struct

        values = []
        dt = data_type.upper()

        if dt == "BOOL":
            for i in range(count):
                byte_idx = i // 8
                bit_idx = i % 8
                if byte_idx < len(data):
                    values.append(bool((data[byte_idx] >> bit_idx) & 1))
        elif dt in ("INT",):
            for i in range(count):
                offset = i * 2
                if offset + 2 <= len(data):
                    values.append(struct.unpack(">h", data[offset:offset + 2])[0])
        elif dt in ("WORD",):
            for i in range(count):
                offset = i * 2
                if offset + 2 <= len(data):
                    values.append(struct.unpack(">H", data[offset:offset + 2])[0])
        elif dt in ("REAL", "FLOAT"):
            for i in range(count):
                offset = i * 4
                if offset + 4 <= len(data):
                    values.append(struct.unpack(">f", data[offset:offset + 4])[0])
        elif dt in ("DINT",):
            for i in range(count):
                offset = i * 4
                if offset + 4 <= len(data):
                    values.append(struct.unpack(">i", data[offset:offset + 4])[0])
        elif dt == "DWORD":
            for i in range(count):
                offset = i * 4
                if offset + 4 <= len(data):
                    values.append(struct.unpack(">I", data[offset:offset + 4])[0])
        elif dt == "BYTE":
            for i in range(count):
                if i < len(data):
                    values.append(data[i])
        elif dt == "STRING":
            # S7 字符串格式: 第一个字节是最大长度，第二个字节是实际长度，后面是字符
            for i in range(count):
                offset = i * 256  # S7 STRING 默认 256 字节
                if offset + 2 <= len(data):
                    max_len = data[offset]
                    actual_len = data[offset + 1]
                    if offset + 2 + actual_len <= len(data):
                        try:
                            s = data[offset + 2:offset + 2 + actual_len].decode("ascii", errors="replace")
                            values.append(s.rstrip("\x00"))
                        except Exception:
                            values.append("")
                    else:
                        values.append("")
                else:
                    values.append("")
        else:
            values = list(data[:count])

        return values

    async def write(self, points: List[WritePoint]) -> None:
        """S7 驱动在采集模块中保持只读，禁止写 PLC。"""
        raise ReadError("S7 driver is read-only in collector")

    async def ping(self) -> bool:
        """S7健康检查（无副作用，不调用 PLC 运行/停止类方法）。"""
        if not self._connected or not self._client:
            return False

        try:
            get_connected = getattr(self._client, "get_connected", None)
            if callable(get_connected):
                loop = asyncio.get_event_loop()
                connected = await loop.run_in_executor(None, get_connected)
                self._connected = bool(connected)
                return self._connected
            return self._connected
        except Exception as exc:
            logger.warning("S7 safe ping failed: %s", exc)
            self._connected = False
            return False
