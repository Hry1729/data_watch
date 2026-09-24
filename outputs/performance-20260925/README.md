# 本轮原始结果索引

- aseline-100dev-<N>pt-15s.json：改动前（git a03c951）帧宽扫描，100 设备 100ms，15 秒。
- aseline-100dev-200pt-10s.json：同上规格的 10 秒试跑。
- fter-watermark-100dev-<N>pt-15s.json：只加"跳过已被快车道覆盖的 latest 段"后的同样扫描。
- fter-watermark-100dev-200pt-60s.json：60 秒长测（未达标，趋势 flat，回放=输入的 95.6%）。
- fter-watermark-100dev-1000pt-30s.json：1000 点档 30 秒；输入侧仅 655.8 帧/秒，回放 253.4 帧/秒。
- fter-watermark-100dev-1000pt-60s.json：**未完成**。运行到约 46 秒时命中 Collector 本地队列
  自身的 1 GiB 上限（PRAGMA max_page_count），SQLite 报 database or disk is full。
  磁盘并未写满，这是产品自身的容量保护。该文件保留作为 R10 的证据，不代表 60 秒达到稳态。
- smoke.json：新脚本的 4 设备×8 点冒烟，用于确认六项判定与全字段对账都能跑通。
- collector-test.Dockerfile：本轮 Collector 测试镜像定义（基于仓库既有
  deploy/python314t.Dockerfile，固定 msgspec/redis/pytest 版本）。

所有运行均 data_correctness=pass、duplicate_frames=0（未完成的 1000pt-60s 除外）。

## 整改后重跑（2026-09-25，修正计数口径）

`corrected-100dev-<N>pt-30s.json`：改用独立逻辑帧计数 + 预热后稳态窗口后的重跑结果。
100 设备、100ms、30 秒、2 CPU Collector 容器。

| 每帧点数 | 稳态确认帧/秒 | 稳态输入帧/秒 | 比值 | 趋势 | 持续回放判定 |
| ---: | ---: | ---: | ---: | --- | --- |
| 10 | 1033.75 | 1000.11 | 1.034 | shrinking | pass |
| 100 | 1024.63 | 1000.45 | 1.025 | flat | pass |
| 200 | 940.85 | 1001.82 | 0.941 | growing | fail |
| 500 | 229.31 | 1006.04 | 0.228 | growing | fail |
| 1000 | — | — | — | — | 未测：撞本地队列 1 GiB 上限（error 字段即该记录） |

`fixed-100dev-10pt-12s-catchup5s.json` / `final-100dev-100pt-20s-catchup4s.json`：
修正线程池生命周期与阶段口径后，`--catchup-seconds` 可正常运行，
并给出「净消减速率 / 额定输入速率」的余量估计（两档均为 0.0%，未达 30%）。

注：`after-watermark-*.json` 对应的水位优化**已停用**（存在正确性漏洞），
这些文件只作历史记录，其收益声明已作废，详见 `docs/performance-review-remediation-20260925.md`。
