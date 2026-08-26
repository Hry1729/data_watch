# 运维部署
<!-- xiaohu-illustration:ops-deploy-01-crate -->
![小狐配图：发布包木箱](../images/xiaohu-ops-deploy-01-crate.png)

> 本文覆盖平台的运行环境、开发启动、中心镜像部署、发版打包、离线镜像、升级回滚与验收。DataWatch CLI 的独立 Windows ZIP、安装、自更新和回滚见 [CLI安装与更新](CLI安装与更新.md)。三个固定角色安装包的最新构建与隔离契约见 [三角色安装包构建与隔离](三角色安装包构建与隔离.md)，交付验收见 [三角色安装包验收清单](三角色安装包验收清单.md)，Ingest 上线门禁见 [Ingest容量压测](Ingest容量压测.md)，问题排查见 [问题排查](问题排查.md)。命令以仓库脚本和当前部署文件为准。
>
> Windows 交付固定为三种角色包：中心式、分布式边缘、分布式中心。三个 EXE 只包含对应角色的 Windows 组件；Backend/Frontend 与 MySQL/Redis/TDengine 走独立 Docker 部署，不进入 Windows 安装包。边缘角色使用 Edge Agent + Storage + Collector + SQLite outbox + Uploader，不包含 Writer 或中心数据库。

---

## 1. 部署形态总览

```text
中心侧（Docker / 现场已有服务）：
  MySQL        业务配置库 device_watch（可外置，不一定由 compose 托管）
  Redis        实时缓冲层 latest/window/stream/dlq
  TDengine     时序历史库 device_data
  backend      Python 3.14t + uvicorn 镜像
  frontend     Nginx 静态资源 + /api、WS 与 /ingest 统一入口镜像

Windows 角色安装包：
  中心式                    App + Agent(:7070) + Collector + Writer
  分布式边缘                Edge App + Edge Agent(:7071) + Storage(:7073) + Edge Collector + Uploader
  分布式中心                Center App + Agent(:7072) + Ingest + Writer
OPC DA proxy             仅随包含 Collector 的角色安装

独立工具包：
  DataWatch CLI            Windows x64 one-folder ZIP，不进入三角色安装包，目标机无需 Python
```

采集模块涉及 OPC DA、DCOM、PLC 网络和现场驱动环境，**不放进 Docker**；中心业务服务与 Web 前端支持镜像部署。

---

## 2. 运行环境

| 组件 | 运行时 / 依赖 |
|------|---------------|
| backend | free-threaded Python 3.14t，uvicorn，`backend/.env` 或 `deploy/backend.env` |
| frontend | Node 22 构建，nginx 1.27-alpine 运行 |
| CLI | Python 3.11+ 源码开发；PyInstaller Windows x64 one-folder 包用于目标机，目标机无需 Python |
| collector / storage / uploader / ingest / writer | free-threaded Python 3.14t，PyInstaller 打包 exe，按 Windows 角色运行 |
| agent | Python/FastAPI 打包 exe，NSSM Windows 服务 |
| app | Tauri + React + Rust + WebView2 |
| OPC DA proxy | .NET Framework 4.8 / x86，Windows COM/DCOM |
| 数据面 | MySQL、Redis、TDengine 3.4.0.9 |
| 打包环境 | 各模块 `.venv-pack`，Inno Setup 6，Node/pnpm，Rust，Docker |

---

## 3. 本地开发启动

### 3.1 backend + frontend

项目根目录：

```powershell
.\start-dev.ps1
.\start-dev.ps1 -Force
```

该脚本只启动：

| 服务 | 地址 |
|------|------|
| backend | `http://127.0.0.1:8000` |
| frontend dev server | `http://127.0.0.1:5173` |

它不会启动 agent、collector、writer、app。

Vite 开发服务只代理 `/api/* -> 127.0.0.1:8000`，当前不代理 `/ingest`。Docker Frontend/nginx 的 8080 端口才是分布式边缘使用的统一入口，同时承载 Web、`/api`、WebSocket 和 `/ingest`。因此开发页面可以使用 5173，但边缘 `center_url` 应继续指向 8080，并保持 `data-watch-frontend` 容器运行；关闭该容器会使所有边缘心跳和 Uploader 上传一并离线。

### 3.2 collector / writer

本地源码调试可用各自 `.venv-pack` 运行。生产/边缘部署应由 agent 启动，不要手工起同名进程，避免重复消费 Redis 或重复写 TDengine。

collector 支持配置预检：

```powershell
cd D:\python\device_alarm\data_watch\collector
.\.venv-pack\Scripts\python.exe main.py --dry-run
```

writer 会从 MySQL 动态发现 `(device_type, group_code)`，不再维护固定 `device_types/group_codes/stable_names`。

### 3.3 app / agent

开发 app 的顺序：

```text
backend(:8000) -> agent(:7070) -> app pnpm tauri dev
```

app 登录需要 agent token 与 backend token 都可获取。backend CORS 开发白名单需包含 Tauri dev 源 `http://localhost:1420` / `http://127.0.0.1:1420`。

---

## 4. 导出中心部署包

本章在开发机或打包机执行，只负责构建、导出和组装发布目录；目标机部署步骤见第 5 章。

### 4.1 当前镜像与部署文件

| 文件 | 作用 |
|------|------|
| `deploy/python314t.Dockerfile` | 构建 `data-watch-python314t:latest` 基础镜像，编译 Python 3.14.4 `--disable-gil` |
| `backend/Dockerfile` | 构建 `data-watch-backend:latest`，复制 TDengine `libtaos.so`，运行 uvicorn |
| `frontend/Dockerfile` | Node 22 构建 Vite，nginx 托管静态文件 |
| `deploy/docker-compose.yml` | 定义 redis、tdengine、backend、frontend 服务 |
| `deploy/nginx.conf` | frontend 容器内 nginx，反代 `/api/`、WebSocket 与宿主机 Ingest `/ingest` |
| `deploy/backend.env.example` | backend 容器环境变量模板 |
| `deploy/taos.cfg` | TDengine 服务配置 |
| `deploy/redis.conf` | Redis 服务配置：内存保险丝（maxmemory）+ 持久化策略，由 compose 挂载，容器重建可复现 |
| `deploy/stack-up.ps1` | 一键从 tar 载入镜像并拉起中心全栈（redis + tdengine + backend + frontend） |

`deploy/docker-compose.yml` 当前服务：

| 服务 | 镜像 | 端口 | 说明 |
|------|------|------|------|
| redis | `redis:latest` | `6379:6379` | profile `infra`，可选由 compose 托管 |
| tdengine | `tdengine/tsdb-amd64:3.4.0.9` | `6030`、`6041`、`6043`、`6062->6060` | profile `infra`，固定 `hostname`/`TAOS_FQDN=tdengine`（否则容器内解析不了自身 FQDN 会启动即崩），挂载 `taos.cfg` 与日志卷 |
| backend | `data-watch-backend:latest` | `8000:8000` | 读取 `deploy/backend.env` |
| frontend | `data-watch-frontend:latest` | `8080:80` | nginx 静态资源 + API/WS 代理 |

MySQL 当前不在 compose 中，需使用宿主机或外部 MySQL。

### 4.2 构建中心镜像

第一次或 Python 基础镜像变化时：

```powershell
cd D:\python\device_alarm\data_watch
docker build -t data-watch-python314t:latest -f .\deploy\python314t.Dockerfile .
```

构建 backend/frontend：

```powershell
docker compose -f .\deploy\docker-compose.yml build backend frontend
```

backend 镜像要点：

- 基于 `data-watch-python314t:latest`；
- 从 TDengine 镜像复制 `libtaos.so`；
- Docker 构建中排除 `perspective-python` 与 `taos-ws-py`；
- 以 `python3.14t -m uvicorn main:app --host 0.0.0.0 --port 8000` 启动。

frontend 镜像要点：

- Node 22 alpine 构建；
- pnpm 安装依赖并 `pnpm build`；
- nginx 运行静态资源；
- 使用 `deploy/nginx.conf` 反代 API 和 WebSocket。

### 4.3 导出应用镜像

中心 Docker 镜像与 Windows 三角色安装包独立发布。离线场景把预构建的运行镜像导出到发布目录 `docker-images\`；文件名不写 Docker tag 里的 `:latest`，统一使用固定 tar 文件名。`data-watch-python314t:latest` 只在打包机构建 Backend 时使用，不导出到目标机，也不进入 Windows 安装包。

```powershell
$ReleaseRoot = "D:\data_watch_release"
New-Item -ItemType Directory -Force "$ReleaseRoot\docker-images" | Out-Null


docker save data-watch-backend:latest    -o "$ReleaseRoot\docker-images\data-watch-backend.tar"
docker save data-watch-frontend:latest   -o "$ReleaseRoot\docker-images\data-watch-frontend.tar"
```

### 4.4 导出基础设施镜像

仅当目标机没有可用的 Redis/TDengine，且准备由 compose 托管两库时，才需要带这两个 tar。输出目录仍是 `docker-images\`，文件名固定为 `redis.tar` 和 `tdengine.tar`，不要写成 `redis:latest.tar`。

```powershell
New-Item -ItemType Directory -Force "$ReleaseRoot\docker-images" | Out-Null
docker pull redis:latest
docker pull tdengine/tsdb-amd64:3.4.0.9
docker save redis:latest                 -o "$ReleaseRoot\docker-images\redis.tar"
docker save tdengine/tsdb-amd64:3.4.0.9  -o "$ReleaseRoot\docker-images\tdengine.tar"
```

> 仓库内已有的 `deploy\data-watch-images.zip` 是这两个镜像的打包版，与上面单独导出的 tar 二选一；用 zip 时解压到 `docker-images\` 即可。

### 4.5 复制部署配置到发布目录

镜像只是程序，目标机还要 `deploy\` 里的配置文件和脚本才能启动。这一步在仓库根目录 `D:\python\device_alarm\data_watch` 执行：

```powershell
New-Item -ItemType Directory -Force "$ReleaseRoot\deploy" | Out-Null
Copy-Item ".\deploy\docker-compose.yml"     "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\taos.cfg"               "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\nginx.conf"             "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\backend.env.example"    "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\python314t.Dockerfile"  "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\redis.conf"             "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\stack-up.ps1"           "$ReleaseRoot\deploy\" -Force
```

> `backend.env` 不拷贝：它是目标机真实配置文件，应由目标机从 `backend.env.example` 复制后填写。

发布目录结构：

```text
D:\data_watch_release\
├─ DataWatch-Setup-<版本>.exe
├─ docker-images\
│  ├─ data-watch-backend.tar
│  ├─ data-watch-frontend.tar

│  ├─ redis.tar
│  └─ tdengine.tar
└─ deploy\
   ├─ docker-compose.yml
   ├─ taos.cfg
   ├─ nginx.conf
   ├─ backend.env.example
   ├─ python314t.Dockerfile
   ├─ redis.conf
   └─ stack-up.ps1
```

镜像只包含程序环境，不包含 MySQL/Redis/TDengine 业务数据；整个 `docker-images\` 目录独立于三个 Windows 安装包。

---

## 5. 目标机中心部署
<!-- xiaohu-illustration:ops-deploy-02-stack -->
![小狐配图：目标机叠叠乐](../images/xiaohu-ops-deploy-02-stack.png)

本章在目标机执行。先判断 Redis/TDengine 是否由 compose 托管，再选择启动方式。

### 5.1 部署方式选择

| 场景 | 使用方式 | 说明 |
|------|----------|------|
| 目标机已有 Redis/TDengine | 只启动 backend/frontend | 手工加载 backend/frontend 镜像后执行 `docker compose up -d` |
| compose 托管 Redis/TDengine | 启动中心全栈 | 执行 `stack-up.ps1`，脚本自动加载 tar 镜像并拉起 redis、tdengine、backend、frontend |

MySQL 不由当前 compose 托管，必须使用宿主机、外部服务器或现场已有 MySQL。

### 5.2 配置 backend.env

部署前复制模板：

```powershell
cd D:\data_watch_release\deploy
Copy-Item .\backend.env.example .\backend.env
notepad .\backend.env
```

关键字段使用占位示例：

```env
PYTHONUTF8=1
BACKEND_HOST=0.0.0.0
BACKEND_PORT=8000
BACKEND_LOG_LEVEL=INFO
BACKEND_CORS_ORIGINS=http://localhost:8080,http://127.0.0.1:8080,http://localhost:1420,http://127.0.0.1:1420

MYSQL_HOST=host.docker.internal
MYSQL_PORT=3306
MYSQL_USER=<mysql-user>
MYSQL_PASSWORD=<mysql-password>
MYSQL_DATABASE=device_watch

TDENGINE_HOST=tdengine
TDENGINE_PORT=6041
TDENGINE_USER=<tdengine-user>
TDENGINE_PASSWORD=<tdengine-password>
TDENGINE_DATABASE=device_data
TDENGINE_LOG_DIR=/tdengine-logs

REDIS_HOST=redis
REDIS_PORT=6379
REDIS_DB=0
REDIS_PASSWORD=

AUTH_SECRET_KEY=<generate-a-strong-random-secret>
PERSPECTIVE_ENABLED=true

# Agent 服务凭据（Backend 调用 Agent 装卸驱动时使用）
DATAWATCH_AGENT_SERVICE_TOKEN=<generate-a-strong-random-token>
DATAWATCH_AGENT_BASE_URL=http://host.docker.internal:7070
```

说明：

- MySQL 可为宿主机、外部服务器或其他容器；compose 当前不托管 MySQL。
- 若 Redis/TDengine 由 compose 托管，`REDIS_HOST=redis`、`TDENGINE_HOST=tdengine`。
- 若 Redis/TDengine 是宿主机或外部服务，把 host 改为 `host.docker.internal` 或实际 IP。
- `AUTH_SECRET_KEY` 生产必须强随机，不能使用模板值。
- `TDENGINE_LOG_DIR=/tdengine-logs` 只有 compose 托管 TDengine 并挂载日志卷时可用；外部 TDengine 会优雅降级。
- **`DATAWATCH_AGENT_SERVICE_TOKEN`（新增）**: Backend 调用 Agent 装卸驱动时的服务凭据，必须与 Agent 侧配置的 `DATAWATCH_AGENT_SERVICE_TOKEN` 保持一致。生成方式：`python -c "import secrets; print(secrets.token_hex(32))"`。
- **`DATAWATCH_AGENT_BASE_URL`（新增）**: Agent 地址。Docker 部署 Backend 时，Agent 在宿主机使用 `http://host.docker.internal:7070`；多节点部署时改为 Agent 实际 IP。

### 5.3 启动中心服务

只启动 backend/frontend：

```powershell
cd D:\data_watch_release\docker-images
docker load -i .\data-watch-backend.tar
docker load -i .\data-watch-frontend.tar

cd D:\data_watch_release\deploy
docker compose -f .\docker-compose.yml up -d
```

compose 托管 Redis/TDengine，并拉起完整中心栈：

```powershell
cd D:\data_watch_release\deploy
powershell.exe -ExecutionPolicy Bypass -File .\stack-up.ps1
```

> 目标机离线且没有 PowerShell 7 时，用 Windows 自带的 `powershell.exe` 即可，不需要安装 `pwsh`。如果现场已经安装 PowerShell 7，也可以使用 `pwsh -File .\stack-up.ps1`。

`stack-up.ps1` 会逐个检查并加载这 4 个运行镜像 tar（路径统一在 `docker-images\` 下，固定文件名，缺任一即报错）：

- `..\docker-images\redis.tar`
- `..\docker-images\tdengine.tar`

- `..\docker-images\data-watch-backend.tar`
- `..\docker-images\data-watch-frontend.tar`

脚本最后执行 `docker compose -f .\docker-compose.yml --profile infra up -d --pull never`，因此会同时拉起 redis、tdengine、backend、frontend。

> 目标机使用预构建的 Backend/Frontend 运行镜像，不在现场重建。`stack-up.ps1` 在启动前强制检查并加载四个 tar；镜像缺失或标签不符时应重新生成 Docker 发布包，而不是把构建基础镜像塞进 Windows 安装器。

### 5.4 Redis 数据与 stream 容量

重新部署 Redis 前若要清空旧数据，必须连命名卷一起删。否则旧 `dump.rdb` 可能被重新加载，一旦超过 `redis.conf` 的 `maxmemory`，noeviction 会全面拒写：

```powershell
docker rm -f data-watch-redis
docker volume ls
docker volume rm data_watch_redis_data   # 项目名 data_watch，卷名带此前缀
```

独立离线资源目录中的 `docker-images\redis.tar` 版本以 Docker 发布包为准；`redis:latest` 是浮动标签，重新拉取不保证版本一致。`redis.conf` 的 `maxmemory 3gb` 是 12G 虚机下的内存保险丝，撞顶会拒写而非把 TDengine 一起拖垮。

**关于 `stream_maxlen`（stream 容量上限）**

正常消费时，Writer 对已落 TDengine 或已成功进入 DLQ 的终态条目执行 `XACK + XDEL`；`stream_maxlen` 是 Writer 停止、持续滞后或删除暂时失败时的应急上限，仓库默认 `50000/shard`：

- **最坏内存预算**：Writer 无法推进时，`50000 × ~9KB ≈ 450MB/shard`；`shard_count=4` 时单个 `(device_type, group)` 组合约 1.8GB。此预算假设只有一个同量级 stream 组合，组合数增多需按 `maxmemory 3gb` 重算（如 2 组同量级 ≈ 3.6GB，会超保险丝）。
- **缓冲窗口变短（重要）**：旧值 500000 名义“3 分钟容量”，但在本机内存放不下、必然 OOM；降到 50000 后，writer 掉线/滞后时每 shard 仅缓冲约几十秒~1 分钟，超出的旧数据会被 MAXLEN 裁掉丢失。本质是用“短缓冲 + 不崩”换“长缓冲 + 崩”。
- **已部署机器生效路径**：中心式由 Collector 的 `stream_maxlen` 生效；分布式中心由 `INGEST_STREAM_MAXLEN` 生效。修改目标机实际配置后必须重启对应 Collector 或 Ingest，只改仓库不会让已安装实例生效。
- **升级前存量**：新版 Writer 只会自动 XDEL 它实际完成终态确认的条目。升级前已经 ACK 的旧 entry 仍可能保留；测试环境需要先停止 Collector/Uploader/Ingest 等写入源，让 Writer 追平后停止 Writer，确认所有消费组 `pending=0` 且 `lag=0`，再对目标 Stream 执行 `XTRIM <key> MAXLEN 0`。正式环境执行前必须备份并逐 Stream 核对，禁止直接 `FLUSHDB`。

### 5.5 查看和访问

查看服务：

```powershell
docker compose -f .\docker-compose.yml ps
docker logs -f data-watch-backend
docker logs -f data-watch-frontend
```

访问地址：

```text
Web 前端: http://127.0.0.1:8080
后端 API: http://127.0.0.1:8080/api/...   # 经 nginx
后端直连: http://127.0.0.1:8000
```

### 5.6 nginx 代理规则

frontend 容器内 nginx 负责：

- `/api/` → `http://backend:8000/api/`；
- `/api/device-monitor/ws/` → backend WebSocket，并保留 Upgrade/Connection；
- `/ingest` → 宿主机 `Ingest :8010`；
- `/healthz` → backend healthz；
- 其他路径回退 `index.html`，支持前端路由刷新。

该 nginx 是当前分布式统一入口，不只是静态前端。生产环境必须独立监控容器状态、8080、`/api` 和一次签名 `/ingest` 请求；不要因本地使用 Vite 开发页面而停止它。

---

## 6. 数据库初始化责任

| 库 | 谁初始化 | 内容 | 部署要求 |
|----|----------|------|----------|
| MySQL `device_watch` | backend | SQLAlchemy 建表，播种超管、菜单、权限 | backend 必须先成功连接一次目标库 |
| TDengine `device_data` | writer | `CREATE DATABASE IF NOT EXISTS`，按需建 stable/child/columns | 可空库，writer 首次写入自建 |
| Redis | 无 schema | latest/window/stream/dlq 运行时产生 | 只需服务可用 |

注意：collector/writer 不负责初始化 MySQL。目标库为空且 backend 未初始化时，app 登录会失败，collector 也读不到设备拓扑。

---

## 7. Windows 角色包部署

### 7.1 安装包内容

三个安装包的组件边界见 [三角色安装包构建与隔离](三角色安装包构建与隔离.md)。中心式包包含 App、Agent、Collector、Writer 和 OPC DA proxy；分布式边缘包包含 Edge App、Edge Agent、Storage、Edge Collector、SQLite outbox、Uploader 和 OPC DA proxy；分布式中心包包含 Center App、Agent、Ingest 和 Writer。Backend、Frontend、MySQL、Redis、TDengine 及所有 Docker tar 均不进入这三个 EXE。

### 7.2 双根目录（中心式示例）

```text
C:\Program Files\DataWatch\          # DATAWATCH_INSTALL_ROOT，只读安装根
  app\
  agent\
  collector\
  writer\
  sidecars\opcda_proxy_service\bin\
  tools\nssm.exe
  templates\

C:\ProgramData\DataWatch\            # DATAWATCH_DATA_ROOT，可写数据根
  config.toml
  collector\config\settings.toml
  writer\config\settings.toml
  runtime\
    logs\
    pids\
    opcda_proxy\
```

安装器/NSSM 注入：

```text
DATAWATCH_INSTALL_ROOT
DATAWATCH_DATA_ROOT
```

各角色的 agent 和本角色子进程实际路径由双根动态派生，不在 config.toml 固定绝对路径；边缘与分布式中心使用各自独立的安装目录、ProgramData、服务名和 Agent 端口。

### 7.3 中心式 bootstrap

首次打开 app：

```text
1. app 连接 agent /bootstrap/status。
2. bootstrap_done=false -> 进入引导页。
3. 填写 MySQL、Redis、TDengine、backend 地址等连接信息。
4. agent 用 DPAPI 保存密码到 config.toml。
5. /bootstrap/test-connection 验证连通。
6. /bootstrap/finish 标记完成。
7. 登录并进入运维页面。
```

密码不要手工写入 config.toml；DPAPI 密文不可跨机器复制。

**Agent 服务凭据配置**：安装器生成 `DATAWATCH_AGENT_SERVICE_TOKEN`，写入角色数据目录下的 `agent_service_token.txt`，并通过 NSSM 环境变量注入 Agent。Backend 需要调用中心本机 Agent 时，必须把同一值配置到独立 Docker 部署的 `backend.env`：中心式读取 `C:\ProgramData\DataWatch\agent_service_token.txt`，分布式中心读取 `C:\ProgramData\DataWatch Distributed Center\agent_service_token.txt`；`DATAWATCH_AGENT_BASE_URL` 分别使用宿主机端口 7070 或 7072。

### 7.4 中心式 collector / writer 启停

```text
app -> agent admin API -> ProcessManager -> collector/writer exe
```

agent 启动子进程时：

- 解密 DB 密码；
- 注入 `DATAWATCH_<DB>_<FIELD>`；
- 设置工作目录；
- 拼接 `--config`、`--runtime-dir`、`--port-mapping` 等运行参数；
- stdout/stderr 写入 runtime logs；
- PID 文件包含 pid + create_time。

停止时先写 `runtime/<name>.shutdown_flag`，超时后才强杀。

### 7.5 Collector 角色的 OPC DA proxy 验收

有 OPC DA center 设备时，agent 启动 collector 前必须：

1. 从 MySQL 检测 `opcda_center` endpoint；
2. 启动或认领对应 proxy；
3. 生成 `runtime\opcda_proxy\port_mapping.json`；
4. 检查端口可连、进程归属正确、proxy 存活；
5. 再启动 collector。

现场验收不能只看 proxy 进程和端口，要确认服务账户具备 DCOM/OPCEnum/OPC Server 权限，并真实读到点位。

### 7.6 分布式边缘启动与数据路径

```text
Edge Agent(:7071)
  -> Storage(:7073) ready
  -> Edge Collector
  -> Uploader
```

Collector 和 Uploader 不直接执行 SQLite 写事务。Collector 将采集帧提交给 Storage；Uploader 经 Storage 领取 outbox 批次并提交 ACK/重试/永久失败状态。Storage 停止时，Agent 应先停止依赖模块；启动 Collector 或 Uploader 时应先恢复 Storage 并等待 `/healthz` 成功。

本机检查：

```powershell
Invoke-RestMethod http://127.0.0.1:7073/healthz
Invoke-RestMethod http://127.0.0.1:7073/v1/status
Get-NetTCPConnection -LocalPort 7071,7073 -State Listen
```

---

## 8. 发版打包

### 8.1 Windows 三角色安装包

```powershell
cd D:\python\device_alarm\data_watch
pwsh -File installer\build.ps1
pwsh -File installer\build.ps1 -Role edge
pwsh -File installer\build.ps1 -Role distributed-center -Iscc "D:\path\to\ISCC.exe"
```

`installer\build.ps1` 默认依次生成三个角色包；-Role 可只构建一个角色。详细组件矩阵、隔离值和 Docker 分轨发布策略见 [三角色安装包构建与隔离](三角色安装包构建与隔离.md)。

角色构建按目标裁剪组件，主要产物如下：

| 阶段 | 动作 | 产物 |
|------|------|------|
| 1 | app：`pnpm tauri build` | Tauri app exe/resources |
| 2 | agent：PyInstaller | `agent\dist\data-watch-agent\` |
| 3 | Python 模块：按角色构建 Collector/Storage/Uploader/Ingest/Writer | 各模块 `dist\` 目录 |
| 4 | OPC DA proxy：仅包含 Collector 的角色执行 .NET 4.8 x86 build | `OpcDaProxyService.exe` |
| 5 | Inno ISCC | `installer\output\DataWatch-<Role>-Setup-<版本>.exe` |

### 8.2 Docker 镜像

`installer\build.ps1` 只构建 Windows 三角色安装包，不调用 Docker，也不读取 `deploy\images`。Backend/Frontend 镜像按第 4 节单独构建与发布，目标机部署按第 5 节执行。

---

## 9. 更新与回滚

### 9.1 更新 backend/frontend 镜像

```powershell
cd D:\data_watch_release\deploy
docker compose -f .\docker-compose.yml up -d --build backend frontend
```

离线目标机：开发机重新 `docker save` → 拷贝 tar → 目标机 `docker load` → `up -d`。

修改 `backend.env` 后无需 rebuild，但要重建容器使环境变量生效：

```powershell
docker compose -f .\docker-compose.yml up -d --force-recreate backend
```

### 9.2 更新 Windows 一体包

1. 开发机重新打包 `installer\build.ps1`；
2. 拷贝新的 `DataWatch-Setup-<版本>.exe` 到目标机；
3. 管理员运行安装器覆盖安装；
4. ProgramData 现场配置保留；
5. 按角色检查 `DataWatchCentralizedAgent`、`DataWatchEdgeAgent` 或 `DataWatchDistributedCenterAgent`，并访问对应端口的 `/bootstrap/status`。

### 9.3 回滚

| 对象 | 回滚方式 |
|------|----------|
| backend/frontend | 保留上一版镜像 tar，`docker load` 后 `docker compose up -d` 指向旧 tag 或重新打 latest |
| backend.env | 恢复备份 env，`up -d --force-recreate backend` |
| Windows 一体包 | 重新运行上一版安装器；ProgramData 配置默认保留 |
| 数据库 | MySQL/TDengine 需依赖部署前备份；镜像回滚不回滚数据 |

不要使用 `docker compose down -v`，`-v` 会删除 compose 管理的数据卷，可能丢 Redis/TDengine 数据。

---

## 10. 端口表

| 端口 | 组件 | 说明 |
|------|------|------|
| 7070 | 中心式 Agent | 本机 `127.0.0.1`，中心式 App 调用 |
| 7071 | 分布式边缘 Agent | 本机 `127.0.0.1`，Edge App 调用 |
| 7072 | 分布式中心 Agent | 本机 `127.0.0.1`，Center App 调用 |
| 7073 | Edge Storage | 分布式边缘本机 `127.0.0.1`，Collector/Uploader/Agent 调用 |
| 8010 | Ingest | 分布式中心宿主机监听，nginx `/ingest/*` 代理目标 |
| 8000 | backend | backend 直连端口 |
| 8080 | frontend nginx | 分布式统一入口，承载 Web、`/api`、WS 与 `/ingest` |
| 5173 | frontend dev | Vite 开发服务 |
| 1420 | app dev | Tauri dev WebView origin |
| 6379 | Redis | 实时缓存/Stream |
| 6030 | TDengine native | TDengine 原生端口 |
| 6041 | TDengine taosAdapter | backend/writer 常用 HTTP/WebSocket 端口 |
| 6043 | TDengine | 组件端口 |
| 6062->6060 | TDengine | compose 映射端口 |

---

## 11. 数据迁移与备份

镜像包不包含业务数据。迁移时分别处理：

| 数据 | 迁移方式 |
|------|----------|
| MySQL `device_watch` | mysqldump / 恢复，包含用户、菜单、设备点位配置 |
| TDengine `device_data` | TDengine 官方导出/数据目录/volume 迁移 |
| Redis | 实时缓存通常不迁移；如果 Stream 中还有未落库数据，先停采集并等 writer 消费完成 |
| ProgramData | 角色配置、日志和边缘 `edge.db`/outbox；DPAPI 密文不可跨机解密，迁移后需重新配置密码 |

---

## 12. 验收清单

分布式阶段 1 至阶段 4 的研发范围已经完成，但下列现场检查和《三角色安装包验收清单》仍需在目标环境签署。自动化测试通过不能替代安装、断网、升级、真实设备和容量证据。

### 中心镜像部署

- `docker compose ps` 中 backend/frontend running；
- 访问 `http://127.0.0.1:8080` 正常；
- `/api/auth/login` 可达；
- WebSocket 经 8080 可连接；
- backend 能连 MySQL/Redis/TDengine；
- TDengine 日志页在 compose 托管 TDengine 时可用，外部 TDengine 时优雅提示不可用。

### 分布式边缘部署

- 桌面安装 Edge App，`DataWatchEdgeAgent` 服务监听本机端口 7071；
- 首次引导生成 node_uuid/Ed25519 密钥并完成 center_url、enrollment code 注册；
- Edge App 的本机设备、采集状态、Storage 状态、离线积压通过 localhost Edge Agent API 可查看，中心断网时仍可用；
- App 可管控 Edge Collector、Storage 和 Uploader，安装目录中不包含 Writer、Backend、MySQL、Redis 或 TDengine；
- Collector 采集结果经 Storage 先落 SQLite outbox，Uploader 恢复联网后严格按序追赶；
- Storage `writer_queue_depth` 不持续增长，`errors_total` 无异常增加，`commit_p99_ms` 满足本机采集节拍；
- OPC DA 场景下 proxy 自动启动且真实点位刷新；
- 中心节点页可看到心跳、配置修订、积压、磁盘水位和累计丢弃数。

---

## 相关文档

- [Ingest容量压测](Ingest容量压测.md)
- [系统架构总览](../01-总体设计/系统架构总览.md)
- [架构决策记录](../01-总体设计/架构决策记录.md)
- [agent 边缘进程编排与凭据](../02-模块设计/agent/边缘进程编排与凭据.md)
- [app 控制台页面与双数据源](../02-模块设计/app/控制台页面与双数据源.md)
- [API 契约](../03-数据与契约/契约/API契约.md)
- [WebSocket 协议](../03-数据与契约/契约/WebSocket协议.md)
- [CLI 安装与更新](CLI安装与更新.md)
- [CLI 模块说明](../02-模块设计/cli/README.md)
- [CLI 输出契约](../03-数据与契约/契约/CLI输出契约.md)
