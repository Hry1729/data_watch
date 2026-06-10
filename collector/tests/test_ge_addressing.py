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
        assert scan_task.read_blocks[0].address == "%R00100"
        assert scan_task.read_blocks[0].count == 3

    def test_separate_blocks_for_gap(self):
        """不连续地址分成 2 块（max_gap=0）"""
        device = Device(
            device_id="qc100",
            device_type="QC",
            protocol="ge_srtp",
            connection_config={"ip": "127.0.0.1"},
        )
        device.add_variable(Variable(variable_id="v1", address="%R100", data_type="INT"))
        device.add_variable(Variable(variable_id="v2", address="%R200", data_type="INT"))

        device.build_scan_tasks()

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 2

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

        scan_task = device.scan_tasks[0]
        assert len(scan_task.read_blocks) == 1
        assert scan_task.read_blocks[0].data_type == "BIT"

        # 验证 result_map
        assert scan_task.result_map["v1"]["bit_offset"] == 0
        assert scan_task.result_map["v2"]["bit_offset"] == 1
        assert scan_task.result_map["v3"]["bit_offset"] == 2

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
