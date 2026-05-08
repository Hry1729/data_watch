# 数据库开发 TODO

> 版本：2026-04-24
> 范围：MySQL 主数据 + 权限域 + TDengine 时序数据
> 当前状态：auth 权限域、种子数据与真实登录闭环已完成验证；报警 / 日志 6 张表已完成结构设计与 SQL 基线；模板同步与跨库补偿结构已完成；点位主键已统一收口到 `var_seq`，当前主任务转为模板验证与后端联调

---

## 一、数据库整体开发内容

当前数据库模块的整体开发范围如下：

### 1.1 MySQL（配置、主数据与权限）

- 设备类型管理：`device_type`
- 设备分组管理：`device_group`
- 驱动 / 协议字典：`comm_protocol`
- 设备管理：`device`
- 点位管理：`device_variable`
- 用户权限：`sys_user`、`sys_role`、`sys_user_role`、`sys_menu`、`sys_permission`、`sys_role_menu`、`sys_role_permission`、`sys_data_permission`
- 后续待开发：统计聚合、系统配置、模板同步增强、跨库补偿 worker

### 1.2 TDengine（实时与历史时序）

- 按设备类型分库：`qc` / `asc` / `agv`
- 按设备编码创建超级表
- 按变量创建子表
- 支持实时最新值查询
- 支持历史时间范围查询
- 支持连续聚合视图

### 1.3 与业务开发的对接目标

- collector 从 MySQL 读取设备、点位和权限相关主数据
- collector 写入 TDengine
- backend 从 MySQL 读取主数据、登录账号、角色菜单和权限
- backend 从 TDengine 读取实时值与历史值
- frontend 基于真实接口完成设备监控与系统管理联调
- Dashboard / Log 统计页基于 MySQL 主数据、报警样本、日志样本形成真实聚合结果

---

## 二、当前开发进度

### 2.1 已完成

| 编号 | 内容 | 状态 | 说明 |
| :--- | :--- | :--- | :--- |
| DB-TODO-001 | 明确 MySQL / TDengine 职责边界 | ✅ 已完成 | MySQL 管主数据与权限，TDengine 管实时与历史 |
| DB-TODO-002 | 落地第一批 MySQL 主数据表 | ✅ 已完成 | 已建 `device_type`、`device_group`、`comm_protocol`、`device`、`device_variable` |
| DB-TODO-003 | 统一协议命名 | ✅ 已完成 | 已统一为 `SNAP7`、`GE SRTP`、`OPC-DA`、`OPC-UA`、`MODBUS` |
| DB-TODO-004 | 设备变量表改为 `device_id` 关联 | ✅ 已完成 | 已取消 `device_code` 关联口径 |
| DB-TODO-005 | 删除 `device_variable.group_id` | ✅ 已完成 | 当前批量导入功能未实施，先移除冗余字段 |
| DB-TODO-006 | 补齐主键 / 唯一约束 / 外键 / 关键索引 | ✅ 已完成 | 已直接更新本机 `device_watch` 数据库 |
| DB-TODO-007 | 同步数据库结构设计文档 | ✅ 已完成 | 已更新 `docs/数据库/数据库结构v3.md` |

### 2.2 进行中

| 编号 | 内容 | 状态 | 说明 |
| :--- | :--- | :--- | :--- |
| DB-TODO-008 | 用当前 MySQL 结构支撑设备 / 点位管理接口 | 🔄 进行中 | 设备、点位主表已具备，但 `device/group-tree`、`device/list`、`point/device-tree`、`point/list` 等真实接口仍待后端补齐 |
| DB-TODO-009 | 建立 MySQL 与 TDengine 的稳定映射规则 | 🔄 进行中 | collector / backend 已切到 `device_code + '_' + var_seq` 新口径，当前保留 legacy 回退以支撑历史数据平滑迁移 |
| DB-TODO-010 | 收口设备状态与实时在线状态语义 | 🔄 进行中 | 当前 `device.status` 先保留，后续再判断是否拆分 |
| DB-TODO-011 | 落地报警表 | ✅ 已完成 | 4 张报警表已完成 migration 与 live schema 校验，后端真实查询已就绪 |
| DB-TODO-012 | 落地日志表 | ✅ 已完成 | 2 张日志表已完成 migration 与 live schema 校验，日志策略表已落地，后端真实查询已就绪 |
| DB-TODO-013 | 落地权限表与登录权限闭环 | 🔄 进行中 | auth 结构、seed、`login / user/info / menus` 已打通，待切 `backend` 模式并完成页面回归 |
| DB-TODO-015 | 变量模板 / 单位字典 / 模板同步能力扩展 | 🔄 进行中 | `unit_dict`、`variable_template`、`template_variable`、`cross_db_task` 已完成 migration / seed / verify，`template_variable` 已按 `var_seq` 收口，待模板抽取验证与同步服务接入 |
| DB-TODO-016 | 形成正式 DDL / 迁移脚本基线 | 🔄 进行中 | auth 域与报警 / 日志域 SQL 已沉淀，业务主表仍待统一为正式 DDL / migration |
| DB-TODO-017 | 补齐 Dashboard / Log 联调用统计 seed 与汇总口径 | 🔄 进行中 | 报警 / 日志样本已写入 live schema，待配合后端验证统计口径 |

### 2.3 未开始

| 编号 | 内容 | 状态 | 说明 |
| :--- | :--- | :--- | :--- |
| DB-TODO-014 | 落地系统配置表 | 📋 未开始 | `sys_config`、`shift_config` |

### 2.4 `DB-TODO-013` 权限专项拆分

| 子任务 | 内容 | 状态 | 说明 |
| :--- | :--- | :--- | :--- |
| DB-TODO-013-01 | 固定权限域 8 张表字段、索引、约束 | ✅ 已完成 | 已在 ORM 与数据库结构文档中落地 |
| DB-TODO-013-02 | 补齐 `sys_user_role` 多角色关联 | ✅ 已完成 | 已对齐前端 `userRoles: string[]` |
| DB-TODO-013-03 | 输出角色 / 用户 / 菜单 / 权限种子方案 | ✅ 已完成 | 已内置 `R_SUPER / R_ADMIN / R_USER` 与 `Super / Admin / User` |
| DB-TODO-013-04 | 打通登录闭环所需数据基础 | ✅ 已完成 | `POST /api/auth/login`、`GET /api/user/info` 已有真实数据源 |
| DB-TODO-013-05 | 打通 backend 模式菜单数据源 | ✅ 已完成 | `/api/v3/system/menus` 已可基于 `sys_role_menu + sys_role_permission` 输出菜单树 |
| DB-TODO-013-06 | 支撑用户 / 角色 / 菜单 / 权限管理 CRUD 的主数据结构 | ✅ 已完成 | 关联表、唯一约束和外键已在代码层具备 |
| DB-TODO-013-07 | 输出正式 MySQL DDL / migration 基线 | ✅ 已完成 | 已新增 auth schema / seed / verify / migration SQL，并在本机库落地 |
| DB-TODO-013-08 | 完成 frontend / backend 权限模式切换验收 | 📋 未开始 | 菜单接口验收通过后再切 `VITE_ACCESS_MODE=backend` |

### 2.5 `DB-TODO-011 / 012 / 017` 告警日志专项拆分

| 子任务 | 内容 | 状态 | 说明 |
| :--- | :--- | :--- | :--- |
| DB-TODO-011-01 | 固定报警域 4 张表字段、约束与索引 | ✅ 已完成 | `alarm_level / alarm_rule / alarm_record / alarm_active` 已完成正式结构设计 |
| DB-TODO-011-02 | 固定 `alarm_record=历史`、`alarm_active=当前` 语义 | ✅ 已完成 | 已明确活动告警快照不再等同于“最近告警” |
| DB-TODO-011-03 | 输出报警域 schema / seed / verify / migration SQL | ✅ 已完成 | 已新增 `alarm_log_*_2026_04_20.sql` 脚本 |
| DB-TODO-011-04 | 执行报警 migration 并校验 live schema | ✅ 已完成 | 已执行到本机 `device_watch`，verify 通过 |
| DB-TODO-012-01 | 固定日志域 2 张表字段、约束与索引 | ✅ 已完成 | `operation_log / system_log` 已完成正式结构设计 |
| DB-TODO-012-02 | 固定 `operation_log + system_log` 联合查询口径 | ✅ 已完成 | `log/list` 继续保持分页、时间、级别、类型筛选 |
| DB-TODO-012-03 | 输出日志域 schema / seed / verify / migration SQL | ✅ 已完成 | 与报警域共用 `alarm_log_*_2026_04_20.sql` 脚本 |
| DB-TODO-012-04 | 执行日志 migration 并校验 live schema | ✅ 已完成 | 已执行到本机 `device_watch`，verify 通过 |
| DB-TODO-012-05 | 落地系统日志清理策略、查询索引与权限增量优化 | ✅ 已完成 | 2026-05-06 已新增 `log_retention_policy`，补 5 个日志查询索引，新增 `log:export/cleanup/policy/debug` 权限并校验旧数据保留 |
| DB-TODO-017-01 | 准备 Dashboard 告警侧联调样本 | ✅ 已完成 | 正式 seed 已覆盖等级、类型、状态、近 24h / 近 7d 告警样本 |
| DB-TODO-017-02 | 准备 Log 页联调样本 | ✅ 已完成 | 正式 seed 已覆盖操作日志与系统运行日志样本 |
| DB-TODO-017-03 | 联动后端验证 `overview / recent-events / alarm-distribution / log/list` | 📋 未开始 | live schema 已就绪，待后端切真实查询后做接口级验收 |

---

## 三、今天的更新内容（2026-04-23）

截至 2026-04-24 已完成以下数据库相关更新：

### 3.1 变量模板 / 单位字典 / 跨库补偿结构设计完成

- 已完成 4 类核心结构设计：
  - `unit_dict`
  - `variable_template`
  - `template_variable`
  - `cross_db_task`
- 已固定当前关键语义：
  - `template_variable` 与 `device_variable` 业务字段同构
  - 运行时主表继续使用 `device_variable`
  - 模板采用“首次应用 + 显式重同步”
  - `unit_id` 为可空公共字段
  - `var_seq` 为两张点位表同步新增的公共字段
  - `cross_db_task` 用于跨 MySQL / TDengine 的异步补偿

### 3.2 模板同步 SQL 基线已输出

- 已新增：
  - `docs/数据库/sql/template_sync_schema_2026_04_23.sql`
  - `docs/数据库/sql/template_sync_seed_2026_04_23.sql`
  - `docs/数据库/sql/template_sync_verify_2026_04_23.sql`
  - `docs/数据库/sql/template_sync_migration_2026_04_23.sql`
- 当前脚本已覆盖：
  - schema
  - 单位字典 seed
  - verify
  - 自包含 migration
- 当前重复执行策略已固定：
  - `unit_dict` 通过 `unit_code` upsert
  - 模板头 / 模板点位不在 seed 中预置固定业务数据
  - 结构迁移只补列、索引、外键、触发器，不删除现有点位业务数据

### 3.3 文档同步完成

- 已更新数据库结构文档：`docs/数据库/数据库结构v3.md`
- 已更新数据库开发 TODO：`docs/数据库/数据库开发TODO.md`
- 已更新数据库优化清单：`docs/数据库/数据库优化.md`
- 已将模板扩展从“规划表名”升级为“正式结构设计 + SQL 基线”

### 3.4 当前代码 / 方案同步更新

- 已在 `backend/app/services/catalog.py` 中补充 `deviceCode` 不可修改校验，与 T-002 口径保持一致
- 已将 TDengine 全量 schema 基线从 `device_id + variable_id` TAG 更新为 `var_seq` TAG
- 已执行 `template_sync_migration_2026_04_23.sql` 与 `template_sync_seed_2026_04_23.sql` 到本机 `device_watch`
- 已执行 `template_sync_verify_2026_04_23.sql`，确认 `unit_dict / variable_template / template_variable / cross_db_task`、新增公共字段与 `trg_device_code_no_update` 触发器可查询
- 模板同步与跨库补偿当前仍处于数据库结构就绪阶段，尚未接入正式后端 worker

### 3.5 最新联调结论

- `login / user/info / menus` 的真实数据库链路仍保持可用
- 当前模板扩展的首要目标不是压缩 MySQL 存储，而是降低点位维护冗余、统一模板同步口径
- 数据库侧当前最高优先级已从“继续扩 auth 表”切换为“抽取标准模板、验证模板首次应用 / 显式重同步、验证 `var_seq` 新口径联调”

### 3.6 2026-04-24 点位主键收口更新

- 已新增 `docs/数据库/sql/point_var_seq_migration_2026_04_24.sql`
- 已新增 `docs/数据库/sql/point_var_seq_seed_2026_04_24.sql`
- 已新增 `docs/数据库/sql/point_var_seq_verify_2026_04_24.sql`
- 已将 `device_variable / template_variable` 的正式业务键统一收口为 `var_seq`
- 已从点位结构中移除 `var_code` 口径，TDengine 子表名统一改为派生值 `device_code + '_' + var_seq`
- 已把桥吊 `qc100` 的 `30 / 11894 / 11900` 三个点位固定为默认主显示样本
- 已对 2 条无法从 legacy `var_code` 直接提取纯序号的联调点位做人工归一：按地址补齐为 `9165 / 9508`，确保约束可顺利落库

---

## 四、下一步建议

建议数据库模块按以下顺序继续推进：

1. 选 1 台标准设备抽取第一套模板，验证 `template_variable` 与 `device_variable` 的同构复制结果
2. 在空设备上验证“首次应用 + 只补缺不覆盖不删除”的同步策略，并同步检查 `template_last_sync_version / template_sync_time`
3. 配合后端继续验证 `var_seq` 新口径下的实时、历史、Dashboard 查询与旧表回退兼容
4. 模板同步稳定后，再接入 `cross_db_task` worker，并继续推进 `sys_data_permission` 业务过滤与系统配置表

---

## 五、当前结论

当前数据库开发已经从“只有主数据表”进入“权限闭环已验证、报警日志已落库、模板同步结构已定稿并沉淀 SQL 基线”阶段：

- 核心 5 张业务主数据表已具备
- 权限域 8 张表已完成结构设计、SQL 基线与数据库落地
- 报警 / 日志 6 张表已完成正式结构设计、数据库落库与 verify 校验
- 模板同步相关 4 类结构已完成正式设计、SQL 基线与本机落库校验
- 默认角色、账号、菜单、权限种子已具备，真实登录与菜单链路已验证通过
- 当前首要任务已经从“继续补权限 / 报警结构”切换为“验证模板抽取与同步、验证 TDengine `var_seq` 新口径联调”
