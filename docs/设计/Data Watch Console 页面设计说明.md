# Data Watch Console 页面设计说明

> 依据 `总览.png`、`collector.png`、`设置.png` 整理。
> 目标：为 AI 辅助开发提供清晰的桌面端页面结构、组件层级和样式约束。

## 1. 设计定位

Data Watch Console 是一个本机桌面控制台，用于管理 `collector` 和 `writer` 两个本地模块。

核心能力：

- 启动、停止、重启 collector / writer。
- 查看模块运行状态、资源占用、运行时长。
- 查看采集点 / 入库点趋势。
- 查看 collector / writer 日志。
- 管理 Redis、MySQL、TDengine、collector、writer 等配置。

整体风格参考 CC Switch：

- 原生桌面应用感。
- 浅色主题。
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
window target size      1600 x 940 or scalable
title bar height        64
top nav height          72
page padding            24
card radius             12
button radius           8
card border width       1
module card height      210 - 230
overview chart height   270 - 290
collector top card      160 - 170
metric card height      105 - 120
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
| +------------------------------------------------------------------------------+|
| |  [large icon]  Collector 采集模块   [运行中]                         [...]   ||
| |                                                                              ||
| |  命令       collector/data-watch-collector.exe                               ||
| |  内存使用   186 MB                         运行时长     03:42:18             ||
| |  CPU 使用率 2.4%                           Redis Stream rt:stream:agv:main:* ||
| |                                                                              ||
| |                                      [启动] [停止] [重启] [日志]              ||
| +------------------------------------------------------------------------------+|
|                                                                                |
| +------------------------------------------------------------------------------+|
| |  [large icon]  Writer 入库模块      [运行中]                         [...]   ||
| |                                                                              ||
| |  命令       writer/data-watch-writer.exe                                     ||
| |  内存使用   142 MB                         运行时长     03:41:55             ||
| |  CPU 使用率 1.1%                           TDengine    device_data / sd{type}||
| |                                                                              ||
| |                                      [启动] [停止] [重启] [日志]              ||
| +------------------------------------------------------------------------------+|
|                                                                                |
| +-------------------------------------+  +-------------------------------------+ |
| |  采集点趋势    最近 10 分钟          |  |  入库点趋势    最近 10 分钟          | |
| |                             12,480 pts/min                         12,430 rows/min |
| |                                                                         ^    | |
| |   16k | - - - - - - - - - - -       |  |   16k | - - - - - - - - - - -       | |
| |   12k | ~~~ blue line ~~~~~~~       |  |   12k | ~~~ blue line ~~~~~~~       | |
| |    8k | - - - - - - - - - - -       |  |    8k | - - - - - - - - - - -       | |
| |    0  +----------------------       |  |    0  +----------------------       | |
| |       10:37 10:39 ... 10:47         |  |       10:37 10:39 ... 10:47         | |
| +-------------------------------------+  +-------------------------------------+ |
|                                                                                |
+--------------------------------------------------------------------------------+
```

### 4.3 组件说明

模块概要卡片：

```text
ModuleSummaryCard
  icon
  title
  status_pill
  more_button
  fields:
    command
    memory
    cpu
    uptime
    redis_stream or tdengine_target
  actions:
    start
    stop
    restart
    open_log
```

趋势图卡片：

```text
TrendChartCard
  title
  range_selector: 最近 10 分钟
  current_value
  unit
  expand_button
  line_chart
```

### 4.4 数据字段

Collector 卡片：

```text
title         Collector 采集模块
status        running
command       collector/data-watch-collector.exe
memory        186 MB
cpu           2.4%
uptime        03:42:18
stream        rt:stream:agv:main:*
```

Writer 卡片：

```text
title         Writer 入库模块
status        running
command       writer/data-watch-writer.exe
memory        142 MB
cpu           1.1%
uptime        03:41:55
tdengine      device_data / sd{type}
```

## 5. Collector 页面

### 5.1 页面目标

展示 collector 模块详情，包括进程信息、运行指标、采集任务、趋势图和实时日志。

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
| +------------------------------------------------------------------------------+|
| | [large icon] Collector 采集模块 [运行中]                                      ||
| |                                                                              ||
| | 命令       collector/data-watch-collector.exe   PID       4820                ||
| | 工作目录   D:/data_watch_deploy/data-watch-collector                         ||
| | 运行时长   03:42:18                         内存使用  186 MB                 ||
| |                                             CPU 使用率 2.4%                   ||
| |                                                       [启动] [停止] [重启] [打开日志] |
| +------------------------------------------------------------------------------+|
|                                                                                |
| +------------------+ +------------------+ +------------------+ +------------------+ |
| | 采集周期          | | 设备连接          | | 采集点位          | | Redis 写入        | |
| | 100 ms           | | 18 / 18          | | 3,264            | | 12,480 pts/min   | |
| +------------------+ +------------------+ +------------------+ +------------------+ |
|                                                                                |
| +---------------------------------------------+ +------------------------------+ |
| | 采集任务                              [R]... | | 实时日志              auto on | |
| | +--------+--------+----------+------+-------+ | |                              | |
| | | 设备   | 分组   | 协议     | 周期 | 点位  | | | 14:26:10 INFO Collector started |
| | +--------+--------+----------+------+-------+ | | 14:26:10 INFO Subscribe Redis    |
| | | AGV-01 | main   | OPC DA   |100ms | 384   | | | 14:26:11 INFO Device connected   |
| | | PLC-02 | line1  | S7       |500ms | 620   | | | 14:26:12 WARN Retry tag read     |
| | | QC-100 | main   | OPC UA   |1000ms| 220   | | | 14:26:12 ERROR read failed       |
| | | METER1 | power  | Modbus   |200ms | 512   | | | 14:26:13 INFO Heartbeat ok       |
| | +--------+--------+----------+------+-------+ | |                              | |
| |                                             | |                              | |
| | 采集点趋势 - 最近10分钟                      | |                              | |
| |  16k | - - - - - - - - - -                  | |                              | |
| |  12k | ~~~ blue line ~~~~~                  | |                              | |
| |   8k | - - - - - - - - - -                  | |                              | |
| |   0  +---------------------                  | |                              | |
| +---------------------------------------------+ +------------------------------+ |
|                                                                                |
+--------------------------------------------------------------------------------+
```

### 5.3 组件说明

Collector 详情卡片：

```text
CollectorHeaderCard
  icon
  title
  status_pill
  command
  working_directory
  pid
  uptime
  memory
  cpu
  actions:
    start
    stop
    restart
    open_log
```

指标卡片：

```text
MetricCard
  icon
  label
  value
  unit
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
    delete_button
  rows:
    timestamp
    level_chip
    message
```

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

Writer 页面应与 Collector 页面结构一致，但内容聚焦 Redis Stream 消费、MySQL 映射、TDengine 写入和 DLQ。

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
| +------------------------------------------------------------------------------+|
| | [large icon] Writer 入库模块 [运行中]                                         ||
| |                                                                              ||
| | 命令       writer/data-watch-writer.exe      PID       4932                  ||
| | 工作目录   D:/data_watch_deploy/data-watch-writer                            ||
| | 运行时长   03:41:55                         内存使用  142 MB                 ||
| |                                             CPU 使用率 1.1%                   ||
| |                                                       [启动] [停止] [重启] [打开日志] |
| +------------------------------------------------------------------------------+|
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
| |  16k | - - - - - - - - - -                  | |                              | |
| |  12k | ~~~ blue line ~~~~~                  | |                              | |
| |   8k | - - - - - - - - - -                  | |                              | |
| |   0  +---------------------                  | |                              | |
| +---------------------------------------------+ +------------------------------+ |
|                                                                                |
+--------------------------------------------------------------------------------+
```

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

## 11. 交互说明

### 11.1 模块控制按钮

```text
启动:
  - 如果模块未运行，启动对应 exe。
  - 启动时设置 working directory。
  - 捕获 stdout / stderr 并写入实时日志面板。

停止:
  - 优先温和停止进程。
  - 超时后可提示是否强制结束。

重启:
  - 先停止，再启动。
  - 用于配置变更后生效。

日志:
  - 总览页点击日志按钮跳转到对应模块详情页，或打开日志面板。
```

### 11.2 连接状态

```text
green dot:
  connected

gray dot:
  unknown or not checked

red dot:
  disconnected
```

### 11.3 设置保存

```text
保存:
  - 仅写配置文件。
  - 不重启进程。

保存并重启 Collector:
  - 写配置文件。
  - 如果 collector 正在运行，执行重启。

保存并重启 Writer:
  - 写配置文件。
  - 如果 writer 正在运行，执行重启。
```

## 12. 推荐实现映射

如果使用 PySide6 / Qt Widgets：

```text
MainWindow
  QMainWindow
  central_widget
    AppTitleBar
    TopNavigationBar
    QStackedWidget
      OverviewPage
      CollectorPage
      WriterPage
      SettingsPage

ProcessManager
  QProcess for collector
  QProcess for writer
  start / stop / restart
  stdout / stderr capture

ConfigManager
  read / write toml
  collector config path
  writer config path

LogModel
  append log rows
  filter by level
  auto scroll

MetricsProvider
  process cpu / memory / pid / uptime
  Redis health
  MySQL health
  TDengine health
  trend series
```

页面组件建议：

```text
SegmentedNav           top tabs and settings tabs
ModuleSummaryCard      overview module card
ModuleHeaderCard       collector/writer detail header
MetricCard             small metrics
TrendChartCard         line chart
TaskTable              collector task table
StreamTable            writer stream table
LogPanel               realtime logs
SettingsRow            one-line config card
StatusPill             running / connected state
ActionButton           start / stop / restart / logs
```

## 13. 开发注意事项

- 普通页面和设置页面的顶部导航不同，不能混用。
- 设置页不能显示 Redis / MySQL / TDengine 顶部连接状态。
- 概览页不显示日志。
- Collector / Writer 详情页才显示实时日志。
- 设置页数据库项必须是一行一个卡片。
- 启动模块时必须设置工作目录，否则 `config/settings.toml` 可能无法找到。
- Writer 和 Collector 必须指向同一个 Redis 实例。
- TDengine stable pattern 使用 `sd{type}`。
- 密码输入框默认掩码，不在日志中打印明文。
- 图表只做轻量趋势，不做复杂 BI 大屏。
