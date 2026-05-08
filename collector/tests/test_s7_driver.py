"""S7驱动测试"""

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
        mapping = {
            (0x81, 0, 101, 1): bytearray([0b00000001]),
            (0x82, 0, 101, 1): bytearray([0b00000010]),
            (0x83, 0, 101, 1): bytearray([0b00000100]),
            (0x83, 0, 1304, 4): bytearray([0x00, 0x00, 0x00, 0x2A]),
            (0x83, 0, 1306, 2): bytearray([0x00, 0x2B]),
            (0x83, 0, 1308, 1): bytearray([0x2C]),
        }
        return mapping[(area, db_number, start, size)]

    def get_connected(self):
        return self.connected

    def plc_run(self):
        self.plc_run_called = True


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
