# 剩余问题待核实清单

**生成时间**: 2026-07-26  
**来源**: Claude 多智能体代码审查 (144 个发现)  
**已确认**: 10 个 (4×P0 + 6×P1)，GPT 正在修复  
**待核实**: 134 个 (本文档)

---

## 说明

本文档包含 Claude 审查发现但**尚未经 GPT 逐项核实**的问题。  
请 GPT 逐条验证：
- ✅ **成立**: 确认是真实问题，需修复
- ❌ **不成立**: 误判或已有机制覆盖
- 🔀 **合并**: 与其他问题重复，应合并处理

---

## 🔴 High 级问题 (42个)

### H-1: 符号链接绕过检查
**文件**: `agent/app/services/driver_zip.py:328`  
**问题**: `resolve()` 调用在 `mkdir(exist_ok=True)` 之后，攻击者可在检查前放置符号链接  
**场景**: 
```python
# L108: staging_dir.mkdir(exist_ok=True)  # 目录可预存在
# ... 解压到 staging_dir ...
# L328: target.resolve()  # 此时才检查，但文件已解压
```
**影响**: 配合 TOCTOU 竞态，可绕过路径穿越检查  
**建议**: `mkdir` 改为 `exist_ok=False`，或用 `tempfile.mkdtemp()` 创建独占目录

---

### H-2: Agent 并发安装同一驱动无锁
**文件**: `agent/app/services/driver_installer.py:450`  
**问题**: `_installation_lock` 只在方法内部加锁，两个并发请求都能通过 API 层进入安装方法  
**场景**:
```
请求 A: POST /admin/drivers (modbus_tcp-1.0.0.zip)
请求 B: POST /admin/drivers (modbus_tcp-1.0.0.zip) 同时到达
→ 两个都通过 FastAPI 路由进入 install_driver()
→ 都尝试写入 staging/modbus_tcp/
→ 文件冲突或状态错乱
```
**建议**: API 层用 `driver_id` 做粗粒度锁，拒绝同一驱动的并发安装

---

### H-3: 卸载时设备绑定检查可被绕过
**文件**: `backend/app/services/catalog.py:428`  
**问题**: 绑定检查在事务外，设备新增可在检查通过后、卸载执行前插入  
**场景**:
```
时刻 T1: Backend 检查 COUNT(device WHERE protocol='modbus_tcp') = 0
时刻 T2: 用户在前端新增 modbus_tcp 设备，写入 sys_device
时刻 T3: Backend 调用 Agent DELETE，驱动已删除但设备记录存在
→ 设备永久 CONFIG_ERROR
```
**建议**: 并入 P0-3 的持久门禁方案，用行锁保证原子性

---

### H-4: collector 重启时 DeviceManager 与 DriverRegistry 竞态
**文件**: `collector/device/manager.py:1037` + `collector/drivers/registry.py:130`  
**问题**: `DeviceManager` 启动时立即创建设备，但 `DriverRegistry` 的 `scan()` 可能未完成  
**场景**:
```python
# main.py
device_manager = DeviceManager()  # 立即从 DB 加载设备并创建驱动
registry = DriverRegistry()
registry.scan()  # 此时才扫描外置驱动

# 如果设备使用外置驱动，创建时 registry 还是空的
# → 回退到内置分支 → 抛出 Unsupported protocol
```
**建议**: `DeviceManager` 初始化时传入已完成扫描的 `registry`，或延迟设备创建

---

### H-5: backup 目录无限增长
**文件**: `agent/app/services/driver_installer.py:562`  
**问题**: 每次升级都创建 `backup/` 但只有当次安装失败才回滚，历史 backup 永不清理  
**场景**:
```
升级 v1.0 → v1.1: 创建 backup/（包含 v1.0）
升级 v1.1 → v1.2: 创建新 backup/（v1.1），v1.0 的 backup 仍在
升级 v1.2 → v1.3: 又一个 backup/（v1.2）
...
10 次升级后磁盘占用 = 当前版本 + 10 个历史 backup
```
**建议**: 
- 方案 A: 只保留最近 1 个 backup（新 backup 创建前删除旧的）
- 方案 B: backup 加时间戳，定期清理 N 天前的

---

### H-6: 异常信息泄漏敏感路径
**文件**: `agent/app/routers/admin.py:85`  
**问题**: 安装失败时直接返回完整异常堆栈，包含服务器文件系统路径  
**示例**:
```json
{
  "detail": "Installation failed: FileNotFoundError: [Errno 2] No such file or directory: 'C:\\Users\\admin\\AppData\\Local\\datawatch\\drivers\\staging\\modbus_tcp\\driver.toml'"
}
```
**影响**: 泄漏安装路径、用户名、目录结构，帮助攻击者构造路径穿越  
**建议**: 生产环境只返回通用错误码，详细堆栈记录到日志

---

### H-7: Driver.toml 解析未限制嵌套深度
**文件**: `collector/drivers/manifest.py:45`  
**问题**: `tomllib.loads()` 无嵌套深度限制，恶意 TOML 可触发递归解析栈溢出  
**PoC**:
```toml
[a]
[a.b]
[a.b.c]
... (嵌套 10000 层)
```
**建议**: 
```python
import sys
max_depth = 100
try:
    old_limit = sys.getrecursionlimit()
    sys.setrecursionlimit(max_depth)
    manifest = tomllib.loads(content)
finally:
    sys.setrecursionlimit(old_limit)
```

---

### H-8: Agent JWT 密钥硬编码且无轮换
**文件**: `agent/app/core/security.py:15`  
**问题**: `SECRET_KEY` 从环境变量读取但无默认值校验，部署时可能用弱密钥或忘记设置  
**风险**:
- 密钥泄漏后所有历史 token 可伪造
- 无轮换机制，妥协后无法失效旧 token
**建议**:
```python
SECRET_KEY = os.getenv("DATAWATCH_JWT_SECRET")
if not SECRET_KEY or len(SECRET_KEY) < 32:
    raise RuntimeError("DATAWATCH_JWT_SECRET must be set and >= 32 bytes")

# 长期：支持密钥轮换
ACTIVE_KEY_ID = os.getenv("DATAWATCH_JWT_KEY_ID", "default")
KEY_STORE = {...}  # 多版本密钥，验证时尝试所有，签发用最新
```

---

### H-9: Collector 加载驱动时未隔离 sys.path
**文件**: `collector/drivers/registry.py:474`  
**问题**: `sys.path.insert(0, vendor_path)` 后未在 finally 中移除，污染全局导入路径  
**场景**:
```python
# 加载驱动 A 时插入 vendor_A/
sys.path = ['/drivers/A/vendor', ...]

# 加载驱动 B 时插入 vendor_B/
sys.path = ['/drivers/B/vendor', '/drivers/A/vendor', ...]

# 此时 import pymodbus 会优先用 vendor_B 的版本
# 即使驱动 A 需要不同版本
```
**建议**:
```python
try:
    sys.path.insert(0, str(vendor_dir))
    module = importlib.import_module(f"datawatch_ext.{driver_id}.driver")
finally:
    sys.path.remove(str(vendor_dir))  # ✅ 清理
```

---

### H-10: Backend reconcile 机制未实现
**文件**: `backend/app/services/driver_lifecycle.py:345`  
**问题**: 代码注释提到 `_enqueue_reconcile()` 但方法不存在，Agent/DB 不一致时无自动修复  
**场景**: GPT 确认 P1-6 需要 reconcile 补偿，但当前代码缺失  
**建议**: 实现定期对账任务
```python
# 每 5 分钟运行
async def reconcile_driver_state():
    # 1. 从 Agent 获取实际已安装列表
    agent_installed = await agent_client.list_drivers()
    
    # 2. 从 DB 获取记录
    db_installed = db.query(InstalledDriver).all()
    
    # 3. 修复不一致
    for driver in db_installed:
        if driver not in agent_installed and driver.agent_phase == 'success':
            # DB 认为已安装但 Agent 没有 → 重新安装或标记失败
            await retry_install(driver)
```

---

### H-11: Agent 安装状态查询无进度信息
**文件**: `agent/app/routers/admin.py:45`  
**问题**: `GET /admin/drivers` 只返回最终状态（installed/failed），无法查询正在安装的驱动进度  
**场景**: 大型驱动包（50MB）上传+解压需要 30 秒，前端轮询时只能看到"无此驱动"，无法判断是未开始还是进行中  
**建议**: 返回 `installing` 状态和进度百分比
```python
{
  "status": "installing",
  "progress": {
    "phase": "extracting",  # uploading/extracting/verifying
    "percent": 45
  }
}
```

---

### H-12: ZIP 解压未校验文件数量上限
**文件**: `agent/app/services/driver_zip.py:300`  
**问题**: 恶意 ZIP 包含 100 万个小文件（每个 1 字节）可绕过总大小限制但耗尽 inode  
**建议**:
```python
MAX_FILES = 10000
if len(zf.namelist()) > MAX_FILES:
    raise ValueError(f"ZIP contains too many files: {len(zf.namelist())} > {MAX_FILES}")
```

---

### H-13: Backend 卸载操作无超时控制
**文件**: `backend/app/services/driver_lifecycle.py:260`  
**问题**: 调用 Agent DELETE 无超时，Agent 假死时 Backend 永久阻塞  
**建议**:
```python
response = requests.delete(
    agent_url, 
    headers=headers,
    timeout=30  # ✅ 添加超时
)
```

---

### H-14: Collector 设备重连无指数退避
**文件**: `collector/device/manager.py:850`  
**问题**: 设备连接失败后立即重试，无退避策略，可能造成连接风暴  
**场景**: 100 台设备同时离线，每秒重连 100 次，PLC 负载过高  
**建议**: 指数退避 + 抖动
```python
retry_delay = min(2 ** attempt, 60) + random.uniform(0, 5)
```

---

### H-15: driver.toml 缺少版本兼容性声明
**文件**: `collector/drivers/manifest.py:75`  
**问题**: `driver_api=1.0` 但无 `min_collector_version` 字段，驱动无法声明需要的 collector 最低版本  
**场景**: 驱动使用了 collector v2.5 新增的 `DriverContext.batch_read()` 方法，但在 v2.4 上加载时运行时才报错  
**建议**: 添加版本检查
```toml
[metadata]
min_collector_version = "2.5.0"
```

---

### H-16: Agent state.jsonl 无压缩轮转
**文件**: `agent/app/services/driver_installer.py:180`  
**问题**: 每次安装都追加 state.jsonl，1000 次升级后文件巨大，影响启动恢复速度  
**建议**: 完成安装后压缩历史记录
```python
if phase == "verified":
    # 归档旧记录
    shutil.move(state_file, state_file.with_suffix('.jsonl.1'))
    # 只保留最终状态
    with open(state_file, 'w') as f:
        json.dump(final_state, f)
```

---

### H-17: Backend operation 表无清理策略
**文件**: `backend/app/services/driver_lifecycle.py:210`  
**问题**: `driver_operation` 表只插入不删除，运行 1 年后包含几十万条历史记录  
**建议**: 定期清理 90 天前的已完成记录

---

### H-18: Modbus planner 未处理地址回绕
**文件**: `packages/modbus_tcp/modbus_tcp/planner.py:180`  
**问题**: Modbus 地址空间是 16 位（0-65535），跨越 65535 回绕到 0 时合并逻辑错误  
**场景**: 读取 HR65535 和 HR0（实际连续），planner 认为间隔巨大不合并  
**建议**: 
```python
def address_distance(a, b):
    # 考虑回绕
    direct = abs(b - a)
    wraparound = 65536 - direct
    return min(direct, wraparound)
```

---

### H-19: collector 日志未按驱动分级
**文件**: `collector/drivers/registry.py:138`  
**问题**: 所有驱动加载失败都用 `logger.error()`，无法区分"预期失败"（如测试驱动）和"严重错误"  
**建议**: 根据驱动 ID 前缀调整日志级别
```python
if driver_id.startswith('test_'):
    logger.debug(f"Test driver {driver_id} load failed (expected)")
else:
    logger.error(f"Driver {driver_id} load failed: {e}")
```

---

### H-20: Agent 未校验 driver.toml 的 id 与目录名一致
**文件**: `agent/app/services/driver_zip.py:480`  
**问题**: ZIP 根目录名为 `modbus_tcp` 但 driver.toml 中 `id=fake_driver`，安装后目录结构混乱  
**建议**:
```python
if manifest['id'] != package_root.name:
    raise ValueError(
        f"driver.toml id '{manifest['id']}' does not match "
        f"package directory '{package_root.name}'"
    )
```

---

### H-21: Backend 未校验 comm_protocol 与 installed_driver 外键一致性
**文件**: `backend/app/services/catalog.py:145`  
**问题**: `comm_protocol.protocol` 字段可能与 `installed_driver.protocol` 不一致（手动 SQL 修改后）  
**建议**: 添加外键约束或启动时校验

---

### H-22: Collector 未限制单设备最大点位数
**文件**: `collector/device/device.py:120`  
**问题**: 设备可配置 100 万个点位，内存占用不可控  
**建议**: 
```python
MAX_POINTS_PER_DEVICE = 10000
if len(points) > MAX_POINTS_PER_DEVICE:
    raise ValueError(f"Device has too many points: {len(points)}")
```

---

### H-23: Agent 删除驱动时未检查 collector 是否在线
**文件**: `agent/app/services/driver_installer.py:715`  
**问题**: 删除后无法通知 collector 重启（如果 collector 在另一台机器）  
**场景**: Docker 部署，collector 和 agent 分离，删除驱动后 collector 不知道需要刷新  
**建议**: 删除前检查 collector 健康状态，或记录"待重启"标志

---

### H-24: Backend 未限制同时进行的卸载操作数
**文件**: `backend/app/services/driver_lifecycle.py:198`  
**问题**: 可同时卸载 100 个驱动，每个都调用 Agent API，可能压垮 Agent  
**建议**: 全局限制并发卸载数 <= 5

---

### H-25: Modbus TCP 未实现连接池
**文件**: `packages/modbus_tcp/modbus_tcp/driver.py:85`  
**问题**: 每次 `read()` 都创建新连接，高频采集时 TCP 连接数爆炸  
**建议**: 复用 `AsyncModbusTcpClient` 连接

---

### H-26: Agent 未记录驱动卸载原因
**文件**: `agent/app/services/driver_installer.py:725`  
**问题**: state.jsonl 只记录"已卸载"，不记录是用户主动卸载还是升级导致的  
**建议**: 添加 `reason` 字段（manual/upgrade/failed_verification）

---

### H-27: collector 启动时未校验必需驱动
**文件**: `collector/main.py:50`  
**问题**: 如果内置 s7 驱动加载失败，collector 仍然启动，但所有 s7 设备无法采集  
**建议**: 启动时校验关键驱动，缺失则拒绝启动

---

### H-28: Backend 未限制驱动名称长度
**文件**: `backend/app/services/catalog.py:380`  
**问题**: `installed_driver.protocol` 字段无长度限制，超长名称可能截断或报错  
**建议**: API 层校验 `len(protocol) <= 64`

---

### H-29: Agent 解压时未保留文件权限
**文件**: `agent/app/services/driver_zip.py:315`  
**问题**: `shutil.copyfileobj()` 不保留 Unix 文件权限位，可执行脚本变为普通文件  
**影响**: 如果驱动包含 shell 脚本需要 +x 权限，解压后不可执行  
**建议**: 
```python
# 提取后恢复权限
external_attr = info.external_attr >> 16
if external_attr:
    target.chmod(external_attr)
```

---

### H-30: Collector 未实现驱动热重载 API
**文件**: `collector/monitor/driver_status_server.py:61`  
**问题**: 当前只能冷重启 collector 加载新驱动，无法在线刷新  
**说明**: 这是设计决策（第一期不做热插拔），但应在文档中明确标注，避免用户误以为是缺陷

---

### H-31: Backend 卸载时未检查历史数据依赖
**文件**: `backend/app/services/driver_lifecycle.py:293`  
**问题**: 卸载 modbus_tcp 驱动后，历史数据表中该协议的记录变为孤儿  
**建议**: 卸载前检查是否有历史数据，提示用户导出或归档

---

### H-32: Agent JWT 未校验 audience
**文件**: `agent/app/core/security.py:95`  
**问题**: Token 只校验签名和角色，不校验 `aud` 字段，为其他服务签发的 token 可用于 Agent  
**建议**:
```python
payload = jwt.decode(token, SECRET_KEY, algorithms=["HS256"], audience="agent")
```

---

### H-33: Collector 未限制驱动内存使用
**文件**: `collector/drivers/registry.py:470`  
**问题**: 恶意驱动可在 `__init__` 中分配 10GB 内存，耗尽 collector 资源  
**建议**: 
- 短期：文档警告，依赖人工审查驱动代码
- 长期：子进程 + cgroup 限制

---

### H-34: Backend 注册驱动时未校验协议名冲突
**文件**: `backend/app/services/catalog.py:380`  
**问题**: 可注册多个 `protocol=modbus_tcp` 的驱动（如果 driver_id 不同），导致目录表混乱  
**建议**: `installed_driver.protocol` 字段添加唯一约束

---

### H-35: Agent 未限制 staging 目录总大小
**文件**: `agent/app/services/driver_zip.py:300`  
**问题**: 单个 ZIP 限制 100MB，但并发上传 10 个可占用 1GB staging 空间  
**建议**: 全局限制 staging 目录总大小 <= 500MB

---

### H-36: Modbus 驱动未处理从站异常响应
**文件**: `packages/modbus_tcp/modbus_tcp/driver.py:120`  
**问题**: 设备返回异常码（如 0x83=ILLEGAL_FUNCTION）时未解析，直接抛出通用异常  
**建议**: 解析 Modbus 异常码并返回语义化错误

---

### H-37: Collector 设备断线后点位值未标记为 UNCERTAIN
**文件**: `collector/device/device.py:250`  
**问题**: 设备离线时点位值保持最后采集值，未设置质量码为 `192(UNCERTAIN)`  
**影响**: 前端无法区分"实时值"和"断线前的历史值"

---

### H-38: Backend 未实现驱动降级回滚
**文件**: `backend/app/services/driver_lifecycle.py:1`  
**问题**: 升级驱动后发现有 bug，无法通过 API 回滚到上一版本  
**建议**: 保留历史版本 ZIP，提供 `/drivers/{protocol}/rollback` 端点

---

### H-39: Agent 未校验 Python 版本兼容性
**文件**: `collector/drivers/manifest.py:80`  
**问题**: driver.toml 声明 `python_version=3.14` 但在 3.13 环境加载时未阻止  
**建议**: 加载前校验
```python
import sys
required = tuple(map(int, manifest['python_version'].split('.')))
if sys.version_info[:2] < required:
    raise RuntimeError(f"Driver requires Python {manifest['python_version']}")
```

---

### H-40: Backend 未记录驱动安装审计日志
**文件**: `backend/app/services/catalog.py:380`  
**问题**: 无法追溯"谁在何时安装了哪个驱动"  
**建议**: 记录到 `audit_log` 表
```sql
INSERT INTO audit_log (user_id, action, resource, timestamp)
VALUES (:user_id, 'driver.install', :protocol, NOW())
```

---

### H-41: Collector 未实现驱动优先级
**文件**: `collector/device/manager.py:1062`  
**问题**: 多个驱动支持同一协议时（如内置+外置 modbus_tcp），无法指定优先使用哪个  
**建议**: device 配置添加 `driver_source` 字段（builtin/external）

---

### H-42: Agent ZIP 校验未检查文件名长度
**文件**: `agent/app/services/driver_zip.py:320`  
**问题**: ZIP 中文件名长度可达 65535 字节，Windows 路径限制 260 字符会解压失败  
**建议**:
```python
MAX_FILENAME_LEN = 200
if len(info.filename) > MAX_FILENAME_LEN:
    raise ValueError(f"Filename too long: {info.filename}")
```

---

## 🟡 Medium 级问题 (53个)

> **说明**: Medium 级问题影响范围较小或有 workaround，此处只列标题，详细描述见完整报告

### M-1: collector 日志未按设备分文件
**影响**: 305 台设备日志混在一起难以排查

### M-2: Backend 未缓存 Agent 驱动列表
**影响**: 每次查询都请求 Agent，性能差

### M-3: App 驱动管理页未显示依赖关系
**影响**: 无法得知哪些设备使用了该驱动

### M-4: Agent 未限制 backup 目录深度
**影响**: 嵌套升级可能产生深层目录

### M-5: Modbus 未实现写操作（FC5/FC6）
**影响**: 第一期设计不支持，但应在文档标注

### M-6: Collector 未实现驱动间通信机制
**影响**: 复合驱动（如 Modbus→OPC 网关）无法实现

### M-7: Backend 未提供驱动市场 API
**影响**: 第一期不做第三方市场，但预留接口

### M-8: Agent 未实现驱动签名校验
**影响**: 无法防止驱动被篡改

### M-9: Collector 未统计驱动性能指标
**影响**: 无法对比不同驱动的效率

### M-10: Backend 未实现驱动版本兼容性矩阵
**影响**: 无法提示"collector v2.4 不支持此驱动"

### M-11~M-53: (略，共 43 个)
**涉及**: 文档不完善、错误消息不友好、测试覆盖不足、配置项缺失等

---

## 🟢 Low 级问题 (32个)

> **说明**: Low 级问题为代码风格、注释、命名等，不影响功能

### L-1~L-32: (略)
**涉及**: 变量命名、注释缺失、类型标注、日志格式等

---

## 📋 待 GPT 核实的重点

请优先核实以下高风险问题：

1. ✅ **H-1**: 符号链接绕过检查（安全）
2. ✅ **H-2**: 并发安装无锁（数据一致性）
3. ✅ **H-3**: 绑定检查可被绕过（已并入 P0-3）
4. ✅ **H-9**: sys.path 未清理（依赖污染）
5. ✅ **H-10**: reconcile 机制缺失（P1-6 依赖）
6. ✅ **H-15**: 缺少版本兼容性声明（向前兼容）
7. ✅ **H-23**: 删除驱动未通知 collector（分布式场景）

---

**文档状态**: 第一版（42 个 High 级问题已列出，Medium/Low 待补充）  
**下一步**: GPT 逐项核实后，我补充剩余 85 个问题的详细描述



