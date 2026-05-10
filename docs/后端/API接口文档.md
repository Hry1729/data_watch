# 工业设备采集平台 API 评审修订版（前后端二次确认稿）

> 文档状态：v1.1 联调冻结基线（已补 auth / system-manage）  
> 适用范围：`/device-monitor` + 登录鉴权 / 系统权限基础接口  
> 唯一契约事实源：本文  
> 派生文件：`frontend/src/api/device-monitor.ts`、`frontend/src/api/auth.ts`、`frontend/src/api/system-manage.ts`、`frontend/src/types/api/device-monitor.d.ts`、`frontend/src/types/api/api.d.ts`  
> 变更门禁：`docs/前后端联调/01-v1接口契约冻结与变更门禁.md`  
> 更新时间：2026-04-19

---

## 1. 文档目标

本版用于后端联调确认，统一 `device-monitor` 与真实数据登录 / 权限基础接口口径。  
本次将本文确认为 v1 联调期间的唯一契约事实源，前端类型、前端 API 适配层和后端实现均从本文派生。

对外声明：
- 本轮联调不改历史查询契约，仅记录优化方向。
- 除历史查询外，其余接口结构视为 v1 冻结基线；签收前仅允许说明性补充，不允许结构性变更。
- 登录、用户信息、菜单、角色、用户管理接口已纳入本文事实源，不再视为“框架外能力”。
- 对前端的 WebSocket 事件结构由后端统一归一化输出，collector 与 Fake Data 不直接定义前端消费格式。

---

## 2. 全局约定

### 2.1 接口前缀

```text
/api/device-monitor
/api/auth
/api/user
/api/role
/api/v3/system
```

### 2.2 统一响应结构

```ts
interface BaseResponse<T = unknown> {
  code: number
  msg: string
  data: T
}
```

说明：
- 除导出类文件流接口外，按 `code/msg/data` 返回。
- 导出类接口前端按 `Blob` 二进制处理。

### 2.3 时间与时区口径

- 时间格式统一：`YYYY-MM-DD HH:mm:ss`。
- 不接收 `timezone` 作为有效业务参数。
- 不返回任何时区回显字段（例如 `query.timezone`）。

### 2.4 分页兼容口径

- 前端优先发送 `pageNo/pageSize`。
- 兼容历史字段 `current/size`。
- 若分页值缺失或非法（非正数），前端默认回退到约定默认值。
- 响应推荐字段：`list/total/pageNo/pageSize`。
- 响应兼容字段：`current/size/pageNum` 可返回，但不作为前端强依赖。

### 2.5 错误码口径

| code | 含义 | 前端处理建议 |
|---|---|---|
| 400 | 参数错误 | 提示并回退默认参数重试 |
| 401 | 未登录或登录失效 | 跳转登录 |
| 403 | 无权限 | 展示无权限空态 |
| 500 | 服务异常 | 模块级空态 + 重试，不阻断整页 |

### 2.6 空数据返回口径

- 列表类：`list: []`，`total: 0`（分页字段保持结构完整）。
- 图表类：数组字段返回 `[]`，汇总字段返回 `0`。
- 不使用 `null` 代替数组或对象结构。

### 2.7 导出实现策略（推荐）

- 小数据量：同步返回 `Blob` 文件流。
- 大数据量：异步任务化（返回任务 ID，轮询状态后下载）。
- 本文接口契约不改变，仅约束实现策略。

---

## 3. 公共类型与枚举字典

### 3.1 通用结构

```ts
interface CompatiblePaginationParams {
  pageNo?: number
  pageSize?: number
  current?: number
  size?: number
}

interface PaginatedResult<T> {
  list: T[]
  total: number
  pageNo?: number
  pageSize?: number
  current?: number
  size?: number
  pageNum?: number
}
```

说明：
- `list/total/pageNo/pageSize` 为推荐分页字段。
- `current/size/pageNum` 为兼容字段，存在性不作强保证。

### 3.2 枚举字典

```ts
type DeviceStatus = 'enabled' | 'disabled'
type ConnectStatus = 'online' | 'offline' | 'unstable'
type DataStatus = 'normal' | 'abnormal' | 'nodata'

type DataType = 'BOOL' | 'INT' | 'DINT' | 'REAL' | 'STRING' | 'WORD' | 'DWORD'

type LogLevel = 'DEBUG' | 'INFO' | 'WARN' | 'ERROR' | 'CRITICAL'
type LogType = 'system' | 'auth' | 'operation' | 'collect' | 'storage' | 'frontend' | 'security' | 'alarm'
```

### 3.3 工作台参数枚举

```ts
type DashboardRecentEventType = 'alarm' | 'collect' | 'connection' | 'all'
type DashboardTrendRangeType = 'last24h' | 'today' | 'last7d'
type DashboardTrendGranularity = 'auto' | '5m' | '15m' | '1h' | '1d'
type DashboardStatRange = 'last1h' | 'last24h' | 'last7d'
type DashboardAlarmGroupBy = 'alarmType' | 'deviceType' | 'severity'
```

### 3.4 认证与权限公共结构

```ts
interface LoginParams {
  userName: string
  password: string
}

interface LoginResponse {
  token: string
  refreshToken: string
}

interface UserInfoResponse {
  userId: number
  userName: string
  email: string
  avatar?: string
  roles: string[]
  buttons: string[]
}

interface RoleGrantPayload {
  menuIds: number[]
  permissionIds: number[]
  dataScopes?: Array<{
    scopeType: string
    scopeValue: string
    enabled?: boolean
  }>
}
```

说明：

- `/api/user/info.buttons` 返回前端直接消费的 `auth_mark` 字符串列表。
- `/api/v3/system/menus[].meta.authList` 仅挂载 `action` 类型权限项。
- 当前上线顺序固定为：先打通 `login + user/info`，再打通 `/api/v3/system/menus`，最后切 `VITE_ACCESS_MODE=backend`。

---

## 4. 全量接口契约（字段清单）

### 4.1 驱动管理

#### GET `/api/device-monitor/driver/list`
- Method/Path：`GET /api/device-monitor/driver/list`
- 用途：用于驱动管理页只读展示与下拉
- Path 参数：无
- Query 参数：`pageNo?: number`、`pageSize?: number`、`keyword?: string`、`driverName?: string`（兼容字段）
- Body 参数：无
- `data` 字段：`PaginatedResult<DriverListItem>`；`list[].id`、`list[].driverName`、`list[].driverDesc`、`list[].readWriteType`、`list[].updateTime`；推荐字段 `list/total/pageNo/pageSize`，兼容可选 `current/size/pageNum`
- 默认值与约束：分页默认 `pageNo=1`、`pageSize=20`；`keyword` 优先于 `driverName`；分页参数需为正数
- 错误处理/空数据约定：遵循全局错误码；空列表返回 `list: []`、`total: 0`

#### GET `/api/device-monitor/driver/options`
- Method/Path：`GET /api/device-monitor/driver/options`
- 用途：用于驱动管理页只读展示与下拉
- Path 参数：无
- Query 参数：无
- Body 参数：无
- `data` 字段：`Array<{ driverName: string; driverDesc: string }>`
- 默认值与约束：无默认参数
- 错误处理/空数据约定：遵循全局错误码；空列表返回 `[]`

### 4.2 设备管理

#### GET `/api/device-monitor/device/group-tree`
- Method/Path：`GET /api/device-monitor/device/group-tree`
- 用途：用于设备管理页查询或操作
- Path 参数：无
- Query 参数：无
- Body 参数：无
- `data` 字段：`DeviceGroupTreeNode[]`；节点字段 `key`、`name`、`count?`、`nodeType?`、`deviceId?`、`deviceCode?`、`deviceName?`、`connectStatus?`、`children?`
- 默认值与约束：`connectStatus` 取值遵循 `ConnectStatus`
- 错误处理/空数据约定：遵循全局错误码；空树返回 `[]`

#### GET `/api/device-monitor/device/list`
- Method/Path：`GET /api/device-monitor/device/list`
- 用途：用于设备管理页查询或操作
- Path 参数：无
- Query 参数：`pageNo?: number`、`pageSize?: number`、`keyword?: string`、`deviceType?: string`、`deviceCode?: string`、`deviceName?: string`、`deviceStatus?: DeviceStatus`、`connectStatus?: ConnectStatus`、`template?: boolean`
- Body 参数：无
- `data` 字段：`PaginatedResult<DeviceListItem>`；`list[].id`、`list[].deviceType`、`list[].deviceCode`、`list[].deviceName`、`list[].driverName`、`list[].deviceStatus`、`list[].connectStatus`、`list[].connectUpdateTime`、`list[].dataStatus`、`list[].dataUpdateTime`、`list[].description?`；推荐字段 `list/total/pageNo/pageSize`，兼容可选 `current/size/pageNum`
- 默认值与约束：分页默认 `pageNo=1`、`pageSize=20`；`deviceStatus` 遵循 `DeviceStatus`；`connectStatus` 遵循 `ConnectStatus`
- 错误处理/空数据约定：遵循全局错误码；空列表返回 `list: []`、`total: 0`

#### GET `/api/device-monitor/device/{id}`
- Method/Path：`GET /api/device-monitor/device/{id}`
- 用途：用于设备管理页查询或操作
- Path 参数：`id: number`
- Query 参数：无
- Body 参数：无
- `data` 字段：`DeviceListItem`；字段同设备列表单项
- 默认值与约束：`id` 必须为正整数
- 错误处理/空数据约定：遵循全局错误码；参数非法或对象不存在按后端错误码返回

#### POST `/api/device-monitor/device`
- Method/Path：`POST /api/device-monitor/device`
- 用途：用于设备管理页新增设备
- Path 参数：无
- Query 参数：无
- Body 参数：`DeviceForm`：`deviceType`、`deviceCode`、`deviceName`、`driverName`、`deviceStatus`、`description?`、`id?`
- `data` 字段：`{ id: number }`
- 默认值与约束：`deviceStatus` 取值遵循 `DeviceStatus`；`id` 在新增场景可省略
- 错误处理/空数据约定：遵循全局错误码；成功返回新建主键

#### PUT `/api/device-monitor/device/{id}`
- Method/Path：`PUT /api/device-monitor/device/{id}`
- 用途：用于设备管理页查询或操作
- Path 参数：`id: number`
- Query 参数：无
- Body 参数：`DeviceForm`：`id?`、`deviceType`、`deviceCode`、`deviceName`、`driverName`、`deviceStatus`、`description?`
- `data` 字段：成功响应 `data` 由后端定义（前端按无强依赖处理）
- 默认值与约束：`id` 必填且应与路径一致；`deviceStatus` 遵循 `DeviceStatus`
- 错误处理/空数据约定：遵循全局错误码

#### DELETE `/api/device-monitor/device/{id}`
- Method/Path：`DELETE /api/device-monitor/device/{id}`
- 用途：用于设备管理页查询或操作
- Path 参数：`id: number`
- Query 参数：无
- Body 参数：无
- `data` 字段：成功响应 `data` 由后端定义（前端按无强依赖处理）
- 默认值与约束：`id` 必须为正整数
- 错误处理/空数据约定：遵循全局错误码

#### DELETE `/api/device-monitor/device/batch`
- Method/Path：`DELETE /api/device-monitor/device/batch`
- 用途：用于设备管理页查询或操作
- Path 参数：无
- Query 参数：无
- Body 参数：`{ ids: number[] }`
- `data` 字段：成功响应 `data` 由后端定义（前端按无强依赖处理）
- 默认值与约束：`ids` 不能为空数组，元素应为正整数
- 错误处理/空数据约定：遵循全局错误码

#### GET `/api/device-monitor/device/export`
- Method/Path：`GET /api/device-monitor/device/export`
- 用途：用于设备管理页查询或操作
- Path 参数：无
- Query 参数：`pageNo?: number`、`pageSize?: number`、`keyword?: string`、`deviceType?: string`、`deviceCode?: string`、`deviceName?: string`、`deviceStatus?: DeviceStatus`、`connectStatus?: ConnectStatus`、`template?: boolean`
- Body 参数：无
- `data` 字段：`Blob`（文件流）
- 默认值与约束：分页默认 `pageNo=1`、`pageSize=10000`；过滤条件与设备列表一致；`template=true` 时导出空模板；文件统一为 `.xlsx`，Sheet1 为说明、Sheet2 为业务数据
- 错误处理/空数据约定：遵循全局错误码；导出接口前端按二进制下载处理

#### POST `/api/device-monitor/device/import`
- Method/Path：`POST /api/device-monitor/device/import`
- 用途：用于设备管理页查询或操作
- Path 参数：无
- Query 参数：无
- Body 参数：`application/json`，字段：`rows: Array<Record<string, string | number | boolean>>`；前端读取 `.xlsx` 后，会将中文表头映射为 JSON 规范字段后提交，例如 `deviceType / deviceCode / deviceName / driverName / deviceStatus / description`
- `data` 字段：`{ success: number; fail: number; errors?: { rowNumber: number; reason: string }[] }`
- 默认值与约束：`rows` 必填；仅保证支持 `.xlsx`；前端当前固定提交规范字段，后端同时兼容中文表头 key 与规范字段 key；按行校验、按行写入，不覆盖已有设备
- 错误处理/空数据约定：遵循全局错误码；返回导入成功/失败计数，可附带前端可展示的失败原因列表

### 4.3 点位管理

#### GET `/api/device-monitor/point/device-tree`
- Method/Path：`GET /api/device-monitor/point/device-tree`
- 用途：用于点位管理页查询或操作
- Path 参数：无
- Query 参数：无
- Body 参数：无
- `data` 字段：`PointDeviceTreeNode[]`；节点字段 `key`、`name`、`nodeType?`、`count?`、`deviceId?`、`deviceCode?`、`children?`
- 默认值与约束：无默认参数
- 错误处理/空数据约定：遵循全局错误码；空树返回 `[]`

#### GET `/api/device-monitor/point/list`
- Method/Path：`GET /api/device-monitor/point/list`
- 用途：用于点位管理页查询或操作
- Path 参数：无
- Query 参数：`pageNo?: number`、`pageSize?: number`、`keyword?: string`、`deviceId?: number`、`deviceCode?: string`、`varSeq?: number`、`pointName?: string`、`ioAddress?: string`
- Body 参数：无
- `data` 字段：`PaginatedResult<PointListItem>`；`list[].id`、`list[].deviceId`、`list[].deviceCode`、`list[].varSeq`、`list[].pointName`、`list[].ioAddress`、`list[].dataType`、`list[].description`、`list[].isReadonly`、`list[].driverName`、`list[].createTime?`、`list[].updateTime?`；推荐字段 `list/total/pageNo/pageSize`，兼容可选 `current/size/pageNum`
- 默认值与约束：分页默认 `pageNo=1`、`pageSize=20`；`dataType` 取值遵循 `DataType`
- 错误处理/空数据约定：遵循全局错误码；空列表返回 `list: []`、`total: 0`

#### GET `/api/device-monitor/point/{id}`
- Method/Path：`GET /api/device-monitor/point/{id}`
- 用途：用于点位管理页查询或操作
- Path 参数：`id: number`
- Query 参数：`deviceId?: number`、`varSeq?: number`
- Body 参数：无
- `data` 字段：`PointListItem`；字段同点位列表单项
- 默认值与约束：兼容保留 `{id}` 技术定位键 `device_variable.id`；若同时提供 `deviceId + varSeq`，后端优先按“设备内业务键”定位点位
- 错误处理/空数据约定：遵循全局错误码；参数非法或对象不存在按后端错误码返回

#### POST `/api/device-monitor/point`
- Method/Path：`POST /api/device-monitor/point`
- 用途：用于点位管理页新增点位
- Path 参数：无
- Query 参数：无
- Body 参数：`PointForm`：`deviceId`、`deviceCode`、`varSeq`、`pointName`、`ioAddress`、`dataType`、`description`、`isReadonly`、`driverName`、`scanRate?`、`isStorage?`、`id?`
- `data` 字段：`{ id: number }`
- 默认值与约束：`dataType` 取值遵循 `DataType`；`id` 在新增场景可省略
- 错误处理/空数据约定：遵循全局错误码；成功返回新建主键

#### PUT `/api/device-monitor/point/{id}`
- Method/Path：`PUT /api/device-monitor/point/{id}`
- 用途：用于点位管理页查询或操作
- Path 参数：`id: number`
- Query 参数：`locatorDeviceId?: number`、`locatorVarSeq?: number`
- Body 参数：`PointForm`：`id?`、`deviceId`、`deviceCode`、`varSeq`、`pointName`、`ioAddress`、`dataType`、`description`、`isReadonly`、`driverName`、`scanRate?`、`isStorage?`
- `data` 字段：成功响应 `data` 由后端定义（前端按无强依赖处理）
- 默认值与约束：兼容保留 `{id}` 技术定位键；若同时提供 `locatorDeviceId + locatorVarSeq`，后端优先按原业务键定位，再按 Body 中新值写回
- 错误处理/空数据约定：遵循全局错误码

#### DELETE `/api/device-monitor/point/{id}`
- Method/Path：`DELETE /api/device-monitor/point/{id}`
- 用途：用于点位管理页查询或操作
- Path 参数：`id: number`
- Query 参数：`deviceId?: number`、`varSeq?: number`
- Body 参数：无
- `data` 字段：成功响应 `data` 由后端定义（前端按无强依赖处理）
- 默认值与约束：兼容保留 `{id}` 技术定位键；若同时提供 `deviceId + varSeq`，后端优先按“设备内业务键”定位删除目标
- 错误处理/空数据约定：遵循全局错误码

#### DELETE `/api/device-monitor/point/batch`
- Method/Path：`DELETE /api/device-monitor/point/batch`
- 用途：用于点位管理页查询或操作
- Path 参数：无
- Query 参数：无
- Body 参数：`{ ids: number[] }`
- `data` 字段：成功响应 `data` 由后端定义（前端按无强依赖处理）
- 默认值与约束：`ids` 不能为空数组，元素应为正整数
- 错误处理/空数据约定：遵循全局错误码

#### GET `/api/device-monitor/point/export`
- Method/Path：`GET /api/device-monitor/point/export`
- 用途：用于点位管理页查询或操作
- Path 参数：无
- Query 参数：`pageNo?: number`、`pageSize?: number`、`keyword?: string`、`deviceId?: number`、`deviceCode?: string`、`pointName?: string`、`ioAddress?: string`、`template?: boolean`
- Body 参数：无
- `data` 字段：`Blob`（文件流）
- 默认值与约束：分页默认 `pageNo=1`、`pageSize=10000`；过滤条件与点位列表一致；`template=true` 时导出空模板；文件统一为 `.xlsx`，Sheet1 为说明、Sheet2 为业务数据
- 错误处理/空数据约定：遵循全局错误码；导出接口前端按二进制下载处理

#### POST `/api/device-monitor/point/import`
- Method/Path：`POST /api/device-monitor/point/import`
- 用途：用于点位管理页查询或操作
- Path 参数：无
- Query 参数：无
- Body 参数：`application/json`，字段：`rows: Array<Record<string, string | number | boolean>>`；前端读取 `.xlsx` 后，会将中文表头映射为 JSON 规范字段后提交，例如 `deviceCode / pointName / ioAddress / dataType / driverName / isReadonly / description / scanRate / isStorage`
- `data` 字段：`{ success: number; fail: number; errors?: { rowNumber: number; reason: string }[] }`
- 默认值与约束：`rows` 必填；仅保证支持 `.xlsx`；前端当前固定提交规范字段，后端同时兼容中文表头 key 与规范字段 key；按行校验、按行写入，不覆盖已有点位
- 错误处理/空数据约定：遵循全局错误码；返回导入成功/失败计数，可附带前端可展示的失败原因列表

#### POST `/api/device-monitor/point/import/template`
- Method/Path：`POST /api/device-monitor/point/import/template`
- 用途：用于点位管理页“从模板导入”，按当前选中设备的设备类型，从 MySQL 模板点位追加未存在的点位
- Path 参数：无
- Query 参数：无
- Body 参数：`application/json`，字段：`deviceId: number`
- `data` 字段：`{ success: number; fail: number; errors?: { rowNumber: number; reason: string }[] }`
- 默认值与约束：`deviceId` 必填且必须是已存在设备；后端优先使用设备已绑定且启用的模板，否则回退到该设备类型下最新启用模板；只追加当前设备中尚不存在的 `var_seq / var_address`，不覆盖已有点位
- 错误处理/空数据约定：遵循全局错误码；设备类型未配置启用模板、模板下无点位或模板点位字段不合法时返回明确错误

### 4.4 实时监控

#### GET `/api/device-monitor/monitor/device-tree`
- Method/Path：`GET /api/device-monitor/monitor/device-tree`
- 用途：用于实时监控页初始化、订阅与推送
- Path 参数：无
- Query 参数：无
- Body 参数：无
- `data` 字段：`MonitorDeviceTreeNode[]`；节点字段 `key`、`name`、`nodeType?`、`status?`、`deviceId?`、`deviceCode?`、`deviceName?`、`children?`
- 默认值与约束：`status` 取值遵循 `ConnectStatus`
- 错误处理/空数据约定：遵循全局错误码；空树返回 `[]`

#### GET `/api/device-monitor/monitor/realtime-init`
- Method/Path：`GET /api/device-monitor/monitor/realtime-init`
- 用途：用于实时监控页初始化、订阅与推送
- Path 参数：无
- Query 参数：`deviceId?: number`
- Body 参数：无
- `data` 字段：`DeviceRealtimeStatus`；`deviceId`、`deviceCode`、`deviceName`、`driverName`、`connectStatus`、`lastCollectTime`、`data[]`；`data[]` 字段为 `varSeq`、`pointName`、`ioAddress`、`value`、`updateTime`
- 默认值与约束：`deviceId` 为可选参数（前端已按可选处理）；`connectStatus` 遵循 `ConnectStatus`
- 错误处理/空数据约定：遵循全局错误码；无实时点位时返回 `data: []`

#### WS `/api/device-monitor/ws/realtime`
- Method/Path：`WS /api/device-monitor/ws/realtime`
- 用途：用于实时监控页初始化、订阅与推送
- Path 参数：无
- Query 参数：无
- Body 参数：客户端消息支持 `subscribe`（`{ action: 'subscribe', channel: 'realtime', deviceIds: number[] }`）、`unsubscribe`（`{ action: 'unsubscribe', channel: 'realtime', deviceIds: number[] }`）、`ping`（`{ action: 'ping' }`）
- `data` 字段：服务端推送 `RealtimePushPayload`，建议按 `type` 分型解析（非强约束）：
  - `type='snapshot'`：建议字段 `type`、`timestamp`、`devices: DeviceRealtimeStatus[]`
  - `type='update'`：建议字段 `type`、`timestamp`、`deviceId`、`data: RealtimeData[]`
  - `type='alarm'`：建议字段 `type`、`timestamp`、`deviceId`、`event`、`data`
  - `type='error'`：建议字段 `type`、`timestamp`，错误上下文可放在 `event` 或 `data`
  - `type='pong'`：建议字段 `type`、`timestamp`
- 默认值与约束：`channel` 固定为 `realtime`；`deviceIds` 元素应为正整数
- 错误处理/空数据约定：连接异常按前端重连策略处理；重复订阅应保持幂等；无推送时保持连接存活

### 4.5 历史数据

#### GET `/api/device-monitor/history/devices`
- Method/Path：`GET /api/device-monitor/history/devices`
- 用途：用于历史数据页查询与导出
- Path 参数：无
- Query 参数：无
- Body 参数：无
- `data` 字段：`HistoryDeviceItem[]`；`id`、`name`、`deviceCode?`、`deviceName?`
- 默认值与约束：无默认参数
- 错误处理/空数据约定：遵循全局错误码；空列表返回 `[]`

#### GET `/api/device-monitor/history/points`
- Method/Path：`GET /api/device-monitor/history/points`
- 用途：用于历史数据页查询与导出
- Path 参数：无
- Query 参数：`deviceId: number`
- Body 参数：无
- `data` 字段：`HistoryPointItem[]`；`varSeq`、`pointName`、`ioAddress`
- 默认值与约束：`deviceId` 必填且应为正整数
- 错误处理/空数据约定：遵循全局错误码；空列表返回 `[]`

#### GET `/api/device-monitor/history/query`
- Method/Path：`GET /api/device-monitor/history/query`
- 用途：用于历史数据页查询与导出（旧版兼容接口）
- Path 参数：无
- Query 参数：`deviceId: number`、`varSeqs: number[]`、`startTime: string`、`endTime: string`
- Body 参数：无
- `data` 字段：`HistoryDataResponse`；`times: string[]`、`points[]`；`points[].varSeq`、`points[].pointName`、`points[].values: (string | number)[]`
- 默认值与约束：`deviceId/varSeqs/startTime/endTime` 均为必填；时间格式遵循全局约定
- 错误处理/空数据约定：遵循全局错误码；空结果返回 `times: []`、`points: []`

#### GET `/api/device-monitor/history/chart`
- Method/Path：`GET /api/device-monitor/history/chart`
- 用途：获取降采样后的趋势数据，用于图表展示
- Path 参数：无
- Query 参数：`deviceId: number`、`varSeqs: number[]`、`startTime: string`、`endTime: string`、`interval?: string`（auto/1s/5s/1m）、`aggregate?: string`（avg/max/min/last）
- Body 参数：无
- `data` 字段：`HistoryDataResponse`；结构同 `/query`
- 默认值与约束：`interval` 默认 `auto`；`aggregate` 默认 `avg`

#### GET `/api/device-monitor/history/raw`
- Method/Path：`GET /api/device-monitor/history/raw`
- 用途：获取原始点位数据，支持分页加载
- Path 参数：无
- Query 参数：`deviceId: number`、`varSeq: number`、`startTime: string`、`endTime: string`、`pageSize?: number`、`cursorTs?: number`（纳秒或毫秒时间戳，用于游标分页）
- Body 参数：无
- `data` 字段：`list[]`（点位数据列表）、`nextCursorTs?`（下一页游标）
- 默认值与约束：`pageSize` 默认 `100`

#### POST `/api/device-monitor/history/export`
- Method/Path：`POST /api/device-monitor/history/export`
- 用途：创建历史数据异步导出任务
- Path 参数：无
- Query 参数：无
- Body 参数：`HistoryExportParams`：`deviceId`、`varSeqs`、`startTime`、`endTime`
- `data` 字段：`{ taskId: string }`
- 默认值与约束：请求体字段要求与历史查询一致
- 错误处理/空数据约定：成功后返回任务 ID，前端需轮询状态

#### GET `/api/device-monitor/history/export/{task_id}/status`
- Method/Path：`GET /api/device-monitor/history/export/{task_id}/status`
- 用途：查询导出任务执行状态
- Path 参数：`task_id: string`
- Query 参数：无
- Body 参数：无
- `data` 字段：`{ status: 'pending' | 'processing' | 'completed' | 'failed', progress?: number, error?: string }`

#### GET `/api/device-monitor/history/export/{task_id}/download`
- Method/Path：`GET /api/device-monitor/history/export/{task_id}/download`
- 用途：下载导出的文件
- Path 参数：`task_id: string`
- Query 参数：无
- Body 参数：无
- `data` 字段：`Blob`（文件流）
- 默认值与约束：仅在任务状态为 `completed` 时可用

### 4.6 系统日志

#### GET `/api/device-monitor/log/list`
- Method/Path：`GET /api/device-monitor/log/list`
- 用途：用于系统日志页筛选与展示
- Path 参数：无
- Query 参数：`pageNo?: number`、`pageSize?: number`、`keyword?: string`、`startTime?: string`、`endTime?: string`、`level?: LogLevel`、`type?: LogType`
- Body 参数：无
- `data` 字段：`PaginatedResult<LogListItem>`；`list[].id`、`list[].time`、`list[].level`、`list[].type`、`list[].content`；推荐字段 `list/total/pageNo/pageSize`，兼容可选 `current/size/pageNum`
- 默认值与约束：分页默认 `pageNo=1`、`pageSize=20`；`level` 遵循 `LogLevel`；`type` 遵循 `LogType`
- 错误处理/空数据约定：遵循全局错误码；空列表返回 `list: []`、`total: 0`
- 数据库事实源：`operation_log + system_log` 已在本机 `device_watch` 落地；2026-05-06 已补 `log_retention_policy`、查询索引和日志管理权限
- 后续扩展接口：日志统计、策略读取/更新、手动清理、DEBUG 开关、客户端日志上报应复用 `log_retention_policy` 与 `operation_log/system_log`，不再新增平行日志事实表

### 4.7 工作台（Dashboard）

#### GET `/api/device-monitor/dashboard/overview`
- Method/Path：`GET /api/device-monitor/dashboard/overview`
- 用途：用于工作台模块渲染或交互
- Path 参数：无
- Query 参数：无
- Body 参数：无
- `data` 字段：`deviceTotal`、`onlineDeviceTotal`、`pointTotal`、`activeAlarmTotal`
- 默认值与约束：无默认参数
- 错误处理/空数据约定：遵循全局错误码；空数据场景各统计字段返回 `0`

#### GET `/api/device-monitor/dashboard/device-type-distribution`
- Method/Path：`GET /api/device-monitor/dashboard/device-type-distribution`
- 用途：用于工作台模块渲染或交互
- Path 参数：无
- Query 参数：`topN?: number`
- Body 参数：无
- `data` 字段：`total`、`groups[]`；`groups[].typeCode`、`groups[].typeName`、`groups[].deviceCount`、`groups[].onlineCount`、`groups[].ratio`
- 默认值与约束：`topN` 默认 `10`
- 错误处理/空数据约定：遵循全局错误码；空数据返回 `total: 0`、`groups: []`

#### GET `/api/device-monitor/dashboard/recent-events`
- Method/Path：`GET /api/device-monitor/dashboard/recent-events`
- 用途：用于工作台模块渲染或交互
- Path 参数：无
- Query 参数：`limit?: number`、`eventType?: DashboardRecentEventType`
- Body 参数：无
- `data` 字段：`list[]`；`list[].eventId`、`list[].eventTime`、`list[].deviceId`、`list[].deviceCode`、`list[].deviceName`、`list[].eventType`、`list[].eventLevel`、`list[].content`、`list[].status`
- 默认值与约束：`limit` 默认 `10`；`eventType` 默认 `all`
- 错误处理/空数据约定：遵循全局错误码；联调阶段当真实告警事实表缺失、查询失败或当前无可用告警样本时，后端可返回确定性的 synthetic alarm 数据兜底；真实结果可用时优先返回真实查询

#### GET `/api/device-monitor/dashboard/trend`
- Method/Path：`GET /api/device-monitor/dashboard/trend`
- 用途：用于工作台模块渲染或交互
- Path 参数：无
- Query 参数：`rangeType?: DashboardTrendRangeType`、`granularity?: DashboardTrendGranularity`、`metricKeys?: string[]`
- Body 参数：无
- `data` 字段：`query.rangeType`、`query.granularity`、`timeAxis: string[]`、`series[]`、`summary`；`series[].metricKey`、`series[].metricName`、`series[].unit`、`series[].data: number[]`；`summary.collectCountTotal`、`summary.alarmCountTotal`、`summary.onlineRateAvg`
- 默认值与约束：`rangeType` 默认 `last24h`；`granularity` 默认 `auto`；`metricKeys` 默认 `['collectCount','alarmCount','onlineRate']`
- 错误处理/空数据约定：遵循全局错误码；空数据返回 `timeAxis: []`、`series: []`，`summary` 各字段为 `0`

#### GET `/api/device-monitor/dashboard/device-connection-health`
- Method/Path：`GET /api/device-monitor/dashboard/device-connection-health`
- 用途：用于工作台模块渲染或交互
- Path 参数：无
- Query 参数：`statRange?: DashboardStatRange`、`topN?: number`、`includeOffline?: boolean`
- Body 参数：无
- `data` 字段：`query.statRange`、`query.topN`、`query.includeOffline`、`overview.onlineDevices`、`overview.offlineDevices`、`overview.riskDevices`、`list[]`；`list[].deviceId`、`list[].deviceCode`、`list[].deviceName`、`list[].deviceType`、`list[].connectStatus`、`list[].exceptionCount`、`list[].timeoutCount`、`list[].reconnectCount`、`list[].successRate`、`list[].lastHeartbeatTime`
- 默认值与约束：`statRange` 默认 `last24h`；`topN` 默认 `10`；`includeOffline` 默认 `true`；`connectStatus` 遵循 `ConnectStatus`
- 错误处理/空数据约定：遵循全局错误码；空数据返回 `overview` 各统计为 `0`、`list: []`

#### GET `/api/device-monitor/dashboard/alarm-distribution`
- Method/Path：`GET /api/device-monitor/dashboard/alarm-distribution`
- 用途：用于工作台模块渲染或交互
- Path 参数：无
- Query 参数：`statRange?: DashboardStatRange`、`groupBy?: DashboardAlarmGroupBy`、`topN?: number`、`includeOther?: boolean`
- Body 参数：无
- `data` 字段：`query.statRange`、`query.groupBy`、`query.topN`、`totalAlarmCount`、`groups[]`、`comparePrev`；`groups[].groupKey`、`groups[].groupName`、`groups[].count`、`groups[].ratio`；`comparePrev.countDiff`、`comparePrev.ratioDiff`
- 默认值与约束：`statRange` 默认 `last24h`；`groupBy` 默认 `alarmType`；`topN` 默认 `6`；`includeOther` 默认 `true`
- 错误处理/空数据约定：遵循全局错误码；联调阶段当真实告警事实表缺失、查询失败或当前无可用告警样本时，后端可返回确定性的 synthetic alarm 聚合数据兜底；真实结果可用时优先返回真实查询

### 4.8 认证与当前用户

#### POST `/api/auth/login`
- Method/Path：`POST /api/auth/login`
- 用途：真实数据登录入口；供前端写入 token 并继续拉取用户信息
- Path 参数：无
- Query 参数：无
- Body 参数：`{ userName: string; password: string }`
- `data` 字段：`token`、`refreshToken`
- 默认值与约束：
  - `userName`、`password` 必填
  - 当前后端返回的 `token` 可直接写入前端 `Authorization`
  - 后端解析时兼容 `Bearer xxx` 和纯 token 字符串
- 联调说明：
  - 当 `AUTH_AUTO_BOOTSTRAP=true` 时，默认可使用 `Super / Admin / User` 三个内置账号联调
  - 三个内置账号初始密码统一为 `123456`

#### POST `/api/auth/logout`
- Method/Path：`POST /api/auth/logout`
- 用途：退出登录确认接口
- Path 参数：无
- Query 参数：无
- Body 参数：无
- `data` 字段：`true`
- 默认值与约束：v1 仅做前端退出确认，不维护独立会话表

#### GET `/api/user/info`
- Method/Path：`GET /api/user/info`
- 用途：返回当前登录用户基础信息、角色和按钮权限
- Path 参数：无
- Query 参数：无
- Body 参数：无
- Header：`Authorization: <token>`
- `data` 字段：`userId`、`userName`、`email`、`avatar`、`roles: string[]`、`buttons: string[]`
- 默认值与约束：
  - `roles` 用于 frontend 模式路由过滤
  - `buttons` 返回 `auth_mark` 列表，用于 `v-auth / hasAuth`
  - 用户被停用时接口返回 `403`

### 4.9 用户管理

#### 用户接口清单

| 接口 | 用途 | 权限要求 | 关键输出 |
|---|---|---|---|
| `GET /api/user/list` | 用户分页列表 | `R_SUPER` / `R_ADMIN` | `records/current/size/total` |
| `GET /api/user/{id}` | 用户详情 | `R_SUPER` / `R_ADMIN` | `Api.SystemManage.UserListItem` |
| `POST /api/user` | 新增用户 | `R_SUPER` / `R_ADMIN` | `{ id }` |
| `PUT /api/user/{id}` | 编辑用户 | `R_SUPER` / `R_ADMIN` | `{ id }` |
| `PATCH /api/user/{id}/status` | 切换用户状态 | `R_SUPER` / `R_ADMIN` | `{ id }` |
| `PATCH /api/user/{id}/password/reset` | 重置密码 | `R_SUPER` / `R_ADMIN` | `{ id }` |
| `DELETE /api/user/{id}` | 删除用户 | `R_SUPER` / `R_ADMIN` | `{ id }` |

#### `GET /api/user/list`
- Query 参数：兼容前端现有 `current/size`，并支持 `userName`、`userPhone`、`userEmail`、`userGender`、`status`
- `data` 字段：`records[]`、`current`、`size`、`total`
- `records[]` 对齐 `Api.SystemManage.UserListItem`：`id`、`avatar`、`status`、`userName`、`userGender`、`nickName`、`userPhone`、`userEmail`、`userRoles`、`createBy`、`createTime`、`updateBy`、`updateTime`

#### 用户写接口请求体

```ts
interface UserPayload {
  userName: string
  nickName?: string
  userPhone?: string
  userEmail?: string
  userGender?: string
  avatar?: string
  status?: string
  userRoles?: string[]
  password?: string
}
```

说明：

- `POST /api/user` 默认密码可不传，不传时后端写入 `123456`
- 删除接口采用逻辑删除，保留审计字段与历史授权痕迹

### 4.10 角色管理

#### 角色接口清单

| 接口 | 用途 | 权限要求 | 关键输出 |
|---|---|---|---|
| `GET /api/role/list` | 角色分页列表 | `R_SUPER` | `records/current/size/total` |
| `GET /api/role/options` | 角色下拉选项 | `R_SUPER` / `R_ADMIN` | `roleId/roleName/roleCode/enabled[]` |
| `GET /api/role/{id}` | 角色详情 | `R_SUPER` | `Api.SystemManage.RoleListItem` |
| `POST /api/role` | 新增角色 | `R_SUPER` | `{ id }` |
| `PUT /api/role/{id}` | 编辑角色 | `R_SUPER` | `{ id }` |
| `PATCH /api/role/{id}/status` | 切换角色状态 | `R_SUPER` | `{ id }` |
| `DELETE /api/role/{id}` | 删除角色 | `R_SUPER` | `{ id }` |
| `GET /api/role/{id}/grant` | 获取角色授权 | `R_SUPER` | `menuIds/permissionIds/dataScopes` |
| `PUT /api/role/{id}/grant` | 更新角色授权 | `R_SUPER` | `{ id }` |

#### 角色写接口请求体

```ts
interface RolePayload {
  roleName: string
  roleCode: string
  description?: string
  enabled?: boolean
}
```

#### 角色授权请求体

```ts
interface RoleGrantPayload {
  menuIds: number[]
  permissionIds: number[]
  dataScopes?: Array<{
    scopeType: string
    scopeValue: string
    enabled?: boolean
  }>
}
```

说明：

- 删除角色前需校验是否仍被用户占用；若存在关联用户，返回 `400`
- 当前 `dataScopes` 仅保留结构和存储，不在设备 / 点位 / 历史查询中强制生效

### 4.11 菜单与权限管理

#### 菜单接口清单

| 接口 | 用途 | 权限要求 | 关键输出 |
|---|---|---|---|
| `GET /api/v3/system/menus` | 当前用户动态菜单树 | 当前登录用户 | `AppRouteRecord[]` |
| `GET /api/v3/system/menus/admin` | 管理端查看完整菜单树 | `R_SUPER` | `AppRouteRecord[]` |
| `POST /api/v3/system/menu` | 新增菜单 | `R_SUPER` | `{ id }` |
| `PUT /api/v3/system/menu/{id}` | 编辑菜单 | `R_SUPER` | `{ id }` |
| `DELETE /api/v3/system/menu/{id}` | 删除菜单 | `R_SUPER` | `{ id }` |
| `GET /api/v3/system/menu/{id}/permissions` | 菜单下权限列表 | `R_SUPER` | `permission[]` |
| `POST /api/v3/system/menu/{id}/permissions` | 新增菜单权限 | `R_SUPER` | `{ id }` |
| `PUT /api/v3/system/menu/{id}/permissions/{permissionId}` | 编辑菜单权限 | `R_SUPER` | `{ id }` |
| `DELETE /api/v3/system/menu/{id}/permissions/{permissionId}` | 删除菜单权限 | `R_SUPER` | `{ id }` |

#### `GET /api/v3/system/menus`
- 返回结构严格对齐前端 `AppRouteRecord[]`
- 过滤规则：
  1. 先按 `sys_role_menu` 收口当前角色可见菜单
  2. 再按 `sys_role_permission` 聚合按钮权限，输出到 `meta.authList`
- `meta.authList[]` 结构：`{ id, title, authMark }`
- 无权限场景返回空数组或 `403`，不允许因为缺菜单导致 `500`

#### 菜单写接口请求体

```ts
interface MenuPayload {
  name: string
  parentId?: number | null
  path: string
  component: string
  titleKey: string
  icon?: string
  menuType?: 'directory' | 'menu' | 'iframe' | 'link'
  link?: string
  isIframe?: boolean
  keepAlive?: boolean
  isHide?: boolean
  isHideTab?: boolean
  fixedTab?: boolean
  activePath?: string
  isFullPage?: boolean
  sort?: number
  enabled?: boolean
}
```

#### 菜单权限写接口请求体

```ts
interface PermissionPayload {
  permissionCode: string
  permissionName: string
  permissionType?: 'page' | 'action' | 'api' | 'data'
  authMark?: string
  description?: string
  sort?: number
  enabled?: boolean
}
```

---

## 5. 历史查询待优化记录（不阻断联调）

- 本轮联调不改 `GET /api/device-monitor/history/query` 契约，继续使用当前结构：`times + points[].values`。
- 该接口本轮目标是先保障图表联调可用，表格分页能力记录为后续优化项。

待优化方案（仅记录，不在本轮执行）：
1. 双接口方案：`history/query-series`（图表）+ `history/query-table`（表格分页）。
2. 单接口模式切换：保留 `history/query`，增加 `viewMode=series|table`。
3. 保持现状：前端将 `times + points` 转换为表格数据。

本轮结论：
- 继续按现接口联调。
- 优化延后到下一迭代，联调阶段不阻断。

---

## 6. 废弃接口（仅保留声明，不作为可用能力）

- `GET /api/device-monitor/dashboard/driver-health`：已废弃，不再作为联调目标接口。  
- 替代接口：`GET /api/device-monitor/dashboard/device-connection-health`。

---

## 7. 对外变更说明（相对上一版）

- 文档范围从单一 `/api/device-monitor` 扩展到“设备监控 + 登录鉴权 + 系统权限基础接口”。
- 文档补齐真实数据登录与权限链路接口：
  - `POST /api/auth/login`
  - `POST /api/auth/logout`
  - `GET /api/user/info`
  - `GET /api/user/list`
  - `GET /api/role/list`
  - `GET /api/v3/system/menus`
- 设备监控域继续保留原 34 个接口 / 通道统计口径，不因为 auth/system-manage 扩展而改变业务接口冻结范围。
- 类型口径继续对齐 `Api.DeviceMonitor.*`、`Api.Auth.*`、`Api.SystemManage.*`，不新增文档独有字段。
- 全文统一“无时区参数”口径。
- 分页响应推荐字段为 `list/total/pageNo/pageSize`，`current/size/pageNum` 仅作兼容可选。
- WebSocket 推送补充 `type` 推荐事件类型（`snapshot/update/alarm/error/pong`），保持与宽松载荷兼容。
- 历史查询接口本轮不改契约，新增“待优化记录”并延后到下一迭代。

---

## 8. 后端确认清单（联调前）

### 8.1 覆盖与可用性

- [ ] 设备监控 34 个接口/通道均已在后端提供对应能力（驱动 2、设备 9、点位 9、实时 3、历史 4、日志 1、工作台 6）。
- [ ] `POST /api/auth/login`、`GET /api/user/info` 已可支撑 frontend 模式登录。
- [ ] `GET /api/v3/system/menus` 已可生成 backend 模式动态菜单树。
- [ ] 用户 / 角色 / 菜单 / 权限管理接口已具备最小闭环能力。
- [ ] 废弃接口已确认不再投产联调口径。

### 8.2 参数与字段

- [ ] 分页兼容字段（`pageNo/pageSize` 与 `current/size`）处理一致。
- [ ] 分页响应推荐包含 `list/total/pageNo/pageSize`，兼容字段不作为强依赖。
- [ ] 批量删除接口请求体 `ids: number[]` 语义明确并稳定。
- [ ] 工作台 6 个接口字段名、默认值、枚举值与本文一致。
- [ ] `/api/user/info.roles`、`buttons` 字段与前端权限模式约定一致。
- [ ] `/api/v3/system/menus[].meta.authList[].authMark` 可被前端直接消费。
- [ ] `user/list`、`role/list` 持续兼容 `records/current/size/total` 分页结构。
- [ ] 无 `timezone` 入参与回显字段。
- [ ] WebSocket 推送字段与 `RealtimePushPayload` 对齐（宽松结构 + 可选 `type` 事件类型）。
- [ ] 历史查询接口维持现契约（`times + points[].values`），本轮不做破坏性调整。

### 8.3 空态与错误处理

- [ ] 列表/图表空数据返回结构完整，不使用 `null` 占位。
- [ ] 400/401/403/500 错误语义与处理预期一致。
- [ ] 用户停用、角色无权限、空菜单树场景均返回可预期的 403 / 空数组，而不是 500。
- [ ] 单接口失败不阻断工作台整页渲染（模块级降级可重试）。
- [ ] 导出能力支持“小数据同步、超阈值异步任务化”的实现策略。
