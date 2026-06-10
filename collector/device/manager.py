"""设备管理器"""

import asyncio
import json
import os
import socket
import time
from typing import Any, Awaitable, Callable, Dict, List, Optional, TYPE_CHECKING

from core.config import CollectorConfig, DeviceConfig, VariableConfig
from core.exceptions import DeviceConnectionError
from core.logger import get_logger
from core.retry import with_retry, CircuitBreaker
from device.device import Device, DeviceStatus, Variable

if TYPE_CHECKING:
    from drivers.base import BaseDriver

logger = get_logger("device_manager")

# 端口映射文件路径（与 check_protocols.py 共享）
PORT_MAPPING_FILE = os.path.join(os.path.dirname(__file__), "..", "port_mapping.json")


EXTERNAL_PUSH_PROTOCOLS = {"http_push", "opcda_sidecar"}

# 中心采集协议（通过代理进程远程采集）
CENTER_COLLECTION_PROTOCOLS = {"opcda_center"}


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
    ):
        self.config = config
        self._status_reporter = status_reporter
        # 显式权威端口映射文件（Agent 生成）；None 时沿用动态分配（dev 期 run.bat 链路）。
        self._port_mapping_file = port_mapping_file
        self.devices: Dict[str, Device] = {}
        self._lock = asyncio.Lock()
        # 设备连接熔断器
        self._circuit_breakers: Dict[str, CircuitBreaker] = {}
        # 重连任务
        self._reconnect_tasks: Dict[str, asyncio.Task] = {}
        # 最大重试次数
        self._max_retries = 3
        # 重连间隔（秒）
        self._reconnect_interval = 30
        self._connect_semaphore = asyncio.Semaphore(10)
        # VM endpoint 到代理端口的映射器
        self._port_mapper: Optional[EndpointPortMapper] = None

    def register_devices(self) -> None:
        """根据配置注册所有设备"""
        # 初始化端口映射器
        self._port_mapper = EndpointPortMapper(mapping_file=self._port_mapping_file)

        # 从配置创建设备
        device_map: Dict[str, Device] = {}
        for dev_cfg in self.config.devices:
            device = Device(
                device_id=dev_cfg.device_id,
                device_type=dev_cfg.device_type,
                protocol=dev_cfg.protocol,
                connection_config={
                    "ip": dev_cfg.ip,
                    "endpoint": dev_cfg.endpoint,
                    "port": dev_cfg.port,
                    "rack": dev_cfg.rack,
                    "slot": dev_cfg.slot,
                    "opc_server": dev_cfg.opc_server,
                },
                device_pk=dev_cfg.device_pk,
                config_version=dev_cfg.config_version,
            )
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
            if is_external_push_protocol(device.protocol):
                device.scan_tasks = []
                logger.info(
                    "Device %s registered as external-push protocol=%s; no polling tasks will be created",
                    device.device_id,
                    device.protocol,
                )
            elif is_center_collection_protocol(device.protocol):
                # 中心采集协议：预分配代理端口
                endpoint = device.connection_config.get("endpoint", "")
                proxy_port = self._port_mapper.get_port(endpoint)
                device.connection_config["proxy_port"] = proxy_port
                device.build_scan_tasks()
                logger.info(
                    "Device %s registered as center-collection protocol=%s; will poll via proxy on port %d",
                    device.device_id,
                    device.protocol,
                    proxy_port,
                )
            else:
                device.build_scan_tasks()

        self.devices = device_map

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

    def _setup_scan_tasks(self, device: Device) -> None:
        """按协议为设备构建采集任务（与 register_devices 同规则）。

        - external-push：无轮询任务
        - center：先分配代理端口再构建任务
        - 其余：直接构建任务
        """
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
            return
        device.build_scan_tasks()

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

        device = Device(
            device_id=dev_cfg.device_id,
            device_type=dev_cfg.device_type,
            protocol=dev_cfg.protocol,
            connection_config={
                "ip": dev_cfg.ip,
                "endpoint": dev_cfg.endpoint,
                "port": dev_cfg.port,
                "rack": dev_cfg.rack,
                "slot": dev_cfg.slot,
                "opc_server": dev_cfg.opc_server,
            },
            device_pk=dev_cfg.device_pk,
            config_version=dev_cfg.config_version,
        )
        for variable in self._build_variables(var_cfgs):
            device.add_variable(variable)
        self._setup_scan_tasks(device)
        self.devices[dev_cfg.device_id] = device
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
        except Exception:
            device.variables = prev_variables
            device.scan_tasks = prev_scan_tasks
            raise
        return device

    async def remove_device(self, device_id: str) -> None:
        """运行期下线一台设备。

        固定顺序（调度循环由上层先行取消）：取消重连任务 → 断开 driver
        → 从 devices / 熔断器移除。不回写 DB 状态（仅日志），保留端口映射。
        """
        device = self.devices.get(device_id)
        if device is None:
            return

        # 1) 取消重连任务，避免刚下线又被拉起
        reconnect_task = self._reconnect_tasks.pop(device_id, None)
        if reconnect_task is not None and not reconnect_task.done():
            reconnect_task.cancel()
            try:
                await reconnect_task
            except asyncio.CancelledError:
                pass

        # 2) 断开 driver（不回写 DB 状态，仅日志）
        try:
            if device.driver:
                await device.driver.disconnect()
        except Exception as exc:
            logger.warning("remove_device %s driver disconnect error: %s", device_id, exc)
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
            # 中心采集协议：连接到代理进程
            return await self._connect_center_collection_device(device)

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

    @with_retry(max_retries=3, base_delay=1.0, backoff_factor=2.0)
    async def _connect_device_impl(self, device: Device) -> bool:
        """实际连接实现"""
        device.status = DeviceStatus.CONNECTING
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
            logger.error("Device %s connect failed: %s", device.device_id, e)
            await self._report_device_status(device, 0, f"connect failed: {e}", "connect")
            raise

    async def _connect_center_collection_device(self, device: Device) -> bool:
        """连接中心采集设备（通过代理进程）

        使用熔断器和重试机制保护连接。
        """
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
        tasks = [self.connect_device(d) for d in self.devices.values()]
        results = await asyncio.gather(*tasks, return_exceptions=True)
        success = sum(1 for r in results if r is True)
        for device, result in zip(self.devices.values(), results):
            if result is not True:
                await self.schedule_reconnect(device)
        logger.info("Connected %s/%s devices", success, len(self.devices))

    async def disconnect_all(self) -> None:
        """断开所有设备"""
        # 停止所有重连任务
        for task in self._reconnect_tasks.values():
            if not task.done():
                task.cancel()
        self._reconnect_tasks.clear()

        # 断开所有设备
        for device in self.devices.values():
            try:
                if device.driver:
                    await device.driver.disconnect()
                device.status = DeviceStatus.OFFLINE
                await self._report_device_status(device, 0, "disconnected", "disconnect")
            except Exception as e:
                logger.warning("Device %s disconnect error: %s", device.device_id, e)
        logger.info("All devices disconnected")

    def _create_driver(self, protocol: str) -> "BaseDriver":
        """根据协议类型创建内置驱动"""
        name = protocol.lower()
        if name == "s7":
            from drivers.s7_driver import S7Driver
            return S7Driver()
        if name == "ge_srtp":
            from drivers.ge_srtp_driver import GESRTPDriver
            return GESRTPDriver()
        if name in ("opc", "opcua", "opc_ua"):
            from drivers.opcua_driver import OPCUADriver
            return OPCUADriver()
        if name == "opcda_center":
            from drivers.opcda_center_driver import OpcDaCenterDriver
            return OpcDaCenterDriver()
        raise DeviceConnectionError(f"Unsupported protocol: {protocol}")

    async def check_device_health(self, device: Device) -> bool:
        """检查设备健康状态"""
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
        if is_external_push_protocol(device.protocol):
            return
        if device.device_id in self._reconnect_tasks:
            return

        async def reconnect_task():
            try:
                while device.status != DeviceStatus.ONLINE:
                    await asyncio.sleep(self._reconnect_interval)
                    try:
                        logger.info("Attempting reconnect for device %s", device.device_id)
                        await self.connect_device(device, use_circuit_breaker=False)
                        if device.status == DeviceStatus.ONLINE:
                            logger.info("Device %s reconnected successfully", device.device_id)
                            await self._report_device_status(device, 1, "reconnected", "connect")
                            break
                    except asyncio.CancelledError:
                        raise
                    except Exception as e:
                        logger.error("Device %s reconnect failed: %s", device.device_id, e)
                        await self._report_device_status(device, 2, f"reconnect failed: {e}", "timeout")
            finally:
                self._reconnect_tasks.pop(device.device_id, None)

        # 启动重连任务
        self._reconnect_tasks[device.device_id] = asyncio.create_task(reconnect_task())
        logger.info("Scheduled reconnect for device %s in %ss", device.device_id, self._reconnect_interval)

    async def mark_device_error(self, device: Device, message: str, event_type: str = "timeout") -> None:
        """标记设备异常并调度重连。"""
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
