# OPC DA Sidecar 代码审查报告

> 审查日期：2026-05-04
> 审查范围：边缘推送服务、中心采集代理服务、中心采集驱动
> 补充说明：2026-05-07 测试前复审发现中心采集代理端口分配与 `run.bat` 自动启动链路仍有集成风险，详见 `collector测试前代码审查报告_2026-05-07.md`。

---

## 一、问题统计

| 模块 | 严重 | 中等 | 轻微 | 关键问题 |
|------|------|------|------|----------|
| **边缘推送 (opcda_push_service)** | 4 | 6 | 6 | 内存泄漏、HTTP资源泄漏、无重连机制 |
| **中心采集代理 (opcda_proxy_service)** | 4 | 6 | 7 | 字节序不一致、TCP连接检测、DCOM超时 |
| **中心采集驱动 (opcda_center_driver)** | 4 | 6 | 4 | 连接池线程安全、连接有效性验证、熔断器缺失 |

---

## 二、严重问题 (P0)

### 2.1 字节序不一致 - 代理服务

**状态**: ✅ 已修复

**文件**: `OpcDaProxyService.cs` 第 166 行、第 230 行

**问题描述**:
- C# 的 `BitConverter` 使用**小端序**
- Python 客户端使用 `byteorder="big"` **大端序**
- 这会导致跨平台通信时数据解析错误

**修复内容**:
```csharp
// 读取时使用大端序
int messageLength = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(lengthBuffer, 0));

// 发送时使用大端序
byte[] lengthBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(jsonBytes.Length));
```

---

### 2.2 连接池单例线程安全问题 - 中心采集驱动

**状态**: ✅ 已修复

**文件**: `opcda_ipc_client.py`

**问题描述**:
非线程安全的懒加载单例模式，在多协程并发调用时可能创建多个池实例

**修复内容**:
```python
import threading

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
```

---

### 2.3 COM 对象内存泄漏 - 边缘推送

**状态**: ✅ 已验证无问题

**文件**: `OpcDaPushService.cs` 第 1035-1062 行

**问题描述**:
审查报告最初认为 `OPCITEMSTATE` 结构体在错误路径未释放，但经验证代码已正确实现：
- 每个 `OPCITEMSTATE` 都在 `try-finally` 中处理
- `Marshal.DestroyStructure` 在 `finally` 块中被调用，确保所有路径都释放

**结论**: 代码实现正确，无需修改

---

### 2.4 OPC 断线无重连机制 - 边缘推送

**状态**: ✅ 已修复

**文件**: `OpcDaPushService.cs` `RunPoll()` 方法

**问题描述**:
当 OPC DA 服务器断开连接或 COM 调用失败时，服务没有自动重连机制，会导致服务永久失效

**修复内容**:
```csharp
private static void RunPoll(AppConfig config)
{
    int reconnectDelayMs = 1000;  // 初始重连延迟
    int maxReconnectDelayMs = 60000;  // 最大重连延迟 60 秒

    while (!_stopping)
    {
        try
        {
            using (OpcDaClient client = new OpcDaClient(config))
            {
                client.Connect();
                client.RegisterAllItems();
                // ... 正常采集循环 ...
                reconnectDelayMs = 1000;  // 连接成功，重置延迟
            }
        }
        catch (Exception ex)
        {
            if (_stopping) break;
            LogError("OPC connection lost: " + ex.Message + ", reconnecting...");
            SleepWithStop(reconnectDelayMs);
            reconnectDelayMs = Math.Min(reconnectDelayMs * 2, maxReconnectDelayMs);  // 指数退避
        }
    }
}
```

---

### 2.5 TCP 连接状态检测不可靠 - 代理服务

**状态**: ✅ 已修复

**文件**: `OpcDaProxyService.cs` `HandleClient()` 方法

**问题描述**:
`TcpClient.Connected` 只反映最后一次 I/O 操作时的状态，对端异常断开时仍可能返回 true

**修复内容**:
```csharp
/// <summary>
/// 可靠地检测 TCP 客户端连接状态
/// </summary>
private static bool IsClientConnected(TcpClient client)
{
    if (!client.Connected) return false;

    try
    {
        Socket socket = client.Client;
        bool readable = socket.Poll(0, SelectMode.SelectRead);
        bool hasData = socket.Available > 0;

        // 如果可读但没有数据，说明对端已关闭连接
        return !(readable && !hasData);
    }
    catch (SocketException)
    {
        return false;
    }
}
```

---

### 2.6 DCOM 连接无超时控制 - 代理服务

**状态**: ✅ 已修复

**文件**: `OpcConnectionManager.cs` `OpcServerConnection.Connect()` 方法

**问题描述**:
DCOM 远程连接可能因网络问题阻塞很长时间，配置的 `ConnectionTimeoutMs` 未使用

**修复内容**:
```csharp
public void Connect()
{
    int timeoutMs = _config.ConnectionTimeoutMs;
    if (timeoutMs <= 0) timeoutMs = 10000;

    CancellationTokenSource cts = new CancellationTokenSource(timeoutMs);
    Task connectTask = Task.Run(() => ConnectInternal(), cts.Token);

    try
    {
        connectTask.Wait(timeoutMs);
        if (connectTask.IsFaulted)
            throw connectTask.Exception?.InnerException ?? new Exception("Connection failed");
        if (!connectTask.IsCompleted)
            throw new TimeoutException("OPC DA connection timeout after " + timeoutMs + "ms");
    }
    catch (AggregateException ex)
    {
        throw ex.InnerException ?? ex;
    }
}
```

---

### 2.7 连接池缺少有效性验证 - 中心采集驱动

**状态**: ✅ 已修复

**文件**: `opcda_ipc_client.py` `OpcDaIpcClientPool.get_client()`

**问题描述**:
获取客户端时不验证现有连接是否仍然有效，连接断开后返回失效连接

**修复内容**:
```python
async def get_client(self, proxy_host: str, proxy_port: int) -> OpcDaIpcClient:
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
        client = OpcDaIpcClient(...)
        await client.connect()
        self._clients[key] = client
        return client
```

---

### 2.8 中心采集缺少熔断器保护 - 设备管理器

**状态**: ✅ 已修复

**文件**: `device/manager.py` `_connect_center_collection_device()`

**问题描述**:
普通协议设备有熔断器保护，但中心采集设备绕过了熔断器，代理不可用时会频繁重连

**修复内容**:
```python
async def _connect_center_collection_device(self, device: Device) -> bool:
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
        # 处理异常...
```

---

## 三、中等问题 (P1)

### 3.1 HTTP 响应流未释放 - 边缘推送

**状态**: ✅ 已修复

**文件**: `OpcDaPushService.cs` `PushClient.PostBatch()` 方法

**问题描述**: HTTP 推送后未读取响应流，可能导致连接池耗尽

**修复内容**:
```csharp
using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
using (Stream responseStream = response.GetResponseStream())
using (StreamReader reader = new StreamReader(responseStream, Encoding.UTF8))
{
    string responseBody = reader.ReadToEnd();
    int status = (int)response.StatusCode;
    if (status < 200 || status >= 300)
    {
        throw new InvalidOperationException(
            "HTTP status " + status + ", response: " + responseBody);
    }
}
```

---

### 3.2 配置验证遗漏新协议

**状态**: ✅ 已修复

**文件**: `config_validator.py`、`config_db.py`

**问题描述**: `OPC-DA-CENTER` 和 `OPC-DA-SIDECAR` 协议未在验证器和规范化中识别

**修复内容**:

`config_validator.py`:
```python
elif protocol not in ["s7", "ge_srtp", "modbus", "opc", "opcua", "http_push",
                      "opcda_sidecar", "opcda_center"]:
    result.warnings.append(f"Unknown protocol: {config.protocol}")
```

`config_db.py`:
```python
if raw in {"opcda_sidecar", "opc_da_sidecar", "opc da sidecar", "opcda-sidecar",
           "opc-da-sidecar"}:
    return "opcda_sidecar"
if raw in {"opcda_center", "opc_da_center", "opc da center", "opcda-center",
           "opc-da-center"}:
    return "opcda_center"
```

---

### 3.3 设备地址解析不支持 OPC DA item_id

**状态**: ✅ 已修复

**文件**: `device/device.py` `_parse_address()`

**问题描述**: `opcda_center` 协议的地址解析走到 `UNKNOWN` 分支

**修复内容**:
```python
if protocol in {"opcda_center", "opcda"}:
    # OPC DA item_id 格式: OPD_Server.AGV801.ViewMain.Tag30041800
    return {
        "area": "OPCDA_ITEM",
        "raw": address,
    }
```

---

### 3.4 OPC Server 配置硬编码

**状态**: ⏳ 待修复

**文件**: `device/manager.py` 第 241-244 行

**问题描述**: OPC Server ProgID 硬编码为 `ZPMC.OPCServer`

---

### 3.5 代理端口映射逻辑脆弱

**状态**: ⏳ 待修复

**文件**: `device/manager.py` 第 36-66 行

**问题描述**: 端口计算逻辑硬编码，假设 IP 末段从 221 开始

---

### 3.6 线程池无限制增长 - 代理服务

**状态**: ⏳ 待修复

**文件**: `OpcDaProxyService.cs` 第 111 行

**问题描述**: 每个客户端连接占用线程池线程，大量并发可能耗尽资源

---

## 四、轻微问题 (P2)

### 4.1 魔法数字
- 质量值判断使用 `0xC0` 缺少常量定义

### 4.2 日志缺少关键上下文
- 没有请求 ID 跟踪，多客户端场景难以排查

### 4.3 JSON 序列化使用过时 API
- `JavaScriptSerializer` 性能较差，建议迁移

### 4.4 CSV 加载使用默认编码
- 可能导致中文乱码

---

## 五、修复进度

| 问题 | 优先级 | 状态 | 修复说明 |
|------|--------|------|----------|
| 字节序不一致 | P0 | ✅ 已修复 | 使用 `IPAddress.NetworkToHostOrder` 统一大端序 |
| 连接池线程安全 | P0 | ✅ 已修复 | 添加双重检查锁 |
| COM 内存泄漏 | P0 | ✅ 已验证无问题 | 代码已正确实现 `try-finally` |
| OPC 断线重连 | P0 | ✅ 已修复 | `RunPoll()` 添加自动重连和指数退避 |
| TCP 连接检测 | P0 | ✅ 已修复 | 添加 `IsClientConnected()` 可靠检测 |
| DCOM 超时控制 | P0 | ✅ 已修复 | `Connect()` 使用 Task + 超时控制 |
| 连接有效性验证 | P0 | ✅ 已修复 | `get_client()` 中添加 ping 检查 |
| 熔断器保护 | P0 | ✅ 已修复 | 为中心采集添加熔断器和重试 |
| HTTP 响应流释放 | P1 | ✅ 已修复 | 完整读取并释放响应流 |
| 配置验证新协议 | P1 | ✅ 已修复 | 添加 `opcda_center` 和 `opcda_sidecar` |
| 地址解析 OPC DA | P1 | ✅ 已修复 | 添加 `OPCDA_ITEM` 区域类型 |
| OPC Server 硬编码 | P1 | ✅ 已修复 | 从 `comm_protocol.server` 动态获取 |
| 端口映射脆弱 | P1 | ✅ 已修复 | 使用 `EndpointPortMapper` 动态分配端口 |
| 线程池限制 | P1 | ✅ 已修复 | 使用 `SemaphoreSlim` 限制并发连接 |

---

## 六、协议配置说明

系统支持两种 OPC DA 连接方式，通过设备的 `protocol` 字段配置：

| 协议标识 | 说明 | 部署模式 |
|----------|------|----------|
| `OPC-DA-CENTER` | 中心采集模式 | 代理进程在中心，通过 DCOM 远程连接 OPC VM |
| `OPC-DA-SIDECAR` | 边缘推送模式 | Sidecar 部署在 OPC VM，本地 COM 连接后 HTTP 推送 |

### 协议名称变体支持

系统支持多种协议名称写法，会自动规范化：

| 协议 | 支持的变体 |
|------|------------|
| `opcda_center` | `opcda_center`, `opc_da_center`, `opc da center`, `opcda-center`, `opc-da-center` |
| `opcda_sidecar` | `opcda_sidecar`, `opc_da_sidecar`, `opc da sidecar`, `opcda-sidecar`, `opc-da-sidecar` |

### 中心采集模式配置

```sql
-- device 表配置
UPDATE device SET
  protocol_id = (SELECT id FROM comm_protocol WHERE protocol = 'OPC-DA-CENTER'),
  endpoint = '172.27.18.221'  -- OPC VM 地址
WHERE device_code = 'AGV801';
```

### 边缘推送模式配置

```sql
-- device 表配置
UPDATE device SET
  protocol_id = (SELECT id FROM comm_protocol WHERE protocol = 'OPC-DA-SIDECAR')
WHERE device_code = 'AGV801';
```

---

## 七、待修复问题详情

### 7.1 OPC Server 硬编码（P1）

**状态**: ✅ 已修复

**影响**: 不同现场可能使用不同的 OPC Server ProgID

**修复方案**: 使用 `comm_protocol.server` 字段存储 OPC Server ProgID

**修改文件**:
- `core/config_db.py`: ORM 模型添加 `server` 字段
- `core/config.py`: `DeviceConfig` 添加 `opc_server` 字段
- `device/manager.py`: 从配置动态获取 OPC Server

**配置示例**:
```sql
-- 在 comm_protocol 表中配置 OPC Server
INSERT INTO comm_protocol (id, protocol, port, server, description)
VALUES (3, 'OPC-DA-CENTER', 9090, 'ZPMC.OPCServer', '远程采集ZPMC...');
```

### 7.2 端口映射脆弱（P1）

**状态**: ✅ 已修复

**影响**: 不同网段的 VM 端口映射可能冲突

**修复方案**: 使用 `EndpointPortMapper` 动态分配端口

**修改文件**: `device/manager.py`

**修复内容**:
```python
class EndpointPortMapper:
    """VM endpoint 到代理端口的动态映射器"""

    def __init__(self, base_port: int = 9090, max_port: int = 9200):
        self._endpoint_to_port: Dict[str, int] = {}
        self._next_port = base_port

    def get_port(self, endpoint: str) -> int:
        """获取或分配 endpoint 对应的端口"""
        if endpoint not in self._endpoint_to_port:
            # 检测端口可用性，跳过被占用端口
            self._endpoint_to_port[endpoint] = self._find_next_available_port()
        return self._endpoint_to_port[endpoint]

    def _is_port_available(self, port: int) -> bool:
        """检测端口是否可用"""
        try:
            with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
                s.bind(("127.0.0.1", port))
                return True
        except OSError:
            return False
```

**特点**:
- 为每个 VM endpoint 分配独立端口
- 自动检测端口可用性，跳过被占用端口
- 从 9090 开始依次分配，最多到 9200
- 不依赖 IP 网段，移植友好

### 7.3 线程池限制（P1）

**状态**: ✅ 已修复

**影响**: 大量并发连接可能耗尽线程池资源

**修复方案**: 使用 `SemaphoreSlim` 限制并发连接数

**修改文件**: `OpcDaProxyService.cs`

**修复内容**:
```csharp
// 配置中添加最大连接数
public int MaxConnections { get; set; }

// 启动时初始化限制器
int maxConnections = _config.MaxConnections > 0 ? _config.MaxConnections : 20;
_connectionLimiter = new SemaphoreSlim(maxConnections, maxConnections);

// AcceptTcpClient 时检查限制
if (!_connectionLimiter.Wait(0))
{
    LogWarn($"Connection rejected (max connections reached)");
    client.Close();
    continue;
}

// 处理完毕后释放
finally
{
    _connectionLimiter?.Release();
}
```

**配置示例**:
```json
{
  "listenPort": 9090,
  "maxConnections": 20,
  "updateRateMs": 500,
  "connectionTimeoutMs": 10000
}
```

