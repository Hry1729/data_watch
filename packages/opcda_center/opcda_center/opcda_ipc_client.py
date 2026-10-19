"""OPC DA 中心采集 IPC 客户端

Python 客户端，通过 TCP 与 x86 代理进程通信。
"""

import asyncio
import json
import threading
import uuid
from datetime import datetime
from typing import Any, Dict, List, Optional

from core.logger import get_logger
from core.opcda_quality import map_opcda_quality
from .opcda_ipc_protocol import (
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
from drivers.base import DriverSample

logger = get_logger("opcda_ipc_client")

_MAX_RESPONSE_BYTES = 64 * 1024 * 1024


def _parse_source_timestamp(raw_timestamp: str) -> datetime:
    if not isinstance(raw_timestamp, str) or not raw_timestamp.strip():
        raise ValueError("OPC DA timestamp must be a non-empty ISO8601 string")
    text = raw_timestamp.strip()
    if text.endswith("Z"):
        text = text[:-1] + "+00:00"
    timestamp = datetime.fromisoformat(text)
    if timestamp.tzinfo is not None:
        timestamp = timestamp.astimezone().replace(tzinfo=None)
    return timestamp


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

    def _discard_connection(self) -> None:
        """丢弃当前连接：缓冲区里可能残留上一请求的响应，绝不能再复用。"""
        self._connected = False
        writer = self._writer
        self._writer = None
        self._reader = None
        if writer is not None:
            try:
                writer.close()
            except Exception:
                pass

    async def _send_request(
        self, request: IpcRequest, timeout: Optional[float] = None
    ) -> IpcResponse:
        """发送请求并接收响应

        timeout 按请求传入（None 用 self.timeout）：连接按代理端口被多个设备共享，
        不能把某个设备的超时写回共享对象。
        """
        if timeout is None:
            timeout = self.timeout
        async with self._lock:
            if not self.is_connected:
                raise ConnectionError("未连接到代理进程")

            # 序列化请求
            request_json = json.dumps(request.to_dict(), ensure_ascii=False)
            request_bytes = request_json.encode("utf-8")

            # 协议格式: 4字节长度 + JSON 数据
            length_header = len(request_bytes).to_bytes(4, byteorder="big")

            # 高-5：任何非正常返回（超时、取消、帧错误、解析失败、request_id 不符）
            # 都丢弃连接：请求可能已发出、响应留在缓冲区，复用会让之后每次都读到上一次的响应。
            completed = False
            try:
                assert self._reader is not None
                assert self._writer is not None
                self._writer.write(length_header + request_bytes)
                await self._writer.drain()

                # 读取响应长度。同一 StreamReader 不支持并发读取，必须由 _lock 串行化。
                response_length_bytes = await asyncio.wait_for(
                    self._reader.readexactly(4),
                    timeout=timeout,
                )
                response_length = int.from_bytes(response_length_bytes, byteorder="big")
                if response_length <= 0 or response_length > _MAX_RESPONSE_BYTES:
                    raise ValueError(f"响应帧长非法: {response_length}")

                # 读取响应数据
                response_bytes = await asyncio.wait_for(
                    self._reader.readexactly(response_length),
                    timeout=timeout,
                )
                response_json = json.loads(response_bytes.decode("utf-8"))
                response = IpcResponse.from_dict(response_json)
                if response.request_id != request.request_id:
                    raise ConnectionError(
                        f"响应 request_id 不匹配: expected={request.request_id} "
                        f"got={response.request_id}"
                    )

                completed = True
                return response

            except asyncio.TimeoutError:
                raise TimeoutError(f"请求超时: {request.request_type}")
            except asyncio.IncompleteReadError as e:
                raise ConnectionError(f"连接中断(读取不完整): {e}")
            except json.JSONDecodeError as e:
                raise ValueError(f"响应解析失败: {e}")
            except ConnectionResetError as e:
                raise ConnectionError(f"连接被重置: {e}")
            finally:
                if not completed:
                    self._discard_connection()

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
        timeout: Optional[float] = None,
    ) -> bool:
        """
        连接到远程 OPC DA 服务器

        Args:
            opc_server: OPC Server ProgID
            vm_host: OPC VM 主机地址
            update_rate_ms: 订阅更新速率（毫秒）
            timeout: 本次请求超时（秒），None 用客户端默认值

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
            response = await self._send_request(request, timeout=timeout)
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
        timeout: Optional[float] = None,
    ) -> Dict[str, Any]:
        """
        读取多个点位

        Args:
            item_ids: 点位 ID 列表
            opc_server: OPC Server ProgID（可选，如未连接需提供）
            vm_host: OPC VM 主机地址（可选，如未连接需提供）
            timeout: 本次请求超时（秒），None 用客户端默认值

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
            response = await self._send_request(request, timeout=timeout)

            if response.status != ResponseStatus.OK:
                logger.error("读取点位失败: %s", response.error)
                return {item_id: None for item_id in item_ids}

            # 解析结果，保留所有样本并映射质量码
            results: Dict[str, DriverSample] = {}
            if response.data and isinstance(response.data, list):
                for item_data in response.data:
                    try:
                        result = OpcDaReadResult.from_dict(item_data)
                        platform_quality = map_opcda_quality(result.quality)
                        source_timestamp = _parse_source_timestamp(result.timestamp)
                        results[result.item_id] = DriverSample(
                            value=result.value,
                            quality=platform_quality,
                            source_timestamp=source_timestamp,
                            protocol_quality=result.quality,
                        )
                    except Exception as e:
                        logger.warning("跳过无法解析的 OPC DA 结果项: %s, data=%s", e, item_data)
                        continue

            # 补充未返回的点位为 None（executor 会跳过）
            missing_items: List[str] = []
            for item_id in item_ids:
                if item_id not in results:
                    results[item_id] = None
                    missing_items.append(item_id)

            if missing_items:
                logger.debug(
                    "OPC DA read missing items: %s",
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
                # 借出前仅做内存态判断（is_connected），不在锁内 ping：
                # ping 是网络往返，会把所有取连接串行化。坏连接由 read 失败
                # （_send_request 置 _connected=False）+ 上层重连兜底，借出时的
                # 冗余 ping 没必要。
                if client.is_connected:
                    return client
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
