"""设备管理器"""

import asyncio
import json
import os
import random
import socket
import threading
import time
from typing import Any, Awaitable, Callable, Dict, List, Optional, TYPE_CHECKING

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.config_validator import ConfigValidator
from core.exceptions import DeviceConnectionError
from core.logger import get_logger
from core.retry import with_retry, CircuitBreaker
from device.device import Device, DeviceStatus, Variable

if TYPE_CHECKING:
    from drivers.base import BaseDriver
    from drivers.registry import DriverRegistry
    from monitor.performance import PerformanceMetrics

logger = get_logger("device_manager")

# 端口映射文件路径（与 check_protocols.py 共享）
PORT_MAPPING_FILE = os.path.join(os.path.dirname(__file__), "..", "port_mapping.json")


EXTERNAL_PUSH_PROTOCOLS = {"http_push", "opcda_sidecar"}

# Modbus TCP 从站号合法范围（不含广播 0）
_MODBUS_UNIT_ID_MIN = 1
_MODBUS_UNIT_ID_MAX = 247

# 覆盖旧驱动前 disconnect 超时（秒）；超时后继续推进，避免重连永久卡住
_DRIVER_DISCONNECT_TIMEOUT_SEC = 5.0

# P0 修复：进程级锁保护注册表初始化，避免多线程并发初始化时重复扫描/导入
_REGISTRY_INIT_LOCK = threading.Lock()


def _modbus_unit_id_from_slot(slot: Optional[int]) -> int:
    """将 device.slot 映射为 Modbus unit_id；仅 slot is None 时默认 1。"""
    unit_id = _MODBUS_UNIT_ID_MIN if slot is None else int(slot)
    if not (_MODBUS_UNIT_ID_MIN <= unit_id <= _MODBUS_UNIT_ID_MAX):
        raise ValueError(
            f"Modbus unit_id 必须在 {_MODBUS_UNIT_ID_MIN}-{_MODBUS_UNIT_ID_MAX}，"
            f"当前 slot={slot!r} → unit_id={unit_id}"
        )
    return unit_id


def build_connection_config(dev_cfg: DeviceConfig) -> Dict[str, Any]:
    """组装设备连接配置；modbus_tcp 额外映射 slot→unit_id。"""
    config: Dict[str, Any] = {
        "ip": dev_cfg.ip,
        "endpoint": dev_cfg.endpoint,
        "port": dev_cfg.port,
        "rack": dev_cfg.rack,
        "slot": dev_cfg.slot,
        "opc_server": dev_cfg.opc_server,
        "read_timeout_ms": dev_cfg.read_timeout_ms,
    }
    if (dev_cfg.protocol or "").lower() == "modbus_tcp":
        config["unit_id"] = _modbus_unit_id_from_slot(dev_cfg.slot)
    return config

# 中心采集协议（通过代理进程远程采集）
CENTER_COLLECTION_PROTOCOLS = {"opcda_center"}


class _NullMetrics:
    def increment(self, _name: str, _amount: int = 1) -> None:
        pass

    def record_timing(self, _name: str, _value_ms: float) -> None:
        pass

    def set_gauge(self, _name: str, _value: int | float) -> None:
        pass


def is_external_push_protocol(protocol: str) -> bool:
    """无需 PLC driver 的外部推送协议。"""
    return (protocol or "").strip().lower() in EXTERNAL_PUSH_PROTOCOLS


def is_center_collection_protocol(protocol: str) -> bool:
    """中心采集协议（通过代理进程远程采集）。"""
    return (protocol or "").strip().lower() in CENTER_COLLECTION_PROTOCOLS


class PortMappingError(Exception):
    """权威 mapping_file 缺失/损坏，或请求的 endpoint 不在权威映射中。"""


class EndpointPortMapper:
    """
    VM endpoint 到代理端口的动态映射器

    功能：
    - 为每个不同的 VM endpoint 分配独立端口
    - 支持从文件加载已有映射（与 check_protocols.py 共享）
    - 检测端口可用性，自动跳过被占用端口
    - 从 base_port 开始依次分配
    """

    def __init__(
        self,
        base_port: int = 9090,
        max_port: int = 9200,
        load_mapping_file: bool = True,
        mapping_file: Optional[str] = None,
    ):
        self._base_port = base_port
        self._max_port = max_port
        self._endpoint_to_port: Dict[str, int] = {}
        self._next_port = base_port
        self._lock = asyncio.Lock()
        # 显式 mapping_file → 权威只读模式：只读该文件、不探测/不分配/不写回。
        self._authoritative = mapping_file is not None
        self._mapping_file = mapping_file

        if mapping_file is not None:
            self._load_authoritative(mapping_file)
        elif load_mapping_file:
            self._load_mapping_from_file()

    def _load_authoritative(self, mapping_file: str) -> None:
        """加载权威映射文件；缺失或损坏即 fail-fast，避免静默退化成动态分配致漂移。"""
        if not os.path.exists(mapping_file):
            raise PortMappingError(f"权威端口映射文件不存在: {mapping_file}")
        try:
            with open(mapping_file, "r", encoding="utf-8") as f:
                data = json.load(f)
        except (OSError, ValueError) as e:
            raise PortMappingError(f"权威端口映射文件无法解析: {mapping_file}: {e}") from e
        if not isinstance(data, dict):
            raise PortMappingError(f"权威端口映射文件格式非法(须为对象): {mapping_file}")
        for endpoint, port in data.items():
            # 损坏的端口值（非 int / bool / 越界）须 fail-fast，不能静默丢弃成"缺失"。
            if type(port) is not int or not (1 <= port <= 65535):
                raise PortMappingError(
                    f"权威端口映射文件含非法端口: {mapping_file} {endpoint!r}={port!r}"
                )
            self._endpoint_to_port[endpoint] = port
        logger.info("加载权威端口映射 %s: %s", mapping_file, self._endpoint_to_port)

    def reload(self) -> None:
        """权威模式下重新读取端口映射文件；非权威模式不做动态重载。"""
        if not self._authoritative or self._mapping_file is None:
            return
        previous = self._endpoint_to_port
        self._endpoint_to_port = {}
        try:
            self._load_authoritative(self._mapping_file)
        except Exception:
            self._endpoint_to_port = previous
            raise

    def _load_mapping_from_file(self) -> None:
        """从文件加载端口映射"""
        mapping_file = os.path.abspath(PORT_MAPPING_FILE)
        if os.path.exists(mapping_file):
            try:
                with open(mapping_file, "r", encoding="utf-8") as f:
                    data = json.load(f)
                if isinstance(data, dict):
                    for endpoint, port in data.items():
                        if isinstance(port, int) and self._base_port <= port <= self._max_port:
                            self._endpoint_to_port[endpoint] = port
                            # 更新 next_port 为已分配端口+1
                            if port >= self._next_port:
                                self._next_port = port + 1
                    logger.info("从 %s 加载端口映射: %s", mapping_file, self._endpoint_to_port)
            except Exception as e:
                logger.warning("加载端口映射文件失败: %s", e)

    def _save_mapping_to_file(self) -> None:
        """保存端口映射到文件"""
        mapping_file = os.path.abspath(PORT_MAPPING_FILE)
        try:
            with open(mapping_file, "w", encoding="utf-8") as f:
                json.dump(self._endpoint_to_port, f, indent=2, ensure_ascii=False)
            logger.debug("保存端口映射到 %s", mapping_file)
        except Exception as e:
            logger.warning("保存端口映射文件失败: %s", e)

    def _is_port_available(self, port: int) -> bool:
        """检测端口是否可用"""
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                s.bind(("127.0.0.1", port))
                return True
        except OSError:
            return False

    def _find_next_available_port(self) -> int:
        """找到下一个可用端口（同步版本，用于初始化）"""
        while self._next_port <= self._max_port:
            if self._is_port_available(self._next_port):
                port = self._next_port
                self._next_port += 1
                return port
            logger.warning("端口 %d 已被占用，跳过", self._next_port)
            self._next_port += 1
        raise RuntimeError(f"无可用端口，范围 {self._base_port}-{self._max_port} 已耗尽")

    async def _find_next_available_port_async(self) -> int:
        """找到下一个可用端口（异步版本）"""
        while self._next_port <= self._max_port:
            if self._is_port_available(self._next_port):
                port = self._next_port
                self._next_port += 1
                return port
            logger.warning("端口 %d 已被占用，跳过", self._next_port)
            self._next_port += 1
        raise RuntimeError(f"无可用端口，范围 {self._base_port}-{self._max_port} 已耗尽")

    def get_port(self, endpoint: str) -> int:
        """获取或分配 endpoint 对应的端口（同步版本）"""
        if not endpoint or endpoint in ("127.0.0.1", "localhost"):
            endpoint = "localhost"

        endpoint = endpoint.strip()

        # 权威只读：命中返回文件端口；缺失即报错，绝不分配/写回。
        if self._authoritative:
            return self._authoritative_port(endpoint)

        if endpoint not in self._endpoint_to_port:
            self._endpoint_to_port[endpoint] = self._find_next_available_port()
            logger.info("为 endpoint %s 分配端口 %d", endpoint, self._endpoint_to_port[endpoint])
            self._save_mapping_to_file()

        return self._endpoint_to_port[endpoint]

    def _authoritative_port(self, endpoint: str) -> int:
        """权威模式查表（已归一）：命中返回，缺失抛错。同步/异步两入口共用。"""
        if endpoint not in self._endpoint_to_port:
            raise PortMappingError(
                f"endpoint 不在权威端口映射中: {endpoint} "
                f"(文件={self._mapping_file} 已知={sorted(self._endpoint_to_port)})"
            )
        return self._endpoint_to_port[endpoint]

    async def get_port_async(self, endpoint: str) -> int:
        """获取或分配 endpoint 对应的端口（异步版本）"""
        async with self._lock:
            if not endpoint or endpoint in ("127.0.0.1", "localhost"):
                endpoint = "localhost"

            endpoint = endpoint.strip()

            # 权威只读：与同步 get_port 对称，权威模式绝不进入动态分配分支。
            if self._authoritative:
                return self._authoritative_port(endpoint)

            if endpoint not in self._endpoint_to_port:
                self._endpoint_to_port[endpoint] = await self._find_next_available_port_async()
                logger.info("为 endpoint %s 分配端口 %d", endpoint, self._endpoint_to_port[endpoint])
                self._save_mapping_to_file()

            return self._endpoint_to_port[endpoint]

    def build_from_devices(self, devices: List[DeviceConfig]) -> None:
        """从设备配置构建映射（启动时预分配）"""
        for dev in devices:
            if is_center_collection_protocol(dev.protocol):
                self.get_port(dev.endpoint)

    def get_mapping(self) -> Dict[str, int]:
        """获取完整的 endpoint -> port 映射表"""
        return dict(self._endpoint_to_port)

    def get_proxy_configs(self) -> List[Dict[str, Any]]:
        """生成代理进程配置列表"""
        configs = []
        for endpoint, port in self._endpoint_to_port.items():
            if endpoint != "localhost":
                configs.append({
                    "vm_host": endpoint,
                    "proxy_port": port,
                })
        return configs


class DeviceManager:
    """管理所有设备：注册、查找、连接、状态监控"""

    def __init__(
        self,
        config: CollectorConfig,
        status_reporter: Optional[Callable[[str, int, str, str], Awaitable[None]]] = None,
        port_mapping_file: Optional[str] = None,
        metrics: Optional["PerformanceMetrics"] = None,
        read_executor=None,
        driver_registry: Optional["DriverRegistry"] = None,
    ):
        self.config = config
        # 专用 PLC 读线程池（§7.3），注入给阻塞型 PLC 驱动；None 时驱动回退默认 executor。
        self._read_executor = read_executor
        self._status_reporter = status_reporter
        # 显式权威端口映射文件（Agent 生成）；None 时沿用动态分配（dev 期 run.bat 链路）。
        self._port_mapping_file = port_mapping_file
        self.devices: Dict[str, Device] = {}
        self._lock = asyncio.Lock()
        self._generation_registry_lock = threading.Lock()
        self._generation_locks: Dict[str, threading.RLock] = {}
        self._device_generations: Dict[str, int] = {}
        # 设备连接熔断器
        self._circuit_breakers: Dict[str, CircuitBreaker] = {}
        # 重连任务
        self._reconnect_tasks: Dict[str, asyncio.Task] = {}
        self._metrics = metrics or _NullMetrics()
        self._metrics.increment("reconnect_scheduled_count", 0)
        self._metrics.increment("reconnect_attempt_count", 0)
        self._metrics.increment("reconnect_success_count", 0)
        self._metrics.increment("reconnect_failure_count", 0)
        self._metrics.set_gauge("reconnect_active", 0)
        self._metrics.set_gauge("reconnect_peak_active", 0)
        self._reconnect_peak_active = 0
        # 最大重试次数
        self._max_retries = 3
        # 重连间隔（秒）
        self._reconnect_interval = 30
        self._reconnect_max_interval = 300
        self._reconnect_jitter_ratio = 0.2
        self._random = random.Random()
        self._sleep = asyncio.sleep
        self._connect_semaphore = asyncio.Semaphore(10)
        # VM endpoint 到代理端口的映射器
        self._port_mapper: Optional[EndpointPortMapper] = None
        # 统一驱动注册表；None 时合并 bundled 与 DATAWATCH_DATA_ROOT/drivers 后懒加载
        with _REGISTRY_INIT_LOCK:
            self._driver_registry: Optional["DriverRegistry"] = (
                driver_registry
            )
        self._driver_registry_scanned = driver_registry is not None

    def _build_connection_config(self, dev_cfg: DeviceConfig) -> Dict[str, Any]:
        config = build_connection_config(dev_cfg)
        if (dev_cfg.protocol or "").lower() == "ge_srtp":
            configured = dev_cfg.ge_max_payload_bytes
            config["ge_max_payload_bytes"] = (
                configured
                if configured is not None
                else self.config.ge_max_payload_bytes
            )
            configured_channels = dev_cfg.ge_parallel_channels
            config["ge_parallel_channels"] = (
                configured_channels
                if configured_channels is not None
                else self.config.ge_parallel_channels
            )
            configured_deadline = dev_cfg.ge_batch_deadline_ms
            batch_deadline_ms = (
                configured_deadline
                if configured_deadline is not None
                else self.config.ge_batch_deadline_ms
            )
            if batch_deadline_ms is not None:
                config["ge_batch_deadline_ms"] = batch_deadline_ms
        return config
    def _generation_lock_for(self, device_id: str) -> threading.RLock:
        with self._generation_registry_lock:
            return self._generation_locks.setdefault(device_id, threading.RLock())

    def _activate_device(self, device: Device) -> None:
        lock = self._generation_lock_for(device.device_id)
        with lock:
            generation = self._device_generations.get(device.device_id, 0) + 1
            self._device_generations[device.device_id] = generation
            device.generation = generation
            device.active = True
            self.devices[device.device_id] = device

    def _advance_device_generation(self, device: Device) -> None:
        lock = self._generation_lock_for(device.device_id)
        with lock:
            generation = self._device_generations.get(device.device_id, 0) + 1
            self._device_generations[device.device_id] = generation
            device.generation = generation
            device.active = True

    def invalidate_device(self, device_id: str) -> int:
        """先将设备置为 tombstone，使旧任务无法再写 Redis。"""
        lock = self._generation_lock_for(device_id)
        with lock:
            device = self.devices.get(device_id)
            if device is None or not device.active:
                return self._device_generations.get(device_id, 0)
            generation = self._device_generations.get(device_id, 0) + 1
            self._device_generations[device_id] = generation
            device.generation = generation
            device.active = False
            return generation

    def is_current_generation(self, device_id: str, generation: int) -> bool:
        lock = self._generation_lock_for(device_id)
        with lock:
            device = self.devices.get(device_id)
            return bool(
                device is not None
                and device.active
                and device.generation == generation
                and self._device_generations.get(device_id) == generation
            )

    def current_generation(self, device_id: str) -> Optional[int]:
        """返回活跃设备当前代次；未注册或 tombstone 设备返回 None。"""
        lock = self._generation_lock_for(device_id)
        with lock:
            device = self.devices.get(device_id)
            if device is None or not device.active:
                return None
            return self._device_generations.get(device_id)

    def run_if_current_generation(
        self,
        device_id: str,
        generation: int,
        action: Callable[[], None],
    ) -> bool:
        """在同设备 generation 锁内验证并执行，阻止 invalidate 与写入交错。"""
        lock = self._generation_lock_for(device_id)
        with lock:
            device = self.devices.get(device_id)
            if not (
                device is not None
                and device.active
                and device.generation == generation
                and self._device_generations.get(device_id) == generation
            ):
                return False
            action()
            return True

    def register_devices(self) -> None:
        """根据配置注册所有设备"""
        # 初始化端口映射器
        self._port_mapper = EndpointPortMapper(mapping_file=self._port_mapping_file)

        # 从配置创建设备
        device_map: Dict[str, Device] = {}
        for dev_cfg in self.config.devices:
            try:
                connection_config = self._build_connection_config(dev_cfg)
                config_error = None
            except Exception as exc:
                connection_config = {
                    "ip": dev_cfg.ip,
                    "endpoint": dev_cfg.endpoint,
                    "port": dev_cfg.port,
                    "rack": dev_cfg.rack,
                    "slot": dev_cfg.slot,
                    "opc_server": dev_cfg.opc_server,
        "read_timeout_ms": dev_cfg.read_timeout_ms,
                }
                config_error = str(exc)
            device = Device(
                device_id=dev_cfg.device_id,
                device_type=dev_cfg.device_type,
                protocol=dev_cfg.protocol,
                connection_config=connection_config,
                device_pk=dev_cfg.device_pk,
                config_version=dev_cfg.config_version,
            )
            if config_error is not None:
                if (
                    self._is_external_protocol(device.protocol)
                    or device.protocol.lower() == "modbus_tcp"
                ):
                    self._mark_config_error(device, config_error)
                else:
                    raise ValueError(config_error)
            device_map[dev_cfg.device_id] = device

        # 将变量分配到对应设备
        for var_cfg in self.config.variables:
            device = device_map.get(var_cfg.device_id)
            if device is None:
                logger.warning(
                    f"Variable {var_cfg.variable_id} references "
                    f"unknown device {var_cfg.device_id}"
                )
                continue
            variable = Variable(
                variable_id=var_cfg.variable_id,
                address=var_cfg.address,
                data_type=var_cfg.data_type,
                scan_rate=var_cfg.scan_rate,
                is_storage=var_cfg.is_storage,
                group_code=var_cfg.group_code,
                group_config_version=var_cfg.group_config_version,
            )
            device.add_variable(variable)

        # 为每个设备构建采集任务，同时预分配端口
        for device in device_map.values():
            if device.status == DeviceStatus.CONFIG_ERROR:
                continue
            try:
                self._setup_scan_tasks(device)
            except Exception as exc:
                if (
                    self._is_external_protocol(device.protocol)
                    or device.protocol.lower() == "modbus_tcp"
                ):
                    self._mark_config_error(device, str(exc))
                else:
                    raise

        self.devices = {}
        for device in device_map.values():
            self._activate_device(device)

        # 输出端口映射信息
        if self._port_mapper.get_mapping():
            logger.info("VM 端口映射表: %s", self._port_mapper.get_mapping())

        logger.info("Registered %s devices", len(self.devices))

    def _build_variables(self, var_cfgs: List[VariableConfig]) -> List[Variable]:
        """将 VariableConfig 列表转换为 Variable 实例列表。"""
        return [
            Variable(
                variable_id=var_cfg.variable_id,
                address=var_cfg.address,
                data_type=var_cfg.data_type,
                scan_rate=var_cfg.scan_rate,
                is_storage=var_cfg.is_storage,
                group_code=var_cfg.group_code,
                group_config_version=var_cfg.group_config_version,
            )
            for var_cfg in var_cfgs
        ]

    def validate_device_config(
        self, dev_cfg: DeviceConfig, var_cfgs: List[VariableConfig]
    ) -> Optional[str]:
        """在替换运行中设备前预建任务，失败时不触碰当前代际。"""
        try:
            candidate = Device(
                device_id=dev_cfg.device_id,
                device_type=dev_cfg.device_type,
                protocol=dev_cfg.protocol,
                connection_config=self._build_connection_config(dev_cfg),
                device_pk=dev_cfg.device_pk,
                config_version=dev_cfg.config_version,
            )
            for variable in self._build_variables(var_cfgs):
                candidate.add_variable(variable)
            self._setup_scan_tasks(candidate)
        except Exception as exc:
            return str(exc)
        return None

    def _setup_scan_tasks(self, device: Device) -> None:
        """按协议为设备构建采集任务（与 register_devices 同规则）。

        - external-push：无轮询任务
        - center：先分配代理端口再构建任务
        - 其余：直接构建任务
        """
        self._attach_external_hooks(device)
        if is_external_push_protocol(device.protocol):
            device.scan_tasks = []
            return
        if is_center_collection_protocol(device.protocol):
            if self._port_mapper is None:
                self._port_mapper = EndpointPortMapper(mapping_file=self._port_mapping_file)
            endpoint = device.connection_config.get("endpoint", "")
            try:
                proxy_port = self._port_mapper.get_port(endpoint)
            except PortMappingError:
                self._port_mapper.reload()
                proxy_port = self._port_mapper.get_port(endpoint)
            device.connection_config["proxy_port"] = proxy_port
            device.build_scan_tasks()
            device.config_error = None
            if device.status == DeviceStatus.CONFIG_ERROR:
                device.status = DeviceStatus.OFFLINE
            return
        device.build_scan_tasks()
        # 仅在成功规划后清除配置错误
        device.config_error = None
        if device.status == DeviceStatus.CONFIG_ERROR:
            device.status = DeviceStatus.OFFLINE

    def _attach_external_hooks(self, device: Device) -> None:
        """Attach optional planner/decode hooks from the unified registry."""
        registry = self._resolve_driver_registry()
        if registry.has_failed(device.protocol):
            reason = registry.failure_reason(device.protocol) or "未知加载错误"
            raise RuntimeError(f"驱动 {device.protocol} 加载失败: {reason}")
        device.external_driver_hooks = registry.get(device.protocol)

    def _is_external_protocol(self, protocol: str) -> bool:
        registry = self._resolve_driver_registry()
        registration = registry.get(protocol)
        return registry.has_failed(protocol) or bool(
            registration
            and (
                registration.source in {"installed", "official"}
                or registration.build_read_plan is not None
            )
        )

    @staticmethod
    def _mark_config_error(device: Device, message: str) -> None:
        """标记配置错误，保留设备可观测但禁止其进入采集与重连路径。"""
        device.status = DeviceStatus.CONFIG_ERROR
        device.config_error = message
        device.scan_tasks = []
        logger.error("Device %s configuration error: %s", device.device_id, message)

    def prepare_candidate_device(
        self, dev_cfg: DeviceConfig, var_cfgs: List[VariableConfig]
    ) -> Device:
        """构建候选设备（含 hooks/scan_tasks），不写入 self.devices。"""
        if self._port_mapper is None:
            self._port_mapper = EndpointPortMapper(mapping_file=self._port_mapping_file)

        try:
            connection_config = self._build_connection_config(dev_cfg)
            config_error = None
        except Exception as exc:
            connection_config = {
                "ip": dev_cfg.ip,
                "endpoint": dev_cfg.endpoint,
                "port": dev_cfg.port,
                "rack": getattr(dev_cfg, "rack", 0),
                "slot": getattr(dev_cfg, "slot", None),
                "opc_server": getattr(dev_cfg, "opc_server", None),
            }
            config_error = str(exc)

        device = Device(
            device_id=dev_cfg.device_id,
            device_type=dev_cfg.device_type,
            protocol=dev_cfg.protocol,
            connection_config=connection_config,
            device_pk=dev_cfg.device_pk,
            config_version=dev_cfg.config_version,
        )
        device.active = False
        for variable in self._build_variables(var_cfgs):
            device.add_variable(variable)
        if config_error is not None:
            self._mark_config_error(device, config_error)
            return device
        # B32：候选取代必须走与冷启动**同一套**变量校验（地址区/偏移/类型宽度组合），
        # 否则 `%MW10` + `REAL` 这类错配在冷启动被拒、热更新却被接受，等于热更新放宽校验。
        candidate_check = ConfigValidator().validate_device_candidate(dev_cfg, var_cfgs)
        if not candidate_check.valid:
            self._mark_config_error(device, "; ".join(candidate_check.errors))
            return device
        try:
            self._setup_scan_tasks(device)
        except Exception as exc:
            self._mark_config_error(device, str(exc))
        return device

    async def discard_candidate(self, candidate: Device) -> None:
        """丢弃未入表的候选：断开连接，不触碰 manager.devices。"""
        driver = candidate.driver
        candidate.driver = None
        candidate.active = False
        await self._safe_disconnect_driver(
            driver, device_id=candidate.device_id, reason="discard_candidate"
        )

    async def swap_device(self, device_id: str, candidate: Device) -> Optional[Device]:
        """原子替换 devices 表中的设备；返回被替换的旧 Device（可能为 None）。

        B3：热替换必须**一并收尾旧代的重连任务**。否则旧任务会继续按旧对象重连旧地址，
        而且因为 ``_reconnect_tasks[device_id]`` 仍被它占用，新代连
        ``schedule_reconnect`` 都会在"已有任务"处早退跳过——表现为新设备不再重连。
        等待旧任务在代际锁**之外**进行，避免持锁阻塞其它设备。
        """
        lock = self._generation_lock_for(device_id)
        with lock:
            old = self.devices.get(device_id)
            generation = self._device_generations.get(device_id, 0) + 1
            self._device_generations[device_id] = generation
            candidate.generation = generation
            candidate.active = True
            if old is not None:
                old.active = False
                old.generation = generation
            self.devices[device_id] = candidate

        # 代际已提升：先在锁外取消/收尾旧重连任务，再断开旧驱动。
        await self._cancel_reconnect_task(device_id)

        if old is not None:
            driver = old.driver
            old.driver = None
            await self._safe_disconnect_driver(
                driver, device_id=device_id, reason="swap_device_old"
            )
        return old

    async def _cancel_reconnect_task(self, device_id: str) -> None:
        """取消并收尾该设备的重连任务（幂等）。调用方须在代际锁之外 await。"""
        task = self._reconnect_tasks.pop(device_id, None)
        if task is None or task.done():
            return
        task.cancel()
        try:
            await task
        except asyncio.CancelledError:
            pass

    def add_device(
        self, dev_cfg: DeviceConfig, var_cfgs: List[VariableConfig]
    ) -> Optional[Device]:
        """运行期注册一台新设备。已存在则拒绝（不覆盖已有 Device）。"""
        if dev_cfg.device_id in self.devices:
            logger.warning(
                "add_device rejected: device %s already exists; use reload instead",
                dev_cfg.device_id,
            )
            return None

        if self._port_mapper is None:
            self._port_mapper = EndpointPortMapper(mapping_file=self._port_mapping_file)

        try:
            connection_config = self._build_connection_config(dev_cfg)
            config_error = None
        except Exception as exc:
            connection_config = {"ip": dev_cfg.ip, "endpoint": dev_cfg.endpoint, "port": dev_cfg.port}
            config_error = str(exc)
        device = Device(
            device_id=dev_cfg.device_id,
            device_type=dev_cfg.device_type,
            protocol=dev_cfg.protocol,
            connection_config=connection_config,
            device_pk=dev_cfg.device_pk,
            config_version=dev_cfg.config_version,
        )
        for variable in self._build_variables(var_cfgs):
            device.add_variable(variable)
        if config_error is not None:
            self._mark_config_error(device, config_error)
        else:
            try:
                self._setup_scan_tasks(device)
            except Exception as exc:
                self._mark_config_error(device, str(exc))
        self._activate_device(device)
        logger.info(
            "Runtime add_device %s protocol=%s vars=%d tasks=%d",
            device.device_id,
            device.protocol,
            len(device.variables),
            len(device.scan_tasks),
        )
        return device

    def rebuild_device_variables(
        self, device_id: str, var_cfgs: List[VariableConfig]
    ) -> Optional[Device]:
        """版本变更时原地重建某设备的变量与采集任务。"""
        device = self.devices.get(device_id)
        if device is None:
            return None
        prev_variables = device.variables
        prev_scan_tasks = device.scan_tasks
        try:
            device.variables = []
            for variable in self._build_variables(var_cfgs):
                device.add_variable(variable)
            self._setup_scan_tasks(device)
            device.config_error = None
            if device.status == DeviceStatus.CONFIG_ERROR:
                device.status = DeviceStatus.OFFLINE
        except Exception:
            device.variables = prev_variables
            device.scan_tasks = prev_scan_tasks
            raise
        self._advance_device_generation(device)
        return device

    async def remove_device(self, device_id: str) -> None:
        """运行期下线一台设备。

        固定顺序（调度循环由上层先行取消）：取消重连任务 → 断开 driver
        → 从 devices / 熔断器移除。不回写 DB 状态（仅日志），保留端口映射。
        """
        device = self.devices.get(device_id)
        if device is None:
            return
        self.invalidate_device(device_id)

        # 1) 取消重连任务，避免刚下线又被拉起
        await self._cancel_reconnect_task(device_id)

        # 2) 断开 driver（不回写 DB 状态，仅日志；超时/失败幂等）
        driver = device.driver
        device.driver = None
        await self._safe_disconnect_driver(
            driver, device_id=device_id, reason="remove_device"
        )
        device.status = DeviceStatus.OFFLINE

        # 3) 从设备表与熔断器移除（保留端口映射）
        self.devices.pop(device_id, None)
        self._circuit_breakers.pop(device_id, None)
        logger.info("Runtime remove_device %s done (port mapping kept)", device_id)

    def get_device(self, device_id: str) -> Optional[Device]:
        """获取设备实例"""
        return self.devices.get(device_id)

    def get_all_devices(self) -> List[Device]:
        """获取所有设备"""
        return list(self.devices.values())

    async def connect_device(self, device: Device, use_circuit_breaker: bool = True) -> bool:
        """连接单个设备"""
        if device.status == DeviceStatus.CONFIG_ERROR:
            return False
        if device.status == DeviceStatus.ONLINE:
            return True
        if is_external_push_protocol(device.protocol):
            device.driver = None
            device.status = DeviceStatus.ONLINE
            device.error_count = 0
            logger.info(
                "Device %s is external-push protocol=%s; skipping PLC connection",
                device.device_id,
                device.protocol,
            )
            await self._report_device_status(
                device,
                1,
                f"external push device registered: {device.protocol}",
                "connect",
            )
            return True

        if is_center_collection_protocol(device.protocol):
            # 中心采集协议：连接到代理进程（透传 use_circuit_breaker，
            # 否则重连路径 use_circuit_breaker=False 对 center 设备失效）
            return await self._connect_center_collection_device(device, use_circuit_breaker)

        # 获取设备的熔断器
        if device.device_id not in self._circuit_breakers:
            self._circuit_breakers[device.device_id] = CircuitBreaker(
                failure_threshold=5,
                recovery_timeout=60,
                expected_exception=DeviceConnectionError
            )

        circuit_breaker = self._circuit_breakers[device.device_id]

        # 使用熔断器保护连接
        if use_circuit_breaker:
            try:
                return await circuit_breaker(self._connect_device_impl)(device)
            except Exception as e:
                logger.error("Device %s connect failed (with circuit breaker): %s", device.device_id, e)
                device.status = DeviceStatus.ERROR
                await self._report_device_status(device, 0, f"connect failed: {e}", "connect")
                return False
        else:
            # 直接连接（用于重连）
            return await self._connect_device_impl(device)

    async def _safe_disconnect_driver(
        self,
        driver: Optional["BaseDriver"],
        *,
        device_id: str,
        reason: str,
    ) -> None:
        """带超时的幂等 disconnect：失败/超时/空实例均不抛，避免卡住重连。"""
        if driver is None:
            return
        try:
            await asyncio.wait_for(
                driver.disconnect(),
                timeout=_DRIVER_DISCONNECT_TIMEOUT_SEC,
            )
        except asyncio.TimeoutError:
            logger.warning(
                "Device %s driver disconnect timed out (%.1fs) reason=%s",
                device_id,
                _DRIVER_DISCONNECT_TIMEOUT_SEC,
                reason,
            )
        except Exception as exc:
            logger.warning(
                "Device %s driver disconnect failed reason=%s: %s",
                device_id,
                reason,
                exc,
            )

    @with_retry(max_retries=3, base_delay=1.0, backoff_factor=2.0)
    async def _connect_device_impl(self, device: Device) -> bool:
        """实际连接实现"""
        device.status = DeviceStatus.CONNECTING
        # 覆盖前先释放旧驱动，避免重试泄漏连接（超时后仍继续覆盖）
        previous = device.driver
        if previous is not None:
            device.driver = None
            await self._safe_disconnect_driver(
                previous, device_id=device.device_id, reason="replace_previous"
            )

        driver = None
        try:
            driver = self._create_driver(device.protocol)
            device.driver = driver
            async with self._connect_semaphore:
                await driver.connect(device.connection_config)
            device.status = DeviceStatus.ONLINE
            device.error_count = 0
            logger.info("Device %s connected", device.device_id)
            await self._report_device_status(device, 1, "connected", "connect")
            return True
        except Exception as e:
            device.status = DeviceStatus.ERROR
            device.error_count += 1
            if driver is not None:
                device.driver = None
                await self._safe_disconnect_driver(
                    driver, device_id=device.device_id, reason="connect_failed_cleanup"
                )
            logger.error("Device %s connect failed: %s", device.device_id, e)
            await self._report_device_status(device, 0, f"connect failed: {e}", "connect")
            raise

    async def _connect_center_collection_device(
        self, device: Device, use_circuit_breaker: bool = True
    ) -> bool:
        """连接中心采集设备（通过代理进程）

        使用熔断器和重试机制保护连接。
        """
        # 重连路径（use_circuit_breaker=False）直接尝试，绕过熔断，与普通设备路径
        # (connect_device 的 else 分支) 对称：否则熔断 OPEN 期间重连被直接拒绝、
        # 空转至 recovery_timeout，无法按 reconnect_interval 真正重试。
        if not use_circuit_breaker:
            return await self._connect_center_collection_device_impl(device)

        # 获取设备的熔断器
        if device.device_id not in self._circuit_breakers:
            self._circuit_breakers[device.device_id] = CircuitBreaker(
                failure_threshold=5,
                recovery_timeout=60,
                expected_exception=DeviceConnectionError
            )

        circuit_breaker = self._circuit_breakers[device.device_id]

        try:
            return await circuit_breaker(self._connect_center_collection_device_impl)(device)
        except Exception as e:
            logger.error(
                "Device %s center collection connect failed (circuit breaker): %s",
                device.device_id,
                e,
            )
            device.status = DeviceStatus.ERROR
            await self._report_device_status(
                device,
                0,
                f"proxy connect failed: {e}",
                "connect",
            )
            return False

    @with_retry(max_retries=3, base_delay=1.0, backoff_factor=2.0)
    async def _connect_center_collection_device_impl(self, device: Device) -> bool:
        """中心采集设备连接实现"""
        device.status = DeviceStatus.CONNECTING
        driver = None
        try:
            # 构建代理连接配置
            endpoint = device.connection_config.get("endpoint", "")
            # 从预分配的端口映射中获取（register_devices 时已分配）
            proxy_port = device.connection_config.get("proxy_port")
            if proxy_port is None:
                # 兜底：如果预分配未执行，使用映射器分配
                proxy_port = await self._port_mapper.get_port_async(endpoint)
                device.connection_config["proxy_port"] = proxy_port
            # 从配置获取 OPC Server ProgID
            opc_server = device.connection_config.get("opc_server")

            proxy_config = {
                "proxy_host": "127.0.0.1",  # 代理进程在本机运行
                "proxy_port": proxy_port,    # 预分配的端口
                "opc_server": opc_server,   # 从 comm_protocol.server 获取
                "vm_host": endpoint,         # OPC VM 地址
                "update_rate_ms": 500,
                "timeout": 10.0,
            }

            # 覆盖前先释放旧驱动（超时后仍继续覆盖）
            previous = device.driver
            if previous is not None:
                device.driver = None
                await self._safe_disconnect_driver(
                    previous,
                    device_id=device.device_id,
                    reason="replace_previous_proxy",
                )

            driver = self._create_driver(device.protocol)
            device.driver = driver
            await driver.connect(proxy_config)

            device.status = DeviceStatus.ONLINE
            device.error_count = 0
            logger.info(
                "Device %s connected via proxy to OPC VM %s (server=%s)",
                device.device_id,
                endpoint,
                opc_server,
            )
            await self._report_device_status(
                device,
                1,
                f"connected via proxy to {endpoint}",
                "connect",
            )
            return True
        except Exception as e:
            device.status = DeviceStatus.ERROR
            device.error_count += 1
            if driver is not None:
                device.driver = None
                await self._safe_disconnect_driver(
                    driver,
                    device_id=device.device_id,
                    reason="proxy_connect_failed_cleanup",
                )
            logger.error(
                "Device %s center collection connect failed: %s",
                device.device_id,
                e,
            )
            await self._report_device_status(
                device,
                0,
                f"proxy connect failed: {e}",
                "connect",
            )
            raise

    async def connect_all(self) -> None:
        """并发连接所有设备"""
        # 同源快照：tasks 与配对 results 的遍历必须基于同一份设备列表，
        # 否则连接期间 self.devices 增删会让 zip 错配 device↔result（A方-5）。
        devices = list(self.devices.values())
        tasks = [self.connect_device(d) for d in devices]
        results = await asyncio.gather(*tasks, return_exceptions=True)
        success = sum(1 for r in results if r is True)
        for device, result in zip(devices, results):
            if result is not True and device.status != DeviceStatus.CONFIG_ERROR:
                await self.schedule_reconnect(device)
        logger.info("Connected %s/%s devices", success, len(devices))

    async def disconnect_all(self) -> None:
        """断开所有设备（并发，每设备独立超时）。"""
        for task in self._reconnect_tasks.values():
            if not task.done():
                task.cancel()
        self._reconnect_tasks.clear()

        devices = list(self.devices.values())

        async def _one(device: Device) -> None:
            driver = device.driver
            device.driver = None
            await self._safe_disconnect_driver(
                driver, device_id=device.device_id, reason="disconnect_all"
            )
            device.status = DeviceStatus.OFFLINE
            try:
                await self._report_device_status(device, 0, "disconnected", "disconnect")
            except Exception as e:
                logger.warning(
                    "Device %s disconnect status report error: %s",
                    device.device_id,
                    e,
                )

        if devices:
            await asyncio.gather(*(_one(d) for d in devices), return_exceptions=True)
        logger.info("All devices disconnected")

    def _resolve_driver_registry(self) -> "DriverRegistry":
        """Return the unified bundled/installed registry, discovering it lazily."""
        from drivers.registry import DriverRegistry, default_drivers_root

        # P0 修复：已初始化时直接返回现有快照，不再重复扫描或合并
        if self._driver_registry is not None:
            return self._driver_registry

        # P0 修复：进程级锁保护初始化，避免多线程并发初始化
        with _REGISTRY_INIT_LOCK:
            # 双重检查：可能在等锁期间已被其他线程初始化
            if self._driver_registry is not None:
                return self._driver_registry

            # 首次初始化：使用 DriverRegistry.discover() 一次性完成扫描+合并
            self._driver_registry_scanned = True
            drivers_root = default_drivers_root()
            try:
                # discover() 内部已经调用了 scan() + 合并 bundled，返回完整注册表
                self._driver_registry = DriverRegistry.discover(drivers_root)
                logger.info(
                    "统一驱动注册表加载完成 root=%s protocols=%s",
                    drivers_root,
                    self._driver_registry.protocols(),
                )
            except Exception as exc:
                logger.warning("统一驱动注册表加载失败 root=%s: %s", drivers_root, exc)
                # 失败时回退到仅内置驱动
                self._driver_registry = DriverRegistry()
            return self._driver_registry

    def _create_driver(self, protocol: str) -> "BaseDriver":
        """Create every polling driver through the unified registry."""
        from drivers.registry import DriverRegistryError, default_data_root

        registry = self._resolve_driver_registry()
        try:
            return registry.create(
                protocol,
                read_executor=self._read_executor,
                data_root=default_data_root(),
                metrics=self._metrics,
            )
        except DriverRegistryError as exc:
            raise DeviceConnectionError(
                f"Unsupported protocol: {protocol}: {exc}"
            ) from exc

    async def check_device_health(self, device: Device) -> bool:
        """检查设备健康状态"""
        if device.status == DeviceStatus.CONFIG_ERROR:
            return False
        if device.status != DeviceStatus.ONLINE:
            return False
        if is_external_push_protocol(device.protocol):
            return True
        if is_center_collection_protocol(device.protocol):
            # 中心采集设备：通过代理进程 ping
            if device.driver is None:
                device.status = DeviceStatus.ERROR
                await self._report_device_status(device, 2, "health check missing driver", "timeout")
                return False
            try:
                ok = await device.driver.ping()
                if not ok:
                    device.status = DeviceStatus.ERROR
                    await self._report_device_status(device, 2, "proxy ping returned false", "timeout")
                    return False
                return True
            except Exception as e:
                logger.warning("Device %s proxy health check failed: %s", device.device_id, e)
                device.status = DeviceStatus.ERROR
                await self._report_device_status(device, 2, f"proxy health check failed: {e}", "timeout")
                return False
        if device.driver is None:
            device.status = DeviceStatus.ERROR
            await self._report_device_status(device, 2, "health check missing driver", "timeout")
            return False

        try:
            # 简单的ping测试
            if hasattr(device.driver, 'ping'):
                ok = await device.driver.ping()
                if not ok:
                    device.status = DeviceStatus.ERROR
                    await self._report_device_status(device, 2, "health ping returned false", "timeout")
                    return False
            return True
        except Exception as e:
            logger.warning("Device %s health check failed: %s", device.device_id, e)
            device.status = DeviceStatus.ERROR
            await self._report_device_status(device, 2, f"health check failed: {e}", "timeout")
            return False

    async def schedule_reconnect(self, device: Device) -> None:
        """调度设备重连"""
        if device.status == DeviceStatus.CONFIG_ERROR:
            return
        if is_external_push_protocol(device.protocol):
            return
        if device.device_id in self._reconnect_tasks:
            return

        async def reconnect_task():
            attempt = 0
            try:
                while device.status != DeviceStatus.ONLINE:
                    await self._sleep(self._next_reconnect_delay(attempt))
                    # B3：提交前再验证当前代。热替换或下线之后，这个闭包捕获的 device
                    # 已不再代表该 device_id，必须停止——否则它会继续连旧地址，并把状态
                    # 写回一个已作废的对象。
                    if self.devices.get(device.device_id) is not device:
                        logger.info(
                            "Reconnect for device %s stopped: no longer the current device object",
                            device.device_id,
                        )
                        break
                    try:
                        self._metrics.increment("reconnect_attempt_count")
                        logger.info("Attempting reconnect for device %s", device.device_id)
                        await self.connect_device(device, use_circuit_breaker=False)
                        if device.status == DeviceStatus.ONLINE:
                            self._metrics.increment("reconnect_success_count")
                            logger.info("Device %s reconnected successfully", device.device_id)
                            await self._report_device_status(device, 1, "reconnected", "connect")
                            break
                        self._metrics.increment("reconnect_failure_count")
                        attempt += 1
                    except asyncio.CancelledError:
                        raise
                    except Exception as e:
                        self._metrics.increment("reconnect_failure_count")
                        attempt += 1
                        logger.error("Device %s reconnect failed: %s", device.device_id, e)
                        await self._report_device_status(device, 2, f"reconnect failed: {e}", "timeout")
            finally:
                self._reconnect_tasks.pop(device.device_id, None)
                self._metrics.set_gauge("reconnect_active", len(self._reconnect_tasks))

        # 启动重连任务
        self._reconnect_tasks[device.device_id] = asyncio.create_task(reconnect_task())
        self._metrics.increment("reconnect_scheduled_count")
        active = len(self._reconnect_tasks)
        self._reconnect_peak_active = max(self._reconnect_peak_active, active)
        self._metrics.set_gauge("reconnect_active", active)
        self._metrics.set_gauge("reconnect_peak_active", self._reconnect_peak_active)
        logger.info("Scheduled reconnect for device %s in %ss", device.device_id, self._reconnect_interval)

    def _next_reconnect_delay(self, attempt: int) -> float:
        base_delay = min(
            self._reconnect_max_interval,
            self._reconnect_interval * (2 ** max(0, attempt)),
        )
        jitter_room = max(0.0, self._reconnect_max_interval - base_delay)
        jitter_cap = min(base_delay * self._reconnect_jitter_ratio, jitter_room)
        return base_delay + self._random.uniform(0.0, jitter_cap)

    async def mark_device_error(self, device: Device, message: str, event_type: str = "timeout") -> None:
        """标记设备异常并调度重连。"""
        if device.status == DeviceStatus.CONFIG_ERROR:
            return
        if device.status == DeviceStatus.ONLINE:
            device.status = DeviceStatus.ERROR
        device.error_count += 1
        device.last_error_time = time.time()
        await self._report_device_status(device, 2, message, event_type)
        await self.schedule_reconnect(device)

    async def _report_device_status(
        self,
        device: Device,
        status: int,
        message: str,
        event_type: str,
    ) -> None:
        if self._status_reporter is None:
            return
        try:
            await self._status_reporter(device.device_id, status, message, event_type)
        except Exception as exc:
            logger.warning("Failed to report device status device=%s: %s", device.device_id, exc)
