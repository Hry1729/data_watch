"""
启动前 dry-run 配置预检模块。

功能：
- 只校验不采集
- 输出设备数、点位数、跳过点、解析失败点
- 合并块数量统计
- 潜在不支持类型警告
"""

from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Set

from core.config import CollectorConfig
from core.logger import get_logger
from device.device import Device, Variable
from device.manager import DeviceManager

logger = get_logger("dry_run")


@dataclass
class DryRunResult:
    """Dry-run 预检结果"""
    # 基本统计
    total_devices: int = 0
    total_variables: int = 0
    storage_variables: int = 0
    skipped_variables: int = 0

    # 按协议统计
    devices_by_protocol: Dict[str, int] = field(default_factory=dict)
    variables_by_protocol: Dict[str, int] = field(default_factory=dict)

    # 地址解析结果
    parse_success: int = 0
    parse_failed: List[Dict[str, Any]] = field(default_factory=list)

    # 合并块统计
    total_read_blocks: int = 0
    read_blocks_by_protocol: Dict[str, int] = field(default_factory=dict)
    merge_ratio: float = 0.0

    # 数据类型统计
    data_types_used: Dict[str, int] = field(default_factory=dict)

    # 警告
    warnings: List[str] = field(default_factory=list)

    # 不支持的类型/地址
    unsupported_types: List[Dict[str, Any]] = field(default_factory=list)
    unsupported_addresses: List[Dict[str, Any]] = field(default_factory=list)

    def to_summary(self) -> str:
        """生成可读的摘要报告"""
        lines = [
            "=" * 60,
            "DRY-RUN 配置预检报告",
            "=" * 60,
            "",
            "【基本统计】",
            f"  设备总数: {self.total_devices}",
            f"  变量总数: {self.total_variables}",
            f"  需存储变量: {self.storage_variables}",
            f"  跳过变量 (is_storage=false): {self.skipped_variables}",
            "",
            "【按协议统计】",
        ]
        for protocol, count in sorted(self.devices_by_protocol.items()):
            vars_count = self.variables_by_protocol.get(protocol, 0)
            lines.append(f"  {protocol.upper()}: {count} 设备, {vars_count} 变量")

        lines.extend([
            "",
            "【地址解析】",
            f"  解析成功: {self.parse_success}",
            f"  解析失败: {len(self.parse_failed)}",
        ])

        if self.parse_failed:
            lines.append("  失败详情:")
            for item in self.parse_failed[:10]:
                lines.append(f"    - {item.get('variable_id', '?')}: {item.get('address', '?')} - {item.get('error', '?')}")
            if len(self.parse_failed) > 10:
                lines.append(f"    ... 还有 {len(self.parse_failed) - 10} 个失败")

        lines.extend([
            "",
            "【合并块统计】",
            f"  读取块总数: {self.total_read_blocks}",
            f"  合并比例: {self.merge_ratio:.2f} (变量数/块数)",
        ])
        for protocol, count in sorted(self.read_blocks_by_protocol.items()):
            lines.append(f"  {protocol.upper()}: {count} 块")

        lines.extend([
            "",
            "【数据类型使用】",
        ])
        for dtype, count in sorted(self.data_types_used.items()):
            lines.append(f"  {dtype}: {count}")

        if self.unsupported_types:
            lines.extend([
                "",
                "【不支持的数据类型】",
            ])
            for item in self.unsupported_types:
                lines.append(f"  - {item.get('variable_id', '?')}: {item.get('data_type', '?')} ({item.get('protocol', '?')})")

        if self.unsupported_addresses:
            lines.extend([
                "",
                "【不支持的地址格式】",
            ])
            for item in self.unsupported_addresses[:10]:
                lines.append(f"  - {item.get('variable_id', '?')}: {item.get('address', '?')} ({item.get('error', '?')})")
            if len(self.unsupported_addresses) > 10:
                lines.append(f"  ... 还有 {len(self.unsupported_addresses) - 10} 个")

        if self.warnings:
            lines.extend([
                "",
                "【警告】",
            ])
            for warn in self.warnings:
                lines.append(f"  - {warn}")

        lines.extend([
            "",
            "=" * 60,
        ])
        return "\n".join(lines)


def run_dry_run(config: CollectorConfig) -> DryRunResult:
    """
    执行 dry-run 配置预检。

    参数：
        config: 采集器配置

    返回：
        DryRunResult: 预检结果
    """
    result = DryRunResult()

    # 基本统计
    result.total_devices = len(config.devices)
    result.total_variables = len(config.variables)

    for var in config.variables:
        if var.is_storage:
            result.storage_variables += 1
        else:
            result.skipped_variables += 1

    # 按协议统计
    for dev in config.devices:
        protocol = dev.protocol.lower()
        result.devices_by_protocol[protocol] = result.devices_by_protocol.get(protocol, 0) + 1

    for var in config.variables:
        # 查找变量对应的设备协议
        dev_protocol = None
        for dev in config.devices:
            if dev.device_id == var.device_id:
                dev_protocol = dev.protocol.lower()
                break
        if dev_protocol:
            result.variables_by_protocol[dev_protocol] = result.variables_by_protocol.get(dev_protocol, 0) + 1

    # 构建设备并检查地址解析
    status_reporter = None  # dry-run 不需要状态上报
    device_manager = DeviceManager(config, status_reporter=status_reporter)
    device_manager.register_devices()

    all_devices = device_manager.get_all_devices()

    for device in all_devices:
        # 构建 scan_tasks 会触发地址解析和合并
        try:
            device.build_scan_tasks()
        except Exception as e:
            result.parse_failed.append({
                "device_id": device.device_id,
                "error": str(e),
            })
            continue

        protocol = device.protocol.lower()

        # 统计读取块
        for scan_task in device.scan_tasks:
            result.total_read_blocks += len(scan_task.read_blocks)
            result.read_blocks_by_protocol[protocol] = (
                result.read_blocks_by_protocol.get(protocol, 0) + len(scan_task.read_blocks)
            )

        # 统计数据类型
        for var in device.variables:
            dtype = (var.data_type or "").upper()
            result.data_types_used[dtype] = result.data_types_used.get(dtype, 0) + 1

            # 检查是否在支持列表中
            supported_types = {
                "BOOL", "BOOLEAN", "BYTE",
                "INT", "WORD", "UINT", "USINT",
                "REAL", "FLOAT",
                "DINT", "DWORD", "UDINT",
                "STRING",
                "TIMER_MS", "COUNTER",  # S7 Timer/Counter
            }
            if dtype not in supported_types:
                result.unsupported_types.append({
                    "variable_id": var.variable_id,
                    "data_type": var.data_type,
                    "protocol": protocol,
                })

        result.parse_success += len(device.variables)

    # 计算合并比例
    if result.total_read_blocks > 0:
        result.merge_ratio = result.total_variables / result.total_read_blocks

    # 检查潜在问题
    _check_potential_issues(config, result)

    return result


def _check_potential_issues(config: CollectorConfig, result: DryRunResult) -> None:
    """检查潜在问题"""
    # 检查变量引用不存在的设备
    device_ids = {dev.device_id for dev in config.devices}
    for var in config.variables:
        if var.device_id not in device_ids:
            result.warnings.append(
                f"变量 {var.variable_id} 引用了不存在的设备 {var.device_id}"
            )

    # 检查空设备
    devices_with_vars: Set[str] = set()
    for var in config.variables:
        devices_with_vars.add(var.device_id)
    for dev in config.devices:
        if dev.device_id not in devices_with_vars:
            result.warnings.append(
                f"设备 {dev.device_id} 没有关联的变量"
            )

    # 检查重复变量 ID
    var_ids: Set[str] = set()
    duplicate_ids: Set[str] = set()
    for var in config.variables:
        if var.variable_id in var_ids:
            duplicate_ids.add(var.variable_id)
        else:
            var_ids.add(var.variable_id)
    if duplicate_ids:
        result.warnings.append(
            f"存在重复的变量 ID: {', '.join(sorted(duplicate_ids))}"
        )

    # 检查短采集周期（可能压力大）
    fast_scan_devices = [dev for dev in config.devices if dev.scan_rate < 1]
    if fast_scan_devices:
        result.warnings.append(
            f"{len(fast_scan_devices)} 个设备的采集周期小于 1 秒，可能导致压力"
        )

    # 检查 GE 离散区非 BOOL（已有限制，但提醒）
    ge_discrete_non_bool = 0
    for var in config.variables:
        for dev in config.devices:
            if dev.device_id == var.device_id and dev.protocol.lower() == "ge_srtp":
                addr = var.address.upper()
                if any(addr.startswith(f"%{a}") for a in ["I", "Q", "M"]):
                    if var.data_type.upper() != "BOOL":
                        ge_discrete_non_bool += 1
    if ge_discrete_non_bool > 0:
        result.warnings.append(
            f"{ge_discrete_non_bool} 个 GE 离散区变量使用非 BOOL 类型，第一版不支持"
        )

    # 检查 OPC DA 直接连接（应该用 sidecar）
    direct_opcda = [dev for dev in config.devices if dev.protocol.lower() == "opcda"]
    if direct_opcda:
        result.warnings.append(
            f"{len(direct_opcda)} 个设备使用 opcda 协议，应使用 opcda_sidecar 或 opcda_center"
        )


async def dry_run_main(config: CollectorConfig) -> int:
    """
    Dry-run 主入口。

    参数：
        config: 采集器配置

    返回：
        int: 退出码 (0=成功, 1=有问题)
    """
    logger.info("Starting dry-run configuration pre-check...")

    result = run_dry_run(config)

    # 输出报告
    print(result.to_summary())

    # 记录到日志
    logger.info("Dry-run completed: devices=%d, variables=%d, blocks=%d, ratio=%.2f",
                result.total_devices,
                result.total_variables,
                result.total_read_blocks,
                result.merge_ratio)

    # 返回退出码
    if result.parse_failed or result.unsupported_types:
        return 1
    return 0
