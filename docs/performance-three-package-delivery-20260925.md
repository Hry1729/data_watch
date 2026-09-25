# 第三轮整改：三个交付包执行记录（2026-09-25）

对应审查给出的三包。每个包一次独立提交。

| 包 | 提交 | 状态 |
| --- | --- | --- |
| A 台账迁移可靠性 | `ac16264` | **完成** |
| B Writer 失败证据与解析 | `2c93ff5` | **完成** |
| C 测试可信度与性能验证 | `1decdcd`、`b4bbb4a` | **部分完成**：公式与受控积压工具已就绪并实测；**完整入库链路未执行**（见下） |

---

## 包 A：缺口台账迁移可靠性 —— 完成

审查指出两点，均已隔离复现后修复。

### A1 迁移无事务保护

原顺序 `建临时表 → 复制 → DROP 旧表 → RENAME` 处于自动提交模式。
在 DROP 之后、RENAME 之前中断，会留下"正式表不存在、记录困在临时表"，
重新打开时又新建空表并可能标成 schema v2 → **再次形成"看起来没有历史缺口"**。

修复：
- `_swap_in()` 把 DROP 旧表 + RENAME 收进同一个 `BEGIN IMMEDIATE` / `COMMIT`；
  全部改用单条 `execute()` 以避开 `executescript()` 的隐式提交。
  连接已在事务中时不再嵌套 BEGIN，交给调用方提交或回滚。
- `_recover_interrupted_migration()` 启动先识别中间态：
  只有临时表 → 用正式 DDL **重建**正式表并搬回数据（不能直接 RENAME，
  临时表可能是不完整结构）；两表并存 → 正式表权威、清理残留。
  **不允许"看到正式表不存在就新建空表"。**

### A2 中间版本的真实元数据被覆盖

列集合判断会把"已有 `run_id`/`session_id`/`resolved_at`、但尚无连续区间字段"的
中间版本也走旧迁移分支，而复制 SQL 一律写 `legacy-import`/`unknown`/`NULL`。
隔离复现：`known-old-run/known-old-session/resolved_at=9999` →
迁移后变成 `legacy-import/unknown/NULL`。

修复：`_migrate_to_v2()` 按源表实际列动态构造映射，**已有列原样保留**，
真正缺失的才补未知；连续区间起点缺失时用首帧时间补齐。

### A 的验证

新增 4 项（对应审查要求的四个结构各一）：
最早结构、带 `run_id` 的中间结构、当前结构、**迁移中断结构**；
另对**换表阶段做失败注入**（首次 RENAME 抛错），断言事务回滚后
旧表与记录原样保留、`user_version` 不前进，重新正常打开后迁移成功且无残留临时表。

collector 本地 96 passed / 20 skipped；真实 Redis 79 passed / 18 skipped。

---

## 包 B：Writer 失败证据与解析 —— 完成

### B1 批次统计冒充单帧证据（先止损）

`_last_sql_*_rows_attempted/succeeded` 在 `write_multi_table` 开始时清零、
随后对整批所有 SQL 累加，是**批次级**统计。原代码用它推导单帧状态：
同一批 A 帧成功、B 帧失败时成功行数=1，**B 被判成 `confirmed_written`**。
已用确定性夹具复现。

修复：单帧 `value_segment`/`quality_segment` 一律 `unknown`
（unknown 表示无法证明，**不代表未写入**）；批次统计移入
`batch_execution_summary` 并标注 `scope: entire write_multi_table batch`，
禁止用于单帧推断。

**仍未闭环**：需 writer 回传带逻辑帧身份的分段结果，才能填出单帧状态；
"结构化失败证据已闭环"不关闭。

### B2 溢出回收路径

`_recover_overdelivered()` 此前直接调 `_handle_entry()` 无批量准备。
现与 XAUTOCLAIM、XPENDING+XCLAIM 一致，先整体 `_prepare_payload_batch`
再带 `prepared` 进入。**三条回收入口 + 正常读取现已共用同一套有界批量准备。**

### B3 剩余重型转换

逐点 `float()`、有限值检查与列名映射原本留在异步 `_handle_entry`，属事件循环上的
重型工作。抽成纯函数 `_convert_values()`，随解码一起在 `_prepare_payload_batch`
里批量执行。mapping 通过新增 `_var_map_for_entry()` **只读缓存**取得
（不触发 MySQL 加载、不改变既有加载时机）；未缓存或后续 reload 时
`converted_columns` 为 None，`_handle_entry` 回退原路径，**判定语义不变**。

### B4 失败移交验证

`push_dlq` 返回 True 只是 Redis 接受写入，**不含持久确认**；
隔离记录与源消息同在 Redis，恢复取决于写入/ACK/删除/AOF 顺序。
代码注释已写明该边界。**可靠移交的恢复论证与专门故障测试仍未做。**

### B 的验证

新增 3 项：批次里另一帧的成功不得被当成本帧证据、批量准备确实产出转换结果
且非法值计入 `bad_var_ids`（map 未命中不转换）、溢出回收确实offload。
writer 176 passed。

---

## 包 C：测试可信度与性能验证 —— 部分完成

### C1 两处阶段公式 —— 已修并复算

- **排空速率**混入追平阶段完成的帧。原始数据：9150 / 追平 2050 / 最终 12000 / 1.602s，
  原报告 1779.16 帧/秒；真实停采后排空 800 帧 ≈ **499.4 帧/秒**。
  现改为在真正停止输入处取独立快照，只算排空阶段自己的增量。
- **接收速率**分子用目标帧数而非实际提交量，输入未达速时会高估。
  现分开输出 `production_receive_fps_actual` 与 `production_receive_fps_target`。

### C2 受控积压与余量工具 —— 已就绪并实测

- 新增 `--seed-backlog-frames`：先按额定速率额外生产到指定积压帧数，再进入追平。
  避免用"正常流水线遗留的积压"充当处理余量证据。
- 新增 `--catchup-rate`：追平可降速（如 0.7），用低于额定的输入处理预置积压。
- 修正预置循环节流：原先用全局 `ticks` 算时间点（已累计生产阶段时间，
  算出来早已过期 → 完全不节流，实测空转 120 秒）。

实测（2 CPU 容器）：

| 配置 | 预置 | 追平 | 结果 |
| --- | --- | --- | --- |
| 20 设备 × 500 点 | 达到目标（320 帧 > 300） | 0.7 倍速 10 秒 | 确认 146 帧/秒、净消减 6 帧/秒、**余量 3.0%**、积压 320→260 |
| 50 设备 × 100 点 | **未达到**（60 秒产 60000 帧只积压 800） | 0.7 倍速 15 秒 | 该点数下回放快于生产，**无法用于余量测试** |

结论：余量必须在**回放确为瓶颈**的配置上测。现有数据仍不足以给出最大处理余量。

### C3 完整入库链路（含 TDengine）—— **未执行**

`taosws`（TDengine 的 WebSocket 连接器，`writer/tdengine_writer.py` 依赖）
**不在 PyPI 分发**（`pip install taosws` 报 `from versions: none`）。
本机已有的是 Windows 版 `_taosws.pyd`（12.6 MB 原生扩展），
无法在 Linux 容器加载，因此 `scripts/validate_docker_pipeline.py`
（合成输入→SQLite→Redis→MySQL→TDengine）本轮**没有运行**。

已提供 `outputs/performance-20260925/pipeline-runner.Dockerfile`：
基础依赖可构建通过；把 TDengine 官方渠道的 **Linux amd64 taosws wheel**
放入 `outputs/performance-20260925/wheels/` 即可直接建成完整链路 runner。

**因此报告中不得声称"完整入库链路已验证"**，
实际输入 / 持久接收 / Redis 确认 / TDengine 完整确认四项**尚未分别对齐**。

---

## 三包之后的验收状态

| 项 | 状态 |
| --- | --- |
| 台账迁移事务化与中断恢复 | **关闭**（含四个结构与换表失败注入） |
| 保留中间版本已有元数据 | **关闭** |
| 批次统计不得冒充单帧证据 | **关闭**（先止损；逐帧归属仍缺） |
| 溢出回收与三条路径解析统一 | **关闭** |
| 逐点重型转换下沉 | **关闭**（判定语义未变） |
| 测试两处阶段公式 | **关闭** |
| 受控积压余量工具 | **关闭**（工具就绪并实测） |
| 冻结版本持续容量与 30% 余量 | **未完成**（需在回放为瓶颈的配置上复测） |
| 完整入库链路（含 TDengine） | **未完成**（环境限制，见 C3） |
| 可靠移交恢复论证与故障注入 | **未完成** |
| 查询侧完整性过滤 | **未完成** |
| 浏览器绘制延迟 | **未完成** |
| 宽帧提速 | **未完成**（旧路线已停用；下一步按"标量时间戳 + 减少重复序列化"做小实验） |
