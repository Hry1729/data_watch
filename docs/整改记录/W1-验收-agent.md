# W1 独立验收记录（agent 组：B15 / B11）

> 验收者：W1 独立验收 agent（agent 组）。**不是** B15/B11 的修复者。
> 依据：`W1-验收协议.md`（§0 硬性规则 / §2 假修复模式 / §3 B15、B11 反证要点 / §4 输出格式）、
> `隔离测试环境.md`（§1 现场栈禁区、§1.1 真实安装禁区、§3.1 测试命令、§3.7 安全基线）、
> 被验收对象自述 `B15.md` / `B11.md`、清单 `Data_Watch_综合审查与Agent整改执行清单_6845e80.md`（B11 第 287-299 行、B15 第 343-355 行）。
> 验收 HEAD：`6845e800990f8352f46655036741bd79ca25eed1`（工作区，未 commit）。
> 验收时间：2026-10-03 之后 / 本机时间约 14:00-15:00（`docker ps` 实测现场栈 `Up 2 hours`）。

## 0. 本次验收的方法与安全约束

**我做了什么（全部为只读 + 仓库外临时脚本）：**

| 项 | 内容 |
|---|---|
| 重跑修复者命令 | 见 B15/B11 各节「我重跑的命令与真实输出」 |
| 自建反证脚本 | 4 个脚本 + 1 个 pytest 插件，全部落在 `%TEMP%\dw_agent_verify\`，**未入库** |
| 修复前行为复现方式 | `git show HEAD:<file>` 把**原始** `agent/app/core/security.py`、`agent/app/routers/admin.py`、`agent/app/services/health_checker.py` 导出到 `%TEMP%`，用 `importlib` 装载为独立模块后调用；**没有改动任何实现文件**（因此无「回注/还原」环节，无需 sha256 回滚校验） |
| 实现文件完整性 | 开工前与收工后 sha256 逐字节一致（见下） |
| 真实安装目录 | **未读、未写、未 stat、未枚举** `C:\ProgramData\DataWatch Edge`；两个自建脚本各带 `sys.addaudithook` 监听 `open/os.replace/os.remove/...`，凡路径含 `programdata` / `datawatch edge` / `agent_service_token` 即记违规，实测 `violations: []` |
| 隔离守卫 | 每个自建脚本在 import `app` 之前把 `DATAWATCH_DATA_ROOT` 指向 `%TEMP%`，并把 `config.CONFIG_PATH` 钉到 `%TEMP%` 下的临时文件；所有接收端为 `bind(("127.0.0.1", 0))` 临时端口，凭据一律 `TESTONLY_*` 哨兵 |
| 现场栈 | 只读 `docker ps` + `redis-cli info keyspace`；**未连接**任何 `data-watch-*` 业务写操作；未启动任何新服务 |
| git | 未做任何 git 写操作；收工 `git status --porcelain` = 43 项、`git stash list` = 4 条（与开工一致） |

**实现文件 sha256（开工 = 收工，未被我改动）：**

```
392E80286DAF29281057C1A3123BE1A5B988B7ADC44BF6847287ADD0451E6A42  agent/app/core/config.py
3978584CDA7EF3BE07542AD43B3B29DF566AB1A2773C1170761649F77AA5CA51  agent/app/core/security.py
D867428A88AB99337E36A8D7EF5FEB9ADE04DDBF4F346B28A90E96F80C02BF96  agent/app/routers/admin.py
85CCCD07726E8D4F132181233BB98DD970B9EA5536D6100F7EF85970E8197392  agent/app/services/health_checker.py
53C07A5C6CCB1CDAFD9DF87A1331C612A266A2828650B66FED0EB0C36AC9177B  agent/config.toml.example
FEA0AEB5FA0E26C7BB5E0AF5A757BB844ADC3C81E9ABEE8D5D58838E47251DDD  agent/tests/test_b15_jwt_secret_enforcement.py
ADB78B8D2CF7E5966C49A2C335BDD6FD1A7D3BBE8F5FE6EDF1657D6CF9DFD268  agent/tests/test_b11_health_target_credentials.py
```

**现场栈安全基线复核（`隔离测试环境.md` §3.7）：**

```
data-watch-frontend | Up 2 hours        data-watch-backend  | Up 2 hours
data-watch-redis    | Up 2 hours        data-watch-mysql    | Up 2 hours
data-watch-tdengine | Up 2 hours
# Keyspace
db0:keys=10,expires=0,avg_ttl=0,subexpiry=0        (基线 10)
config get databases -> 16                          (基线 16)
```

`Up` 时长单调增长（开工基线 17 分钟 → 2 小时），未重启；键数/库数与基线一致 → **无越界写入**。

---

### B15 验收结论

- **验收者 / 依据的修复报告**：W1 独立验收 agent（agent 组） / `docs\整改记录\B15.md`（W1-A1）、清单第 343-355 行。

- **我重跑的命令与真实输出（关键片段）**：

```powershell
# 1) 修复者声称的回归命令（仓库根）
$env:PYTHONPATH='D:\python\device_alarm\data_watch\agent\.venv-pack\Lib\site-packages'
python -m pytest agent/tests/test_b15_jwt_secret_enforcement.py -q -p no:cacheprovider
#   实测 => 28 passed in 0.96s          （修复者报告：28 passed in 0.33s）

python -m pytest agent/tests -q -p no:cacheprovider
#   实测 => 305 passed, 1 skipped, 1 warning in 52.35s
#          （修复者报告：305 passed, 1 skipped, 1 warning in 54.79s）

python -m pytest agent/tests --collect-only -q -p no:cacheprovider
#   实测 => 306 tests collected in 0.43s      （= 305 passed + 1 skipped）
python -m pytest agent/tests/test_s3_storage_ipc_token.py --collect-only -q
#   实测 => 4 tests collected                 （证明 +4 来自 W1-S3，不属 B15/B11）

# 2) 我自建的反证探针（%TEMP%，54 项断言）
python %TEMP%\dw_agent_verify\b15_probe.py
#   实测 => B15 probe: 54 checks, 0 failed / VERDICT: ALL-PASS

# 3) 参考：修复者「48 用例审计钩子」自证的独立复跑（用我自己的钩子）
python %TEMP%\dw_agent_verify\run_fixer_tests_audited.py
#   实测 => 48 passed in 4.17s
#          pytest exit code: 0
#          violations: []
#          VERDICT: NO-ACCESS-TO-REAL-INSTALL
```

- **与修复者报告的差异（逐字核对结果）**：

| 修复者声明 | 我的核对结果 |
|---|---|
| 修复后 `agent/tests` = `301 passed, 1 skipped`；最终共享工作区 `305 passed, 1 skipped` | ✅ 数值一致（时间不同：我 52.35s，含并发 agent 的 4 个 S3 用例）。`253 + 28 + 20(B11) + 4(S3) = 305` 逐项对齐 |
| `test_b15_...py` 修复后 28 passed | ✅ 逐字一致 |
| 修复前 `17 failed, 4 passed` | ⚠️ **未复跑**（当前工作区已是修复态，复跑需回退实现文件；按纪律不做）。取而代之：我直接用 `git show HEAD:` 导出**原始** `security.py` 复现了缺陷本体（见反例 2/3），证据强于其修复前的用例计数 |
| 「既有配置走 `_ensure_jwt_secret_unlocked`」 | ✅ 代码与实测一致；**已存在**配置文件（非缺失场景）确实走校验 |
| 「32 字符下限只在配置加载处强制，加密边界只拦空值」 | ✅ 属实，且**我独立复现了其成因**：把下限搬到加密边界后 `test_admin_auth.py` 恰好 **3 failed, 7 passed**，失败项就是其点名的 3 个（见反例 5） |
| 「无新增 skip/xfail；未删除/放宽既有断言」 | ✅ `agent/tests` 全目录只有 1 处 `pytest.skip`，位于**未被修改**的 `test_driver_install.py:338`（既有）；`git status` 中没有任何既有测试文件被修改 |

- **我构造的反例与结果（7 组，全部实跑）**：

1. **既有配置矩阵（bootstrap_done=true）逐项拒绝、且文件零改写**：`""`、`"   "`、键缺失、数字 `12345`、浮点 `1.5`、数组 `["a"*64]`、布尔 `true`、`"test-secret"`(11)、`"0"*31`、32 个空格 —— 10/10 抛 `ValueError`，且 10/10 磁盘字节（按 `read_bytes()` 比对，规避 CRLF 陷阱）与拒绝前完全一致 → **不带静默轮换、不顺手改写**。
2. **空密钥伪造 admin token（改前/改后对照，真实 `deps.get_current_admin`）**：
   - 改前（`security_before.verify_token`）→ `verify_token(forged, "")` **返回 `attacker`**；把它接到真实 `deps.get_current_admin` 上 → **返回 `attacker`**（缺陷复现，与修复者自述一致）。
   - 改后 → `verify_token` 抛 `JWTError`；`get_current_admin` → **401**；`get_current_user` → **401**。
3. **WebSocket 路径（真实 `viewer.tail_logs`，Fake WS + 哨兵 log_manager）**：改后 → `close(4001)` 且 `log_manager.subscribe` **从未被调用**；把 `viewer.verify_token` 换回改前实现 → `subscribe("collector")` **被调用**（证明该通路改前确实直达 `log_manager`）。
4. **损坏 / 半结构配置**：损坏 TOML → `TomlDecodeError` 且原文件字节不变；`[agent]` 段缺失 → `ValueError`；`agent = "not-a-dict"`（标量段）→ `ValueError`。
5. **边界反证（32 下限的位置）**：`validate_jwt_secret` 用 `strip()` 计长但返回原值；31 字符在配置加载处被拒，32 字符被接受。**关键反证**：用 `-p b15strict` 插件把「32 下限」搬到加密边界（不改任何文件）后跑既有文件 →
   `3 failed, 7 passed`，失败项 = `test_admin_dependency_requires_admin_role`、`test_agent_token_has_fixed_issuer_and_audience`、`test_driver_inventory_allows_viewer_but_mutation_requires_admin` —— **与修复者披露的 3 个用例完全一致**。
6. **首装崩溃注入（修复者未做）**：既有弱密钥 + `bootstrap_done=false`，在 `os.replace` 前注入 `OSError` → 原 `config.toml` **字节不变**（`.config.toml.tmp` 残留，符合原子替换语义）；再次加载仍能生成 64 hex 并落盘稳定。
7. **`bootstrap_done` 键缺失的新发现（修复者未提）**：既有配置 `jwt_secret="changeme"` 且 **无** `bootstrap_done` 键 → 走「首装」分支，**静默（有 WARNING 日志）生成并落盘新密钥**，`len=64`、二次加载稳定。判为**不构成会话静默失效**：`auth.py:35-36` 在该状态下 `login` 直接 503，不可能存在合法会话（代码级确认）。

- **反向检查（是否矫枉过正 / 是否破坏合法链路）**：

| 反向项 | 结果 |
|---|---|
| 合法存量配置（64 hex / 恰好 32 / 带前后空格的 32） | ✅ 加载返回**原值**，文件**字节不变**，两次加载同一值 |
| 仓库内真实 dev 配置 `agent/config.toml` | ✅ 正常加载（`secret_len=64`、`bootstrap_done=True`，未打印密钥），`load_config` 后文件 sha256 **不变** |
| 无配置（缺失） | ✅ 生成 64 hex、落盘、无残留 tmp、重启不变 |
| 首装（`bootstrap_done=false`）弱密钥 | ✅ 原子补写 + `WARNING` 明确提示「旧令牌全部失效」，二次加载稳定 |
| 静默频繁轮换 | ❌ 不存在：合格密钥永不改写；不合格 + 已 bootstrap → 拒绝启动且文件不动（连跑 3 次均报错、字节一致） |

- **假修复模式排查（协议 §2 清单逐项）**：
  - `try/except` 吞异常：B15 的 `except ValueError: pass` 属**控制流**（随后要么 `raise`，要么生成强密钥），任何返回路径都不会放出弱密钥 → 非吞异常。实现 diff 中未新增其他 `except`。
  - 放宽断言 / 注释断言 / 删测试：`git diff` 中既有测试文件**零改动**，无删除、无 `>=` 替换、无 tolerance 放宽。
  - 新增 `skip`/`xfail`/`skipif`：`agent/tests` 全目录仅 1 处既有 skip（未修改文件）；两个新文件内 0 处。
  - 只改 mock/fake 层：B15 用例经真实 `load_config` / `get_current_admin` / `get_current_user` / `viewer.tail_logs`；`monkeypatch` 只用于替换**密钥来源**与 tmp 路径，未替换被验逻辑。
  - 改测试迁就代码：既有测试未被改动；**但**「把 32 下限从加密边界移到配置边界」这一设计选择，客观上是为不修改越权范围外的 `test_admin_auth.py` 而让策略就位。修复者已主动披露，我复现了其必要性（反例 5）→ 见「遗留问题 1」的独立判断。
  - 用静默轮换代替拒绝空密钥：**未发生**（反例 1/4/6/7）。
  - 关闭鉴权/整体跳过校验：未发生，`deps` 保持 401（反例 2）。
  - B7/B41 专属模式：不适用。

- **回归数字对照**：

| 模块 | 基线（W0-A，协议 §1） | 我实测 | 判定 |
|---|---|---|---|
| agent（全量） | 253 passed, 1 skipped | **305 passed, 1 skipped, 1 warning** | ✅ 只增不减；+52 = B15 28 + B11 20 + W1-S3 4 |
| agent（B15 文件） | —（新增） | 28 passed | ✅ 与报告一致 |

  无失败、无新增跳过；`collect-only` 306 = 305 + 1 skip 自洽。

- **越界检查（`git diff --stat` / 工作区）**：B15 只碰 `agent/app/core/config.py`、`agent/app/core/security.py`、`agent/config.toml.example`，外加**新增** `agent/tests/test_b15_jwt_secret_enforcement.py`。逐行读过 diff：只有新增函数/常量/注释与 2 处入口调用，**无顺手重构、无删除既有死代码、无无关格式化**。工作区其余改动（`edge/*`、`backend/*`、`cli/*`、`ingest/*`、`docs/*`、`agent/app/services/edge_storage_client.py`）分属其他条目/其他 agent，与 B15 无交集。

- **证据等级**：**单元 + 真实入口（函数级：`load_config` / `get_current_admin` / `get_current_user` / `viewer.tail_logs`）**；含独立安全审计钩子证明（`violations: []`）。**无隔离集成、无目标机验收**（B15 无外部服务；frozen/target 机未授权）。

- **最终裁决：证实**（附 1 项策略边界判定 + 3 项残留，均不推翻验收要求；详见「遗留问题」1-3）。

---

### B11 验收结论

- **验收者 / 依据的修复报告**：W1 独立验收 agent（agent 组） / `docs\整改记录\B11.md`（W1-A1）、清单第 287-299 行。

- **我重跑的命令与真实输出（关键片段）**：

```powershell
$env:PYTHONPATH='D:\python\device_alarm\data_watch\agent\.venv-pack\Lib\site-packages'
python -m pytest agent/tests/test_b11_health_target_credentials.py -q -p no:cacheprovider
#   实测 => 20 passed in 4.00s          （修复者报告：20 passed in 4.13s）
python -m pytest agent/tests -q -p no:cacheprovider
#   实测 => 305 passed, 1 skipped, 1 warning in 52.35s

# 我自建的隔离集成反证探针（本机回环临时端口接收端 + TESTONLY_* 哨兵 + 真实 DPAPI 密文）
python %TEMP%\dw_agent_verify\b11_probe.py
#   实测 => B11 probe: 53 checks, 0 failed / VERDICT: ALL-PASS
#   其中改前复现段（原始 admin.py + 原始 health_checker.py，来自 git show HEAD）：
#     [PASS] B11-pre P1 changed host/port: new target RECEIVES stored password :: 300 bytes, status=None
#     [PASS] B11-pre P2 301/302/303: credential forwarded to another origin (sink_reqs=1, leaked=1)
#     [PASS] B11-pre P2 307/308: NOT forwarded (urllib 对 POST 的 307/308 直接抛 HTTPError)
#     [PASS] B11-pre P3 host-smuggled port: request leaks (success=True, reqs=1)
python %TEMP%\dw_agent_verify\extra_probe.py      # 4 checks, 0 failed
python %TEMP%\dw_agent_verify\f2c_proxy.py        # 代理场景（见残留 R3）
```

- **与修复者报告的差异（逐字核对结果）**：

| 修复者声明 | 我的核对结果 |
|---|---|
| 改前「新目标收到 **300 字节**，内含 `HELLO 3` + `AUTH default TESTONLY_stored_pw_sentinel`」 | ✅ **逐字命中**：我用原始 `admin.py`+`health_checker.py` 实测同样 **300 字节**，字节内容含 `AUTH`+哨兵 |
| 改前「TDengine 302 后另一 origin 收到 `Basic(root:...)`」 | ✅ 复现（301/302/303 均转发）。**我补充**：307/308 在 urllib 下对 POST 直接 `HTTPError`，**改前也不泄漏** —— 结论不变（已加固为 5 种状态码全部不跟随） |
| 改后 host 变体 7 种全部 400 | ✅ 复现；我另加 11 种（`0x7f000001`、`127.1`、`localhost.`、尾空格/Tab/换行/NUL、`127.0.0.1#`、`127.0.0.1/path`、`176.0.0.1`）→ 全部 400 且存储接收端 0 字节 |
| 「改前不存在任何拒绝分支」 | ✅ 复现（改前 `status=None` 且凭据外发） |
| 「`_target_address`：host 只允许主机名/IPv4 字面量/方括号 IPv6 字面量」 | ⚠️ **不完全准确**：`_HOST_PATTERN = r"^[A-Za-z0-9._-]+$"` 用 `$`，因此 **尾随换行的 host（`"127.0.0.1\n"`）被接受**（我的 12 项「非法地址」首轮 11 项被拒、1 项通过）。影响有界：端口单独校验、urllib 对该 host 抛错 → `success=False`、接收端 0 请求；健康检查端点的精确字符串比较会先判为「目标变更」→ 400。属**声明与实现的偏差**，非凭据泄漏 |
| 「无新增 skip/xfail；未删除/放宽既有断言」 | ✅ 同 B15 |
| 「未覆盖：TDengine 明文 Basic+http、DNS 重绑定、代理环境变量」 | ✅ 三项均确认未修；其中**代理项我补测并复现了实际暴露**（残留 R3） |

- **我构造的反例与结果**：

**改前（原始文件复现缺陷本体，证明「修复前」不是自述）**
1. 只改 host/port：存储接收端 A 收到哨兵、**新接收端 B 收到 300 字节且含 `TESTONLY_stored_pw_sentinel`**、`status=None`（无拒绝）。
2. 302 跨 origin：origin 先收到带凭据的 POST，**sink 收到 `Basic root:TESTONLY_stored_pw_sentinel`**（301/303 同样；307/308 不跟随）。
3. `host = "127.0.0.1:<sink_port>#"`：`check_tdengine` 返回 `success=True` 且 sink 实收请求（凭据发往夹带端口）。

**改后（隔离接收端 + 哨兵）**
4. host+port 变更 → **400**，新接收端 0 字节、存储接收端 0 字节；**只改 host（不提交 port）** → 400，存储接收端 0 字节；`port`/`user`/`database` 任一被改 → 400。
5. 18 种 host 变体（含 `LOCALHOST`、`127.0.0.1.`、`2130706433`、`0177.0.0.1`、`0x7f000001`、`127.1`、`localhost.`、`[::1]`、`::1`、`127.0.0.1:1`、`127.0.0.1:1@evil`、`127.0.0.1#`、`127.0.0.1/path`、尾随空格/Tab/换行/NUL、`176.0.0.1`）→ 全部 400 + 0 字节。
6. **显式 `password=None`**（想绕过「未显式提供口令」判断）→ 仍 **400**；显式空串 `password=""` + 改目标 → 放行且**不发送存储凭据**。
7. 重定向 301/302/303/307/308 → sink 无 `Authorization`、**无任何请求**，且断言 origin **确实**带了凭据（防空转），`success=False`。
8. 地址/端口：11 组非法输入全部拒绝；`host-smuggled port` → `success=False`、0 请求（未发起连接）。

- **反向检查（是否矫枉过正 / 是否破坏合法链路）**：

| 反向项 | 结果 |
|---|---|
| 合法已配置目标一键检查（未改任何字段） | ✅ TDengine `success=True` 且 origin 收到存储凭据；**Redis** 未改目标 `success=True` 且接收端见到哨兵 |
| 显式临时凭据 + 新目标 | ✅ 可用；新目标收到 `TEMP`，存储口令 0 次出现、旧目标 0 请求 |
| 同一目标 + 显式临时口令 | ✅ 用临时口令，存储口令不外发（我补的反例 4/extra-E2） |
| 无存储密文（`password_enc=""`）+ 新目标 | ✅ **不被拦**（未退化成「一律需人工授权」） |
| 共享调用点 `viewer_health`（未改动，共用 checker） | ✅ 未改目标的下 Redis `connected`（我补的 extra-E3/ Q14） |
| `bootstrap_test_connection`（显式凭据路径） | ✅ `success=True`，未因地址加固而失效 |
| 存储 host 为尾点变体 + 显式凭据 | ✅ 放行，存储密文不外发 |

- **假修复模式排查（协议 §2 清单逐项）**：
  - `try/except` 吞异常：实现 diff **未新增任何 try/except**；`check_redis/mysql/tdengine` 的 `except Exception` 是改动前既有的，且校验发生在建连**之前**（反例 8 证明非法地址根本不发起连接）。
  - 放宽断言/删断言/注释测试：既有测试零改动；新增测试只加严（如断言 sink 的 `requests == []`，并额外断言初始请求**确实**带凭据以防「空转通过」）。
  - 新增 `skip`/`xfail`：0 处。
  - 只改 mock/fake：接收端是**真 socket / 真 http.server**，密文是**真 DPAPI**，入口是**真路由函数** `admin.health_check`。
  - 改测试迁就代码：无。
  - 关闭鉴权：`admin.py:365` 仍 `Depends(get_current_admin)`；未放宽为 viewer/service。
  - 用静默轮换代替拒绝：不适用（B15 条）。

- **回归数字对照**：同 B15 —— agent `253 → 305 passed, 1 skipped`（+52 = 28 + 20 + 4），失败 0，无新增跳过。

- **越界检查（`git diff --stat` / 工作区）**：B11 只碰 `agent/app/routers/admin.py`（+38/-?，实际仅 `health_check` 内 22 行新增 + 常量）与 `agent/app/services/health_checker.py`（+48），外加**新增** `agent/tests/test_b11_health_target_credentials.py`。逐行读过 diff：无重构、无删除既有死代码、未新增依赖（`re`/`urllib.error` 均为标准库）。

- **证据等级**：**隔离集成**（真实路由函数入口 + 本机回环真 socket 接收端 + `TESTONLY_*` 哨兵 + 真实 DPAPI 密文 + 原始 HEAD 文件复现改前行为）；地址校验纯函数部分为**纯函数/单元**级。**无目标机验收**，未启动任何真实数据库。

- **最终裁决：证实**（附 3 项残留/偏差：R1 明文 `password` 配置形态、R2 `$` 尾部换行、R3 `HTTP_PROXY`；均不推翻清单 299 行的验收要求）。

---

## 总表

| 条目 | 裁决 | 证据等级 | 遗留问题 |
|---|---|---|---|
| **B15** | **证实** | 单元 + 真实入口（函数级） | ①32 下限仅在配置边界（独立判断：**可接受**，当前不可达）；②`bootstrap_done` 键缺失时对弱密钥静默轮换（有 WARNING，登录本为 503 → 不构成会话失效）；③跨进程并发首装、frozen/目标机、真实 uvicorn WS/HTTP 未验 |
| **B11** | **证实** | 隔离集成（+ 纯函数） | ①存储段为**明文 `password`**（非产品写入形态）时绑定检查失效，凭据跟随新目标（已复现）；②`_HOST_PATTERN` 用 `$`，尾随换行 host 被 `_target_address` 接受（声明与实现偏差，无越界影响）；③`HTTP_PROXY` 在进程启动前设置时，TDengine 存储口令被送往代理（已复现）；④TDengine 明文 Basic+http、DNS 重绑定保持未修（架构决策） |

## 遗留问题（按优先级）

1. **B15「32 字符边界」独立判断 —— 结论：可接受，不构成残留缺口（当前不可达），但属应记账的纵深防御缺口。**
   - **可达性核查（已做）**：`create_access_token`/`verify_token` 的全部 4 个调用点（`deps.get_current_user:26`、`deps.get_current_admin:45-49`、`auth.login:40-45`、`viewer.tail_logs:160`）**都**从 `load_config()` 取密钥；`grep` 全 agent 代码，除 `config.py` 外**没有任何**地方读写 `jwt_secret`，也没有第二个 `toml.load(CONFIG_PATH)`；`bootstrap_config`/`update_config`/`mutate_config`（均先 `load_config` 校验）都不写 `jwt_secret`。因此「绕过 `load_config` 直接调 create/verify」在当前代码里**没有真实入口**，11 字符密钥无法通过任何现有路径进入签发/校验。
   - **为什么仍要记账**：加密层的契约是「非空」，比配置层策略弱一档。任何人日后新增一个密钥来源（环境变量、安装器写入、第二个配置文件）就会重新打开弱密钥口子，而测试不会告警。建议下一批次二选一：(a) 把 `_require_jwt_secret` 的 `min_length` 提到 32；(b) 更省事：把 `agent/tests/test_admin_auth.py` 的 `"test-secret"` 哨兵改为 ≥32 字符（它只是夹具值，不是真实密钥），然后 (a) 自然成立。**我不认为这足以把 B15 判为未修复或推翻**：清单 355 行的验收要求（空/弱示例值不得启动、拒绝空密钥伪造、合法存量稳定、首装原子生成）逐项实测通过，而 `load_config` 是唯一真实密钥入口。
2. **B11-R1（残留，已复现）**：若 `config.toml` 的 db 段用**明文 `password`** 而非 `password_enc`（`_decrypt_password` 明确支持该键），绑定检查 `if target_cfg.get("password_enc")` 不成立 → 改 host 既不 400 也不记日志，明文口令被发往新目标（实测 `sinkB_reqs=1, leaked=True`）。**可达性**：grep 全仓库，产品代码不会把明文 `password` 写进 config.toml（`_apply_db_section` 加密并 `pop("password")`；`apply_db_patch`/`_default_config` 只写 `password_enc`）→ 需人工编辑配置才可达，故不推翻验收。**建议**：把条件改为「存在任何存储凭据（`password_enc` 或 `password`）即拒绝」。
3. **B11-R2（偏差，已复现）**：`_HOST_PATTERN = r"^[A-Za-z0-9._-]+$"` 的 `$` 允许尾随 `\n`。建议改 `\Z` 或 `fullmatch`。影响有界（端口独立校验；urllib 对该 host 报错、接收端 0 请求；API 层精确比较已先拦），故非泄漏，但与其自述不符，应记账。
4. **B11-R3（残留，新发现并复现）**：`_TDENGINE_OPENER` 在 import 时构建，urllib 的 `ProxyHandler` 在构建时快照 `getproxies()`。若进程启动前已设 `HTTP_PROXY`，TDengine 健康检查（含**未变更**的存储目标）会把请求发给代理，代理收到 `Basic root:TESTONLY_stored_pw_sentinel`，直连目标 0 请求（实测：`proxy_requests=1, direct_requests=0`）。修复者在 §10.6 已披露「未测」，我补测后**确认存在暴露**。**建议**：opener 显式 `ProxyHandler({})`（不读环境变量代理）。此项不推翻「凭据不得跟着新目标走」的验收要求（代理不是请求体可控的新目标），但属同一凭据面的真实缺口。
5. **双方均未覆盖（如实登记）**：目标机/frozen 形态验收；真实 uvicorn 下的 HTTP Bearer 与 WebSocket 合法正例；B15 跨进程（双进程）并发首装（仅进程内 `RLock` 已验）；B11 真实 MySQL/Redis 的完整成功语义（仅字节级与握手级断言）、IPv6 真实连接、DNS 重绑定与 IP pinning；TDengine 明文 `http://` + Basic 未改（协议/架构决策，需另立条目）。
6. **安全**：本次验收全程未触碰 `C:\ProgramData\DataWatch Edge`（审计钩子 `violations: []`），未连接现场栈，未做 git 写操作，4 条 stash 与未跟踪文件原样保留，实现文件 sha256 与开工一致。
