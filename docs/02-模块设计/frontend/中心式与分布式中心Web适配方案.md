# Web 前端中心式与分布式中心适配方案

> 状态：实施方案
> 范围：frontend Web 控制台、backend 能力发现与中心运维代理
> 不包含：分布式边缘桌面 App 页面改造、运行时角色切换开关

---

## 1. 目标与结论

Web 前端继续只维护一个 Vue 代码库和一个 Docker 镜像。页面由 Backend 在部署时固定的中心角色决定：

- centralized：显示中心式 Collector -> Redis -> Writer -> TDengine 的完整页面；
- distributed-center：显示 Uploader -> Ingest -> Redis -> Writer -> TDengine 的中心页面，并增加边缘节点管理；
- 设备、点位、实时、历史、日志、用户、角色、菜单等中心业务页面在两种角色下共用；
- 不提供中心式/分布式运行时切换开关，修改部署角色必须修改服务端部署配置并重启 Backend；
- 权限和部署能力取交集：有权限但当前部署无该模块时，菜单、路由和操作均不可用。

因此不需要分别构建两份 Web 镜像，也不能把角色写死在 Web 构建参数里。角色事实源必须在 Backend。

---

## 2. 当前差距

| 项目 | 当前状态 | 目标状态 |
|---|---|---|
| 部署角色 | Web 无角色概念 | Backend 返回 centralized 或 distributed-center |
| 工作台 | /device-monitor/dashboard 为业务页；/dashboard/console 仍是模板示例 | 工作台与中心控制台均为真实业务页面 |
| 数据采集 | 无独立 Collector/Ingest/Writer 运维页 | 按角色显示采集监控或汇聚监控，并始终显示入库监控 |
| 分布式节点 | Backend 已有接口，Web 无页面 | 分布式中心显示边缘节点管理页 |
| 数据链路健康 | Web 未展示新版六段链路 | 工作台展示 Collector/Uploader/Ingest/Redis/Writer/TDengine |
| 设置 | Web 无中心运行设置页 | 增加中心设置页，区分 Docker 只读配置与 Agent 可管理配置 |
| 运维访问 | 浏览器不能安全访问中心宿主机 Agent | Web 只访问 Backend，由 Backend 使用服务凭据代理 Agent |
| 菜单权限 | RBAC 种子无新增页面/操作权限 | 菜单权限与部署能力双重过滤 |

当前 module-monitor 仅提供 Collector/Writer 的业务只读指标，且没有 Ingest 指标和进程控制代理，不能直接支撑完整中心控制台。

---

## 3. 总体架构

~~~mermaid
flowchart LR
    WEB["同一 Web 镜像"] -->|JWT + REST/WS| BE["Backend"]
    BE --> PROFILE["部署角色与能力"]
    BE --> MYSQL[(MySQL)]
    BE --> REDIS[(Redis)]
    BE --> TD[(TDengine)]
    BE -->|服务凭据，仅内网| AGENT["中心 Agent"]
    AGENT --> C["Collector<br/>仅中心式"]
    AGENT --> I["Ingest<br/>仅分布式中心"]
    AGENT --> W["Writer"]
~~~

约束：

1. 浏览器不得直连 127.0.0.1:7070/7072，因为该地址指向访问者自己的电脑，而不是中心服务器。
2. Backend 代理只允许访问当前角色的受管模块，不接受可执行路径或任意进程名。
3. Backend 的用户 JWT 负责页面权限；Backend 到 Agent 使用独立服务凭据，二者不能混用。
4. Backend 与 Frontend 镜像保持通用，Windows 三角色安装包边界保持不变。

---

## 4. 部署角色与能力协议

### 4.1 配置项

Backend 增加：

~~~env
DATAWATCH_DEPLOYMENT_ROLE=centralized
# centralized 对应中心式 Agent
DATAWATCH_AGENT_BASE_URL=http://host.docker.internal:7070
~~~

分布式中心使用：

~~~env
DATAWATCH_DEPLOYMENT_ROLE=distributed-center
DATAWATCH_AGENT_BASE_URL=http://host.docker.internal:7072
~~~

允许值仅为 centralized、distributed-center。默认值为 centralized，用于兼容已有中心式部署。

### 4.2 能力发现接口

新增鉴权接口：

~~~http
GET /api/device-monitor/system/capabilities
~~~

建议响应：

~~~json
{
  "deploymentRole": "distributed-center",
  "modules": {
    "collector": false,
    "uploader": false,
    "ingest": true,
    "writer": true
  },
  "features": {
    "distributedNodes": true,
    "moduleControl": true,
    "centerSettings": true,
    "pipelineHealth": true
  }
}
~~~

兼容规则：

- 接口返回 200：严格使用服务端能力；
- 旧 Backend 返回 404：仅作为升级兼容，回退到 centralized；
- 网络失败、5xx 或非法角色：不猜测角色，显示“系统能力加载失败”并提供重试；
- 能力只保存在内存，不把角色作为可编辑项写入浏览器存储。

---

## 5. 页面与导航矩阵

### 5.1 目标菜单

| 一级菜单 | 二级页面 | 中心式 | 分布式中心 | 当前 Web |
|---|---|:---:|:---:|---|
| 仪表盘 | 工作台 | 显示 | 显示 | 已有，需升级 |
| 仪表盘 | 控制台 | 显示 | 显示 | 当前为模板页，需重做 |
| 数据采集 | 采集监控 | 显示 | 隐藏 | 缺少独立页面 |
| 数据采集 | 汇聚监控 | 隐藏 | 显示 | 缺少 |
| 数据采集 | 入库监控 | 显示 | 显示 | 缺少独立页面 |
| 设备监控 | 设备通讯 | 显示 | 显示 | 已有 /device-monitor/monitor |
| 设备监控 | 驱动管理 | 显示 | 显示 | 已有 |
| 设备监控 | 设备管理 | 显示 | 显示 | 已有 |
| 设备监控 | 点位管理 | 显示 | 显示 | 已有 |
| 设备监控 | 实时监控 | 显示 | 显示 | 已有 |
| 设备监控 | 历史数据 | 显示 | 显示 | 已有 |
| 分布式 | 边缘节点 | 隐藏 | 显示 | 缺少 |
| 系统管理 | 系统日志 | 显示 | 显示 | 已有 |
| 系统管理 | 数据库日志 | 显示 | 显示 | 已有日志终端，可更名整理 |
| 系统管理 | 用户管理 | 显示 | 显示 | 已有 |
| 系统管理 | 角色管理 | 显示 | 显示 | 已有 |
| 系统管理 | 菜单管理 | 显示 | 显示 | 已有 |
| 系统管理 | 中心设置 | 显示 | 显示 | 缺少 |

Web 不增加边缘本机 Collector/Uploader 控制页。边缘本机运维仍由边缘桌面 App 负责；中心 Web 只展示所有边缘节点的聚合状态、分配和告警。

### 5.2 路由建议

| 页面 | 路由 | 所需能力 |
|---|---|---|
| 工作台 | /dashboard/workbench | dashboard |
| 中心控制台 | /dashboard/console | moduleControl |
| 采集监控 | /collection/collector | collector |
| 汇聚监控 | /collection/ingest | ingest |
| 入库监控 | /collection/writer | writer |
| 边缘节点 | /distributed/nodes | distributedNodes |
| 中心设置 | /system/settings | centerSettings |

现有设备监控路由可先保留兼容重定向，再逐步统一路径，避免用户收藏和动态菜单立即失效。

---

## 6. 页面设计要求

### 6.1 工作台

两种部署共用同一页面骨架：

- 顶部指标：设备总数、在线设备、点位总数、系统告警；
- 组合趋势：采集量面积图、设备在线数柱形图、告警折线图；
- 设备类型分布、设备通讯概览；
- 数据链路健康：Collector、Uploader、Ingest、Redis、Writer、TDengine 六段链路；
- 最近系统日志。

角色差异：

- 中心式：Collector、Redis、Writer、TDengine 为有效阶段；Uploader、Ingest 显示“不适用”，不计入健康结论；
- 分布式中心：Collector/Uploader 聚合所有已审批边缘节点；任一边缘异常为黄色闪烁，全部异常为红色；Ingest、Redis、Writer、TDengine 使用中心状态；
- 不把“不适用”显示成故障。

建议新增统一接口 /api/device-monitor/dashboard/pipeline-health。现有 /edge-pipeline-health 可保留兼容，由新接口在服务端按角色聚合，避免 Web 自行拼接多套口径。

### 6.2 中心控制台

采用桌面 App 控制台的中心版本：

- 中心式：Collector + Writer 两个模块；
- 分布式中心：Ingest + Writer 两个模块；
- 每个模块展示状态、PID、运行时长、CPU、内存、重启次数和速率趋势；
- 管理员可启动、停止、重启；普通监控用户只读；
- 操作按钮必须有执行中、成功、失败和冲突状态，禁止连续重复提交。

### 6.3 采集、汇聚与入库监控

三个页面共用统一监控布局组件：

- 顶部 4 到 5 个关键指标卡；
- 左下为速率/积压/错误趋势；
- 右下为模块日志；
- Collector 增加设备任务、点位数、周期和最后采集时间；
- Ingest 增加请求率、确认率、重复批次、拒绝批次、活跃边缘和延迟；
- Writer 增加 Stream lag、PEL、DLQ、写入速率和 TDengine 错误。

业务指标继续来自 Backend/Redis/MySQL；进程指标和原始模块日志经 Backend 代理 Agent。

### 6.4 边缘节点管理

复用桌面 App 已验证的交互和 Backend 已有接口，至少包含：

- 节点列表：审批状态、在线状态、Collector/Uploader 状态、配置修订、积压水位、最后心跳；
- 注册码生成、审批/停用、密钥轮换；
- 设备分配：已停用设备灰显但保留分配；已分配给其他节点的设备灰显；按设备分类筛选；
- 同步设备启停状态，不删除已分配设备；
- 节点详情中的异常、积压和配置不同步提示。

### 6.5 中心设置

设置页必须区分两类配置：

1. Docker 服务配置：Backend/MySQL/Redis/TDengine 的地址和环境变量只读展示，修改方式链接到部署说明；
2. Windows 中心模块配置：由 Backend 代理中心 Agent 读取和保存。

按角色显示：

- 中心式：连接状态、Collector 设置、Writer 设置；
- 分布式中心：连接状态、Ingest 设置、Writer 设置、边缘策略；
- 保存按钮只作用于当前页实际修改的模块；重启按钮只在配置需要重启对应模块时出现；
- Web 不提供部署角色切换控件。

---

## 7. Backend 与 Agent 接口改造

### 7.1 Backend 运维代理

新增 Backend 运维域，建议前缀：

~~~text
GET  /api/operations/modules
GET  /api/operations/modules/{name}/status
GET  /api/operations/modules/{name}/metrics
POST /api/operations/modules/{name}/start
POST /api/operations/modules/{name}/stop
POST /api/operations/modules/{name}/restart
GET  /api/operations/health
GET  /api/operations/config
PUT  /api/operations/config
WS   /api/operations/modules/{name}/logs
~~~

实现约束：

- Backend 校验用户权限后，再使用 DATAWATCH_AGENT_SERVICE_TOKEN 调用 Agent；
- Agent 增加受限服务端点或扩展现有服务凭据权限，仅允许当前角色的模块白名单；
- 不把 Agent 管理员 JWT、DPAPI 密文或数据库密码返回浏览器；
- 日志代理限制单连接速率、历史行数和并发数；
- 启停/重启写入系统审计日志；
- Backend 和 Agent 都校验角色，前端隐藏菜单不能替代服务端授权。

### 7.2 业务监控接口

保留现有：

- /api/device-monitor/module-monitor/collector
- /api/device-monitor/module-monitor/writer
- /api/device-monitor/module-monitor/rates

补充：

- /api/device-monitor/module-monitor/ingest
- /api/device-monitor/dashboard/pipeline-health

Ingest 指标优先从 Redis/内存采样器读取；需要持久化的趋势再进入统一指标存储，不从日志文本反向解析。

---

## 8. 权限与导航策略

新增页面权限建议：

~~~text
dashboard:workbench:view
operations:console:view
operations:module:view
operations:module:control
distributed:node:view
distributed:node:manage
system:settings:view
system:settings:update
~~~

前端可见条件：

~~~text
visible = route.requiredCapability 满足 AND user.permissions 满足
~~~

规则：

- 无能力：菜单隐藏，直接访问路由跳 404 或工作台；
- 有能力但无权限：菜单隐藏，直接访问返回 403；
- 有查看权限无控制权限：页面可见，启停/保存操作隐藏或禁用；
- Backend API 必须重复执行相同权限和角色校验；
- 菜单管理页不得让管理员把当前部署不支持的页面强行变成可用能力。

Backend 菜单种子新增全部中心页面，Web 在拿到用户菜单后再按部署能力过滤。这样同一套用户、角色和菜单数据可服务两种中心部署。

---

## 9. 前端状态与失败策略

新增 Pinia deployment store：

~~~text
idle -> loading -> ready
                 -> degraded
~~~

状态包含：deploymentRole、capabilities、loadedAt、error。

启动顺序：

1. 登录并获取用户权限；
2. 获取部署能力；
3. 对动态菜单执行能力过滤；
4. 注册可用路由并进入默认页；
5. 默认页始终为 /dashboard/workbench。

失败处理：

- 能力接口失败时不渲染可能错误的运维菜单；
- 已进入页面后能力刷新失败，保留最后一次内存快照并显示顶部降级提示；
- 切换账号必须清空用户权限和能力状态；
- 不用 localStorage 保存部署角色，避免服务端角色变化后展示旧菜单。

---

## 10. 实施阶段

### 阶段 1：能力与路由基座（P0）

1. Backend 增加 DATAWATCH_DEPLOYMENT_ROLE、能力接口和配置校验；
2. Web 增加 deployment store、路由能力元数据和菜单二次过滤；
3. 更新 Backend 菜单/权限种子与数据库迁移；
4. 增加旧 Backend 404 -> centralized 的兼容测试；
5. 更新 Docker 示例环境变量和部署文档。

验收：同一前端镜像连接两种 Backend 配置时，菜单矩阵准确，直接输入不支持的 URL 也不能进入。

### 阶段 2：中心式页面补齐（P0）

1. 将业务工作台统一到 /dashboard/workbench，删除或替换模板控制台内容；
2. 补中心控制台、采集监控、入库监控和中心设置；
3. 建立 Backend -> Agent 受限代理与审计；
4. 接入统一数据链路健康卡片。

验收：中心式部署完整显示 Collector/Writer 页面，不出现 Ingest、边缘节点或 Uploader 运维入口。

### 阶段 3：分布式中心页面补齐（P1）

1. 补汇聚监控及 Ingest 指标接口；
2. 移植边缘节点管理页面并复用现有 /api/distributed/*；
3. 工作台接入边缘 Collector/Uploader 聚合健康；
4. 中心控制台切换为 Ingest + Writer。

验收：分布式中心不显示本机 Collector 页面；节点注册、审批、分配、同步、密钥轮换和状态展示完整可用。

### 阶段 4：一致性与发布（P1）

1. 统一 Web 与桌面 App 的术语、状态色、指标口径和空态；
2. 完成权限矩阵、浏览器响应式、断网与 Agent 不可达测试；
3. 构建一次 Frontend 镜像，在两种中心部署上分别验收；
4. 更新 API 契约、运维部署文档和验收清单。

---

## 11. 建议修改范围

实施时预计涉及：

~~~text
backend/app/core/config.py
backend/app/api/routes/system.py
backend/app/api/routes/module_monitor.py
backend/app/api/routes/operations.py              # 新增
backend/app/services/operations_proxy.py           # 新增
backend/app/services/auth_bootstrap.py
agent/app/routers/service.py                       # 新增或扩展受限服务端点
frontend/src/api/deployment.ts                     # 新增
frontend/src/api/operations.ts                     # 新增
frontend/src/store/modules/deployment.ts           # 新增
frontend/src/router/modules/*.ts
frontend/src/router/guards/beforeEach.ts
frontend/src/views/dashboard/*
frontend/src/views/collection/*                    # 新增
frontend/src/views/distributed/*                   # 新增
frontend/src/views/system/settings/*               # 新增
frontend/src/locales/langs/zh.json
deploy/backend.env.example
deploy/docker-compose.yml
docs/03-数据与契约/契约/API契约.md
~~~

具体提交应按“能力基座 -> 中心式页面 -> 分布式页面”拆分，避免一次提交同时改动全部路由和页面。

---

## 12. 验收清单

### 构建与部署

- [ ] 只构建一份 data-watch-frontend 镜像；
- [ ] centralized 与 distributed-center 使用同一镜像摘要；
- [ ] Backend 启动时拒绝非法部署角色；
- [ ] 页面中不存在运行时部署角色切换开关；
- [ ] Web 请求中不存在浏览器直连 Agent 的地址。

### 中心式

- [ ] 工作台、控制台、采集监控、入库监控和中心设置可见；
- [ ] 汇聚监控和边缘节点不可见且直接 URL 不可访问；
- [ ] 数据链路把 Uploader/Ingest 标为不适用，不产生误告警；
- [ ] Collector/Writer 状态、趋势、日志和授权操作可用。

### 分布式中心

- [ ] 工作台、控制台、汇聚监控、入库监控、边缘节点和中心设置可见；
- [ ] 本机采集监控不可见且直接 URL 不可访问；
- [ ] Collector/Uploader 按边缘节点聚合，部分异常黄、全部异常红；
- [ ] 节点注册、审批、设备分配、状态同步和密钥轮换可用；
- [ ] Ingest/Writer 状态、趋势、日志和授权操作可用。

### 共用业务页

- [ ] 设备通讯、驱动、设备、点位、实时、历史、日志、用户、角色和菜单在两种部署均可用；
- [ ] 页面字段、筛选条件和数据口径在两种部署一致；
- [ ] 用户权限与部署能力取交集；
- [ ] Agent、Redis 或 TDengine 不可达时显示明确降级状态，不把未知误报为健康；
- [ ] 旧 Backend 缺少能力接口时仅回退中心式，其他错误可见且可重试。

---

## 13. 最终边界

- Windows 继续交付中心式、分布式边缘、分布式中心三个固定角色安装包；
- Docker 继续独立部署 Backend、Frontend、MySQL、Redis、TDengine；
- Web 只交付一个镜像，由 Backend 部署角色驱动页面；
- 中心式与分布式中心共享全部中心业务页面，仅替换 Redis 之前的运维页面和增加分布式节点管理；
- 边缘本机离线运维、SQLite outbox 和 Uploader 控制继续属于边缘桌面 App，不进入中心 Web。

