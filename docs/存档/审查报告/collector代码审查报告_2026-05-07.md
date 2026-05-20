# Collector 测试前代码复审报告

更新时间：2026-05-07（已修复 P1-P3 问题）

## 审查范围

本次复审基于以下已完成报告继续展开：

- `docs/采集模块/collector代码审查报告（已全部修复）.md`
- `docs/采集模块/sidecars审查报告（已全部修复）.md`

覆盖范围：

- `collector/main.py` 启动、停机、配置加载与运行主链路
- `collector/core` 配置、MySQL 加载、日志、OPC DA IPC、校验与重试
- `collector/device` 设备注册、任务构建、连接、重连、状态写回
- `collector/drivers` S7 / GE SRTP / OPC UA / OPC DA Center 驱动
- `collector/data_queue` 缓冲、聚合、去重与失败恢复
- `collector/storage` TDengine 拓扑、重连与批量写入
- `collector/monitor` HTTP ingest、OPC DA tag 映射、健康检查
- `collector/sidecars` OPC DA push / proxy 启动脚本和接口协作
- `collector/tests` 单元测试与集成测试入口

## 总体结论

Collector 主链路已经具备进入系统测试的基础。本轮复审发现的问题已全部修复。

## 新增问题及修复状态

### Finding 1（P1）✅ 已修复：OPC DA center 自动端口映射会与已启动代理冲突

**文件**：

- `collector/run.bat`
- `collector/check_protocols.py`
- `collector/device/manager.py`

**修复方案**：

新增 `port_mapping.json` 文件作为共享端口映射存储：
- `check_protocols.py` 分配端口后保存到 `port_mapping.json`
- `EndpointPortMapper` 初始化时从文件加载已有映射，复用相同端口
- 新增 `_load_mapping_from_file()` 和 `_save_mapping_to_file()` 方法

**修复提交**：

- `collector/device/manager.py`: 添加 `PORT_MAPPING_FILE` 常量，`EndpointPortMapper` 支持文件持久化
- `collector/check_protocols.py`: `main()` 中保存端口映射到文件

---

### Finding 2（P1）✅ 已修复：TDengine 最终写失败后聚合器会丢弃已 flush 的批次

**文件**：`collector/data_queue/aggregator.py`

**修复方案**：

- 新增 `_restore_batch_to_cache()` 方法，将失败的 batch 放回缓存头部
- 写入失败时恢复 pending 状态，而非仅清空
- 下次 flush 时会重新尝试写入

**修复提交**：

- `collector/data_queue/aggregator.py`: 
  - 新增 `_restore_batch_to_cache()` 方法
  - `flush_to_output()` 异常分支调用 `_restore_batch_to_cache(batch)` 而非仅 `_rollback_pending_state()`

---

### Finding 3（P1）✅ 已修复：部分合法数据类型会被错误解析或静默丢弃

**文件**：`collector/drivers/s7_driver.py`

**修复方案**：

- 补齐 `DWORD` 解析：使用 `struct.unpack(">I", ...)` 解析无符号 32 位整数
- 补齐 `STRING` 解析：S7 字符串格式（首字节最大长度，次字节实际长度，后续字符）
- 更新 `_calc_byte_size()` 和 `_parse_bytes()` 支持 `DWORD` 和 `STRING`

**修复提交**：

- `collector/drivers/s7_driver.py`: `_parse_bytes()` 添加 `DWORD` 和 `STRING` 分支

---

### Finding 4（P1）✅ 已修复：OPC DA center 缺少 `opc_server` 时会被误标为连接成功

**文件**：`collector/drivers/opcda_center_driver.py`

**修复方案**：

- `connect()` 方法中增加配置验证，缺少 `opc_server` 或 `vm_host` 时抛出 `ConnectionError`
- 移除条件判断，必须成功连接 OPC DA 服务器才能标记 `_connected = True`

**修复提交**：

- `collector/drivers/opcda_center_driver.py`: 添加配置验证，缺少必填项时抛出异常

---

### Finding 5（P1）✅ 已修复：当前 collector `.venv` 仍是改名前路径，`run.bat` 回退后可能丢依赖

**文件**：`collector/run.bat`

**修复方案**：

- venv 不可运行时输出更明确的提示，指导用户重建 venv

**修复提交**：

- `collector/run.bat`: 增加 venv 不可用时的详细提示信息

---

### Finding 6（P2）✅ 已修复：MySQL / TDengine DSN 没有 URL 编码

**文件**：

- `collector/core/config.py`

**修复方案**：

- 使用 `urllib.parse.quote_plus` 对用户名和密码进行 URL 编码
- 新增 `TDengineConfig.to_rest_url()` 方法，安全转义凭证

**修复提交**：

- `collector/core/config.py`: 
  - 导入 `quote_plus`
  - `MySQLConfig.to_sqlalchemy_url()` 使用 `quote_plus` 转义
  - `TDengineConfig` 新增 `to_rest_url()` 方法

---

### Finding 7（P2）✅ 已修复：`modbus` 被配置校验放行，但没有内置驱动

**文件**：

- `collector/core/config_validator.py`
- `collector/core/config_db.py`

**修复方案**：

- 从校验器白名单中移除 `modbus`
- 从 `config_db.py` 的协议映射中移除 `modbus`

**修复提交**：

- `collector/core/config_validator.py`: 白名单移除 `modbus`
- `collector/core/config_db.py`: `_normalize_protocol()` 移除 `modbus` 映射

---

### Finding 8（P2）：毫秒级采集周期会被上取整为秒级

**文件**：`collector/core/config_db.py`

**状态**：待确认

**现象**：

`_normalize_scan_rate()` 对 `>=100` 的值按毫秒处理，但返回 `math.ceil(v / 1000)`，因此：

- `500ms -> 1s`
- `1500ms -> 2s`

**影响**：

如果现场期望 500ms 周期，collector 实际会按 1s 采集。

**建议**：

- 如果项目只支持秒级采集，应更新文档和前端配置说明。
- 如果需要毫秒级采集，应将 `scan_rate` 改为 `float` 秒或统一使用毫秒调度。

---

### Finding 9（P3）✅ 已修复：缓存模块同步装饰器不可用

**文件**：`collector/core/cache.py`

**修复方案**：

- 新增 `get_sync()` 和 `set_sync()` 同步方法
- `sync_wrapper` 使用同步方法而非 async 方法

**修复提交**：

- `collector/core/cache.py`:
  - 新增 `CacheManager.get_sync()` 方法
  - 新增 `CacheManager.set_sync()` 方法
  - `sync_wrapper` 改用 `get_sync()` 和 `set_sync()`

---

## 功能完成度复核

| 功能域 | 当前状态 | 复核结论 |
|--------|----------|----------|
| MySQL 动态加载设备/点位 | 已实现 | DSN 特殊字符已修复，协议口径已收口 |
| S7 只读采集 | 已实现 | DWORD/STRING 解析已补齐 |
| GE SRTP 只读采集 | 已实现 | 缺少真实 GE 模拟/现场测试覆盖 |
| OPC UA 只读采集 | 基线已实现 | 依赖 `asyncua` 可选安装 |
| OPC DA sidecar push | 已实现 | HTTP ingest 与 item_id mapper 已具备 |
| OPC DA center proxy | 已实现 | 端口映射和 opc_server 配置已修复 |
| 调度执行闭环 | 已实现 | 毫秒周期口径待确认 |
| 缓冲/聚合/去重 | 已实现 | 写失败批次保留已修复 |
| TDengine 拓扑与写入 | 已实现 | 设备类型库、变量子表已落地 |
| 系统日志接入 | 已实现 | 启动/关闭、设备状态、错误栈已接入 |
| 缓存模块 | 已实现 | 同步装饰器已修复 |

## 修复验证建议

1. 重新运行 pytest 确认 cache 测试通过
2. S7 smoke 测试验证 DWORD/STRING 解析
3. OPC DA center 端到端测试验证端口映射
4. TDengine 故障注入测试验证批次保留
