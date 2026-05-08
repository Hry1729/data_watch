# OPC DA 中心采集模式实施计划

## 一、概述

### 1.1 目标
实现中心采集模式，使 collector 可以直接从中心服务器远程采集 OPC DA 数据，无需在每台 OPC VM 上部署 sidecar。

### 1.2 选择方案
**B1: 32 位代理进程** - 在中心服务器运行 x86 代理程序，通过 IPC 与 collector 主进程通信。

### 1.3 整体置信度
**0.75**

---

## 二、架构设计

```
┌─────────────────────────────────────────────────────────────┐
│                中心采集服务器 (collector)                    │
│                                                             │
│  ┌─────────────────┐         ┌─────────────────────────┐   │
│  │ OpcDaCenterDriver│◄──────►│ OpcDaProxyService.exe   │   │
│  │ (Python 64-bit) │  TCP    │ (x86 .NET)              │   │
│  │                 │  :9090  │                         │   │
│  └─────────────────┘         └─────────────────────────┘   │
│         │                              │                    │
│         │                              │ 远程 DCOM          │
│         ▼                              ▼                    │
│  ┌─────────────────┐         ┌─────────────────────────┐   │
│  │ DataBuffer      │         │ OPC VM (172.27.18.221)  │   │
│  │ Aggregator      │         │ ZPMC.OPCServer          │   │
│  │ TDengine        │         │ AGV801-AGV810           │   │
│  └─────────────────┘         └─────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

---

## 三、文件结构

### 3.1 新增文件（不修改现有文件）

```
collector/
├── drivers/
│   └── opcda_center_driver.py      # ✅ 新增：中心采集驱动
│
├── sidecars/
│   └── opcda_proxy_service/        # ✅ 新增：代理程序目录
│       ├── OpcDaProxyService.cs    # ✅ 新增：代理服务源码
│       ├── OpcConnectionManager.cs # ✅ 新增：OPC 连接管理
│       ├── build.ps1               # ✅ 新增：编译脚本
│       ├── run.bat                 # ✅ 新增：启动脚本
│       └── opcda_proxy_config.json # ✅ 新增：代理配置
│
├── core/
│   ├── opcda_ipc_protocol.py       # ✅ 新增：IPC 协议定义
│   └── opcda_ipc_client.py         # ✅ 新增：IPC 客户端
│
└── scripts/
    └── init_opcda_center_protocol.sql  # ✅ 新增：协议初始化 SQL
```

### 3.2 复用但不修改的文件

| 文件 | 复用方式 |
|------|----------|
| `sidecars/opcda_push_service/OpcDaPushService.cs` | 参考 OPC DA COM 操作代码 |
| `core/opcda_mapper.py` | 直接引用，用于 item_id 映射 |
| `drivers/base.py` | 继承 BaseDriver 基类 |

### 3.3 修改的文件

| 文件 | 修改内容 |
|------|----------|
| `device/manager.py` | 新增中心采集协议支持函数和驱动创建逻辑 |

---

## 四、分阶段实施计划

### 阶段 1：IPC 协议定义 ✅ 已完成

**目标**：定义 collector 与代理进程之间的通信协议

**文件**：`collector/core/opcda_ipc_protocol.py`

**协议格式**：
```python
# 请求类型
REQUEST_READ = "read"      # 读取点位
REQUEST_PING = "ping"      # 心跳检测
REQUEST_SHUTDOWN = "shutdown"  # 关闭代理

# 响应格式
{
    "status": "ok" | "error",
    "data": [...],  # 读取结果
    "error": "错误信息"
}
```

**工作量**：0.5 天 → 实际完成

---

### 阶段 2：代理服务程序开发 ✅ 已完成

**目标**：开发 x86 代理服务程序

**文件**：`collector/sidecars/opcda_proxy_service/`

**核心功能**：
1. 监听 TCP 端口（默认 9090）
2. 接收读取请求
3. 远程 DCOM 连接 OPC DA
4. 返回点位数据

**配置示例**：
```json
{
  "listenPort": 9090,
  "opcServers": [
    {
      "vmHost": "172.27.18.221",
      "serverProgId": "ZPMC.OPCServer",
      "channel": "OPD_Server",
      "devices": ["AGV801", "AGV802", "AGV803", "AGV804", "AGV805",
                  "AGV806", "AGV807", "AGV808", "AGV809", "AGV810"]
    }
  ],
  "updateRateMs": 500,
  "connectionTimeoutMs": 10000
}
```

**工作量**：3 天 → 实际完成

---

### 阶段 3：IPC 客户端开发 ✅ 已完成

**目标**：开发 Python IPC 客户端

**文件**：`collector/core/opcda_ipc_client.py`

**接口**：
```python
class OpcDaIpcClient:
    """与代理进程通信的 IPC 客户端"""
    
    async def connect(self, host: str, port: int) -> None:
        """连接代理进程"""
        
    async def read_items(self, item_ids: List[str]) -> Dict[str, Any]:
        """读取多个点位"""
        
    async def ping(self) -> bool:
        """心跳检测"""
        
    async def close(self) -> None:
        """关闭连接"""
```

**工作量**：1 天 → 实际完成

---

### 阶段 4：驱动程序开发 ✅ 已完成

**目标**：开发 OpcDaCenterDriver 驱动

**文件**：`collector/drivers/opcda_center_driver.py`

**接口**：
```python
class OpcDaCenterDriver(BaseDriver):
    """OPC DA 中心采集驱动"""
    
    async def connect(self, config: Dict[str, Any]) -> None:
        """
        连接代理进程
        config:
          - proxy_host: 127.0.0.1
          - proxy_port: 9090
          - vm_host: 172.27.18.221 (用于标识来源)
        """
        
    async def read(self, blocks: List[ReadBlock]) -> Dict[str, Any]:
        """
        通过代理读取远程 OPC DA
        ReadBlock.address 约定为完整 item_id
        """
```

**工作量**：2 天 → 实际完成

---

### 阶段 5：协议注册 ✅ 已完成

**目标**：在系统中注册新协议

**文件**：`collector/scripts/init_opcda_center_protocol.sql`

**SQL**：
```sql
-- 新增协议
INSERT INTO comm_protocol (id, protocol, port, description)
VALUES (9001, 'opcda_center', 9090, 'OPC DA Center Collection via Proxy')
ON DUPLICATE KEY UPDATE
    protocol = VALUES(protocol),
    description = VALUES(description);

-- 更新设备使用中心采集协议
UPDATE device SET protocol_id = 9001 
WHERE device_code = 'agv801' AND protocol_id = 9999;
```

**工作量**：0.5 天 → 实际完成

---

### 阶段 6：设备管理集成 ✅ 已完成

**目标**：修改 DeviceManager 支持新协议

**文件**：`collector/device/manager.py`

**改动**：
```python
# 新增中心采集协议集合
CENTER_COLLECTION_PROTOCOLS = {"opcda_center"}

def is_center_collection_protocol(protocol: str) -> bool:
    return protocol.lower() in CENTER_COLLECTION_PROTOCOLS

# 在协议处理逻辑中增加
async def _connect_center_collection_device(self, device: Device) -> bool:
    """连接中心采集设备（通过代理进程）"""
    ...

# 驱动创建逻辑中增加
elif protocol.lower() == "opcda_center":
    from drivers.opcda_center_driver import OpcDaCenterDriver
    return OpcDaCenterDriver()
```

**工作量**：1 天 → 实际完成

---

### 阶段 7：集成测试 ⏳ 待执行

**测试项**：
1. 代理进程启动和停止
2. 代理连接远程 OPC DA
3. 驱动与代理通信
4. 数据写入 TDengine
5. 断线重连测试
6. 性能压测

**工作量**：2 天

---

## 五、各阶段置信度汇总

| 阶段 | 任务 | 置信度 | 状态 |
|------|------|--------|------|
| 1 | IPC 协议定义 | 0.95 | ✅ 已完成 |
| 2 | 代理服务程序 | 0.80 | ✅ 已完成 |
| 3 | IPC 客户端 | 0.85 | ✅ 已完成 |
| 4 | 驱动程序 | 0.75 | ✅ 已完成 |
| 5 | 协议注册 | 0.90 | ✅ 已完成 |
| 6 | 设备管理集成 | 0.80 | ✅ 已完成 |
| 7 | 集成测试 | 0.70 | ⏳ 待执行 |

**整体置信度：0.75**

---

## 六、风险与缓解

| 风险 | 概率 | 影响 | 缓解措施 |
|------|------|------|----------|
| 远程 DCOM 配置失败 | 高 | 高 | 提供详细配置文档，考虑 OPC UA 网关替代 |
| 防火墙阻止 RPC 端口 | 高 | 高 | 提前与网络管理员协调端口开放 |
| 代理进程崩溃 | 中 | 高 | 实现自动重启和心跳检测 |
| 网络延迟影响采集 | 中 | 中 | 使用订阅模式，增加缓存队列 |
| DCOM 权限问题 | 高 | 中 | 提供 Windows 安全配置指南 |

---

## 七、DCOM 配置指南（关键）

### 7.1 OPC VM 端配置

```powershell
# 1. 启用远程 DCOM
dcomcnfg.exe
# -> 组件服务 -> 计算机 -> 我的电脑 -> 属性
# -> 默认属性 -> 启用分布式 COM

# 2. 配置 OPC Server 权限
# -> DCOM 配置 -> ZPMC.OPCServer -> 属性
# -> 安全 -> 启动和激活权限 -> 自定义 -> 编辑
# -> 添加 collector 服务账号，允许本地启、远程启动

# 3. 防火墙开放端口
# RPC 动态端口范围：49152-65535
# 或配置固定 RPC 端口
netsh advfirewall firewall add rule name="RPC" dir=in action=allow protocol=tcp localport=135
```

### 7.2 Collector 服务端配置

```powershell
# 1. 配置 DCOM 客户端
dcomcnfg.exe
# -> 组件服务 -> 计算机 -> 我的电脑 -> 属性
# -> 默认属性 -> 默认身份验证级别：连接
# -> 默认模拟级别：标识

# 2. 配置凭据
cmdkey /add:172.27.18.221 /user:DOMAIN\opc_user /pass:password
```

---

## 八、时间估算

| 阶段 | 工作量 | 依赖 | 状态 |
|------|--------|------|------|
| 阶段 1 | 0.5 天 | 无 | ✅ 已完成 |
| 阶段 2 | 3 天 | 阶段 1 | ✅ 已完成 |
| 阶段 3 | 1 天 | 阶段 1 | ✅ 已完成 |
| 阶段 4 | 2 天 | 阶段 2, 3 | ✅ 已完成 |
| 阶段 5 | 0.5 天 | 无 | ✅ 已完成 |
| 阶段 6 | 1 天 | 阶段 4, 5 | ✅ 已完成 |
| 阶段 7 | 2 天 | 全部 | ⏳ 待执行 |
| **总计** | **10 天** | | **85% 完成** |

---

## 九、验收标准

1. ⏳ 代理进程可以远程连接 OPC DA 服务器
2. ⏳ 驱动可以通过代理读取点位数据
3. ⏳ 数据正确写入 TDengine
4. ⏳ 断线后可以自动重连
5. ⏳ 采集延迟 < 1 秒
6. ⏳ 系统稳定运行 24 小时无故障

---

## 十、后续优化方向

1. **订阅模式支持**：代理进程支持 OPC DA 订阅，减少主动轮询开销
2. **多 OPC VM 支持**：一个代理进程支持连接多台 OPC VM
3. **本地缓存**：网络中断时代理进程缓存数据，恢复后补发
4. **监控告警**：代理进程状态监控和告警
