# Data Watch 测试准入复核（2026-09-26）

结论：本轮发现的阻塞问题已经最小修复，可以交给 DeepSeek 开始下一轮隔离测试。此结论不等于容量验收通过，也不等于可部署上线。基线为 44ecbdd495240fcf8d916ea8e29ada88a0c3d7cc，实际测试应使用当前未提交工作区及本次 source 哈希，不要只 checkout 基线。

## 本次审查后修复

- Writer 刷新映射后重新检查整帧所有点：新增点出现、旧点消失时保留整帧待处理，避免写半帧后确认。
- 拒收缺口日志恢复：残留表含正式表没有的额外列时拒绝删除，保留两表供人工处理；同结构覆盖与单一残留自动恢复不变。
- Redis 交付成功但本地清理失败时，已领取数量归零；本地待处理数据仍保留。
- 测试脚本在 outbox 锁内一起读取接收、清理计数和队列数量；守恒检查取消累计输入百分比容差，逐样本核对并纳入总结果。
- 尾段增长速度改为按时间加权；空质量码和非整数错误质量码作为对账差异报告。
- 完整链路只有一个有效窗口时不再误报稳态通过。
- 两脚本增加约30秒一次、带 fsync 的 `.progress.jsonl` 进度留档；必须把输出目录挂载到宿主机。进度文件不代表最终通过，不能抵御宿主磁盘本身损坏。
- Collector 历史检查改为 Redis 分页读取，核对每个设备/时间戳/分片身份及缺失、重复，按 audit-stride 抽样核对完整字段。抽样不能表述为全部历史点值逐项通过。
- 完整链路全字段内存对账增加 2,000,000 预计值单元上限，在连接服务前拒绝超限规模，防止测到最后耗尽内存。

## 独立复验

- Windows Python 3.14.6t、GIL关闭：Collector 全量 **594 passed、24 skipped、1 warning**，235.78秒。跳过的真实服务测试不计为通过。
- Writer 全量：**184 passed**。
- scripts 全量：**81 passed**。
- 进程独占锁专项额外重复 **8/8** 通过。先前宿主环境的81项失败没有在本次正确ABI与依赖路径下重现。
- 修改范围的 diff 检查通过。本轮没有运行 Docker 长测、没有重启/停止业务容器，也没有提交推送。

Collector 复验所用环境（PowerShell）：
```powershell
$env:PYTHONPATH='D:\python\device_alarm\data_watch\outputs\performance-20260924\python314t-test-deps;D:\python\device_alarm\data_watch\backend\.py314t-site;D:\python\device_alarm\data_watch\collector;D:\python\device_alarm\data_watch'
$env:PYTHONDONTWRITEBYTECODE='1'
python3.14t -m pytest collector/tests -q -p no:cacheprovider --tb=short
```

## 交给 DeepSeek 的测试顺序

1. 先核对当前文件 SHA256、解释器 ABI、GIL、依赖与 Docker 服务。复核时 Docker 服务已可查询到29.6.1；不据此推断业务容器已恢复，业务服务恢复另行获得授权。
2. 重建唯一命名的隔离环境，先做短冒烟，检查守恒残差为0、进度文件落在宿主机、最终退出码与 verdicts 一致。
3. 补做 **20设备×500点×100ms、30分钟 Collector 容量长测**。使用 validate_collector_ingest_capacity.py，输出使用新目录，按既有报告参数准备内存和磁盘护栏。发生中断保留进度并判未完成，不从进度文件宣称通过。
4. 在同一个隔离 Redis 上、固定相同资源与负载，做 everysec 与 always 的短时对照；都保持 AOF和WAITAOF，只改隔离实例，结束恢复配置。这是归因实验，不是建议修改业务持久化设置。
5. 使用 validate_docker_pipeline.py 做小规模完整链路及映射变化验证；例如默认5设备×100点×60秒在护栏内。该脚本不是生产 writer/main.py 入口，不能替代生产多worker容量验证。
6. 通过以上阶段后，再逐级进行百台千点、真实生产入口与浏览器联合负载。完整链路高负载长测需要流式全字段对账工具；不要绕过本轮内存护栏硬跑。30%余量、故障矩阵和现场硬件验收仍未完成。

之前通过的200点长测是20台设备、每秒200帧，不是百台设备、每秒1000帧。AOF等待归因仍需实验B，不可仅凭WAITAOF自身CPU计数推断刷盘等待耗时。

## 证据与版本

旧 review-package 和原始 JSON 保留为历史证据，不能再代表修复后的工作区。本次另存 `outputs/recheck-20260926-task34/review-ready/`，包含当前源文件快照、完整工作区补丁、新增文件、SHA256及回归日志。后续若改代码，必须重新记录哈希；长测结果不能沿用旧版本的通过结论。
