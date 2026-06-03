"""配置验证器"""

import ipaddress
import re
import socket
from typing import Any, Dict, List, Optional, Tuple, Union
from dataclasses import dataclass, field

from core.config import CollectorConfig, DeviceConfig, VariableConfig, MySQLConfig
from core.exceptions import ConfigError
from core.logger import get_logger

logger = get_logger("config_validator")


@dataclass
class ValidationResult:
    """验证结果"""
    valid: bool
    errors: List[str] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)

    def _merge(self, other: "ValidationResult") -> None:
        """合并另一个验证结果"""
        self.errors.extend(other.errors)
        self.warnings.extend(other.warnings)


class ConfigValidator:
    """配置验证器"""

    def __init__(self):
        self._rules = {
            "mysql": self._validate_mysql_config,
            "device": self._validate_device_config,
            "variable": self._validate_variable_config,
            "global": self._validate_global_config,
        }

    def validate_config(self, config: CollectorConfig) -> ValidationResult:
        """验证完整配置"""
        result = ValidationResult(valid=True)

        # 验证各个部分
        if "mysql" in self._rules:
            mysql_result = self._rules["mysql"](config.mysql)
            result._merge(mysql_result)

        # 验证设备配置
        for device_config in config.devices:
            device_result = self._rules["device"](device_config)
            result._merge(device_result)

        device_protocols = {dev.device_id: dev.protocol for dev in config.devices}

        # 验证变量配置
        for var_config in config.variables:
            var_result = self._validate_variable_config(
                var_config,
                protocol=device_protocols.get(var_config.device_id),
            )
            result._merge(var_result)

        # 验证全局配置
        global_result = self._rules["global"](config)
        result._merge(global_result)

        result.valid = len(result.errors) == 0
        return result

    def _validate_device_config(self, config: DeviceConfig) -> ValidationResult:
        """验证设备配置"""
        result = ValidationResult(valid=True)

        # 验证设备ID
        if not config.device_id or not re.match(r'^[a-zA-Z0-9_]+$', config.device_id):
            result.errors.append(f"Invalid device ID: {config.device_id}")

        # 验证设备类型
        if not config.device_type or not config.device_type.strip():
            result.errors.append("Device type is required")

        # 验证协议
        if not config.protocol or not config.protocol.strip():
            result.errors.append("Protocol is required")
        protocol = config.protocol.lower()
        if protocol == "opcda":
            result.errors.append(
                "OPC DA is a 32-bit COM/DCOM protocol in this project baseline; "
                "do not connect it directly from the 64-bit collector. "
                "Use a 32-bit sidecar service or an OPC UA gateway, then ingest via HTTP/OPC UA."
            )
        elif protocol not in ["s7", "ge_srtp", "opc", "opcua", "http_push",
                              "opcda_sidecar", "opcda_center"]:
            result.warnings.append(f"Unknown protocol: {config.protocol}")

        # 验证IP地址
        if config.ip:
            try:
                ipaddress.ip_address(config.ip)
            except ValueError:
                if not self._is_valid_hostname(config.ip):
                    result.errors.append(f"Invalid device IP address: {config.ip}")

        # 验证端口
        if config.port and not (1 <= config.port <= 65535):
            result.errors.append(f"Invalid device port: {config.port}")

        # 验证扫描周期
        if config.scan_rate <= 0:
            result.errors.append(f"Invalid scan rate: {config.scan_rate}")
        elif config.scan_rate > 3600:  # 最大1小时
            result.warnings.append(f"Scan rate is very large: {config.scan_rate}s")

        return result

    def _validate_mysql_config(self, config: MySQLConfig) -> ValidationResult:
        """验证 MySQL 配置"""
        result = ValidationResult(valid=True)

        if not config.host or not config.host.strip():
            result.errors.append("MySQL host is required")

        if not (1 <= config.port <= 65535):
            result.errors.append(f"Invalid MySQL port: {config.port}")

        if not config.user or not config.user.strip():
            result.errors.append("MySQL username is required")

        if not config.database or not re.match(r'^[a-zA-Z0-9_]+$', config.database):
            result.errors.append("Invalid MySQL database name")

        return result

    def _validate_variable_config(
        self,
        config: VariableConfig,
        protocol: Optional[str] = None,
    ) -> ValidationResult:
        """验证变量配置"""
        result = ValidationResult(valid=True)

        # 验证变量ID
        if not config.variable_id or not re.match(r'^[a-zA-Z0-9_]+$', config.variable_id):
            result.errors.append(f"Invalid variable ID: {config.variable_id}")

        if config.var_seq <= 0:
            result.errors.append(f"Invalid variable sequence: {config.var_seq}")

        # 验证设备ID引用
        if not config.device_id or not config.device_id.strip():
            result.errors.append("Device ID is required for variable")

        # 验证地址格式
        address_valid = bool(config.address) and self._validate_address(
            config.address,
            protocol=protocol,
        )
        if not address_valid:
            result.errors.append(f"Invalid variable address: {config.address}")

        # 验证数据类型（含 GE PLC 无符号类型、S7 Timer/Counter）
        valid_types = [
            "BOOL", "BOOLEAN", "BYTE",
            "INT", "WORD", "UINT", "USINT",
            "REAL", "FLOAT",
            "DINT", "DWORD", "UDINT",
            "STRING",
            "TIMER_MS", "COUNTER",  # S7 Timer/Counter
        ]
        data_type_valid = bool(config.data_type) and config.data_type.upper() in valid_types
        if not data_type_valid:
            result.errors.append(f"Invalid data type: {config.data_type}")
        elif address_valid and (protocol or "").strip().lower() in {"s7", "snap7", "siemens"}:
            ok, message = self._validate_s7_address_data_type(config.address, config.data_type)
            if not ok:
                result.errors.append(message)

        # 验证扫描周期
        if config.scan_rate <= 0:
            result.errors.append(f"Invalid scan rate: {config.scan_rate}")

        # 验证是否存储
        if not isinstance(config.is_storage, bool):
            result.errors.append("is_storage must be boolean")

        return result

    def _validate_global_config(self, config: CollectorConfig) -> ValidationResult:
        """验证全局配置"""
        result = ValidationResult(valid=True)

        # 验证外部接口采集配置
        ingest = config.interface_ingest
        if ingest.enabled:
            if not ingest.path.startswith("/"):
                result.errors.append("interface_ingest.path must start with '/'")
            if not (1 <= ingest.port <= 65535):
                result.errors.append(f"Invalid interface ingest port: {ingest.port}")
            if ingest.max_batch_size <= 0:
                result.errors.append("interface_ingest.max_batch_size must be > 0")
            # 安全检查：绑定公网地址时必须配置非空 API Key
            host = (ingest.host or "").strip()
            api_key = (ingest.api_key or "").strip()
            if host in {"0.0.0.0", "::"} and not api_key:
                result.errors.append(
                    f"interface_ingest.api_key is required when binding to {host}. "
                    "Either set a non-empty api_key or bind to 127.0.0.1 for local-only access."
                )

        # 验证 Redis 配置（取模分片、Stream 长度、连接参数）
        redis_cfg = config.redis
        if redis_cfg.stream_shard_count < 1:
            result.errors.append(
                f"redis.stream_shard_count must be >= 1, got {redis_cfg.stream_shard_count}"
            )
        if redis_cfg.stream_maxlen < 1:
            result.errors.append(
                f"redis.stream_maxlen must be >= 1, got {redis_cfg.stream_maxlen}"
            )
        if not (1 <= redis_cfg.port <= 65535):
            result.errors.append(f"Invalid redis port: {redis_cfg.port}")
        if redis_cfg.db < 0:
            result.errors.append(f"Invalid redis db: {redis_cfg.db}")

        # 验证设备与变量的关系
        device_ids = {dev.device_id for dev in config.devices}
        for var in config.variables:
            if var.device_id not in device_ids:
                result.errors.append(f"Variable {var.variable_id} references unknown device {var.device_id}")

        return result

    def _validate_address(self, address: str, protocol: Optional[str] = None) -> bool:
        """验证地址格式"""
        normalized_protocol = (protocol or "").strip().lower()
        if normalized_protocol in {"s7", "snap7", "siemens"}:
            return self._validate_s7_address(address)
        if normalized_protocol == "ge_srtp":
            return self._validate_ge_address(address)
        if normalized_protocol in {"opcda", "opcda_center", "opcda_sidecar"}:
            return self._validate_opcda_item_id(address)
        if normalized_protocol in {"opc", "opcua", "opc_ua", "http_push"}:
            return bool(address and address.strip())

        # Unknown protocol: accept only known read address families.
        return self._validate_s7_address(address) or self._validate_ge_address(address)

    @staticmethod
    def _validate_s7_address(address: str) -> bool:
        """验证 S7 标准 DB/I/Q/M/T/C 地址，拒绝旧式 DB<num>.<offset>。"""
        upper = address.upper()

        # Timer/Counter: %T<num> or %C<num>
        tc_match = re.match(r'^%?[TC](\d+)$', upper)
        if tc_match:
            return True

        db_match = re.match(r'^%?DB\d+\.(DBX|DBB|DBW|DBD)(\d+)(?:\.([0-7]))?$', upper)
        if db_match:
            access = db_match.group(1)
            byte_offset = int(db_match.group(2))
            bit_suffix = db_match.group(3)
            if access == "DBX":
                return bit_suffix is not None
            if bit_suffix is not None:
                return False
            if access in {"DBW", "DBD"} and byte_offset % 2 != 0:
                return False
            return True

        area_match = re.match(r'^%?[IQM](B|W|D)?(\d+)(?:\.([0-7]))?$', upper)
        if not area_match:
            return False
        width = area_match.group(1)
        bit_suffix = area_match.group(3)
        if bit_suffix is not None:
            return width is None
        return width is not None

    @staticmethod
    def _validate_s7_address_data_type(address: str, data_type: str) -> Tuple[bool, str]:
        """验证 S7 地址宽度与数据类型组合，提前拦截运行期读取错误。"""
        upper = address.upper()
        dt = (data_type or "").upper()

        db_match = re.match(r'^%?DB\d+\.(DBX|DBB|DBW|DBD)(\d+)(?:\.([0-7]))?$', upper)
        if db_match:
            access = db_match.group(1)
            allowed = {
                "DBX": {"BOOL"},
                "DBB": {"BYTE"},
                "DBW": {"INT", "WORD"},
                "DBD": {"DINT", "DWORD", "REAL", "FLOAT"},
            }[access]
            if dt in allowed:
                return True, ""
            return (
                False,
                f"Invalid S7 address/data_type combination: {address} requires one of "
                f"{sorted(allowed)}, got {data_type}",
            )

        area_match = re.match(r'^%?[IQM](B|W|D)?(\d+)(?:\.([0-7]))?$', upper)
        if area_match:
            width = area_match.group(1)
            bit_suffix = area_match.group(3)
            if bit_suffix is not None:
                allowed = {"BOOL"}
            elif width == "B":
                allowed = {"BYTE"}
            elif width == "W":
                allowed = {"INT", "WORD"}
            elif width == "D":
                allowed = {"DINT", "DWORD", "REAL", "FLOAT"}
            else:
                allowed = set()

            if dt in allowed:
                return True, ""
            return (
                False,
                f"Invalid S7 address/data_type combination: {address} requires one of "
                f"{sorted(allowed)}, got {data_type}",
            )

        # Timer: %T<num> 支持 TIMER_MS（毫秒）或 WORD（原始值）
        timer_match = re.match(r'^%?T(\d+)$', upper)
        if timer_match:
            allowed = {"TIMER_MS", "WORD", "INT"}
            if dt in allowed:
                return True, ""
            return (
                False,
                f"Invalid S7 Timer data_type: {address} requires one of "
                f"{sorted(allowed)}, got {data_type}",
            )

        # Counter: %C<num> 支持 COUNTER 或 WORD（原始值）
        counter_match = re.match(r'^%?C(\d+)$', upper)
        if counter_match:
            allowed = {"COUNTER", "WORD", "INT"}
            if dt in allowed:
                return True, ""
            return (
                False,
                f"Invalid S7 Counter data_type: {address} requires one of "
                f"{sorted(allowed)}, got {data_type}",
            )

        return True, ""

    @staticmethod
    def _validate_ge_address(address: str) -> bool:
        """验证 GE SRTP 地址。"""
        return bool(re.match(r'^%(R|AI|AQ|I|Q|M)\d+$', address.upper()))

    @staticmethod
    def _validate_opcda_item_id(address: str) -> bool:
        """验证 OPC DA ItemID：非空，并且由点号分隔的非空片段组成。"""
        if not address or not isinstance(address, str):
            return False
        value = address.strip()
        if not value:
            return False
        parts = value.split(".")
        if len(parts) < 2:
            return False
        return all(part.strip() for part in parts)

    @staticmethod
    def _is_valid_hostname(value: str) -> bool:
        """允许 localhost / 内网 DNS 名称，避免只接受 IP。"""
        if value == "localhost":
            return True
        try:
            socket.getaddrinfo(value, None)
            return True
        except socket.gaierror:
            return bool(re.match(r"^[A-Za-z0-9][A-Za-z0-9.-]{0,252}$", value))

    def suggest_improvements(self, config: CollectorConfig) -> List[str]:
        """提供配置改进建议"""
        suggestions = []

        # 检查是否有未使用的变量
        device_ids = {dev.device_id for dev in config.devices}
        unused_vars = [var for var in config.variables if var.device_id not in device_ids]
        if unused_vars:
            suggestions.append(f"Consider removing {len(unused_vars)} unused variables")

        # 检查扫描周期
        fast_scanning = [dev for dev in config.devices if dev.scan_rate == 1]
        if len(fast_scanning) > 10:
            suggestions.append(f"Many devices ({len(fast_scanning)}) are scanning at 1s interval, consider grouping")

        return suggestions


def validate_config(config: CollectorConfig) -> ValidationResult:
    """验证配置的便捷函数"""
    validator = ConfigValidator()
    return validator.validate_config(config)


def get_config_suggestions(config: CollectorConfig) -> List[str]:
    """获取配置建议的便捷函数"""
    validator = ConfigValidator()
    return validator.suggest_improvements(config)
