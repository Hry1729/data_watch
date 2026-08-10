# DataWatch CLI

DataWatch CLI 是面向运维人员、脚本和 AI Agent 的 Backend 命令行客户端。当前已完成 M0-M3：真实 HTTP/WebSocket 调用、三级权限预检查、只读诊断、设备与点位管理、节点与系统管理均已接入；M4 MCP Server 按计划延期到其他工作完成后单独设计。

CLI 只访问 Backend API，不直接连接 MySQL、Redis 或 TDengine，也不输出模拟业务数据。

## 安装和运行

在项目根目录使用 PowerShell：

```powershell
python -m pip install -r cli\requirements.txt
python -m cli.main version
python -m cli.main --help
```

配置 Backend 并登录：

```powershell
python -m cli.main config set-url http://127.0.0.1:8000
python -m cli.main system health
python -m cli.main auth login --username admin
python -m cli.main auth whoami
python -m cli.main auth capabilities
```

自动化程序通过标准输入传递密码，不要把密码写入命令参数：

```powershell
$password | python -m cli.main auth login --username admin --password-stdin
```

修改当前账号密码时，标准输入依次提供旧密码和新密码：

```powershell
@($oldPassword, $newPassword) | python -m cli.main auth change-password --password-stdin
```

## Profile 和凭据

```powershell
python -m cli.main config profile create production --url https://backend.example.com --timeout 30
python -m cli.main config profile use production
python -m cli.main config profile list
python -m cli.main --profile production system health
python -m cli.main --api-url http://127.0.0.1:8000 --timeout 5 system health
```

全局选项必须写在一级命令之前。支持以下环境变量：

| 环境变量 | 作用 |
|---|---|
| `DATAWATCH_CONFIG_FILE` | 覆盖配置文件路径 |
| `DATAWATCH_PROFILE` | 覆盖当前 Profile |
| `DATAWATCH_API_URL` | 覆盖 Backend URL |
| `DATAWATCH_TIMEOUT` | 覆盖请求超时秒数 |
| `DATAWATCH_TOKEN` | 覆盖 Windows 凭据中的访问令牌 |
| `DATAWATCH_REFRESH_TOKEN` | 配合环境访问令牌提供刷新令牌 |

默认配置文件为 `%APPDATA%\DataWatch\cli-config.json`。登录令牌不写入配置文件，而是按 Profile 保存到 Windows Credential Manager。环境 Token 的优先级更高。

## 权限模型

`/api/user/info` 分别返回：

- `buttons`：前端按钮可见性使用的 `auth_mark`。
- `permissions`：Backend 和 CLI 鉴权使用的 `permission_code`。

CLI 只使用 `permissions` 做本地预检查，Backend 仍是最终鉴权方。`auth capabilities` 会根据当前账号列出允许和拒绝的命令。

- `R_USER`：仪表盘、设备运行状态、实时数据、历史查询、模块与日志只读。
- `R_ADMIN`：在授权范围内增加设备、点位、驱动、历史导出和边缘节点管理。
- `R_SUPER`：增加用户、角色、菜单、日志策略、DEBUG 和清理等系统管理能力。

实际能力以账号收到的 `permission_code` 和 Backend 角色检查为准。

## 可用功能

| 命令组 | 主要能力 |
|---|---|
| `config` | URL、Profile 和超时配置 |
| `auth` | 登录、账号信息、能力发现、改密、注销 |
| `dashboard` | 概览、趋势、类型分布、连接和边缘链路健康 |
| `devices` | 运行状态、目录查询、增删改、启停、XLSX 导入导出 |
| `points` | 点位查询、增删改、启停、模板和 XLSX 导入导出 |
| `realtime` | 设备树、单次读取、WebSocket 持续订阅 |
| `history` | query/chart/raw/matrix 和异步导出下载 |
| `system-alerts` | 基于真实系统日志的 WARN/ERROR/CRITICAL 汇总和列表 |
| `modules` | Collector、Writer、速率、聚合健康和采集配置 |
| `drivers` | 驱动列表、选项、注册、卸载和卸载状态 |
| `nodes` | 节点、占用、分配、启停、设置、注册码和状态同步 |
| `logs` | 日志查询、TDengine 日志、策略、临时 DEBUG 和清理 |
| `users` | 用户查询、创建、更新、启停、重置密码和删除 |
| `roles` | 角色管理和菜单/权限授权 |
| `menus` | 当前菜单、管理树、菜单和权限定义管理 |
| `system` | Backend 健康检查 |

使用任一层级的 `--help` 查看完整参数，例如：

```powershell
python -m cli.main devices --help
python -m cli.main realtime watch --help
python -m cli.main roles grants set --help
```

## 业务标识解析

面向用户和 AI 的查询不要求知道设备数据库 ID 或内部采集组 `g001`：

- 设备可使用现场称呼、设备编码、业务分组名称或 ID，例如 `桥吊100`、`QC100`、`轨道吊701陆侧`、`轨道吊701L`、`AGV801`。
- 点位可使用 IO 地址、点位名称或变量序号，例如 `I101`、`DB1702.DBW0`、`CraneID`。设备现场称呼会按 `桥吊→QC`、`轨道吊→ASC`、`陆侧→L`、`海侧→S` 归一化，大小写不敏感。
- `realtime get/watch` 根据点位自动推断设备和采集组；内部 `--group` 参数保持兼容但不在帮助中展示。
- `history query/chart/raw/matrix/export` 可省略 `--device`；多个点位必须属于同一设备。
- 匹配采用“精确优先、唯一模糊匹配”。若多个设备存在同名点位，CLI 返回结构化候选，AI 必须结合设备名称追问用户，不得默认选择第一项。

可先显式查看解析结果：

```powershell
python -m cli.main points resolve I101 --device 轨道吊701陆侧
python -m cli.main points resolve DB1702.DBW0
```

## 常用示例

```powershell
# 设备启停状态与采集连接状态
python -m cli.main devices status --device 桥吊100

# 按 IO 地址或点位名称读取实时值，设备和采集组由 CLI 推断
python -m cli.main realtime get --point DB1702.DBW0
python -m cli.main realtime watch --device QC100 --point CraneID

# 历史查询；点位全局唯一时无需提供设备
python -m cli.main history query --point CraneID --start "2026-08-10 00:00:00" --end "2026-08-10 01:00:00"

# 设备和点位管理
python -m cli.main devices create --file device.json
python -m cli.main points import --file points.xlsx

# 节点追加设备：CLI 会合并现有分配，全量提交时不会删除旧绑定
python -m cli.main nodes assign <node-uuid> --device AGV801 --confirm <node-uuid>

# 系统管理
python -m cli.main users disable 9 --confirm 9
python -m cli.main roles grants set 5 --file grant.json --confirm 5
python -m cli.main logs cleanup --before "2026-08-01T00:00:00" --confirm cleanup
```

## 实时订阅

`realtime watch` 输出 JSON Lines。WebSocket URL 不携带 Token；连接后 CLI 先发送：

```json
{"type":"auth","token":"<登录接口返回的原值>"}
```

认证成功后再发送订阅消息。当前 Backend 路由实际接受 `device_monitor:monitor:view` 或 `device_monitor:realtime:view`，CLI 与该行为保持一致。

## 高风险操作

删除、批量删除、驱动卸载、节点分配/停用、用户或角色停用与删除、角色授权修改、菜单删除、日志清理等命令要求 `--confirm` 精确匹配资源标识或固定确认词。确认失败时不会发送写请求。

创建和更新复杂对象使用 `--file payload.json`；设备和点位导入使用 `.xlsx`。下载命令先写临时文件，再原子替换目标文件。

## 输出契约

成功结果写入 stdout：

```json
{"ok":true,"command":"version","data":{"version":"0.1.0"}}
```

失败结果写入 stderr：

```json
{"ok":false,"command":"auth whoami","error":{"type":"auth_required","message":"请先执行 auth login"}}
```

默认每次命令输出一个紧凑 JSON 文档；全局 `--pretty` 可格式化输出。`realtime watch` 是每行一个 JSON 文档。CLI 不把访问令牌、刷新令牌或密码写入成功输出。

| 退出码 | 含义 |
|---:|---|
| `0` | 成功 |
| `2` | 参数、配置或确认错误 |
| `3` | 未登录、令牌失效或认证失败 |
| `4` | 权限不足 |
| `5` | 网络连接失败 |
| `6` | Backend 或 CLI 执行错误 |
| `7` | 部分成功 |
| `8` | 请求超时 |

## 业务边界

当前项目没有设备点位告警引擎。CLI 不提供阈值越限、通讯中断、状态位告警、确认或恢复命令，也不会调用 Dashboard 的 synthetic 告警接口。`system-alerts` 仅表示真实系统/操作日志中的高等级事件。

Backend 当前签发 Refresh Token，但没有消费它的刷新端点；访问令牌失效后需重新登录。M4 MCP Server 和 M5 独立 EXE/安装包尚未实施。
