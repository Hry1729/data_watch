"""OPC DA 中心采集 IPC 协议定义

定义 collector 主进程与 x86 代理进程之间的通信协议。
协议基于 TCP + JSON，简单可靠。

协议版本: 1.0
"""

from dataclasses import dataclass
from enum import Enum
from typing import Any, Dict, List, Optional


class RequestType(str, Enum):
    """请求类型"""
    READ = "read"           # 读取点位
    PING = "ping"           # 心跳检测
    SHUTDOWN = "shutdown"   # 关闭代理
    CONNECT = "connect"     # 连接 OPC 服务器
    DISCONNECT = "disconnect"  # 断开 OPC 服务器


class ResponseStatus(str, Enum):
    """响应状态"""
    OK = "ok"
    ERROR = "error"


# 代理预共享令牌的环境变量（与代理配置 authToken 相同；未设置/纯空白 = 不发送）
PROXY_TOKEN_ENV = "DATAWATCH_OPCDA_PROXY_TOKEN"


def normalize_proxy_token(token: Optional[str]) -> Optional[str]:
    """Trim；空/纯空白视为未配置（与代理端 NormalizeToken 一致）。"""
    text = (token or "").strip()
    return text or None


# 协议版本
PROTOCOL_VERSION = "1.0"

# 默认端口
DEFAULT_PROXY_PORT = 9090

# 请求超时（秒）
DEFAULT_REQUEST_TIMEOUT = 10.0

# 心跳间隔（秒）
DEFAULT_PING_INTERVAL = 5.0


@dataclass
class IpcRequest:
    """IPC 请求"""
    request_id: str          # 请求唯一标识
    request_type: RequestType
    payload: Optional[Dict[str, Any]] = None
    auth_token: Optional[str] = None  # 代理配置了 authToken 时必须携带

    def to_dict(self) -> Dict[str, Any]:
        result = {
            "request_id": self.request_id,
            "request_type": self.request_type.value,
            "payload": self.payload or {},
            "version": PROTOCOL_VERSION,
        }
        if self.auth_token:
            result["auth_token"] = self.auth_token
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "IpcRequest":
        return cls(
            request_id=data["request_id"],
            request_type=RequestType(data["request_type"]),
            payload=data.get("payload"),
            auth_token=data.get("auth_token"),
        )


@dataclass
class IpcResponse:
    """IPC 响应"""
    request_id: str          # 对应请求 ID
    status: ResponseStatus
    data: Optional[Any] = None
    error: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        result = {
            "request_id": self.request_id,
            "status": self.status.value,
            "version": PROTOCOL_VERSION,
        }
        if self.data is not None:
            result["data"] = self.data
        if self.error is not None:
            result["error"] = self.error
        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "IpcResponse":
        return cls(
            request_id=data["request_id"],
            status=ResponseStatus(data["status"]),
            data=data.get("data"),
            error=data.get("error"),
        )


@dataclass
class OpcDaReadRequest:
    """OPC DA 读取请求载荷"""
    opc_server: str          # OPC Server ProgID
    vm_host: str             # OPC VM 主机地址
    item_ids: List[str]      # 点位 ID 列表

    def to_dict(self) -> Dict[str, Any]:
        return {
            "opc_server": self.opc_server,
            "vm_host": self.vm_host,
            "item_ids": self.item_ids,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "OpcDaReadRequest":
        return cls(
            opc_server=data["opc_server"],
            vm_host=data["vm_host"],
            item_ids=data["item_ids"],
        )


@dataclass
class OpcDaReadResult:
    """单个点位读取结果"""
    item_id: str
    value: Any
    quality: int             # OPC 质量代码 (0-192, 192=Good)
    timestamp: str           # ISO8601 时间戳

    def to_dict(self) -> Dict[str, Any]:
        return {
            "item_id": self.item_id,
            "value": self.value,
            "quality": self.quality,
            "timestamp": self.timestamp,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "OpcDaReadResult":
        return cls(
            item_id=data["item_id"],
            value=data["value"],
            quality=data["quality"],
            timestamp=data["timestamp"],
        )


@dataclass
class OpcDaConnectRequest:
    """OPC DA 连接请求载荷"""
    opc_server: str          # OPC Server ProgID
    vm_host: str             # OPC VM 主机地址
    update_rate_ms: int = 500  # 订阅更新速率

    def to_dict(self) -> Dict[str, Any]:
        return {
            "opc_server": self.opc_server,
            "vm_host": self.vm_host,
            "update_rate_ms": self.update_rate_ms,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "OpcDaConnectRequest":
        return cls(
            opc_server=data["opc_server"],
            vm_host=data["vm_host"],
            update_rate_ms=data.get("update_rate_ms", 500),
        )


def create_read_request(request_id: str, opc_server: str, vm_host: str, item_ids: List[str]) -> IpcRequest:
    """创建读取请求"""
    read_payload = OpcDaReadRequest(
        opc_server=opc_server,
        vm_host=vm_host,
        item_ids=item_ids,
    ).to_dict()
    return IpcRequest(
        request_id=request_id,
        request_type=RequestType.READ,
        payload=read_payload,
    )


def create_ping_request(request_id: str) -> IpcRequest:
    """创建心跳请求"""
    return IpcRequest(
        request_id=request_id,
        request_type=RequestType.PING,
    )


def create_shutdown_request(request_id: str) -> IpcRequest:
    """创建关闭请求"""
    return IpcRequest(
        request_id=request_id,
        request_type=RequestType.SHUTDOWN,
    )


def create_connect_request(request_id: str, opc_server: str, vm_host: str, update_rate_ms: int = 500) -> IpcRequest:
    """创建连接请求"""
    connect_payload = OpcDaConnectRequest(
        opc_server=opc_server,
        vm_host=vm_host,
        update_rate_ms=update_rate_ms,
    ).to_dict()
    return IpcRequest(
        request_id=request_id,
        request_type=RequestType.CONNECT,
        payload=connect_payload,
    )


def create_ok_response(request_id: str, data: Any = None) -> IpcResponse:
    """创建成功响应"""
    return IpcResponse(
        request_id=request_id,
        status=ResponseStatus.OK,
        data=data,
    )


def create_error_response(request_id: str, error: str) -> IpcResponse:
    """创建错误响应"""
    return IpcResponse(
        request_id=request_id,
        status=ResponseStatus.ERROR,
        error=error,
    )
