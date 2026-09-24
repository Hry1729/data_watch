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
