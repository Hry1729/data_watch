"""S7协议驱动实现（基于python-snap7）"""

import asyncio
import re
import threading
from typing import Any, Dict, List

import snap7

from core.exceptions import DeviceConnectionError, ReadError
from core.logger import get_logger
from drivers.base import BaseDriver, ReadBlock

logger = get_logger("s7_driver")


class S7Driver(BaseDriver):
    """西门子S7 PLC驱动，基于python-snap7"""

    _AREA_CODES = {
        "I": 0x81,  # process inputs
        "Q": 0x82,  # process outputs
        "M": 0x83,  # merkers / memory
        "T": 0x1D,  # timers (S7-300/400)
        "C": 0x1C,  # counters (S7-300/400)
    }
    _DB_ADDRESS_RE = re.compile(
        r"^%?DB(?P<db>\d+)\.(?P<access>DBX|DBB|DBW|DBD)(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
        re.IGNORECASE,
    )
    _AREA_ADDRESS_RE = re.compile(
        r"^%?(?P<area>I|Q|M)(?P<width>B|W|D)?(?P<byte>\d+)(?:\.(?P<bit>\d+))?$",
        re.IGNORECASE,
    )
    _TIMER_COUNTER_RE = re.compile(
        r"^%?(?P<type>T|C)(?P<num>\d+)$",
        re.IGNORECASE,
    )

    def __init__(self, read_executor=None):
        super().__init__()
        # 专用 PLC 读线程池（§7.3）；None 时回退默认 executor，保持原行为与测试兼容。
        self._read_executor = read_executor
        self._client = None
        self._client_lock = threading.Lock()
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
        read_timeout_ms = config.get("read_timeout_ms")
        if read_timeout_ms is not None:
            from snap7.type import Parameter
            timeout_ms = int(read_timeout_ms)
            self._client.set_param(Parameter.SendTimeout, timeout_ms)
            self._client.set_param(Parameter.RecvTimeout, timeout_ms)
        try:
            # snap7 是阻塞调用，用线程池执行
            loop = asyncio.get_event_loop()
            await loop.run_in_executor(
                self._read_executor, self._client.connect, self._ip, self._rack, self._slot, port
            )
            # 不再人为钳位 PDU：早期 Invalid address(0x05) 实为配置了不存在的点位所致，
            # 删点后已解决，与 PDU 无关。保留 snap7 与 PLC 协商出的真实 PDU——大块连续读时
            # 高 PDU（如 480）可减少底层分帧往返，提升读取速度。日志照打协商值便于核对。
            negotiated_pdu = getattr(self._client, "pdu_length", None)
            self._connected = True
            logger.info(
                f"S7 connected to {self._ip}:{port} rack={self._rack} slot={self._slot} "
                f"pdu_negotiated={negotiated_pdu}"
            )
        except Exception as e:
            raise DeviceConnectionError(f"S7 connect failed: {e}") from e

    async def disconnect(self) -> None:
        """断开S7连接"""
        loop = asyncio.get_event_loop()
        await loop.run_in_executor(self._read_executor, self._disconnect_client)

    def _disconnect_client(self) -> None:
        """在线程中等待当前读取结束后断开连接。"""
        with self._client_lock:
            if self._client is None:
                return
            try:
                self._client.disconnect()
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
            return await loop.run_in_executor(self._read_executor, self._read_blocks, blocks)

    def _read_blocks(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """在线程中批量读取所有block"""
        from snap7.error import S7ConnectionError, S7TimeoutError
        with self._client_lock:
            if self._client is None:
                raise ReadError("S7 driver not connected")

            result = {}
            for block in blocks:
                try:
                    values = self._read_single_block(block)
                    result[block.address] = values
                except (S7ConnectionError, S7TimeoutError, ConnectionError, TimeoutError, OSError) as e:
                    self._connected = False
                    raise ReadError(f"S7 connection failed while reading {block.address}: {e}") from e
                except Exception as e:
                    logger.error(f"S7 read block {block.address} failed: {e}")
                    result[block.address] = None
            return result

    def _read_single_block(self, block: ReadBlock) -> List[Any]:
        """读取单个地址块"""
        addr = block.address
        count = block.count
        data_type = block.data_type

        upper_addr = addr.upper()
        if upper_addr.startswith("DB") or upper_addr.startswith("%DB"):
            return self._read_db_block(addr, data_type, count)

        # 检查是否为 Timer/Counter
        tc_match = self._TIMER_COUNTER_RE.match(addr.upper())
        if tc_match:
            return self._read_timer_counter_block(addr, data_type, count)

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

            unit_size = {"DBB": 1, "DBW": 2, "DBD": 4}[access]
            expected_size = unit_size * count
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

        unit_size = {"B": 1, "W": 2, "D": 4}.get(width)
        expected_size = unit_size * count if unit_size is not None else None
        actual_size = self._calc_byte_size(data_type, count)
        if expected_size is not None and actual_size != expected_size:
            raise ReadError(
                f"S7 address {address} expects {expected_size} bytes, got data_type={data_type} count={count}"
            )

        byte_size = expected_size if expected_size is not None else actual_size
        raw = self._client.read_area(self._AREA_CODES[area], 0, byte_offset, byte_size)
        return self._parse_bytes(raw, data_type, count)

    def _read_timer_counter_block(self, address: str, data_type: str, count: int) -> List[Any]:
        """
        读取 S7 Timer (%T) 或 Counter (%C)。

        Timer 值为 BCD 格式的 S5TIME，单位为 10ms 基准的时基倍数。
        Counter 值为 BCD 格式的计数值。

        返回：
            Timer: 毫秒数（int）或原始 BCD 值（视 data_type）
            Counter: 整数值（int）
        """
        match = self._TIMER_COUNTER_RE.match(address.upper())
        if not match:
            raise ReadError(f"Invalid Timer/Counter address: {address}")

        area_type = match.group("type").upper()
        num = int(match.group("num"))

        if area_type not in self._AREA_CODES:
            raise ReadError(f"Unknown S7 Timer/Counter type: {area_type}")

        # python-snap7 read_area() 对 Timer/Counter 的 size 参数含义是"读取几个",
        # 不是字节数。每个 Timer/Counter 返回 2 字节。
        raw = self._client.read_area(
            self._AREA_CODES[area_type],
            0,
            num,
            count,
        )
        expected_bytes = count * 2
        if len(raw) < expected_bytes:
            raise ReadError(
                f"S7 Timer/Counter read returned {len(raw)} bytes, expected {expected_bytes}: {address}"
            )

        results = []
        for i in range(count):
            item_raw = raw[i * 2:(i + 1) * 2]
            if area_type == "T":
                # Timer: 解析 S5TIME BCD 格式并转换为毫秒
                results.append(self._parse_s5time(item_raw, data_type))
            else:
                # Counter: 解析 BCD 格式计数值
                results.append(self._parse_counter_bcd(item_raw, data_type))

        return results

    @staticmethod
    def _parse_s5time(data: bytearray, data_type: str) -> Any:
        """
        解析 S7 S5TIME BCD 格式为毫秒数。

        S5TIME 格式 (2 字节):
        - 字节 0:
          - bit 7-6: 时基 (00=10ms, 01=100ms, 10=1s, 11=10s)
          - bit 5-4: 未使用 (总是 0)
          - bit 3-0: BCD 百位 (0-9)
        - 字节 1:
          - bit 7-4: BCD 十位 (0-9)
          - bit 3-0: BCD 个位 (0-9)

        返回：
            data_type 为 "TIMER_MS" 或默认: 毫秒数 (int)
            data_type 为 "WORD": 原始 2 字节值 (int)
        """
        if data_type.upper() == "WORD":
            return (data[0] << 8) | data[1]

        # 解析 BCD 值: 百位在字节0的低4位，十位和个位在字节1
        bcd_hundreds = data[0] & 0x0F
        bcd_tens = (data[1] >> 4) & 0x0F
        bcd_ones = data[1] & 0x0F
        bcd_value = bcd_hundreds * 100 + bcd_tens * 10 + bcd_ones

        # 解析时基 (bits 7-6)
        time_base_bits = (data[0] >> 6) & 0x03
        time_base_ms = {0: 10, 1: 100, 2: 1000, 3: 10000}.get(time_base_bits, 10)

        return bcd_value * time_base_ms

    @staticmethod
    def _parse_counter_bcd(data: bytearray, data_type: str) -> Any:
        """
        解析 S7 Counter BCD 格式为整数值。

        Counter 格式 (2 字节): BCD 编码的计数值 (0-999)
        - 字节 0 高 4 位: 未使用 (0)
        - 字节 0 低 4 位: BCD 百位 (0-9)
        - 字节 1 高 4 位: BCD 十位 (0-9)
        - 字节 1 低 4 位: BCD 个位 (0-9)

        返回：
            data_type 为 "COUNTER" 或默认: 整数值 (int)
            data_type 为 "WORD": 原始 2 字节值 (int)
        """
        if data_type.upper() == "WORD":
            return (data[0] << 8) | data[1]

        # 解析 BCD 值 (范围 0-999)
        bcd_hundreds = data[0] & 0x0F
        bcd_tens = (data[1] >> 4) & 0x0F
        bcd_ones = data[1] & 0x0F

        return bcd_hundreds * 100 + bcd_tens * 10 + bcd_ones

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


    async def ping(self) -> bool:
        """S7健康检查（无副作用，不调用 PLC 运行/停止类方法）。"""
        try:
            loop = asyncio.get_event_loop()
            return await loop.run_in_executor(self._read_executor, self._ping_client)
        except Exception as exc:
            logger.warning("S7 safe ping failed: %s", exc)
            self._connected = False
            return False

    def _ping_client(self) -> bool:
        """在线程中串行检查连接，避免与读取共同操作同一 socket。"""
        with self._client_lock:
            if not self._connected or self._client is None:
                return False
            get_connected = getattr(self._client, "get_connected", None)
            if callable(get_connected):
                self._connected = bool(get_connected())
            return self._connected


def create_driver(context):
    return S7Driver(read_executor=context.read_executor)
