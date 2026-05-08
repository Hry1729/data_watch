# OPC DA 中心采集代理服务

## 一、概述

OPC DA 中心采集代理服务是一个 **32 位（x86）.NET 程序**，用于解决 64 位 Python collector 无法直接访问 32 位 OPC DA COM 组件的问题。

通过 TCP/IP 协议，collector 主进程（64 位 Python）与代理进程（32 位 .NET）通信，实现 OPC DA 数据的远程采集。

---

## 二、架构设计

### 2.1 整体架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          中心采集服务器                                  │
│                                                                         │
│  ┌───────────────────────┐         ┌───────────────────────────────┐   │
│  │   Collector 主进程     │         │    OpcDaProxyService.exe     │   │
│  │   (Python 64-bit)     │         │    (.NET x86)                │   │
│  │                       │   TCP   │                               │   │
│  │  ┌─────────────────┐  │  :9090  │  ┌─────────────────────────┐ │   │
│  │  │OpcDaCenterDriver│◄─┼─────────┼─►│  TCP Listener           │ │   │
│  │  └─────────────────┘  │         │  └─────────────────────────┘ │   │
│  │          │            │         │              │                │   │
│  │          ▼            │         │              ▼                │   │
│  │  ┌─────────────────┐  │         │  ┌─────────────────────────┐ │   │
│  │  │OpcDaIpcClient   │  │         │  │OpcConnectionManager     │ │   │
│  │  └─────────────────┘  │         │  └─────────────────────────┘ │   │
│  │          │            │         │              │                │   │
│  └──────────┼────────────┘         └──────────────┼────────────────┘   │
│             │                                     │                     │
│             │                                     │ COM/DCOM            │
│             │                                     ▼                     │
│             │              ┌───────────────────────────────────────┐   │
│             │              │         OPC DA Server                 │   │
│             │              │  (本地: ZPMC.OPCServer)               │   │
│             │              │  (远程: 172.27.18.221 上的 OPC Server) │   │
│             │              └───────────────────────────────────────┘   │
│             │                                                             │
│             ▼                                                             │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                        DataBuffer                                │   │
│  │                           ▼                                      │   │
│  │                      TDengine                                    │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 为何需要代理进程

| 问题 | 说明 |
|------|------|
| **位数不兼容** | OPC DA 是 32 位 COM 组件，无法被 64 位 Python 直接加载 |
| **COM 注册限制** | 64 位进程无法调用 32 位 COM 服务器 |
| **解决方案** | 使用 32 位代理进程，通过进程间通信（IPC）桥接 |

---

## 三、数据流

### 3.1 采集数据流

```
┌─────────┐    ┌─────────────┐    ┌─────────────┐    ┌───────────┐    ┌──────────┐
│ OPC DA  │───►│   Proxy     │───►│    TCP      │───►│  Driver   │───►│ TDengine │
│ Server  │    │  (x86)      │    │   :9090     │    │ (Python)  │    │          │
└─────────┘    └─────────────┘    └─────────────┘    └───────────┘    └──────────┘
     │                │                  │                 │
     │   COM/DCOM     │    TCP/JSON      │   Python API    │
     │   读取数据      │    传输数据       │   写入数据库     │
     ▼                ▼                  ▼                 ▼
  点位值          序列化响应          解析并缓存        持久化存储
```

### 3.2 请求-响应流程

```
Collector                              Proxy Service
    │                                       │
    │  1. 连接 TCP (127.0.0.1:9090)         │
    │──────────────────────────────────────►│
    │                                       │
    │  2. 发送 CONNECT 请求                 │
    │  {request_id, "connect",              │
    │   payload: {opc_server, vm_host}}     │
    │──────────────────────────────────────►│
    │                                       │  3. 连接 OPC DA
    │                                       │     (COM/DCOM)
    │  4. 返回响应                          │
    │  {request_id, "ok", data: {connected}}│◄─────────────
    │◄──────────────────────────────────────│
    │                                       │
    │  5. 发送 READ 请求                    │
    │  {request_id, "read",                 │
    │   payload: {item_ids: [...]}}         │
    │──────────────────────────────────────►│
    │                                       │  6. 读取点位
    │                                       │     (IOPCSyncIO.Read)
    │  7. 返回点位数据                       │
    │  {request_id, "ok",                   │◄─────────────
    │   data: [{item_id, value, quality}]}  │
    │◄──────────────────────────────────────│
    │                                       │
    │  8. 发送 PING 心跳                    │
    │──────────────────────────────────────►│
    │                                       │
    │  9. 返回心跳响应                       │
    │◄──────────────────────────────────────│
    │                                       │
```

---

## 四、通信协议

### 4.1 协议格式

**传输层**：TCP 长连接

**应用层**：长度前缀 + JSON

```
┌────────────────┬─────────────────────────────┐
│  4 字节长度头   │       JSON 数据             │
│  (Big Endian)  │   (UTF-8 编码)              │
└────────────────┴─────────────────────────────┘
```

### 4.2 请求类型

| 类型 | 用途 | Payload |
|------|------|---------|
| `connect` | 连接 OPC DA 服务器 | `{opc_server, vm_host, update_rate_ms}` |
| `disconnect` | 断开 OPC 连接 | 无 |
| `read` | 读取点位数据 | `{item_ids: [...]}` |
| `ping` | 心跳检测 | 无 |
| `shutdown` | 关闭代理服务 | 无 |

### 4.3 消息示例

**连接请求**：
```json
{
  "request_id": "abc-123",
  "request_type": "connect",
  "payload": {
    "opc_server": "ZPMC.OPCServer",
    "vm_host": "",
    "update_rate_ms": 500
  },
  "version": "1.0"
}
```

**连接响应**：
```json
{
  "request_id": "abc-123",
  "status": "ok",
  "data": { "connected": true },
  "version": "1.0"
}
```

**读取请求**：
```json
{
  "request_id": "def-456",
  "request_type": "read",
  "payload": {
    "item_ids": [
      "OPD_Server.AGV801.Fault.Tag30041800",
      "OPD_Server.AGV801.Fault.Tag30041801"
    ]
  },
  "version": "1.0"
}
```

**读取响应**：
```json
{
  "request_id": "def-456",
  "status": "ok",
  "data": [
    {
      "item_id": "OPD_Server.AGV801.Fault.Tag30041800",
      "value": 123.45,
      "quality": 192,
      "timestamp": "2025-05-03T14:30:00.000+08:00"
    },
    {
      "item_id": "OPD_Server.AGV801.Fault.Tag30041801",
      "value": null,
      "quality": 0,
      "timestamp": "2025-05-03T14:30:00.000+08:00"
    }
  ],
  "version": "1.0"
}
```

### 4.4 质量代码

| 值 | 含义 |
|----|------|
| 192 (0xC0) | Good - 数据有效 |
| 0 | Bad - 数据无效 |
| 其他 | Uncertain 或混合状态 |

---

## 五、工作方式

### 5.1 本地模式（vm_host 为空）

```
┌──────────────────────────────────────────────┐
│              同一台 Windows 机器              │
│                                              │
│  ┌─────────────┐      COM      ┌──────────┐ │
│  │   Proxy     │◄──────────────│ OPC DA   │ │
│  │  (x86)      │               │ Server   │ │
│  └─────────────┘               └──────────┘ │
│        ▲                                     │
│        │ TCP                                 │
│        ▼                                     │
│  ┌─────────────┐                            │
│  │  Collector  │                            │
│  │  (Python)   │                            │
│  └─────────────┘                            │
└──────────────────────────────────────────────┘
```

**特点**：
- 无需配置 DCOM
- OPC Server 和 Proxy 在同一进程空间
- 延迟最低

### 5.2 远程模式（vm_host 有值）

```
┌────────────────────────┐         ┌────────────────────────┐
│   中心采集服务器        │  DCOM  │    OPC VM              │
│                        │  RPC   │   172.27.18.221        │
│  ┌─────────────┐       │        │  ┌──────────────────┐  │
│  │   Proxy     │◄──────┼────────┼──│ OPC DA Server    │  │
│  │  (x86)      │       │        │  │ ZPMC.OPCServer   │  │
│  └─────────────┘       │        │  └──────────────────┘  │
│        ▲               │        │                        │
│        │ TCP           │        └────────────────────────┘
│        ▼               │
│  ┌─────────────┐       │
│  │  Collector  │       │
│  │  (Python)   │       │
│  └─────────────┘       │
└────────────────────────┘
```

**特点**：
- 需要配置远程 DCOM 权限
- 需要开放防火墙 RPC 端口
- 支持采集多台 OPC VM

2026-05-04补充：

本次针对 OPC DA 远程采集链路中的 DCOM 与防火墙配置进行了基础排查。当前中心采集服务器与 OPC VM 位于同一网络环境，双方网络可达，远程访问连接正常；OPC VM 上 Windows 防火墙各配置文件未启用，因此本机防火墙不会拦截 DCOM/RPC 通信，暂不需要额外配置 TCP 135 或 RPC 动态端口入站规则。同时，从中心采集服务器访问 OPC VM 的 TCP 135 端口测试正常，说明 RPC Endpoint Mapper 入口可达；OPC VM 侧 DCOM 访问权限、启动和激活权限已按要求放开，当前配置允许 Everyone 访问，基础 DCOM 权限层面未发现明显限制。因此，后续如仍出现远程 OPC 连接异常，可暂时排除同网段网络连通、Windows 防火墙、TCP 135 端口和基础 DCOM 权限问题，继续从 OPCEnum 服务状态、ZPMC.OPCServer 注册状态、32 位/64 位 COM 组件匹配、OPC Server 的 DCOM Identity 配置、远程 COM 激活能力、事件查看器中的 DistributedCOM 错误，以及 Proxy 程序运行账户和 DCOM 加固兼容性等方向继续排查。需要注意的是，若后续网络环境、防火墙策略、安全软件或虚拟化平台安全策略发生变化，则仍需重新确认 TCP 135 及 RPC 通信是否被拦截。

### 5.3 连接管理

代理服务支持多客户端连接，每个连接独立管理：

```
                    ┌─────────────────────┐
                    │   Proxy Service     │
                    │   (单例进程)         │
                    └─────────────────────┘
                              │
            ┌─────────────────┼─────────────────┐
            │                 │                 │
            ▼                 ▼                 ▼
    ┌───────────────┐ ┌───────────────┐ ┌───────────────┐
    │  Client 1     │ │  Client 2     │ │  Client 3     │
    │  (AGV801)     │ │  (AGV802)     │ │  (AGV803)     │
    └───────────────┘ └───────────────┘ └───────────────┘
            │                 │                 │
            ▼                 ▼                 ▼
    ┌─────────────────────────────────────────────────┐
    │            共享 OPC Server 连接                  │
    │            (连接池复用)                          │
    └─────────────────────────────────────────────────┘
```

---

## 六、代码结构

### 6.1 文件列表

```
opcda_proxy_service/
├── OpcDaProxyService.cs       # 主程序入口、TCP 服务
├── OpcConnectionManager.cs    # OPC DA 连接管理、点位读取
├── opcda_proxy_config.json    # 配置文件
├── build.ps1                  # 编译脚本（PowerShell）
└── run.bat                    # 启动脚本
```

### 6.2 类图

```
┌─────────────────────────────────────────────────────────────────────┐
│                           OpcDaProxyService.cs                      │
├─────────────────────────────────────────────────────────────────────┤
│  class Program                                                       │
│  ├─ Main()                    # 入口函数                             │
│  ├─ StartListener()           # 启动 TCP 监听                        │
│  ├─ HandleClient()            # 处理客户端连接                        │
│  ├─ ProcessRequest()          # 分发请求处理                          │
│  └─ ProcessConnect/Read()     # 具体请求处理                         │
│                                                                      │
│  class IpcRequest                                                    │
│  ├─ RequestId                 # 请求 ID                              │
│  ├─ RequestType               # 请求类型                              │
│  ├─ Payload                   # 请求参数                              │
│  └─ FromJson()                # JSON 反序列化                        │
│                                                                      │
│  class IpcResponse                                                   │
│  ├─ RequestId                 # 对应请求 ID                          │
│  ├─ Status                    # ok/error                             │
│  ├─ Data                      # 响应数据                              │
│  ├─ Error                     # 错误信息                              │
│  └─ ToJson()                  # JSON 序列化                          │
│                                                                      │
│  class ProxyConfig                                                   │
│  ├─ ListenPort                # 监听端口                              │
│  ├─ OpcServers                # OPC 服务器配置列表                    │
│  └─ Load()                    # 加载配置文件                          │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        OpcConnectionManager.cs                      │
├─────────────────────────────────────────────────────────────────────┤
│  class OpcConnectionManager                                          │
│  ├─ Connect()                 # 连接 OPC 服务器                      │
│  ├─ Disconnect()              # 断开所有连接                          │
│  ├─ ReadItems()               # 读取点位数据                          │
│  └─ _connections              # 连接池（Dictionary）                 │
│                                                                      │
│  class OpcServerConnection                                           │
│  ├─ Connect()                 # 建立 COM/DCOM 连接                   │
│  ├─ ReadItems()               # 创建临时组读取点位                    │
│  ├─ CreateTempGroup()         # 创建临时 OPC 组                      │
│  └─ RemoveGroup()             # 移除临时组                            │
│                                                                      │
│  class GroupContext                                                  │
│  ├─ ServerGroupHandle         # OPC 组句柄                           │
│  ├─ ItemMgt                   # IOPCItemMgt 接口                     │
│  ├─ SyncIO                    # IOPCSyncIO 接口                      │
│  └─ ItemBindings              # 点位绑定列表                          │
│                                                                      │
│  ─────────────────────────────────────────────                       │
│  OPC DA COM Interfaces:                                              │
│  ├─ IOPCServer                # OPC 服务器接口                       │
│  ├─ IOPCItemMgt               # OPC 项目管理接口                      │
│  └─ IOPCSyncIO                # OPC 同步读写接口                      │
└─────────────────────────────────────────────────────────────────────┘
```

### 6.3 调用序列

```
Program.Main()
    │
    ├──► ProxyConfig.Load()           # 加载配置
    │
    ├──► StartListener()              # 启动 TCP 监听
    │       │
    │       └──► AcceptTcpClient()    # 等待客户端连接
    │               │
    │               └──► HandleClient()
    │                       │
    │                       ├──► ReadExact(4 bytes)    # 读取长度
    │                       ├──► ReadExact(N bytes)    # 读取消息
    │                       ├──► IpcRequest.FromJson() # 解析请求
    │                       │
    │                       ├──► ProcessRequest()
    │                       │       │
    │                       │       ├──► "connect" → ProcessConnect()
    │                       │       │                  │
    │                       │       │                  └──► OpcConnectionManager.Connect()
    │                       │       │                          │
    │                       │       │                          └──► OpcServerConnection.Connect()
    │                       │       │                                  │
    │                       │       │                                  ├──► Type.GetTypeFromProgID()
    │                       │       │                                  └──► Activator.CreateInstance()
    │                       │       │
    │                       │       └──► "read" → ProcessRead()
    │                       │                       │
    │                       │                       └──► OpcConnectionManager.ReadItems()
    │                       │                               │
    │                       │                               └──► OpcServerConnection.ReadItems()
    │                       │                                       │
    │                       │                                       ├──► CreateTempGroup()
    │                       │                                       │       ├──► IOPCServer.AddGroup()
    │                       │                                       │       └──► IOPCItemMgt.AddItems()
    │                       │                                       │
    │                       │                                       ├──► IOPCSyncIO.Read()
    │                       │                                       │
    │                       │                                       └──► RemoveGroup()
    │                       │
    │                       └──► SendResponse()       # 发送响应
    │
    └──► OpcConnectionManager.Dispose()  # 清理资源
```

---

## 七、配置说明

### 7.1 配置来源

**重要说明**：中心采集模式的 OPC 配置**从数据库动态获取**，而非 JSON 配置文件。

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         配置数据流                                        │
│                                                                          │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐              │
│  │   MySQL     │     │  Collector  │     │   Proxy     │              │
│  │   数据库    │────►│  (Python)   │────►│  (.NET)     │              │
│  └─────────────┘     └─────────────┘     └─────────────┘              │
│         │                   │                   │                      │
│         │                   │                   │                      │
│         ▼                   ▼                   ▼                      │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │ device 表:                                                       │   │
│  │   - device_code: agv801                                         │   │
│  │   - endpoint: 172.27.18.221  ← OPC VM 地址                      │   │
│  │   - protocol_id: 9001 (opcda_center)                            │   │
│  │                                                                  │   │
│  │ device_variable 表:                                              │   │
│  │   - var_address: OPD_Server.AGV801.Fault.Tag30041800 ← item_id  │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────────────┘
```

### 7.2 JSON 配置文件（仅代理进程参数）

代理服务的 JSON 配置文件**仅包含代理进程自身的参数**：

```json
{
  "listenPort": 9090,
  "updateRateMs": 500,
  "connectionTimeoutMs": 10000,

  "_comment": "以下 opcServers 仅供文档参考，实际配置从数据库获取",
  "opcServers": [
    {
      "vmHost": "172.27.18.221",
      "serverProgId": "ZPMC.OPCServer",
      "channel": "OPD_Server",
      "devices": ["AGV801", "AGV802", ...]
    }
  ]
}
```

### 7.3 配置项说明

| 字段 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `listenPort` | int | TCP 监听端口 | 9090 |
| `updateRateMs` | int | OPC 组更新速率（毫秒） | 500 |
| `connectionTimeoutMs` | int | 连接超时（毫秒） | 10000 |
| `opcServers` | array | **仅供参考**，实际配置从数据库获取 | - |

### 7.4 数据库配置（device 表）

| 字段 | 说明 | 示例 |
|------|------|------|
| `device_code` | 设备编码 | `agv801` |
| `protocol_id` | 协议 ID | `3` (OPCDA_CENTER) |
| `endpoint` | OPC VM 地址 | `172.27.18.221` |
| `enable` | 是否启用 | `1` |

### 7.5 数据库配置（device_variable 表）

| 字段 | 说明 | 示例 |
|------|------|------|
| `device_id` | 设备主键 | `151` |
| `var_seq` | 点位序号 | `306` |
| `var_address` | OPC item_id | `OPD_Server.AGV801.Fault.Tag30007101` |
| `is_storage` | 是否存储 | `1` |

---

## 八、编译与运行

### 8.1 编译要求

- Windows 操作系统
- .NET Framework 4.8（或 4.x）
- OPC DA COM 组件已注册

### 8.2 编译命令

```powershell
cd collector/sidecars/opcda_proxy_service
powershell -ExecutionPolicy Bypass -File build.ps1
```

输出：`bin/OpcDaProxyService.exe`

### 8.3 运行命令

```batch
run.bat
```

或直接运行：

```batch
bin\OpcDaProxyService.exe --config opcda_proxy_config.json
```

### 8.4 命令行参数

| 参数 | 说明 |
|------|------|
| `-c, --config <path>` | 配置文件路径 |
| `-h, --help` | 显示帮助信息 |

---

## 九、与 Collector 集成

### 9.1 协议注册

```sql
-- MySQL 中注册协议
INSERT INTO comm_protocol (id, protocol, port, description)
VALUES (9001, 'opcda_center', 9090, 'OPC DA Center Collection via Proxy');
```

### 9.2 设备配置

```sql
-- 配置设备使用中心采集协议
UPDATE device SET 
    protocol_id = 9001,
    endpoint = ''  -- 本地 OPC DA
WHERE device_code = 'agv801';

-- 远程 OPC DA
UPDATE device SET 
    protocol_id = 9001,
    endpoint = '172.27.18.221'  -- 远程 VM
WHERE device_code = 'agv801';
```

### 9.3 点位配置

`device_variable.var_address` 需要配置完整的 OPC item_id：

```
OPD_Server.AGV801.Fault.Tag30041800
```

格式：`{channel}.{device}.{group}.{tag}`

---

## 十、错误排查

### 10.1 常见错误

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `Could not create COM server` | OPC Server 未注册 | 运行 OPC Server 注册工具 |
| `Access denied` | DCOM 权限不足 | 配置 DCOM 权限 |
| `RPC server unavailable` | 防火墙阻止 RPC | 开放 RPC 端口（135 + 动态端口） |
| `Invalid item ID` | item_id 格式错误 | 检查点位配置 |
| `Quality = 0` | 点位不存在或质量差 | 检查 OPC Server 状态 |

### 10.2 日志分析

代理服务输出格式：

```
2025-05-03 14:30:00.123 [INFO] Listening on 0.0.0.0:9090
2025-05-03 14:30:05.456 [INFO] Client connected: 127.0.0.1:54321
2025-05-03 14:30:05.789 [INFO] Connected to OPC server: ZPMC.OPCServer
2025-05-03 14:30:10.012 [ERROR] Read failed: Invalid item ID
```

---

## 十一、性能优化

### 11.1 当前策略

- 每次读取创建临时 OPC 组，读取后销毁
- 支持批量读取多个点位
- TCP 长连接复用

### 11.2 优化方向

| 优化项 | 说明 | 预期收益 |
|--------|------|----------|
| 组复用 | 保持 OPC 组不销毁 | 减少 COM 调用开销 |
| 订阅模式 | 使用 IOPCAsyncIO2 订阅 | 数据推送，减少轮询 |
| 连接池 | 多连接并行读取 | 提高吞吐量 |
| 批量优化 | 合并多个请求 | 减少网络往返 |

---

## 十二、附录

### 12.1 OPC DA 质量代码

| 代码 | 名称 | 含义 |
|------|------|------|
| 0xC0 (192) | Good | 数据质量良好 |
| 0x00 (0) | Bad | 数据质量差 |
| 0x40 (64) | Uncertain | 数据质量不确定 |
| 0xD8 (216) | Good - Local Override | 本地覆盖值 |

### 12.2 相关文件

| 文件路径 | 说明 |
|----------|------|
| `collector/core/opcda_ipc_protocol.py` | Python 端协议定义 |
| `collector/core/opcda_ipc_client.py` | Python 端 IPC 客户端 |
| `collector/drivers/opcda_center_driver.py` | Python 端驱动实现 |
| `docs/采集模块/OPC_DA_中心采集实施计划.md` | 实施计划文档 |
