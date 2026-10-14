# Writer 目标机更新与三组验证

本包只更新 Writer，并提供诊断工具。前后端、Collector、Agent 和数据库镜像无需重新部署。分片数、扫描周期、缓冲上限及 ACK/DLQ 规则保持原样。

**已安装 2026-10-14 新中心式安装包（源码 `8ca1f42f`）时，Writer 已更新，直接从第 2 节设置基线；无需再运行 `update_writer.ps1`。** 只有保留旧安装包、单独更新 Writer 时才执行第 1 节。

源版本、Python/依赖版本、原生连接器和所有交付文件的 SHA256 在 `release-manifest.json`。Writer 内的 `writer-release.json` 保留同一版本信息。脚本修订包保留原 Writer 二进制，另用 `script_revision` 记录脚本提交。原生连接器复用上轮 GIL 补丁 wheel，打包时核对 wheel、构建环境、交付包内 `.pyd` 三者的二进制哈希；不声明 wheel 字节可重复构建。

目标机需 Windows PowerShell 5.1 或 7、正在运行的 Backend 容器及 Docker 命令，无需另装 Python。请使用管理员 PowerShell，以便取得 Writer CPU 指标和更新安装目录。本机未做性能压测；吞吐和实际数据库验收由本轮目标机报告决定。

请将本包解压到 D 盘，例如 `D:\writer-target-8ca1f42f`。更新和参数脚本的操作日志位于解压目录的 `operation-logs`，测试报告位于 `target-results`；日志不写入 C 盘。原配置、配置备份及安装目录仍在各自原位置。某一步报错时先停下，不要继续下一组或执行 `capture`。

## 1. 更新前

解压 ZIP，进入解压目录。在应用控制台停止 **Writer**，等状态显示停止；其他模块保持运行。必须通过应用停止，使 Agent 记录期望停止状态；单独结束 PID 会被 Agent 自动拉起。

中心式默认路径如下。自定义安装时改为实际路径；分布式中心通常是 `C:\Program Files\DataWatch Distributed Center\writer` 和 `C:\ProgramData\DataWatch Distributed Center\writer\config\settings.toml`。

```powershell
$writer = "C:\Program Files\DataWatch Centralized\writer"
$settings = "C:\ProgramData\DataWatch\writer\config\settings.toml"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\update_writer.ps1 -WriterDirectory "$writer"
```

更新器先核对 payload 哈希，再验证完整新目录能在同一父目录重命名，通过后保留整份旧 Writer 并切换新目录。重命名不会把源目录嵌套进已有目标目录。它不会覆盖 ProgramData 中的连接配置，也不清理 Redis 或数据库。记下输出的 `writer.backup-...` 路径。若仍报拒绝访问，回传 D 盘的 `operation-logs`，里面有错误码和相关目录权限；无需自行放宽安装目录权限。

## 2. 基线：4 Worker / 每流200帧

Writer 仍停止时执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\writer_test.ps1 -Mode baseline -SettingsPath "$settings"
```

参数脚本只设置 `[writer]` 的 `consumer_workers`、`batch_count`，其余参数从首次运行时的原文件保留。原配置备份留在配置目录，沿用原文件 ACL；备份可能含连接配置，请留在目标机，不放进回传报告。

在应用中启动 Writer，约60秒后执行：

参数脚本只改配置，不自动启动进程。`capture` 提示 Writer 未运行时，应先在应用中启动 Writer；它不是性能测试结果。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\writer_test.ps1 -Mode capture -SettingsPath "$settings" -DurationSeconds 300
```

默认日志目录根据配置位置推导为数据根下的 `runtime`。运行目录有自定义时，额外传 `-RuntimeRoot "实际runtime目录"`。Backend 容器改名时加 `-BackendContainer "实际名称"`；消费组不是 `writer_cg` 时加 `-RedisConsumerGroup "实际组名"`。需确认 Backend 和 Writer 连接同一组 Redis、MySQL 和 TDengine。

## 3. 8 Worker / 每流200帧

先在应用中停止 Writer，再执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\writer_test.ps1 -Mode workers8 -SettingsPath "$settings"
```

在应用中启动 Writer，约60秒后再次执行上面的 `capture`。有效 Worker 数最多等于 Stream 数；本次目标形态应为两类 scope 各4分片，共8个 Stream。报告没有8个有效 Worker 时，不能把这轮视为8 Worker对照。

## 4. 4 Worker / 每流400帧

先在应用中停止 Writer，再执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\writer_test.ps1 -Mode batch400 -SettingsPath "$settings"
```

在应用中启动 Writer，约60秒后再次 `capture`。此轮同时将 Worker 恢复为4，缓冲上限仍沿用原配置。`batch_count` 还控制部分 PEL 回收预算，这个影响需要结合报告判断。

## 5. 收尾与回退

测试后先停止 Writer，恢复原参数，然后在应用中启动 Writer：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\writer_test.ps1 -Mode restore -SettingsPath "$settings"
```

若还需要回退二进制，Writer 停止时使用更新器给出的旧版本备份路径：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\update_writer.ps1 -WriterDirectory "$writer" -RollbackBackup "实际writer.backup目录"
```

参数脚本检测到测试期间的其他配置修改时会拒绝覆盖；原备份仍保留，可先核对那次修改。更新/回退失败时保留临时目录和旧版本，不强杀进程或删除数据。

## 回传与判定

回传解压目录内的整个 `target-results`，每轮会有独立目录，包含：

- `report.md`、`summary.json`、`samples.jsonl`：吞吐、积压、循环分解、执行器排队/调用/恢复和线程 CPU；
- `test-profile.json`：实际配置的 Worker、batch_count、缓冲上限与配置哈希；
- `data-check.json`：开始观察时只读抽取的至多8帧，在计时结束后对照实际值表和质量窄表。每个抽样帧核对全部已映射点；参考值只在内存传递，不写报告。

以成功确认入库速度、持续输入下的积压趋势和失败计数为主。三轮输入速率、初始积压不同，不能只比峰值或最后排空时间。新计时只比较同层阶段；线程 CPU 时钟对短调用的零值不能证明没有计算。

抽查 `pass` 仅证明这些帧的值和质量；`not_tested` 表示没有参考帧、映射/连接不可用或尚未查到落库，不能当作通过。同一主键的重写可能被 TDengine 覆盖，行数不增加无法证明从未重复执行，也不能据此证明全部历史数据完整。全量对账仍需受控的输入清单。

诊断脚本退出码0只表示生成了报告。不要把报告生成、源版本固定或离线校验写成目标机性能已通过。
