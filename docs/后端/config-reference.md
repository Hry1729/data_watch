# 配置参考

本文档详细说明了工业设备监控系统（Data Watch）后端服务的配置加载优先级、所有 30+ 项配置参数，并提供了本地最小化部署所需的必填环境变量清单。

---

## 1. 配置加载优先级

后端配置是由 [config.py](file:///D:/python/device_alarm/data_watch/backend/app/core/config.py) 中的 `get_settings()` 函数统一加载的。配置值遵循以下五个层级的覆盖顺序（优先级从高到低）：

1. **系统环境变量 (System Environment Variables)**
   - 调用 `os.getenv(name)`，具有最高优先级。会直接覆盖任何从配置文件或默认值加载的参数。
2. **本地环境配置文件 `backend/.env`**
   - 如果该文件存在，将解析其中的 `KEY=VALUE` 健值对，并覆盖优先级更低的配置。
3. **环境模板文件 `backend/.env.example`**
   - 仅用于提供开发环境的默认占位符。在读取 `backend/.env` 前会先尝试读取此文件，如果 `.env` 中有相同的键，`.env` 的值会覆盖 `.env.example`。
4. **采集器配置文件 `collector/config/settings.toml`**
   - 后端会尝试读取同级目录下的 [settings.toml](file:///D:/python/device_alarm/data_watch/collector/config/settings.toml)（即公共配置文件），提取 `[runtime]`, `[mysql]` 和 `[tdengine]` 区块的内容。若 `.env` 和系统变量中均未配置相应参数，则使用该文件中的对应项（如 MySQL 密码等）。*注：Redis 相关配置项不受此文件影响，直接使用系统环境变量、`.env` 或代码硬编码默认值。*
5. **代码默认值 (Hardcoded Python Defaults)**
   - 当上述配置文件或系统环境变量中均不存在某一配置时，退化为使用 Python 代码中声明的硬编码默认值。

---

## 2. 39项详细配置参数列表

以下是系统目前支持的所有 39 项配置项详情表：

| 属性名 | 对应环境变量名 | 类型 | 默认值来源及默认值 | 参数说明 |
| :--- | :--- | :--- | :--- | :--- |
| `app_name` | `BACKEND_APP_NAME` | `str` | 代码默认值: `"Industrial Device Monitor Backend"` | 后端应用名称 |
| `app_version` | `BACKEND_APP_VERSION` | `str` | 代码默认值: `"0.1.0"` | 后端应用版本号 |
| `log_level` | `BACKEND_LOG_LEVEL` | `str` | `settings.toml` 的 `[runtime].log_level` (默认 `"INFO"`) | 系统日志打印级别 (`DEBUG`, `INFO`, `WARN`, `ERROR` 等) |
| `api_prefix` | `BACKEND_API_PREFIX` | `str` | 代码默认值: `"/api/device-monitor"` | 后端 API 路由的公共前缀 |
| `host` | `BACKEND_HOST` | `str` | 代码默认值: `"0.0.0.0"` | 服务监听的 IP 地址 |
| `port` | `BACKEND_PORT` | `int` | 代码默认值: `8000` | 服务监听的端口号 |
| `cors_origins` | `BACKEND_CORS_ORIGINS` | `list[str]` | 逗号分隔解析，默认: `["http://localhost:3000", "http://localhost:5173", "http://127.0.0.1:5173"]` | 跨域资源共享 (CORS) 允许的源站白名单列表 |
| `mysql_host` | `MYSQL_HOST` | `str` | `settings.toml` 的 `[mysql].host` (默认 `"127.0.0.1"`) | MySQL 数据库连接 Host |
| `mysql_port` | `MYSQL_PORT` | `int` | `settings.toml` 的 `[mysql].port` (默认 `3306`) | MySQL 数据库连接端口 |
| `mysql_user` | `MYSQL_USER` | `str` | `settings.toml` 的 `[mysql].user` (默认 `"root"`) | MySQL 数据库用户名 |
| `mysql_password` | `MYSQL_PASSWORD` | `str` | `settings.toml` 的 `[mysql].password` (若未配置或文件不存在，硬编码退化值为 `""`) | MySQL 数据库密码 |
| `mysql_database` | `MYSQL_DATABASE` | `str` | `settings.toml` 的 `[mysql].database` (默认 `"device_watch"`) | MySQL 业务数据库名 |
| `mysql_charset` | `MYSQL_CHARSET` | `str` | `settings.toml` 的 `[mysql].charset` (默认 `"utf8mb4"`) | MySQL 数据库字符集 |
| `tdengine_host` | `TDENGINE_HOST` | `str` | 代码默认值: `"127.0.0.1"` | TDengine 时序数据库 Host |
| `tdengine_port` | `TDENGINE_PORT` | `int` | 代码默认值: `6041` | TDengine 客户端 RESTful/WebSocket 服务连接端口 |
| `tdengine_user` | `TDENGINE_USER` | `str` | 代码默认值: `"root"` | TDengine 数据库用户名 |
| `tdengine_password` | `TDENGINE_PASSWORD` | `str` | 代码默认值: `""` | TDengine 数据库密码 |
| `tdengine_database` | `TDENGINE_DATABASE` | `str` | 代码默认值: `"device_data"` | TDengine 存储设备时序数据的数据库名 |
| `tdengine_use_ssl` | `TDENGINE_USE_SSL` | `bool` | 代码默认值: `False` (由 `1/true/yes` 解析) | TDengine 连接是否启用 SSL 安全证书 |
| `perspective_enabled` | `PERSPECTIVE_ENABLED` | `bool` | 代码默认值: `True` (由 `1/true/yes` 解析) | 是否启用 Perspective 实时流数据聚合分析 WebSocket 引擎 |
| `perspective_ws_path` | `PERSPECTIVE_WS_PATH` | `str` | 代码默认值: `"/ws/perspective"` | Perspective 客户端连接的 WebSocket 子路由路径 |
| `perspective_max_clients` | `PERSPECTIVE_MAX_CLIENTS` | `int` | 代码默认值: `200` | Perspective 最大并发客户端连接数 |
| `perspective_session_idle_ttl_seconds` | `PERSPECTIVE_SESSION_IDLE_TTL_SECONDS` | `int` | 代码默认值: `900` | Perspective 会话空闲最大存活秒数 (默认 15 分钟) |
| `perspective_session_restore_ttl_seconds` | `PERSPECTIVE_SESSION_RESTORE_TTL_SECONDS` | `int` | 代码默认值: `300` | 客户端掉线后允许重新连回恢复 Perspective 会话的超时秒数 (默认 5 分钟) |
| `perspective_max_sessions_per_owner` | `PERSPECTIVE_MAX_SESSIONS_PER_OWNER` | `int` | 代码默认值: `3` | 每个用户身份标识所能拥有的最大并发 Perspective 分析会话数 |
| `perspective_max_sessions_global` | `PERSPECTIVE_MAX_SESSIONS_GLOBAL` | `int` | 代码默认值: `100` | 全局允许开辟的最大 Perspective 会话数限制 |
| `perspective_max_points_per_query` | `PERSPECTIVE_MAX_POINTS_PER_QUERY` | `int` | 代码默认值: `20` | Perspective 单词查询限制的最大点位数 |
| `perspective_max_range_hours` | `PERSPECTIVE_MAX_RANGE_HOURS` | `int` | 代码默认值: `24` | 允许查询 Perspective 时序分析的最大历史范围长度 (小时) |
| `perspective_max_records_per_query` | `PERSPECTIVE_MAX_RECORDS_PER_QUERY` | `int` | 代码默认值: `200000` | Perspective 查询最大返回记录数防止大查询导致内存溢出 |
| `perspective_ping_interval_seconds` | `PERSPECTIVE_PING_INTERVAL_SECONDS` | `int` | 代码默认值: `15` | Perspective 传输心跳包 (Ping/Pong) 间隔 (秒) |
| `perspective_timeout_seconds` | `PERSPECTIVE_TIMEOUT_SECONDS` | `int` | 代码默认值: `45` | Perspective 会话交互的超时阈值 (秒) |
| `auth_secret_key` | `AUTH_SECRET_KEY` | `str` | 代码默认值: `"device-monitor-dev-secret"` | JWT Token 加密签名的散列密钥，**生产环境必须替换** |
| `auth_access_token_expire_seconds` | `AUTH_ACCESS_TOKEN_EXPIRE_SECONDS` | `int` | 代码默认值: `28800` | 登录 Access Token 有效时长 (秒，默认 8 小时) |
| `auth_refresh_token_expire_seconds` | `AUTH_REFRESH_TOKEN_EXPIRE_SECONDS` | `int` | 代码默认值: `604800` | 登录 Refresh Token 有效时长 (秒，默认 7 天) |
| `auth_auto_bootstrap` | `AUTH_AUTO_BOOTSTRAP` | `bool` | 代码默认值: `True` (由 `1/true/yes` 解析) | 是否在服务启动时自动检查并写入/补齐默认的 MySQL 用户、角色与权限元数据 |
| `redis_host` | `REDIS_HOST` | `str` | 代码默认值: `"127.0.0.1"` | 实时数据缓存 Redis 的连接 Host |
| `redis_port` | `REDIS_PORT` | `int` | 代码默认值: `6379` | 实时数据缓存 Redis 的连接端口 |
| `redis_db` | `REDIS_DB` | `int` | 代码默认值: `0` | 默认选用的 Redis 数据库索引号 |
| `redis_password` | `REDIS_PASSWORD` | `str` | 代码默认值: `""` | Redis 连接密码 |

### 动态计算属性 (Computed DSN)

后端代码还会基于以上基本属性动态计算以下只读连接配置：
*   **MySQL 异步 DSN (`mysql_async_dsn`)**:
    `mysql+aiomysql://{mysql_user}:{mysql_password}@{mysql_host}:{mysql_port}/{mysql_database}?charset={mysql_charset}`
*   **TDengine WebSocket DSN (`tdengine_ws_dsn`)**:
    `ws://{tdengine_user}:{tdengine_password}@{tdengine_host}:{tdengine_port}/{tdengine_database}` (如果开启 `tdengine_use_ssl`，则协议头自动调整为 `wss`)
*   **Perspective WebSocket 连接路由 (`perspective_ws_route`)**:
    `{api_prefix}/{perspective_ws_path}` (保证以 `/` 开头)

---

## 3. 本地最小部署必填环境变量清单

为了在本地搭建开发或测试环境，如果你的外部组件（如 MySQL, TDengine, Redis）使用非默认值，或服务使用 Docker 进行桥接部署，请务必建立 `backend/.env` 并按需指定以下环境变量：

### A. 数据库连接设置
*   **`MYSQL_PASSWORD`**：本地 MySQL 的实际密码。如果 `settings.toml` 不存在或未定义密码，系统将退化使用空密码 `""`。如果你的本地密码不同，请在 `.env` 中修改：
    ```env
    MYSQL_PASSWORD=your_mysql_password
    ```
*   **`TDENGINE_PASSWORD`**：如果你的本地 TDengine 密码为非空（如官方默认的 `taosdata` 或自定义密码）：
    ```env
    TDENGINE_PASSWORD=your_tdengine_password
    ```
*   **`REDIS_PASSWORD`**：如果你的 Redis 服务开启了密码校验：
    ```env
    REDIS_PASSWORD=your_redis_password
    ```

### B. Docker Compose 容器部署设置
如果你使用 Docker 部署后端服务，而数据库安装在 Windows 宿主机上，需修改各组件的 Host 指向桥接网卡：
```env
MYSQL_HOST=host.docker.internal
TDENGINE_HOST=host.docker.internal
REDIS_HOST=host.docker.internal
```

### C. 安全与权限（推荐项）
*   **`AUTH_SECRET_KEY`**：为了保证 JWT token 签名的绝对安全，在部署时必须随机生成一个密钥（可以通过命令行工具生成），避免使用默认 of `"device-monitor-dev-secret"`：
    ```env
    AUTH_SECRET_KEY=b38d3b17b23c791f2c9744f9346a7c0138330b26f69233f53ea86bdb30a2970e
    ```
