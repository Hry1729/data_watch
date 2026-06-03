# Data Watch Console 页面设计说明

> 依据 `总览.png`、`collector.png`、`设置.png` 整理，并同步 app1.0 当前 React/Tauri 实现状态。
> 目标：为 AI 辅助开发提供清晰的桌面端页面结构、组件层级和样式约束。

## 1. 设计定位

Data Watch Console 是一个本机桌面控制台，用于管理 `collector` 和 `writer` 两个本地模块。

桌面端采用 `Tauri + React + shadcn/ui` 开发：

- DataWatch Agent（Windows 服务）负责进程启动、停止、配置读写、健康检查和日志采集。
- Tauri 负责窗口、系统托盘和 HTTP 代理，用于解决 WebView CORS 与本机 Agent 通信问题。
- React 负责页面状态、路由和模块化 UI 组织。
- shadcn/ui 负责基础组件风格，页面样式在其基础上做轻量定制。
- React 通过 Agent HTTP API 驱动运维操作，通过 Backend API 获取业务指标、速率和趋势数据。

核心能力：

- 在总览页启动、停止、重启 collector / writer。
- 在总览页查看模块运行状态、资源占用、运行时长。
- 查看采集点 / 入库点趋势。
- 在 collector / writer 详情页查看任务、趋势和实时日志。
- 管理 Redis、MySQL、TDengine、collector、writer 等配置。

整体风格参考 CC Switch：

- 原生桌面应用感。
- 支持亮色 / 暗色 / 跟随系统三态主题。
- 无左侧导航栏。
- 无底部状态栏。
- 页面顶部使用紧凑分段导航。
- 内容以大卡片、一行一个配置项、轻量图表为主。

## 2. 全局窗口结构

### 2.1 普通页面窗口

适用于：总览页、Collector 页、Writer 页。

```text
+--------------------------------------------------------------------------------+
| [app icon] Data Watch Console                                      _  []  X     |
+--------------------------------------------------------------------------------+
|                         +-----------+-----------+-----------+      Redis MySQL  |
|                         |   总览    | Collector |  Writer   |      TDengine [S] |
|                         +-----------+-----------+-----------+                   |
+--------------------------------------------------------------------------------+
|                                                                                |
|                                page content                                    |
|                                                                                |
+--------------------------------------------------------------------------------+
```

说明：

- 顶部第一行是系统标题栏，包含应用图标、应用名、窗口按钮。
- 顶部第二行是应用导航栏。
- 普通页面导航只包含三个入口：`总览`、`Collector`、`Writer`。
- 右侧显示连接状态：`Redis`、`MySQL`、`TDengine`。
- 最右侧是设置按钮。
- 不使用左侧导航栏。
- 不使用底部状态栏。

### 2.2 设置页面窗口

设置页进入后，隐藏普通页面导航和连接状态。

```text
+--------------------------------------------------------------------------------+
| [app icon] Data Watch Console                                      _  []  X     |
+--------------------------------------------------------------------------------+
|                                                                                |
|  [<-]  设置                                                                     |
|                                                                                |
|  +----------------+----------------+----------------+----------------+         |
|  |      基础      |     数据库     |   Collector    |     Writer     |         |
|  +----------------+----------------+----------------+----------------+         |
|                                                                                |
|                                settings content                                |
|                                                                                |
+--------------------------------------------------------------------------------+
```

说明：

- 设置页左上角有返回按钮。
- 标题为 `设置`。
- 设置页自己的导航栏为：`基础`、`数据库`、`Collector`、`Writer`。
- 设置页不显示 `总览 / Collector / Writer` 主导航。
- 设置页不显示 Redis / MySQL / TDengine 顶部连接状态。

## 3. 视觉规范

### 3.1 颜色

```text
window background       #F7F9FC
card background         #FFFFFF
border                  #DDE3ED
text primary            #111827
text secondary          #475569
text muted              #6B7280
accent blue             #1677FF
accent blue hover       #0F6CE8
success green           #16A05D
success background      #EAF8EF
danger red              #E5483F
warning amber           #F59E0B
chart line blue         #1677FF
```

### 3.2 尺寸与间距

```text
window target size      1280 x 800 or scalable
title bar height        64
top nav height          72
page padding            24
card radius             12
button radius           8
card border width       1
overview module card    230 - 260
overview chart height   160
detail main area        595
metric card height      105 - 120
log panel height        560
settings row height     96 - 112
```

### 3.3 字体

```text
font family             Segoe UI, Microsoft YaHei UI, Inter, sans-serif
window title            20 / 600
page title              28 / 700
card title              24 / 700
section title           22 / 700
body                    16 / 400
meta label              14 / 500
metric value            28 / 700
log text                14 / monospace
```

### 3.4 通用组件风格

```text
Card:
  background: white
  border: 1px solid border
  border-radius: 12px
  shadow: very subtle

Active tab:
  border: 1px solid accent blue
  text: accent blue
  background: white or light blue

Primary button:
  border: 1px solid border
  background: white
  icon color: accent blue / success / danger

Status pill:
  running: green text + pale green background
  stopped: gray text + pale gray background
  error: red text + pale red background
```

## 4. 总览页

### 4.1 页面目标

展示 collector 和 writer 两个模块的概要信息，并提供基础操作入口。

总览页包含：

- Collector 概要卡片。
- Writer 概要卡片。
- 采集点趋势图。
- 入库点趋势图。

### 4.2 ASCII 结构

```text
+--------------------------------------------------------------------------------+
| [icon] Data Watch Console                                          _  []  X     |
+--------------------------------------------------------------------------------+
|                         +-----------+-----------+-----------+    o Redis        |
|                         |   总览    | Collector |  Writer   |    o MySQL  [G]  |
|                         +-----------+-----------+-----------+    o TDengine     |
+--------------------------------------------------------------------------------+
|                                                                                |
| +--------------------------------------+  +------------------------------------+ |
| | Collector 采集模块        [运行中]    |  | Writer 入库模块        [运行中]    | |
| | 实时设备数据采集与流式入队            |  | Redis Stream 消费与 TDengine 写入  | |
| |                                      |  |                                    | |
| | +----------+ +----------+ +----------+ |  | +----------+ +----------+ +--------+ |
| | |运行时长   | |内存使用   | |CPU 使用率 | |  | |运行时长   | |内存使用   | |CPU 使用率|
| | |03:42:18 | |186 MB    | |2.4%      | |  | |03:41:55 | |142 MB    | |1.1%    |
| | +----------+ +----------+ +----------+ |  | +----------+ +----------+ +--------+ |
| |                                      |  |                                    | |
| | [启动] [停止] [重启] [日志]           |  | [启动] [停止] [重启] [日志]         | |
| +--------------------------------------+  +------------------------------------+ |
|                                                                                |
| +------------------------------------------------------------------------------+|
| | 采集点趋势                                        最近 10 分钟                ||
| | -- pts/min                                                                    ||
| | +--------------------------------------------------------------------------+ ||
| | |                                数据接入中                                | ||
| | +--------------------------------------------------------------------------+ ||
| +------------------------------------------------------------------------------+|
|                                                                                |
| +------------------------------------------------------------------------------+|
| | 入库点趋势                                        最近 10 分钟                ||
| | -- rows/min                                                                   ||
| | +--------------------------------------------------------------------------+ ||
| | |                                数据接入中                                | ||
| | +--------------------------------------------------------------------------+ ||
| +------------------------------------------------------------------------------+|
|                                                                                |
+--------------------------------------------------------------------------------+
```

### 4.3 组件说明

模块概要卡片：

```text
ModuleSummaryCard
  title
  subtitle
  status_pill
  metrics:
    uptime
    memory
    cpu
  actions:
    start
    stop
    restart
    log
```

约束：

- 模块概要卡片不显示大图标。
- 三个运行指标小卡片不显示 icon。
- 启动按钮为蓝色 outline 风格：蓝色边框、蓝色文字、蓝色图标。
- 停止按钮为红色 outline 风格。
- 四个操作按钮高度统一，略高于默认按钮。

趋势图卡片：

```text
TrendChart
  title
  range_label: 最近 10 分钟
  current_value
  unit
  state: empty | loading | error | ready
  chart_area
```

### 4.4 数据字段

Collector 卡片：

```text
title         Collector 采集模块
subtitle      实时设备数据采集与流式入队
status        running
memory        186 MB
cpu           2.4%
uptime        03:42:18
```

Writer 卡片：

```text
title         Writer 入库模块
subtitle      Redis Stream 消费与 TDengine 批量写入
status        running
memory        142 MB
cpu           1.1%
uptime        03:41:55
```

## 5. Collector 页面

### 5.1 页面目标

展示 collector 模块详情，包括采集指标、采集任务、采集趋势和实时日志。

详情页不再显示进程标题卡片，也不显示启动 / 停止 / 重启 / 打开日志操作按钮；模块控制集中在总览页完成。

### 5.2 ASCII 结构

```text
+--------------------------------------------------------------------------------+
| [icon] Data Watch Console                                          _  []  X     |
+--------------------------------------------------------------------------------+
|                         +-----------+-----------+-----------+    o Redis        |
|                         |   总览    | Collector |  Writer   |    o MySQL  [G]  |
|                         +-----------+-----------+-----------+    o TDengine     |
+--------------------------------------------------------------------------------+
|                                                                                |
| +------------------+ +------------------+ +------------------+ +------------------+ |
| | 采集周期          | | 设备连接          | | 采集点位          | | Redis 写入        | |
| | 100 ms           | | 18 / 18          | | 3,264            | | 12,480 pts/min   | |
| +------------------+ +------------------+ +------------------+ +------------------+ |
|                                                                                |
| +---------------------------------------------+ +------------------------------+ |
| | 采集任务                                     | | 实时日志              auto on | |
| | +--------+--------+----------+------+-------+ | |                              | |
| | | 设备   | 分组   | 协议     | 周期 | 点位  | | | 14:26:10 INFO Collector started |
| | +--------+--------+----------+------+-------+ | | 14:26:10 INFO Subscribe Redis    |
| | | AGV-01 | main   | OPC DA   |100ms | 384   | | | 14:26:11 INFO Device connected   |
| | | PLC-02 | line1  | S7       |500ms | 620   | | | 14:26:12 WARN Retry tag read     |
| | | QC-100 | main   | OPC UA   |1000ms| 220   | | | 14:26:12 ERROR read failed       |
| | +--------+--------+----------+------+-------+ | |                              | |
| |                                             | |                              | |
| | 采集点趋势 - 最近10分钟                      | |   log rows scroll inside card  | |
| | +-----------------------------------------+ | |                              | |
| | |              数据接入中                  | | |                              | |
| | +-----------------------------------------+ | |                              | |
| +---------------------------------------------+ +------------------------------+ |
|                                                                                |
+--------------------------------------------------------------------------------+
```

### 5.3 组件说明

指标卡片：

```text
MetricCard
  label
  value
```

采集任务表格：

```text
CollectorTaskTable
  columns:
    device
    group
    protocol
    interval
    point_count
    latest_time
    quality
```

日志面板：

```text
LogPanel
  header:
    title
    auto_scroll_toggle
    clear_button
  body:
    internal_scroll_area
    max_lines: 5000
  rows:
    timestamp
    level_chip
    message
```

布局约束：

- 页面主体采用左 3/5、右 2/5 两列布局。
- 主体区域固定高度，当前实现为 `595px`。
- 右侧日志卡片与左侧「任务表 + 趋势图」底部对齐。
- 日志持续追加时只能在日志卡片内部滚动，不得撑高页面。
- 日志卡片边框与趋势图一致：白底、`1px solid #DDE3ED`、`border-radius: 12px`。

### 5.4 示例表格数据

```text
+----------+-------+------------+--------+--------+----------+---------+
| 设备      | 分组  | 协议        | 周期   | 点位   | 最新时间 | 质量    |
+----------+-------+------------+--------+--------+----------+---------+
| AGV-01   | main  | OPC DA     | 100ms  | 384    | 14:26:08 | Good    |
| PLC-S7-02| line1 | S7         | 500ms  | 620    | 14:26:10 | Good    |
| QC-100   | main  | OPC UA     |1000ms  | 220    | 14:26:05 | Good    |
| METER-01 | power | Modbus TCP | 200ms  | 512    | 14:26:09 | Good    |
| ENV-01   | env   | Modbus RTU |1000ms  | 128    | 14:26:07 | Good    |
+----------+-------+------------+--------+--------+----------+---------+
```

## 6. Writer 页面

### 6.1 页面目标

Writer 页面与 Collector 页面结构一致，但内容聚焦 Redis Stream 消费、TDengine 写入和 DLQ。

详情页不再显示进程标题卡片，也不显示启动 / 停止 / 重启 / 打开日志操作按钮；模块控制集中在总览页完成。

### 6.2 建议 ASCII 结构

```text
+--------------------------------------------------------------------------------+
| [icon] Data Watch Console                                          _  []  X     |
+--------------------------------------------------------------------------------+
|                         +-----------+-----------+-----------+    o Redis        |
|                         |   总览    | Collector |  Writer   |    o MySQL  [G]  |
|                         +-----------+-----------+-----------+    o TDengine     |
+--------------------------------------------------------------------------------+
|                                                                                |
| +------------------+ +------------------+ +------------------+ +------------------+ |
| | Stream Lag       | | PEL Pending      | | 批量写入          | | DLQ              | |
| | 24               | | 3                | | 12,430 rows/min  | | 0                | |
| +------------------+ +------------------+ +------------------+ +------------------+ |
|                                                                                |
| +---------------------------------------------+ +------------------------------+ |
| | Stream 消费任务                      [R]... | | 实时日志              auto on | |
| | +-----------------------+------+-------+----+ | |                              | |
| | | Stream Key            | Lag  | PEL   |ACK | | | INFO XREADGROUP streams=8     |
| | +-----------------------+------+-------+----+ | | INFO map var_id to td_column   |
| | | rt:stream:agv:main:0  | 12   | 1     | OK | | | INFO batch insert rows=512    |
| | | rt:stream:agv:main:1  | 8    | 0     | OK | | | INFO XACK success count=512   |
| | | rt:stream:plc:line1:0 | 4    | 2     | OK | | | WARN pending drain claimed=2  |
| | +-----------------------+------+-------+----+ | |                              | |
| |                                             | |                              | |
| | 入库点趋势 - 最近10分钟                      | |                              | |
| | +-----------------------------------------+ | |   log rows scroll inside card  | |
| | |              数据接入中                  | | |                              | |
| | +-----------------------------------------+ | |                              | |
| +---------------------------------------------+ +------------------------------+ |
|                                                                                |
+--------------------------------------------------------------------------------+
```

### 6.3 组件说明

指标卡片：

```text
MetricCard
  label
  value
```

Stream 消费任务表格：

```text
WriterStreamTable
  columns:
    stream_key
    lag
    pel
    ack
```

日志面板：

```text
LogPanel
  header:
    title
    auto_scroll_toggle
    clear_button
  body:
    internal_scroll_area
    max_lines: 5000
  rows:
    timestamp
    level_chip
    message
```

布局约束：

- 页面主体采用左 3/5、右 2/5 两列布局。
- 主体区域固定高度，当前实现为 `595px`。
- 右侧日志卡片与左侧「Stream 消费任务 + 入库点趋势」底部对齐。
- 日志持续追加时只能在日志卡片内部滚动，不得撑高页面。
- 日志卡片边框与趋势图一致：白底、`1px solid #DDE3ED`、`border-radius: 12px`。

## 7. 设置页：数据库

### 7.1 页面目标

配置 Redis、MySQL、TDengine 连接参数，并提供保存和保存后重启策略。

### 7.2 ASCII 结构

```text
+--------------------------------------------------------------------------------+
| [icon] Data Watch Console                                          _  []  X     |
+--------------------------------------------------------------------------------+
|                                                                                |
|  [<-]  设置                                                                     |
|                                                                                |
|  +----------------+----------------+----------------+----------------+         |
|  |      基础      |     数据库     |   Collector    |     Writer     |         |
|  +----------------+----------------+----------------+----------------+         |
|                                                                                |
|  Redis 实时缓冲                                                               |
|  Collector 写入 Stream, Writer 消费 Stream, 二者必须指向同一实例。              |
|  +----------------------------------------------------------------------------+|
|  | [icon] Redis     主机 [127.0.0.1]  端口 [6379]  DB [0]  密码 [******]       ||
|  |                  Stream 分片数 [4]                         [已连接] [测试] ||
|  +----------------------------------------------------------------------------+|
|                                                                                |
|  MySQL 配置库                                                                 |
|  用于读取设备、点位、变量映射和 td_column_name。                               |
|  +----------------------------------------------------------------------------+|
|  | [icon] MySQL     主机 [127.0.0.1]  端口 [3306]  用户 [root] 密码 [******]   ||
|  |                  数据库 [device_watch]                       [已连接] [测试]||
|  +----------------------------------------------------------------------------+|
|                                                                                |
|  TDengine 时序库                                                              |
|  Writer 将 Redis Stream 批量写入 TDengine 宽表。                               |
|  +----------------------------------------------------------------------------+|
|  | [icon] TDengine  主机 [127.0.0.1]  端口 [6041] 用户 [root] 密码 [******]    ||
|  |                  数据库 [device_data] Stable 模式 [sd{type}] [已连接] [测试]||
|  +----------------------------------------------------------------------------+|
|                                                                                |
|  保存策略                                                                     |
|  连接参数保存后需要重启对应模块生效。                                          |
|  +----------------------------------------------------------------------------+|
|  | [保存]        [保存并重启 Collector]        [保存并重启 Writer]             ||
|  |                                                [!] 重启后生效               ||
|  +----------------------------------------------------------------------------+|
|                                                                                |
+--------------------------------------------------------------------------------+
```

### 7.3 设置页规则

- 每个数据库配置是一整行卡片，不拆成小卡片网格。
- 输入框横向排列。
- 密码字段默认掩码。
- 每行右侧提供连接状态和 `测试` 按钮。
- 保存策略单独成一行。
- 修改连接参数后提示 `重启后生效`。

## 8. 设置页：基础

基础页可放置应用级配置。

```text
+--------------------------------------------------------------------------------+
|  界面语言                                                                     |
|  切换后立即预览界面语言，保存后永久生效。                                      |
|  +-----------------------------------+                                         |
|  | [中文] [English] [日本語]          |                                         |
|  +-----------------------------------+                                         |
|                                                                                |
|  外观主题                                                                     |
|  选择应用的外观主题，立即生效。                                                |
|  +-----------------------------------+                                         |
|  | [浅色] [深色] [跟随系统]           |                                         |
|  +-----------------------------------+                                         |
|                                                                                |
|  页面密度                                                                     |
|  控制卡片和表格的间距。                                                        |
|  +-----------------------------------+                                         |
|  | [紧凑] [标准] [宽松]               |                                         |
|  +-----------------------------------+                                         |
+--------------------------------------------------------------------------------+
```

## 9. 设置页：Collector

```text
+--------------------------------------------------------------------------------+
|  启动配置                                                                     |
|  +----------------------------------------------------------------------------+|
|  | 可执行文件 [collector/data-watch-collector.exe]      [选择文件]             ||
|  | 工作目录   [D:/data_watch_deploy/data-watch-collector] [选择目录]           ||
|  +----------------------------------------------------------------------------+|
|                                                                                |
|  采集配置                                                                     |
|  +----------------------------------------------------------------------------+|
|  | 默认采集周期 [100] ms      日志级别 [INFO]      开机自启 [on/off]            ||
|  +----------------------------------------------------------------------------+|
|                                                                                |
|  Redis Key 预览                                                               |
|  +----------------------------------------------------------------------------+|
|  | rt:stream:{device_type}:{group_code}:{shard}                                ||
|  | rt:latest:{device_code}:{group_code}                                        ||
|  | rt:window:{device_code}:{group_code}:{scan_rate}                            ||
|  +----------------------------------------------------------------------------+|
+--------------------------------------------------------------------------------+
```

## 10. 设置页：Writer

```text
+--------------------------------------------------------------------------------+
|  启动配置                                                                     |
|  +----------------------------------------------------------------------------+|
|  | 可执行文件 [writer/data-watch-writer.exe]          [选择文件]               ||
|  | 工作目录   [D:/data_watch_deploy/data-watch-writer] [选择目录]              ||
|  +----------------------------------------------------------------------------+|
|                                                                                |
|  消费配置                                                                     |
|  +----------------------------------------------------------------------------+|
|  | flush_interval_ms [500]  batch_count [200]  max_buffer_rows [2000]          ||
|  | pending_min_idle_ms [30000] pending_max_retry [5]                           ||
|  +----------------------------------------------------------------------------+|
|                                                                                |
|  入库策略                                                                     |
|  +----------------------------------------------------------------------------+|
|  | TDengine database [device_data]     Stable pattern [sd{type}]               ||
|  | DLQ key pattern [rt:dlq:{device_type}:{group_code}]                         ||
|  +----------------------------------------------------------------------------+|
+--------------------------------------------------------------------------------+
```

## 11. 入口页：登录与 Bootstrap 向导

### 11.1 登录页

登录页是每次打开应用后进入主界面的认证入口。

目标：

- 输入 username / password。
- 同时获取 Agent JWT 和 Backend Token。
- 登录成功后进入总览页。
- 登录失败时在表单下方显示错误信息。

```text
+--------------------------------------------------------------------------------+
| [icon] Data Watch Console                                          _  []  X     |
+--------------------------------------------------------------------------------+
|                                                                                |
|                                                                                |
|                         +--------------------------------+                     |
|                         | Data Watch Console             |                     |
|                         | 本机采集与入库控制台             |                     |
|                         |                                |                     |
|                         | 用户名                         |                     |
|                         | [ admin                    ]   |                     |
|                         |                                |                     |
|                         | 密码                           |                     |
|                         | [ ******                   ]   |                     |
|                         |                                |                     |
|                         | [ 登录 ]                       |                     |
|                         |                                |                     |
|                         | Agent: http://127.0.0.1:xxxxx  |                     |
|                         +--------------------------------+                     |
|                                                                                |
|                                                                                |
+--------------------------------------------------------------------------------+
```

说明：

- 登录页不显示普通页面导航。
- 登录页不显示设置导航。
- 登录页不显示 Redis / MySQL / TDengine 连接状态。
- 密码输入框默认掩码。
- 登录请求走 Agent API 或 Tauri HTTP proxy，不在前端硬编码 token。

### 11.2 Bootstrap 向导页

Bootstrap 向导页只在首次安装、MySQL 未配置或 Agent 返回 bootstrap required 时出现。

目标：

- 填写 MySQL 连接信息。
- 测试连接。
- 完成初始化后跳转登录页。

```text
+--------------------------------------------------------------------------------+
| [icon] Data Watch Console                                          _  []  X     |
+--------------------------------------------------------------------------------+
|                                                                                |
|  初始化向导                                                                     |
|  首次使用前需要配置 MySQL，Agent 将使用该连接读取系统配置。                     |
|                                                                                |
|  +----------------------------------------------------------------------------+|
|  | MySQL 配置库                                                               ||
|  |                                                                            ||
|  | 主机 [127.0.0.1]   端口 [3306]   用户 [root]   密码 [******]               ||
|  | 数据库 [device_watch]                                                       ||
|  |                                                                            ||
|  | [测试连接]                                      [完成初始化]                ||
|  +----------------------------------------------------------------------------+|
|                                                                                |
|  状态: 等待测试连接                                                             |
|                                                                                |
+--------------------------------------------------------------------------------+
```

说明：

- Bootstrap 页是进入登录页前的安装向导。
- 只处理 MySQL 初始连接。
- 测试连接成功后才能点击完成初始化。
- 完成初始化后重新检查 `/bootstrap/status`，再跳转登录页。

## 12. 交互说明

### 12.1 模块控制按钮

```text
位置:
  - 启动 / 停止 / 重启 / 日志按钮只出现在总览页模块概要卡片中。
  - Collector / Writer 详情页不再显示模块控制按钮。

启动:
  - React 调用 Agent HTTP API。
  - Agent 判断模块状态并启动对应 exe。
  - Agent 负责设置 workingDirectory。
  - Tauri 不直接启动 collector / writer。

停止:
  - React 调用 Agent HTTP API。
  - Agent 优先温和停止进程。
  - 超时后的强制结束策略由 Agent 决定并返回结果。

重启:
  - React 调用 Agent HTTP API。
  - Agent 执行先停止、再启动。
  - 用于配置变更后生效。

日志:
  - 总览页点击日志按钮跳转到对应模块详情页。
  - 详情页通过 WS /viewer/logs/{name}/tail 接收 Agent 推送。
  - 日志面板与左侧任务表 + 趋势图区同高，日志内容在卡片内部滚动。
  - Agent 将 collector / writer stdout / stderr 写入滚动日志文件。
  - Tauri 不捕获 stdout / stderr。
```

### 12.2 连接状态

```text
green dot:
  connected

blue dot:
  checking

gray dot:
  unknown or not checked

red dot:
  disconnected
```

### 12.3 设置保存

```text
保存:
  - React 调用 Agent HTTP API。
  - Agent 写配置文件。
  - 不重启进程。

保存并重启 Collector:
  - Agent 写配置文件。
  - 如果 collector 正在运行，执行重启。

保存并重启 Writer:
  - Agent 写配置文件。
  - 如果 writer 正在运行，执行重启。
```

## 13. 推荐实现映射

### 13.1 技术栈

```text
Desktop shell:
  Tauri window
  Tauri tray
  Tauri HTTP proxy

Frontend:
  React
  TypeScript
  shadcn/ui
  Tailwind CSS

Agent:
  DataWatch Agent
  Windows service
  Python FastAPI
  Process / Config / Health / Metrics / Logs owner

Charts:
  Recharts
  TrendChart lightweight line / area chart
  1 / 5 / 10 minute range switch

Theme:
  custom ThemeContext
  light / dark / system
  semantic CSS tokens
```

### 13.2 前端页面结构

```text
App.tsx
  AppTitleBar
  MainNavigation
  <Routes> or page state switcher

pages/
  LoginPage.tsx
  BootstrapPage.tsx
  OverviewPage.tsx
  CollectorPage.tsx
  WriterPage.tsx
  SettingsPage.tsx

components/
  ModuleSummaryCard.tsx
  TrendChart.tsx
  LogPanel.tsx
  ConnectionStatusBar.tsx
  AppLayout.tsx
```

说明：

- Login / Bootstrap 页面不显示普通主导航。
- 登录成功后的普通页面使用 `MainNavigation` 显示 `总览 / Collector / Writer`。
- 设置页进入后隐藏普通导航，改用设置页内部导航 `基础 / 数据库 / Collector / Writer`。
- 页面切换可以使用 React Router，也可以用内部 page state 控制；首版以简单可维护为准。

### 13.3 shadcn/ui 组件建议

```text
Tabs                   top tabs and settings tabs
Button                 overview start / stop / restart / log, save, test
Card                   module cards, metric cards, settings rows
Badge                  running / connected / warn / error states
Input                  host / port / user / password / path fields
Table                  collector task table, writer stream table
Switch                 auto scroll, auto start, theme follow system
Dialog                 confirm stop, restart warning, error details
ScrollArea             optional wrapper; current LogPanel uses internal overflow-y-auto
Separator              top bar split, settings row split
Tooltip                icon button hints
```

自定义组件仍应包裹 shadcn/ui 基础组件，避免直接堆复杂样式。

### 13.4 Agent API 与 Tauri 映射

```text
React / Tauri proxy -> Agent HTTP API:
  GET  /bootstrap/status
  PUT  /bootstrap/config
  POST /bootstrap/test-connection
  POST /bootstrap/finish

  POST /auth/login

  POST /admin/processes/collector/start
  POST /admin/processes/collector/stop
  POST /admin/processes/collector/restart
  POST /admin/processes/writer/start
  POST /admin/processes/writer/stop
  POST /admin/processes/writer/restart

  GET  /viewer/status
  GET  /viewer/processes/{name}/metrics
  GET  /viewer/processes/{name}/status
  GET  /viewer/health

  GET  /admin/config
  PUT  /admin/config
  POST /admin/health/check

  WS   /viewer/logs/{name}/tail

React -> Backend API:
  POST /api/auth/login
  GET  /api/device-monitor/module-monitor/collector
  GET  /api/device-monitor/module-monitor/writer
  GET  /api/device-monitor/module-monitor/rates
  GET  /api/device-monitor/log/list

趋势图数据源：
  Backend module-monitor/rates。
  app 通过 useModuleRates 每 5 秒轮询。
  TrendChart 使用同源数据展示总览 2 张、Collector 1 张、Writer 1 张趋势图。
  不使用 /viewer/trends/*。

顶部连接状态点：
  Agent GET /viewer/health。
  app 通过 useConnectionHealth 每 10 秒轮询。
  窗口隐藏时暂停，重新可见时立即刷新。
  颜色语义：green=connected, blue=checking, red=disconnected, gray=unknown。

Tauri Rust commands:
  agent_request(...)
    - HTTP proxy from WebView to Agent
    - attaches Agent JWT when available
    - avoids WebView CORS issues

  show_window()
    - restore window from tray

  hide_to_tray()
    - hide main window to system tray
```

实现要求：

- Tauri Rust 层不实现 ProcessManager / ConfigManager / HealthManager / MetricsManager。
- 启动 collector / writer 时由 Agent 设置 `workingDirectory`。
- collector 的工作目录应指向 `data-watch-collector` 部署目录。
- writer 的工作目录应指向 `data-watch-writer` 部署目录。
- 日志由 Agent 写滚动文件，并通过 WebSocket 推送给前端。
- 配置读写统一由 Agent 完成，Tauri 只代理请求。
- 密码字段只在表单中短暂存在，日志和状态事件中不得输出明文。

### 13.5 推荐目录结构

```text
app/
  package.json
  src/
    App.tsx
    main.tsx
    index.css
    pages/
      LoginPage.tsx
      BootstrapPage.tsx
      OverviewPage.tsx
      CollectorPage.tsx
      WriterPage.tsx
      SettingsPage.tsx
    components/
      ModuleSummaryCard.tsx
      TrendChart.tsx
      LogPanel.tsx
      AppLayout.tsx
      ConnectionStatusBar.tsx
      ThemeToggle.tsx
    contexts/
      AuthContext.tsx
      ThemeContext.tsx
    hooks/
      useModuleStatus.ts
      useModuleMetrics.ts
      useModuleRates.ts
      useConnectionHealth.ts
      useWebSocketLog.ts
    lib/
      agent-client.ts
      backend-client.ts
      mock-agent.ts
  src-tauri/
    tauri.conf.json
    src/
      main.rs
      http_proxy.rs
      tray.rs
```

## 14. 开发注意事项

- 登录页和 Bootstrap 页是进入主界面的必经路径。
- Bootstrap 页只在 Agent 判断 MySQL 未配置时显示。
- 普通页面和设置页面的顶部导航不同，不能混用。
- 设置页不能显示 Redis / MySQL / TDengine 顶部连接状态。
- 概览页不显示日志。
- Collector / Writer 详情页才显示实时日志。
- Collector / Writer 详情页不显示进程标题卡片和操作按钮卡。
- 模块启动 / 停止 / 重启入口集中放在总览页。
- 日志面板高度必须被父级约束，日志行只能在面板内部滚动，不能撑高页面。
- 日志面板边框必须与趋势图卡片一致。
- 设置页数据库项必须是一行一个卡片。
- Tauri 不直接启动 collector / writer。
- Tauri 不直接读写 collector / writer 配置。
- Tauri 不捕获 collector / writer stdout / stderr。
- Agent 启动模块时必须设置工作目录，否则 `config/settings.toml` 可能无法找到。
- Writer 和 Collector 必须指向同一个 Redis 实例。
- TDengine stable pattern 使用 `sd{type}`。
- 密码输入框默认掩码，不在日志中打印明文。
- 图表只做轻量趋势，不做复杂 BI 大屏。
