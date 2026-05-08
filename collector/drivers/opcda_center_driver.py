"""OPC DA 中心采集驱动

通过代理进程远程采集 OPC DA 数据。
不修改现有边缘推送模式代码，独立实现中心采集逻辑。
"""

import asyncio
from typing import Any, Dict, List, Optional

from core.logger import get_logger
from core.opcda_ipc_client import OpcDaIpcClient, OpcDaIpcClientPool
from core.opcda_ipc_protocol import DEFAULT_PROXY_PORT
from drivers.base import BaseDriver, ReadBlock, WritePoint

logger = get_logger("opcda_center_driver")

# 模块级连接池单例（同一代理端口共享连接）
_ipc_pool: Optional[OpcDaIpcClientPool] = None


def get_ipc_pool() -> OpcDaIpcClientPool:
    """获取全局连接池单例"""
    global _ipc_pool
    if _ipc_pool is None:
        _ipc_pool = OpcDaIpcClientPool()
    return _ipc_pool


class OpcDaCenterDriver(BaseDriver):
    """OPC DA 中心采集驱动

    通过 x86 代理进程远程采集 OPC DA 数据。

    配置示例:
    {
        "proxy_host": "127.0.0.1",
        "proxy_port": 9090,
        "opc_server": "ZPMC.OPCServer",
        "vm_host": "172.27.18.221",
        "update_rate_ms": 500,
        "timeout": 10.0,
    }
    """

    # 协议标识
    PROTOCOL_NAME = "opcda_center"

    def __init__(self):
        super().__init__()
        self._ipc_client: Optional[OpcDaIpcClient] = None
        self._owns_client: bool = True  # 是否拥有客户端（用于判断是否需要关闭）
        self._opc_server: Optional[str] = None
        self._vm_host: Optional[str] = None
        self._update_rate_ms: int = 500
        self._timeout: float = 10.0

    async def connect(self, config: Dict[str, Any]) -> None:
        """连接到代理进程和远程 OPC DA 服务器

        Args:
            config: 连接配置
                - proxy_host: 代理进程主机地址（默认 127.0.0.1）
                - proxy_port: 代理进程端口（默认 9090）
                - opc_server: OPC Server ProgID
                - vm_host: OPC VM 主机地址
                - update_rate_ms: 订阅更新速率（毫秒）
                - timeout: 请求超时（秒）
        """
        proxy_host = config.get("proxy_host", "127.0.0.1")
        proxy_port = config.get("proxy_port", DEFAULT_PROXY_PORT)
        self._opc_server = config.get("opc_server", "")
        self._vm_host = config.get("vm_host", "")
        self._update_rate_ms = config.get("update_rate_ms", 500)
        self._timeout = config.get("timeout", 10.0)

        # 使用连接池获取客户端（同一代理端口共享连接）
        pool = get_ipc_pool()
        self._ipc_client = await pool.get_client(proxy_host, proxy_port)
        self._owns_client = False  # 标记客户端来自连接池，不要在 disconnect 时关闭

        # 验证必需配置
        if not self._opc_server:
            raise ConnectionError(
                f"OPC DA 中心采集驱动缺少 opc_server 配置: proxy={proxy_host}:{proxy_port}"
            )
        if not self._vm_host:
            raise ConnectionError(
                f"OPC DA 中心采集驱动缺少 vm_host 配置: proxy={proxy_host}:{proxy_port}"
            )

        # 连接远程 OPC DA 服务器
        success = await self._ipc_client.connect_opc_server(
            self._opc_server,
            self._vm_host,
            self._update_rate_ms,
        )
        if not success:
            raise ConnectionError(
                f"无法连接到 OPC DA 服务器: {self._opc_server}@{self._vm_host}"
            )

        self._connected = True
        self._config = config

        logger.info(
            "OPC DA 中心采集驱动已连接: proxy=%s:%s, opc=%s@%s",
            proxy_host,
            proxy_port,
            self._opc_server,
            self._vm_host,
        )

    async def disconnect(self) -> None:
        """断开连接（连接池管理的连接不主动关闭）"""
        # 如果是自己创建的客户端，才关闭
        # 来自连接池的客户端不关闭，由连接池统一管理
        if self._owns_client and self._ipc_client is not None:
            try:
                await self._ipc_client.close()
            except Exception as e:
                logger.warning("断开连接时出错: %s", e)
            finally:
                self._ipc_client = None

        self._connected = False
        logger.info("OPC DA 中心采集驱动已断开")

    async def read(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """
        通过代理读取远程 OPC DA 数据

        Args:
            blocks: 读取块列表
                ReadBlock.address 约定为完整 item_id
                格式: {channel}.{device}.{group}.{tag}
                例如: OPD_Server.AGV801.ViewMain.Tag30041800

        Returns:
            {address: value} 字典
        """
        if not self._connected or self._ipc_client is None:
            raise ConnectionError("驱动未连接")

        if not blocks:
            return {}

        # 提取所有 item_id
        item_ids = [block.address for block in blocks]

        async with self._read_lock:
            try:
                results = await self._ipc_client.read_items(
                    item_ids,
                    opc_server=self._opc_server,
                    vm_host=self._vm_host,
                )
                return results
            except Exception as e:
                logger.error("读取点位失败: %s", e)
                return {block.address: None for block in blocks}

    async def write(self, points: List[WritePoint]) -> None:
        """
        写入数据（OPC DA 中心采集模式暂不支持写入）

        Raises:
            NotImplementedError: OPC DA 中心采集模式暂不支持写入操作
        """
        raise NotImplementedError(
            "OPC DA 中心采集模式暂不支持写入操作"
        )

    async def ping(self) -> bool:
        """健康检查"""
        if self._ipc_client is None:
            return False
        return await self._ipc_client.ping()

    async def reconnect(self, config: Dict[str, Any]) -> None:
        """重连"""
        await self.disconnect()
        await self.connect(config)

    @classmethod
    def from_device_config(
        cls,
        device_id: str,
        device_type: str,
        protocol: str,
        connection_config: Dict[str, Any],
    ) -> "OpcDaCenterDriver":
        """
        从设备配置创建驱动实例

        Args:
            device_id: 设备 ID
            device_type: 设备类型
            protocol: 协议名称
            connection_config: 连接配置

        Returns:
            驱动实例
        """
        driver = cls()
        return driver


# 协议注册信息
DRIVER_INFO = {
    "protocol_name": OpcDaCenterDriver.PROTOCOL_NAME,
    "driver_class": OpcDaCenterDriver,
    "description": "OPC DA 中心采集驱动（通过代理进程远程采集）",
    "config_schema": {
        "proxy_host": {
            "type": "string",
            "default": "127.0.0.1",
            "description": "代理进程主机地址",
        },
        "proxy_port": {
            "type": "integer",
            "default": 9090,
            "description": "代理进程端口",
        },
        "opc_server": {
            "type": "string",
            "description": "OPC Server ProgID",
        },
        "vm_host": {
            "type": "string",
            "description": "OPC VM 主机地址",
        },
        "update_rate_ms": {
            "type": "integer",
            "default": 500,
            "description": "订阅更新速率（毫秒）",
        },
        "timeout": {
            "type": "number",
            "default": 10.0,
            "description": "请求超时（秒）",
        },
    },
}
