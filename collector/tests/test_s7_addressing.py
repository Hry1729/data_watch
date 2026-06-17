import pytest

from device.device import Device, Variable
from drivers.base import ReadBlock
from drivers.s7_driver import S7Driver
from collector.executor import TaskExecutor


def test_device_build_scan_tasks_does_not_merge_across_db_numbers():
    """不同 DB 号是独立内存区，绝不跨 DB 合并；每个 DB 各成自己的 BYTE 块。"""
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
    # 按 DB 号排序：1, 30, 50, 100；每个 DB 单点也合成 DBB BYTE 块
    assert [
        (block.address, block.count, block.data_type)
        for block in device.scan_tasks[0].read_blocks
    ] == [
        ("DB1.DBB0", 1, "BYTE"),
        ("DB30.DBB100", 4, "BYTE"),
        ("DB50.DBB20", 2, "BYTE"),
        ("DB100.DBB10", 1, "BYTE"),
    ]
    result_map = device.scan_tasks[0].result_map
    assert result_map["1"] == {
        "block_address": "DB1.DBB0",
        "byte_offset": 0,
        "bit": 0,
        "data_type": "BOOL",
    }
    assert result_map["4"] == {
        "block_address": "DB30.DBB100",
        "byte_offset": 0,
        "bit": None,
        "data_type": "REAL",
    }


def test_device_build_scan_tasks_merges_contiguous_db_points():
    """同 DB 内连续点合并成一个 BYTE 块；超过 max_gap 的空洞断成新块。"""
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1", "rack": 0, "slot": 1},
    )
    device.add_variable(Variable(variable_id="1", address="%DB1702.DBW0", data_type="INT"))
    device.add_variable(Variable(variable_id="2", address="%DB1702.DBB2", data_type="BYTE"))
    device.add_variable(Variable(variable_id="3", address="%DB1702.DBB3", data_type="BYTE"))
    device.add_variable(Variable(variable_id="4", address="%DB1702.DBD4", data_type="REAL"))
    device.add_variable(Variable(variable_id="5", address="%DB1702.DBX440.6", data_type="BOOL"))

    device.build_scan_tasks()

    scan_task = device.scan_tasks[0]
    # 0..8 连续合一块；440 与前段相隔 432 字节(>64)，断成独立块
    assert [
        (block.address, block.count, block.data_type) for block in scan_task.read_blocks
    ] == [
        ("DB1702.DBB0", 8, "BYTE"),
        ("DB1702.DBB440", 1, "BYTE"),
    ]
    assert scan_task.result_map["1"] == {
        "block_address": "DB1702.DBB0",
        "byte_offset": 0,
        "bit": None,
        "data_type": "INT",
    }
    assert scan_task.result_map["4"] == {
        "block_address": "DB1702.DBB0",
        "byte_offset": 4,
        "bit": None,
        "data_type": "REAL",
    }
    assert scan_task.result_map["5"] == {
        "block_address": "DB1702.DBB440",
        "byte_offset": 0,
        "bit": 6,
        "data_type": "BOOL",
    }


def test_device_build_scan_tasks_merges_contiguous_real_array():
    """典型场景：DB1702 内一片连续 REAL 数组合并为单块，块数从 N 降到 1。"""
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1", "rack": 0, "slot": 1},
    )
    for i in range(50):
        device.add_variable(
            Variable(variable_id=str(i), address=f"%DB1702.DBD{i * 4}", data_type="REAL")
        )

    device.build_scan_tasks()

    blocks = device.scan_tasks[0].read_blocks
    assert len(blocks) == 1
    assert (blocks[0].address, blocks[0].count, blocks[0].data_type) == (
        "DB1702.DBB0",
        200,
        "BYTE",
    )
    assert device.scan_tasks[0].result_map["49"]["byte_offset"] == 196


def test_device_keeps_width_mismatched_db_point_standalone():
    """访问宽度与数据类型不一致的 DB 点不并入合并块，保持单点读交驱动校验。"""
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1", "rack": 0, "slot": 1},
    )
    # DBW(2字节) 配 REAL(4字节) 属错配；DBD(4字节) 配 REAL 正常会被合并
    device.add_variable(Variable(variable_id="bad", address="DB1.DBW0", data_type="REAL"))
    device.add_variable(Variable(variable_id="ok", address="DB1.DBD4", data_type="REAL"))

    device.build_scan_tasks()

    addrs = [(b.address, b.count, b.data_type) for b in device.scan_tasks[0].read_blocks]
    # 错配点保持原始地址单点读；正常点合并为 BYTE 块
    assert ("DB1.DBW0", 1, "REAL") in addrs
    assert ("DB1.DBB4", 4, "BYTE") in addrs
    assert "bad" not in device.scan_tasks[0].result_map
    assert device.scan_tasks[0].result_map["ok"]["block_address"] == "DB1.DBB4"


def test_executor_extracts_db_values_from_merged_block():
    """合并块的字节经 executor 还原，结果与单点读取一致。"""
    # DBB0..7: INT(123) @0, BYTE @2, BYTE @3, REAL(10.0) @4
    raw = [0x00, 0x7B, 0x12, 0x34, 0x41, 0x20, 0x00, 0x00]

    assert TaskExecutor._extract_mapped_s7_value(
        raw, {"byte_offset": 0, "bit": None, "data_type": "INT"}
    ) == 123
    assert TaskExecutor._extract_mapped_s7_value(
        raw, {"byte_offset": 2, "bit": None, "data_type": "BYTE"}
    ) == 0x12
    assert TaskExecutor._extract_mapped_s7_value(
        raw, {"byte_offset": 4, "bit": None, "data_type": "REAL"}
    ) == pytest.approx(10.0)
    assert TaskExecutor._extract_mapped_s7_value(
        [0b01000000], {"byte_offset": 0, "bit": 6, "data_type": "BOOL"}
    ) is True


def test_device_build_scan_tasks_merges_contiguous_s7_area_bytes():
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1", "rack": 0, "slot": 1},
    )
    device.add_variable(Variable(variable_id="1", address="%I101.0", data_type="BOOL"))
    device.add_variable(Variable(variable_id="2", address="%I101.1", data_type="BOOL"))
    device.add_variable(Variable(variable_id="3", address="%IB102", data_type="BYTE"))
    device.add_variable(Variable(variable_id="4", address="%Q101.0", data_type="BOOL"))

    device.build_scan_tasks()

    scan_task = device.scan_tasks[0]
    assert [(block.address, block.count, block.data_type) for block in scan_task.read_blocks] == [
        ("%IB101", 2, "BYTE"),
        ("%QB101", 1, "BYTE"),
    ]
    assert scan_task.result_map["1"] == {
        "block_address": "%IB101",
        "byte_offset": 0,
        "bit": 0,
        "data_type": "BOOL",
    }
    assert scan_task.result_map["2"] == {
        "block_address": "%IB101",
        "byte_offset": 0,
        "bit": 1,
        "data_type": "BOOL",
    }
    assert scan_task.result_map["3"] == {
        "block_address": "%IB101",
        "byte_offset": 1,
        "bit": None,
        "data_type": "BYTE",
    }


def test_executor_extracts_values_from_merged_s7_area_byte_block():
    raw = [0b00000010, 0x2C, 0x00, 0x2B]

    assert TaskExecutor._extract_mapped_s7_value(
        raw,
        {"byte_offset": 0, "bit": 1, "data_type": "BOOL"},
    ) is True
    assert TaskExecutor._extract_mapped_s7_value(
        raw,
        {"byte_offset": 1, "bit": None, "data_type": "BYTE"},
    ) == 44
    assert TaskExecutor._extract_mapped_s7_value(
        raw,
        {"byte_offset": 2, "bit": None, "data_type": "INT"},
    ) == 43


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


def test_s7_driver_reads_percent_prefixed_db_formats():
    calls = []

    class FakeClient:
        def db_read(self, db_number, start, size):
            calls.append((db_number, start, size))
            mapping = {
                (1702, 0, 2): bytearray([0x00, 0x7B]),
                (1702, 2, 1): bytearray([0x12]),
                (1702, 4, 4): bytearray([0x41, 0x20, 0x00, 0x00]),
                (1702, 440, 1): bytearray([0b01000000]),
            }
            return mapping[(db_number, start, size)]

    driver = S7Driver()
    driver._client = FakeClient()
    driver._connected = True

    assert driver._read_single_block(ReadBlock("%DB1702.DBW0", data_type="INT")) == [123]
    assert driver._read_single_block(ReadBlock("%DB1702.DBB2", data_type="BYTE")) == [0x12]
    result = driver._read_single_block(ReadBlock("%DB1702.DBD4", data_type="REAL"))
    assert result[0] == pytest.approx(10.0)
    assert driver._read_single_block(ReadBlock("%DB1702.DBX440.6", data_type="BOOL")) == [True]
    assert calls == [
        (1702, 0, 2),
        (1702, 2, 1),
        (1702, 4, 4),
        (1702, 440, 1),
    ]


# ==================== Timer/Counter Addressing Tests ====================

def test_device_parses_timer_address():
    """设备解析 %T 地址"""
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1"},
    )
    parsed = device._parse_s7_address("%T10")
    assert parsed["area"] == "T"
    assert parsed["num"] == 10


def test_device_parses_counter_address():
    """设备解析 %C 地址"""
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1"},
    )
    parsed = device._parse_s7_address("%C5")
    assert parsed["area"] == "C"
    assert parsed["num"] == 5


def test_device_build_scan_tasks_handles_timer_counter():
    """Timer/Counter 按连续编号合并，不与 I/Q/M 混合。"""
    device = Device(
        device_id="qc100",
        device_type="QC",
        protocol="s7",
        connection_config={"ip": "127.0.0.1"},
    )
    device.add_variable(Variable(variable_id="t1", address="%T1", data_type="TIMER_MS"))
    device.add_variable(Variable(variable_id="t2", address="%T2", data_type="TIMER_MS"))
    device.add_variable(Variable(variable_id="c1", address="%C1", data_type="COUNTER"))
    device.add_variable(Variable(variable_id="c2", address="%C2", data_type="COUNTER"))
    device.add_variable(Variable(variable_id="i1", address="%I101.0", data_type="BOOL"))

    device.build_scan_tasks()

    scan_task = device.scan_tasks[0]
    blocks = {(block.address, block.count, block.data_type) for block in scan_task.read_blocks}
    assert ("%T1", 2, "TIMER_MS") in blocks
    assert ("%C1", 2, "COUNTER") in blocks
    assert ("%IB101", 1, "BYTE") in blocks
    assert scan_task.result_map["t1"]["value_offset"] == 0
    assert scan_task.result_map["t2"]["value_offset"] == 1
    assert scan_task.result_map["c1"]["value_offset"] == 0
    assert scan_task.result_map["c2"]["value_offset"] == 1


def test_executor_extracts_values_from_merged_timer_counter_block():
    assert TaskExecutor._extract_mapped_s7_value(
        [500, 12300],
        {"value_offset": 1, "data_type": "TIMER_MS"},
    ) == 12300
