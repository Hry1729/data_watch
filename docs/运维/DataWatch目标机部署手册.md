# DataWatch 目标机部署手册（Docker 数据面 + Windows 一体包）

更新日期：2026-06-07

本文档说明当前推荐的目标电脑部署方式。旧文档暂不删除；如与旧文档冲突，以本文档和 `specs/002-agent-opcda-proxy/quickstart.md` 为准。

## 一、部署结论

推荐采用混合部署：

```text
Docker / 外部服务：
  MySQL
  Redis
  TDengine
  Backend

Windows 一体安装包：
  Data Watch Console 桌面 app
  DataWatch Agent
  collector
  writer
  OPC DA proxy sidecar
```

职责边界：

- app 只作为 UI，不直接启动 collector、writer、proxy。
- Agent 是 Windows 侧中枢，负责进程启动、停止、重启、状态、日志、OPC DA proxy 编排。
- collector / writer 作为 Windows 本机进程运行，不放进 Docker。
- OPC DA proxy 由 Agent 按需拉起，不再依赖 `collector/run.bat`。
- MySQL / Redis / TDengine / Backend 继续用 Docker 或现场已有服务部署。

## 二、目标目录约定

程序文件放在 `Program Files`，运行配置和日志放在 `ProgramData`。

```text
C:\Program Files\DataWatch\
├─ app\
├─ agent\
├─ collector\
├─ writer\
└─ sidecars\
   └─ opcda_proxy_service\
      └─ bin\
         └─ OpcDaProxyService.exe

C:\ProgramData\DataWatch\
├─ config.toml
├─ collector\
│  └─ config\
│     └─ settings.toml
├─ writer\
│  └─ config\
│     └─ settings.toml
└─ runtime\
   ├─ logs\
   ├─ pids\
   └─ opcda_proxy\
      ├─ port_mapping.json
      └─ proxy_9090.json
```

说明：

- `Program Files` 里的程序目录应视为只读。
- `ProgramData` 里的配置、日志、PID、runtime 文件允许 Agent 写入。
- collector 启动时由 Agent 显式传入 `--config` 和 `--port-mapping`，不依赖工作目录推断配置。

## 三、部署前置条件

目标电脑需要准备：

- Windows 10 / Windows Server 或更高版本。
- Docker Desktop 或现场已有 MySQL / Redis / TDengine / Backend 服务。
- .NET Framework 4.8 运行时。
- DataWatch 一体安装包。
- Backend 可访问 MySQL / Redis / TDengine。
- Agent 可访问 Backend、MySQL、Redis、TDengine。
- 如使用 OPC DA：
  - 目标 OPC Server 的 `ProgID` 已知，默认 `ZPMC.OPCServer`。
  - channel 已知，默认 `OPD_Server`。
  - Agent Windows 服务账户具备 DCOM / OPCEnum / OPC Server 访问权限。

## 四、在自己电脑上准备发布产物

本节说明在开发电脑 `D:\python\device_alarm\data_watch` 上如何生成目标机部署需要的产物。建议每次发版时先在本机完成打包、整理 staging 目录，再拷贝到目标机。

### 1. 建议的本机发版目录

建议新建一个临时发布目录，例如：

```powershell
$ReleaseRoot = "D:\data_watch_release"
New-Item -ItemType Directory -Force $ReleaseRoot
New-Item -ItemType Directory -Force "$ReleaseRoot\app"
New-Item -ItemType Directory -Force "$ReleaseRoot\agent"
New-Item -ItemType Directory -Force "$ReleaseRoot\collector"
New-Item -ItemType Directory -Force "$ReleaseRoot\writer"
New-Item -ItemType Directory -Force "$ReleaseRoot\sidecars\opcda_proxy_service"
New-Item -ItemType Directory -Force "$ReleaseRoot\config-templates"
New-Item -ItemType Directory -Force "$ReleaseRoot\docker-images"
New-Item -ItemType Directory -Force "$ReleaseRoot\deploy"
```

最终整理目标：

```text
D:\data_watch_release\
├─ app\                         # Tauri 安装包或 app bundle
├─ agent\                       # Agent 源码/venv 运行包，或后续 exe 包
├─ collector\                   # data-watch-collector PyInstaller 产物
├─ writer\                      # data-watch-writer PyInstaller 产物
├─ sidecars\
│  └─ opcda_proxy_service\      # OpcDaProxyService.exe 与运行依赖
├─ config-templates\            # config.toml.example / settings.toml 模板
├─ docker-images\               # Docker 离线镜像 tar
└─ deploy\                      # docker-compose.yml / backend.env 等部署文件
```

### 2. 打包桌面 app（Data Watch Console）

进入 app 目录：

```powershell
cd D:\python\device_alarm\data_watch\app
```

先做 TypeScript 和 Vite 构建：

```powershell
pnpm install
pnpm exec tsc --noEmit
pnpm build
```

生成 Tauri 安装包：

```powershell
pnpm tauri build
```

产物通常在：

```text
app\src-tauri\target\release\bundle\msi\
app\src-tauri\target\release\bundle\nsis\
```

常见文件示例：

```text
Data Watch Console_0.1.0_x64_en-US.msi
Data Watch Console_0.1.0_x64-setup.exe
```

复制到发布目录：

```powershell
Copy-Item ".\src-tauri\target\release\bundle\msi\*" "$ReleaseRoot\app\" -Recurse -Force
Copy-Item ".\src-tauri\target\release\bundle\nsis\*" "$ReleaseRoot\app\" -Recurse -Force
```

说明：

- app 默认连接本机 Agent：`http://127.0.0.1:7070`。
- Backend 地址由环境/配置决定，目标机需保证 Backend 可达。
- Tauri 窗口默认大小当前为 `1280x800`，配置在 `app/src-tauri/tauri.conf.json`。

### 3. 打包 collector

collector 使用 PyInstaller 打包，建议使用独立 `.venv-pack`，不要复用开发 `.venv`。

首次准备打包环境：

```powershell
cd D:\python\device_alarm\data_watch\collector

py -3.14 -m venv .venv-pack
.\.venv-pack\Scripts\python.exe -m pip install --upgrade pip
.\.venv-pack\Scripts\python.exe -m pip install pyinstaller
.\.venv-pack\Scripts\python.exe -m pip install -r requirements.txt
```

执行打包：

```powershell
cd D:\python\device_alarm\data_watch\collector
.\.venv-pack\Scripts\pyinstaller.exe data-watch-collector.spec
```

产物目录：

```text
collector\dist\data-watch-collector\
├─ data-watch-collector.exe
└─ _internal\...
```

复制到发布目录：

```powershell
Copy-Item "D:\python\device_alarm\data_watch\collector\dist\data-watch-collector\*" "$ReleaseRoot\collector\" -Recurse -Force
# 模板取脱敏版（含密码的 settings.toml 已不入库，仅本地 dev 用）
Copy-Item "D:\python\device_alarm\data_watch\installer\templates\collector-settings.toml.example" "$ReleaseRoot\config-templates\collector-settings.toml" -Force
```

注意：

- 目标机上不要再直接运行 `collector\run.bat`。
- OPC DA proxy 由 Agent 自动编排。
- collector 已支持 `--config` 和 `--port-mapping`，目标机由 Agent 自动传入。

### 4. 打包 writer

首次准备打包环境：

```powershell
cd D:\python\device_alarm\data_watch\writer

py -3.14 -m venv .venv-pack
.\.venv-pack\Scripts\python.exe -m pip install --upgrade pip
.\.venv-pack\Scripts\python.exe -m pip install pyinstaller
.\.venv-pack\Scripts\python.exe -m pip install -r requirements.txt
```

执行打包：

```powershell
cd D:\python\device_alarm\data_watch\writer
.\.venv-pack\Scripts\pyinstaller.exe data-watch-writer.spec
```

产物目录：

```text
writer\dist\data-watch-writer\
├─ data-watch-writer.exe
└─ _internal\...
```

复制到发布目录：

```powershell
Copy-Item "D:\python\device_alarm\data_watch\writer\dist\data-watch-writer\*" "$ReleaseRoot\writer\" -Recurse -Force
# 模板取脱敏版（含密码的 settings.toml 已不入库，仅本地 dev 用）
Copy-Item "D:\python\device_alarm\data_watch\installer\templates\writer-settings.toml.example" "$ReleaseRoot\config-templates\writer-settings.toml" -Force
```

### 5. 准备 OPC DA proxy sidecar

当前 proxy 位于：

```text
collector\sidecars\opcda_proxy_service\
```

目标机不应现场编译 proxy，所以发布包必须带已编译文件：

```text
collector\sidecars\opcda_proxy_service\bin\OpcDaProxyService.exe
```

复制 sidecar：

```powershell
Copy-Item "D:\python\device_alarm\data_watch\collector\sidecars\opcda_proxy_service\*" "$ReleaseRoot\sidecars\opcda_proxy_service\" -Recurse -Force
```

确认发布目录中存在：

```powershell
Test-Path "$ReleaseRoot\sidecars\opcda_proxy_service\bin\OpcDaProxyService.exe"
```

如返回 `False`，需要先在开发机完成 proxy 编译，不能把 `build.ps1` 当成目标机运行依赖。

### 6. 准备 Agent

当前 Agent 已有 FastAPI 源码和 NSSM 服务安装脚本，但尚未统一成 PyInstaller exe 包。当前推荐发版方式是：**随一体包复制 Agent 源码 + `.venv` 运行环境，目标机用 NSSM 注册 Windows 服务**。

本机准备 Agent 运行环境：

```powershell
cd D:\python\device_alarm\data_watch\agent

py -3.14 -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe -m pip install -r requirements.txt
```

验证 Agent 可启动：

```powershell
cd D:\python\device_alarm\data_watch\agent
.\.venv\Scripts\python.exe main.py
```

看到：

```text
Uvicorn running on http://127.0.0.1:7070
```

即可 `Ctrl+C` 停止。

复制 Agent 到发布目录：

```powershell
Copy-Item "D:\python\device_alarm\data_watch\agent\app" "$ReleaseRoot\agent\app" -Recurse -Force
Copy-Item "D:\python\device_alarm\data_watch\agent\service" "$ReleaseRoot\agent\service" -Recurse -Force
Copy-Item "D:\python\device_alarm\data_watch\agent\main.py" "$ReleaseRoot\agent\" -Force
Copy-Item "D:\python\device_alarm\data_watch\agent\requirements.txt" "$ReleaseRoot\agent\" -Force
Copy-Item "D:\python\device_alarm\data_watch\agent\config.toml.example" "$ReleaseRoot\config-templates\agent-config.toml.example" -Force
```

如果希望目标机离线安装 Python 依赖，需要另行准备 wheelhouse：

```powershell
cd D:\python\device_alarm\data_watch\agent
New-Item -ItemType Directory -Force "$ReleaseRoot\agent\wheelhouse"
.\.venv\Scripts\python.exe -m pip download -r requirements.txt -d "$ReleaseRoot\agent\wheelhouse"
```

目标机离线安装时：

```powershell
.\.venv\Scripts\python.exe -m pip install --no-index --find-links .\wheelhouse -r requirements.txt
```

> 后续可选增强：为 Agent 新增 PyInstaller spec，产出 `data-watch-agent.exe`，这样目标机无需 Python venv。当前文档先按已存在的源码/venv + NSSM 方式描述。

### 7. 本机回归检查

发版前建议跑：

```powershell
cd D:\python\device_alarm\data_watch\agent
.\.venv\Scripts\python.exe -m pytest

cd D:\python\device_alarm\data_watch\collector
.\.venv\Scripts\python.exe -m pytest

cd D:\python\device_alarm\data_watch\app
pnpm exec tsc --noEmit
```

当前 002 OPC DA proxy 编排完成时的基线为：

```text
agent     95 passed
collector 146 passed
```

### 8. 打包 Docker 镜像到发布目录

如果目标机可以联网并能直接 `docker compose up -d --build`，可以跳过本节。但现场网络经常不可控，建议每次发版都把前后端镜像导出到 `D:\data_watch_release\docker-images`，和 Windows 一体包一起拷贝。

#### 8.1 准备 Docker 部署文件

复制 compose 文件和环境变量模板：

```powershell
Copy-Item "D:\python\device_alarm\data_watch\deploy\docker-compose.yml" "$ReleaseRoot\deploy\docker-compose.yml" -Force
# taos.cfg 必须一起拷贝：compose 会把它挂进 TDengine 容器，缺失会导致 tdengine 起不来
Copy-Item "D:\python\device_alarm\data_watch\deploy\taos.cfg" "$ReleaseRoot\deploy\taos.cfg" -Force
Copy-Item "D:\python\device_alarm\data_watch\deploy\backend.env.example" "$ReleaseRoot\deploy\backend.env.example" -Force
Copy-Item "D:\python\device_alarm\data_watch\deploy\python314t.Dockerfile" "$ReleaseRoot\deploy\python314t.Dockerfile" -Force
Copy-Item "D:\python\device_alarm\data_watch\deploy\nginx.conf" "$ReleaseRoot\deploy\nginx.conf" -Force
```

如果你已经为目标机填好了 `backend.env`，也一并复制：

```powershell
Copy-Item "D:\python\device_alarm\data_watch\deploy\backend.env" "$ReleaseRoot\deploy\backend.env" -Force
```

没有 `backend.env` 时，目标机部署前从 `backend.env.example` 复制并填写。

#### 8.2 构建前后端镜像

第一次需要先构建 Python 3.14t 基础镜像：

```powershell
cd D:\python\device_alarm\data_watch
docker build -t data-watch-python314t:latest -f .\deploy\python314t.Dockerfile .
```

构建前后端镜像：

```powershell
cd D:\python\device_alarm\data_watch
docker compose -f .\deploy\docker-compose.yml build backend frontend
```

确认镜像存在：

```powershell
docker images data-watch-python314t
docker images data-watch-backend
docker images data-watch-frontend
```

#### 8.3 可选拉取 infra 镜像

如果目标机也需要离线运行 Redis / TDengine，可以在本机先拉取：

```powershell
docker pull redis:latest
docker pull tdengine/tsdb-amd64:3.4.0.9
```

如果目标机使用现场已有 Redis / TDengine，则不需要导出这两个 infra 镜像。

#### 8.4 导出镜像 tar

导出前后端与 Python 基础镜像：

```powershell
docker save data-watch-python314t:latest -o "$ReleaseRoot\docker-images\data-watch-python314t_latest.tar"
docker save data-watch-backend:latest -o "$ReleaseRoot\docker-images\data-watch-backend_latest.tar"
docker save data-watch-frontend:latest -o "$ReleaseRoot\docker-images\data-watch-frontend_latest.tar"
```

如需离线携带 Redis / TDengine：

```powershell
docker save redis:latest -o "$ReleaseRoot\docker-images\redis_latest.tar"
docker save tdengine/tsdb-amd64:3.4.0.9 -o "$ReleaseRoot\docker-images\tdengine_3.4.0.9.tar"
```

确认产物：

```powershell
Get-ChildItem "$ReleaseRoot\docker-images"
```

#### 8.5 迁移到目标机后的导入命令

目标机拿到 `D:\data_watch_release` 后，导入镜像：

```powershell
cd D:\data_watch_release\docker-images

docker load -i .\data-watch-python314t_latest.tar
docker load -i .\data-watch-backend_latest.tar
docker load -i .\data-watch-frontend_latest.tar
```

如携带 infra 镜像：

```powershell
docker load -i .\redis_latest.tar
docker load -i .\tdengine_3.4.0.9.tar
```

导入后检查：

```powershell
docker images data-watch-backend
docker images data-watch-frontend
docker images redis
docker images tdengine/tsdb-amd64
```

### 9. 发布包检查清单

拷贝到目标机前，确认：

```text
D:\data_watch_release\app\                         有 Tauri msi/nsis 安装包
D:\data_watch_release\agent\main.py                存在
D:\data_watch_release\agent\app\                   存在
D:\data_watch_release\agent\service\install_service.py 存在
D:\data_watch_release\collector\data-watch-collector.exe 存在
D:\data_watch_release\writer\data-watch-writer.exe 存在
D:\data_watch_release\sidecars\opcda_proxy_service\bin\OpcDaProxyService.exe 存在
D:\data_watch_release\config-templates\            有配置模板
D:\data_watch_release\deploy\docker-compose.yml    存在
D:\data_watch_release\deploy\taos.cfg              存在（compose 挂载，缺失则 TDengine 起不来）
D:\data_watch_release\docker-images\               有 data-watch-backend/data-watch-frontend 镜像 tar
```

## 五、部署步骤

### 1. 部署 Docker / 后端服务

如果目标机已导入 `D:\data_watch_release\docker-images` 中的离线镜像，则不需要在目标机重新 build。推荐步骤：

```powershell
cd D:\data_watch_release\deploy
Copy-Item .\backend.env.example .\backend.env
notepad .\backend.env
docker compose -f .\docker-compose.yml up -d
```

重点确认：

```env
MYSQL_HOST=...
MYSQL_PORT=3306
MYSQL_DATABASE=device_watch

REDIS_HOST=...
REDIS_PORT=6379

TDENGINE_HOST=...
TDENGINE_PORT=6041
TDENGINE_DATABASE=device_data

BACKEND_CORS_ORIGINS=http://localhost:1420,http://127.0.0.1:1420

# 必须改成强随机串，不能留模板占位/默认值
AUTH_SECRET_KEY=换成一串足够长的随机密钥
```

> 生成强随机 `AUTH_SECRET_KEY`：
> `& "C:\Users\Hry\.local\bin\python3.14t.exe" -c "import secrets; print(secrets.token_urlsafe(48))"`
> 漏配会导致后端启动 WARNING，且任何人可用公开默认密钥伪造登录 token。`backend.env.example` 仅为模板，必须复制成 `backend.env` 填真实值后容器才会加载。

如果目标机可以联网并需要现场构建，则可改用：

```powershell
cd D:\data_watch_release\deploy
docker compose -f .\docker-compose.yml up -d --build
```

如果 MySQL / Redis / TDengine / Backend 已由现场平台提供，则只需启动 `backend` / `frontend`，并确保 `backend.env` 指向现场服务。

如果需要 compose 同时托管 Redis / TDengine，执行：

```powershell
cd D:\data_watch_release\deploy
docker compose -f .\docker-compose.yml --profile infra up -d
```

日常仅更新前后端时，不要执行 `docker compose down -v`，避免删除数据卷。

#### TDengine 保留期与磁盘控制（部署时务必确认）

目标：305 设备 × 1000 点 × 100ms、仅变化存储、约 500GB 盘下不写爆磁盘。参数：**KEEP 14d / DURATION 1d / logKeepDays 4 / minimalDataDirGB 50 / sDebugFlag 131**。

- **compose 托管 TDengine 时**：`docker-compose.yml` 已把 `deploy/taos.cfg` 挂进容器，自动生效 `logKeepDays 4`（服务日志留 4 天）、`minimalDataDirGB 50`（数据盘剩余 <50GB 停写，最后防线）、`sDebugFlag 131`（同步模块只记 error/warning，避免日志刷屏）。
- **数据保留期 KEEP**：由建库语句控制。首次建库（writer / `scripts/init_tdengine_v2.py`）已带 `KEEP 14d DURATION 1d`（实际保留约 14~15 天）。**若 `device_data` 库已存在**（之前建过），`CREATE IF NOT EXISTS` 不改其保留期，需手动让 KEEP 生效（DURATION 建库后不可改，沿用原值）：

  ```powershell
  docker exec data-watch-tdengine taos -s "ALTER DATABASE device_data KEEP 14d"
  ```

- **现场自管 TDengine（不走 compose）时**：上面这些不会自动应用，需在现场 TDengine 的 `taos.cfg` 自行配 `logKeepDays`/`minimalDataDirGB`/`sDebugFlag` 并重启，且对 `device_data` 执行上面的 `ALTER ... KEEP 14d`。

> ⚠️ `minimalDataDirGB` 触发会**停止写入（采集中断）**，是最后防线、非常规手段。正常应让 KEEP 把磁盘稳定在阈值以下。
> ⚠️ 14d/4d/50GB 是按估算给的初值；**上线后建议测 1~2 周实际增速再定稿**：`KEEP_天 ≈ 400GB ÷ 实测 GB每天`（用 `du -sh /var/lib/taos` 量增速）。

### 2. 安装 DataWatch Windows 一体包

安装包应完成：

```text
1. 复制 app / agent / collector / writer / sidecars。
2. 创建 C:\ProgramData\DataWatch\... 运行目录。
3. 写入默认 agent config.toml、collector settings.toml、writer settings.toml。
4. 注册 DataWatch Agent Windows 服务。
5. 创建 Data Watch Console 桌面快捷方式。
```

安装完成后，确认：

```powershell
Get-Service DataWatchAgent
```

服务应处于 `Running` 或可手动启动。

如果当前还没有一体安装器，可以先手动部署发布目录：

```powershell
New-Item -ItemType Directory -Force "C:\Program Files\DataWatch"
New-Item -ItemType Directory -Force "C:\ProgramData\DataWatch\agent"
New-Item -ItemType Directory -Force "C:\ProgramData\DataWatch\collector\config"
New-Item -ItemType Directory -Force "C:\ProgramData\DataWatch\writer\config"
New-Item -ItemType Directory -Force "C:\ProgramData\DataWatch\runtime"

Copy-Item "$ReleaseRoot\agent" "C:\Program Files\DataWatch\agent" -Recurse -Force
Copy-Item "$ReleaseRoot\collector" "C:\Program Files\DataWatch\collector" -Recurse -Force
Copy-Item "$ReleaseRoot\writer" "C:\Program Files\DataWatch\writer" -Recurse -Force
Copy-Item "$ReleaseRoot\sidecars" "C:\Program Files\DataWatch\sidecars" -Recurse -Force

Copy-Item "$ReleaseRoot\config-templates\agent-config.toml.example" "C:\ProgramData\DataWatch\config.toml" -Force
Copy-Item "$ReleaseRoot\config-templates\collector-settings.toml" "C:\ProgramData\DataWatch\collector\config\settings.toml" -Force
Copy-Item "$ReleaseRoot\config-templates\writer-settings.toml" "C:\ProgramData\DataWatch\writer\config\settings.toml" -Force
```

手动部署 app 时，直接运行 `$ReleaseRoot\app\` 中的 Tauri 安装包。

### 3. 注册 Agent Windows 服务

如果安装器尚未自动注册服务，可以用 NSSM 手动注册。先确保目标机有 `nssm.exe`，然后在目标机执行：

```powershell
cd "C:\Program Files\DataWatch\agent"

py -3.14 -m venv .venv
.\.venv\Scripts\python.exe -m pip install --upgrade pip
.\.venv\Scripts\python.exe -m pip install -r requirements.txt

.\.venv\Scripts\python.exe .\service\install_service.py `
  --nssm "C:\tools\nssm\nssm.exe" `
  install `
  --python "C:\Program Files\DataWatch\agent\.venv\Scripts\python.exe" `
  --agent-dir "C:\Program Files\DataWatch\agent"

.\.venv\Scripts\python.exe .\service\install_service.py `
  --nssm "C:\tools\nssm\nssm.exe" `
  start
```

验证：

```powershell
Get-Service DataWatchAgent
Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status
```

### 4. 初始化 Agent（Bootstrap 向导）

**不要手动编辑 `config.toml` 的密码**——密码以 DPAPI 加密存为 `password_enc`，机器+用户绑定，只能经界面录入（自动加密），不可手填、不可跨机复制。`config.toml` 由安装器从模板自动生成（初始 `bootstrap_done=false`、各密码为空），路径在 `C:\ProgramData\DataWatch\config.toml`。

初始化通过桌面 App 完成：

```text
1. 打开 Data Watch Console。
2. bootstrap_done=false 时首屏是【引导页】(不是登录页)：填 MySQL host/port/user/password → 测试连接 → 完成(写入 bootstrap_done=true)。
3. 进入【登录页】，用账号登录。
4. 登录后在【设置】页配置 redis / tdengine 连接(bootstrap 只配 MySQL)。
```

> ⚠️ **登录账号不是 Agent 自己存的**，而是查第 2 步那台 **MySQL 的 `sys_user` 表**（由 backend 用 `AUTH_AUTO_BOOTSTRAP` 播种，默认 Super/123456）。所以 bootstrap 填的 MySQL 必须：① 目标机网络可达；② 已被 backend 初始化、`sys_user` 内有账号。否则登录会 **503**（连库失败）或 **401**（账号不存在）。若 bootstrap 填错 MySQL 导致登录不进，见「八、常见故障」的自救步骤。

config.toml 结构参考（`password_enc` 由界面录入后自动写入；进程段 executable/working_dir 由安装根/数据根自动派生，**均不需手填**）：

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

注意：

- 密码以 DPAPI 加密存为 `password_enc`，**机器+用户绑定，不可手填、不可跨机复制**——换机须在该机重新走引导/设置。
- redis/mysql/tdengine 连接信息以 Agent 的 `config.toml` 为**唯一来源**：Agent 启动 collector/writer 时解密并经 `DATAWATCH_<DB>_<FIELD>` 环境变量下发覆盖，故 collector/writer 的 `settings.toml` **无需也不应**填凭据。
- `[collector]`/`[writer]` 的 executable/working_dir 由安装根/数据根自动派生，无需配置。

### 5. 配置 collector

连接凭据由 Agent 统一注入（见上节），`collector\config\settings.toml` 只保留**运行参数**（日志、连接池、interface_ingest、Redis 分片/容量等），无需填写 host/port/user/password。其中的连接段仅作脱离 Agent 独立运行 collector 时的 dev 回退，生产由 Agent 的 env 覆盖。模板见 `settings.toml.example`。

OPC DA 中心采集不需要手动启动 proxy。Agent 会：

```text
1. 读取 MySQL 设备协议配置。
2. 检测 opcda_center endpoint。
3. 生成 runtime\opcda_proxy\port_mapping.json。
4. 为每个 endpoint 启动一个 proxy。
5. 等 proxy 端口 ready。
6. 启动 collector，并传入 --config / --port-mapping。
```

### 6. 配置 writer

同 collector：连接凭据由 Agent 注入，`writer\config\settings.toml` 只保留运行参数（[writer] 刷写/批量、Redis group/consumer 等）。模板见 `settings.toml.example`。

writer 没有 OPC DA proxy 编排逻辑，由 Agent 直接启动。

## 六、启动与验证

### 1. 启动服务

```powershell
Start-Service DataWatchAgent
```

确认 Agent 可访问：

```powershell
Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status
```

### 2. 启动桌面 app

打开桌面快捷方式：

```text
Data Watch Console
```

进入 app 后：

```text
1. 首次安装：引导页填 MySQL → 测试 → 完成(bootstrap)。
2. 登录(账号来自该 MySQL 的 sys_user，默认 Super/123456)。
3. 在【设置】页配置 redis / tdengine 连接。
4. 测试 Redis / MySQL / TDengine。
5. 启动 collector(Agent 会把三库连接经 env 注入)。
6. 启动 writer。
```

### 3. 验证 collector

在 app 点击启动 collector 后，应满足：

- Agent 日志显示先准备 proxy，再启动 collector。
- 如存在 `opcda_center` 设备，`runtime\opcda_proxy\port_mapping.json` 被生成。
- `runtime\logs\proxy_9090\current.log` 存在。
- collector 状态变为 running。
- 实时点位有刷新。

如果无 `opcda_center` 设备：

- 不应启动 proxy。
- collector 启动参数不包含 `--port-mapping`。

### 4. 验证 writer

在 app 点击启动 writer 后，应满足：

- writer 状态变为 running。
- Redis Stream 消费状态正常。
- TDengine 有入库数据。
- writer 日志可查看。

## 七、OPC DA 服务账户验收（SC-P06）

这是当前部署方式最关键的现场验收项。

必须确认：**Agent 作为 Windows 服务运行时，proxy 在同一服务账户上下文下能连通 OPC DA/DCOM，并真实读到点位。**

不要只看：

```text
proxy 进程存在
9090 端口在监听
collector 进程 running
```

这些只能证明进程链路启动了，不能证明 DCOM 权限正确。

必须验证：

```text
1. Agent 服务账户已确认。
2. OPCEnum / 目标 OPC Server 的 DCOM 权限包含该服务账户。
3. proxy 日志无 DCOM / OPCEnum / ProgID / Identity 连接错误。
4. collector 日志显示成功连接 OPC。
5. 实时监控或落库数据中点位值真实刷新。
```

如果失败，优先收集：

```text
C:\ProgramData\DataWatch\runtime\logs\proxy_9090\current.log
C:\ProgramData\DataWatch\runtime\logs\collector\current.log
C:\ProgramData\DataWatch\config.toml
C:\ProgramData\DataWatch\collector\config\settings.toml
目标 endpoint / port / serverProgId / channel
Windows 服务账户名称
```

## 八、常见故障

### 1. app 显示 Agent 不可达

检查：

```powershell
Get-Service DataWatchAgent
Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status
```

如果 7070 不通，先看 Agent 服务和 Agent 日志。

### 2. 登录后 Backend 不可达

检查 Backend 地址、CORS、容器端口：

```powershell
Invoke-RestMethod `
  -Method POST `
  -Uri http://127.0.0.1:8000/api/auth/login `
  -ContentType "application/json" `
  -Body '{"userName":"用户名","password":"密码"}'
```

如果 PowerShell 能 200，但 app 失败，优先检查 Backend CORS 是否包含 Tauri WebView origin。

### 3. 登录失败（503 / 401），或 bootstrap 填错锁死

Agent 登录**无本地账号**，查 config.toml 里那台 MySQL 的 `sys_user`。先看 `runtime\logs\agent-service.err.log` 区分：

```text
503 "Agent not initialized"  → bootstrap_done=false，还没走引导 → 打开 App 走引导页
503 "Database unavailable"   → MySQL 连不上/凭据错 → 核对 [mysql] host/port/user，确认目标机能访问该库
401 用户名或密码错误          → 该 MySQL 无 sys_user(backend 未播种该库)或账号错 → 用 backend 初始化过的库与账号
```

若 bootstrap 已完成但填错 MySQL → 既登不进、又因 `bootstrap_done=true` 无法重走引导（`/bootstrap/*` 返回 403）。自救（DPAPI 密码不能手填，须重走引导）：

```powershell
& "C:\Program Files\DataWatch\tools\nssm.exe" stop DataWatchAgent
# 用记事本把 config.toml 里 bootstrap_done 改回 false
notepad C:\ProgramData\DataWatch\config.toml
& "C:\Program Files\DataWatch\tools\nssm.exe" start DataWatchAgent
# 重开 App → 引导页 → 填正确 MySQL → 完成 → 登录
```

### 4. collector / writer 连不上 MySQL / Redis / TDengine

连接凭据由 Agent 从其 `config.toml`（DPAPI 加密）解密后，经 `DATAWATCH_<DB>_<FIELD>` 环境变量注入。排查：

```text
Agent config.toml 的 [mysql]/[redis]/[tdengine] 是否填对、bootstrap 是否完成
collector/writer 是否由 Agent 拉起（独立手动启动则不会有注入，需自行设 env 或填本地 settings.toml）
```

### 5. proxy 端口 ready 失败

表现：

```text
collector 不启动
API 返回 400
错误包含 endpoint / port / proxy log path
```

检查：

```text
runtime\logs\proxy_9090\current.log
端口是否被无关进程占用
OpcDaProxyService.exe 是否存在
.NET Framework 4.8 是否安装
```

### 6. proxy ready 但采不到点

这通常是 DCOM / OPC 权限问题。

检查：

```text
collector 日志中的 serverProgId / endpoint / proxy_port
proxy 日志中的 OPCEnum / DCOM / Identity 错误
dcomcnfg 中服务账户权限
```

### 7. 重启 Agent 后端口冲突

正常情况下，Agent 会认领仍存活的 `proxy_*.pid`，不会重复拉起。

如果仍冲突，检查：

```text
runtime\pids\proxy_*.pid
runtime\opcda_proxy\port_mapping.json
是否有非 Agent 进程占用了 sticky port
```

## 九、部署完成标准

部署完成后，应满足：

- app 能打开并登录。
- Agent Windows 服务开机自启。
- app 通过 Agent 启停 collector / writer。
- collector / writer 状态和日志可在 app 中查看。
- Redis / MySQL / TDengine 健康检查成功。
- 有 OPC DA 时，Agent 自动启动 proxy，且 collector 实际读到点位。
- writer 能消费 Redis Stream 并写入 TDengine。
- 目标机完成 SC-P06 服务账户 DCOM 实测。

## 十、相关文档

- `docs/运维/打包.md`
- `docs/运维/项目Docker镜像部署与迁移说明.md`
- `specs/002-agent-opcda-proxy/spec.md`
- `specs/002-agent-opcda-proxy/quickstart.md`
