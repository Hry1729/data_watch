"""
GE SRTP 地址解析与批量合并测试。

测试覆盖：
1. 地址解析：%R/%AI/%AQ/%I/%Q/%M
2. 合并：连续地址、空洞分隔、超限拆块、不同区域/周期不合并
3. 单点还原：INT/WORD/DINT/REAL/BOOL
4. 集成：fake driver 返回批量值，executor 还原正确
"""

import struct
import math
from unittest.mock import patch

import pytest

from device.device import Variable
from tests.package_support import PackageDevice as Device
from packages.ge_srtp.ge_srtp.planner import Decoder, Planner
from drivers.base import ReadBlock
from collector.bounded_executor import ExecutorSaturatedError
from collector.executor import TaskExecutor


class TestGEAddressParsing:
    """测试 GE 地址解析"""

    def test_parse_r_register_with_zero_padding(self):
        """测试 %R 地址解析（零填充格式）"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        parsed = device._parse_ge_address("%R00001")
        assert parsed["area"] == "R"
        assert parsed["logical_number"] == 1
        assert parsed["protocol_offset"] == 0  # logical_number - 1

    def test_parse_r_register_without_padding(self):
        """测试 %R 地址解析（无填充格式）"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        parsed = device._parse_ge_address("%R100")
        assert parsed["area"] == "R"
        assert parsed["logical_number"] == 100
        assert parsed["protocol_offset"] == 99

    def test_parse_ai_register(self):
        """测试 %AI 地址解析"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        parsed = device._parse_ge_address("%AI0001")
        assert parsed["area"] == "AI"
        assert parsed["logical_number"] == 1
        assert parsed["protocol_offset"] == 0

    def test_parse_aq_register(self):
        """测试 %AQ 地址解析"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        parsed = device._parse_ge_address("%AQ0021")
        assert parsed["area"] == "AQ"
        assert parsed["logical_number"] == 21
        assert parsed["protocol_offset"] == 20

    def test_parse_discrete_input(self):
        """测试 %I 地址解析（离散量）"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        parsed = device._parse_ge_address("%I00001")
        assert parsed["area"] == "I"
        assert parsed["logical_number"] == 1
        assert parsed["protocol_offset"] == 0  # 全区域一律 -1（%I1 = bit 0）

    def test_parse_discrete_output(self):
        """测试 %Q 地址解析（离散量）"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        parsed = device._parse_ge_address("%Q00001")
        assert parsed["area"] == "Q"
        assert parsed["logical_number"] == 1
        assert parsed["protocol_offset"] == 0

    def test_parse_discrete_internal(self):
        """测试 %M 地址解析（离散量）"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        parsed = device._parse_ge_address("%M00201")
        assert parsed["area"] == "M"
        assert parsed["logical_number"] == 201
        assert parsed["protocol_offset"] == 200

    @pytest.mark.parametrize(
        ("address", "area", "logical_number"),
        [
            ("%T00001", "T", 1),
            ("%S00002", "S", 2),
            ("%SA00003", "SA", 3),
            ("%SB00004", "SB", 4),
            ("%SC00005", "SC", 5),
            ("%G00006", "G", 6),
        ],
    )
    def test_parse_additional_discrete_memory(
        self, address, area, logical_number
    ):
        """新增离散内存区沿用 GE 1 基逻辑地址。"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )

        parsed = device._parse_ge_address(address)

        assert parsed["area"] == area
        assert parsed["logical_number"] == logical_number
        assert parsed["protocol_offset"] == logical_number - 1
    def test_invalid_ge_address(self):
        """测试无效 GE 地址"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        with pytest.raises(Exception):
            device._parse_ge_address("R100")  # 缺少 % 前缀

        with pytest.raises(Exception):
            device._parse_ge_address("%X100")  # 无效区域

    @pytest.mark.parametrize(
        "address",
        [
            "%R0", "%AI0", "%AQ0", "%I0", "%Q0", "%M0",
            "%T0", "%S0", "%SA0", "%SB0", "%SC0", "%G0",
        ],
    )
    def test_ge_address_zero_is_invalid(self, address):
        """GE 逻辑地址 0 无效"""
        device = Device(
            device_id="test",
            device_type="PLC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        with pytest.raises(Exception, match="must be >= 1"):
            device._parse_ge_address(address)


class TestGEBlockMerging:
    """测试 GE 块合并"""

    def test_merge_contiguous_r_registers(self):
        """连续 %R 地址合并成 1 块"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1", "port": 18245},
        )
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%R101", data_type="INT"))
        device.add_variable(Variable(variable_id="v3", address="%R102", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        # 块地址应为 PLC 逻辑地址字符串
        assert scan_task.read_blocks[0].address == "%R00099"
        assert scan_task.read_blocks[0].count == 4
        assert scan_task.result_map["v1"]["word_offset"] == 1
        assert scan_task.result_map["v2"]["word_offset"] == 2
        assert scan_task.result_map["v3"]["word_offset"] == 3

    def test_merge_across_gap_within_limit(self):
        """跨空洞合并：gap 与总跨度均在上限内 -> 合并为 1 块，弃读空洞数据"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %R100 -> 偏移量 99, %R200 -> 偏移量 199
        # gap = 199 - 100 = 99 <= 125, span = 200 - 99 = 101 <= 125 -> 合并
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%R200", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%R00100"
        assert scan_task.read_blocks[0].count == 101
        # 相对偏移正确
        assert scan_task.result_map["v1"]["word_offset"] == 0
        assert scan_task.result_map["v2"]["word_offset"] == 100

    def test_merge_ai_with_real_type(self):
        """%AI REAL 类型占 2 words，与后续 INT 可合并"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%AI1", data_type="REAL"))
        device.add_variable(Variable(variable_id="v2", address="%AI3", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        # REAL 占 2 words (%AI1 -> offset 0,1)，INT 占 1 word (%AI3 -> offset 2)
        # 应该合并成一个块（offset 0-2）
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].count == 3

        # 验证 result_map
        assert scan_task.result_map["v1"]["word_offset"] == 0
        assert scan_task.result_map["v1"]["word_length"] == 2
        assert scan_task.result_map["v2"]["word_offset"] == 2

    def test_split_block_exceeding_max_words(self):
        """超过 125 words 自动拆块"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # 添加 130 个连续 INT 变量（每个 1 word）
        for i in range(130):
            device.add_variable(
                Variable(variable_id=f"v{i}", address=f"%R{i+1}", data_type="INT")
            )

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        # 应该拆成 2 块
        assert len(scan_task.read_blocks) == 2
        assert scan_task.read_blocks[0].count == 125
        assert scan_task.read_blocks[1].count == 5

    def test_separate_blocks_for_different_areas(self):
        """不同区域不合并"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%AI1", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 2  # 不同区域分开

    def test_separate_tasks_for_different_scan_rates(self):
        """不同 scan_rate 分成不同任务"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="INT", scan_rate=1.0))
        device.add_variable(Variable(variable_id="v2", address="%R101", data_type="INT", scan_rate=5.0))

        device.build_scan_tasks()

        assert len(device.scan_tasks) == 2

    def test_merge_discrete_bools(self):
        """离散量 BOOL 合并"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I2", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v3", address="%I3", data_type="BOOL"))

        device.build_scan_tasks()

        # byte 模式：%I1~3 -> bits 0..2 落在 byte 0 -> 1 块 1 字节，块首=逻辑 %I1
        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].data_type == "BYTE"
        assert scan_task.read_blocks[0].address == "%I00001"
        assert scan_task.read_blocks[0].count == 1

        # 验证 result_map（bit_offset 相对块首位）
        assert scan_task.result_map["v1"]["bit_offset"] == 0
        assert scan_task.result_map["v2"]["bit_offset"] == 1
        assert scan_task.result_map["v3"]["bit_offset"] == 2
        assert scan_task.result_map["v1"]["packing"] == "byte"

    @pytest.mark.parametrize(
        "area",
        ["T", "S", "SA", "SB", "SC", "G"],
    )
    def test_merge_additional_discrete_memory_bools(self, area):
        """新增离散内存区也应合并为 byte 模式只读块。"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address=f"%{area}1", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address=f"%{area}2", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        block = scan_task.read_blocks[0]
        assert block.address == f"%{area}00001"
        assert block.count == 1
        assert block.data_type == "BYTE"
        assert scan_task.result_map["v1"]["bit_offset"] == 0
        assert scan_task.result_map["v2"]["bit_offset"] == 1
        assert scan_task.result_map["v1"]["packing"] == "byte"

    def test_split_register_when_span_exceeds_max_words(self):
        """跨空洞时总跨度超过 125 words -> 正确拆块（gap 在限内但 span 超限）"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %R1 -> 偏移量 0, %R127 -> 偏移量 126
        # gap = 126 - 1 = 125 <= 125，但 span = 127 - 0 = 127 > 125 -> 拆块
        device.add_variable(Variable(variable_id="v1", address="%R1", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%R127", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 2

    def test_merge_discrete_bools_across_gap(self):
        """离散区跨空洞合并：gap 与跨度均在上限内 -> 1 块，bit_offset 正确"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # 逻辑地址 %I1/%I10 -> 0基协议偏移 0/9（全区域一律 -1）
        # byte 模式：bit 0 -> byte 0，bit 9 -> byte 1（end 10 -> byte_end 2）-> 1 块 2 字节
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I10", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].data_type == "BYTE"
        assert scan_task.read_blocks[0].count == 2
        assert scan_task.result_map["v1"]["bit_offset"] == 0
        assert scan_task.result_map["v2"]["bit_offset"] == 9

    def test_gap_data_excluded_from_result_map(self):
        """空洞数据不进 result_map：条目数等于真实变量数"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # 3 个点跨两个空洞合并为 1 块，result_map 只含这 3 个变量
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%R150", data_type="INT"))
        device.add_variable(Variable(variable_id="v3", address="%R200", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        # 空洞（%R101..149, %R151..199）不进 result_map
        assert len(scan_task.result_map) == 3
        assert set(scan_task.result_map.keys()) == {"v1", "v2", "v3"}
        assert scan_task.result_map["v1"]["word_offset"] == 0
        assert scan_task.result_map["v2"]["word_offset"] == 50
        assert scan_task.result_map["v3"]["word_offset"] == 100

    def test_merge_register_span_exactly_max_words(self):
        """span 恰好等于 125 words -> 仍合并为 1 块（边界含）"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %R1 -> offset 0, %R125 -> offset 124, span = 125 - 0 = 125 <= 125 -> 合并
        device.add_variable(Variable(variable_id="v1", address="%R1", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%R125", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%R00001"
        assert scan_task.read_blocks[0].count == 125
        assert scan_task.result_map["v1"]["word_offset"] == 0
        assert scan_task.result_map["v2"]["word_offset"] == 124

    def test_merge_discrete_span_exactly_max_bytes(self):
        """离散区 span 恰好等于 250 字节 -> 仍合并为 1 块（边界含）"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %I1 -> 位 0 -> 字节 0；%I2000 -> 位 1999（结束 2000）-> 字节结束 250
        # span = 250 - 0 = 250 <= 250 -> 合并
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I2000", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].count == 250
        assert scan_task.result_map["v1"]["bit_offset"] == 0
        assert scan_task.result_map["v2"]["bit_offset"] == 1999

    def test_split_discrete_when_span_exceeds_max_bytes(self):
        """离散区 span 超过 250 字节 -> 正确拆块"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %I1 -> 字节 0；%I2001 -> 位 2000（结束 2001）-> 字节结束 251
        # span = 251 > 250 -> 拆块
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I2001", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 2

    def test_discrete_block_byte_aligned_nonzero_base(self):
        """块首位按字节界对齐：非零字节基址时地址与 bit_offset 正确"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %I9 -> bit 8 -> byte 1（块首位 bit 8=逻辑 %I9）；%I72 -> bit 71 -> byte 8（end 72 -> byte_end 9）
        # count = 9 - 1 = 8 >= 8 不触发低扩，保持非零基址
        device.add_variable(Variable(variable_id="v1", address="%I9", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I72", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%I00009"
        assert scan_task.read_blocks[0].count == 8
        assert scan_task.result_map["v1"]["bit_offset"] == 0
        assert scan_task.result_map["v2"]["bit_offset"] == 63

    def test_discrete_byte_boundary_offsets(self):
        """字节边界 0/1 基防混淆：%I7/%I8/%I15/%I16 的块界与 bit_offset"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %I7/8/15/16 -> 位 6,7,14,15：byte_start = 0，byte_end = ceil(16/8) = 2
        # -> 1 块 2 字节，块首=逻辑 %I1；%I8=byte0 MSB位、%I9(若有)=byte1 LSB位
        device.add_variable(Variable(variable_id="v7", address="%I7", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v8", address="%I8", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v15", address="%I15", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v16", address="%I16", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%I00001"
        assert scan_task.read_blocks[0].count == 2
        assert scan_task.result_map["v7"]["bit_offset"] == 6
        assert scan_task.result_map["v8"]["bit_offset"] == 7
        assert scan_task.result_map["v15"]["bit_offset"] == 14
        assert scan_task.result_map["v16"]["bit_offset"] == 15

    def test_merge_overlapping_real_and_int(self):
        """重叠 item：INT 落在 REAL 的第二个 word 上 -> 仍 1 块且偏移正确"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %R100 REAL -> offset 99..101（占 2 words），%R101 INT -> offset 100..101
        # start(100) - current_end(101) = -1（重叠）-> 合并，块不缩短
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="REAL"))
        device.add_variable(Variable(variable_id="v2", address="%R101", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%R00098"
        assert scan_task.read_blocks[0].count == 4
        assert scan_task.result_map["v1"]["word_offset"] == 2
        assert scan_task.result_map["v1"]["word_length"] == 2
        assert scan_task.result_map["v2"]["word_offset"] == 3

    def test_accumulate_then_split_starts_new_block_at_third_point(self):
        """连续累积后遇 span 超限 -> 第二块从触发拆分的点开始"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %R1(offset 0) 与 %R50(offset 49) 合并（span 50）；
        # %R127(offset 126)：gap = 126 - 50 = 76 <= 125 但 span = 127 > 125 -> 拆块
        device.add_variable(Variable(variable_id="v1", address="%R1", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%R50", data_type="INT"))
        device.add_variable(Variable(variable_id="v3", address="%R127", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 2
        assert scan_task.read_blocks[0].address == "%R00001"
        assert scan_task.read_blocks[0].count == 50
        assert scan_task.read_blocks[1].address == "%R00124"
        assert scan_task.read_blocks[1].count == 4
        assert scan_task.result_map["v3"]["word_offset"] == 3
        assert scan_task.result_map["v3"]["block_address"] == "%R00124"

    def test_discrete_high_small_block_extended_downward(self):
        """区域顶部离散小块向低地址扩齐，避免驱动垫长越过区域顶。"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%Q2019", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%Q2026", data_type="BOOL"))

        device.build_scan_tasks()

        # %Q2019/%Q2026 -> bits 2018/2025 -> bytes 252..254(count 2) -> 低扩至 246
        # 块首位 bit 1968 = 逻辑 %Q1969
        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%Q01969"
        assert scan_task.read_blocks[0].count == 8
        assert scan_task.result_map["v1"]["bit_offset"] == 50
        assert scan_task.result_map["v2"]["bit_offset"] == 57

    def test_register_small_block_extended_downward(self):
        """寄存器小块向低地址扩齐，避免驱动 max(count,4) 向高地址垫长。"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%AI285", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%AI0282"
        assert scan_task.read_blocks[0].count == 4
        assert scan_task.result_map["v1"]["word_offset"] == 3

    def test_low_end_small_block_kept(self):
        """贴近区域底的小块保持原块首，由驱动在低端垫长，安全。"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%I00001"
        assert scan_task.read_blocks[0].count == 1
        assert scan_task.result_map["v1"]["bit_offset"] == 0

    def test_discrete_non_bool_raises_error(self):
        """离散区非 BOOL 类型报错（第一版限制）"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="INT"))

        with pytest.raises(Exception, match="only supports BOOL"):
            device.build_scan_tasks()


class TestGEValueExtraction:
    """测试从合并块提取值"""

    def test_extract_int_from_register_block(self):
        """从寄存器块提取 INT"""
        raw = [100, 200, 300]
        mapping = {
            "area": "R",
            "word_offset": 1,
            "data_type": "INT",
        }
        assert Decoder._extract_mapped_ge_value(raw, mapping) == 200

    def test_extract_negative_int_from_register_block(self):
        """从寄存器块提取负数 INT（gesrtp-py 已返回 signed）"""
        raw = [-1]
        mapping = {
            "area": "R",
            "word_offset": 0,
            "data_type": "INT",
        }
        assert Decoder._extract_mapped_ge_value(raw, mapping) == -1

    def test_extract_int_from_unsigned_input(self):
        """unsigned 输入兼容转换为 signed INT"""
        raw = [0xFFFF]  # unsigned 65535
        mapping = {
            "area": "R",
            "word_offset": 0,
            "data_type": "INT",
        }
        assert Decoder._extract_mapped_ge_value(raw, mapping) == -1  # signed

    def test_extract_word_from_register_block(self):
        """从寄存器块提取 WORD（无符号）"""
        raw = [100, 65535, 300]
        mapping = {
            "area": "R",
            "word_offset": 1,
            "data_type": "WORD",
        }
        assert Decoder._extract_mapped_ge_value(raw, mapping) == 65535

    def test_unsupported_word_order_raises_error(self):
        """不支持 word_order 时抛错"""
        raw = [0, 0x0001, 0x0000]
        mapping = {
            "area": "R",
            "word_offset": 1,
            "word_length": 2,
            "data_type": "DINT",
            "word_order": "high_low",  # 不支持
        }
        with pytest.raises(ValueError, match="Unsupported GE word_order"):
            Decoder._extract_mapped_ge_value(raw, mapping)

    def test_extract_2_word_type_requires_enough_words(self):
        """2-word 类型 word 不足时抛错，避免静默返回低 word。"""
        raw = [100]
        mapping = {
            "area": "R",
            "word_offset": 0,
            "word_length": 2,
            "data_type": "DINT",
        }
        with pytest.raises(ValueError, match="GE 2-word type DINT needs words"):
            Decoder._extract_mapped_ge_value(raw, mapping)

    def test_extract_dint_from_register_block(self):
        """从寄存器块提取 DINT（2 words 组合）"""
        raw = [0, 0x0001, 0x0000]  # low=1, high=0 -> DINT=1
        mapping = {
            "area": "R",
            "word_offset": 1,
            "word_length": 2,
            "data_type": "DINT",
        }
        assert Decoder._extract_mapped_ge_value(raw, mapping) == 1

        # 负数测试
        raw2 = [0, 0xFFFF, 0xFFFF]  # low=65535, high=65535 -> DINT=-1
        mapping2 = {
            "area": "R",
            "word_offset": 1,
            "word_length": 2,
            "data_type": "DINT",
        }
        assert Decoder._extract_mapped_ge_value(raw2, mapping2) == -1

    def test_extract_real_from_register_block(self):
        """从寄存器块提取 REAL（2 words 组合）"""
        # 浮点数 10.5 的 IEEE 754 表示
        # 0x41280000 = 10.5（浮点数）
        # 低字 = 0x0000, 高字 = 0x4128
        raw_bytes = struct.pack('<f', 10.5)
        low = struct.unpack('<H', raw_bytes[:2])[0]
        high = struct.unpack('<H', raw_bytes[2:])[0]
        raw = [0, low, high]

        mapping = {
            "area": "R",
            "word_offset": 1,
            "word_length": 2,
            "data_type": "REAL",
        }
        result = Decoder._extract_mapped_ge_value(raw, mapping)
        assert abs(result - 10.5) < 0.01

    def test_log_ge_nonfinite_value_reports_raw_words_and_word_order_candidates(self):
        """GE REAL 解出 NaN 时记录原始 word 与两种字序候选，便于现场判断字序。"""
        raw = [0x0000, 0x7FC0]  # low_high -> IEEE 754 NaN
        mapping = {
            "area": "R",
            "block_address": "%R00100",
            "word_offset": 0,
            "word_length": 2,
            "data_type": "REAL",
        }
        variable = Variable(variable_id="v1", address="%R100", data_type="REAL")
        value = Decoder._extract_mapped_ge_value(raw, mapping)

        assert math.isnan(value)
        with patch("packages.ge_srtp.ge_srtp.planner.logger.warning") as warning:
            Decoder._log_ge_nonfinite_value(
                "qc100", variable, raw, mapping, value
            )

        warning.assert_called_once()
        args = warning.call_args.args
        assert "GE REAL/FLOAT decoded non-finite" in args[0]
        assert args[1] == "qc100"
        assert args[2] == "v1"
        assert args[3] == "%R100"
        assert args[4] == "REAL"
        assert args[5] == "%R00100"
        assert args[6] == 0
        assert args[7] == 0x0000
        assert args[8] == 0x7FC0
        assert math.isnan(args[9])
        assert math.isfinite(args[10])

    def test_extract_bool_from_discrete_block(self):
        """从离散块提取 BOOL"""
        raw = [True, False, True, False]
        mapping = {
            "area": "I",
            "bit_offset": 2,
            "data_type": "BOOL",
        }
        assert Decoder._extract_mapped_ge_value(raw, mapping) is True

        mapping2 = {
            "area": "I",
            "bit_offset": 1,
            "data_type": "BOOL",
        }
        assert Decoder._extract_mapped_ge_value(raw, mapping2) is False

    def test_extract_bool_from_discrete_byte_block(self):
        """从 byte 模式离散块提取 BOOL（LSB-first 拆位）"""
        # byte0 = 0b0000_0010（bit 1 置位），byte1 = 0b0000_0001（bit 8 置位）
        raw = [0x02, 0x01]

        def mapping(bit):
            return {"area": "I", "bit_offset": bit, "data_type": "BOOL", "packing": "byte"}

        assert Decoder._extract_mapped_ge_value(raw, mapping(1)) is True
        assert Decoder._extract_mapped_ge_value(raw, mapping(0)) is False
        assert Decoder._extract_mapped_ge_value(raw, mapping(8)) is True
        assert Decoder._extract_mapped_ge_value(raw, mapping(9)) is False

    def test_extract_bool_from_single_int_byte_block(self):
        """count=1 时 driver 返回单 int 的退化：归一化后仍可拆位"""
        mapping = {"area": "I", "bit_offset": 1, "data_type": "BOOL", "packing": "byte"}
        assert Decoder._extract_mapped_ge_value(0x02, mapping) is True

    @pytest.mark.parametrize("area", ["T", "S", "SA", "SB", "SC", "G"])
    def test_extract_bool_from_additional_discrete_memory(self, area):
        """新增离散区复用 byte 模式的 LSB-first 位解码。"""
        mapping = {
            "area": area,
            "bit_offset": 1,
            "data_type": "BOOL",
            "packing": "byte",
        }
        assert Decoder._extract_mapped_ge_value([0x02], mapping) is True

    def test_extract_byte_packed_out_of_range_raises(self):
        """bit_offset 超出块字节数 -> 带上下文的 ValueError 而非裸 IndexError"""
        mapping = {
            "area": "I",
            "bit_offset": 16,
            "data_type": "BOOL",
            "packing": "byte",
            "block_address": "%I00001",
        }
        with pytest.raises(ValueError, match="out of range"):
            Decoder._extract_mapped_ge_value([0x00, 0x00], mapping)


class _FakeGeDriver:
    """记录调用参数的假 GE 驱动（byte/bit 模式路径测试用）"""

    def __init__(self):
        self.calls = []

    def read_discrete_input(self, offset, count, mode='bit'):
        self.calls.append(("I", offset, count, mode))
        return [0] * count

    def read_discrete_output(self, offset, count, mode='bit'):
        self.calls.append(("Q", offset, count, mode))
        return [0] * count

    def read_discrete_internal(self, offset, count, mode='bit'):
        self.calls.append(("M", offset, count, mode))
        return [0] * count
    def read_discrete_temp(self, offset, count, mode='bit'):
        self.calls.append(("T", offset, count, mode))
        return [0] * count

    def read_system_memory(self, mem_type, offset, count, mode='bit'):
        self.calls.append((mem_type, offset, count, mode))
        return [0] * count

    def read_global_memory(self, offset, count, mode='bit'):
        self.calls.append(("G", offset, count, mode))
        return [0] * count



class TestGEDriverByteMode:
    """driver 适配层：BYTE 离散块走 byte 模式（地址 //8），BIT 块保留 bit 路径"""

    def _make_driver(self):
        from packages.ge_srtp.ge_srtp.driver import GESRTPDriver
        drv = GESRTPDriver()
        drv._driver = _FakeGeDriver()
        drv._connected = True
        return drv

    def test_byte_block_offset_zero(self):
        """%I00001（%I1 所在块，bit 0）-> byte_offset 0，客户端全链路合法"""
        drv = self._make_driver()
        drv._read_block(ReadBlock("%I00001", count=2, data_type="BYTE"))
        assert drv._driver.calls == [("I", 0, 2, "byte")]

    def test_byte_block_nonzero_base_q_area(self):
        drv = self._make_driver()
        drv._read_block(ReadBlock("%Q00009", count=1, data_type="BYTE"))
        assert drv._driver.calls == [("Q", 1, 1, "byte")]

    def test_byte_block_m_area(self):
        drv = self._make_driver()
        drv._read_block(ReadBlock("%M00017", count=3, data_type="BYTE"))
        assert drv._driver.calls == [("M", 2, 3, "byte")]

    @pytest.mark.parametrize("area", ["T", "S", "SA", "SB", "SC", "G"])
    def test_additional_discrete_memory_uses_read_only_byte_api(self, area):
        """新增离散区必须路由到对应 read_* 接口，且按字节偏移读取。"""
        drv = self._make_driver()
        drv._read_block(ReadBlock(f"%{area}00009", count=2, data_type="BYTE"))
        assert drv._driver.calls == [(area, 1, 2, "byte")]

    def test_byte_block_not_aligned_raises(self):
        from core.exceptions import ReadError
        drv = self._make_driver()
        with pytest.raises(ReadError, match="not byte-aligned"):
            drv._read_block(ReadBlock("%I00004", count=1, data_type="BYTE"))

    def test_bit_block_keeps_legacy_path(self):
        drv = self._make_driver()
        drv._read_block(ReadBlock("%I00001", count=10, data_type="BIT"))
        assert drv._driver.calls == [("I", 0, 10, "bit")]


class _RejectingExecutor:
    def submit(self, *_args, **_kwargs):
        raise ExecutorSaturatedError("plc_read executor capacity reached: 1")


@pytest.mark.asyncio
async def test_ge_read_propagates_executor_saturation():
    """读池背压必须交给 TaskExecutor 处理，不能退化为普通块失败。"""
    from packages.ge_srtp.ge_srtp.driver import GESRTPDriver

    drv = GESRTPDriver(read_executor=_RejectingExecutor())
    drv._driver = _FakeGeDriver()
    drv._connected = True

    with pytest.raises(ExecutorSaturatedError):
        await drv.read([ReadBlock("%R00001", count=1, data_type="WORD")])


@pytest.mark.asyncio
async def test_ge_read_aborts_remaining_blocks_when_connection_is_lost():
    """致命连接错误后不得继续读取同批次后续 Block。"""
    from packages.ge_srtp.ge_srtp.driver import GESRTPDriver
    from packages.ge_srtp.ge_srtp.transport import exceptions as ge_exceptions

    class FatalGeDriver:
        def __init__(self):
            self.calls = 0

        def read_register(self, _offset, _count):
            self.calls += 1
            raise ge_exceptions.TimeoutError("connection lost")

        def is_connected(self):
            return False

    fatal = FatalGeDriver()
    drv = GESRTPDriver()
    drv._driver = fatal
    drv._connected = True

    blocks = [
        ReadBlock("%R00001", count=1, data_type="WORD"),
        ReadBlock("%R00002", count=1, data_type="WORD"),
    ]
    with pytest.raises(ge_exceptions.TimeoutError):
        await drv.read(blocks)

    assert fatal.calls == 1
    assert drv.is_connected is False


@pytest.mark.asyncio
async def test_ge_read_records_block_and_batch_metrics():
    """GE 指标应记录请求数量、区域、字节量、RTT 与批次结果。"""
    from packages.ge_srtp.ge_srtp.driver import GESRTPDriver
    from monitor.performance import PerformanceMetrics

    class MetricsGeDriver(_FakeGeDriver):
        def read_register(self, _offset, count):
            self.calls.append(("R", _offset, count))
            return [0] * count

        def is_connected(self):
            return True

    metrics = PerformanceMetrics()
    drv = GESRTPDriver(metrics=metrics)
    drv._driver = MetricsGeDriver()
    drv._connected = True

    result = await drv.read([
        ReadBlock("%R00001", count=2, data_type="WORD"),
        ReadBlock("%I00001", count=3, data_type="BYTE"),
    ])

    snapshot = metrics.snapshot()
    assert set(result) == {"%R00001", "%I00001"}
    assert snapshot["timings"]["ge_srtp_block_rtt_ms"]["count"] == 2
    assert snapshot["timings"]["ge_srtp_batch_ms"]["count"] == 1
    assert snapshot["counters"]["ge_srtp_block_success_count"] == 2
    assert snapshot["counters"]["ge_srtp_batch_success_count"] == 1
    assert snapshot["gauges"]["ge_srtp_last_batch_block_count"] == 2
    assert snapshot["gauges"]["ge_srtp_last_batch_payload_bytes"] == 7
    assert snapshot["gauges"]["ge_srtp_last_batch_blocks_r"] == 1
    assert snapshot["gauges"]["ge_srtp_last_batch_blocks_i"] == 1



class TestGEIntegration:
    """集成测试：模拟完整读取流程"""

    def test_result_map_structure(self):
        """验证 result_map 结构正确"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="REAL"))
        device.add_variable(Variable(variable_id="v2", address="%R102", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]

        # 验证 result_map 结构
        assert "v1" in scan_task.result_map
        assert scan_task.result_map["v1"]["protocol"] == "ge_srtp"
        assert scan_task.result_map["v1"]["area"] == "R"
        assert scan_task.result_map["v1"]["data_type"] == "REAL"
        assert scan_task.result_map["v1"]["word_length"] == 2

    def test_datapoint_count_equals_variable_count(self):
        """合并后 DataPoint 数量等于变量数量"""
        # 模拟 executor 解析流程
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%R101", data_type="INT"))
        device.add_variable(Variable(variable_id="v3", address="%R102", data_type="REAL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]

        # 模拟驱动返回：[INT, INT, REAL_low, REAL_high]
        mock_result = {scan_task.read_blocks[0].address: [42, 100, 0, 0x4128]}

        # 手动模拟 _parse_result 的核心逻辑
        points = []
        for variable in scan_task.variables:
            if not variable.is_storage:
                continue
            mapped = scan_task.result_map.get(variable.variable_id)
            if mapped:
                raw_values = mock_result.get(mapped["block_address"])
                if raw_values:
                    value = Decoder._extract_mapped_ge_value(raw_values, mapped)
                    points.append(value)

        # 验证点数等于变量数
        assert len(points) == 3
        assert points[0] == 42  # v1 INT
        assert points[1] == 100  # v2 INT
        # v3 REAL 需要从 2 words 还原


class TestGEWordSize:
    """测试 GE word 宽度计算"""

    def test_int_word_size(self):
        assert Planner._ge_word_size("INT") == 1

    def test_word_word_size(self):
        assert Planner._ge_word_size("WORD") == 1

    def test_dint_word_size(self):
        assert Planner._ge_word_size("DINT") == 2

    def test_real_word_size(self):
        assert Planner._ge_word_size("REAL") == 2

    def test_float_word_size(self):
        assert Planner._ge_word_size("FLOAT") == 2

    def test_unknown_type_defaults_to_one(self):
        assert Planner._ge_word_size("UNKNOWN") == 1


@pytest.mark.parametrize(
    ("payload_bytes", "expected_blocks"),
    [(250, 2), (500, 1), (1024, 1)],
)
def test_ge_payload_size_changes_planned_block_count(payload_bytes, expected_blocks):
    device = Device(
        device_id="ge_payload",
        device_type="PLC",
        protocol="ge_srtp",
        connection_config={
            "ip": "127.0.0.1",
            "ge_max_payload_bytes": payload_bytes,
        },
    )
    device.add_variable(Variable("r1", "%R00001", "INT"))
    device.add_variable(Variable("r200", "%R00200", "INT"))

    device.build_scan_tasks()

    blocks = device.scan_tasks[0].read_blocks
    assert len(blocks) == expected_blocks
    if expected_blocks == 1:
        assert blocks[0].count == 200


def test_ge_planner_rejects_invalid_payload_size():
    from core.exceptions import DeviceConnectionError

    device = Device(
        device_id="ge_invalid_payload",
        device_type="PLC",
        protocol="ge_srtp",
        connection_config={"ip": "127.0.0.1", "ge_max_payload_bytes": 1025},
    )
    device.add_variable(Variable("r1", "%R00001", "INT"))

    with pytest.raises(DeviceConnectionError, match="Invalid GE max payload"):
        device.build_scan_tasks()

@pytest.mark.asyncio
async def test_ge_connect_creates_isolated_parallel_channels(monkeypatch):
    import packages.ge_srtp.ge_srtp.driver as ge_module

    class FakeConnection:
        instances = []

        def __init__(self, **kwargs):
            self.kwargs = kwargs
            self.connected = False
            self.disconnect_calls = 0
            self.__class__.instances.append(self)

        def connect(self):
            self.connected = True

        def disconnect(self):
            self.connected = False
            self.disconnect_calls += 1

        def is_connected(self):
            return self.connected

    monkeypatch.setattr(ge_module, "GE_SRTP_Driver", FakeConnection)
    driver = ge_module.GESRTPDriver()

    await driver.connect(
        {
            "ip": "127.0.0.1",
            "port": 18245,
            "slot": 1,
            "ge_parallel_channels": 2,
        }
    )

    assert len(FakeConnection.instances) == 2
    assert driver._drivers == FakeConnection.instances
    assert driver._drivers[0] is not driver._drivers[1]
    assert driver.is_connected is True

    await driver.disconnect()
    assert [item.disconnect_calls for item in FakeConnection.instances] == [1, 1]


@pytest.mark.asyncio
async def test_ge_parallel_read_round_robins_blocks_per_isolated_channel():
    from packages.ge_srtp.ge_srtp.driver import GESRTPDriver

    class Channel:
        def __init__(self, name):
            self.name = name
            self.calls = []

        def read_register(self, offset, count):
            self.calls.append((offset, count))
            return [offset] * count

        def is_connected(self):
            return True

    first = Channel("first")
    second = Channel("second")
    driver = GESRTPDriver()
    driver._drivers = [first, second]
    driver._driver = first
    driver._connected = True
    blocks = [
        ReadBlock(f"%R{index:05d}", count=1, data_type="WORD")
        for index in range(1, 5)
    ]

    result = await driver.read(blocks)

    assert set(result) == {block.address for block in blocks}
    assert first.calls == [(0, 1), (2, 1)]
    assert second.calls == [(1, 1), (3, 1)]


@pytest.mark.asyncio
async def test_ge_parallel_read_fails_over_remaining_blocks_to_survivor():
    from packages.ge_srtp.ge_srtp.driver import GESRTPDriver
    from monitor.performance import PerformanceMetrics
    from packages.ge_srtp.ge_srtp.transport import exceptions as ge_exceptions

    class FailedChannel:
        def __init__(self):
            self.calls = 0
            self.connected = True

        def read_register(self, _offset, _count):
            self.calls += 1
            self.connected = False
            raise ge_exceptions.TimeoutError("channel lost")

        def is_connected(self):
            return self.connected

    class SurvivorChannel:
        def __init__(self):
            self.calls = []

        def read_register(self, offset, count):
            self.calls.append((offset, count))
            return [offset] * count

        def is_connected(self):
            return True

    failed = FailedChannel()
    survivor = SurvivorChannel()
    metrics = PerformanceMetrics()
    driver = GESRTPDriver(metrics=metrics)
    driver._drivers = [failed, survivor]
    driver._driver = failed
    driver._connected = True
    blocks = [
        ReadBlock(f"%R{index:05d}", count=1, data_type="WORD")
        for index in range(1, 5)
    ]

    result = await driver.read(blocks)

    assert set(result) == {block.address for block in blocks}
    assert failed.calls == 1
    assert set(survivor.calls) == {(0, 1), (1, 1), (2, 1), (3, 1)}
    assert driver._drivers == [survivor]
    assert driver._driver is survivor
    assert driver.is_connected is True
    snapshot = metrics.snapshot()
    assert snapshot["counters"]["ge_srtp_channel_degraded_count"] == 1
    assert snapshot["counters"]["ge_srtp_failover_retry_block_count"] == 2
    assert snapshot["gauges"]["ge_srtp_active_channel_count"] == 1

@pytest.mark.asyncio
async def test_ge_single_channel_deadline_stops_before_next_block():
    import time as wall_time

    from packages.ge_srtp.ge_srtp.driver import GESRTPDriver
    from monitor.performance import PerformanceMetrics

    class SlowChannel:
        def __init__(self):
            self.calls = []

        def read_register(self, offset, count):
            self.calls.append((offset, count))
            wall_time.sleep(0.02)
            return [offset] * count

        def is_connected(self):
            return True

    channel = SlowChannel()
    metrics = PerformanceMetrics()
    driver = GESRTPDriver(metrics=metrics)
    driver._driver = channel
    driver._connected = True
    driver._batch_deadline_ms = 5
    blocks = [
        ReadBlock(f"%R{index:05d}", count=1, data_type="WORD")
        for index in range(1, 4)
    ]

    result = await driver.read(blocks)

    assert channel.calls == [(0, 1)]
    assert result[blocks[0].address] == [0]
    assert result[blocks[1].address] is None
    assert result[blocks[2].address] is None
    counters = metrics.snapshot()["counters"]
    assert counters["ge_srtp_batch_deadline_count"] == 1
    assert counters["ge_srtp_batch_deadline_block_count"] == 2
    assert counters["ge_srtp_batch_partial_count"] == 1


@pytest.mark.asyncio
async def test_ge_parallel_channels_share_one_batch_deadline():
    import time as wall_time

    from packages.ge_srtp.ge_srtp.driver import GESRTPDriver
    from monitor.performance import PerformanceMetrics

    class SlowChannel:
        def __init__(self):
            self.calls = []

        def read_register(self, offset, count):
            self.calls.append((offset, count))
            wall_time.sleep(0.02)
            return [offset] * count

        def is_connected(self):
            return True

    first = SlowChannel()
    second = SlowChannel()
    metrics = PerformanceMetrics()
    driver = GESRTPDriver(metrics=metrics)
    driver._drivers = [first, second]
    driver._driver = first
    driver._connected = True
    driver._batch_deadline_ms = 5
    blocks = [
        ReadBlock(f"%R{index:05d}", count=1, data_type="WORD")
        for index in range(1, 5)
    ]

    result = await driver.read(blocks)

    assert first.calls == [(0, 1)]
    assert second.calls == [(1, 1)]
    assert result[blocks[0].address] == [0]
    assert result[blocks[1].address] == [1]
    assert result[blocks[2].address] is None
    assert result[blocks[3].address] is None
    counters = metrics.snapshot()["counters"]
    assert counters["ge_srtp_batch_deadline_count"] == 1
    assert counters["ge_srtp_batch_deadline_block_count"] == 2
    assert counters["ge_srtp_batch_partial_count"] == 1
