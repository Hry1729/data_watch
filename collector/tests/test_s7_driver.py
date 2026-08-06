"""S7驱动测试"""

import asyncio
import threading
from unittest.mock import patch

import pytest

from drivers.base import ReadBlock
from drivers.s7_driver import S7Driver


class FakeS7Client:
    def __init__(self):
        self.db_calls = []
        self.area_calls = []
        self.plc_run_called = False
        self.connected = True

    def db_read(self, db_number, start, size):
        self.db_calls.append((db_number, start, size))
        mapping = {
            (1, 0, 1): bytearray([0b00000001]),
            (100, 10, 1): bytearray([0x12]),
            (50, 20, 2): bytearray([0x00, 0x7B]),
            (30, 100, 4): bytearray([0x41, 0x20, 0x00, 0x00]),
        }
        return mapping[(db_number, start, size)]

    def read_area(self, area, db_number, start, size):
        self.area_calls.append((area, db_number, start, size))
        # I/Q/M 区域
        mapping = {
            (0x81, 0, 101, 1): bytearray([0b00000001]),
            (0x81, 0, 101, 2): bytearray([0b00000010, 0x2C]),
            (0x82, 0, 101, 1): bytearray([0b00000010]),
            (0x83, 0, 101, 1): bytearray([0b00000100]),
            (0x83, 0, 1304, 4): bytearray([0x00, 0x00, 0x00, 0x2A]),
            (0x83, 0, 1306, 2): bytearray([0x00, 0x2B]),
            (0x83, 0, 1308, 1): bytearray([0x2C]),
            # 定时器区域（0x1D）：S5TIME BCD 格式
            # T1：50 * 10ms = 500ms（time_base=00, BCD=50）
            # 字节0 = (0 << 6) | 0 = 0x00, 字节1 = (5 << 4) | 0 = 0x50
            (0x1D, 0, 1, 1): bytearray([0x00, 0x50]),
            # T2：123 * 100ms = 12300ms（time_base=01, BCD=123）
            # 字节0 = (1 << 6) | 1 = 0x41, 字节1 = (2 << 4) | 3 = 0x23
            (0x1D, 0, 2, 1): bytearray([0x41, 0x23]),
            # T1-T2 批量读取
            (0x1D, 0, 1, 2): bytearray([0x00, 0x50, 0x41, 0x23]),
            # T10：500 * 1s = 500000ms（time_base=10, BCD=500）
            # 字节0 = (2 << 6) | 5 = 0x85, 字节1 = (0 << 4) | 0 = 0x00
            (0x1D, 0, 10, 1): bytearray([0x85, 0x00]),
            # 计数器区域（0x1C）：BCD 格式（0-999）
            # C1：BCD 123 = 十进制 123
            (0x1C, 0, 1, 1): bytearray([0x01, 0x23]),
            # C5：BCD 99 = 十进制 99
            (0x1C, 0, 5, 1): bytearray([0x00, 0x99]),
        }
        return mapping[(area, db_number, start, size)]

    def get_connected(self):
        return self.connected

    def plc_run(self):
        self.plc_run_called = True


class BlockingS7Client(FakeS7Client):
    def __init__(self):
        super().__init__()
        self.read_started = threading.Event()
        self.release_read = threading.Event()
        self.disconnect_called = threading.Event()
        self.get_connected_called = threading.Event()

    def db_read(self, db_number, start, size):
        self.read_started.set()
        self.release_read.wait(timeout=2)
        return super().db_read(db_number, start, size)

    def disconnect(self):
        self.disconnect_called.set()
        self.connected = False

    def get_connected(self):
        self.get_connected_called.set()
        return self.connected


def make_driver(client):
    driver = S7Driver()
    driver._client = client
    driver._connected = True
    return driver


def test_read_standard_db_formats():
    client = FakeS7Client()
    driver = make_driver(client)

    assert driver._read_single_block(ReadBlock("DB1.DBX0.0", data_type="BOOL")) == [True]
    assert driver._read_single_block(ReadBlock("DB100.DBB10", data_type="BYTE")) == [0x12]
    assert driver._read_single_block(ReadBlock("DB50.DBW20", data_type="INT")) == [123]
    assert driver._read_single_block(ReadBlock("DB30.DBD100", data_type="REAL"))[0] == pytest.approx(10.0)
    assert client.db_calls == [
        (1, 0, 1),
        (100, 10, 1),
        (50, 20, 2),
        (30, 100, 4),
    ]


def test_read_s7_area_formats_from_mysql_baseline():
    client = FakeS7Client()
    driver = make_driver(client)

    assert driver._read_single_block(ReadBlock("%I101.0", data_type="BOOL")) == [True]
    assert driver._read_single_block(ReadBlock("%Q101.1", data_type="BOOL")) == [True]
    assert driver._read_single_block(ReadBlock("%M101.2", data_type="BOOL")) == [True]
    assert driver._read_single_block(ReadBlock("%MD1304", data_type="DINT")) == [42]
    assert driver._read_single_block(ReadBlock("%MW1306", data_type="INT")) == [43]
    assert driver._read_single_block(ReadBlock("%MB1308", data_type="BYTE")) == [44]


def test_read_s7_area_byte_block_with_count_for_merged_task():
    client = FakeS7Client()
    driver = make_driver(client)

    assert driver._read_single_block(ReadBlock("%IB101", count=2, data_type="BYTE")) == [2, 44]
    assert client.area_calls == [(0x81, 0, 101, 2)]


def test_rejects_legacy_db_offset_format():
    driver = make_driver(FakeS7Client())

    with pytest.raises(Exception, match="Unsupported S7 DB address format"):
        driver._read_single_block(ReadBlock("DB100.0", data_type="INT"))


def test_rejects_ambiguous_s7_area_and_bool_byte_formats():
    driver = make_driver(FakeS7Client())

    with pytest.raises(Exception, match="bit suffix or B/W/D"):
        driver._read_single_block(ReadBlock("%I101", data_type="BOOL"))

    with pytest.raises(Exception, match="BOOL values must use DBX"):
        driver._read_single_block(ReadBlock("DB100.DBB10", data_type="BOOL"))


@pytest.mark.asyncio
async def test_ping_is_safe_and_does_not_call_plc_run():
    client = FakeS7Client()
    driver = make_driver(client)

    assert await driver.ping() is True
    assert client.plc_run_called is False


@pytest.mark.asyncio
async def test_s7_write_is_disabled():
    driver = make_driver(FakeS7Client())

    with pytest.raises(Exception, match="read-only"):
        await driver.write([])


@pytest.mark.asyncio
async def test_disconnect_waits_for_cancelled_read_worker():
    client = BlockingS7Client()
    driver = make_driver(client)

    read_task = asyncio.create_task(
        driver.read([ReadBlock("DB1.DBX0.0", data_type="BOOL")])
    )
    for _ in range(100):
        if client.read_started.is_set():
            break
        await asyncio.sleep(0.01)
    assert client.read_started.is_set()

    read_task.cancel()
    with pytest.raises(asyncio.CancelledError):
        await read_task

    disconnect_task = asyncio.create_task(driver.disconnect())
    await asyncio.sleep(0.05)
    assert not disconnect_task.done()
    assert driver._client is client
    assert not client.disconnect_called.is_set()

    client.release_read.set()
    await asyncio.wait_for(disconnect_task, timeout=1)

    assert client.disconnect_called.is_set()
    assert driver._client is None
    assert driver.is_connected is False


@pytest.mark.asyncio
async def test_ping_waits_for_in_flight_read_worker():
    client = BlockingS7Client()
    driver = make_driver(client)

    read_task = asyncio.create_task(
        driver.read([ReadBlock("DB1.DBX0.0", data_type="BOOL")])
    )
    for _ in range(100):
        if client.read_started.is_set():
            break
        await asyncio.sleep(0.01)
    assert client.read_started.is_set()

    ping_task = asyncio.create_task(driver.ping())
    await asyncio.sleep(0.05)
    assert not ping_task.done()
    assert not client.get_connected_called.is_set()

    client.release_read.set()
    await asyncio.wait_for(read_task, timeout=1)

    assert await asyncio.wait_for(ping_task, timeout=1) is True
    assert client.get_connected_called.is_set()


# ==================== 定时器/计数器测试 ====================

def test_read_timer_as_milliseconds():
    """Timer 读取为毫秒数"""
    client = FakeS7Client()
    driver = make_driver(client)

    # T1：50 * 10ms = 500ms
    result = driver._read_single_block(ReadBlock("%T1", data_type="TIMER_MS"))
    assert result == [500]

    # T2：123 * 100ms = 12300ms
    result = driver._read_single_block(ReadBlock("%T2", data_type="TIMER_MS"))
    assert result == [12300]

    # T10：500 * 1s = 500000ms
    result = driver._read_single_block(ReadBlock("%T10", data_type="TIMER_MS"))
    assert result == [500000]


def test_read_timer_as_word():
    """Timer 读取为原始 WORD 值"""
    client = FakeS7Client()
    driver = make_driver(client)

    result = driver._read_single_block(ReadBlock("%T1", data_type="WORD"))
    assert result == [0x0050]


def test_read_timer_batch_with_count():
    """Timer count>1 时按 Timer 个数批量读取，不按字节数读取"""
    client = FakeS7Client()
    driver = make_driver(client)

    result = driver._read_single_block(ReadBlock("%T1", count=2, data_type="TIMER_MS"))
    assert result == [500, 12300]
    assert client.area_calls == [(0x1D, 0, 1, 2)]


def test_read_counter_as_int():
    """Counter 读取为整数值"""
    client = FakeS7Client()
    driver = make_driver(client)

    # C1：BCD 0x0123 = 123
    result = driver._read_single_block(ReadBlock("%C1", data_type="COUNTER"))
    assert result == [123]

    # C5：BCD 0x0099 = 99
    result = driver._read_single_block(ReadBlock("%C5", data_type="COUNTER"))
    assert result == [99]


def test_read_counter_as_word():
    """Counter 读取为原始 WORD 值"""
    client = FakeS7Client()
    driver = make_driver(client)

    result = driver._read_single_block(ReadBlock("%C1", data_type="WORD"))
    assert result == [0x0123]


def test_parse_s5time_bcd():
    """S5TIME BCD 解析测试"""
    # 10ms 时基, BCD=50 -> 500ms
    data = bytearray([0x00, 0x50])
    assert S7Driver._parse_s5time(data, "TIMER_MS") == 500

    # 100ms 时基, BCD=123 -> 12300ms
    # 字节0 = (1 << 6) | 1 = 0x41, 字节1 = (2 << 4) | 3 = 0x23
    data = bytearray([0x41, 0x23])
    assert S7Driver._parse_s5time(data, "TIMER_MS") == 12300

    # 1s 时基, BCD=500 -> 500000ms
    # 字节0 = (2 << 6) | 5 = 0x85, 字节1 = 0x00
    data = bytearray([0x85, 0x00])
    assert S7Driver._parse_s5time(data, "TIMER_MS") == 500000

    # 10s 时基, BCD=999 -> 9990000ms
    # 字节0 = (3 << 6) | 9 = 0xC9, 字节1 = (9 << 4) | 9 = 0x99
    data = bytearray([0xC9, 0x99])
    assert S7Driver._parse_s5time(data, "TIMER_MS") == 9990000


def test_parse_counter_bcd():
    """Counter BCD 解析测试"""
    # BCD 0x0123 = 123
    data = bytearray([0x01, 0x23])
    assert S7Driver._parse_counter_bcd(data, "COUNTER") == 123

    # BCD 0x0999 = 999
    data = bytearray([0x09, 0x99])
    assert S7Driver._parse_counter_bcd(data, "COUNTER") == 999

    # BCD 0x0000 = 0
    data = bytearray([0x00, 0x00])
    assert S7Driver._parse_counter_bcd(data, "COUNTER") == 0


# ==================== PDU 协商值保留 Tests ====================

class FakePduClient:
    """模拟 connect 后 pdu_length 停在协商值的 snap7 client。"""

    def __init__(self, pdu_length):
        self.pdu_length = pdu_length
        self.param_calls = []

    def set_param(self, parameter, value):
        self.param_calls.append((parameter, value))

    def connect(self, ip, rack, slot, port):
        # 真实 snap7 在此协商 PDU；测试里 pdu_length 由构造值表示协商结果。
        self.connect_args = (ip, rack, slot, port)


@pytest.mark.asyncio
async def test_connect_keeps_negotiated_pdu_above_240():
    """协商 PDU>240（如 480）时保留不改写，大块读可借高 PDU 减少分帧往返。"""
    fake = FakePduClient(pdu_length=480)
    with patch("snap7.client.Client", return_value=fake):
        driver = S7Driver()
        await driver.connect({"ip": "127.0.0.1", "rack": 0, "slot": 1})
    assert fake.pdu_length == 480
    assert driver.is_connected is True


@pytest.mark.asyncio
async def test_connect_keeps_negotiated_pdu_at_240():
    """协商 PDU=240 时同样保留协商值不改写。"""
    fake = FakePduClient(pdu_length=240)
    with patch("snap7.client.Client", return_value=fake):
        driver = S7Driver()
        await driver.connect({"ip": "127.0.0.1", "rack": 0, "slot": 1})
    assert fake.pdu_length == 240

@pytest.mark.asyncio
async def test_connect_applies_explicit_read_timeout_only_when_configured():
    from snap7.type import Parameter

    fake = FakePduClient(pdu_length=480)
    with patch("snap7.client.Client", return_value=fake):
        driver = S7Driver()
        await driver.connect(
            {"ip": "127.0.0.1", "rack": 0, "slot": 1, "read_timeout_ms": 850}
        )

    assert fake.param_calls == [
        (Parameter.SendTimeout, 850),
        (Parameter.RecvTimeout, 850),
    ]
