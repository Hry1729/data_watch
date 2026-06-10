"""OPC DA 中心采集 IPC 客户端

Python 客户端，通过 TCP 与 x86 代理进程通信。
"""

import asyncio
import json
import threading
import uuid
from typing import Any, Dict, List, Optional

from core.logger import get_logger
from core.opcda_ipc_protocol import (
    DEFAULT_PROXY_PORT,
    DEFAULT_REQUEST_TIMEOUT,
    IpcRequest,
    IpcResponse,
    OpcDaReadResult,
    RequestType,
    create_ping_request,
    create_read_request,
    create_connect_request,
    create_shutdown_request,
    ResponseStatus,
)

logger = get_logger("opcda_ipc_client")

_MAX_RESPONSE_BYTES = 64 * 1024 * 1024


class OpcDaIpcClient:
    """与代理进程通信的 IPC 客户端"""

    def __init__(
        self,
        host: str = "127.0.0.1",
        port: int = DEFAULT_PROXY_PORT,
        timeout: float = DEFAULT_REQUEST_TIMEOUT,
    ):
        self.host = host
        self.port = port
        self.timeout = timeout
        self._reader: Optional[asyncio.StreamReader] = None
        self._writer: Optional[asyncio.StreamWriter] = None
        self._connected = False
        self._lock = asyncio.Lock()

    @property
    def is_connected(self) -> bool:
        return self._connected and self._writer is not None

    async def connect(self) -> None:
        """连接代理进程"""
        async with self._lock:
            if self._connected:
                return

            try:
                self._reader, self._writer = await asyncio.wait_for(
                    asyncio.open_connection(self.host, self.port),
                    timeout=self.timeout,
                )
                self._connected = True
                logger.info(
                    "IPC 客户端已连接到代理进程 %s:%s",
                    self.host,
                    self.port,
                )
            except asyncio.TimeoutError:
                raise ConnectionError(
                    f"连接代理进程超时: {self.host}:{self.port}"
                )
            except OSError as e:
                raise ConnectionError(
                    f"无法连接代理进程 {self.host}:{self.port}: {e}"
                )

    async def close(self) -> None:
        """关闭连接"""
        async with self._lock:
            if self._writer is not None:
                try:
                    self._writer.close()
                    await self._writer.wait_closed()
                except Exception as e:
                    logger.warning("关闭连接时出错: %s", e)
                finally:
                    self._writer = None
                    self._reader = None
                    self._connected = False
                    logger.info("IPC 客户端已断开")

    async def _send_request(self, request: IpcRequest) -> IpcResponse:
        """发送请求并接收响应"""
        async with self._lock:
            if not self.is_connected:
                raise ConnectionError("未连接到代理进程")

            # 序列化请求
            request_json = json.dumps(request.to_dict(), ensure_ascii=False)
            request_bytes = request_json.encode("utf-8")

            # 协议格式: 4字节长度 + JSON 数据
            length_header = len(request_bytes).to_bytes(4, byteorder="big")

            try:
                assert self._reader is not None
                assert self._writer is not None
                self._writer.write(length_header + request_bytes)
                await self._writer.drain()

                # 读取响应长度。同一 StreamReader 不支持并发读取，必须由 _lock 串行化。
                response_length_bytes = await asyncio.wait_for(
                    self._reader.readexactly(4),
                    timeout=self.timeout,
                )
                response_length = int.from_bytes(response_length_bytes, byteorder="big")
                if response_length <= 0 or response_length > _MAX_RESPONSE_BYTES:
                    self._connected = False
                    raise ValueError(f"响应帧长非法: {response_length}")

                # 读取响应数据
                response_bytes = await asyncio.wait_for(
                    self._reader.readexactly(response_length),
                    timeout=self.timeout,
                )
                response_json = json.loads(response_bytes.decode("utf-8"))
                response = IpcResponse.from_dict(response_json)

                return response

            except asyncio.TimeoutError:
                self._connected = False
                raise TimeoutError(f"请求超时: {request.request_type}")
            except asyncio.IncompleteReadError as e:
                self._connected = False
                raise ConnectionError(f"连接中断(读取不完整): {e}")
            except json.JSONDecodeError as e:
                raise ValueError(f"响应解析失败: {e}")
            except ConnectionResetError as e:
                self._connected = False
                raise ConnectionError(f"连接被重置: {e}")

    async def ping(self) -> bool:
        """心跳检测"""
        request_id = str(uuid.uuid4())
        request = create_ping_request(request_id)

        try:
            response = await self._send_request(request)
            return response.status == ResponseStatus.OK
        except Exception as e:
            logger.warning("心跳检测失败: %s", e)
            return False

    async def connect_opc_server(
        self,
        opc_server: str,
        vm_host: str,
        update_rate_ms: int = 500,
    ) -> bool:
        """
        连接到远程 OPC DA 服务器

        Args:
            opc_server: OPC Server ProgID
            vm_host: OPC VM 主机地址
            update_rate_ms: 订阅更新速率（毫秒）

        Returns:
            是否连接成功
        """
        request_id = str(uuid.uuid4())
        request = create_connect_request(
            request_id,
            opc_server,
            vm_host,
            update_rate_ms,
        )

        try:
            response = await self._send_request(request)
            if response.status == ResponseStatus.OK:
                logger.info(
                    "已连接到 OPC DA 服务器: %s@%s",
                    opc_server,
                    vm_host,
                )
                return True
            else:
                logger.error(
                    "连接 OPC DA 服务器失败: serverProgId=%s endpoint=%s proxy_port=%s error=%s",
                    opc_server,
                    vm_host,
                    self.port,
                    response.error,
                )
                return False
        except Exception as e:
            logger.error(
                "连接 OPC DA 服务器异常: serverProgId=%s endpoint=%s proxy_port=%s err=%s",
                opc_server,
                vm_host,
                self.port,
                e,
            )
            return False

    async def read_items(
        self,
        item_ids: List[str],
        opc_server: Optional[str] = None,
        vm_host: Optional[str] = None,
    ) -> Dict[str, Any]:
        """
        读取多个点位

        Args:
            item_ids: 点位 ID 列表
            opc_server: OPC Server ProgID（可选，如未连接需提供）
            vm_host: OPC VM 主机地址（可选，如未连接需提供）

        Returns:
            {item_id: value} 字典，失败时值为 None
        """
        if not item_ids:
            return {}

        request_id = str(uuid.uuid4())

        # 构建读取请求
        payload = {"item_ids": item_ids}
        if opc_server:
            payload["opc_server"] = opc_server
        if vm_host:
            payload["vm_host"] = vm_host

        request = IpcRequest(
            request_id=request_id,
            request_type=RequestType.READ,
            payload=payload,
        )

        try:
            response = await self._send_request(request)

            if response.status != ResponseStatus.OK:
                logger.error("读取点位失败: %s", response.error)
                return {item_id: None for item_id in item_ids}

            # 解析结果
            results: Dict[str, Any] = {}
            bad_quality: Dict[str, int] = {}
            if response.data and isinstance(response.data, list):
                for item_data in response.data:
                    try:
                        result = OpcDaReadResult.from_dict(item_data)
                    except Exception as e:
                        logger.warning("跳过无法解析的 OPC DA 结果项: %s, data=%s", e, item_data)
                        continue
                    # 只返回质量好的数据
                    if result.quality >= 192:  # OPC Good quality
                        results[result.item_id] = result.value
                    else:
                        results[result.item_id] = None
                        bad_quality[result.item_id] = result.quality
                        logger.debug(
                            "点位质量不佳: %s, quality=%s",
                            result.item_id,
                            result.quality,
                        )

            # 补充未返回的点位
            missing_items: List[str] = []
            for item_id in item_ids:
                if item_id not in results:
                    results[item_id] = None
                    missing_items.append(item_id)

            if item_ids and all(results.get(item_id) is None for item_id in item_ids):
                logger.warning(
                    "OPC DA read returned no good values: requested=%s, bad_quality=%s, missing=%s",
                    len(item_ids),
                    dict(list(bad_quality.items())[:3]),
                    missing_items[:3],
                )

            return results

        except Exception as e:
            logger.error("读取点位异常: %s", e)
            return {item_id: None for item_id in item_ids}

    async def shutdown_proxy(self) -> bool:
        """关闭代理进程"""
        request_id = str(uuid.uuid4())
        request = create_shutdown_request(request_id)

        try:
            response = await self._send_request(request)
            return response.status == ResponseStatus.OK
        except Exception as e:
            logger.warning("关闭代理进程失败: %s", e)
            return False


class OpcDaIpcClientPool:
    """IPC 客户端连接池，管理多个 OPC VM 的连接

    线程安全的连接池实现，支持：
    - 双重检查锁保证单例安全
    - 连接有效性验证
    - 自动重连失效连接
    """

    def __init__(self, timeout: float = DEFAULT_REQUEST_TIMEOUT):
        self._timeout = timeout
        self._clients: Dict[str, OpcDaIpcClient] = {}
        self._lock = asyncio.Lock()
        self._thread_lock = threading.Lock()

    async def get_client(
        self,
        proxy_host: str,
        proxy_port: int = DEFAULT_PROXY_PORT,
    ) -> OpcDaIpcClient:
        """
        获取或创建客户端连接

        如果连接存在但已失效，会自动重连。

        Args:
            proxy_host: 代理进程主机地址
            proxy_port: 代理进程端口

        Returns:
            IPC 客户端实例
        """
        key = f"{proxy_host}:{proxy_port}"

        async with self._lock:
            if key in self._clients:
                client = self._clients[key]
                # 验证连接有效性
                if client.is_connected:
                    try:
                        if await client.ping():
                            return client
                    except Exception as e:
                        logger.warning("连接健康检查失败 %s: %s，将重建连接", key, e)
                # 连接无效，清理并重建
                try:
                    await client.close()
                except Exception:
                    pass
                del self._clients[key]

            # 创建新连接
            client = OpcDaIpcClient(
                host=proxy_host,
                port=proxy_port,
                timeout=self._timeout,
            )
            await client.connect()
            self._clients[key] = client
            return client

    async def remove_client(self, proxy_host: str, proxy_port: int) -> None:
        """移除失效的客户端连接"""
        key = f"{proxy_host}:{proxy_port}"
        async with self._lock:
            if key in self._clients:
                try:
                    await self._clients[key].close()
                except Exception:
                    pass
                del self._clients[key]

    async def close_all(self) -> None:
        """关闭所有客户端连接"""
        async with self._lock:
            for client in self._clients.values():
                try:
                    await client.close()
                except Exception as e:
                    logger.warning("关闭客户端失败: %s", e)
            self._clients.clear()

    async def health_check(self) -> Dict[str, bool]:
        """检查所有客户端连接状态"""
        results = {}
        for key, client in self._clients.items():
            try:
                results[key] = await client.ping()
            except Exception:
                results[key] = False
        return results


# 模块级连接池单例（线程安全）
_ipc_pool: Optional[OpcDaIpcClientPool] = None
_ipc_pool_lock = threading.Lock()


def get_ipc_pool() -> OpcDaIpcClientPool:
    """获取连接池单例（线程安全的双重检查锁）"""
    global _ipc_pool
    if _ipc_pool is None:
        with _ipc_pool_lock:
            if _ipc_pool is None:  # double-check
                _ipc_pool = OpcDaIpcClientPool()
    return _ipc_pool
