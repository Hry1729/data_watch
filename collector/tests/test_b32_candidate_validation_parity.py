"""B32 回归：热更新候选必须与冷启动走同一套地址/类型组合校验。

修复前 ``prepare_candidate_device``（热更新候选）只做连接配置与扫描任务，**不跑
``_validate_variable_config``**；而冷启动 ``validate_config`` 会调用
``_validate_s7_address_data_type``。于是 ``%MW10`` + ``REAL`` 这类错配冷启动被拒、
热更新却被接受——热更新等于放宽了校验。

现在候选取代调用 ``ConfigValidator.validate_device_candidate``，它与冷启动对同一设备/变量
执行的是**同一个** device/variable 校验器。
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from core.config import CollectorConfig, DeviceConfig, VariableConfig  # noqa: E402
from core.config_validator import ConfigValidator  # noqa: E402
from device.device import DeviceStatus  # noqa: E402
from device.manager import DeviceManager  # noqa: E402

# 地址宽度与数据类型错配（应被冷/热两条路径同时拒绝）
MISMATCHES = [
    ("%MW10", "REAL"),      # W 宽度只容 INT/WORD
    ("%MW10", "DINT"),
    ("%MD10", "INT"),       # D 宽度不容 INT
    ("%M10.0", "INT"),      # 位地址只容 BOOL
    ("DB1.DBW0", "REAL"),
    ("DB1.DBD0", "INT"),
    ("DB1.DBX0.0", "INT"),
    ("%MB10", "INT"),       # B 宽度只容 BYTE
]

# 合法组合（两条路径都应放行）
LEGAL = [
    ("%MW10", "INT"),
    ("%MW10", "WORD"),
    ("%MD10", "REAL"),
    ("%MD10", "DINT"),
    ("%M10.0", "BOOL"),
    ("DB1.DBW0", "INT"),
    ("DB1.DBD0", "REAL"),
    ("DB1.DBX0.0", "BOOL"),
    ("%MB10", "BYTE"),
]


def _manager() -> DeviceManager:
    manager = DeviceManager(CollectorConfig(devices=[], variables=[]))
    manager.register_devices()
    return manager


def _config(address: str, data_type: str) -> tuple[DeviceConfig, VariableConfig, CollectorConfig]:
    device = DeviceConfig("PL01", "QC", "s7", "192.168.1.90", port=102, slot=1)
    variable = VariableConfig("v1", "PL01", address, data_type, 1, True, 1)
    return device, variable, CollectorConfig(devices=[device], variables=[variable])


@pytest.mark.parametrize("address,data_type", MISMATCHES)
def test_mismatch_is_rejected_by_both_cold_and_hot(address: str, data_type: str) -> None:
    device, variable, config = _config(address, data_type)

    cold = ConfigValidator().validate_config(config)
    assert cold.valid is False, f"冷启动应拒绝 {address}+{data_type}"

    candidate = _manager().prepare_candidate_device(device, [variable])
    assert candidate.status == DeviceStatus.CONFIG_ERROR, (
        f"热更新候选应拒绝 {address}+{data_type}，实际 status={candidate.status} "
        f"error={candidate.config_error!r}"
    )
    assert "combination" in (candidate.config_error or ""), candidate.config_error


@pytest.mark.parametrize("address,data_type", LEGAL)
def test_legal_combination_passes_both_paths(address: str, data_type: str) -> None:
    device, variable, config = _config(address, data_type)

    cold = ConfigValidator().validate_config(config)
    assert not [item for item in cold.errors if "combination" in item], cold.errors

    candidate = _manager().prepare_candidate_device(device, [variable])
    assert "combination" not in (candidate.config_error or ""), candidate.config_error


@pytest.mark.parametrize("address,data_type", MISMATCHES + LEGAL)
def test_cold_and_hot_verdicts_agree(address: str, data_type: str) -> None:
    """一致性：候选"有组合错误"当且仅当冷启动报出组合错误（B32 的根因类别）。"""
    device, variable, config = _config(address, data_type)

    cold_rejects = any(
        "combination" in item for item in ConfigValidator().validate_config(config).errors
    )
    candidate = _manager().prepare_candidate_device(device, [variable])
    hot_rejects = "combination" in (candidate.config_error or "")

    assert cold_rejects is hot_rejects, (
        f"{address}+{data_type}: 冷启动拒绝={cold_rejects} 热更新拒绝={hot_rejects}"
    )


def test_candidate_error_reports_every_variable_problem_at_once() -> None:
    """一次候选校验应列出所有问题变量的错误，便于一次修完。"""
    device = DeviceConfig("PL01", "QC", "s7", "192.168.1.90", port=102, slot=1)
    variables = [
        VariableConfig("v1", "PL01", "%MW10", "REAL", 1, True, 1),
        VariableConfig("v2", "PL01", "DB1.DBD0", "INT", 1, True, 1),
    ]
    candidate = _manager().prepare_candidate_device(device, variables)

    assert candidate.status == DeviceStatus.CONFIG_ERROR
    assert "v1" in (candidate.config_error or "") or "%MW10" in (candidate.config_error or "")
    assert "v2" in (candidate.config_error or "") or "DB1.DBD0" in (candidate.config_error or "")
