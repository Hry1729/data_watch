# API 参考手册

本篇文档为后端的 API 接口参考手册，整理了后端所有路由模块共计 80 个 HTTP 接口。每个接口都列出了对应的 Method、Path、权限码要求（提取自 `require_permissions`）、Query/Body 参数模型、以及标准的返回结构。

---

## 统一响应结构
所有 HTTP 接口的响应均遵循以下标准 JSON 格式：
```json
{
  "code": 200,
  "msg": "success",
  "data": null // 接口具体返回的有效数据
}
```
*注：下文各接口的返回结构中，如无特殊声明，均只展示 `data` 字段内的具体结构。对于文件导出与下载类接口（如设备导出、点位导出、历史数据下载等），它们将直接返回二进制文件流响应，不遵循上述 JSON 响应格式。*

---

## 一、 Auth 认证模块 (3 个接口)
*源码参考：[auth.py](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/auth.py)*

### 1. 用户登录
- **Method**: `POST`
- **Path**: `/api/auth/login`
- **权限要求**: 无 (公开接口)
- **请求参数 (Query)**:
  - `userName` (str, 必填): 用户名
  - `password` (str, 必填): 密码
- **返回结构 (`data` 字段)**:
  ```json
  {
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", // JWT Access Token
    "refreshToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." // JWT Refresh Token
  }
  ```

### 2. 用户登出
- **Method**: `POST`
- **Path**: `/api/auth/logout`
- **权限要求**: 需在 Header 携带有效 JWT 凭证 (`Authorization: Bearer <Token>`)
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

### 3. 获取当前登录用户信息
- **Method**: `GET`
- **Path**: `/api/user/info`
- **权限要求**: 需携带有效 JWT 凭证
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  {
    "userId": 1,
    "userName": "admin",
    "email": "admin@example.com",
    "avatar": "http://example.com/avatar.jpg",
    "roles": ["admin"],
    "buttons": ["sys:user:list", "sys:user:add", "device_monitor:device:view"] // 前端按钮级权限标记列表
  }
  ```

---

## 二、 驱动管理模块 (2 个接口)
*源码参考：[catalog.py](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/catalog.py)*

### 1. 查询驱动分页列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/driver/list`
- **权限要求**: `device_monitor:driver:view`
- **请求参数 (Query)**:
  - `pageNo` / `current` (str, 可选): 页码，默认为 1
  - `pageSize` / `size` (str, 可选): 每页条数，默认为 10
  - `keyword` / `driverName` (str, 可选): 模糊查询关键字
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "id": 1,
        "driverName": "Modbus TCP 驱动",
        "driverCode": "modbus_tcp",
        "description": "用于连接标准 Modbus TCP 协议设备的驱动"
      }
    ],
    "total": 1
  }
  ```

### 2. 查询驱动下拉选项
- **Method**: `GET`
- **Path**: `/api/device-monitor/driver/options`
- **权限要求**: `device_monitor:driver:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "value": "modbus_tcp",
      "label": "Modbus TCP 驱动"
    }
  ]
  ```

---

## 三、 设备管理模块 (11 个接口)
*源码参考：[catalog.py](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/catalog.py)*

### 1. 获取设备分组树
- **Method**: `GET`
- **Path**: `/api/device-monitor/device/group-tree`
- **权限要求**: `device_monitor:device:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "id": "g1",
      "name": "一号车间",
      "children": [
        {
          "id": "g1_1",
          "name": "A区域",
          "children": []
        }
      ]
    }
  ]
  ```

### 2. 获取设备分页列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/device/list`
- **权限要求**: `device_monitor:device:view`
- **请求参数 (Query)**:
  - `pageNo` / `current` (str, 可选): 页码
  - `pageSize` / `size` (str, 可选): 每页条数
  - `keyword` (str, 可选): 模糊搜索关键字
  - `deviceType` (str, 可选): 设备类型
  - `deviceCode` (str, 可选): 设备编码
  - `deviceName` (str, 可选): 设备名称
  - `deviceStatus` (str, 可选): 设备运行状态
  - `connectStatus` (str, 可选): 通信连接状态
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "id": 1,
        "deviceCode": "dev_001",
        "deviceName": "1#空压机",
        "deviceType": "compressor",
        "deviceStatus": "1",
        "connectStatus": "1"
      }
    ],
    "total": 1
  }
  ```

### 3. 导出设备数据 / 模版下载
- **Method**: `GET`
- **Path**: `/api/device-monitor/device/export`
- **权限要求**: `device_monitor:device:export`
- **请求参数 (Query)**:
  - 同列表查询过滤参数
  - `template` (str, 可选): 传入 `"1"` / `"true"` 时导出导入模版文件，否则导出过滤后的设备数据
- **返回结构**: Excel 二进制流文件。
  - 模版下载文件名: `device-import-template.xlsx`
  - 数据导出文件名: `device-export.xlsx`

### 4. 导入设备数据
- **Method**: `POST`
- **Path**: `/api/device-monitor/device/import`
- **权限要求**: `device_monitor:device:import`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "rows": [
      {
        "deviceCode": "dev_002",
        "deviceName": "2#空压机",
        "deviceType": "compressor",
        "driverCode": "modbus_tcp"
      }
    ]
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "successCount": 1,
    "failCount": 0,
    "errors": []
  }
  ```

### 5. 获取单个设备详情
- **Method**: `GET`
- **Path**: `/api/device-monitor/device/{device_id}`
- **权限要求**: `device_monitor:device:view`
- **请求参数 (Path)**:
  - `device_id` (int, 必填): 设备主键 ID
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 1,
    "deviceCode": "dev_001",
    "deviceName": "1#空压机",
    "deviceType": "compressor",
    "driverCode": "modbus_tcp",
    "groupId": 1,
    "scanRateMs": 1000,
    "enabled": true
  }
  ```

### 6. 新增设备
- **Method**: `POST`
- **Path**: `/api/device-monitor/device`
- **权限要求**: `device_monitor:device:create`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "deviceCode": "dev_003",
    "deviceName": "3#空压机",
    "deviceType": "compressor",
    "driverCode": "modbus_tcp",
    "groupId": 1,
    "scanRateMs": 1000,
    "enabled": true
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 3,
    "deviceCode": "dev_003"
  }
  ```

### 7. 修改设备
- **Method**: `PUT`
- **Path**: `/api/device-monitor/device/{device_id}`
- **权限要求**: `device_monitor:device:update`
- **请求参数 (Path & Body - JSON)**:
  - Path: `device_id` (int)
  - Body: 包含待修改的设备字段 dict
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

### 8. 批量删除设备
- **Method**: `DELETE`
- **Path**: `/api/device-monitor/device/batch`
- **权限要求**: `device_monitor:device:delete`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "ids": [1, 2]
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

### 9. 删除单个设备
- **Method**: `DELETE`
- **Path**: `/api/device-monitor/device/{device_id}`
- **权限要求**: `device_monitor:device:delete`
- **请求参数 (Path)**:
  - `device_id` (int)
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

### 10. 获取设备关联的点位分组列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/device/{device_code}/point-groups`
- **权限要求**: `device_monitor:device:view`
- **请求参数 (Path)**:
  - `device_code` (str): 设备编码
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "groupCode": "g001",
      "groupName": "基础测量"
    }
  ]
  ```

### 11. 获取设备特定分组下的变量/参数列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/device/{device_code}/variables`
- **权限要求**: `device_monitor:device:view`
- **请求参数 (Path & Query)**:
  - Path: `device_code` (str)
  - Query: `group_code` (str, 可选): 默认为 `"g001"`
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "varSeq": 1,
      "varCode": "temperature",
      "varName": "温度",
      "dataType": "float"
    }
  ]
  ```

---

## 四、 点位管理模块 (10 个接口)
*源码参考：[catalog.py](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/catalog.py)*

### 1. 获取点位树
- **Method**: `GET`
- **Path**: `/api/device-monitor/point/device-tree`
- **权限要求**: `device_monitor:point:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "id": 1,
      "label": "1#空压机",
      "type": "device",
      "children": [
        {
          "id": 101,
          "label": "温度点位",
          "type": "point"
        }
      ]
    }
  ]
  ```

### 2. 获取点位分页列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/point/list`
- **权限要求**: `device_monitor:point:view`
- **请求参数 (Query)**:
  - `pageNo` / `current`, `pageSize` / `size` (str, 可选): 分页参数
  - `keyword`, `deviceId`, `deviceCode`, `groupId`, `varSeq`, `pointName`, `ioAddress` (str, 可选): 过滤参数
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "id": 101,
        "deviceId": 1,
        "deviceCode": "dev_001",
        "varSeq": 1,
        "pointName": "采集温度",
        "ioAddress": "40001",
        "dataType": "float"
      }
    ],
    "total": 1
  }
  ```

### 3. 导出点位数据 / 模版下载
- **Method**: `GET`
- **Path**: `/api/device-monitor/point/export`
- **权限要求**: `device_monitor:point:export`
- **请求参数 (Query)**:
  - 同点位列表过滤参数
  - `template` (str, 可选): 传入 `"1"` / `"true"` 时导出导入模版文件，否则导出过滤后的点位数据
- **返回结构**: Excel 二进制流文件。
  - 模版下载文件名: `point-import-template.xlsx`
  - 数据导出文件名: `point-export.xlsx`

### 4. 导入点位数据
- **Method**: `POST`
- **Path**: `/api/device-monitor/point/import`
- **权限要求**: `device_monitor:point:import`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "rows": [
      {
        "deviceCode": "dev_001",
        "varSeq": 1,
        "pointName": "采集温度",
        "ioAddress": "40001",
        "dataType": "float"
      }
    ]
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "successCount": 1,
    "failCount": 0,
    "errors": []
  }
  ```

### 5. 从模板导入设备点位
- **Method**: `POST`
- **Path**: `/api/device-monitor/point/import/template`
- **权限要求**: `device_monitor:point:import`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "deviceId": 1
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

### 6. 获取单个点位详情
- **Method**: `GET`
- **Path**: `/api/device-monitor/point/{point_id}`
- **权限要求**: `device_monitor:point:view`
- **请求参数 (Path & Query)**:
  - Path: `point_id` (int, 必填)
  - Query: `deviceId` (str, 可选), `varSeq` (str, 可选) - 双主键备用定位
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 101,
    "deviceId": 1,
    "varSeq": 1,
    "pointName": "采集温度",
    "ioAddress": "40001",
    "dataType": "float"
  }
  ```

### 7. 新增点位
- **Method**: `POST`
- **Path**: `/api/device-monitor/point`
- **权限要求**: `device_monitor:point:create`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "deviceId": 1,
    "varSeq": 2,
    "pointName": "采集压力",
    "ioAddress": "40002",
    "dataType": "float"
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 102
  }
  ```

### 8. 修改点位
- **Method**: `PUT`
- **Path**: `/api/device-monitor/point/{point_id}`
- **权限要求**: `device_monitor:point:update`
- **请求参数 (Path & Query & Body - JSON)**:
  - Path: `point_id` (int)
  - Query: `locatorDeviceId` (str, 可选), `locatorVarSeq` (str, 可选)
  - Body: 包含需修改的点位字段 dict
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

### 9. 批量删除点位
- **Method**: `DELETE`
- **Path**: `/api/device-monitor/point/batch`
- **权限要求**: `device_monitor:point:delete`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "ids": [101, 102]
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

### 10. 删除点位
- **Method**: `DELETE`
- **Path**: `/api/device-monitor/point/{point_id}`
- **权限要求**: `device_monitor:point:delete`
- **请求参数 (Path & Query)**:
  - Path: `point_id` (int)
  - Query: `deviceId` (str, 可选), `varSeq` (str, 可选) - 双重主键辅助定位
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

---

## 五、 实时监控模块 (4 个接口)
*源码参考：[monitor.py](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/monitor.py)*

### 1. 监控设备树
- **Method**: `GET`
- **Path**: `/api/device-monitor/monitor/device-tree`
- **权限要求**: `device_monitor:monitor:view` 或 `device_monitor:realtime:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "id": 1,
      "deviceCode": "dev_001",
      "deviceName": "1#空压机",
      "connectStatus": "1"
    }
  ]
  ```

### 2. 实时监控初始化数据
- **Method**: `GET`
- **Path**: `/api/device-monitor/monitor/realtime-init`
- **权限要求**: `device_monitor:monitor:view` 或 `device_monitor:realtime:view`
- **请求参数 (Query)**:
  - `deviceId` (int, 可选): 设备 ID
  - `groupCode` (str, 可选): 默认为 `"g001"`
- **返回结构 (`data` 字段)**:
  ```json
  {
    "deviceCode": "dev_001",
    "groupCode": "g001",
    "variables": [
      {
        "varSeq": 1,
        "varCode": "temperature",
        "value": 45.2,
        "status": "good",
        "ts": 1680000000.0
      }
    ]
  }
  ```

### 3. 获取实时窗口数据 (按设备 ID)
- **Method**: `GET`
- **Path**: `/api/device-monitor/monitor/realtime-window`
- **权限要求**: `device_monitor:monitor:view` 或 `device_monitor:realtime:view`
- **请求参数 (Query)**:
  - `deviceId` (int, 必填): 设备 ID
  - `groupCode` (str, 可选): 分组编码，默认 `"g001"`
  - `scanRateMs` (int, 可选): 扫描频率
  - `limit` (int, 可选): 返回条数，默认 10
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "ts": 1680000000.0,
      "values": {
        "temperature": 45.2,
        "pressure": 0.6
      }
    }
  ]
  ```

### 4. 获取实时窗口数据 (按设备编码 Code)
- **Method**: `GET`
- **Path**: `/api/device-monitor/realtime-window`
- **权限要求**: `device_monitor:monitor:view` 或 `device_monitor:realtime:view`
- **请求参数 (Query)**:
  - `device_code` (str, 必填): 设备编码
  - `group_code` (str, 可选): 分组编码，默认 `"g001"`
  - `scan_rate_ms` (int, 可选): 扫描频率
  - `limit` (int, 可选): 返回条数，默认 10
- **返回结构 (`data` 字段)**:
  与通过设备 ID 获取的实时窗口数据格式相同。

---

## 六、 历史数据模块 (10 个接口)
*源码参考：[history.py 路由](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/history.py) | [history.py 服务](file:///D:/python/device_alarm/data_watch/backend/app/services/history.py)*

### 1. 获取用于选择的历史设备列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/devices`
- **权限要求**: `device_monitor:history:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "id": 1,
      "deviceCode": "dev_001",
      "deviceName": "1#空压机"
    }
  ]
  ```

### 2. 获取设备类型列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/device-types`
- **权限要求**: `device_monitor:history:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "code": "compressor",
      "name": "空压机"
    }
  ]
  ```

### 3. 获取指定设备下的点位列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/points`
- **权限要求**: `device_monitor:history:view`
- **请求参数 (Query)**:
  - `deviceId` (str/int, 必填): 设备 ID
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "varSeq": 1,
      "pointName": "采集温度"
    }
  ]
  ```

### 4. 同步查询历史数据 (对接 Perspective 视图缓存)
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/query`
- **权限要求**: `device_monitor:history:view`
- **请求参数 (Query & Headers)**:
  - Query Parameters:
    - `deviceId` (str/int, 必填): 设备 ID
    - `startTime` (str, 必填): 开始时间戳
    - `endTime` (str, 必填): 结束时间戳
    - `varSeqs` 或 `varSeqs[]` 或 `pointIds` 或 `pointIds[]` (必填, 列表或逗号分隔): 查询的点位 varSeq
  - Header (可选):
    - `X-Perspective-Session-Id` (str): 可视会话 ID
    - `X-Perspective-Query-Version` (int): 可视查询版本号
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "ts": "2026-05-23T12:00:00Z",
      "val": 45.2,
      "varSeq": 1
    }
  ]
  ```

### 5. 获取降采样后图表趋势数据
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/chart`
- **权限要求**: `device_monitor:history:view`
- **请求参数 (Query)**:
  - `deviceId`, `startTime`, `endTime`, `varSeqs` (必填，同上)
  - `interval` (str, 可选): 采样跨度 (如 "auto", "1m", "5m")
  - `aggregate` (str, 可选): 采样聚合方法 (如 "avg", "max", "min")
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "ts": "2026-05-23T12:00:00Z",
      "values": {
        "1": 45.2 // key为varSeq, value为采样后的值
      }
    }
  ]
  ```

### 6. 获取游标分页的原始数据
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/raw`
- **权限要求**: `device_monitor:history:view`
- **请求参数 (Query)**:
  - `deviceId`, `varSeq`, `startTime`, `endTime` (必填)
  - `pageSize` (int, 可选): 每页行数，默认 1000，上限 5000
  - `cursorTs` (str, 可选): 游标时间戳，供下一页加载
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "ts": "2026-05-23T12:00:00.123Z",
        "val": 45.21
      }
    ],
    "nextCursorTs": "2026-05-23T12:00:05.123Z" // 若为空说明无下一页数据
  }
  ```

### 7. 获取原始对齐矩阵历史数据
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/raw-matrix`
- **权限要求**: `device_monitor:history:view`
- **请求参数 (Query)**:
  - `deviceId`, `startTime`, `endTime`, `varSeqs` (必填，点位最多 10 个)
  - `alignGranularity` (str, 可选): 对齐精度，支持 `"100ms"`, `"500ms"`, `"1s"`，默认 `"1s"`
  - `pageSize` (int, 可选): 分页条数，默认 200，上限 500
  - `cursorTs` (str, 可选): 游标时间戳
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "ts": "2026-05-23T12:00:00.000Z",
        "values": {
          "1": 45.2, // key为varSeq, value为对齐后的值
          "2": 0.6
        }
      }
    ],
    "nextCursorTs": "2026-05-23T12:00:01.000Z"
  }
  ```

### 8. 创建异步导出历史数据任务
- **Method**: `POST`
- **Path**: `/api/device-monitor/history/export`
- **权限要求**: `device_monitor:history:export`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "deviceId": 1,
    "varSeqs": [1, 2],
    "startTime": "2026-05-23T00:00:00Z",
    "endTime": "2026-05-23T12:00:00Z"
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "taskId": "export_task_a7d8c9e..." // 任务 ID
  }
  ```

### 9. 查询导出任务状态
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/export/{task_id}/status`
- **权限要求**: `device_monitor:history:export`
- **请求参数 (Path)**:
  - `task_id` (str): 任务 ID
- **返回结构 (`data` 字段)**:
  ```json
  {
    "status": "completed", // "pending" | "running" | "completed" | "failed"
    "progress": 100, // 导出百分比
    "error": null, // 错误信息
    "createdAt": "2026-05-23 14:00:00"
  }
  ```

### 10. 下载导出结果 CSV 文件
- **Method**: `GET`
- **Path**: `/api/device-monitor/history/export/{task_id}/download`
- **权限要求**: `device_monitor:history:export`
- **请求参数 (Path)**:
  - `task_id` (str): 任务 ID
- **返回结构**: CSV 格式二进制文件流。
  - 文件下载名格式: `历史数据_<设备名称>_<时间戳>.csv`

---

## 七、 仪表盘模块 (6 个接口)

### 1. 仪表盘汇总总览统计
- **Method**: `GET`
- **Path**: `/api/device-monitor/dashboard/overview`
- **权限要求**: `device_monitor:dashboard:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  {
    "deviceTotal": 50, // 设备总数
    "onlineDeviceTotal": 45, // 在线设备数
    "offlineDeviceTotal": 2, // 离线设备数
    "riskDeviceTotal": 3, // 风险/不稳定设备数
    "pointTotal": 1200, // 采集点位总数
    "systemAlertTotal": 10, // 24小时内系统与操作告警总数 (WARN/ERROR/CRITICAL)
    "systemAlertComparePrev": {
      "countDiff": 2, // 告警差值
      "ratioDiff": 20.0 // 告警变动百分比
    },
    "generatedAt": "2026-05-23 15:00:00" // 生成时间戳
  }
  ```

### 2. 设备类型分布统计
- **Method**: `GET`
- **Path**: `/api/device-monitor/dashboard/device-type-distribution`
- **权限要求**: `device_monitor:dashboard:view`
- **请求参数 (Query)**:
  - `topN` (str, 可选): 限制返回类型数量，默认为 10
- **返回结构 (`data` 字段)**:
  ```json
  {
    "total": 50, // 参与统计设备总数
    "groups": [
      {
        "typeCode": "compressor", // 类型简称
        "typeName": "空压机", // 类型显示名称
        "deviceCount": 20, // 该类型设备总数
        "onlineCount": 18, // 正在运行/在线设备数
        "ratio": 40.0 // 占比 (%)
      }
    ]
  }
  ```

### 3. 最近事件列表
- **Method**: `GET`
- **Path**: `/api/device-monitor/dashboard/recent-events`
- **权限要求**: `device_monitor:dashboard:view`
- **请求参数 (Query)**:
  - `limit` (str, 可选): 返回条数限制，默认为 10
  - `eventType` (str, 可选): 事件类型过滤 ("alarm", "all" 等)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "eventId": "1", // 事件 ID/报警序列
        "eventTime": "2026-05-23 14:30:00", // 发生时间
        "deviceId": 1, // 关联设备 ID
        "deviceCode": "dev_001", // 设备编码
        "deviceName": "1#空压机", // 设备名称
        "eventType": "alarm", // 固定为 "alarm"
        "eventLevel": "警告", // 级别名称 (如 警告, 严重)
        "content": "排气温度超高告警（联调假数据）", // 日志/告警正文
        "status": "1" // 告警状态 ("1"=活跃)
      }
    ]
  }
  ```

### 4. 仪表盘指标趋势统计
- **Method**: `GET`
- **Path**: `/api/device-monitor/dashboard/trend`
- **权限要求**: `device_monitor:dashboard:view`
- **请求参数 (Query)**:
  - `rangeType` (str, 可选): 时间范围 (如 `"today"`, `"week"`)
  - `granularity` (str, 可选): 统计粒度 (如 `"hour"`, `"day"`)
  - `metricKeys` (list[str] | str, 可选): 趋势指标键列表 (支持 `collectCount`, `alarmCount`, `onlineRate`)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "query": {
      "rangeType": "today",
      "granularity": "hour"
    },
    "timeAxis": ["12:00", "13:00", "14:00"], // 时间轴标签数组
    "series": [
      {
        "metricKey": "collectCount",
        "metricName": "采集次数",
        "unit": "次",
        "data": [1000, 1200, 1100] // 与时间轴对应的指标值序列
      }
    ],
    "summary": {
      "collectCountTotal": 3300, // 累积采集总数
      "alarmCountTotal": 5, // 累积告警数
      "onlineRateAvg": 98.2 // 平均在线率
    }
  }
  ```

### 5. 设备连接健康度指标
- **Method**: `GET`
- **Path**: `/api/device-monitor/dashboard/device-connection-health`
- **权限要求**: `device_monitor:dashboard:view`
- **请求参数 (Query)**:
  - `statRange` (str, 可选): 统计时间窗口
  - `topN` (str, 可选): 返回条数，默认 10
  - `includeOffline` (str, 可选): 是否包括离线设备 (默认 "true")
- **返回结构 (`data` 字段)**:
  ```json
  {
    "query": {
      "statRange": "today",
      "topN": 10,
      "includeOffline": true
    },
    "overview": {
      "onlineDevices": 45,
      "offlineDevices": 2,
      "riskDevices": 3
    },
    "list": [
      {
        "deviceId": 1,
        "deviceCode": "dev_001",
        "deviceName": "1#空压机",
        "deviceType": "compressor",
        "connectStatus": "online", // "online", "unstable", "offline"
        "exceptionCount": 0, // 异常帧数
        "timeoutCount": 0, // 超时帧数
        "reconnectCount": 0, // 重连帧数
        "successRate": 100.0, // 连接成功率 (%)
        "lastHeartbeatTime": "2026-05-23 15:00:00" // 最后一次心跳上报时间
      }
    ]
  }
  ```

### 6. 告警分布数据分析
- **Method**: `GET`
- **Path**: `/api/device-monitor/dashboard/alarm-distribution`
- **权限要求**: `device_monitor:dashboard:view`
- **请求参数 (Query)**:
  - `statRange` (str, 可选): 时间窗口
  - `groupBy` (str, 可选): 聚合分组依据 ("type", "level", "device")
  - `topN` (str, 可选): 显示前 N 组，默认 6
  - `includeOther` (str, 可选): 是否聚合显示 "其他" (默认 "true")
- **返回结构 (`data` 字段)**:
  ```json
  {
    "query": {
      "statRange": "today",
      "groupBy": "type",
      "topN": 6
    },
    "totalAlarmCount": 50, // 周期内告警总计数
    "groups": [
      {
        "groupKey": "temp_high", // 聚合标识键
        "groupName": "温控异常", // 聚合显示名
        "count": 15 // 该项发生的告警条数
      }
    ],
    "comparePrev": {
      "countDiff": 5, // 同比差值
      "ratioDiff": 11.11 // 变动百分比
    }
  }
  ```

---

## 八、 日志管理模块 (7 个接口)
*源码参考：[log.py 路由](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/log.py) | [log.py 服务](file:///D:/python/device_alarm/data_watch/backend/app/services/log.py)*

### 1. 日志列表查询
- **Method**: `GET`
- **Path**: `/api/device-monitor/log/list`
- **权限要求**: `device_monitor:log:view`
- **请求参数 (Query)**:
  - `pageNo` / `current`, `pageSize` / `size` (str, 可选): 分页参数
  - `keyword`, `startTime`, `endTime`, `level`, `type`, `kind`, `source`, `serviceName`, `deviceCode`, `operatorName`, `requestId`, `traceId`, `resultStatus` (str, 可选): 日志多维度过滤参数
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "id": 10001, // 日志物理 ID
        "kind": "operation", // 日志大类 ("operation" | "system")
        "time": "2026-05-23 14:00:00", // 发生时间
        "level": "INFO", // 日志级别 ("INFO", "WARN", "ERROR" 等)
        "type": "operation", // 业务分类/日志类型
        "source": "SysUser", // 触发模块或源文件
        "serviceName": "", // 服务名称 (若为 system 类型的服务日志则有值)
        "moduleName": "SysUser", // 操作的模块名
        "actionName": "create_user", // 具体的操作名
        "operatorName": "admin", // 操作人或标识 (若为 system 类型的系统日志则为 "system")
        "deviceCode": "", // 关联设备 Code (如有)
        "content": "登录成功: admin", // 日志信息正文
        "resultStatus": "success", // 执行结果状态 ("success" | "fail")
        "durationMs": 12, // 执行耗时 (ms)
        "requestId": "req_uuid_xxx", // 请求标识
        "traceId": "req_uuid_xxx", // 全局调用链 Trace ID
        "detail": "{}", // 发生变化的对象差异详情等 (JSON 字符串)
        "exceptionStack": null // 运行异常时的错误堆栈 (若为 system 类型的系统日志且有异常时则有值)
      }
    ],
    "total": 1
  }
  ```

### 2. 日志统计分析
- **Method**: `GET`
- **Path**: `/api/device-monitor/log/stats`
- **权限要求**: `device_monitor:log:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  {
    "todayTotal": 1020, // 今日产生日志总条数
    "yesterdayTotal": 1200, // 昨日产生日志总条数
    "todayWarnTotal": 20, // 今日警告级总数
    "todayErrorTotal": 2, // 今日错误级总数
    "todayCriticalTotal": 0, // 今日致命级总数
    "debugEnabled": false, // 当前是否临时开启了后台 DEBUG 日志落库
    "debugRemainingSeconds": 0, // 临时 DEBUG 落库剩余倒计时秒数
    "debugEnabledUntil": "2026-05-23 16:00:00", // 临时 DEBUG 落库到期截止时间
    "totalSizeMb": 124.5, // 估算的数据库日志物理占用空间大小 (MB)
    "maxTotalMb": 1024, // 清理策略中约定的物理容量配额限制 (MB)
    "lastCleanupTime": "2026-05-23 03:00:00", // 上一次成功执行清理的时间
    "lastCleanupDeletedTotal": 1500, // 上一次清理所删除的历史日志条数总和
    "levelTrend": {
      "labels": ["00:00", "02:00"],
      "series": [
        {
          "name": "INFO",
          "data": [100, 150]
        }
      ]
    },
    "typeRatio": [
      {
        "name": "auth",
        "value": 50.0 // 类型所占比例 (%)
      }
    ],
    "sourceRatio": [
      {
        "name": "backend.login",
        "value": 30.0 // 来源占比 (%)
      }
    ]
  }
  ```

### 3. 获取系统日志落库及清理策略
- **Method**: `GET`
- **Path**: `/api/device-monitor/log/policy`
- **权限要求**: `device_monitor:log:view`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  {
    "enabled": true, // 是否启用自动清理
    "retentionDays": 30, // 日志保留天数
    "maxTotalMb": 1024, // 日志最大占用空间数(MB)
    "cleanupTime": "02:00:00", // 清理任务执行时刻
    "capacityCheckIntervalMinutes": 60, // 容量检查频率
    "batchSize": 1000,
    "debugDbEnabled": false, // 是否临时启用 DEBUG 落库
    "debugEnabledUntil": null, // DEBUG 临时启用截止时间
    "debugReason": null,
    "debugEnabledBy": null,
    "lastCleanupTime": "2026-05-23 02:00:00",
    "updateBy": "admin",
    "updateTime": "2026-05-20 10:00:00"
  }
  ```

### 4. 更新日志清理策略配置
- **Method**: `PUT`
- **Path**: `/api/device-monitor/log/policy`
- **权限要求**: `device_monitor:log:policy` (需管理员)
- **请求参数 (Body - JSON)**:
  包含更新后的策略字段 dict，如 `enabled`, `retentionDays`, `maxTotalMb` 等。
- **返回结构 (`data` 字段)**:
  更新后的完整策略数据（同上）。

### 5. 临时切换 DEBUG 日志落库开关
- **Method**: `POST`
- **Path**: `/api/device-monitor/log/debug`
- **权限要求**: `device_monitor:log:debug` (需管理员)
- **请求参数 (Body - JSON)**:
  ```json
  {
    "enabled": true, // 启用或关闭
    "durationMinutes": 60, // 自动开启时长 (分钟)
    "reason": "排查数据丢失故障"
  }
  ```
- **返回结构 (`data` 字段)**:
  更新后的完整策略数据。

### 6. 手动触发强制日志清理
- **Method**: `POST`
- **Path**: `/api/device-monitor/log/cleanup`
- **权限要求**: `device_monitor:log:cleanup` (需管理员)
- **请求参数 (Body - JSON)**:
  ```json
  {
    "mode": "time", // "policy" 按策略清理，"time" 按指定时间段清理
    "beforeTime": "2026-05-01 00:00:00",
    "startTime": "2026-05-01 00:00:00",
    "endTime": "2026-05-15 00:00:00",
    "force": true // 强制执行
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "cleanedCount": 500 // 已清理的日志记录条数
  }
  ```

### 7. 客户端前端日志匿名上报 (带限流)
- **Method**: `POST`
- **Path**: `/api/device-monitor/log/client`
- **权限要求**: 公开接口 (匿名，每个 IP 每分钟限流 30 条)
- **请求参数 (Body - JSON)**:
  ```json
  {
    "level": "ERROR",
    "content": "Uncaught SyntaxError...",
    "detail": {
      "url": "http://localhost/realtime",
      "userAgent": "Firefox"
    }
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  true
  ```

---

## 九、 系统管理模块 (27 个接口)
*源码参考：[system_manage.py](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/system_manage.py)*

### 【系统基本接口 (2 个)】

#### 1. 系统底座健康检查
- **Method**: `GET`
- **Path**: `/healthz`
- **权限要求**: 无 (公开接口)
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  {
    "service": "backend",
    "stage": "monitor-read-ready",
    "apiPrefix": "/api/device-monitor",
    "mysqlDatabase": "device_watch",
    "tdengineDatabase": "device_data"
  }
  ```

#### 2. 系统服务级 Ping 检查
- **Method**: `GET`
- **Path**: `/api/device-monitor/system/ping`
- **权限要求**: 无 (公开接口)
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  {
    "service": "device-monitor",
    "stage": "monitor-read-ready",
    "nextStep": "ws/realtime + local startup validation",
    "apiPrefix": "/api/device-monitor"
  }
  ```

---

### 【系统用户管理接口 (7 个)】

#### 1. 系统用户分页列表查询
- **Method**: `GET`
- **Path**: `/api/user/list`
- **权限要求**: `system:user:view`
- **请求参数 (Query)**:
  - `pageNo` / `current`, `pageSize` / `size` (str, 可选): 分页参数
  - `userName`, `userPhone`, `userEmail`, `userGender`, `status` (str, 可选)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "id": 1,
        "userName": "admin",
        "nickName": "超级管理员",
        "userPhone": "13800000000",
        "userEmail": "admin@example.com",
        "userGender": "male",
        "avatar": "",
        "status": "1",
        "userRoles": ["admin"]
      }
    ],
    "total": 1
  }
  ```

#### 2. 系统用户详情查询
- **Method**: `GET`
- **Path**: `/api/user/{user_id}`
- **权限要求**: `system:user:view`
- **请求参数 (Path)**:
  - `user_id` (int): 用户 ID
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 1,
    "avatar": "",
    "status": "1", // "1" 启用，"0" 禁用
    "userName": "admin",
    "userGender": "male",
    "nickName": "超级管理员",
    "userPhone": "13800000000",
    "userEmail": "admin@example.com",
    "userRoles": ["admin"], // 已授权的角色代码列表
    "createBy": "system",
    "createTime": "2026-05-20 10:00:00",
    "updateBy": "system",
    "updateTime": "2026-05-20 10:00:00"
  }
  ```

#### 3. 新建系统用户
- **Method**: `POST`
- **Path**: `/api/user`
- **权限要求**: `system:user:create`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "userName": "operator",
    "nickName": "值班操作员",
    "userPhone": "13800001111",
    "userEmail": "op@example.com",
    "userGender": "male",
    "userRoles": ["operator"], // 关联的角色代码列表
    "password": "login_password", // 初始密码
    "status": "1" // "1" 启用，"0" 禁用
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 创建成功后的系统用户 ID
  }
  ```

#### 4. 修改系统用户配置
- **Method**: `PUT`
- **Path**: `/api/user/{user_id}`
- **权限要求**: `system:user:update`
- **请求参数 (Path & Body - JSON)**:
  - Path: `user_id` (int)
  - Body: 与新建系统用户类似的 Payload (userName 非必传)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 修改成功后的系统用户 ID
  }
  ```

#### 5. 修改系统用户启用状态
- **Method**: `PATCH`
- **Path**: `/api/user/{user_id}/status`
- **权限要求**: `system:user:update`
- **请求参数 (Path & Body - JSON)**:
  - Path: `user_id` (int)
  - Body: `{"status": "0"}` (启用为 "1"，禁用为 "0")
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 更新成功后的系统用户 ID
  }
  ```

#### 6. 重置系统用户密码
- **Method**: `PATCH`
- **Path**: `/api/user/{user_id}/password/reset`
- **权限要求**: `system:user:password_reset`
- **请求参数 (Path & Body - JSON)**:
  - Path: `user_id` (int)
  - Body: `{"password": "new_password"}` (若不传或为空，后端 `PasswordResetPayload` 校验类会自动兼容并重置为默认值 `"123456"`)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 重置密码成功后的系统用户 ID
  }
  ```

#### 7. 删除系统用户
- **Method**: `DELETE`
- **Path**: `/api/user/{user_id}`
- **权限要求**: `system:user:delete`
- **请求参数 (Path)**:
  - `user_id` (int)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 删除成功后的系统用户 ID
  }
  ```

---

### 【系统角色管理接口 (9 个)】

#### 1. 角色分页列表查询
- **Method**: `GET`
- **Path**: `/api/role/list`
- **权限要求**: `system:role:view` 或 `system:role:grant`
- **请求参数 (Query)**:
  - `pageNo` / `current`, `pageSize` / `size` (str, 可选)
  - `roleName`, `roleCode`, `description`, `enabled`, `startTime`, `endTime` (str, 可选)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "list": [
      {
        "id": 1,
        "roleName": "管理员",
        "roleCode": "admin",
        "description": "拥有系统最高权限",
        "enabled": true
      }
    ],
    "total": 1
  }
  ```

#### 2. 系统用户编辑时角色下拉选项
- **Method**: `GET`
- **Path**: `/api/role/options`
- **权限要求**: `system:role:view` 或 `system:user:create` 或 `system:user:update`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "value": "admin",
      "label": "管理员"
    }
  ]
  ```

#### 3. 查询角色详情
- **Method**: `GET`
- **Path**: `/api/role/{role_id}`
- **权限要求**: `system:role:view`
- **请求参数 (Path)**:
  - `role_id` (int)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "roleId": 1,
    "roleName": "管理员",
    "roleCode": "admin",
    "description": "拥有系统最高权限",
    "enabled": true,
    "createTime": "2026-05-20 10:00:00"
  }
  ```

#### 4. 新建角色
- **Method**: `POST`
- **Path**: `/api/role`
- **权限要求**: `system:role:create`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "roleName": "测试角色",
    "roleCode": "test_role",
    "description": "测试用",
    "enabled": true
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 创建成功后的角色 ID
  }
  ```

#### 5. 修改角色配置
- **Method**: `PUT`
- **Path**: `/api/role/{role_id}`
- **权限要求**: `system:role:update`
- **请求参数 (Path & Body - JSON)**:
  - Path: `role_id` (int)
  - Body: 同新建角色参数 Payload。
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 修改成功后的角色 ID
  }
  ```

#### 6. 切换角色启用状态
- **Method**: `PATCH`
- **Path**: `/api/role/{role_id}/status`
- **权限要求**: `system:role:update`
- **请求参数 (Path & Body - JSON)**:
  - Path: `role_id` (int)
  - Body: `{"enabled": false}`
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 状态变更成功后的角色 ID
  }
  ```

#### 7. 删除角色
- **Method**: `DELETE`
- **Path**: `/api/role/{role_id}`
- **权限要求**: `system:role:delete`
- **请求参数 (Path)**:
  - `role_id` (int)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 删除成功后的角色 ID
  }
  ```

#### 8. 获取角色的授权数据
- **Method**: `GET`
- **Path**: `/api/role/{role_id}/grant`
- **权限要求**: `system:role:view`
- **请求参数 (Path)**:
  - `role_id` (int)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "menuIds": [1, 2, 3], // 角色关联的菜单列表
    "permissionIds": [10, 11], // 角色关联的按钮权限列表
    "dataScopes": []
  }
  ```

#### 9. 更新角色的菜单和按钮权限授权
- **Method**: `PUT`
- **Path**: `/api/role/{role_id}/grant`
- **权限要求**: `system:role:grant`
- **请求参数 (Path & Body - JSON)**:
  - Path: `role_id` (int)
  - Body:
    ```json
    {
      "menuIds": [1, 2],
      "permissionIds": [10],
      "dataScopes": []
    }
    ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 2 // 授权成功后的角色 ID
  }
  ```

---

### 【系统菜单与按钮权限管理接口 (9 个)】

#### 1. 获取当前登录用户的左侧动态菜单树
- **Method**: `GET`
- **Path**: `/api/v3/system/menus`
- **权限要求**: 需登录携带 JWT 凭证
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "id": 1,
      "path": "/system",
      "name": "system",
      "component": "Layout",
      "meta": {
        "title": "menu.system", // 国际化语言 Key
        "icon": "setting-icon", // 图标
        "keepAlive": true, // 是否页面缓存
        "isHide": false, // 是否在菜单中隐藏
        "isHideTab": false, // 是否在 Tab 页中隐藏
        "link": null, // 外链地址 (如有)
        "isIframe": false, // 是否是 iframe 嵌入
        "fixedTab": false, // 是否固定 Tab 页
        "activePath": null, // 激活的高亮菜单路由
        "isFullPage": false, // 是否是全屏页
        "authList": [ // 该菜单下拥有的按钮级权限列表
          {
            "id": 10,
            "title": "新增用户",
            "authMark": "sys:user:add"
          }
        ]
      },
      "children": [] // 嵌套的子菜单树列表
    }
  ]
  ```

#### 2. 系统菜单管理处查询完整菜单树
- **Method**: `GET`
- **Path**: `/api/v3/system/menus/admin`
- **权限要求**: `system:menu:view` 或 `system:role:grant`
- **请求参数**: 无
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "id": 1,
      "parentId": null, // 父节点 ID
      "path": "/system",
      "name": "system",
      "component": "Layout",
      "menuType": "menu", // 菜单节点类型 ("menu" / "button" 等)
      "sort": 10, // 排序权重
      "enabled": true, // 启用状态
      "meta": {
        "title": "menu.system",
        "icon": "setting-icon",
        "keepAlive": true,
        "isHide": false,
        "isHideTab": false,
        "link": null,
        "isIframe": false,
        "fixedTab": false,
        "activePath": null,
        "isFullPage": false,
        "authList": []
      },
      "children": [] // 包含禁用菜单的完整子树
    }
  ]
  ```

#### 3. 新建菜单
- **Method**: `POST`
- **Path**: `/api/v3/system/menu`
- **权限要求**: `system:menu:create`
- **请求参数 (Body - JSON)**:
  ```json
  {
    "name": "system", // 路由名称
    "path": "/system", // 路由路径
    "component": "Layout", // 组件名
    "titleKey": "menu.system", // 国际化 title Key
    "icon": "setting-icon", // 图标
    "parentId": null, // 父菜单 ID，根节点为 null
    "menuType": "menu", // 类型: "menu" | "iframe" 等
    "sort": 10, // 排序权重
    "enabled": true
  }
  ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 101 // 新增成功的菜单 ID
  }
  ```

#### 4. 修改菜单配置
- **Method**: `PUT`
- **Path**: `/api/v3/system/menu/{menu_id}`
- **权限要求**: `system:menu:update`
- **请求参数 (Path & Body - JSON)**:
  - Path: `menu_id` (int)
  - Body: 同新建菜单参数 Payload 结构。
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 101 // 修改成功后的菜单 ID
  }
  ```

#### 5. 删除菜单
- **Method**: `DELETE`
- **Path**: `/api/v3/system/menu/{menu_id}`
- **权限要求**: `system:menu:delete`
- **请求参数 (Path)**:
  - `menu_id` (int)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 101 // 删除成功后的菜单 ID
  }
  ```

#### 6. 获取菜单下包含的按钮权限列表
- **Method**: `GET`
- **Path**: `/api/v3/system/menu/{menu_id}/permissions`
- **权限要求**: `system:menu:view`
- **请求参数 (Path)**:
  - `menu_id` (int): 菜单 ID
- **返回结构 (`data` 字段)**:
  ```json
  [
    {
      "id": 10,
      "permissionCode": "sys:user:add", // 按钮权限编码
      "permissionName": "新增系统用户", // 按钮权限名称
      "permissionType": "action", // 权限动作类型
      "authMark": "sys:user:add", // 按钮级路由鉴权标识
      "description": "允许管理员创建用户", // 描述
      "sort": 1, // 排序
      "enabled": true, // 启用状态
      "menuId": 5 // 所属菜单 ID
    }
  ]
  ```

#### 7. 在菜单下创建按钮权限
- **Method**: `POST`
- **Path**: `/api/v3/system/menu/{menu_id}/permissions`
- **权限要求**: `system:menu:create`
- **请求参数 (Path & Body - JSON)**:
  - Path: `menu_id` (int)
  - Body:
    ```json
    {
      "permissionCode": "sys:user:delete",
      "permissionName": "删除系统用户",
      "permissionType": "action",
      "authMark": "sys:user:delete", // 按钮级校验标识
      "sort": 2,
      "enabled": true
    }
    ```
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 102 // 创建成功后的按钮权限 ID
  }
  ```

#### 8. 修改菜单下的按钮权限
- **Method**: `PUT`
- **Path**: `/api/v3/system/menu/{menu_id}/permissions/{permission_id}`
- **权限要求**: `system:menu:update`
- **请求参数 (Path & Body - JSON)**:
  - Path: `menu_id` (int), `permission_id` (int)
  - Body: 同上。
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 102 // 修改成功后的按钮权限 ID
  }
  ```

#### 9. 删除菜单下的按钮权限
- **Method**: `DELETE`
- **Path**: `/api/v3/system/menu/{menu_id}/permissions/{permission_id}`
- **权限要求**: `system:menu:delete`
- **请求参数 (Path)**:
  - Path: `menu_id` (int), `permission_id` (int)
- **返回结构 (`data` 字段)**:
  ```json
  {
    "id": 102 // 删除成功后的按钮权限 ID
  }
  ```
