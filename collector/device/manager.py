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
from core.retry import with_retry, CircuitBreaker, ConnectionPool, classify_error
from core.plugin_system import get_plugin_manager
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


class EndpointPortMapper:
    """
    VM endpoint 到代理端口的动态映射器

    功能：
    - 为每个不同的 VM endpoint 分配独立端口
    - 支持从文件加载已有映射（与 check_protocols.py 共享）
    - 检测端口可用性，自动跳过被占用端口
    - 从 base_port 开始依次分配
    """

    def __init__(self, base_port: int = 9090, max_port: int = 9200, load_mapping_file: bool = True):
        self._base_port = base_port
        self._max_port = max_port
        self._endpoint_to_port: Dict[str, int] = {}
        self._next_port = base_port
        self._lock = asyncio.Lock()

        # 从文件加载已有映射
        if load_mapping_file:
            self._load_mapping_from_file()

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

        if endpoint not in self._endpoint_to_port:
            self._endpoint_to_port[endpoint] = self._find_next_available_port()
            logger.info("为 endpoint %s 分配端口 %d", endpoint, self._endpoint_to_port[endpoint])
            self._save_mapping_to_file()

        return self._endpoint_to_port[endpoint]

    async def get_port_async(self, endpoint: str) -> int:
        """获取或分配 endpoint 对应的端口（异步版本）"""
        async with self._lock:
            if not endpoint or endpoint in ("127.0.0.1", "localhost"):
                endpoint = "localhost"

            endpoint = endpoint.strip()

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
    ):
        self.config = config
        self._status_reporter = status_reporter
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
        self._port_mapper = EndpointPortMapper()

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
        """根据协议类型创建驱动"""
        # 使用插件系统创建驱动
        plugin_manager = get_plugin_manager()
        driver_class = plugin_manager.protocol_registry.get_driver_class(protocol.lower())

        if driver_class:
            return driver_class()
        else:
            # 回退到内置驱动
            if protocol.lower() == "s7":
                from drivers.s7_driver import S7Driver
                return S7Driver()
            elif protocol.lower() == "ge_srtp":
                from drivers.ge_srtp_driver import GESRTPDriver
                return GESRTPDriver()
            elif protocol.lower() in ("opc", "opcua", "opc_ua"):
                from drivers.opcua_driver import OPCUADriver
                return OPCUADriver()
            elif protocol.lower() == "opcda_center":
                from drivers.opcda_center_driver import OpcDaCenterDriver
                return OpcDaCenterDriver()
            else:
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
