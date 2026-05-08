# Collector 审查优化建议

日期：2026-05-08

来源：结合 `codeflow-report.md` 自动分析结果与本轮人工核验。

## 结论摘要

本轮审查中，自动报告对 `collector` 的总体方向有参考价值，但存在一些常见误报，例如 README 示例密码、第三方示例代码中的清屏命令、未调用函数等。当前更值得优先处理的是采集链路的部署安全、配置一致性、队列丢点可观测性、停机数据完整性，以及 TDengine 故障时的缓存保护。

明文密码问题已确认存在，但按当前决策先标记为暂时忽略，不进入近期修复队列。

## 优先处理项

| 优先级 | 问题 | 影响 | 建议方案 | 置信度 |
|---|---|---|---|---|
| P1 | HTTP ingest 默认监听 `0.0.0.0`，且 `api_key` 为空时直接放行 | 如果部署到可被访问的网络，外部请求可直接推送采集数据 | 生产环境强制要求 API Key；默认 host 改为 `127.0.0.1`；在配置校验中将 `0.0.0.0 + 空 api_key` 升级为错误或显式 dev-only | 90% |
| P1 | `run.bat` 启动前协议检测只读固定 `config/settings.toml` | 使用 `COLLECTOR_CONFIG` 启动时，协议检测与主进程实际配置可能不一致，导致 OPC DA helper 启动判断错误 | 让 `check_protocols.py` 复用 `load_collector_config_from_env()`，或增加 `--config` 参数并由 `run.bat` 传入 | 88% |
| P1 | 采集任务入队可能丢点，但上层没有统计或报警 | 队列满时只在底层打印 warning，业务侧难以发现真实丢点 | `TaskExecutor` 比较 `accepted < len(points)`，记录 WARN、system_log 和 metrics | 86% |
| P1 | 停机时只 flush aggregator cache，没有先 drain `DataBuffer` 队列 | 优雅停机时，仍在 `DataBuffer` 中但尚未进入 aggregator 的点位可能丢失 | shutdown 时停止 scheduler 后，先批量取空 `DataBuffer` 剩余点，再送入 aggregator，最后执行 final flush | 90% |
| P1 | TDengine 挂掉时失败 batch 会放回 aggregator cache，但 cache 没有上限 | TDengine 长时间不可用时可能造成内存持续增长 | 为 aggregator cache 增加上限；连续写失败后退避或暂停采集；超限时执行明确丢弃策略并写 system_log | 82% |

## 次优先处理项

| 优先级 | 问题 | 影响 | 建议方案 | 置信度 |
|---|---|---|---|---|
| P2 | ingest 请求体只限制点数，不限制原始 body 字节数 | 大请求可能先占用大量内存，再进入 JSON 解析和点数校验 | 增加 `max_body_bytes` 配置，超过限制直接返回 `413` | 84% |
| P2 | TDengine WebSocket DSN 中账号密码未 URL encode | 密码含特殊字符时可能连接失败 | 构造 DSN 时对 user/password 使用 `quote_plus` | 78% |
| P2 | collector 时间依赖系统本地 `datetime.now()` | 当前本机环境下基本可用，但与后端统一时间工具不完全一致 | 后续补充 collector 侧 `time_utils.now_local()`，统一为 Asia/Shanghai 语义 | 65% |

## 暂时忽略项

| 问题 | 说明 | 置信度 |
|---|---|---|
| 配置文件中的明文数据库密码 | 已确认 `collector/config/settings.toml`、`settings.fake_bootstrap.toml` 中存在明文密码。按当前决策暂时忽略，后续如进入生产化整理，再改为 example/local/env 分层配置 | 95% |
| README 示例密码 | 多数属于文档示例，不构成直接运行风险 | 90% |
| 测试 fixture 中的默认密码 | 用于测试构造对象，暂不作为安全问题处理 | 85% |
| `drivers/gesrtp-py-main/examples` 中的 `os.system('cls')` | 示例清屏命令，报告标为高危，但不是当前运行链路核心风险 | 95% |
| 未调用函数列表 | 其中部分可能是公共入口、测试入口或预留扩展点，不建议为降低报告数量直接删除 | 75% |
| 大文件 / God Object | 是维护性问题，但拆分风险较高，应在稳定性问题处理后再规划 | 70% |

## 验证建议

| 验证项 | 方法 |
|---|---|
| ingest 无 API Key 拦截 | 设置 `host=0.0.0.0` 且 `api_key=""`，确认配置校验失败或启动拒绝 |
| `COLLECTOR_CONFIG` 生效 | 使用临时配置文件启动 `run.bat`，确认协议检测和主进程读取同一配置 |
| 队列满丢点可见 | 将 `queue_max_size` 调小，模拟高频入队，确认日志和 metrics 能看到 dropped |
| 停机不丢 buffer | 启动后向 buffer 注入点位，立即触发 shutdown，确认剩余点能进入 final flush |
| TDengine 故障保护 | 停掉 TDengine，持续采集，确认 cache 不无限增长且有清晰告警 |

