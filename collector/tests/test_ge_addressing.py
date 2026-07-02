"""
GE SRTP 地址解析与批量合并测试。

测试覆盖：
1. 地址解析：%R/%AI/%AQ/%I/%Q/%M
2. 合并：连续地址、空洞分隔、超限拆块、不同区域/周期不合并
3. 单点还原：INT/WORD/DINT/REAL/BOOL
4. 集成：fake driver 返回批量值，executor 还原正确
"""

import struct
import pytest

from device.device import Device, Variable
from drivers.base import ReadBlock
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
        assert parsed["protocol_offset"] == 1  # 离散区暂不减 1

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
        assert parsed["protocol_offset"] == 1

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
        assert parsed["protocol_offset"] == 201

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

    @pytest.mark.parametrize("address", ["%R0", "%AI0", "%AQ0", "%I0", "%Q0", "%M0"])
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
        # %R100 -> offset 99, %R200 -> offset 199
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

        # byte 模式：bits 1..3 落在 byte 0 -> 1 块 1 字节，块首位 bit 0
        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].data_type == "BYTE"
        assert scan_task.read_blocks[0].address == "%I00000"
        assert scan_task.read_blocks[0].count == 1

        # 验证 result_map（bit_offset 相对块首位）
        assert scan_task.result_map["v1"]["bit_offset"] == 1
        assert scan_task.result_map["v2"]["bit_offset"] == 2
        assert scan_task.result_map["v3"]["bit_offset"] == 3
        assert scan_task.result_map["v1"]["packing"] == "byte"

    def test_split_register_when_span_exceeds_max_words(self):
        """跨空洞时总跨度超过 125 words -> 正确拆块（gap 在限内但 span 超限）"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %R1 -> offset 0, %R127 -> offset 126
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
        # 逻辑地址 %I1/%I10 -> 0基协议偏移 1/10（离散区暂不减1，待现场校准）
        # byte 模式：bit 1 -> byte 0，bit 10 -> byte 1（end 11 -> byte_end 2）-> 1 块 2 字节
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I10", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].data_type == "BYTE"
        assert scan_task.read_blocks[0].count == 2
        assert scan_task.result_map["v1"]["bit_offset"] == 1
        assert scan_task.result_map["v2"]["bit_offset"] == 10

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
        # %I1 -> bit 1 -> byte 0；%I1999 -> bit 1999（end 2000）-> byte_end 250
        # span = 250 - 0 = 250 <= 250 -> 合并
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I1999", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].count == 250
        assert scan_task.result_map["v1"]["bit_offset"] == 1
        assert scan_task.result_map["v2"]["bit_offset"] == 1999

    def test_split_discrete_when_span_exceeds_max_bytes(self):
        """离散区 span 超过 250 字节 -> 正确拆块"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # %I1 -> byte 0；%I2000 -> bit 2000（end 2001）-> byte_end 251
        # span = 251 > 250 -> 拆块
        device.add_variable(Variable(variable_id="v1", address="%I1", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I2000", data_type="BOOL"))

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
        # %I9 -> bit 9 -> byte 1（块首位 bit 8）；%I16 -> bit 16 -> byte 2（end 17 -> byte_end 3）
        device.add_variable(Variable(variable_id="v1", address="%I9", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v2", address="%I16", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%I00000"
        assert scan_task.read_blocks[0].count == 3
        assert scan_task.result_map["v1"]["bit_offset"] == 9
        assert scan_task.result_map["v2"]["bit_offset"] == 16

    def test_discrete_byte_boundary_offsets(self):
        """字节边界 0/1 基防混淆：%I7/%I8/%I15/%I16 的块界与 bit_offset"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        # bits 7,8,15,16：byte_start = 7//8 = 0，byte_end = (17+7)//8 = 3 -> 1 块 3 字节，块首位 bit 0
        device.add_variable(Variable(variable_id="v7", address="%I7", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v8", address="%I8", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v15", address="%I15", data_type="BOOL"))
        device.add_variable(Variable(variable_id="v16", address="%I16", data_type="BOOL"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%I00000"
        assert scan_task.read_blocks[0].count == 3
        assert scan_task.result_map["v7"]["bit_offset"] == 7
        assert scan_task.result_map["v8"]["bit_offset"] == 8
        assert scan_task.result_map["v15"]["bit_offset"] == 15
        assert scan_task.result_map["v16"]["bit_offset"] == 16

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

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].address == "%Q01968"
        assert scan_task.read_blocks[0].count == 8
        assert scan_task.result_map["v1"]["bit_offset"] == 51
        assert scan_task.result_map["v2"]["bit_offset"] == 58

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
        assert scan_task.read_blocks[0].address == "%I00000"
        assert scan_task.read_blocks[0].count == 1
        assert scan_task.result_map["v1"]["bit_offset"] == 1

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
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping) == 200

    def test_extract_negative_int_from_register_block(self):
        """从寄存器块提取负数 INT（gesrtp-py 已返回 signed）"""
        raw = [-1]
        mapping = {
            "area": "R",
            "word_offset": 0,
            "data_type": "INT",
        }
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping) == -1

    def test_extract_int_from_unsigned_input(self):
        """unsigned 输入兼容转换为 signed INT"""
        raw = [0xFFFF]  # unsigned 65535
        mapping = {
            "area": "R",
            "word_offset": 0,
            "data_type": "INT",
        }
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping) == -1  # signed

    def test_extract_word_from_register_block(self):
        """从寄存器块提取 WORD（无符号）"""
        raw = [100, 65535, 300]
        mapping = {
            "area": "R",
            "word_offset": 1,
            "data_type": "WORD",
        }
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping) == 65535

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
            TaskExecutor._extract_mapped_ge_value(raw, mapping)

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
            TaskExecutor._extract_mapped_ge_value(raw, mapping)

    def test_extract_dint_from_register_block(self):
        """从寄存器块提取 DINT（2 words 组合）"""
        raw = [0, 0x0001, 0x0000]  # low=1, high=0 -> DINT=1
        mapping = {
            "area": "R",
            "word_offset": 1,
            "word_length": 2,
            "data_type": "DINT",
        }
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping) == 1

        # 负数测试
        raw2 = [0, 0xFFFF, 0xFFFF]  # low=65535, high=65535 -> DINT=-1
        mapping2 = {
            "area": "R",
            "word_offset": 1,
            "word_length": 2,
            "data_type": "DINT",
        }
        assert TaskExecutor._extract_mapped_ge_value(raw2, mapping2) == -1

    def test_extract_real_from_register_block(self):
        """从寄存器块提取 REAL（2 words 组合）"""
        # 浮点数 10.5 的 IEEE 754 表示
        # 0x41280000 = 10.5
        # low word = 0x0000, high word = 0x4128
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
        result = TaskExecutor._extract_mapped_ge_value(raw, mapping)
        assert abs(result - 10.5) < 0.01

    def test_extract_bool_from_discrete_block(self):
        """从离散块提取 BOOL"""
        raw = [True, False, True, False]
        mapping = {
            "area": "I",
            "bit_offset": 2,
            "data_type": "BOOL",
        }
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping) is True

        mapping2 = {
            "area": "I",
            "bit_offset": 1,
            "data_type": "BOOL",
        }
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping2) is False

    def test_extract_bool_from_discrete_byte_block(self):
        """从 byte 模式离散块提取 BOOL（LSB-first 拆位）"""
        # byte0 = 0b0000_0010（bit 1 置位），byte1 = 0b0000_0001（bit 8 置位）
        raw = [0x02, 0x01]

        def mapping(bit):
            return {"area": "I", "bit_offset": bit, "data_type": "BOOL", "packing": "byte"}

        assert TaskExecutor._extract_mapped_ge_value(raw, mapping(1)) is True
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping(0)) is False
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping(8)) is True
        assert TaskExecutor._extract_mapped_ge_value(raw, mapping(9)) is False

    def test_extract_bool_from_single_int_byte_block(self):
        """count=1 时 driver 返回单 int 的退化：归一化后仍可拆位"""
        mapping = {"area": "I", "bit_offset": 1, "data_type": "BOOL", "packing": "byte"}
        assert TaskExecutor._extract_mapped_ge_value(0x02, mapping) is True

    def test_extract_byte_packed_out_of_range_raises(self):
        """bit_offset 超出块字节数 -> 带上下文的 ValueError 而非裸 IndexError"""
        mapping = {
            "area": "I",
            "bit_offset": 16,
            "data_type": "BOOL",
            "packing": "byte",
            "block_address": "%I00000",
        }
        with pytest.raises(ValueError, match="out of range"):
            TaskExecutor._extract_mapped_ge_value([0x00, 0x00], mapping)


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


class TestGEDriverByteMode:
    """driver 适配层：BYTE 离散块走 byte 模式（地址 //8），BIT 块保留 bit 路径"""

    def _make_driver(self):
        from drivers.ge_srtp_driver import GESRTPDriver
        drv = GESRTPDriver()
        drv._driver = _FakeGeDriver()
        drv._connected = True
        return drv

    def test_byte_block_offset_zero(self):
        """%I00000（%I1 所在块）-> byte_offset 0，客户端全链路合法"""
        drv = self._make_driver()
        drv._read_block(ReadBlock("%I00000", count=2, data_type="BYTE"))
        assert drv._driver.calls == [("I", 0, 2, "byte")]

    def test_byte_block_nonzero_base_q_area(self):
        drv = self._make_driver()
        drv._read_block(ReadBlock("%Q00008", count=1, data_type="BYTE"))
        assert drv._driver.calls == [("Q", 1, 1, "byte")]

    def test_byte_block_m_area(self):
        drv = self._make_driver()
        drv._read_block(ReadBlock("%M00016", count=3, data_type="BYTE"))
        assert drv._driver.calls == [("M", 2, 3, "byte")]

    def test_byte_block_not_aligned_raises(self):
        from core.exceptions import ReadError
        drv = self._make_driver()
        with pytest.raises(ReadError, match="not byte-aligned"):
            drv._read_block(ReadBlock("%I00004", count=1, data_type="BYTE"))

    def test_bit_block_keeps_legacy_path(self):
        drv = self._make_driver()
        drv._read_block(ReadBlock("%I00001", count=10, data_type="BIT"))
        assert drv._driver.calls == [("I", 1, 10, "bit")]


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
                    value = TaskExecutor._extract_mapped_ge_value(raw_values, mapped)
                    points.append(value)

        # 验证点数等于变量数
        assert len(points) == 3
        assert points[0] == 42  # v1 INT
        assert points[1] == 100  # v2 INT
        # v3 REAL 需要从 2 words 还原


class TestGEWordSize:
    """测试 GE word 宽度计算"""

    def test_int_word_size(self):
        assert Device._ge_word_size("INT") == 1

    def test_word_word_size(self):
        assert Device._ge_word_size("WORD") == 1

    def test_dint_word_size(self):
        assert Device._ge_word_size("DINT") == 2

    def test_real_word_size(self):
        assert Device._ge_word_size("REAL") == 2

    def test_float_word_size(self):
        assert Device._ge_word_size("FLOAT") == 2

    def test_unknown_type_defaults_to_one(self):
        assert Device._ge_word_size("UNKNOWN") == 1
