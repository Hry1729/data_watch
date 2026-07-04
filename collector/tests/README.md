# collector/tests

collector 的单元/集成测试（`test_*.py`，pytest 收集）与 GE 现场调试工具（非 pytest，带 `.spec` 可打包成 exe 带到现场）。

**运行测试**（cwd = `collector/tests`）：

```powershell
..\.venv-pack\Scripts\python.exe -m pytest
```

需真实 MySQL 的集成测试见 `test_config_db_integration.py`，默认跳过，设环境变量 `RUN_DB_INTEGRATION=1` 启用。

---

## 配置加载与校验
- `test_config_loader.py` — 测试 `settings.toml` 加载与 `DATAWATCH_*` 环境变量覆盖（MySQL 密码、连接字段）
- `test_config_validator.py` — 测试配置校验器：S7 地址基线放行、Redis 非法参数拒绝、遗留/歧义 S7 地址拒绝
- `test_config_reload_loop.py` — 测试配置热重载循环的设备变更分类（新增/删除/保留）
- `test_dry_run.py` — Dry-run 配置预检测试（设备/变量计数、按协议统计）

## S7 驱动与寻址
- `test_s7_driver.py` — S7 驱动测试，用 `FakeS7Client` 模拟 `db_read`/`read_area`，覆盖 I/Q/M/T 区与多数据类型解析
- `test_s7_addressing.py` — S7 寻址与扫描块合并测试（不跨 DB 号合并、连续 DB 点合并、结果映射）

## GE SRTP 寻址
- `test_ge_addressing.py` — GE SRTP 地址解析（%R/%AI/%AQ/%I/%Q/%M）与批量合并、单点还原、executor 集成
- `test_ge_srtp_protocol.py` — GE SRTP 协议层测试（NACK 头状态字节解析、半包 recv 拼装、读满防半包）

## 设备管理与重连
- `test_device_manager.py` — 设备管理器测试（注册、连接、连接失败、端口映射 `EndpointPortMapper`）
- `test_runtime_topology.py` — 运行期设备拓扑变更测试（新增/删除 PLC、external-push、center 设备场景）
- `test_shutdown_flag.py` — 启动时清理残留 `shutdown_flag`，避免残留 flag 让 collector 启动即自杀
- `test_retry.py` — `with_retry` 重试装饰器测试（失败重试、使用 `self.logger.warning` 记录）

## 调度器
- `test_scheduler_metrics.py` — 调度器指标测试（执行周期 lag、deadline miss 计数、任务数 gauge、inflight）

## 执行器与有界池
- `test_executor.py` — 执行器单元测试：Redis 多段写入（window→stream→latest）失败语义与顺序回归
- `test_bounded_executor.py` — 有界线程池执行器测试（取消等待不提前释放容量、饱和抛 `ExecutorSaturatedError`）
- `test_collect_task_snapshot.py` — `CollectTask` 快照冻结测试（scan_task 元数据不可变，防止运行期被改）

## Redis 写入与客户端
- `test_redis_writer.py` — `RedisWriter` 单元测试（write_latest/window/stream，key 命名、JSON 编码、分片）
- `test_redis_client.py` — Redis 客户端测试（使用 `BlockingConnectionPool`，max_connections=64、timeout=2）
- `test_serialization_compat.py` — Collector Redis JSON 负载的 msgspec 与 stdlib json 语义等价性回归门

## 数据质量
- `test_data_quality.py` — 数据质量管理器测试（值范围规则、变化率规则、质量等级判定）

## 接口接入
- `test_interface_ingest.py` — 外部接口采集测试（`_IngestHTTPServer` 多段 Redis 写入、payload 归一化、写入顺序）
- `test_opcda_center_driver.py` — OPC DA 中心采集驱动连接失败错误补定位字段（endpoint/port/serverProgId）
- `test_opcua_driver.py` — OPC UA 驱动安全边界测试（collector 中 write 被禁用，read-only）
- `test_check_protocols.py` — `check_protocols` 脚本测试（center/sidecar 独立去重，同 endpoint 两类协议不互屏）

## 集成与冒烟
- `test_integration.py` — 采集链路集成测试（executor 读失败触发重连、Redis 写失败保持设备在线）
- `test_config_db_integration.py` — ConfigDB 真实 MySQL 集成冒烟（需 `RUN_DB_INTEGRATION=1`，钉死 SQL 契约）

## 性能指标与监控
- `test_performance_metrics.py` — 性能指标测试（`TimingHistogram` 分位、线程安全、运行时快照、GC 暂停跟踪）

## 配置数据库
- `test_config_db_scan_rate.py` — ConfigDB `scan_rate` 归一化（毫秒精度）与 `DeviceSignature` 设备字段变更判定

## 其他/脚手架
- `conftest.py` — pytest 配置与公共 fixtures（`mock_driver`、`collector_config`、`integration` marker）
- `__init__.py` — 测试包标识（仅一行 docstring）

## 现场调试工具（非 pytest）
- `bench_ge_srtp.py` — GE SRTP 单块 RTT 基准，只读，验证 124 块串行扫描耗时；用法见文件头 docstring；可用 `bench_ge_srtp.spec` 打包成 exe
- `diag_ge_discrete.py` — GE 离散量 bit/byte 字序诊断，只读，定位 %Q 等离散点 byte 模式取值错位；可用 `diag_ge_discrete.spec` 打包成 exe
