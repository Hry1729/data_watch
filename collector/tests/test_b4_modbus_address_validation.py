"""B4 回归：Modbus 地址校验必须复用驱动自身的规则。

修复前 ``ConfigValidator._validate_address`` 没有 modbus 分支，Modbus 变量落到"未知协议"
兜底 ``_validate_s7_address or _validate_ge_address``：
- ``HR10`` / ``holding:10`` 这类**合法** Modbus 地址被误拒（冷启动直接判配置非法）；
- ``C5`` 只是**碰巧**通过（被当成 S7 计数器），并非按驱动规则通过；
- 且 modbus_tcp 不在已知协议表里，会被报成 "Unknown protocol"。

现在按文件路径加载 ``packages/modbus_tcp/modbus_tcp/address.py`` 并直接调用其
``parse_address``：单一真值源，且与驱动加载顺序无关（该模块只依赖标准库）。
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))

from core.config import CollectorConfig, DeviceConfig, VariableConfig  # noqa: E402
from core.config_validator import ConfigValidator, _load_modbus_address_module  # noqa: E402

validator = ConfigValidator()

LEGAL = ["HR10", "hr10", "holding:10", "HOLDING:0", "C0", "c5", "coil:3", "COIL:0"]
ILLEGAL = ["", "   ", "X10", "HR", "holding:", "HR-1", "HR 10", "10",
           "DB100.DBW0", "%R100", "coil:1:2", "holding:abc"]


def _driver_accepts(address: str) -> bool:
    module = _load_modbus_address_module()
    assert module is not None, "必须能加载到驱动自身的地址规则"
    try:
        module.parse_address(address)
    except Exception:
        return False
    return True


def test_driver_address_rules_are_loadable() -> None:
    module = _load_modbus_address_module()
    assert module is not None
    assert hasattr(module, "parse_address")


@pytest.mark.parametrize("address", LEGAL)
def test_legal_modbus_addresses_are_accepted(address: str) -> None:
    assert validator._validate_address(address, protocol="modbus_tcp") is True


@pytest.mark.parametrize("address", ILLEGAL)
def test_illegal_modbus_addresses_are_still_rejected(address: str) -> None:
    assert validator._validate_address(address, protocol="modbus_tcp") is False


@pytest.mark.parametrize("address", LEGAL + ILLEGAL)
def test_validator_matches_driver_rule_exactly(address: str) -> None:
    """防漂移：校验器结论必须与驱动规则逐一一致（这正是 B4 的根因）。"""
    assert validator._validate_address(address, protocol="modbus_tcp") is _driver_accepts(address)


def test_modbus_protocol_is_no_longer_reported_as_unknown() -> None:
    device = DeviceConfig("MB01", "QC", "modbus_tcp", "192.168.1.80", port=502, slot=1)
    variable = VariableConfig("v1", "MB01", "HR10", "INT", 1, True, 1)
    result = validator.validate_config(CollectorConfig(devices=[device], variables=[variable]))

    assert not any("Unknown protocol" in item for item in result.warnings), result.warnings
    assert result.valid, result.errors


def test_one_bad_modbus_address_is_reported_as_variable_error() -> None:
    """单设备地址写错应报为可定位的变量错误，而不是静默通过。"""
    device = DeviceConfig("MB02", "QC", "modbus_tcp", "192.168.1.81", port=502, slot=1)
    variable = VariableConfig("v1", "MB02", "DB100.DBW0", "INT", 1, True, 1)
    result = validator.validate_config(CollectorConfig(devices=[device], variables=[variable]))

    assert result.valid is False
    assert any("Invalid variable address" in item for item in result.errors), result.errors


def test_frozen_rules_work_without_source_packages(tmp_path, monkeypatch):
    import core.config_validator as module
    target = tmp_path / "_internal" / "modbus-rules" / "address.py"
    target.parent.mkdir(parents=True)
    target.write_bytes((REPO_ROOT / "packages/modbus_tcp/modbus_tcp/address.py").read_bytes())
    monkeypatch.setattr(module, "_MODBUS_ADDRESS_CANDIDATES", (target,))
    monkeypatch.setattr(module, "_modbus_address_module", None)
    monkeypatch.setattr(module, "_modbus_address_module_resolved", False)
    assert validator._validate_address("HR10", "modbus_tcp")
    assert not validator._validate_address("DB100.DBW0", "modbus_tcp")


def test_missing_rules_can_be_loaded_after_becoming_available(tmp_path, monkeypatch):
    import core.config_validator as module
    target = tmp_path / "address.py"
    monkeypatch.setattr(module, "_MODBUS_ADDRESS_CANDIDATES", (target,))
    monkeypatch.setattr(module, "_modbus_address_module", None)
    monkeypatch.setattr(module, "_modbus_address_module_resolved", False)
    assert not validator._validate_address("HR10", "modbus_tcp")
    target.write_bytes((REPO_ROOT / "packages/modbus_tcp/modbus_tcp/address.py").read_bytes())
    assert validator._validate_address("HR10", "modbus_tcp")
