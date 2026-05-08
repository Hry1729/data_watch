# AGV OPC DA 远程采集对接方案

## 一、生产环境拓扑

根据用户描述的生产环境：

```
┌─────────────────────────────────────────────────────────────────┐
│                        OPC DA 虚拟机 (172.27.18.221)            │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  ZPMC.OPCServer (32-bit COM)                            │   │
│  │  ├─ AGV801 ─┬─ Control                                   │   │
│  │  ├─ AGV802  │─ Fault                                     │   │
│  │  ├─ ...     │─ ViewMain                                  │   │
│  │  └─ AGV810 ─┴─ ViewPage1-6                               │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  OpcDaPushService.exe (x86 .NET Framework)              │   │
│  │  - 订阅/轮询 OPC DA                                       │   │
│  │  - HTTP 推送到中心 collector                             │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ HTTP POST /ingest/opcda-tags
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    中心采集服务器 (collector)                    │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  InterfaceIngestServer (:8090)                          │   │
│  │  - 接收 opcda_tag_batch_v1                              │   │
│  │  - OpcDaTagMapper: item_id → variable_id                │   │
│  │  - DataBuffer → Aggregator → TDengine                   │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## 二、两种部署方案对比

### 方案 A：每台 VM 部署 Sidecar（推荐，置信度 0.90）

**架构**：每台 OPC VM 上运行一个 OpcDaPushService.exe

**优点**：
- 避免远程 DCOM 配置复杂性
- 本地 COM 调用稳定可靠
- sidecar 故障只影响该 VM 上的 AGV
- 网络故障时可本地缓存

**缺点**：
- 需要在每台 VM 上部署 sidecar
- 需要管理多个 sidecar 进程

**实现步骤**：
1. 在 `172.27.18.221` 上部署 `OpcDaPushService.exe`
2. 配置 `collectorUrl` 指向中心 collector 的 `http://<中心IP>:8090/ingest/opcda-tags`
3. 中心 collector 通过 item_id 映射到 MySQL 点位配置

### 方案 B：远程 DCOM 访问（置信度 0.60）

**架构**：中心 collector 直接远程连接 OPC DA

**优点**：
- sidecar 部署简单，只在中心运行

**缺点**：
- DCOM 远程配置复杂（防火墙、RPC 动态端口、服务账号）
- 64 位 Python 无法直接加载 32 位 OPC DA COM
- 网络延迟可能影响订阅回调
- 远程故障排查困难

## 三、MySQL 数据模型调整

需要在 `device` 表中增加字段记录 AGV 所在的 OPC VM 地址：

### 3.1 方案一：使用现有 endpoint 字段

```
device 表:
- device_code: agv801
- protocol_id: opcda_sidecar (新增协议类型)
- endpoint: 172.27.18.221 (OPC VM 地址，用于标识来源)
```

### 3.2 方案二：新增 opc_vm_host 字段

```sql
ALTER TABLE device ADD COLUMN opc_vm_host VARCHAR(64) DEFAULT NULL 
COMMENT 'OPC DA 虚拟机地址（用于 OPC DA sidecar 设备标识来源）';
```

### 3.3 推荐：使用 endpoint 字段

置信度：0.85

原因：
- 不需要修改表结构
- endpoint 字段对 `opcda_sidecar` 协议的设备可以复用为"OPC VM 地址"
- sidecar 推送的数据中已包含 `device_id`，中心不需要反向查询

## 四、配置 SQL 更新

### 4.1 设备配置

```sql
-- AGV801-AGV810 在 VM 172.27.18.221
INSERT INTO device (id, device_code, device_name, type_id, group_id, status, protocol_id, endpoint)
VALUES
    (100000, 'agv801', 'AGV801', 9922, 9912, 1, 9999, '172.27.18.221'),
    (100001, 'agv802', 'AGV802', 9922, 9912, 1, 9999, '172.27.18.221'),
    ...
    (100009, 'agv810', 'AGV810', 9922, 9912, 1, 9999, '172.27.18.221');
```

### 4.2 点位配置

关键：`var_address` 存储完整的 OPC DA item_id

```sql
INSERT INTO device_variable (id, device_id, var_seq, var_name, var_type, var_address)
VALUES
    (200000, 100000, 1, 'ViewMain_Tag30041800', 'BOOL', 'OPD_Server.AGV801.ViewMain.Tag30041800');
```

### 4.3 OPC VM 配置表（可选）

如果需要管理多台 OPC VM，可以新增配置表：

```sql
CREATE TABLE opc_vm_config (
    id INT PRIMARY KEY AUTO_INCREMENT,
    vm_host VARCHAR(64) NOT NULL COMMENT 'VM IP 地址',
    vm_name VARCHAR(128) COMMENT 'VM 名称',
    opc_server_prog_id VARCHAR(128) NOT NULL COMMENT 'OPC Server ProgID',
    channel VARCHAR(64) NOT NULL COMMENT 'OPC Channel',
    device_range_start INT COMMENT '负责的设备 ID 起始',
    device_range_end INT COMMENT '负责的设备 ID 结束',
    status TINYINT DEFAULT 1 COMMENT '1=在线, 0=离线',
    last_heartbeat DATETIME COMMENT '最后心跳时间',
    UNIQUE KEY uk_vm_host (vm_host)
) COMMENT 'OPC DA 虚拟机配置';
```

## 五、Sidecar 配置更新

### 5.1 配置文件

```json
{
  "serverProgId": "ZPMC.OPCServer",
  "comHost": "",
  "channel": "OPD_Server",
  "itemFormat": "{channel}.{device}.{group}.{tag}",
  "readMode": "subscription",
  "postEnabled": true,
  "collectorUrl": "http://<中心IP>:8090/ingest/opcda-tags",
  "apiKey": "<可选的API密钥>",
  "source": "cache",
  "updateRateMs": 500,
  "devices": [
    {
      "deviceId": "agv801",
      "opcDevice": "AGV801",
      "groups": [...]
    }
  ]
}
```

### 5.2 推送载荷示例

```json
{
  "source": "opcda_sidecar",
  "schema": "opcda_tag_batch_v1",
  "server": "ZPMC.OPCServer",
  "com_host": "172.27.18.221",
  "channel": "OPD_Server",
  "sent_at": "2026-05-02T15:30:00+08:00",
  "points": [
    {
      "device_id": "agv801",
      "opc_device": "AGV801",
      "group": "ViewMain",
      "tag": "Tag30041800",
      "item_id": "OPD_Server.AGV801.ViewMain.Tag30041800",
      "value": true,
      "quality": 192,
      "quality_good": true,
      "timestamp": "2026-05-02T15:30:00+08:00"
    }
  ]
}
```

## 六、对接流程

### 6.1 中心 Collector 准备

1. **确认 ingest_server 已支持 opcda_tag_batch_v1**
   - `ingest_server.py` 的 `_handle_opcda_ingest()` 已实现
   - 需要 `OpcDaTagMapper` 初始化

2. **执行 MySQL 配置 SQL**
   ```bash
   mysql -u root -p device_watch < scripts/agv_opcda_config.sql
   ```

3. **重启 collector**
   ```bash
   python main.py
   ```

4. **验证 mapper 加载**
   ```bash
   curl http://127.0.0.1:8090/ingest/health
   # 应返回 opcda_ingest_path 和 opcda_mappings 数量
   ```

### 6.2 Sidecar 部署

1. **复制 sidecar 到 OPC VM**
   ```powershell
   # 在 OPC VM 上
   copy \\共享目录\OpcDaPushService D:\OpcDaPushService
   ```

2. **编辑配置文件**
   ```powershell
   copy opcda_push_config.example.json opcda_push_config.json
   notepad opcda_push_config.json
   # 修改 collectorUrl 为中心 collector 地址
   ```

3. **运行 sidecar**
   ```powershell
   .\run.bat
   ```

### 6.3 验证对接

1. **检查 sidecar 日志**
   - 确认 OPC DA 订阅成功
   - 确认 HTTP POST 成功

2. **检查 collector 日志**
   - 确认收到 opcda_tag_batch_v1 请求
   - 确认 item_id 映射成功

3. **检查 TDengine 写入**
   ```sql
   SELECT COUNT(*) FROM agv.agv801_1;
   ```

## 七、置信度评估

| 组件 | 置信度 | 说明 |
|------|--------|------|
| Sidecar 本地采集 | 0.95 | 已在本地测试环境验证 |
| HTTP 推送到中心 | 0.92 | 代码已实现，待网络验证 |
| item_id 映射 | 0.90 | 需确保 MySQL var_address 正确配置 |
| TDengine 写入 | 0.88 | 需确保 var_seq 与子表对应 |
| 端到端数据流 | 0.85 | 完整链路待生产验证 |

## 八、风险与缓解

| 风险 | 影响 | 缓解措施 |
|------|------|----------|
| 网络中断 | 采集数据丢失 | sidecar 增加本地缓存队列 |
| item_id 不匹配 | 点位无法入库 | 提前验证 CSV tag 与 MySQL 配置 |
| OPC Server 重启 | 临时中断 | sidecar 自动重连机制 |
| TDengine 写入失败 | 数据积压 | collector 增加重试和告警 |

## 九、下一步行动

1. **执行 MySQL 配置 SQL**（立即可做）
2. **更新 sidecar 配置**（需要 OPC VM 访问权限）
3. **验证端到端数据流**（需要两边配合）
