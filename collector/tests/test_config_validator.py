"""配置校验测试。"""

from core.config import CollectorConfig, DeviceConfig, RedisConfig, VariableConfig
from core.config_validator import validate_config


def test_validator_accepts_current_s7_mysql_address_baseline():
    config = CollectorConfig(
        devices=[DeviceConfig("qc100", "QC", "s7", "127.0.0.1")],
        variables=[
            VariableConfig("1", "qc100", "%I101.0", "BOOL", 1, True, 1),
            VariableConfig("2", "qc100", "%MD1304", "DINT", 1, True, 2),
            VariableConfig("3", "qc100", "DB30.DBD100", "REAL", 1, True, 3),
            VariableConfig("4", "qc100", "%DB1702.DBW0", "INT", 1, True, 4),
            VariableConfig("5", "qc100", "%DB1702.DBB2", "BYTE", 1, True, 5),
            VariableConfig("6", "qc100", "%DB1702.DBB3", "BYTE", 1, True, 6),
            VariableConfig("7", "qc100", "%DB1702.DBD4", "REAL", 1, True, 7),
            VariableConfig("8", "qc100", "%DB1702.DBX440.6", "BOOL", 1, True, 8),
        ],
    )

    result = validate_config(config)

    assert result.valid, result.errors


def test_validator_rejects_invalid_redis_config():
    config = CollectorConfig(
        devices=[DeviceConfig("qc100", "QC", "s7", "127.0.0.1")],
        variables=[VariableConfig("1", "qc100", "%MD1304", "DINT", 1, True, 1)],
        redis=RedisConfig(port=0, db=-1, stream_shard_count=0, stream_maxlen=0),
    )

    result = validate_config(config)

    assert not result.valid
    assert any("stream_shard_count" in e for e in result.errors)
    assert any("stream_maxlen" in e for e in result.errors)
    assert any("Invalid redis port" in e for e in result.errors)
    assert any("Invalid redis db" in e for e in result.errors)


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


def test_validator_rejects_s7_address_data_type_mismatches():
    config = CollectorConfig(
        devices=[DeviceConfig("qc100", "QC", "s7", "127.0.0.1")],
        variables=[
            VariableConfig("1", "qc100", "%I101.0", "INT", 1, True, 1),
            VariableConfig("2", "qc100", "%MB1304", "BOOL", 1, True, 2),
            VariableConfig("3", "qc100", "DB30.DBD100", "INT", 1, True, 3),
        ],
    )

    result = validate_config(config)

    assert not result.valid
    assert len([
        error for error in result.errors
        if "Invalid S7 address/data_type combination" in error
    ]) == 3


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

def test_validator_accepts_additional_ge_discrete_memory_areas():
    """GE 配置校验接受新增的只读 BOOL 内存区。"""
    config = CollectorConfig(
        devices=[DeviceConfig("ge001", "ASC", "ge_srtp", "127.0.0.1")],
        variables=[
            VariableConfig("1", "ge001", "%T1", "BOOL", 1, True, 1),
            VariableConfig("2", "ge001", "%S2", "BOOL", 1, True, 2),
            VariableConfig("3", "ge001", "%SA3", "BOOL", 1, True, 3),
            VariableConfig("4", "ge001", "%SB4", "BOOL", 1, True, 4),
            VariableConfig("5", "ge001", "%SC5", "BOOL", 1, True, 5),
            VariableConfig("6", "ge001", "%G6", "BOOL", 1, True, 6),
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


def test_validator_accepts_ge_unsigned_types():
    """GE SRTP 支持 UINT/UDINT/USINT 无符号类型。"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("ge001", "ASC", "ge_srtp", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("1", "ge001", "%R100", "UINT", 1, True, 1),
            VariableConfig("2", "ge001", "%R102", "UDINT", 1, True, 2),
            VariableConfig("3", "ge001", "%R104", "USINT", 1, True, 3),
        ],
    )

    result = validate_config(config)

    assert result.valid, result.errors


def test_validator_accepts_s7_unsigned_types():
    """S7 WORD/DWORD/BYTE 对应无符号类型。"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("s7100", "QC", "s7", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("1", "s7100", "%MW100", "WORD", 1, True, 1),
            VariableConfig("2", "s7100", "%MD100", "DWORD", 1, True, 2),
            VariableConfig("3", "s7100", "%MB100", "BYTE", 1, True, 3),
        ],
    )

    result = validate_config(config)

    assert result.valid, result.errors


def test_validator_accepts_s7_timer_counter():
    """S7 Timer/Counter 地址与数据类型验证。"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("s7100", "QC", "s7", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("1", "s7100", "%T1", "TIMER_MS", 1, True, 1),
            VariableConfig("2", "s7100", "%T10", "WORD", 1, True, 2),
            VariableConfig("3", "s7100", "%C1", "COUNTER", 1, True, 3),
            VariableConfig("4", "s7100", "%C5", "INT", 1, True, 4),
        ],
    )

    result = validate_config(config)

    assert result.valid, result.errors


def test_validator_rejects_invalid_timer_counter_data_type():
    """Timer/Counter 使用错误数据类型应被拒绝。"""
    config = CollectorConfig(
        devices=[
            DeviceConfig("s7100", "QC", "s7", "127.0.0.1"),
        ],
        variables=[
            VariableConfig("1", "s7100", "%T1", "BOOL", 1, True, 1),
            VariableConfig("2", "s7100", "%C1", "REAL", 1, True, 2),
        ],
    )

    result = validate_config(config)

    assert not result.valid
    assert any("Timer" in error for error in result.errors)
    assert any("Counter" in error for error in result.errors)

def test_read_timeout_is_optional_positive_and_independent_from_scan_rate():
    valid = CollectorConfig(
        devices=[
            DeviceConfig(
                "qc100", "QC", "s7", "127.0.0.1",
                scan_rate=0.1, read_timeout_ms=900,
            )
        ],
        variables=[
            VariableConfig("1", "qc100", "%MD1304", "DINT", 0.1, True, 1)
        ],
    )
    assert validate_config(valid).valid

    valid.devices[0].read_timeout_ms = 0
    result = validate_config(valid)
    assert not result.valid
    assert any("Invalid read timeout" in error for error in result.errors)


def test_validator_rejects_invalid_global_ge_payload():
    config = CollectorConfig(ge_max_payload_bytes=1025)

    result = validate_config(config)

    assert not result.valid
    assert any("runtime.ge_max_payload_bytes" in error for error in result.errors)


def test_validator_rejects_invalid_device_ge_payload_override():
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                "ge001",
                "PLC",
                "ge_srtp",
                "127.0.0.1",
                ge_max_payload_bytes=7,
            )
        ]
    )

    result = validate_config(config)

    assert not result.valid
    assert any("Invalid GE max payload" in error for error in result.errors)

def test_validator_rejects_invalid_global_ge_parallel_channels():
    config = CollectorConfig(ge_parallel_channels=3)

    result = validate_config(config)

    assert not result.valid
    assert any("runtime.ge_parallel_channels" in error for error in result.errors)


def test_validator_rejects_invalid_device_ge_parallel_channels_override():
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                "ge_parallel",
                "PLC",
                "ge_srtp",
                "127.0.0.1",
                ge_parallel_channels=0,
            )
        ]
    )

    result = validate_config(config)

    assert not result.valid
    assert any("Invalid GE parallel channels" in error for error in result.errors)

def test_validator_rejects_invalid_global_ge_batch_deadline():
    config = CollectorConfig(ge_batch_deadline_ms=0)

    result = validate_config(config)

    assert not result.valid
    assert any("runtime.ge_batch_deadline_ms" in error for error in result.errors)


def test_validator_rejects_invalid_device_ge_batch_deadline_override():
    config = CollectorConfig(
        devices=[
            DeviceConfig(
                "ge_deadline",
                "PLC",
                "ge_srtp",
                "127.0.0.1",
                ge_batch_deadline_ms=-1,
            )
        ]
    )

    result = validate_config(config)

    assert not result.valid
    assert any("Invalid GE batch deadline" in error for error in result.errors)
