import pytest

from device.device import Device, Variable
from drivers.base import ReadBlock
from drivers.s7_driver import S7Driver


def test_device_build_scan_tasks_preserves_standard_db_addresses():
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1", "rack": 0, "slot": 1},
    )
    device.add_variable(Variable(variable_id="1", address="DB1.DBX0.0", data_type="BOOL"))
    device.add_variable(Variable(variable_id="2", address="DB100.DBB10", data_type="BYTE"))
    device.add_variable(Variable(variable_id="3", address="DB50.DBW20", data_type="INT"))
    device.add_variable(Variable(variable_id="4", address="DB30.DBD100", data_type="REAL"))

    device.build_scan_tasks()

    assert len(device.scan_tasks) == 1
    assert [block.address for block in device.scan_tasks[0].read_blocks] == [
        "DB1.DBX0.0",
        "DB100.DBB10",
        "DB50.DBW20",
        "DB30.DBD100",
    ]


def test_device_rejects_odd_dbw_address():
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1", "rack": 0, "slot": 1},
    )

    with pytest.raises(Exception, match="DBW"):
        device._parse_address("DB1.DBW1")


def test_device_rejects_ambiguous_s7_area_address():
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1", "rack": 0, "slot": 1},
    )

    with pytest.raises(Exception, match="bit suffix or B/W/D"):
        device._parse_address("%I101")


def test_s7_driver_reads_standard_db_formats():
    calls = []

    class FakeClient:
        def db_read(self, db_number, start, size):
            calls.append((db_number, start, size))
            mapping = {
                (1, 0, 1): bytearray([0b00000001]),
                (100, 10, 1): bytearray([0x12]),
                (50, 20, 2): bytearray([0x00, 0x7B]),
                (30, 100, 4): bytearray([0x41, 0x20, 0x00, 0x00]),
            }
            return mapping[(db_number, start, size)]

    driver = S7Driver()
    driver._client = FakeClient()
    driver._connected = True

    assert driver._read_single_block(ReadBlock("DB1.DBX0.0", data_type="BOOL")) == [True]
    assert driver._read_single_block(ReadBlock("DB100.DBB10", data_type="BYTE")) == [0x12]
    assert driver._read_single_block(ReadBlock("DB50.DBW20", data_type="INT")) == [123]
    result = driver._read_single_block(ReadBlock("DB30.DBD100", data_type="REAL"))
    assert result[0] == pytest.approx(10.0)
    assert calls == [
        (1, 0, 1),
        (100, 10, 1),
        (50, 20, 2),
        (30, 100, 4),
    ]
