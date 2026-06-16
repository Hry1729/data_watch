# DataWatch 部署与打包文档

更新日期：2026-06-16

本文档是 DataWatch 部署与打包的**唯一权威运维手册**，覆盖从开发机打包发版到目标机部署、验证、运维、排错的完整流程。

部署形态：**Docker 数据面 + Windows 一体安装包**。Windows 侧统一由一键安装器 `DataWatch-Setup-x.y.z.exe` 交付，以 DataWatch Agent 为中枢编排 collector / writer / OPC DA proxy，不再使用手工铺 staging、手动 `cd` 启动、`run.bat` 等旧式部署方式。

> 架构设计依据与 002 特性实现落点见 `specs/002-agent-opcda-proxy/`（spec / plan / tasks / quickstart）。

---

## 一、总览与架构

### 1.1 部署形态

```text
Docker / 现场已有服务（数据面）：
  MySQL        业务配置库（device_watch）
  Redis        实时缓冲层（collector 写 Stream，writer 消费）
  TDengine     时序宽表库（device_data）
  Backend      FastAPI 后端（Python 3.14t free-threading 镜像）
  Frontend     Nginx 静态资源 + /api 反向代理

Windows 一体安装包（采集面）：
  Data Watch Console   桌面 app（Tauri，仅 UI）
  DataWatch Agent      Windows 侧中枢（FastAPI，监听 7070）
  collector            采集进程
  writer               落库进程
  OPC DA proxy         32 位 .NET sidecar（net48/x86）
```

采集模块涉及 OPC DA、DCOM、PLC 网络、现场驱动环境，**不放进 Docker**，统一运行在 Windows 主机。

### 1.2 职责边界

- **app** 只作为 UI，不直接启动 collector / writer / proxy，所有进程操作都走 Agent API。
- **Agent** 是 Windows 侧中枢，负责进程启动、停止、重启、状态、日志、OPC DA proxy 按需编排。
- **collector / writer** 作为 Windows 本机进程由 Agent 拉起，不放进 Docker。
- **OPC DA proxy** 由 Agent 按需差量拉起，不依赖 `collector/run.bat`。
- **MySQL / Redis / TDengine / Backend** 用 Docker 或现场已有服务部署。

### 1.3 数据流

```text
PLC / OPC Server
   └─(OPC DA proxy, 仅中心采集)→ collector
                                    └─写→ Redis Stream
                                              └─consume→ writer ─写→ TDengine 宽表
MySQL  ←配置/拓扑→ collector / writer / backend
Backend ←查询→ Frontend / app（经 Agent 鉴权与编排）
```

### 1.4 凭据单一来源

redis / mysql / tdengine 的连接凭据**唯一来源是 Agent 的 `config.toml`**（DPAPI 加密）。Agent 启动 collector / writer 时解密并经 `DATAWATCH_<DB>_<FIELD>` 环境变量注入覆盖，因此 collector / writer 的 `settings.toml` **不放也不应放凭据**，只保留运行参数。

---

## 二、部署前置条件

目标电脑需要准备：

- Windows 10 / Windows Server 2019 或更高版本。
- **.NET Framework 4.8 运行时**（OPC DA proxy 依赖；Win10/Win11 多数自带）。
- Docker Desktop（Linux containers）或现场已有 MySQL / Redis / TDengine / Backend 服务。
- DataWatch 一体安装包 `DataWatch-Setup-x.y.z.exe`。
- 离线部署时：前后端 Docker 镜像 tar + `deploy/` 部署文件。
- Backend 可访问 MySQL / Redis / TDengine；Agent 可访问 Backend、MySQL、Redis、TDengine。
- 目标 MySQL 必须**已被 backend 初始化**（`sys_user` 表内有账号，默认 `Super/123456`），否则 app 无法登录。
- 如使用 OPC DA 中心采集：
  - 目标 OPC Server 的 `ProgID` 已知（默认 `ZPMC.OPCServer`）、channel 已知（默认 `OPD_Server`）。
  - **Agent Windows 服务账户**具备 DCOM / OPCEnum / OPC Server 访问权限（SC-P06 验收项，见第六节）。
- 浏览器内核（访问 Web 前端时）：Chrome/Edge Chromium ≥ 111、Firefox ≥ 128、Safari ≥ 16.4（Tailwind CSS v4 要求）。

---

## 三、打包发版（开发机）

发版分两条独立轨道：**Windows 一体安装包**（`build.ps1` 一键产出）与 **Docker 镜像**（单独构建导出）。两者最终一起拷给目标机。

项目根：`D:\python\device_alarm\data_watch`。

### 3.1 一键打包 Windows 安装包（推荐主线）

在仓库根执行：

```powershell
cd D:\python\device_alarm\data_watch
pwsh -File installer\build.ps1
```

ISCC 不在默认位置时显式指定：

```powershell
pwsh -File installer\build.ps1 -Iscc "D:\ruanjian\Inno Setup 6\ISCC.exe"
```

脚本按顺序做 6 件事，任一段失败立即中断（`$LASTEXITCODE` fail-fast）：

| 阶段 | 动作 | 产物 |
| --- | --- | --- |
| [0/6] 开工前检查 | 探测 ISCC + 校验外部静态件在位 | 缺失即报错 |
| [1/6] app | `pnpm install` → `pnpm tauri build` | `app\src-tauri\target\release\app.exe` + `resources\` |
| [2/6] agent | `agent\.venv-pack` 下 PyInstaller | `agent\dist\data-watch-agent\` |
| [3/6] collector | `collector\.venv-pack` 下 PyInstaller | `collector\dist\data-watch-collector\` |
| [4/6] writer | `writer\.venv-pack` 下 PyInstaller | `writer\dist\data-watch-writer\` |
| [5/6] proxy | `sidecars\opcda_proxy_service\build.ps1`（csc，net48/x86） | `...\bin\OpcDaProxyService.exe` |
| [6/6] ISCC | 编译 `DataWatch.iss` | `installer\Output\DataWatch-Setup-<版本>.exe` |

跑通后末尾打印产物名、大小、时间，例如：

```text
=== DONE ===
Name          : DataWatch-Setup-0.1.0.exe
SizeMB        : 57.7
LastWriteTime : 2026/6/9 13:06:47
```

版本号取自 `DataWatch.iss` 的 `#define MyAppVersion`，改版本只改 `.iss` 一处，`build.ps1` 自动跟随。

### 3.2 一次性前置条件

`build.ps1` 不负责安装工具链，首次打包前需在本机准备好：

- **Inno Setup 6**（提供 `ISCC.exe`）。
- **Node + pnpm**、**Rust 工具链**（`pnpm tauri build` 需要）。
- **三个打包专用 venv**：`agent\.venv-pack`、`collector\.venv-pack`、`writer\.venv-pack`，各含 PyInstaller 与对应 `requirements.txt`。创建示例（以 collector 为例，agent / writer 同理）：

  ```powershell
  cd D:\python\device_alarm\data_watch\collector
  py -3.14 -m venv .venv-pack
  .\.venv-pack\Scripts\python.exe -m pip install --upgrade pip
  .\.venv-pack\Scripts\python.exe -m pip install pyinstaller
  .\.venv-pack\Scripts\python.exe -m pip install -r requirements.txt
  ```

- **外部静态件**（体积大 / 外部获取，不入库，需预先放好）：
  - `installer\win64\nssm.exe`
  - `installer\webview2\MicrosoftEdgeWebview2Setup.exe`（evergreen 引导器；离线机可换 ~127MB standalone 同名文件）
  - 配置模板：`agent\config.toml.example`、`installer\templates\collector-settings.toml.example`、`installer\templates\writer-settings.toml.example`

缺任何一项，`build.ps1` 在 [0/6] 阶段直接报明缺哪个，不会跑到一半才失败。

### 3.3 只改了某一块时（省时重建）

`build.ps1` 始终全量构建（保证可复现）。改动小、想省时间时可只跑对应阶段，再单独跑 ISCC：

| 改了什么 | 单独重建命令（在对应目录） |
| --- | --- |
| 前端 / Tauri | `cd app; pnpm tauri build` |
| agent Python | `cd agent; .\.venv-pack\Scripts\pyinstaller.exe data-watch-agent.spec --noconfirm` |
| collector Python | `cd collector; .\.venv-pack\Scripts\pyinstaller.exe data-watch-collector.spec --noconfirm` |
| writer Python | `cd writer; .\.venv-pack\Scripts\pyinstaller.exe data-watch-writer.spec --noconfirm` |
| OPC DA proxy（C#） | `cd collector\sidecars\opcda_proxy_service; .\build.ps1` |
| 重新封装安装器 | `& "D:\ruanjian\Inno Setup 6\ISCC.exe" installer\DataWatch.iss` |

> 拿不准依赖关系时，直接跑 `installer\build.ps1` 全量最稳。

### 3.4 Docker 镜像构建与导出

`build.ps1` 只产 Windows 安装包，**不含 Docker 镜像**，前后端镜像需单独构建导出。

第一次需要先构建 Python 3.14t 基础镜像（耗时较长，可能几十分钟）：

```powershell
cd D:\python\device_alarm\data_watch
docker build -t data-watch-python314t:latest -f .\deploy\python314t.Dockerfile .
```

构建前后端镜像：

```powershell
docker compose -f .\deploy\docker-compose.yml build backend frontend
```

导出镜像 tar（建议导到发布目录 `D:\data_watch_release\docker-images`）：

```powershell
$ReleaseRoot = "D:\data_watch_release"
New-Item -ItemType Directory -Force "$ReleaseRoot\docker-images" | Out-Null

docker save data-watch-python314t:latest -o "$ReleaseRoot\docker-images\data-watch-python314t_latest.tar"
docker save data-watch-backend:latest    -o "$ReleaseRoot\docker-images\data-watch-backend_latest.tar"
docker save data-watch-frontend:latest   -o "$ReleaseRoot\docker-images\data-watch-frontend_latest.tar"
```

目标机若还需离线运行 Redis / TDengine（现场没有时），额外导出：

```powershell
docker pull redis:latest
docker pull tdengine/tsdb-amd64:3.4.0.9
docker save redis:latest                 -o "$ReleaseRoot\docker-images\redis_latest.tar"
docker save tdengine/tsdb-amd64:3.4.0.9  -o "$ReleaseRoot\docker-images\tdengine_3.4.0.9.tar"
```

一并复制 Docker 部署文件到发布目录：

```powershell
New-Item -ItemType Directory -Force "$ReleaseRoot\deploy" | Out-Null
Copy-Item ".\deploy\docker-compose.yml"     "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\taos.cfg"               "$ReleaseRoot\deploy\" -Force   # 必带，缺失 TDengine 起不来
Copy-Item ".\deploy\backend.env.example"    "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\python314t.Dockerfile"  "$ReleaseRoot\deploy\" -Force
Copy-Item ".\deploy\nginx.conf"             "$ReleaseRoot\deploy\" -Force
```

### 3.5 发布包产物清单

拷贝到目标机前，确认发布目录包含：

```text
D:\data_watch_release\
├─ DataWatch-Setup-<版本>.exe          ← Windows 一体安装包（installer\Output\ 产出）
├─ docker-images\
│  ├─ data-watch-python314t_latest.tar
│  ├─ data-watch-backend_latest.tar
│  ├─ data-watch-frontend_latest.tar
│  └─（可选）redis_latest.tar / tdengine_3.4.0.9.tar
└─ deploy\
   ├─ docker-compose.yml
   ├─ taos.cfg                          ← compose 挂载，缺失则 TDengine 起不来
   ├─ backend.env.example
   ├─ python314t.Dockerfile
   └─ nginx.conf
```

> Docker 镜像只迁移程序环境，**不含数据库业务数据**；数据迁移见第九节。

---

## 四、目标机部署

部署顺序：先起 Docker 数据面 → 再装 Windows 一体包 → 初始化 Agent → 在 app 内启动 collector / writer。

### 数据库初始化责任（务必先读）

三个库由谁初始化、部署前要不要先做，是硬性依赖，弄反了会出现「登录不进 / collector 空转」：

| 库 | 谁初始化 | 初始化内容 | 部署时要不要先做 |
| --- | --- | --- | --- |
| **MySQL** `device_watch` | **backend** | 启动时 `Base.metadata.create_all` 建**全部表**（sys_user/角色/菜单/权限 + device/device_variable/comm_protocol/device_type），并播种超管 `Super/123456`、菜单、权限 | **必须先让 backend 连一次该库完成建表+播种**；之后设备拓扑再人工录入 |
| **TDengine** `device_data` | **writer** | 连上即 `CREATE DATABASE IF NOT EXISTS device_data KEEP 14d DURATION 1d`，并按需自建超级表 `sd{device_type}`、子表（`device_code`） | 不用管，writer 首次连接/写入时自建 |
| **Redis** | 无 | 无 schema | 不用管 |

要点：

- **collector / writer 都不会初始化 MySQL**：collector 对 MySQL 纯 `SELECT` 读拓扑，writer 只往已存在的 `system_log` 表 `INSERT` 日志，二者均无任何建库建表 DDL。
- **MySQL 必须 backend 先初始化**：若目标库为空且 backend 没连过 → app 登录失败（无 `sys_user`，503/401）、collector 起来也空转（查不到 device 行）。
- **建表 ≠ 有设备数据**：`create_all` 只建空表 + 播种账号/菜单/权限；真正的**设备拓扑（device 行、点位）需在 app/后台录入**，collector 读这些行。
- **TDengine 可给空库**（甚至库不存在也行），writer 自建，无需手动跑 `scripts/init_tdengine_v2.py`（那是显式初始化的备选）。

### 4.1 部署 Docker 数据面

#### 4.1.1 导入镜像（离线）

```powershell
cd D:\data_watch_release\docker-images
docker load -i .\data-watch-python314t_latest.tar
docker load -i .\data-watch-backend_latest.tar
docker load -i .\data-watch-frontend_latest.tar
# 现场无 Redis/TDengine 时再导入：
# docker load -i .\redis_latest.tar
# docker load -i .\tdengine_3.4.0.9.tar
docker images
```

#### 4.1.2 配置 backend.env

```powershell
cd D:\data_watch_release\deploy
Copy-Item .\backend.env.example .\backend.env
notepad .\backend.env
```

重点字段：

```env
MYSQL_HOST=host.docker.internal      # MySQL 在宿主机时用此；外部库填其 IP
MYSQL_PORT=3306
MYSQL_USER=root
MYSQL_PASSWORD=你的 MySQL 密码
MYSQL_DATABASE=device_watch

REDIS_HOST=host.docker.internal      # compose 托管时改为服务名 redis
REDIS_PORT=6379

TDENGINE_HOST=host.docker.internal   # compose 托管时改为服务名 tdengine
TDENGINE_PORT=6041
TDENGINE_USER=root
TDENGINE_PASSWORD=你的 TDengine 密码
TDENGINE_DATABASE=device_data

BACKEND_CORS_ORIGINS=http://localhost:8080,http://127.0.0.1:8080,http://localhost:1420,http://127.0.0.1:1420

AUTH_SECRET_KEY=换成一串足够长的随机密钥
```

> **`AUTH_SECRET_KEY` 必须改**，生成强随机串：
> `& "C:\Users\Hry\.local\bin\python3.14t.exe" -c "import secrets; print(secrets.token_urlsafe(48))"`
> 留作模板占位会导致后端启动 WARNING，且任何人可用公开默认密钥伪造登录 token。`backend.env.example` 仅为模板，必须复制成 `backend.env` 填真实值后容器才会加载。

注意：
- 这里填的是 MySQL / TDengine / Redis 密码，**不是 Windows 登录密码**。
- `BACKEND_CORS_ORIGINS` 是浏览器 / WebView 跨域白名单：8080 对应 Web 前端，**1420 对应 Tauri 桌面 app**。后端精确匹配 Origin，必须含协议+主机+端口，末尾不加 `/`。漏配 1420 会导致后端日志 200 但 app 显示「后端服务不可达」。
- 换 Linux 服务器部署时 `host.docker.internal` 不一定可用，改成数据库服务器 IP 或 Docker 网络服务名。

#### 4.1.3 启动前后端

默认只启动 frontend / backend（不动 Redis / TDengine 数据卷）：

```powershell
cd D:\data_watch_release\deploy
docker compose -f .\docker-compose.yml up -d
```

仅当目标机需要 compose 托管 Redis / TDengine 时（现场没有）才执行：

```powershell
docker compose -f .\docker-compose.yml --profile infra up -d redis tdengine
```

> 日常更新前后端时**不要**加 `--profile infra`，也**不要** `docker compose down -v`，否则可能删除数据库数据卷。

查看与访问：

```powershell
docker compose -f .\docker-compose.yml ps
docker logs -f data-watch-backend
docker logs -f data-watch-frontend
```

```text
前端：   http://127.0.0.1:8080
后端API：http://127.0.0.1:8080/api/...   （经前端 Nginx 反代）
后端直连：http://127.0.0.1:8000
```

#### 4.1.4 TDengine 保留期与磁盘控制（务必确认）

目标：305 设备 × 1000 点 × 100ms、仅变化存储、约 500GB 盘下不写爆磁盘。参数：**KEEP 14d / DURATION 1d / logKeepDays 4 / minimalDataDirGB 50 / sDebugFlag 131**。

- **compose 托管 TDengine 时**：`docker-compose.yml` 已挂 `deploy/taos.cfg`，自动生效 `logKeepDays 4`（日志留 4 天）、`minimalDataDirGB 50`（数据盘剩余 <50GB 停写，最后防线）、`sDebugFlag 131`（同步模块只记 error/warning）。
- **数据保留期 KEEP** 由建库语句控制。首次建库已带 `KEEP 14d DURATION 1d`。**若 `device_data` 库已存在**，`CREATE IF NOT EXISTS` 不改其保留期，需手动让 KEEP 生效（DURATION 建库后不可改）：

  ```powershell
  docker exec data-watch-tdengine taos -s "ALTER DATABASE device_data KEEP 14d"
  ```

- **现场自管 TDengine（不走 compose）时**：上述不会自动应用，需在现场 `taos.cfg` 自行配 `logKeepDays`/`minimalDataDirGB`/`sDebugFlag` 并重启，且对 `device_data` 执行上面的 `ALTER ... KEEP 14d`。

> ⚠️ `minimalDataDirGB` 触发会**停止写入（采集中断）**，是最后防线。正常应靠 KEEP 把磁盘稳定在阈值以下。
> ⚠️ 14d/4d/50GB 是估算初值，上线后建议测 1~2 周实际增速再定稿：`KEEP_天 ≈ 400GB ÷ 实测GB每天`（用 `du -sh /var/lib/taos` 量增速）。

#### 4.1.5 前端「数据库日志」页前置条件

`docker-compose.yml` 已把 TDengine `/var/log/taos` 以命名卷 `tdengine_log` 只读挂到 backend 的 `/tdengine-logs`（`backend.env` 的 `TDENGINE_LOG_DIR` 指向），供前端「日志监控」页**底部**「数据库日志」面板在线查看。

- **仅 compose 托管 TDengine 时可用**；现场自管 / 远程 TDengine 时 backend 读不到，页面优雅提示「当前部署未启用」。
- ⚠️ 命名卷会遮挡旧容器层日志：给已有容器新增该卷并重建后，旧历史日志不可见，需保留先 `docker cp` 备份。

### 4.2 安装 Windows 一体包

#### 4.2.1 运行安装器

```powershell
Start-Process "DataWatch-Setup-0.1.0.exe" -Verb RunAs   # 需管理员/UAC
# 静默安装：追加 /VERYSILENT
```

#### 4.2.2 安装后目录布局（双根契约）

程序文件落 `Program Files`（**只读**），运行配置 / 日志落 `ProgramData`（**可写**）：

```text
C:\Program Files\DataWatch\          ← DATAWATCH_INSTALL_ROOT（只读安装根）
├─ app\                              # app.exe + resources
├─ agent\                            # data-watch-agent.exe + _internal
├─ collector\                        # data-watch-collector.exe + _internal
├─ writer\                           # data-watch-writer.exe + _internal
├─ sidecars\opcda_proxy_service\bin\ # OpcDaProxyService.exe
├─ tools\                            # nssm.exe
└─ templates\                        # 三个 *.example 脱敏配置模板

C:\ProgramData\DataWatch\            ← DATAWATCH_DATA_ROOT（可写数据根）
├─ config.toml                       # Agent 配置（首次安装从模板下发）
├─ collector\config\settings.toml
├─ writer\config\settings.toml
└─ runtime\
   ├─ logs\                          # 含 agent-service.out/err.log、proxy/collector 日志
   ├─ pids\
   └─ opcda_proxy\
      ├─ port_mapping.json
      └─ proxy_9090.json
```

> agent/collector/writer 的可执行路径、工作目录、settings.toml 路径运行期由两根环境变量**自动派生**，**不**在 config.toml 写绝对路径。

#### 4.2.3 安装器做了什么（DataWatch.iss）

1. 铺所有程序文件到 `Program Files\DataWatch\`。
2. 建 `ProgramData\DataWatch\` 下各运行目录。
3. **下发默认配置**（`ProvisionConfigs`，仅当目标不存在时拷，不覆盖现场已填配置）：`*.example` → `config.toml` / `collector\config\settings.toml` / `writer\config\settings.toml`。
4. **注册 Windows 服务 `DataWatchAgent`**（NSSM）：先 stop/remove 同名旧服务（幂等）→ install 指向 `agent\data-watch-agent.exe` → 经 `AppEnvironmentExtra` 在进程启动前注入 `DATAWATCH_INSTALL_ROOT` / `DATAWATCH_DATA_ROOT` → 设开机自启、重定向 stdout/stderr 到 `runtime\logs\` → 启动服务。
5. **WebView2 检测**：Win10 缺运行时则静默安装，Win11 自带跳过。
6. 创建桌面与开始菜单快捷方式 `Data Watch Console`。

**卸载行为**：停止并删除 `DataWatchAgent` 服务。交互卸载时会询问是否同时删除 `C:\ProgramData\DataWatch\` 下配置、运行数据与日志；选择"是"后重装会重新进入初始化向导，选择"否"则重装沿用旧配置。静默卸载会自动清理 `C:\ProgramData\DataWatch\`，适合自动化干净重装。覆盖安装 / 升级不触发卸载器，现场配置会保留。

#### 4.2.4 确认服务起来

```powershell
Get-Service DataWatchAgent
Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status
```

服务应 `Running`；`/bootstrap/status` 应返回 `{"in_bootstrap": true}`（全新机）或 `false`（已初始化）。

### 4.3 初始化 Agent（Bootstrap 向导）

**不要手动编辑 `config.toml` 的密码**——密码以 DPAPI 加密存为 `password_enc`，**机器+用户绑定，不可手填、不可跨机复制**。`config.toml` 由安装器从模板生成（初始 `bootstrap_done=false`、密码为空），路径 `C:\ProgramData\DataWatch\config.toml`。

> 配置文件缺失也安全：Agent 启动时若 `config.toml` 不存在会自动生成默认配置（`bootstrap_done=false`），app 据此自动进入引导页。

初始化全程通过桌面 app 完成：

```text
1. 打开 Data Watch Console。
2. bootstrap_done=false → 首屏是【引导页】(不是登录页)：
   填 MySQL host/port/user/password → 测试连接 → 完成(写 bootstrap_done=true)。
3. 进入【登录页】，用账号登录。
4. 登录后在【设置】页配置 redis / tdengine 连接（bootstrap 只配 MySQL）。
```

> ⚠️ **登录账号不是 Agent 自己存的**，而是查第 2 步那台 MySQL 的 `sys_user` 表（由 backend 用 `AUTH_AUTO_BOOTSTRAP` 播种，默认 `Super/123456`）。所以 bootstrap 填的 MySQL 必须：① 目标机网络可达；② 已被 backend 初始化、`sys_user` 内有账号。否则登录会 503（连库失败）或 401（账号不存在）。锁死自救见第八节。

`config.toml` 结构参考（`password_enc` 由界面录入后自动写入；进程段 executable/working_dir 由双根自动派生，**均不需手填**）：

```toml
[agent]
host = "127.0.0.1"
port = 7070
runtime_dir = "C:\\ProgramData\\DataWatch\\runtime"
jwt_secret = "..."          # 自动生成，勿手填
bootstrap_done = true

[mysql]                      # 由引导页录入
host = "..."; port = 3306; user = "root"; database = "device_watch"; password_enc = "..."

[redis]                      # 登录后在「设置」页录入
host = "..."; port = 6379; password_enc = "..."

[tdengine]                   # 登录后在「设置」页录入
host = "..."; port = 6041; user = "root"; database = "device_data"; password_enc = "..."

[opcda]
server_prog_id = "ZPMC.OPCServer"
channel = "OPD_Server"
```

> ⚠️ **已知待修 P2**：`agent\config.toml.example` 仍残留 `[collector]`/`[writer]` 的 `executable`/`working_dir` 段（注释"必须配置否则 400"）。现逻辑已改为按双根自动派生，该段为**死配置**，填了不生效，待清理。

### 4.4 collector / writer 配置说明

连接凭据由 Agent 统一注入（见 1.4），`settings.toml` 只保留**运行参数**，无需填 host/port/user/password：

- **collector**：日志、连接池、interface_ingest、Redis 分片/容量等。
- **writer**：`[writer]` 刷写/批量、Redis group/consumer 等。

其中的连接段仅作脱离 Agent 独立运行时的 dev 回退，生产由 Agent 的 env 覆盖。模板见各自 `settings.toml.example`。

**OPC DA 中心采集无需手动启动 proxy**，Agent 会在启动 collector 前自动：

```text
1. 读取 MySQL 设备协议配置，检测 opcda_center endpoint。
2. 粘性更新 endpoint→port 映射，写 runtime\opcda_proxy\port_mapping.json。
3. 差量启动缺失 endpoint 的 proxy（已在线的不重启）。
4. 等所有必需 proxy 的 TCP 端口 ready（超时即失败，不启动 collector）。
5. 启动 collector，显式传入 --config / --port-mapping。
```

writer 无 proxy 编排，由 Agent 直接启动。

---

## 五、启动与验证

### 5.1 启动顺序

```powershell
Start-Service DataWatchAgent
Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status
```

打开桌面 `Data Watch Console`，依次：

```text
1. 首次安装：引导页填 MySQL → 测试 → 完成(bootstrap)。
2. 登录（账号来自该 MySQL 的 sys_user，默认 Super/123456）。
3. 在【设置】页配置 redis / tdengine 连接并测试。
4. 启动 collector（Agent 把三库连接经 env 注入）。
5. 启动 writer。
```

### 5.2 验证 collector

在 app 点击启动 collector 后应满足：

- Agent 日志显示**先准备 proxy，再启动 collector**（顺序不可颠倒）。
- 有 `opcda_center` 设备时：`runtime\opcda_proxy\port_mapping.json` 被生成，`runtime\logs\proxy_9090\current.log` 存在。
- collector 状态变为 running，实时点位有刷新。
- 无 `opcda_center` 设备时：不启动 proxy，collector 启动参数不含 `--port-mapping`。

### 5.3 验证 writer

- writer 状态变为 running。
- Redis Stream 消费状态正常。
- TDengine 有入库数据，writer 日志可查看。

> writer 与 collector 必须连同一个 Redis 实例；collector 写 Stream、writer 消费 Stream 写 TDengine，两者须同时运行数据才能落库。TDengine 宽表由 writer 首次写入时自动创建（超级表 `sd{device_type}`，如 `sdqc`/`sdasc`；子表以 `device_code` 命名），无需手动初始化。

### 5.4 部署完成标准

- app 能打开并登录。
- Agent Windows 服务开机自启。
- app 通过 Agent 启停 collector / writer，状态与日志可在 app 查看。
- Redis / MySQL / TDengine 健康检查成功。
- 有 OPC DA 时 Agent 自动启动 proxy，且 collector **实际读到点位**。
- writer 能消费 Redis Stream 并写入 TDengine。
- 目标机完成 SC-P06 服务账户 DCOM 实测（见第六节）。

---

## 六、OPC DA 服务账户验收（SC-P06）

这是当前部署方式**最关键的现场验收项**，也是成败级风险。

必须确认：**Agent 作为 Windows 服务运行时，proxy 在同一服务账户上下文下能连通 OPC DA/DCOM，并真实读到点位。**

不要只看（这些只证明进程链路启动了，不证明 DCOM 权限正确）：

```text
proxy 进程存在 / 9090 端口在监听 / collector 进程 running
```

必须验证：

```text
1. Agent 服务账户已确认。
2. OPCEnum / 目标 OPC Server 的 DCOM 权限包含该服务账户。
3. proxy 日志无 DCOM / OPCEnum / ProgID / Identity 连接错误。
4. collector 日志显示成功连接 OPC。
5. 实时监控或落库数据中点位值真实刷新。
```

> 排查 OPC 读取时，proxy 日志默认每 10s 打一行聚合统计（`Read summary ... good/bad`）；需要逐点看 quality 时，让 proxy 以 DEBUG 运行（环境变量 `OPCDA_PROXY_LOG_LEVEL=DEBUG`），日志会逐点打印 `Read item=... quality=0x...`（`0x00C0` 为 good）。

失败时优先收集：

```text
C:\ProgramData\DataWatch\runtime\logs\proxy_9090\current.log
C:\ProgramData\DataWatch\runtime\logs\collector\current.log
C:\ProgramData\DataWatch\config.toml
C:\ProgramData\DataWatch\collector\config\settings.toml
目标 endpoint / port / serverProgId / channel
Windows 服务账户名称
```

详细验收清单见 `specs/002-agent-opcda-proxy/quickstart.md`。

---

## 七、更新与运维

### 7.1 更新前后端（Docker）

在 `deploy` 目录执行（命令不会启动/重建 Redis / TDengine，它们在 `infra` profile）：

```powershell
cd D:\data_watch_release\deploy   # 或开发机仓库根的 deploy

# 只改了前端
docker compose -f .\docker-compose.yml build frontend
docker compose -f .\docker-compose.yml up -d frontend

# 只改了后端
docker compose -f .\docker-compose.yml build backend
docker compose -f .\docker-compose.yml up -d backend

# 前后端都改
docker compose -f .\docker-compose.yml up -d --build
```

依赖变化或缓存异常（改了 `backend/requirements.txt`、`frontend/package.json`、`pnpm-lock.yaml`、`python314t.Dockerfile`）时强制无缓存构建：

```powershell
docker compose -f .\docker-compose.yml build --no-cache backend frontend
docker compose -f .\docker-compose.yml up -d
```

改了 `python314t.Dockerfile` 还需先重建基础镜像：

```powershell
docker build -t data-watch-python314t:latest -f .\deploy\python314t.Dockerfile .
docker compose -f .\docker-compose.yml up -d --build
```

> 离线目标机更新：在开发机重新 `docker save` 导出 tar → 拷到目标机 → `docker load` → `up -d`。
> 改了 `backend.env` 不一定要重 build，但要重建容器让环境变量生效：
> `docker compose -f .\docker-compose.yml up -d --force-recreate backend`

### 7.2 更新 Windows 一体包

改了 app / agent / collector / writer / proxy 任意一块后：

```text
1. 开发机重新打包：pwsh -File installer\build.ps1（或 3.3 单模块重建 + ISCC）。
2. 把新的 DataWatch-Setup-<版本>.exe 拷到目标机。
3. 目标机直接运行新安装器（幂等：会 stop/remove 旧服务再装，覆盖 Program Files 程序文件）。
4. ProgramData 下的 config.toml / settings.toml 不被覆盖（ProvisionConfigs 仅在缺失时下发），现场配置保留。
5. 装完核验服务：Get-Service DataWatchAgent + /bootstrap/status。
```

### 7.3 常用运维命令

```powershell
# 停止 / 重启容器
docker compose -f .\docker-compose.yml down
docker compose -f .\docker-compose.yml restart backend frontend

# 查看
docker compose -f .\docker-compose.yml ps
docker logs -f data-watch-backend
docker logs -f data-watch-frontend

# 删除镜像（保留数据卷）
docker rmi data-watch-backend:latest data-watch-frontend:latest data-watch-python314t:latest
```

```powershell
# Windows 服务
Get-Service DataWatchAgent
Start-Service DataWatchAgent
Stop-Service DataWatchAgent
```

> ⚠️ **绝不要** `docker compose down -v`——`-v` 会删除 compose 管理的数据卷，若曾用 `--profile infra` 起过 Redis / TDengine 会丢数据。

---

## 八、常见故障

### 8.1 app 显示 Agent 不可达

```powershell
Get-Service DataWatchAgent
Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status
```

7070 不通先看 Agent 服务状态和 `runtime\logs\agent-service.err.log`。

### 8.2 登录后 Backend 不可达

```powershell
Invoke-RestMethod -Method POST `
  -Uri http://127.0.0.1:8000/api/auth/login `
  -ContentType "application/json" `
  -Body '{"userName":"用户名","password":"密码"}'
```

PowerShell 能 200 但 app 失败 → 桌面 app 后端请求经 Tauri Rust 层转发，不受浏览器 CORS 限制；优先检查引导页保存的后端地址、Agent 是否能读取运行期配置，以及 app 是否为最新安装包。Web 前端（浏览器访问 `http://127.0.0.1:8080`）仍受 `BACKEND_CORS_ORIGINS` 约束。

### 8.3 登录失败（503 / 401）或 bootstrap 填错锁死

Agent 登录**无本地账号**，查 config.toml 里那台 MySQL 的 `sys_user`。看 `runtime\logs\agent-service.err.log` 区分：

```text
503 "Agent not initialized"  → bootstrap_done=false，还没走引导 → 打开 app 走引导页
503 "Database unavailable"   → MySQL 连不上/凭据错 → 核对 [mysql] host/port/user，确认目标机能访问该库
401 用户名或密码错误          → 该 MySQL 无 sys_user(backend 未播种)或账号错 → 用 backend 初始化过的库与账号
```

bootstrap 已完成但填错 MySQL → 既登不进、又因 `bootstrap_done=true` 无法重走引导（`/bootstrap/*` 返回 403）。自救（DPAPI 密码不能手填，须重走引导）：

```powershell
& "C:\Program Files\DataWatch\tools\nssm.exe" stop DataWatchAgent
notepad C:\ProgramData\DataWatch\config.toml   # 把 bootstrap_done 改回 false
& "C:\Program Files\DataWatch\tools\nssm.exe" start DataWatchAgent
# 重开 app → 引导页 → 填正确 MySQL → 完成 → 登录
```

重装后没有进入初始化向导 → 通常是卸载时选择保留了 `C:\ProgramData\DataWatch\config.toml`。可重新卸载并选择删除配置与运行数据，或手动删除 `C:\ProgramData\DataWatch` 后再安装。

### 8.4 collector / writer 连不上 MySQL / Redis / TDengine

连接凭据由 Agent 从其 `config.toml`（DPAPI）解密后经 `DATAWATCH_<DB>_<FIELD>` env 注入。排查：

```text
Agent config.toml 的 [mysql]/[redis]/[tdengine] 是否填对、bootstrap 是否完成
collector/writer 是否由 Agent 拉起（独立手动启动不会有注入）
三者是否指向同一个 Redis 实例
```

### 8.5 proxy 端口 ready 失败

表现：collector 不启动、API 返回 400、错误含 endpoint / port / proxy 日志路径。检查：

```text
runtime\logs\proxy_9090\current.log
端口是否被无关进程占用
OpcDaProxyService.exe 是否存在
.NET Framework 4.8 是否安装
```

### 8.6 proxy ready 但采不到点（quality bad / value null）

多为 DCOM / OPC 权限或点位配置问题。检查：

```text
collector 日志中的 serverProgId / endpoint / proxy_port
proxy 日志中的 OPCEnum / DCOM / Identity 错误（设 OPCDA_PROXY_LOG_LEVEL=DEBUG 看逐点 quality）
dcomcnfg 中服务账户权限
点位地址(item id)是否在目标 OPC Server 真实存在
```

> 本机自测 OPC 时若用回环地址，库里设备 `endpoint` 与 proxy 端口映射须用同一字符串（`127.0.0.1`/`localhost`/空已统一归一为 `localhost`）；改端点后删 `port_mapping.json` 让其重建，避免错位到未监听端口（WinError 1225）。

### 8.7 重启 Agent 后端口冲突

正常情况 Agent 会认领仍存活的 `proxy_*.pid`，不重复拉起。仍冲突时检查：

```text
runtime\pids\proxy_*.pid
runtime\opcda_proxy\port_mapping.json
是否有非 Agent 进程占用了 sticky port
```

### 8.8 前端页面排版混乱但请求全 200

优先确认目标浏览器内核版本满足 Tailwind CSS v4 要求（Chrome/Edge ≥ 111）。国产浏览器需切「极速模式」并确认 Chromium 内核版本。可在地址栏打开 `chrome://version` / `edge://version` 查看。

---

## 九、数据库迁移

Docker 镜像只含程序，不含 MySQL / Redis / TDengine 业务数据。迁移到其他电脑时数据库单独处理：

```text
MySQL      导出 / 导入 device_watch 库
TDengine   导出 / 导入 device_data 库，或迁移 TDengine 数据目录 / volume
Redis      实时缓存通常不迁移；若用 Redis Stream 做未落库队列，停机迁移前先确认 writer 已消费完成
```

如果目标机直接连接原数据库服务器，则无需迁移数据，只需在 `deploy/backend.env` 与 Agent 引导/设置页中配置正确的数据库地址和账号密码。

---

## 十、相关文档

- `specs/002-agent-opcda-proxy/spec.md` / `plan.md` / `tasks.md` —— Agent 中枢编排架构设计与实现依据。
- `specs/002-agent-opcda-proxy/quickstart.md` —— SC-P06 目标机人工验收清单。
- `installer\DataWatch.iss` / `installer\build.ps1` —— 一键打包与安装器脚本源。
