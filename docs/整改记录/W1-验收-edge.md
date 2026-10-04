# W1 独立验收报告（edge 组）｜B25 + S3

- 验收者：**W1 验收 agent（edge 组，独立于任何修复者）**
- 依据：`docs/整改记录/W1-验收协议.md`（§0 硬性规则、§1.1 串行回归、§2 假修复模式、§3 反证要点、§4 输出格式）、`docs/整改记录/隔离测试环境.md`
- 被验收对象：`docs/整改记录/B25.md`（W1-E1）、`docs/整改记录/S3.md`（W1-S3）
- 工作目录：`D:\python\device_alarm\data_watch`；HEAD 实测 `6845e800990f8352f46655036741bd79ca25eed1`
- 验收时间：2026-10-03 13:20–14:10（本地）
- 环境：系统 `python` 3.14.6（cp314）、pytest 9.1.1；全部测试在 `%TEMP%\dw_edge_verify\` 临时目录 + 回环临时端口 + 临时 SQLite 内完成
- **未触碰**：5 个 `data-watch-*` 现场容器、`C:\ProgramData\DataWatch Edge`；未做任何 git 写操作；验收前后 `git stash list` 仍为 **4** 条，未跟踪文件原样保留

## 0. 验收者自己核对的基线（与修复者无关的事实）

| 项 | 我的实测 |
|---|---|
| `git rev-parse HEAD` | `6845e800990f8352f46655036741bd79ca25eed1` |
| 现场栈（`docker ps`，只读） | `data-watch-frontend / backend / redis / mysql / tdengine` 全部 `Up 2 hours`（§3.7 基线为 `Up 17 minutes`，单调增长、未重启） |
| 现场 Redis `db0`（只读 `INFO keyspace`） | `db0:keys=10,expires=0`；`databases`=16 → 与 §3.7 基线一致 |
| 隔离栈 | `dw-test-redis` / `dw-test-mysql` `Up 2 hours (healthy)` |
| 我改动仓库实现文件 | **否**。验收开始与结束时对 6 个实现文件取 sha256，逐一相同（见 §3） |

**pristine sha256（验收开始 = 验收结束）**

```
32d05237bce609cf6826cdeca4d6cc92e46ca0400d779898861ad919afebefb7  edge/uploader_main.py
e63d894fa28f8a86a96ab795f0cc8e4b37d6b4c4877ee7c500141ee340bcf459  edge/app/uploader.py
f05d9e4cdde33df12c27a50185f850d870da621d28374d54c4b766a002c048bb  edge/app/storage_service.py
d8d43c78bd0f723f324f4632632a0831c4cf15ae8fb4b12b1b29d237282ab1e0  edge/app/storage_client.py
b24103b9a2813556285f1f578fd8bc4021f42ff357eb566ea5ffc004ee3f83ac  edge/storage_main.py
20d5901205029154affd2346cc56fdeb03fc881def12215b8b2df6f1f55a2abe  agent/app/services/edge_storage_client.py
```

---

## 1. B25 验收结论

### 验收者 / 依据的修复报告
验收者：W1 验收 agent（edge 组）。依据：`docs/整改记录/B25.md`；任务卡 `edge/uploader_main.py` 构造与取锁顺序、`edge/app/uploader.py` 构造期恢复。

### 我重跑的命令与真实输出（关键片段）

```text
$ python -m pytest edge/tests -q                     # workdir=仓库根
84 passed in 16.71s                                   # 首轮
84 passed in 16.94s                                   # 收尾复跑（串行，见 §3 并发说明）

$ python -m pytest edge/tests/test_outbox_b25_lock_order.py -q
4 passed in 1.17s

$ git diff --numstat -- edge/uploader_main.py edge/app/uploader.py
17      10      edge/uploader_main.py
71      12      edge/app/uploader.py
```

**代码顺序复核（静态 + 动态）**

```text
uploader_main.py：with SingleInstanceLock 在第 92 行，await uploader.recover_inflight() 在第 101 行
uploader.py：构造器段（recover_inflight 定义之前）不含 recover_inflight 调用
我自建的 Recorder store：Uploader(...) 构造后 recorder.calls == []   → 构造器零存储调用
```

真实入口（我自建的双/多进程夹具，真实 `SingleInstanceLock` + 真实 Edge Storage HTTP IPC）胜方日志：

```text
2026-10-03 13:51:28,517 | INFO | edge.uploader | Uploader starting node=node-b25-verify ...
2026-10-03 13:51:28,592 | INFO | httpx | HTTP Request: POST http://127.0.0.1:62792/v1/command "HTTP/1.1 200 OK"
2026-10-03 13:51:28,592 | INFO | edge.uploader | Recovered 3 inflight outbox records
```
败方日志尾部：
```text
  File "...\edge\app\uploader.py", line 655, in __enter__
    raise RuntimeError("another uploader instance is running") from exc
RuntimeError: another uploader instance is running
```

### 与修复者报告的差异（逐字核对结果）

| 项 | 修复者报告 | 我的实测 | 判定 |
|---|---|---|---|
| `edge/tests/test_outbox_b25_lock_order.py` | `4 passed in 1.18s` | `4 passed in 1.17s` | 一致 |
| edge 全量 | 43 → **47 passed** | 现在 **84 passed**（43 基线 + B25 4 + S3 13 + 同 agent 的 B5/B6 20） | 数字不矛盾（其它条目也在同一模块） |
| `edge/app/uploader.py` 改动量 | `git diff --stat`：**+9/-1** | `git diff --numstat`：**+71/-12** | **不一致**（该文件同时含同 agent 的 B5/B6 改动；报告把整文件的行数记成只有 B25 的增量） |
| 构造器无副作用 | 自述已删除 | 我独立复核：Recorder 记录为空 + 影子回注后 4 用例全红 | 一致 |
| 「败方在未发起任何存储调用之前即因锁失败退出」 | 自述 | 我测得败方日志无 `Recovered`、无 `POST /v1/command`，且 outbox 逐行/哈希/mtime 不变 | 一致 |

### 我构造的反例与结果

反例全部用**我自写的夹具**（`%TEMP%\dw_edge_verify\b25_verify.py`、`b25_split.py`），不依赖修复者的测试文件。

1. **失败实例是否零状态写入（4 进程并发，比修复者的 2 进程更强）**
   `4 个真实 edge/uploader_main.py 子进程同时启动（同一 root/库/锁）`
   ```text
   [4proc] alive=[0] exited=[1, 2, 3] rc=[1, 1, 1]
   [OK ] 并发 4 进程中恰好 1 个存活
   [OK ] 败方全部非零退出 / 日志为锁冲突 / 日志无任何存储调用
   [OK ] 追加 3 个败方后 outbox 内容逐行不变
   [OK ] 追加 3 个败方后 edge.db sha256 不变 :: 7436604fe5d43e68 -> 7436604fe5d43e68
   [OK ] 追加 3 个败方后 edge.db mtime 不变
   ```
   追加的 3 个败方用 `subprocess.run` 真实等待退出（`rc=1` 且日志含 `another uploader instance is running`），排除「被 terminate 而误判」。
   **附加观测（低危，新发现）**：每个败方在互斥区之外会向锁文件 append 1 字节 —— 锁文件大小随失败次数 1→4→7→10 单调增长（实测）。这不是 outbox 状态写入，但属无界增长；`SingleInstanceLock.__enter__` 在 `msvcrt.locking` 之前就 `write(b"0")`。

2. **取锁成功后、恢复完成前崩溃，再启动能否恢复旧租约**
   为不碰仓库文件，我把 `edge/uploader_main.py` 复制到临时目录并在「取锁之后、`Uploader(...)` 构造之前」注入 `await asyncio.sleep(8)`（仅加一行，顺序不变；仓库文件 sha256 复核一致）。
   ```text
   [crash] 4s 时存活=True outbox={'inflight': 3} 锁被占用=True
   [OK ] 崩溃窗口内：已持锁但尚未恢复（3 条仍 inflight）
   [OK ] 崩溃窗口内：锁确实被占用
   [crash] 已硬杀（rc=1），outbox = {'inflight': 3}
   [OK ] 窗口内崩溃后重启：新实例正常恢复旧崩溃租约 :: {'pending': 3}
   [OK ] 重启实例日志确认恢复 3 条 :: ['... Recovered 3 inflight outbox records']
   [OK ] 窗口内崩溃未丢失任何记录（总数仍 3）
   ```
   → 该崩溃点不产生不可恢复状态（新实例 lease_owner 必然不同，故旧租约可被回收）。

3. **≥3 进程并发（修复者自述的未覆盖项，已补）**：见反例 1（4 进程），恰好 1 个胜出。

4. **正常运行实例的 inflight 不被重置（自伤）**
   ```text
   [OK ] 运行 12s 后自己的 live 租约未被自己重置 :: (1, 'inflight', 'solo-live', ...)
   [OK ] 运行 12s 后库内容 sha256 不变（无自伤写入）
   ```
   另在 4 进程场景中人为置入 `live-owner` 未过期租约，7 个败方轮番尝试后该行仍为 `inflight/live-owner`。

5. **★ 新反例（修复者自述的未覆盖项，我实测确认它不是理论问题）：锁与库分指**
   场景：`DATAWATCH_EDGE_DB` 指向**同一个** outbox 库，`DATAWATCH_DATA_ROOT` 分指 `rootA` / `rootB`（`uploader_main.py` 只用后者派生锁）。
   ```text
   [A] 取锁 ...\rootA\edge\uploader.lock 并恢复崩溃租约: True -> [(1,'pending',None),(2,'pending',None)]
   [A] 运行中，人为置入 A 的 live 租约: [(1,'pending',None),(2,'inflight','A-live-owner')]
   [B] 启动后: [(1,'pending',None),(2,'pending',None)]
   [A] 存活=True  [B] 存活=True      锁A=True  锁B=True
   [B] 日志: ... 'Recovered 1 inflight outbox records'
   [结论] 同库不同锁可同时运行 = True
   [结论] B 重置了 A 运行中的 live 租约 = True
   [结论] A 的 live 租约: (2,'inflight','A-live-owner') -> (2,'pending',None)
   ```
   → **验收要求第 3 条（「正常运行实例的 inflight 不被重置」）在一条配置组合下被实测违反**，且两个「单实例」同时运行。
   **可达性判断（诚实结论）**：`edge/app/paths.py:9` 让 `DATAWATCH_EDGE_DB` 成为**优先**旋钮，`uploader_main.py` 的锁只认 `DATAWATCH_DATA_ROOT`，二者本就可分指；仓库自身就把它当独立旋钮用（`edge/tests/test_uploader_main.py:27` 只设 `DATAWATCH_EDGE_DB`）。但随包部署链（`agent/app/routers/admin.py:133-134,161-162,189-190`）**总是同时**把两者设为同一 `data_root()`，故默认部署不会触发。
   **是否削弱 B25 的结论**：是 —— B25 的正确性依赖「同锁 ⇒ 同库」这一**未被部署层保证、也未在代码层强制**的前提；该前提不成立时，修复只把「抢锁失败者写状态」收敛掉了，却挡不住「两个都抢到各自锁的实例互抢租约」。闭合方式在授权文件内即可实现（在 `uploader_main.py` 里由 `edge_database_path()` 派生锁路径），属最小改动范畴，建议补。

6. **回注反证（可选加分项，全程在 `%TEMP%` 影子树内，仓库文件零风险）**
   工具先断言 pattern 命中（各 1 次）再写入影子副本：
   ```text
   [inject] B25-a 构造器内 recover_inflight: edge/app/uploader.py 已按 pattern 命中（1 次）改回旧行为
   [inject] B25-b 构造早于取锁: edge/uploader_main.py 已按 pattern 命中（1 次）改回旧行为
   --- [回注 B25] rc=1 ---   4 failed in 2.25s
   仓库文件未被改动 = True（三个文件 sha256 与 pristine 逐一相同）
   ```
   → 修复者自己的 4 个用例在旧行为下**全部失败**，测试非空转。

### 反向检查（是否矫枉过正 / 是否破坏合法链路）
- 胜出实例仍能经真实 IPC 恢复（`Recovered 3 inflight outbox records`，HTTP 200）。
- 正常运行实例不被自己重置（反例 4）。
- 除锁语义外无行为变化：`Uploader` 构造器只少了 1 次存储副作用，`close()` 进入锁内 `finally`、`store.close()` 留在外层，实测进程退出码/日志正常（`Uploader stopped`）。
- 未发现因「收紧」导致合法启动被误拒的情况（`recover_inflight` 的 `lease_owner <> ?` 语义**未收紧**，修复者已说明理由，我认同：收紧会让刚崩溃重启的实例空等一个租约周期）。

### 假修复模式排查结果（协议 §2 逐项）
| 模式 | 结论 |
|---|---|
| try/except 吞异常 | 无新增（`edge/app/uploader.py` B25 段无新增 except） |
| 放宽断言（`==`→`>=`、删 assert） | 无；B25 新用例为精确断言（`store.stats() == {...}`、逐行快照相等） |
| 新增 skip/xfail/pytest.skip | `edge/tests` 全目录 grep 无任何 skip/xfail 标记 |
| 注释掉既有断言 | `edge/tests` 仅 `test_storage_service.py` 被改（S3），assert 数 15→15 |
| 只改 mock/fake 层 | 否：我用真实子进程 + 真实文件锁 + 真实 HTTP IPC 独立复现 |
| 改测试迁就代码 | B25 新用例是「先复现缺陷再固化契约」，回注后全红，非迁就 |
| 把永久拒绝整体删掉 / 鉴权默认关闭 | 不适用（B25 无关） |
| 其它条目专属模式 | 不适用 |

### 回归数字对照
| 模块 | W0-A 基线 | 现在（我串行实测） | 差异 |
|---|---|---|---|
| edge | 43 passed | **84 passed / 0 failed**（两次均为 84） | +41，全部为新增用例；无跳过、无 xfail、无既有测试被删 |

### 越界检查（git diff --stat 是否只碰授权文件）
- 授权内：`edge/uploader_main.py`（顺序）、`edge/app/uploader.py`（构造器去副作用）、新建 `edge/tests/test_outbox_b25_lock_order.py`。
- **注意**：`edge/app/uploader.py` 的 diff 同时包含同 agent 的 B5/B6 改动（`_persist_confirmed`、`_quarantine_budget_exhausted` 等），因此无法从 diff 中单独隔离 B25 的行数；这解释了上方「+9/-1 vs +71/-12」的差异，不构成越界（B5/B6 亦为该 agent 的条目），但**修复报告的行数陈述不准确**。
- 未发现删除既有死代码、未改 `edge/tests/conftest.py`、未改其它既有测试。

### 证据等级
**真实入口 + 隔离集成**（真实 `edge/uploader_main.py` 子进程、真实 `msvcrt` 文件锁、真实 Edge Storage HTTP IPC、临时 SQLite）；配套我自建的构造器级与静态顺序检查（单元）。未做目标机验收（未授权）。

### 最终裁决
**证实（有条件）**
- 主体结论成立：取锁确实早于一切 outbox 状态恢复；构造器零存储写入；失败实例零 outbox 状态写；胜出实例正常恢复崩溃租约；运行中实例的 inflight 不被重置；≥3 进程并发成立；修复者的测试非空转（回注全红）。
- **条件**：以上第 3 条以「同锁 ⇒ 同库」为前提。当 `DATAWATCH_EDGE_DB` 与 `DATAWATCH_DATA_ROOT` 分指时，该前提不成立，两实例可同时运行且互抢**未过期**租约（实测复现）。默认部署链不会产生该组合，故不判「推翻」，但**必须列为遗留缺陷**。

### 遗留问题
1. **P1**：锁路径由 `DATAWATCH_DATA_ROOT` 单独派生，与 `edge_database_path()` 可解耦 → 同库不同锁时验收要求第 3 条可被违反（已实测）。建议在 `edge/uploader_main.py`（授权文件内）改为由 `edge_database_path()` 派生锁路径，或在取锁后校验「锁定身份与库身份一致」。
2. **P2**：`SingleInstanceLock.__enter__` 在加锁前 `write(b"0")`，每个失败进程向共享锁文件 append 1 字节（实测 1→4→7→10），无上限增长（非 outbox 状态写，不影响 B25 判定）。
3. **未覆盖**：目标机验收（WinSW/NSSM 服务账户、非交互会话下的 `msvcrt` 行为、多用户拓扑）；多个 `DATAWATCH_DATA_ROOT` 的服务真实部署方式未核实。

---

## 2. S3 验收结论

### 验收者 / 依据的修复报告
验收者：W1 验收 agent（edge 组）。依据：`docs/整改记录/S3.md`；清单 S3（1431–1443 行）三条验收要求 + 「不要复用 Agent service token / 不要把 Storage 开到全部网卡」。

### 我重跑的命令与真实输出（关键片段）

```text
$ python -m pytest edge/tests/test_storage_ipc_auth_s3.py -q
13 passed in 6.35s                       （修复者报告：13 passed）

$ python -m pytest edge/tests/test_storage_service.py -q
4 passed in 1.45s                        （修复者报告：4 passed）

$ $env:PYTHONPATH="$PWD\agent\.venv-pack\Lib\site-packages"; python -m pytest agent/tests/test_s3_storage_ipc_token.py -q
4 passed in 2.66s                        （修复者报告：4 passed）

$ python -m pytest edge/tests -q
84 passed in 16.71s / 16.94s             （见 §3）
```

我自建夹具（`%TEMP%\dw_edge_verify\s3_verify.py`、`s3_verify2.py`、`sim_check.py`）的**无凭据攻击**实测（真实 `StorageEngine` + 真实 `ThreadingHTTPServer`，刻意**不传** `authenticator` 以走生产默认 `IpcAuthenticator.from_file()`）：

```text
=== A. 无凭据（裸 httpx，无任何自定义 header）===
[OK ] unauth config.apply / frame.commit / latest.write / device.status / outbox.recover /
      outbox.claim / outbox.complete / outbox.retry / session.prepare / session.complete /
      session.current / maintenance / status  -> 全部 HTTP 401  {"ok":false,"error":"missing or invalid Edge Storage IPC token"}
[OK ] 无凭据攻击后 outbox 行数不变 :: [] -> []      （config revision 亦不变）
--- A2. 伪造/空/错令牌：''、64 个 0、not-a-token、篡改末位、Bearer <真令牌>、大写化真令牌 -> 全部 401
--- A3. GET /v1/status -> 401；GET /healthz -> 200；GET /v1/unknown -> 404；POST /other -> 404；
        PUT/OPTIONS /v1/command -> 501；坏 JSON -> 400 且状态未变；无 token 字段的合法 JSON -> 401
=== D. 三步链（攻击者抢租约 -> claim -> complete(sent)）===
[OK ] 链步 1/2/3 无凭据 outbox.recover/claim/complete -> 401
[OK ] 三步链后 outbox 逐字节不变        [OK ] chain via collector token -> 403
[OK ] 合法 uploader 仍能 complete :: [('real-frame', 1, 'sent', None, None, 0)]
```
→ 修复者「改后全 401」的结论**逐条复现**，且我是在**不看其测试文件**的情况下自建夹具得到。

### 与修复者报告的差异（逐字核对结果）

| 项 | 修复者报告 | 我的实测 | 判定 |
|---|---|---|---|
| 13 / 4 / 4 passed | 一致 | 一致 | 一致 |
| 无凭据全 401、状态零变化 | 自述 | 我自建夹具逐条复现（13 类动作 + 三步链） | 一致 |
| `--host 0.0.0.0` → exit 2 | 自述 rc=2 | rc=2，且我另测 `::` / `0` / `192.168.1.10` / `example.com` / `0:0:0:0:0:0:0:0` 亦 rc=2，均零写入 | 一致且更强 |
| **`scripts/simulate_edge_collector.py`「不受影响」** | S3.md §部署兼容第 5 条 | **实测 rc=1 崩溃**（403，见反例 2b） | **不一致（自述被证伪）** |
| 改动文件清单 | 列 7 个文件 | `edge/app/storage_service.py`(+166/-3)、`storage_client.py`(+92/-1) **还含 B6 的 `reject_count`/`http_status` 改动**，其改动表未列 | 不一致（清单不完整，不属安全性回归） |
| 「无第三方依赖」 | 自述 | 我解析 diff 新增 import：`ipaddress/hmac/secrets/subprocess/json/pathlib/sys` + 内部模块，无第三方 | 一致 |

### 我构造的反例与结果

1. **完全不带凭据的 13 类动作 + 三步链** → 全 401，outbox 与 config 零变化（见上）。另测「坏令牌 6 种」全 401；`GET /v1/status` 401、`/healthz` 200、未知路径 404、非 POST/GET 方法 501。
2. **合法三方分别验证（真实客户端库 + 真实入口点角色推断，不显式传 `role`）**
   ```text
   [legit] collector role=collector token_present=True
   [OK ] collector.commit_frame 成功 :: local_seq=1
   [OK ] collector 白名单 5 动作全部成功（frame.commit / latest.write / device.status / config.refresh_schedule / outbox.snapshot）
   [legit] uploader  role=uploader token_present=True
   [OK ] uploader.upload_interval_ms / claim_batch / session 切换 / maintenance / status 全部成功
   [legit] agent     token_present=True   （用 Agent 自己的 urllib 客户端）
   [OK ] agent.apply(config.apply) 成功 :: rev=5   + config.revision / refresh_schedule / status
   ```
   越权矩阵（3 角色 × 18 动作 = 54 组，逐组核对白名单）：除 `session.complete` 传伪造 state 得 500（载荷错误、角色本身被允许）外，**全部符合预期**（白名单内 200、白名单外 403）。
2b. **★ 新反例（证伪修复者的一处自述）**：仓库自带 `scripts/simulate_edge_collector.py` 第 270-271 行 `EdgeStorageClient()` → `upload_interval_ms()`，而 collector 白名单**不含** `outbox.upload_interval`：
   ```text
   [sim] 角色推断 = collector
   [sim] upload_interval_ms() 抛异常: StorageUnavailable: ... 403 Forbidden
   [real script] rc=1
   [real script] stdout: edge database: ... / QC100/g001: period=100ms points=1 ids=[1] / QC101/g001: ...
   [real script] stderr: edge.app.storage_client.StorageUnavailable: Edge Storage unavailable: Client Error '403 Forbidden' ...
   [对照] uploader -> outbox.upload_interval = HTTP 200      [对照] collector -> 同动作 = HTTP 403
   ```
   该脚本是仓库内合法的第一方调用方（角色推断确实为 `collector`，但**动作**越界），修复者只核对了角色推断、未核对动作白名单，故「不受影响」不成立。属功能性回归（fail-closed，非安全缺陷）。
3. **凭据强度与来源**
   ```text
   [OK ] 三份令牌互不相同 / 长度 64 hex
   [OK ] 令牌不含哨兵 DATAWATCH_AGENT_SERVICE_TOKEN（我用哨兵值注入，未读真实令牌文件）
   [OK ] git grep AGENT_SERVICE_TOKEN|agent_service_token -- edge/ agent/app/services/edge_storage_client.py → 0 命中
   [icacls] storage-ipc-tokens.json → 仅 3 个显式 ACE（属主 / BUILTIN\Administrators / NT AUTHORITY\SYSTEM），无 (I) 继承项
   [OK ] 空 tokens 的令牌文件导致 StorageHttpServer 构造抛 ValueError（fail-closed，不会无认证启动）
   ```
   另：代码中**不存在**关闭认证/匿名放行的开关（grep `DISABLE|ALLOW_ANON|NO_AUTH|INSECURE|SKIP_AUTH|allow_anonymous` → 0 命中）；`StorageHttpServer.__init__` 默认即 `IpcAuthenticator.from_file()`，漏传参数不会降级为无认证。
4. **★ 新反例（守卫生效性缺陷）：`--host ""` 绕过非回环拒绝**
   ```text
   [pred] _is_loopback('') = True      ← 修复者自己的用例也把它断言成回环地址
   [pred] _is_loopback(' ') = True
   [empty-host] child alive after 6s = True (rc=None)
   [empty-host] listeners on 7099 = '0.0.0.0'
   [empty-host] 本机非回环 IPv4 = '172.18.112.1'
   [empty-host] GET http://172.18.112.1:7099/healthz -> HTTP 200
   日志: Edge Storage listening on http://:7099 database=... ipc_token_file=...
   ```
   → `--host ""`（或 `DATAWATCH_EDGE_STORAGE_HOST=""`）**未被拒绝**，真实入口把 IPC 绑到 **0.0.0.0** 且可被非回环地址访问，直接违反清单「不要将 Storage 开到全部网卡」。`socket.bind(("", port))` 语义即 INADDR_ANY，而 `_is_loopback("")` 返回 True 是**误判**；`edge/tests/test_storage_ipc_auth_s3.py:473` 与已同步文档（`docs/04-运维部署/README.md:589`）都把「非回环一律拒绝」写成了无条件事实。触发面有限（显式空 `--host` 或空环境变量；部署链 `admin.py:192` 有 `or "127.0.0.1"` 兜底），但不能算「真被拒绝」。
   反向：`_is_loopback("127.1")` 返回 False → 合法回环短写被**过度拦截**（rc=2，可用性小问题）。
5. **反向检查（是否矫枉过正 / 破坏合法链路）**：白名单完整性我按真实调用点逐一核对 —— collector：`edge_outbox_writer.py:25,46,49,51,95` + `config_snapshot.py:425,469` → 只用到 `outbox.snapshot / latest.write / frame.commit / device.status / config.refresh_schedule`，**全部在白名单内**；uploader：`uploader.py` 全部动作在白名单内；agent：4 个动作在白名单内。唯一缺口是反例 2b 的 `scripts/simulate_edge_collector.py`。另 `/v1/status` 变为需令牌，运维文档已在工作区同步修订（`docs/04-运维部署/README.md:583-589`、`问题排查.md:355-361`、`边缘运行时实现说明.md:33`）。
6. **我另外尝试但未能推翻的（诚实记录）**
   - 路径/方法绕过：`//v1/command`、`/v1/command?x=1`（→404）、`PUT/OPTIONS`（→501）、双 token 头、空格/大小写变体 → 均未绕过。
   - 令牌文件损坏/空/缺 `tokens` 键 → 服务拒绝启动（fail-closed），未出现「无认证兜底」。
   - 是否另有本机监听面：grep `edge/`、`collector/` 全目录仅 `StorageHttpServer` 一处监听（`storage_main.py:74`）。
   - 越权：用 collector / agent 令牌调 uploader 专属动作全部 403；用 uploader 令牌调 `frame.commit/latest.write/config.apply/device.status` 全部 403。
7. **直接改 SQLite 绕过 IPC（信任边界披露，非本条目缺陷）**
   ```text
   [direct-db] 未持任何 IPC 令牌直接 UPDATE 后 outbox = [('legit-1','sent'), ('x','pending')]
   [direct-db] edge.db ACL      = MAGICHU\CodexSandboxUsers:(I)(M,DC) | <SID>:(I)(M,DC) | SYSTEM:(I)(F) | Administrators:(I)(F) | myrh:(I)(F)
   [direct-db] 令牌文件 ACL     = myrh:(F) | Administrators:(F) | SYSTEM:(F)      ← 无继承、更严
   ```
   → IPC 层已认证，但 `edge.db` 本身是普通 SQLite 文件：任何对它有写权限的本地主体无需任何令牌即可改 outbox。实测该库**只有继承 ACL**（本机含一个非属主的 `Modify` ACE），而令牌文件被显式收紧 → 即「令牌文件的保护强于它所要保护的库」。故清单验收要求「普通本地账户/无身份进程无法更改 outbox」在当前实现下**由安装器目录 ACL 单独承担**（修复者已列为「需架构决策/未覆盖」）。我未创建第二本地账户，故「跨账户不可写」**未验证**。（补充分析：令牌文件与 `edge.db` 同目录，若父目录对他人可写，删除并重建令牌文件可绕过文件级 ACL —— 需第二账户才能实证，标未覆盖。）

### 假修复模式排查结果（协议 §2 逐项）
| 模式 | 结论 |
|---|---|
| try/except 吞掉异常让测试变绿 | 新增 except 仅 4 处：`load_client_token` 的 `(OSError, ValueError)→None`（fail-closed）、`ensure_token_file` 的 `FileExistsError`（竞争处理，正确）、`_restrict_token_file_acl` 的 `(OSError, SubprocessError)→WARNING`（已披露的尽力而为 ACL，**不是**吞掉鉴权判定） |
| 放宽断言 | 既有 `test_storage_service.py` assert 数 **15→15**；新用例为精确状态码/精确库状态 |
| 新增 skip/xfail | `edge/tests` 全目录无 skip/xfail 标记 |
| 注释掉断言 | 无（逐条 diff 核对） |
| 只改 mock/fake 层 | 否：我用真实 HTTPServer + 真实 SQLite + 真实回环端口 + 真实 `storage_main.py` 子进程 |
| 改测试迁就代码 | `test_storage_service.py` 把一个跨角色 client 拆成 3 个按角色的 client（修复者已获准并披露），断言与命令覆盖面不变；这确实使该既有用例不再覆盖「自动角色推断」——我用自建夹具补测了自动推断（反例 2） |
| **把鉴权默认关闭 / 整体跳过校验** | **未发生**：默认即加载令牌文件、无关闭开关、令牌文件无效则拒绝启动（均实测） |
| 把「永久拒绝」整体删掉 | 不适用；我做了反向回注证明「若删掉鉴权，其测试会红」 |
| B15/B41 专属模式 | 不适用 |

### 回注反证（可选加分项，影子树内，仓库零风险）
```text
[inject] S3 移除 IPC 认证与角色白名单: edge/app/storage_service.py 已按 pattern 命中（1 次）改回旧行为
--- [回注 S3] rc=1 ---   8 failed, 5 passed in 6.31s
仓库文件未被改动 = True（三个文件 sha256 与 pristine 逐一相同）
```
→ 13 个用例中与认证/白名单相关的 8 个在旧行为下失败（另 5 个是与鉴权无关的令牌生成/回环解析用例），认证断言非空转。

### 回归数字对照
| 模块 | W0-A 基线 | 现在（我串行实测） | 差异 |
|---|---|---|---|
| edge | 43 passed | **84 passed / 0 failed** | +41；其中 S3 新增 13；无 skip/xfail；既有测试仅 `test_storage_service.py` 被改且 assert 15→15 |
| agent（仅 S3 新增文件） | 253 passed / 1 skipped（W0-A） | `agent/tests/test_s3_storage_ipc_token.py` **4 passed** | 与修复者一致；**agent 全量我未跑**（S3.md 报告 305 passed/1 skipped，未由我复核 → 标未覆盖） |

### 越界检查（git diff --stat 是否只碰授权文件）
- 授权内：`edge/app/storage_service.py`、`edge/app/storage_client.py`、`edge/storage_main.py`、新建 `edge/tests/test_storage_ipc_auth_s3.py`。
- 清单外但**已在报告中声明并称获协调者批准（方案 A/B）**：`agent/app/services/edge_storage_client.py`、新建 `agent/tests/test_s3_storage_ipc_token.py`、修改既有 `edge/tests/test_storage_service.py`。**批准事实我无法独立核实**（无批准记录文件可查），仅能确认其内容符合批准条件（专用 IPC 令牌、未复用 service token、未改安装器、未引入第三方依赖、拆分而非削弱断言、无测试专用超级角色）。
- **清单不完整**：`edge/app/storage_service.py` 与 `edge/app/storage_client.py` 中还含 B6（`reject_count`、`http_status`）改动，S3.md 的改动表未提及（推测为 W1-E1 的 B6 落地，需协调者确认归属）。
- 未发现删除既有死代码、未改 `edge/tests/conftest.py`。

### 证据等级
- 无凭据 401 / 越权 403 / 合法三角色可用：**隔离集成**（真实 HTTPServer + 真实 SQLite + 真实回环端口）+ **真实入口**（真实 `storage_main.py` 子进程 rc=2；真实 `edge/uploader_main.py` 子进程经认证 200 恢复租约）。
- 角色推断真值表、令牌文件格式/去重、`_is_loopback` 真值：**纯函数 / 单元**。
- 空 host 绑定全网卡：**真实入口**（真实子进程 + 实际监听地址 + 从非回环地址 HTTP 200）。
- 跨账户不可读令牌 / 目标机 ACL / 服务账户拓扑：**未覆盖**。

### 最终裁决
**证实（有条件）**
- 三条验收要求全部成立且由我独立复现：无凭据/坏凭据一律 401 且状态零变化；未确认数据无法被未授权 complete（含三步链）；合法 Collector/Uploader/Agent 在白名单内可用、越权 403；`--host 0.0.0.0` 等非回环值 rc=2 且零写入；无第三方依赖；未复用 Agent service token；无关闭认证的开关。
- **条件（两个必须修的问题）**：
  1. `--host ""` / `DATAWATCH_EDGE_STORAGE_HOST=""` 绕过非回环守卫并真实绑定 `0.0.0.0`（实测可从非回环地址访问）→ 清单「不要将 Storage 开到全部网卡」在该输入下不成立；
  2. `scripts/simulate_edge_collector.py` 因 collector 白名单缺 `outbox.upload_interval` 而 403 崩溃（实测 rc=1）→ 协议 §3 S3「不能让合法链路瘫掉」未完全通过，且修复者「不受影响」的自述被证伪。

### 遗留问题
1. **P1**：修 `_is_loopback`（空串/纯空白必须判为非回环，或直接拒绝空 host 启动），并同步 `edge/tests/test_storage_ipc_auth_s3.py:473` 的断言与 `docs/04-运维部署/README.md:589` 的表述。
2. **P1**：把 `outbox.upload_interval` 加入 collector 白名单，或让 `simulate_edge_collector.py` 不再调用它；二者取其一并回归 `scripts/` 下的第一方调用方（建议对 `scripts/*` 做一次「角色 × 动作」静态核对）。
3. **P2（可观测性）**：`edge/app/storage_client.py:347-356` 把 401/403 与网络不可达都折叠成 `StorageUnavailable("Edge Storage unavailable: ...")`，运维无法区分「凭据错」与「服务挂」；agent 侧同样。
4. **P2（可用性）**：`_is_loopback` 对合法回环写法过度拦截 —— 实测 `127.1`、`localhost.`、`[::1]`、`127.000.000.001`、`127.0.0.0/8` 均判为 False → rc=2（`::ffff:127.0.0.1`、`LOCALHOST`、`127.0.0.1 `（尾空格）、`::1` 判为 True）。
5. **未覆盖**：跨本地账户实测（无第二账户，未创建）、安装器/服务账户下的令牌目录 ACL、`icacls` 失败时的降级路径、令牌轮换、非 Windows 平台。
6. **信任边界披露**：`edge.db` 无产品级 ACL 加固（实测仅继承 ACL，本机含非属主 Modify ACE），任何对其可写的主体可绕过 IPC 直接改 outbox；且令牌文件与库同目录，父目录可写者可删改令牌文件绕过文件级 ACL。目标机上该边界由安装器承担，本轮未授权验证。

---

## 3. 总表与并发说明

### 3.1 总表
| 条目 | 裁决 | 证据等级 | 遗留问题（要点） |
|---|---|---|---|
| **B25** | **证实（有条件）** | 真实入口 + 隔离集成 | 锁/库分指时验收要求第 3 条可被违反（实测复现）；锁文件每失败一次 append 1 字节；目标机未覆盖 |
| **S3** | **证实（有条件）** | 隔离集成 + 真实入口 | `--host ""` 绕过非回环守卫并绑定 0.0.0.0（实测）；`simulate_edge_collector.py` 403 崩溃（实测，证伪其自述）；401/403 折叠为「unavailable」；跨账户 ACL 未覆盖 |

### 3.2 回归与并发说明
- 我两次串行运行 `python -m pytest edge/tests -q`（验收开始、收尾）**均为 84 passed / 0 failed**，无一次出现并发污染失败。
- 收尾检查 `edge/**` 全部 `.py` 的 mtime：代码侧最新为 13:21:19（`edge/app/uploader.py`）、测试侧最新为 13:14:07，均早于我 13:36 之后的实测；6 个实现文件的 sha256 在验收全程未变 → 我的结论对应的是同一个工作区快照。
- 另：验收期间工作区仍有**其它条目 agent 的新增未跟踪文件**（如 `agent/tests/test_b11_residue_regressions.py`、`ingest/tests/test_b7_*.py`）出现，属他人范围，我未触碰。
- 未做 git 写操作；4 条既有 stash 原样；未跟踪文件原样；未创建/删除仓库文件（仅本报告）。
- 我全部反例脚本位于 `%TEMP%\dw_edge_verify\`（`s3_verify.py`、`s3_verify2.py`、`b25_verify.py`、`b25_split.py`、`sim_check.py`、`reinject.py` 及影子树 `shadow\`），如需复核可直接重跑；回注反证完全在影子树内进行，仓库实现文件 sha256 前后一致。
