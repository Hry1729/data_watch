# 中心直写 Redis：运行测试超时修复

## 范围与根因

基线 HEAD：`983ba9a9ece2376e490895faaf2a78db2a262d87`。
接手时已有 `collector/main.py` 的 RedisWriter 默认入口修改和两个未跟踪测试文件；本轮保留它们，没有回滚生产代码。

1. 原测试顺序是 `await app.start()` 后再 `await app.shutdown()`。实际 `CollectorApp.start()` 在完成装配后执行 `await self._shutdown.wait()`，收到 `app.stop()` 才继续关闭并返回。因此测试永远无法到达后面的 shutdown；不是已证实的生产启停死锁。
2. 原测试将 `DATAWATCH_DATA_ROOT` 覆盖为空临时目录，遮蔽了 conftest 准备的驱动激活目录，导致 S7 驱动发现失败。本轮复制已有 S7 测试包到本例临时目录，保留真实驱动发现、规划与解码，只替换 PLC connect/read/disconnect。
3. 当前默认 Python 3.14t 缺少 msgspec；普通 3.14 虽有依赖，却与驱动清单 cp314t 不匹配。本轮仅在 `outputs/central-runtime-fix-20260927/deps` 安装锁定版本 msgspec 0.21.1，使用 3.14t 验证。
4. 全量回归首轮唯一失败的 Outbox 锁测试，其子进程缺少 collector 模块路径。相同环境直接执行导入得到 `ModuleNotFoundError: storage`；在测试命令中显式设置 PYTHONPATH 后该测试通过。此次失败发生于锁断言之前，不能据此判定锁实现存在竞态。

## 本轮修改

仅修改 `collector/tests/test_central_runtime_redis_only.py` 的测试实现，另增加本报告：

- 将 start 作为后台任务运行，等待启动与数据就绪，发送 stop 并等待真实 shutdown 完成；等待数据 15 秒、等待退出 20 秒，finally 执行清理。
- 保留无设备 bootstrap 场景，增加 S7 调度采集场景：两个相邻 INT 经真实合并、解码和分组后输出 42、84。
- 构造真实 CollectorApp，不传 writer_factory，不替换 RedisWriter、调度器或执行器。
- 对 latest、window、Stream 的实际值及质量码、Stream v2 契约、rt:change 通知作断言。
- 将 DurableRedisWriter 与 CollectorRetryOutbox 构造器替换为调用即失败；在运行中和关闭后检查采样 SQLite 与回放线程，并检查 WAITAOF / EVALSHA 命令统计。
- Redis 必须是显式指定的本地非 6379 端口且数据库为空；驱动状态服务使用随机端口。Redis 客户端设置网络超时并注册测试结束清理。

没有修改生产 start/shutdown 的生命周期语义，也没有修改分布式入口。边缘仍使用 EdgeOutboxWriter + EdgeStorageClient，并非 DurableRedisWriter。

## 验证记录

- 修正生命周期后的原运行测试：2 passed，1.64 秒（普通 Python 3.14，无设备）。
- 增加真实 S7 规划、解码与执行后：3 passed，2.06 秒（Python 3.14t）。
- 首轮 Collector 全量：607 passed / 1 failed / 24 skipped，139.30 秒；失败为上述子进程模块路径问题。
- 补齐子进程路径并加强运行中断言后，中心运行、架构契约和 Outbox 锁测试合计：14 passed，2.38 秒。
- 最终 Collector 全量：**608 passed / 24 skipped / 0 failed**，138.25 秒；另有一条第三方 asyncua 弃用告警。原始结果：`outputs/central-runtime-fix-20260927/collector-final-results.xml`。

## 复跑命令（PowerShell，项目根目录）

先提供一个专用、空的临时 Redis 实例，不得指向业务 Redis。将下方端口替换为该实例的实际映射端口：

```powershell
$env:PYTHONPATH = "$PWD\collector;$PWD;$PWD\outputs\central-runtime-fix-20260927\deps"
$env:DW_CENTRAL_TEST_REDIS_URL = 'redis://127.0.0.1:<隔离端口>/0'
py -3.14t -m pytest collector/tests/test_central_runtime_redis_only.py collector/tests/test_central_redis_only_default.py -q -o faulthandler_timeout=30
py -3.14t -m pytest collector/tests -q -o faulthandler_timeout=40
```

本轮专用容器：`dwv-central-fix-20260927-01`，映射端口 51936。测试结束后删除该容器及其匿名卷，不复用此地址作为业务配置。两个 XML 报告位于被忽略的 outputs 目录，未提交到 Git。

## 结论边界

已验证模拟 PLC I/O 下，真实中心装配与采集执行链直写 Redis 并正常停止；这不是实际 PLC 硬件连通性测试。

没有执行 Writer → TDengine 端到端对账、30 台同负载 A/B 或长测；没有验证遗留 Outbox 的无库/空库/非空库切换策略，也未改容量工具、测量命名或指标语义。不能从本轮结果得出性能改善比例或部署已就绪。

未部署、未推送、未修改业务 Redis 持久化配置，未处理业务遗留 Outbox，未使用 reset/clean/stash。
