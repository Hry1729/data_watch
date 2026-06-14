# 项目 Docker 镜像部署与迁移说明

更新日期：2026-06-01

## 一、总体结论

本项目后续迁移和部署统一采用 Docker 镜像方式：

```text
frontend   使用 Docker 镜像运行，Nginx 提供静态资源和接口反向代理
backend    使用 Docker 镜像运行，镜像内置 Python 3.14t free-threading
collector  暂不容器化，以 PyInstaller 打包为 exe 方式在 Windows 主机运行
writer     暂不容器化，以 PyInstaller 打包为 exe 方式在 Windows 主机运行，与 collector 独立进程分开打包
MySQL      默认使用宿主机已有服务或外部数据库
TDengine   默认使用宿主机已有服务或外部数据库；compose 中仅作为可选 infra profile
Redis      默认使用宿主机已有服务或外部 Redis；compose 中仅作为可选 infra profile
```

采集模块涉及 OPC DA、DCOM、PLC 网络、IOM 点位和现场驱动环境，暂时不建议放入 Docker。正式部署时，采集模块仍建议运行在 Windows 主机环境中。

## 二、今天完成的 Docker 部署改造

今天已补齐前后端镜像部署所需文件：

```text
<项目根目录>/.dockerignore
<项目根目录>/backend/Dockerfile
<项目根目录>/backend/.dockerignore
<项目根目录>/frontend/Dockerfile
<项目根目录>/frontend/.dockerignore
<项目根目录>/frontend/.env.production
<项目根目录>/deploy/python314t.Dockerfile
<项目根目录>/deploy/nginx.conf
<项目根目录>/deploy/backend.env.example
<项目根目录>/deploy/docker-compose.yml
```

关键处理：

```text
1. 后端基础镜像使用 Python 3.14t free-threading。
2. 前端接口路径本身使用 /api，生产环境 VITE_API_URL 保持空，由 Nginx 反向代理到后端容器。
3. 前端权限模式改为 backend，使用后端数据库菜单和权限。
4. docker-compose 默认只编排 frontend 和 backend。
5. Redis / TDengine 放入 `infra` profile，只有显式启用时才启动，避免更新前后端代码时影响数据库容器和数据卷。
6. 后端容器默认通过 host.docker.internal 访问宿主机 MySQL / Redis / TDengine。
7. 后端 Docker 构建中暂时跳过 perspective-python，因为它在 Python 3.14t 下源码编译失败；后端代码会以可选能力降级运行。
8. 前端 Docker 构建中关闭 Husky，并允许 pnpm 依赖构建脚本，避免容器构建失败。
```

## 三、目录和占位符说明

文档中统一使用：

```text
<项目根目录>  D:\python\device_alarm\data_watch
```

实际 PowerShell 操作时：

```powershell
cd D:\python\device_alarm\data_watch
```

## 四、首次部署流程

### 1. 安装并启动 Docker Desktop

确认 Docker Desktop 已启动，并使用 Linux containers。

验证：

```powershell
docker version
docker compose version
```

如果 `docker version` 只有 Client 没有 Server，说明 Docker Engine 没启动。

### 2. 配置部署环境变量

复制后端环境变量模板：

```powershell
cd D:\python\device_alarm\data_watch
Copy-Item .\deploy\backend.env.example .\deploy\backend.env
notepad .\deploy\backend.env
```

重点修改：

```env
MYSQL_HOST=host.docker.internal
MYSQL_PORT=3306
MYSQL_USER=root
MYSQL_PASSWORD=你的 MySQL 密码
MYSQL_DATABASE=device_watch

BACKEND_CORS_ORIGINS=http://localhost:8080,http://127.0.0.1:8080,http://localhost:1420,http://127.0.0.1:1420

TDENGINE_HOST=host.docker.internal
TDENGINE_PORT=6041
TDENGINE_USER=root
TDENGINE_PASSWORD=你的 TDengine 密码
TDENGINE_DATABASE=device_data

AUTH_SECRET_KEY=换成一串足够长的随机密钥
```

> **`AUTH_SECRET_KEY` 必须改**：生成强随机串
> `& "C:\Users\Hry\.local\bin\python3.14t.exe" -c "import secrets; print(secrets.token_urlsafe(48))"`
> 若留作模板占位/默认值，后端启动会打印 WARNING，且任何人可用公开默认密钥伪造登录 token。
> 注：`backend.env.example` 仅为模板，必须复制成 `backend.env` 并填真实值后才会被容器加载。

注意：

```text
这里填写的是 MySQL / TDengine 的账号密码，不是 Windows 登录密码。
如果数据库运行在另一台服务器上，把 host.docker.internal 改成那台服务器的 IP。
BACKEND_CORS_ORIGINS 是浏览器/WebView 的跨域白名单：8080 对应 Web 前端，1420 对应 Tauri 桌面 app 开发 WebView。后端精确匹配 Origin，必须包含协议、主机和端口，末尾不要加 /。
```

### 3. 构建 Python 3.14t 基础镜像

第一次部署需要先构建后端基础镜像：

```powershell
cd D:\python\device_alarm\data_watch
docker build -t data-watch-python314t:latest -f .\deploy\python314t.Dockerfile .
```

这个步骤会编译 Python 3.14t，耗时较长，第一次可能需要几十分钟。

如果拉取 `debian:bookworm-slim` 超时，需要先配置 Docker 镜像加速器或代理。

### 4. 构建并启动前后端

默认命令只构建并启动 `frontend`、`backend`。Redis 和 TDengine 不会随代码更新一起重建或重启。

```powershell
cd D:\python\device_alarm\data_watch
docker compose -f .\deploy\docker-compose.yml up -d --build
```

如果你确实需要让 compose 临时托管 Redis / TDengine，才执行：

```powershell
docker compose -f .\deploy\docker-compose.yml --profile infra up -d redis tdengine
```

> compose 托管 TDengine 时会自动挂载 `deploy/taos.cfg`（`logKeepDays 4` / `minimalDataDirGB 50` / `sDebugFlag 131`）。数据保留期 `KEEP` 由建库语句控制（首建即 `KEEP 14d DURATION 1d`）；**若 `device_data` 库已存在**，需手动让 KEEP 生效：`docker exec data-watch-tdengine taos -s "ALTER DATABASE device_data KEEP 14d"`。详见《DataWatch目标机部署手册.md》「TDengine 保留期与磁盘控制」。

日常改前后端代码时不要执行 `--profile infra`，也不要使用 `docker compose down -v`，否则可能删除数据库数据卷。

查看容器状态：

```powershell
docker compose -f .\deploy\docker-compose.yml ps
```

查看日志：

```powershell
docker logs -f data-watch-backend
docker logs -f data-watch-frontend
```

访问前端：

```text
http://127.0.0.1:8080
```

后端接口由前端 Nginx 代理：

```text
http://127.0.0.1:8080/api/...
```

后端容器端口也映射到宿主机：

```text
http://127.0.0.1:8000
```

## 五、采集模块与 writer 启动方式

collector 和 writer 均在 Windows 主机运行，两者是独立进程，需要同时启动数据才能完整落库。

### 五.一 collector 采集模块

#### 开发阶段直接运行（源码模式）

```powershell
cd D:\python\device_alarm\data_watch\collector
```

推荐使用：

```cmd
run.bat
```

`run.bat` 会自动检测 OPC DA 协议，并在必要时启动辅助 proxy 服务，然后再拉起 collector 主进程。

如果需要跳过协议检测直接启动（调试用）：

```powershell
.\.venv\Scripts\python.exe main.py
```

#### 打包为 exe

##### 1. 准备打包虚拟环境（首次）

在 collector 目录下独立创建打包用虚拟环境，与开发用 `.venv` 分开：

```powershell
cd D:\python\device_alarm\data_watch\collector

py -3.14 -m venv .venv-pack
.\.venv-pack\Scripts\python.exe -m pip install pyinstaller
.\.venv-pack\Scripts\python.exe -m pip install -r requirements.txt
```

##### 2. 打包命令

```powershell
cd D:\python\device_alarm\data_watch\collector

.\.venv-pack\Scripts\pyinstaller.exe data-watch-collector.spec
```

打包完成后产物目录为：

```text
collector\dist\data-watch-collector\
├─ data-watch-collector.exe   ← 主程序
└─ （依赖 DLL / .pyd 文件）
```

##### 3. 部署目录结构

collector 以**当前工作目录**为基准读取 `config/settings.toml`，启动前必须先 `cd` 到部署目录。推荐结构：

```text
data-watch-collector\
├─ data-watch-collector.exe
├─ （其他 dist 文件）
└─ config\
   └─ settings.toml       ← 从 collector\config\settings.toml 复制并修改
```

将 `collector\dist\data-watch-collector\` 整个目录与 `collector\config\settings.toml` 一起拷贝到目标位置。

##### 4. 修改配置

编辑目标目录下的 `config\settings.toml`，重点修改连接地址和密码：

```toml
[mysql]
host = "目标 MySQL 地址"
password = "你的 MySQL 密码"
database = "device_watch"

[redis]
host = "127.0.0.1"
port = 6379
password = ""           # 无密码留空

[interface_ingest]
enabled = true
host = "127.0.0.1"
port = 8090
```

##### 5. 在目标电脑启动

**必须先 cd 到部署目录**（collector 以当前工作目录为基准读取 `config/settings.toml`）：

```powershell
cd <collector 部署目录>
.\data-watch-collector.exe
```

启动成功后会看到：

```text
Redis connected: 127.0.0.1:6379
MySQL connected: 127.0.0.1:3306 db=device_watch
[ingest] Listening on 127.0.0.1:8090
```

**注意**：打包的 exe 不包含 `run.bat` 的协议检测逻辑。如果现场使用 OPC DA 中心采集模式，需要手动先启动对应 proxy 服务，再运行 exe。

### 五.二 writer 模块打包与启动

writer 负责从 Redis Stream 消费数据并批量写入 TDengine 宽表。它与 collector 分开打包为独立 exe，配置文件也是独立的。

#### 1. 准备虚拟环境（首次）

writer 依赖与 collector 基本相同但更精简，建议在 writer 目录下独立创建打包用虚拟环境：

```powershell
cd D:\python\device_alarm\data_watch\writer

py -3.14 -m venv .venv-pack
.\.venv-pack\Scripts\python.exe -m pip install pyinstaller
.\.venv-pack\Scripts\python.exe -m pip install -r requirements.txt
```

#### 2. 打包为 exe

```powershell
cd D:\python\device_alarm\data_watch\writer

.\.venv-pack\Scripts\pyinstaller.exe data-watch-writer.spec
```

打包完成后输出目录为：

```text
writer\dist\data-watch-writer\
├─ data-watch-writer.exe   ← 主程序
└─ （依赖 DLL / .pyd 文件）
```

#### 3. 部署目录结构

writer 在启动时通过 `open("config/settings.toml", "rb")` 以**当前工作目录**为基准读取配置，而不是 exe 所在目录。因此启动前必须先 `cd` 到 writer 部署目录，否则会报找不到配置文件。如果通过桌面管理器或服务管理工具启动，必须将 `workingDirectory` 设为 writer 部署目录。推荐的部署目录结构：

```text
data-watch-writer\
├─ data-watch-writer.exe
├─ （其他 dist 文件）
└─ config\
   └─ settings.toml       ← 从 writer\config\settings.toml 复制并修改密码
```

将 `writer\dist\data-watch-writer\` 整个目录与 `writer\config\settings.toml` 一起拷贝到目标位置。

#### 4. 修改配置

编辑目标目录下的 `config\settings.toml`，重点修改密码和连接地址：

```toml
[redis]
host = "127.0.0.1"
port = 6379
password = ""           # 无密码留空

[mysql]
host = "目标 MySQL 地址"
password = "你的 MySQL 密码"
database = "device_watch"

[tdengine]
host = "目标 TDengine 地址"
password = "你的 TDengine 密码"
database = "device_data"

[writer]
flush_interval_ms = 500
batch_count = 200
max_buffer_rows = 2000
pending_min_idle_ms = 30000
pending_max_retry = 5
```

#### 5. 启动 writer

**必须先 cd 到部署目录**（writer 以当前工作目录为基准读取 `config/settings.toml`）：

```powershell
cd <writer 部署目录>
.\data-watch-writer.exe
```

启动成功后会看到：

```text
Redis connected: 127.0.0.1:6379 db=0
MySQL connected: 127.0.0.1:3306 db=device_watch
[tdengine] connected to 127.0.0.1:6041 db=device_data
Discovered N stream scope(s): [...]
Consuming N stream key(s): [...]
```

**注意**：writer 和 collector 必须连接同一个 Redis 实例，writer 才能消费 collector 写入的 Stream 数据。

## 六、以后更新代码后如何更新镜像

### 情况一：只改了前端代码

重新构建并启动前端：

```powershell
cd D:\python\device_alarm\data_watch
docker compose -f .\deploy\docker-compose.yml build frontend
docker compose -f .\deploy\docker-compose.yml up -d frontend
```

### 情况二：只改了后端代码

重新构建并启动后端：

```powershell
cd D:\python\device_alarm\data_watch
docker compose -f .\deploy\docker-compose.yml build backend
docker compose -f .\deploy\docker-compose.yml up -d backend
```

### 情况三：前后端都改了

```powershell
cd D:\python\device_alarm\data_watch
docker compose -f .\deploy\docker-compose.yml up -d --build
```

该命令不会启动或重建 Redis / TDengine，因为它们已经被放入 `infra` profile。

### 情况四：依赖变化或缓存异常

例如修改了：

```text
backend/requirements.txt
frontend/package.json
frontend/pnpm-lock.yaml
deploy/python314t.Dockerfile
```

建议强制无缓存构建：

```powershell
cd D:\python\device_alarm\data_watch
docker compose -f .\deploy\docker-compose.yml build --no-cache backend frontend
docker compose -f .\deploy\docker-compose.yml up -d
```

如果修改了 `deploy/python314t.Dockerfile`，还需要先重建基础镜像：

```powershell
docker build -t data-watch-python314t:latest -f .\deploy\python314t.Dockerfile .
docker compose -f .\deploy\docker-compose.yml up -d --build
```

## 七、如何导出镜像

在已经构建成功的电脑上执行：

```powershell
cd D:\python\device_alarm\data_watch
New-Item -ItemType Directory -Force .\deploy\images

docker save -o .\deploy\images\data-watch-python314t.tar data-watch-python314t:latest
docker save -o .\deploy\images\data-watch-backend.tar data-watch-backend:latest
docker save -o .\deploy\images\data-watch-frontend.tar data-watch-frontend:latest
```

默认只导出前后端程序镜像。只有目标电脑没有 Redis / TDengine，且你明确要用 compose 的 `infra` profile 托管它们时，再额外导出：

```powershell
docker save -o .\deploy\images\redis.tar redis:latest
docker save -o .\deploy\images\tdengine.tar tdengine/tsdb-amd64:3.4.0.9
```

也可以打成一个压缩包，便于拷贝：

```powershell
Compress-Archive -Path .\deploy\images\*.tar -DestinationPath .\deploy\data-watch-images.zip -Force
```

需要一起拷贝到目标电脑的内容：

```text
deploy/images/*.tar                     ← 默认只含前后端程序镜像
deploy/docker-compose.yml
deploy/backend.env
必要的 MySQL / Redis / TDengine 连接信息或数据库备份
collector/dist/data-watch-collector/    ← collector 打包产物（整个目录）
collector/config/settings.toml         ← collector 配置
writer/dist/data-watch-writer/         ← writer 打包产物（整个目录）
writer/config/settings.toml            ← writer 独立配置
```

如果 Redis / TDengine 使用宿主机已有服务或外部服务器，不要把它们的镜像和数据卷混在前后端更新包里。

如果目标电脑还需要重新构建镜像，则还要拷贝完整源码；如果只是导入镜像运行，则前后端源码不是必须项。

## 八、其他电脑如何导入镜像并启动

### 1. 安装 Docker Desktop

目标电脑需要安装并启动 Docker Desktop，且使用 Linux containers。

### 2. 放置部署文件

建议保持目录结构：

```text
D:\data_watch_deploy\
├─ deploy\
│  ├─ docker-compose.yml
│  ├─ taos.cfg                    ← TDengine 服务端配置，compose 挂载，缺失则 tdengine 起不来
│  ├─ backend.env
│  └─ images\
│     ├─ data-watch-python314t.tar
│     ├─ data-watch-backend.tar
│     └─ data-watch-frontend.tar
├─ data-watch-collector\          ← collector 打包产物（整个目录）
│  ├─ data-watch-collector.exe
│  └─ config\
│     └─ settings.toml
└─ data-watch-writer\             ← writer 打包产物（整个目录）
   ├─ data-watch-writer.exe
   └─ config\
      └─ settings.toml
```

### 3. 导入镜像

```powershell
cd D:\data_watch_deploy

docker load -i .\deploy\images\data-watch-python314t.tar
docker load -i .\deploy\images\data-watch-backend.tar
docker load -i .\deploy\images\data-watch-frontend.tar
```

如需使用 compose 的 `infra` profile 托管 Redis / TDengine，再额外导入：

```powershell
docker load -i .\deploy\images\redis.tar
docker load -i .\deploy\images\tdengine.tar
```

确认镜像已存在：

```powershell
docker images
```

应能看到：

```text
data-watch-python314t   latest
data-watch-backend      latest
data-watch-frontend     latest
```

### 4. 修改目标电脑的 backend.env

```powershell
notepad .\deploy\backend.env
```

重点修改：

```env
MYSQL_HOST=目标环境 MySQL 地址        ← MySQL 仍在宿主机时用 host.docker.internal
MYSQL_PASSWORD=目标环境 MySQL 密码
BACKEND_CORS_ORIGINS=http://localhost:8080,http://127.0.0.1:8080,http://localhost:1420,http://127.0.0.1:1420
REDIS_HOST=目标环境 Redis 地址        ← Redis 在宿主机时用 host.docker.internal
TDENGINE_HOST=目标环境 TDengine 地址  ← TDengine 在宿主机时用 host.docker.internal
TDENGINE_PASSWORD=目标环境 TDengine 密码
AUTH_SECRET_KEY=目标环境密钥
```

如果桌面 app 打包后使用了不同 Origin，应以开发者工具 Network 中请求头的 `Origin` 为准追加到 `BACKEND_CORS_ORIGINS`。

默认部署不启动 compose 内的 Redis / TDengine，`backend.env` 中对应的 HOST 应指向宿主机或外部服务器：

```env
REDIS_HOST=host.docker.internal
TDENGINE_HOST=host.docker.internal
```

只有显式使用 compose 的 `infra` profile 托管 Redis / TDengine 时，才改成服务名：

```env
REDIS_HOST=redis
TDENGINE_HOST=tdengine
```

### 5. 启动容器

默认只启动前后端：

```powershell
cd D:\data_watch_deploy
docker compose -f .\deploy\docker-compose.yml up -d
```

如果目标电脑没有 Redis / TDengine，并且确认要让 compose 托管基础设施，再额外执行：

```powershell
docker compose -f .\deploy\docker-compose.yml --profile infra up -d redis tdengine
```

查看状态：

```powershell
docker compose -f .\deploy\docker-compose.yml ps
docker logs -f data-watch-backend
docker logs -f data-watch-frontend
```

访问：

```text
http://目标电脑IP:8080
```

如果只在本机访问：

```text
http://127.0.0.1:8080
```

### 6. 检查目标浏览器内核版本

前端镜像运行时不需要目标电脑安装 Node、npm 或 pnpm，但目标电脑浏览器必须支持现代 CSS。本项目前端使用 Tailwind CSS v4，要求浏览器内核足够新：

```text
Chrome / Chromium / Edge Chromium 内核 >= 111
Firefox >= 128
Safari >= 16.4
```

不支持 IE 模式、兼容模式、旧版 360 / 搜狗 / QQ 浏览器内核。如果登录页本身已经排版混乱，但 Network 中 CSS/JS/API 请求全部是 200，优先检查浏览器内核版本。

目标电脑可在浏览器地址栏打开：

```text
chrome://version
edge://version
```

如果使用国产浏览器，需要切换到“极速模式”，并确认实际 Chromium 内核版本满足要求。Tailwind CSS v4 官方兼容说明见：[Tailwind CSS Compatibility](https://tailwindcss.com/docs/compatibility)。

## 九、数据库数据如何迁移

Docker 镜像只包含程序，不包含 MySQL、Redis 和 TDengine 的业务数据。

迁移到其他电脑时，数据库需要单独处理：

```text
MySQL      导出 / 导入 device_watch 库
TDengine   导出 / 导入 device_data 库，或迁移 TDengine 数据目录 / volume
Redis      实时缓存通常不迁移；如使用 Redis Stream 做未落库队列，停机迁移前应先确认 writer 已消费完成
```

如果目标电脑直接连接原来的数据库服务器，则不需要迁移数据库数据，只需要在 `deploy/backend.env` 中配置正确的数据库地址和账号密码。

## 十、常用运维命令

停止容器：

```powershell
docker compose -f .\deploy\docker-compose.yml down
```

重启容器：

```powershell
docker compose -f .\deploy\docker-compose.yml restart backend frontend
```

查看容器：

```powershell
docker ps
```

查看后端日志：

```powershell
docker logs -f data-watch-backend
```

查看前端日志：

```powershell
docker logs -f data-watch-frontend
```

删除旧容器但保留镜像：

```powershell
docker compose -f .\deploy\docker-compose.yml down
```

不要使用：

```powershell
docker compose -f .\deploy\docker-compose.yml down -v
```

`-v` 会删除 compose 管理的数据卷。如果曾经用 `--profile infra` 启动过 Redis / TDengine，这会带来数据丢失风险。

删除镜像：

```powershell
docker rmi data-watch-backend:latest
docker rmi data-watch-frontend:latest
docker rmi data-watch-python314t:latest
```

## 十一、注意事项

1. `backend.env` 里的数据库密码是 MySQL / TDengine / Redis 密码，不是 Windows 登录密码。
2. `host.docker.internal` 表示容器访问 Windows 宿主机。默认前后端 compose 不启动 Redis / TDengine，所以 `MYSQL_HOST`、`REDIS_HOST`、`TDENGINE_HOST` 通常保持 `host.docker.internal` 或改成外部服务器 IP。
3. 换到 Linux 服务器部署时，`host.docker.internal` 不一定可用，应改成数据库服务器 IP 或 Docker 网络服务名。
4. **只有本机首次切换到 compose 的 `infra` profile 管理 Redis/TDengine 时**，才需要先停止原来的独立容器，否则端口冲突导致 compose 启动失败：

   ```powershell
   docker stop Redis tdengine
   docker rm Redis tdengine
   docker compose -f .\deploy\docker-compose.yml --profile infra up -d redis tdengine
   ```
5. 后端必须先有 `data-watch-python314t:latest` 基础镜像，否则后端镜像无法构建。
6. 前端接口定义已自带 `/api`，生产环境 `VITE_API_URL` 应保持空，不要再使用 Apifox mock 地址，也不要设置成 `/api`。
7. 代码更新后，必须重新 build 对应镜像，否则容器运行的仍是旧代码。
8. 镜像导出导入只迁移程序环境，不迁移数据库数据。
9. 桌面 app 连接容器后端时，`BACKEND_CORS_ORIGINS` 必须包含 `http://localhost:1420` 和 `http://127.0.0.1:1420`。否则后端日志可能是 200，但 WebView 会因 CORS 拦截而显示“后端服务不可达”。
10. 修改 `deploy/backend.env` 后，不一定需要重新 build 镜像，但必须重建后端容器让环境变量生效：`docker compose -f .\deploy\docker-compose.yml up -d --force-recreate backend`。
11. collector 和 writer 均需在目标 Windows 主机上以 exe 方式运行，两者须连接同一个 Redis 实例；collector 负责写入 Stream，writer 负责消费 Stream 写入 TDengine。两者必须同时运行，数据才能落库。
12. writer 以**当前工作目录**为基准读取 `config/settings.toml`，启动前必须先 `cd` 到 writer 部署目录；通过桌面管理器或服务工具启动时须将 `workingDirectory` 设为 writer 部署目录。
13. writer 使用独立配置文件 `config/settings.toml`（位于 writer 部署目录的 config 子目录），不与 collector 共享配置文件。
14. 前端页面显示异常但所有请求都是 200 时，优先确认目标浏览器内核版本是否满足 Tailwind CSS v4 要求。
15. Redis 是实时缓冲层的必要依赖。后端容器通过 `backend.env` 中的 `REDIS_HOST` 访问 Redis，collector 和 writer 通过各自 `config/settings.toml` 中的 `[redis]` 配置访问 Redis。三者必须指向同一个 Redis 实例。
16. TDengine 宽表由 writer 在首次写入时自动创建（超级表命名为 `sd{device_type}`，如 `sdqc`、`sdasc`），无需手动运行初始化脚本。子表以 `device_code` 命名，首次写入时通过 `INSERT INTO ... USING ... TAGS` 自动建立。
17. 打包 writer 时须先在 `writer/` 目录下独立创建 `.venv-pack` 虚拟环境并安装 PyInstaller，使用 `writer/data-watch-writer.spec` 执行打包，产物在 `writer/dist/data-watch-writer/`。
