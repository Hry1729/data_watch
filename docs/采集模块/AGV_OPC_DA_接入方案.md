# AGV 32 位 OPC DA 接入方案

更新时间：2026-05-02

## 结论

AGV 的 ZPMC 自研 OPC 数据源属于 **32 位 OPC DA**。当前 collector 主进程使用 64 位 Python 运行，不能在同一进程内稳定加载 32 位 OPC DA COM 组件。

正式推荐方案已经确认：

1. collector 主进程继续保持 64 位 Python，负责 MySQL 配置、数据队列、聚合、TDengine 写入、健康检查和统一日志。
2. AGV OPC DA 读取由 **32 位 C#/.NET Framework sidecar** 独立完成。
3. sidecar 只读 OPC DA Item，不执行任何写入或控制类操作。
4. sidecar 通过 collector 的 HTTP ingest 接口把数据推送回主采集链路。
5. collector 侧使用 `protocol=opcda_sidecar` 表示外部推送设备；直接配置 `protocol=opcda` 会被启动校验拒绝。

本方案的核心是"位数隔离 + 职责隔离"：OPC DA COM 访问留在 32 位 Windows 进程内，collector 不直接碰 OPC DA COM。

---

## 正式架构

```
┌─────────────────────────────────────────────────────────────────┐
│  ZPMC 32 位 OPC DA Server (ZPMC.OPCServer)                      │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ COM/DCOM (32 位客户端)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  C#/.NET Framework x86 Sidecar (OpcDaPushService.exe)           │
│  - 订阅模式 (UpdateRate=500ms)                                   │
│  - 推送端点: POST /ingest/opcda-tags                             │
│  - Payload schema: opcda_tag_batch_v1                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ HTTP POST
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  Python Collector (64 位)                                        │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ InterfaceIngestServer                                        ││
│  │ - /ingest/opcda-tags 端点                                    ││
│  │ - OpcDaTagMapper: item_id → variable_id 映射                 ││
│  │ - 写入 DataBuffer                                            ││
│  └─────────────────────────────────────────────────────────────┘│
│                              │                                    │
│                              ▼                                    │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │ DataBuffer → DataAggregator → BatchWriter                   ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  TDengine                                                        │
│  - 数据库: agv                                                   │
│  - 超级表: agv801, agv802, ...                                   │
│  - 子表: agv801_1, agv801_2, ... (device_code + var_seq)        │
└─────────────────────────────────────────────────────────────────┘
```

---

## Sidecar 配置

### 配置文件格式

```json
{
  "serverProgId": "ZPMC.OPCServer",
  "comHost": "",
  "channel": "OPD_Server",
  "itemFormat": "{channel}.{device}.{group}.{tag}",
  "readMode": "subscription",
  "postEnabled": true,
  "collectorUrl": "http://127.0.0.1:8090/ingest/opcda-tags",
  "updateRateMs": 500,
  "pushIntervalMs": 500,
  "reportIntervalMs": 5000,
  "includeBadQuality": true,
  "devices": [
    {
      "deviceId": "agv801",
      "opcDevice": "AGV801",
      "groups": [
        { "name": "Fault", "csv": "data/AGV801/Fault.csv" },
        { "name": "ViewMain", "csv": "data/AGV801/ViewMain.csv" }
      ]
    }
  ]
}
```

### 推送载荷格式

```json
{
  "source": "opcda_sidecar",
  "schema": "opcda_tag_batch_v1",
  "points": [
    {
      "device_id": "agv801",
      "item_id": "OPD_Server.AGV801.ViewMain.Tag30041800",
      "value": 123.45,
      "quality": 192,
      "timestamp": "2026-05-02T18:00:00+08:00"
    }
  ]
}
```

---

## MySQL 点位配置

### 设备表

```sql
INSERT INTO device (device_code, device_name, type_id, group_id, enable, protocol_id, endpoint)
VALUES ('agv801', 'AGV801', 
        (SELECT id FROM device_type WHERE type_code='AGV'), 
        1, 1, 
        (SELECT id FROM comm_protocol WHERE protocol='opcda_sidecar'), 
        '127.0.0.1:8090');
```

### 点位表

```sql
INSERT INTO device_variable (device_id, var_seq, var_name, var_type, var_address, is_storage)
VALUES (
  (SELECT id FROM device WHERE device_code='agv801'),
  1,
  '大车行走速度',
  'REAL',
  'OPD_Server.AGV801.ViewMain.Tag30041800',  -- 完整 item_id
  1
);
```

**重要**: `var_address` 字段存储完整的 OPC DA item_id，用于映射到 `variable_id`。

---

## Tag 映射机制

### 映射器 (OpcDaTagMapper)

```python
# 核心映射逻辑
class OpcDaTagMapper:
    def __init__(self, variables):
        # key: var_address (完整 item_id) → value: variable_id
        self._mapping = {var.address: var.variable_id for var in variables}
    
    def get_variable_id(self, item_id: str) -> Optional[str]:
        return self._mapping.get(item_id)
```

### 映射流程

```
Sidecar 推送: item_id = "OPD_Server.AGV801.ViewMain.Tag30041800"
                    │
                    ▼
OpcDaTagMapper.get_variable_id()
                    │
                    ▼
MySQL 查询: var_address = "OPD_Server.AGV801.ViewMain.Tag30041800"
                    │
                    ▼
返回: variable_id = "1001"
```

---

## 文件清单

| 文件 | 功能 |
|------|------|
| `sidecars/opcda_push_service/OpcDaPushService.cs` | C# sidecar 主程序 |
| `sidecars/opcda_push_service/opcda_push_config_*.json` | Sidecar 配置文件 |
| `sidecars/opcda_push_service/run_*.bat` | 运行脚本 |
| `core/opcda_mapper.py` | Tag 映射器 |
| `monitor/ingest_server.py` | HTTP ingest 服务（含 OPC DA 端点） |

---

## 端点说明

| 端点 | 功能 | Payload 格式 |
|------|------|--------------|
| `/ingest/health` | 健康检查 | GET |
| `/ingest` | 通用点位接收 | `{device_id, variable_id, value, timestamp}` |
| `/ingest/opcda-tags` | OPC DA 专用 | `{schema: "opcda_tag_batch_v1", points: [...]}` |

---

## 验收标准

1. ✅ Sidecar 能连接 `ZPMC.OPCServer` 并成功订阅
2. ✅ Sidecar 推送数据到 `/ingest/opcda-tags`
3. ✅ Collector 正确映射 item_id 到 variable_id
4. ✅ 数据写入 TDengine 子表 `agv.agv801_xxx`
5. ✅ Ctrl+C 能正常终止 Sidecar
6. ✅ 日志可追踪完整链路

---

## 前端配置说明

当设备通讯类型选择 **OPC DA Sidecar** 时：

1. **io_type、var_offset、var_bit** 字段自动忽略（已设为可空）
2. **var_address** 字段填写完整的 OPC DA item_id
3. item_id 格式: `{channel}.{device}.{group}.{tag}`，例如 `OPD_Server.AGV801.ViewMain.Tag30041800`

```
┌─────────────────────────────────────────────────────┐
│  点位配置 (OPC DA Sidecar)                           │
├─────────────────────────────────────────────────────┤
│  完整地址 (var_address):                             │
│  [OPD_Server.AGV801.ViewMain.Tag30041800        ]  │
│                                                     │
│  提示: OPC DA 地址格式为 Channel.Device.Group.Tag   │
│                                                     │
│  数据类型: [REAL     ▼]                             │
│  是否存储: [✓]                                      │
└─────────────────────────────────────────────────────┘
```
