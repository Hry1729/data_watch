# Perspective 集成设计文档

> **版本**: v1.0
> **创建日期**: 2026-04-18
> **适用范围**: 历史数据页面可视化增强
> **集成模式**: 客户端/服务器复制模式

---

## 一、概述

### 1.1 背景

工业设备数据采集与监控平台需要处理大量时序数据的可视化展示。现有历史数据页面使用 ECharts 实现图表渲染，存在以下问题：

1. **大数据量性能瓶颈**：历史数据查询结果超过一定规模时，前端渲染性能下降
2. **交互分析能力有限**：用户无法灵活地对数据进行透视、筛选、聚合等交互操作
3. **并发压力**：多用户同时访问时，每个用户独立请求和处理数据

### 1.2 解决方案

引入 **Perspective** 库，采用**客户端/服务器复制模式**，实现：

- 服务端统一管理数据表格，客户端通过 WebSocket 同步数据副本
- 前端 `<perspective-viewer>` 组件提供高性能渲染和交互式分析能力
- 支持虚拟滚动、增量更新等性能优化特性

### 1.3 改动范围

| 页面 | 是否改动 | 说明 |
|------|----------|------|
| 历史数据页 | ✅ 是 | 移除 ECharts，集成 Perspective |
| 实时监控页 | ❌ 否 | 保持现有架构不变 |
| 工作台页 | ❌ 否 | 保持现有架构不变 |
| 其他页面 | ❌ 否 | 不涉及 |

---

## 二、架构设计

### 2.1 客户端/服务器复制模式架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           前端（浏览器）                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐│
│  │  Perspective Worker                                                  ││
│  │  ├── 本地 Table 副本（自动同步）                                     ││
│  │  ├── View 视图计算                                                   ││
│  │  └── <perspective-viewer> 组件渲染                                   ││
│  │      ├── datagrid（数据表格）                                        ││
│  │      ├── y_line（折线图）                                            ││
│  │      ├── y_bar（柱状图）                                             ││
│  │      └── 其他图表类型                                                ││
│  └─────────────────────────────────────────────────────────────────────┘│
│                              ↑ WebSocket                                 │
│                              ↓ 数据同步                                  │
└─────────────────────────────────────────────────────────────────────────┘
                               ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                           后端（Python 服务）                             │
│  ┌─────────────────────────────────────────────────────────────────────┐│
│  │  Perspective Server (perspective-python)                             ││
│  │  ├── Table（主数据表）                                               ││
│  │  │   └── 历史数据表（history_table）                                 ││
│  │  ├── View（视图定义，可选）                                          ││
│  │  └── WebSocket Handler（客户端数据同步）                             ││
│  └─────────────────────────────────────────────────────────────────────┘│
│                              ↑ 数据写入                                  │
└─────────────────────────────────────────────────────────────────────────┘
                               ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                           数据源                                         │
│  ┌─────────────────────────────────────────────────────────────────────┐│
│  │  TDengine（历史数据查询）                                            ││
│  │  └── 查询结果写入 Perspective Table                                  ││
│  └─────────────────────────────────────────────────────────────────────┘│
│                                                                          │
│  注：采集模块数据直接写入 TDengine，不参与 Perspective 数据流            │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 数据流设计

#### 历史数据查询流程

```
用户操作                    前端                     后端                      Perspective
   │                         │                        │                          │
   │  选择设备/点位/时间      │                        │                          │
   │  点击"查询"             │                        │                          │
   ├────────────────────────►│                        │                          │
   │                         │  HTTP: 查询历史数据     │                          │
   │                         ├───────────────────────►│                          │
   │                         │                        │  查询 TDengine           │
   │                         │                        ├─────────────────────────►│
   │                         │                        │  返回时序数据             │
   │                         │                        │◄─────────────────────────┤
   │                         │                        │                          │
   │                         │                        │  写入 Perspective Table   │
   │                         │                        ├─────────────────────────►│
   │                         │                        │                          │
   │                         │  WebSocket: 数据同步    │                          │
   │                         │◄───────────────────────┤◄─────────────────────────┤
   │                         │                        │                          │
   │  <perspective-viewer>   │                        │                          │
   │  自动渲染图表            │                        │                          │
   │◄────────────────────────┤                        │                          │
```

#### 并发场景处理

```
用户A ──┐
用户B ──┼──► Perspective Server ──► 共享 Table ──► 广播更新
用户C ──┘         │
                  └── 单次写入，多客户端同步
```

---

## 三、数据结构设计

### 3.1 Perspective Table Schema

历史数据表结构：

| 字段名 | 类型 | 说明 |
|--------|------|------|
| `timestamp` | datetime | 时间戳 |
| `deviceId` | integer | 设备ID |
| `deviceCode` | string | 设备编码 |
| `varSeq` | integer | 点位业务键（`var_seq`） |
| `pointName` | string | 点位名称 |
| `ioAddress` | string | IO地址 |
| `value` | float | 点位值 |

### 3.2 WebSocket 消息格式

#### 服务器 → 客户端：增量更新

```json
{
  "action": "update",
  "timestamp": "2026-04-18 10:00:05.123",
  "records": [
    {
      "timestamp": "2026-04-18 10:00:00.000",
      "deviceId": 991001,
      "deviceCode": "qc100",
      "varSeq": 30,
      "pointName": "主起升速度",
      "ioAddress": "DB1.0",
      "value": 120.5
    }
  ]
}
```

#### 服务器 → 客户端：全量快照

```json
{
  "action": "snapshot",
  "timestamp": "2026-04-18 10:00:00.000",
  "records": [
    // 所有数据记录
  ]
}
```

#### 客户端 → 服务器：订阅请求

```json
{
  "action": "subscribe",
  "table": "history_table"
}
```

---

## 四、模块改动清单

### 4.1 后端模块

| 序号 | 文件路径 | 改动类型 | 改动内容 |
|------|----------|----------|----------|
| 1 | `backend/requirements.txt` | ➕ 新增依赖 | 添加 `perspective-python` |
| 2 | `backend/app/core/config.py` | ✏️ 修改 | 新增 Perspective 配置项 |
| 3 | `backend/app/services/perspective.py` | 🆕 新增 | Perspective Server 管理器 |
| 4 | `backend/app/api/routes/perspective.py` | 🆕 新增 | WebSocket 端点实现 |
| 5 | `backend/app/api/router.py` | ✏️ 修改 | 注册 WebSocket 路由 |
| 6 | `backend/app/services/monitor.py` | ✏️ 修改 | 历史查询后写入 Perspective |

### 4.2 前端模块

| 序号 | 文件路径 | 改动类型 | 改动内容 |
|------|----------|----------|----------|
| 1 | `frontend/package.json` | ➕ 新增依赖 | 添加 Perspective 相关包 |
| 2 | `frontend/src/utils/perspective.ts` | 🆕 新增 | Perspective 客户端封装 |
| 3 | `frontend/src/views/device_monitor/history/index.vue` | 🔄 重构 | 移除 ECharts，集成 Perspective |
| 4 | `frontend/src/types/api/device-monitor.d.ts` | ✏️ 修改 | 新增 Perspective 类型定义 |
| 5 | `frontend/vite.config.ts` | ✏️ 修改 | 配置 WASM 资源路径 |

---

## 五、接口设计

### 5.1 WebSocket 端点

**端点**: `WS /api/device-monitor/ws/perspective`

**功能**: Perspective 客户端/服务器数据同步

**连接流程**:
1. 客户端建立 WebSocket 连接
2. 客户端发送 `subscribe` 消息订阅表格
3. 服务器返回当前数据快照
4. 服务器持续推送增量更新

### 5.2 HTTP 接口（保持不变）

历史数据查询接口保持现有设计：

- `GET /api/device-monitor/history/devices` - 获取设备列表
- `GET /api/device-monitor/history/points` - 获取点位列表
- `GET /api/device-monitor/history/query` - 查询历史数据

查询结果同时：
1. 返回给前端（兼容模式）
2. 写入 Perspective Table（新模式）

---

## 六、前端组件设计

### 6.1 perspective-viewer 配置

历史数据页面核心配置：

```html
<perspective-viewer
  editable="false"
  selectable
  columns='["pointName", "ioAddress", "value", "timestamp"]'
  row-pivots='[]'
  column-pivots='[]'
  sort='[["timestamp", "asc"]]'
  aggregates='{"value": "avg"}'
  plugin="y_line"
  class="h-full"
></perspective-viewer>
```

### 6.2 图表类型支持

| 图表类型 | plugin 值 | 适用场景 |
|----------|-----------|----------|
| 折线图 | `y_line` | 时序数据趋势分析 |
| 柱状图 | `y_bar` | 离散值对比 |
| 散点图 | `xy_scatter` | 相关性分析 |
| 数据表 | `datagrid` | 原始数据查看 |

### 6.3 交互功能

| 功能 | 说明 |
|------|------|
| 图表类型切换 | 用户可动态切换折线图/柱状图/数据表 |
| 列拖拽 | 支持拖拽调整列顺序 |
| 透视分析 | 支持行透视、列透视 |
| 筛选 | 支持按值筛选数据 |
| 排序 | 支持按任意列排序 |
| 导出 | 内置 CSV/JSON 导出功能 |

---

## 七、并发性能设计

### 7.1 服务端优化

| 策略 | 说明 |
|------|------|
| **共享 Table** | 多用户查询相同数据时共享同一个 Table 实例 |
| **按需推送** | 只推送用户订阅的数据变更 |
| **连接池管理** | 限制最大连接数，防止资源耗尽 |

### 7.2 客户端优化

| 策略 | 说明 |
|------|------|
| **虚拟滚动** | Perspective 内置支持，大数据量自动启用 |
| **增量更新** | 只传输变化数据，减少网络开销 |
| **Web Worker** | 数据处理在独立线程进行，不阻塞 UI |

### 7.3 并发容量估算

| 场景 | 用户数 | 预期性能 |
|------|--------|----------|
| 日常使用 | 30-50 人 | 良好 |
| 高峰期 | 80-100 人 | 可接受 |
| 极端情况 | >100 人 | 需要横向扩展 |

---

## 八、依赖版本

### 8.1 后端依赖

```
# backend/requirements.txt
perspective-python>=3.0.0
```

### 8.2 前端依赖

```json
{
  "@finos/perspective": "^3.0.0",
  "@finos/perspective-viewer": "^3.0.0",
  "@finos/perspective-viewer-d3fc": "^3.0.0",
  "@finos/perspective-viewer-datagrid": "^3.0.0"
}
```

---

## 九、配置项

### 9.1 后端配置

```python
# backend/app/core/config.py

@dataclass
class Settings:
    # ... 现有配置 ...
    
    # Perspective 配置
    perspective_enabled: bool = True
    perspective_ws_path: str = "/ws/perspective"
    perspective_max_clients: int = 200
```

### 9.2 前端配置

```bash
# frontend/.env.development

VITE_PERSPECTIVE_WS_URL=/api/device-monitor/ws/perspective
VITE_PERSPECTIVE_ENABLED=true
```

---

## 十、实施计划

| 阶段 | 内容 | 预计工时 |
|------|------|----------|
| 1. 后端基础 | 安装依赖 + 配置 + Perspective Server | 0.5 天 |
| 2. WebSocket 集成 | 端点实现 + 客户端同步 | 1 天 |
| 3. 前端基础 | 安装依赖 + 工具类封装 | 0.5 天 |
| 4. 历史页重构 | 移除 ECharts + 集成 Perspective | 1.5 天 |
| 5. 联调测试 | 端到端验证 | 0.5 天 |
| **总计** | - | **4 天** |

---

## 十一、风险与缓解

| 风险 | 等级 | 缓解措施 |
|------|------|----------|
| Perspective Python WebSocket 与 TurboAPI 集成方式不明确 | 中 | 提前 POC 验证 |
| 大数据量历史查询性能 | 中 | 时间范围限制 + 虚拟滚动 |
| Web Component 样式冲突 | 低 | CSS 隔离 + 自定义主题 |
| 多用户并发连接管理 | 中 | 连接池 + 共享 Table |

---

## 十二、变更记录

| 日期 | 变更内容 | 变更人 |
|------|----------|--------|
| 2026-04-18 | 创建文档，定义客户端/服务器复制模式架构 | @coordinator |
