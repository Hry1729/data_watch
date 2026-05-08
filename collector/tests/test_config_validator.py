"""配置校验测试。"""

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.config_validator import validate_config


def test_validator_accepts_current_s7_mysql_address_baseline():
    config = CollectorConfig(
        devices=[DeviceConfig("qc100", "QC", "s7", "127.0.0.1")],
        variables=[
            VariableConfig("1", "qc100", "%I101.0", "BOOL", 1, True, 1),
            VariableConfig("2", "qc100", "%MD1304", "DINT", 1, True, 2),
            VariableConfig("3", "qc100", "DB30.DBD100", "REAL", 1, True, 3),
        ],
    )

    result = validate_config(config)

    assert result.valid, result.errors


def test_validator_rejects_legacy_and_ambiguous_s7_addresses():
    config = CollectorConfig(
        devices=[DeviceConfig("qc100", "QC", "s7", "127.0.0.1")],
        variables=[
            VariableConfig("1", "qc100", "DB100.0", "INT", 1, True, 1),
            VariableConfig("2", "qc100", "%I101", "BOOL", 1, True, 2),
            VariableConfig("3", "qc100", "DB1.DBW1", "INT", 1, True, 3),
        ],
    )

    result = validate_config(config)

    assert not result.valid
    assert len([error for error in result.errors if "Invalid variable address" in error]) == 3


def test_validator_uses_protocol_specific_address_rules():
    config = CollectorConfig(
        devices=[
            DeviceConfig("ge001", "ASC", "ge_srtp", "127.0.0.1"),
            DeviceConfig("opc001", "AGV", "opcua", "127.0.0.1", port=4840),
        ],
        variables=[
            VariableConfig("1", "ge001", "%I101", "BOOL", 1, True, 1),
            VariableConfig("2", "opc001", "ns=2;s=Channel1.Device1.Temp", "REAL", 1, True, 2),
        ],
    )

    result = validate_config(config)

    assert result.valid, result.errors


def test_validator_rejects_direct_opcda_in_64bit_collector():
    config = CollectorConfig(
        devices=[
            DeviceConfig("agv801", "AGV", "opcda", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("1", "agv801", "ZPMC.AGV801.Speed", "REAL", 1, True, 100),
        ],
    )

    result = validate_config(config)

    assert not result.valid
    assert any("32-bit sidecar" in error for error in result.errors)


def test_validator_accepts_opcda_sidecar_push_protocol():
    config = CollectorConfig(
        devices=[
            DeviceConfig("agv801", "AGV", "opcda_sidecar", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("1", "agv801", "ZPMC.AGV801.Speed", "REAL", 1, True, 100),
        ],
    )

    result = validate_config(config)

    assert result.valid, result.errors
