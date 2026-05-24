# 认证与权限说明

本文档详细梳理了系统的认证和权限校验流程、完整的权限码定义及其角色矩阵，并详细解析了当开启自动初始化 (`AUTH_AUTO_BOOTSTRAP=true`) 时向 MySQL 中落库的初始数据和结构。

---

## 1. JWT 登录、校验与刷新流转流程

系统认证架构基于无状态的 JWT Token，流程涵盖登录、路由/接口校验、刷新及登出。

### A. 登录流 (Login Flow)
1. **发起请求**：客户端通过 `POST /api/auth/login` 发送用户名和密码。
2. **逻辑校验**：
   - 后端 `AuthService.login()` 根据用户名查询 `sys_user`。
   - 验证用户是否存在且未删除（`deleted=0`），账户状态是否启用（`status="1"`）。
   - 提取密码散列值，利用自定义 PBKDF2 算法 `verify_password()` 执行 HMAC 比较校验。
3. **加载权限**：
   - 若校验通过，获取用户被分配且启用的角色列表（`sys_user_role` 与 `sys_role` 关联，按排序字段升序）。
   - 如果用户没有任何角色，将抛出 `403` 异常。
4. **生成 Token**：
   - 登录成功后，更新该用户的 `last_login_time` 和 `last_login_ip` 字段并提交数据库。
   - 调用 `create_signed_token()` 生成 **Access Token** 和 **Refresh Token**。这两个 Token 的签名以 `Bearer ` 为前缀。
   - Token 载荷 (Claims) 包括：`{"uid": user.id, "userName": user.user_name, "roles": [...], "type": "access"/"refresh", "iat": <timestamp>, "exp": <timestamp>}`。*注：此处 userName 对应 sys_user 表的 user_name 字段，而非 name。*
   - Access Token 的过期时间默认 8 小时；Refresh Token 的过期时间默认 7 天。
5. **返回数据**：向前端返回 JSON：`{"token": "Bearer <access_token_jwt>", "refreshToken": "Bearer <refresh_token_jwt>"}`。

### B. 接口访问验证流 (API Verification Flow)
1. **携带凭证**：前端 HTTP 请求拦截器（`frontend/src/utils/http/index.ts`）在发送请求时，自动从 Pinia 状态管理器中读取 `accessToken` 并挂载在请求头的 `Authorization` 字段中。
2. **中间件拦截**：
   - 路由接收到请求，若路由方法中指定了权限依赖（如使用 `require_permissions(credentials, "permission_code")`）。
   - `require_permissions` 将解析 `Authorization` 请求头并传递至 `resolve_principal()`。
3. **Token 解析与完整性校验**：
   - `resolve_principal` 通过 `decode_signed_token` 去除 Bearer 前缀并解开 JWT 格式。
   - 检验 HMAC-SHA256 签名，判断 `exp` 时间戳是否已过期。
   - 确保载荷中声明的 `type` 为 `"access"`。
   - 根据载荷中的 `uid` 查询 `sys_user` 表，验证当前用户依然存在且未被删除（不满足则抛出 401）；如果用户状态不启用（`status != "1"`），则抛出的是 `ApiError(403, "当前账号不可用，请联系管理员")` 异常。
4. **权限判定**：
   - `require_permissions` 会提取该用户的最终有效权限码集合（通过 `fetch_user_permission_codes` 联合 `sys_user_role` -> `sys_role_permission` -> `sys_permission` 查询得到）。
   - 判定 API 要求的权限集合是否与用户拥有的权限集合存在交集。若无交集，则抛出 `403` 异常（提示“无权限访问该接口”）。

### C. Token 刷新流 (Token Refresh Flow)
*   **特别说明**：虽然系统在登录时返回了 `refreshToken`，但经源码排查，目前后端 API 路由与前端 HTTP 请求客户端**均未实现**自动刷新 Token 的路由（即无 `/api/auth/refresh` 接口或静默刷新拦截器）。
*   当 Access Token 过期时，后端会直接返回 401 错误。
*   前端响应拦截器检测到 HTTP 状态码为 401 时，将触发 `handleUnauthorizedError()`，清除用户的登录态，并在延迟 500ms 后强制将用户登出，重定向回 `/login` 登录界面。

### D. 登出流 (Logout Flow)
1. 发起 `POST /api/auth/logout` 请求。
2. 后端尝试根据请求头的 `Authorization` 获取当前用户信息：
   - 若 Token 有效，写入系统审计日志：`登出成功: <用户名>`。
   - 若 Token 无效，仍写入日志但标记用户信息为空：`登出（无有效登录状态）`。
3. 前端收到成功响应后，彻底清空本地 Pinia 与 Web Storage 里的 Token 数据。

---

## 2. 权限码角色矩阵表

系统目前拥有的所有 **40 个权限码**，以及各预设角色（`R_SUPER`、`R_ADMIN`、`R_USER`）的授权情况和 API 路由校验关系如下：

| 权限码 | 权限名称 | 类型 | 关联菜单 | R_SUPER | R_ADMIN | R_USER | API 路由校验详情 |
| :--- | :--- | :--- | :--- | :---: | :---: | :---: | :--- |
| **设备监控业务相关 (device_monitor)** | | | | | | | |
| `device_monitor:dashboard:view` | 工作台查看 | page | MonitorDashboard | **✓** | **✓** | **✓** | 校验于 dashboard 各统计、图表 API |
| `device_monitor:monitor:view` | 监控卡片查看 | page | DeviceMonitorCard | **✓** | **✓** | ── | 校验于设备树与实时数据窗口 API |
| `device_monitor:driver:view` | 驱动管理查看 | page | DriverManage | **✓** | **✓** | ── | 校验于驱动列表和选项接口 |
| `device_monitor:device:view` | 设备管理查看 | page | DeviceManage | **✓** | **✓** | ── | 校验于设备列表、选项、统计等接口 |
| `device_monitor:device:create` | 设备新增 | action | DeviceManage | **✓** | **✓** | ── | 校验于 `POST /device/create` |
| `device_monitor:device:update` | 设备编辑 | action | DeviceManage | **✓** | **✓** | ── | 校验于 `PUT /device/update` |
| `device_monitor:device:delete` | 设备删除 | action | DeviceManage | **✓** | **✓** | ── | 校验于 `DELETE /device/delete` 及其批量接口 |
| `device_monitor:device:import` | 设备导入 | action | DeviceManage | **✓** | **✓** | ── | 校验于 `POST /device/import` |
| `device_monitor:device:export` | 设备导出 | action | DeviceManage | **✓** | **✓** | ── | 校验于 `POST /device/export` |
| `device_monitor:point:view` | 点位管理查看 | page | PointManage | **✓** | **✓** | ── | 校验于点位列表、属性、详情接口 |
| `device_monitor:point:create` | 点位新增 | action | PointManage | **✓** | **✓** | ── | 校验于 `POST /point/create` |
| `device_monitor:point:update` | 点位编辑 | action | PointManage | **✓** | **✓** | ── | 校验于 `PUT /point/update` |
| `device_monitor:point:delete` | 点位删除 | action | PointManage | **✓** | **✓** | ── | 校验于 `DELETE /point/delete` 及其批量接口 |
| `device_monitor:point:import` | 点位导入 | action | PointManage | **✓** | **✓** | ── | 校验于 `POST /point/import` 及其模板接口 |
| `device_monitor:point:export` | 点位导出 | action | PointManage | **✓** | **✓** | ── | 校验于 `POST /point/export` |
| `device_monitor:realtime:view` | 实时监控查看 | page | RealtimeMonitor | **✓** | **✓** | **✓** | 校验于实时监控初始化与实时数据窗口 API |
| `device_monitor:realtime:subscribe` | 实时监控订阅 | action | RealtimeMonitor | **✓** | **✓** | **✓** | *仅作前端菜单/页面按钮元素控制用* |
| `device_monitor:realtime:refresh` | 实时监控刷新 | action | RealtimeMonitor | **✓** | **✓** | **✓** | *仅作前端按钮级别控制用* |
| `device_monitor:history:view` | 历史数据查看 | page | HistoryData | **✓** | **✓** | **✓** | 校验于历史数据查询、时序参数接口 |
| `device_monitor:history:export` | 历史数据导出 | action | HistoryData | **✓** | **✓** | ── | 校验于历史数据导出 Excel、查询导出状态接口 |
| `device_monitor:log:view` | 系统日志查看 | page | SystemLog | **✓** | **✓** | **✓** | 校验于日志列表查询与日志级别统计接口 |
| `device_monitor:log:export` | 系统日志导出 | action | SystemLog | **✓** | ── | ── | *仅内置于初始化库，无后端 API 显式校验* |
| `device_monitor:log:cleanup` | 系统日志清理 | action | SystemLog | **✓** | ── | ── | 校验于 `POST /log/cleanup` |
| `device_monitor:log:policy` | 系统日志策略配置 | action | SystemLog | **✓** | ── | ── | 校验于 `POST /log/policy` |
| `device_monitor:log:debug` | 系统日志DEBUG开关 | action | SystemLog | **✓** | ── | ── | 校验于 `POST /log/debug` (开/关服务日志调试) |
| `device_monitor:log_terminal:view` | 日志终端查看 | page | LogTerminal | **✓** | **✓** | **✓** | *仅作前端菜单/页面入口控制用* |
| **系统管理相关 (system)** | | | | | | | |
| `system:user:view` | 用户管理查看 | page | User | **✓** | ── | ── | 校验于用户列表及角色选项接口 |
| `system:user:create` | 用户新增 | action | User | **✓** | ── | ── | 校验于 `POST /user/create` |
| `system:user:update` | 用户编辑 | action | User | **✓** | ── | ── | 校验于 `PUT /user/update` 与状态变更接口 |
| `system:user:delete` | 用户删除 | action | User | **✓** | ── | ── | 校验于 `DELETE /user/delete` |
| `system:user:password_reset` | 用户密码重置 | action | User | **✓** | ── | ── | 校验于 `POST /user/password-reset` |
| `system:role:view` | 角色管理查看 | page | Role | **✓** | ── | ── | 校验于角色列表、分配用户/菜单详情接口 |
| `system:role:create` | 角色新增 | action | Role | **✓** | ── | ── | 校验于 `POST /role/create` |
| `system:role:update` | 角色编辑 | action | Role | **✓** | ── | ── | 校验于 `PUT /role/update` 与状态变更接口 |
| `system:role:delete` | 角色删除 | action | Role | **✓** | ── | ── | 校验于 `DELETE /role/delete` |
| `system:role:grant` | 角色授权 | action | Role | **✓** | ── | ── | 校验于 `POST /role/grant` |
| `system:menu:view` | 菜单管理查看 | page | Menus | **✓** | ── | ── | 校验于菜单列表与树状图接口 |
| `system:menu:create` | 菜单新增 | action | Menus | **✓** | ── | ── | 校验于 `POST /menu/create` |
| `system:menu:update` | 菜单编辑 | action | Menus | **✓** | ── | ── | 校验于 `PUT /menu/update` |
| `system:menu:delete` | 菜单删除 | action | Menus | **✓** | ── | ── | 校验于 `DELETE /menu/delete` |

---

## 3. Bootstrap 自动初始化落库行为与数据说明

当后端配置文件或系统环境变量中 `AUTH_AUTO_BOOTSTRAP` 设置为 `True` 时（系统默认值即为 `True`），服务在每次启动时的 Lifespan 阶段都会自动检测并修复/初始化 MySQL 数据。

### A. 执行机制与规则
- **非破坏性更新**：如果表内已经存在同名的用户、角色、菜单或权限，初始化逻辑将**不会覆盖**这些已有的记录，而是采取增量写入或空字段补齐机制。
- **建表行为**：自动通过 SQLAlchemy 的 `Base.metadata.create_all` 生成所有系统角色权限表（如数据库内不存在对应表）。

### B. 初始化写入数据详情
当系统首次在空白库启动时，将自动向 MySQL 注入以下核心数据：

1. **预设角色 (`sys_role` 表)**
   - `R_SUPER`：超级管理员，描述为“系统内置超级管理员，拥有全部权限”。
   - `R_ADMIN`：运维管理员，描述为“设备监控业务管理员，拥有设备监控权限”。*注：根据 `auth_bootstrap.py` 的定义，R_ADMIN 并不包含 system 相关的系统管理权限（如用户/角色/菜单管理），因此实际在系统内不能执行用户管理。*
   - `R_USER`：监控用户，描述为“普通监控账号，仅保留只读查看能力”。

2. **系统用户 (`sys_user` 表)**
   - 生成 3 个预设账号，默认密码哈希值通过 `hash_password("123456")` 计算而来（密码统一为 `123456`），状态均激活（`status="1"`）：
     - `Super`：超级管理员（关联 `R_SUPER` 角色，邮箱：`super@device-monitor.local`，电话：`13800000001`）
     - `Admin`：运维管理员（关联 `R_ADMIN` 角色，邮箱：`admin@device-monitor.local`，电话：`13800000002`）
     - `User`：监控用户（关联 `R_USER` 角色，邮箱：`user@device-monitor.local`，电话：`13800000003`）

3. **用户角色映射 (`sys_user_role` 表)**
   - 建立对应的关联条目，例如 `User(Super).id` <-> `Role(R_SUPER).id`。

4. **菜单数据 (`sys_menu` 表)**
   - 插入系统完整的树形菜单元数据（包含 Dashboard、DeviceMonitor 监控管理核心菜单、Widgets、Examples、Article、Safeguard 及 System 系统管理模块）。

5. **角色菜单授权 (`sys_role_menu` 表)**
   - 根据预置规则配置不同角色可访问的前端页面树：
     - `R_SUPER`：获得全部菜单项访问权。
     - `R_ADMIN`：仅能访问 `DeviceMonitor` 主菜单及其下辖的所有 9 个子菜单页面。
     - `R_USER`：仅允许访问工作台、实时监控、历史数据、系统日志、日志终端这 5 个页面。

6. **角色权限绑定 (`sys_role_permission` 表)**
   - `R_SUPER`：自动绑定所有 40 个权限码。
   - `R_ADMIN`：绑定所有 `device_monitor:` 开头的权限码，但出于安全性考虑，**剔除**了系统日志清理（`device_monitor:log:cleanup`）、策略配置（`device_monitor:log:policy`）以及 DEBUG 开关（`device_monitor:log:debug`）权限。
   - `R_USER`：仅绑定部分工作台查看、实时监控订阅/刷新、历史数据查看和系统日志查看权限。
   - **页面权限自动补齐 (`_seed_role_menu_page_permissions`)**：系统会自动查询所有 `type="page"`（页面级查看）的权限，如果其关联的菜单在该角色的 `sys_role_menu` 列表中，则自动把该权限赋权给该角色。

7. **数据 scope 范围定义 (`sys_data_permission` 表)**
   - 给三个预设角色均写入了一条默认的全局数据权限：`scope_type="all"`，`scope_value="*"`，`enabled=True`。表示所有用户默认拥有所有点位和设备的数据可见性。

---

## 4. 相关源码参考

*   **安全认证核心**：[security.py](file:///D:/python/device_alarm/data_watch/backend/app/core/security.py)
*   **权限初始化种子**：[auth_bootstrap.py](file:///D:/python/device_alarm/data_watch/backend/app/services/auth_bootstrap.py)
*   **API 拦截依赖**：[permissions.py](file:///D:/python/device_alarm/data_watch/backend/app/api/permissions.py)
*   **业务逻辑实现**：[auth.py](file:///D:/python/device_alarm/data_watch/backend/app/services/auth.py)

