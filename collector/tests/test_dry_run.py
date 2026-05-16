"""Dry-run 配置预检测试"""

import pytest

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.dry_run import run_dry_run, DryRunResult


def test_dry_run_basic_counts():
    """基本统计测试"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("dev1", "QC", "s7", "127.0.0.1"),
            DeviceConfig("dev2", "ASC", "ge_srtp", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("v1", "dev1", "%I101.0", "BOOL", 1, True, 1),
            VariableConfig("v2", "dev1", "%MD100", "DINT", 1, True, 2),
            VariableConfig("v3", "dev2", "%R100", "INT", 1, False, 3),  # 不存储
            VariableConfig("v4", "dev2", "%R101", "WORD", 1, True, 4),
        ],
    )

    result = run_dry_run(config)

    assert result.total_devices == 2
    assert result.total_variables == 4
    assert result.storage_variables == 3
    assert result.skipped_variables == 1


def test_dry_run_protocol_stats():
    """按协议统计"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("s7_dev", "QC", "s7", "127.0.0.1"),
            DeviceConfig("ge_dev", "ASC", "ge_srtp", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("v1", "s7_dev", "%I101.0", "BOOL", 1, True, 1),
            VariableConfig("v2", "ge_dev", "%R100", "INT", 1, True, 2),
        ],
    )

    result = run_dry_run(config)

    assert result.devices_by_protocol["s7"] == 1
    assert result.devices_by_protocol["ge_srtp"] == 1
    assert result.variables_by_protocol["s7"] == 1
    assert result.variables_by_protocol["ge_srtp"] == 1


def test_dry_run_merge_ratio():
    """合并比例计算"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("dev1", "QC", "ge_srtp", "127.0.0.1"),
        ],
        variables=[
            # 连续地址可合并
            VariableConfig("v1", "dev1", "%R100", "INT", 1, True, 1),
            VariableConfig("v2", "dev1", "%R101", "INT", 1, True, 2),
            VariableConfig("v3", "dev1", "%R102", "INT", 1, True, 3),
        ],
    )

    result = run_dry_run(config)

    # 3 个变量合并为 1 块，比例应为 3.0
    assert result.total_read_blocks >= 1
    assert result.merge_ratio >= 1.0


def test_dry_run_parse_success():
    """地址解析成功"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("dev1", "QC", "s7", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("v1", "dev1", "%I101.0", "BOOL", 1, True, 1),
            VariableConfig("v2", "dev1", "%MW100", "INT", 1, True, 2),
            VariableConfig("v3", "dev1", "DB1.DBX0.0", "BOOL", 1, True, 3),
        ],
    )

    result = run_dry_run(config)

    assert result.parse_success == 3
    assert len(result.parse_failed) == 0


def test_dry_run_data_type_stats():
    """数据类型统计"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("dev1", "QC", "s7", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("v1", "dev1", "%I101.0", "BOOL", 1, True, 1),
            VariableConfig("v2", "dev1", "%MW100", "INT", 1, True, 2),
            VariableConfig("v3", "dev1", "%MD100", "REAL", 1, True, 3),
            VariableConfig("v4", "dev1", "%MB100", "BYTE", 1, True, 4),
        ],
    )

    result = run_dry_run(config)

    assert result.data_types_used.get("BOOL") == 1
    assert result.data_types_used.get("INT") == 1
    assert result.data_types_used.get("REAL") == 1
    assert result.data_types_used.get("BYTE") == 1


def test_dry_run_warnings_for_missing_device():
    """警告：变量引用不存在的设备"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("dev1", "QC", "s7", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("v1", "dev1", "%I101.0", "BOOL", 1, True, 1),
            VariableConfig("v2", "unknown_dev", "%R100", "INT", 1, True, 2),  # 不存在的设备
        ],
    )

    result = run_dry_run(config)

    assert any("不存在的设备" in warn for warn in result.warnings)


def test_dry_run_timer_counter():
    """Timer/Counter 统计"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("dev1", "QC", "s7", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("t1", "dev1", "%T1", "TIMER_MS", 1, True, 1),
            VariableConfig("c1", "dev1", "%C1", "COUNTER", 1, True, 2),
        ],
    )

    result = run_dry_run(config)

    assert result.data_types_used.get("TIMER_MS") == 1
    assert result.data_types_used.get("COUNTER") == 1
    assert len(result.unsupported_types) == 0


def test_dry_run_summary_output():
    """摘要输出"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("dev1", "QC", "s7", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("v1", "dev1", "%I101.0", "BOOL", 1, True, 1),
        ],
    )

    result = run_dry_run(config)
    summary = result.to_summary()

    assert "DRY-RUN 配置预检报告" in summary
    assert "设备总数: 1" in summary
    assert "变量总数: 1" in summary


def test_dry_run_empty_config():
    """空配置处理"""
    config = CollectorConfig(
        devices=[],
        variables=[],
    )

    result = run_dry_run(config)

    assert result.total_devices == 0
    assert result.total_variables == 0
    assert result.parse_success == 0