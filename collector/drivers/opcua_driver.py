"""OPC UA 协议驱动实现（基于 asyncua，可选依赖）"""

from typing import Any, Dict, List

from core.exceptions import DeviceConnectionError, ReadError
from core.logger import get_logger
from .base import BaseDriver, ReadBlock, WritePoint

logger = get_logger("opcua_driver")


class OPCUADriver(BaseDriver):
    """OPC UA 驱动"""

    def __init__(self):
        super().__init__()
        self._client = None

    async def connect(self, config: Dict[str, Any]) -> None:
        """
        连接 OPC UA 服务器
        config:
          - endpoint: opc.tcp://127.0.0.1:4840
        """
        endpoint = config.get("endpoint")
        if not endpoint:
            ip = config.get("ip", "127.0.0.1")
            port = config.get("port", 4840)
            endpoint = f"opc.tcp://{ip}:{port}"

        try:
            from asyncua import Client  # 可选依赖，按需导入
        except ImportError as exc:
            raise DeviceConnectionError(
                "OPC UA driver requires 'asyncua'. Please install: pip install asyncua"
            ) from exc

        try:
            read_timeout_ms = config.get("read_timeout_ms")
            kwargs = {}
            if read_timeout_ms is not None:
                kwargs["timeout"] = int(read_timeout_ms) / 1000.0
            self._client = Client(url=endpoint, **kwargs)
            await self._client.connect()
            self._connected = True
            self._config = config
            logger.info("OPC UA connected to %s", endpoint)
        except Exception as exc:
            self._connected = False
            self._client = None
            raise DeviceConnectionError(f"OPC UA connect failed: {exc}") from exc

    async def disconnect(self) -> None:
        """断开 OPC UA 连接"""
        if self._client is not None:
            try:
                await self._client.disconnect()
            except Exception as exc:
                logger.warning("OPC UA disconnect error: %s", exc)
            finally:
                self._client = None
                self._connected = False

    async def read(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """
        读取 OPC UA 节点值
        ReadBlock.address 约定为 NodeId 字符串，如：
          - ns=2;s=Channel1.Device1.Temperature
          - ns=3;i=1001
        """
        if not self._connected or self._client is None:
            raise ReadError("OPC UA driver not connected")

        result: Dict[str, Any] = {}
        async with self._read_lock:
            for block in blocks:
                try:
                    node = self._client.get_node(block.address)
                    value = await node.read_value()
                    result[block.address] = [value]
                except Exception as exc:
                    logger.error("OPC UA read failed for %s: %s", block.address, exc)
                    result[block.address] = None
        return result

    async def write(self, points: List[WritePoint]) -> None:
        """OPC UA 驱动在采集模块中保持只读，禁止写 PLC/服务端节点。"""
        raise ReadError("OPC UA driver is read-only in collector")
