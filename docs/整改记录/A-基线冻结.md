# 批次 A 验收版本冻结记录

> 目的：批次 A 的修复**尚未提交**，因此 `HEAD = 6845e80` **不能**作为验收版本标识 ——
> 它标识的是"未改动的上游"，而我们的成果全在工作区。本文件用一个**内容哈希清单**来冻结真正的验收版本。

## 1. 冻结对象与时刻

- 冻结时刻：脚本执行时确定（见文末"生成方式"）。
- 上游 HEAD：`6845e800990f8352f46655036741bd79ca25eed1`（**未被改动、未新增 commit**）。
- 暂存区：0 项。`origin/main...HEAD` = `0 0`（未推送）。
- 既有 stash：**4 条，名称与内容原样保留**。
- 修改的已跟踪文件：**30 个**；新增未跟踪文件：见 §3。
- 完整 diff：`docs/整改记录/A-完整diff.patch`（仅含**已跟踪文件的改动**；新增文件见 §3 清单）。

## 2. 已跟踪文件改动清单（SHA256 全量）

| 文件 | 状态 | SHA256 |
|---|---|---|
| `agent/app/core/config.py` | 已跟踪-修改 | `392E80286DAF29281057C1A3123BE1A5B988B7ADC44BF6847287ADD0451E6A42` |
| `agent/app/core/security.py` | 已跟踪-修改 | `3978584CDA7EF3BE07542AD43B3B29DF566AB1A2773C1170761649F77AA5CA51` |
| `agent/app/routers/admin.py` | 已跟踪-修改 | `A81F9AF1FF38ED0EF145C55EE3ED95A02849790DAB0839ED8AA8C368A5618119` |
| `agent/app/services/edge_storage_client.py` | 已跟踪-修改 | `293B8724D27F6ECFCDBE94EB0D478D468A7EB731201975ED00F8E93B82FC130D` |
| `agent/app/services/health_checker.py` | 已跟踪-修改 | `0A37BB280C7F9BFE5B72229BCF7BE93F74CF39C2A1FCA7A5B7250AC4027D979B` |
| `agent/config.toml.example` | 已跟踪-修改 | `53C07A5C6CCB1CDAFD9DF87A1331C612A266A2828650B66FED0EB0C36AC9177B` |
| `backend/app/api/routes/catalog.py` | 已跟踪-修改 | `8E1CED72C41527C75E1F684683039CAA5190E47192D81906247997725DF4AF94` |
| `cli/update_manager.py` | 已跟踪-修改 | `4582FEBFD2032783B32062D4118C1D255D3597336FACB518EDB94AC5DCB46433` |
| `docs/01-总体设计/分布式部署架构方案.md` | 已跟踪-修改 | `AC854DEEE9479E26A81E024F95309E921EC91630043C47F592C1C0D554BD811A` |
| `docs/01-总体设计/架构决策记录.md` | 已跟踪-修改 | `EE0591AFA4782776815D0ED7F0F082D488DC9875D4454E1FF67BC65984CFA698` |
| `docs/02-模块设计/cli/总体设计与架构.md` | 已跟踪-修改 | `F170D7BCCE04846B45428467A26DA5524B6E6E2CA56F612ED021DDB2C436754D` |
| `docs/02-模块设计/edge/边缘运行时实现说明.md` | 已跟踪-修改 | `F91B3469A2A51817F5E57CFDE17DDA713362C2DD4F3FE7CD439C3D675F25CD67` |
| `docs/03-数据与契约/DW-01-确认语义与持久化边界.md` | 已跟踪-修改 | `1F6BE3F9907AC4631EBCD7010DF3EFC9474D45CE39AE3C9F9861FE53E8E4CCF4` |
| `docs/04-运维部署/CLI安装与更新.md` | 已跟踪-修改 | `0B0F3FD0AABA6EDDEDD25A5DFAAEDD761718F5AA061869907ABED90B16432A2E` |
| `docs/04-运维部署/README.md` | 已跟踪-修改 | `5CEC307B22130B4F084127AA2C2396AFEECFAE64C74B539104CD54DB6C79C7B8` |
| `docs/04-运维部署/三角色安装包验收清单.md` | 已跟踪-修改 | `C42B1678AA9DD345C968326288C620E09F3B088AB54F75DA56F967CE3160D053` |
| `docs/04-运维部署/问题排查.md` | 已跟踪-修改 | `8D3E1D0461C0574DEE36AF902CAF663A86C6F8562CF5B0AB5CD7CA03F1F87FFC` |
| `docs/06-验证记录/2026-09/reliability-review-delivery.md` | 已跟踪-修改 | `92CED991E7C6FC21F4846A22BF42845518C424D7FF0B73FFC2CFAB89DEB9E2FA` |
| `docs/joint-ladder-20260927.md` | 已跟踪-修改 | `06133C6F5191B07B08DC5AB9E783C95A2E6BACDE47E1908D8C4966AC525B5678` |
| `docs/joint-measurement-20260927.md` | 已跟踪-修改 | `EC9F589E3F3C8A519A8D89C3D0ED38AD73DC870A28F37A793E0FDE75F95D3CED` |
| `edge/app/outbox.py` | 已跟踪-修改 | `28173AD0B6F0A1501DD012F067C9BD33CF9CC4B353B35A53294151A797F1599F` |
| `edge/app/storage_client.py` | 已跟踪-修改 | `631AF9D8C028E640CF61A8089161E3754222D39AA53F4BD1BD1EE141A204DB65` |
| `edge/app/storage_service.py` | 已跟踪-修改 | `F05D9E4CDDE33DF12C27A50185F850D870DA621D28374D54C4B766A002C048BB` |
| `edge/app/uploader.py` | 已跟踪-修改 | `2E8F57BC021DC002A28FEED9F1A138155AB5B9154A1D8EE50EC65EF2C1B125FD` |
| `edge/storage_main.py` | 已跟踪-修改 | `7DF89CDB4E4B8D53F3C4897E02F0703678927C9914EC09768C2C89858122E4DC` |
| `edge/tests/test_storage_service.py` | 已跟踪-修改 | `8C33B5963C78CC3434A632141644E03F1A1383E168B7C630577F87E40E69084F` |
| `edge/tests/test_uploader_main.py` | 已跟踪-修改 | `EAFD245923CAD73832C2574110E7DF405CF0B11463C62500C2A2830D8610D93E` |
| `edge/uploader_main.py` | 已跟踪-修改 | `48DD1F600C51B4B7AEECE8B3A07870D067595CB89CFFC4FE8AE89B2C5BB6E270` |
| `ingest/app/repository.py` | 已跟踪-修改 | `A46B02D950B55D2B7AE81A020A9B392903A705039B0C50C2C874711F22802EEE` |
| `scripts/simulate_edge_collector.py` | 已跟踪-修改 | `927EA9D462EA50FFE3DE6E313D6AC47E87ABB0948CDBA3A90B9ABCEF9798D5F3` |

## 3. 新增未跟踪文件清单（SHA256 全量）

| 文件 | 类型 | SHA256 |
|---|---|---|
| `agent/tests/test_b11_health_target_credentials.py` | 新增回归测试 | `ADB78B8D2CF7E5966C49A2C335BDD6FD1A7D3BBE8F5FE6EDF1657D6CF9DFD268` |
| `agent/tests/test_b11_residue_regressions.py` | 新增回归测试 | `F25D998C9FBFB0CDD715E490910D07FCF0FF387C1AE059F0E3A7274BEB276629` |
| `agent/tests/test_b15_jwt_secret_enforcement.py` | 新增回归测试 | `FEA0AEB5FA0E26C7BB5E0AF5A757BB844ADC3C81E9ABEE8D5D58838E47251DDD` |
| `agent/tests/test_s3_storage_ipc_token.py` | 新增回归测试 | `276E94108432632FD708CAD2989A1881CB96800CD11EDBE99700D1BCA4CCE5D5` |
| `backend/app/tests/test_b7_cache_invalidation.py` | 新增回归测试 | `4F3419EE07C0E487A86EBA49C8EA3DA25E71E246A5D630F4F65FB59ABBEF4134` |
| `cli/tests/test_update_swap_b41.py` | 新增回归测试 | `107E851E23FF59481438D29724FE00DDFD01CBE462FA4F0587E39F0407B22C9F` |
| `docs/Data_Watch_综合审查与Agent整改执行清单_6845e80.md` | 其他 | `5CC657D80C62074F462569D89CF84666C8D0287EB2383A6C377AF4113BFADA6F` |
| `docs/整改记录/A-完整diff.patch` | 台账/验收记录 | `5DE1154F9D8D135B88C7C66EA1CF047F498B56E979E362A16225CA84EE2A0985` |
| `docs/整改记录/B11.md` | 台账/验收记录 | `4E04CECF2ECB168A2C5400823DBA3D432ADA935C4B5055CEAD164D54A50CF6A7` |
| `docs/整改记录/B15.md` | 台账/验收记录 | `D932341D2A62A5EB56E3D37D16280E3C0DA1EA038F6CA66BD5F4380E31442280` |
| `docs/整改记录/B25.md` | 台账/验收记录 | `C1FE2DCA3A173EA4D4E7BC7CEDB39CDFA0140C7BB69DD5048FB9132E4811BDE4` |
| `docs/整改记录/B41.md` | 台账/验收记录 | `407790324F2C2BDED36F1E8B1AD21D75527895A2E7F024FA009AFDE811918812` |
| `docs/整改记录/B5.md` | 台账/验收记录 | `243B15BACFD12961D0D22B6E58B934DD190909CFED25D016F7AE7914E46369FA` |
| `docs/整改记录/B6.md` | 台账/验收记录 | `868964A2C6993F455B7899A8C8A9C99C174FAD44B53E3F610D02CB24F7C688C3` |
| `docs/整改记录/B7.md` | 台账/验收记录 | `11F812D6FD19615817E4113D4FF818A27B864508F5A7A6AECD1103D77DF943B7` |
| `docs/整改记录/D39.md` | 台账/验收记录 | `97C41EDC6DBB1C7A0D2E1FA29B7430E48AA3F990C9D23866A720F41317E9FBFF` |
| `docs/整改记录/README.md` | 台账/验收记录 | `3B9553779CAC441EBCC93E2323FDA6744EA5047113E7E6180D73FCE59FDFE9F1` |
| `docs/整改记录/S3.md` | 台账/验收记录 | `E7080F6E02B4776529259CEA298AA98CA9F3DF2412823AE2B5E710EDC5B04CFB` |
| `docs/整改记录/W0-A-基线.md` | 台账/验收记录 | `094750F8338B4DB6D0842E31A5C603652994BF93A015FC44A80AA432CC5276C1` |
| `docs/整改记录/W0-B-P0任务卡.md` | 台账/验收记录 | `968234E0C9E5CD82522750DF9DD5DF6F6FFD4CB7000B2E7523B4AA837F2FA189` |
| `docs/整改记录/W1-验收-B41-D39.md` | 台账/验收记录 | `73A3D7519889A6CDD23240C3E5ADC69AA9CC1757FFA5233FB820D57AFAAEE09A` |
| `docs/整改记录/W1-验收-B7.md` | 台账/验收记录 | `537E3A6131C55769D104CF78F5EBB0A5642DA2DDCBAC92277F1426FF04FE8F47` |
| `docs/整改记录/W1-验收-S3-ABC.md` | 台账/验收记录 | `CA585845BAE615A1A68E41E05F4399ACCBF6C3E48F0E85C25E94FF0B4AE39FB2` |
| `docs/整改记录/W1-验收-agent.md` | 台账/验收记录 | `3D9297CDAE308C25C7B9360659B349527361DF566EF939FD347A7BF95DDD7512` |
| `docs/整改记录/W1-验收-edge.md` | 台账/验收记录 | `A2C6D7441FBBAC502E1422B6E4219BA80A0CDC6E42E5772345C6C3846BB8C9CD` |
| `docs/整改记录/W1-验收协议.md` | 台账/验收记录 | `041A2B791895D5EDAE99B8333A3DA7478FFD9BBC6F56F635112AFEC531A0DAC5` |
| `docs/整改记录/隔离测试环境.md` | 台账/验收记录 | `5F1A4210A5AF033DBD34A257F2C7660B37FCB2C9A42D94FAA6F32F8223D4DD6F` |
| `edge/tests/test_outbox_b25_lock_order.py` | 新增回归测试 | `6A1C079A8819DCFFFCEBC8A2D19D62208F20C72C8076CB85F5C5921055B62968` |
| `edge/tests/test_outbox_b6_attempt_separation.py` | 新增回归测试 | `E64D78CAF6DAD2D0A6C8DC65FEEFAD90F274506F432BCBD336BC121372902C7C` |
| `edge/tests/test_storage_ipc_auth_s3.py` | 新增回归测试 | `00CB016F02BF0ACB7A9205A76809FDC36E120CAA1835C048D9ABA66664B4FFAE` |
| `edge/tests/test_uploader_b5_ack_preservation.py` | 新增回归测试 | `FC520D66D84FB9A4D67FC71D26D7611CBA39BD3E33D607905AC287321D82349A` |
| `edge/tests/test_uploader_b5_entry_crash.py` | 新增回归测试 | `B31D3AF421321614C0C487E26B53BA8792D6D0DC3A111323CD8DBF5028A73077` |
| `edge/tests/test_uploader_b5_independent.py` | 新增回归测试 | `318BA8B5FEE572897384FEB5215F1308343812E950838FE217B0E82B0FA8309A` |
| `ingest/tests/test_b7_authoritative_reread.py` | 新增回归测试 | `9F025E9BE6D2A734153DED8E4FBE7E0B9C6B2ECF388EEC1465AE3B3215BE58C6` |
| `ingest/tests/test_b7_isolated_mysql_integration.py` | 新增回归测试 | `440D955C645943F37AE3BBF5A41394D827FF9DAB7954196C5B8C0E36F0B8E9E9` |


## 4. 测试环境与命令（复现冻结版本的测试）

| 模块 | 解释器 | 命令（workdir = 仓库根，除非注明） |
|---|---|---|
| cli | 系统 python | `python -m pytest cli/tests -q` |
| collector | `collector\.venv-pack\Scripts\python.exe`（cp314t） | `... -m pytest collector/tests -q` |
| writer | 系统 python | `python -m pytest writer/tests -q` |
| edge | 系统 python | `python -m pytest edge/tests -q` |
| ingest | 系统 python（cp314） | `python -m pytest ingest/tests -q --ignore=ingest/tests/test_http.py` |
| agent | 系统 python + `PYTHONPATH=agent\.venv-pack\Lib\site-packages` | `python -m pytest agent/tests -q` |
| backend | `python3.14t.exe` + `PYTHONPATH=backend\.py314t-site`（**须排第一**）+ **`-Xgil=0`** | workdir=`backend`：`python3.14t -Xgil=0 -m pytest app/tests -q` |
| frontend | Node v24.18.0 | workdir=`frontend`：`npm test` |

两个硬坑：① backend 的 `PYTHONPATH` 必须让 `backend\.py314t-site` **排在最前**，否则 cryptography 触发 access violation 硬崩；② backend 必须加 `-Xgil=0`，否则 `test_free_threading` 失败。
隔离 MySQL/Redis 的哨兵参数见 `隔离测试环境.md` §2、§4、§7。回归脚本：`D:\python\device_alarm\dw-test-env\regression.ps1`。

## 5. 批次 A 最终串行回归（所有修改型 agent 停止后执行）

| 模块 | W0-A 基线 | 最终 | 判定 |
|---|---|---|---|
| cli | 98 | **124 passed** | +26 |
| collector | 619 / 26 skipped / **1 failed** | 619 / 26 / **1 failed** | 未变 |
| writer | 184 | **184 passed** | 未变 |
| edge | 43 | **96 passed** | +53 |
| ingest | 50 / **1 failed** / 5 skipped | 68 / **1 failed** / 6 skipped | +18 |
| agent | 253 / 1 skipped | **347 passed** / 1 skipped | +94 |
| backend | 204 / 6 subtests | **221 passed** / 6 subtests | +17 |
| frontend | 27 / 0 fail | 27 / 0 fail | 未变 |

## 6. 两项既有失败：按"必须核对具体错误，不能只看数量"的要求

### 6.1 `ingest/tests/test_repository.py::test_parent_schema_preflight_marks_workers_ready`
具体错误：`ImportError: ❌ TurboAPI requires a free-threading Python build (no-GIL).`
位置：`D:\Program Files\python\python3.14.6\Lib\site-packages\turboapi\version_check.py:81`
性质：**环境依赖冲突**（TurboAPI 需 free-threading，而 `msgspec` 只有 cp314 轮子，两者互斥）。与本轮改动**无因果关系**（本轮未改 `ingest/**` 中触发该 import 的路径）。

### 6.2 `collector/tests/test_collector_receipt_v2.py::test_outbox_owner_lock_rejects_second_process_and_crash_releases`
具体错误：`AssertionError: assert '' == 'ready'`，位置 `collector/tests/test_collector_receipt_v2.py:286`。
**根因已定位（本轮新增证据）**：该用例用 `subprocess.Popen([sys.executable, "-c", child_code], env=os.environ.copy())` 起子进程，子进程执行 `from storage.collector_outbox import ...`。**不带 `PYTHONPATH` 时子进程报 `ModuleNotFoundError: No module named 'storage'`**，无法打印 `ready`，父进程读到空串。
+ 实测：给子进程加上 `PYTHONPATH=<repo>\collector` 后，子进程**能正常打印 `ready`**（但用例随后在 `collector/storage/collector_outbox.py:179` 以另一个形态失败 → 说明该用例在本机还有第二层问题）。
+ **结构性事实：本轮未修改任何 `collector/**` 文件**（`git status` 可验），故本轮改动**不可能**导致该失败。

### 6.3 结论（诚实）
**不能证明它与基线是"同一个根因"** —— W0-A 基线当时只记录了「1 failed（既有 owner-lock）」，**未记录错误详情**。可成立的表述是：同一个用例、同样 1 个、且位于本轮改动面之外。
**⇒ B–E 的固化要求：基线必须记录每个既有失败的具体错误文本与位置，否则"未变"无法被证明。**

## 7. 补报：一处未出现在自述清单中的改动

`docs/04-运维部署/README.md`（mtime 14:57:41）的内容为 C 项的三类 bind 失败分类说明。
- 该文件**在 S3 的授权范围内**，改动内容**与实现一致**（已由独立验收者核对）。
- 它**未出现在 A/B/C 三项的自述改动清单**中，属**自述完整性缺口**，由独立验收者发现。
- 此处仅作**补报与归属确认**，不代表需要回退。

## 8. 边界与限制（诚实声明）

- **未 commit / 未 push / 未部署**；暂存区 0 项；4 条 stash 名称与内容原样。
- **现场栈的"零写入"claim 必须收窄**：
  * 5 容器 `Up 4 hours` 未重启 ⇒ 只证明**未重启**。
  * Redis `db0` 键数 10 与开工前一致 ⇒ 只证明 **db0 的键数量**未变；**值被改写不会改变键数**。
  * **本轮开工时未对现场栈做内容级基线哈希** ⇒ **内容级完整性无法事后证明**。
  * 因此可成立的表述仅为**过程约束**：所有 agent 被明令禁止写入现场栈，且其改动涉及的代码路径不连接现场服务。**这不是内容级证据。**
  * **B–E 修正措施**：开工前建立**内容级基线**（导出并哈希关键键值集合、记录关键表校验和或行数摘要），而非只记键数与 uptime。
- **快照一致性**：本冻结清单是**某一时刻**的产物。**若冻结后任何文件再被修改，依赖该文件的结论必须重验**；"落盘产物 + agent 是否 running"只用于判断"是否还在写"，**不能替代一致快照**。

## 9. 生成方式（可复现）

- 修改清单：`git -c core.quotepath=false diff --name-only`
- 新增清单：`git -c core.quotepath=false ls-files --others --exclude-standard`
- 完整 diff：`git -c core.quotepath=false diff > docs/整改记录/A-完整diff.patch`（**仅含已跟踪文件的改动**）
- 哈希：`Get-FileHash <file> -Algorithm SHA256`
- 注：`A-完整diff.patch` 与 `A-基线冻结.md` 自身也出现在未跟踪清单中（自引用，无害）。

## 10. 待确认后的提交方案（**尚未执行**）

建议一次性提交为一个**可回退**的 commit（**不 push**）：
- message 建议：`fix(batch-A): 修复 7 个 P0 与 S3 本机 IPC 信任边界；同步 D39 架构口径`
- 是否把 `docs/整改记录/**`（本批证据台账）一并纳入，**请用户确认**。
- 提交前后各记一次哈希清单，核对 commit 内容与本冻结版本一致。
- **行尾注意**：工作区为 LF，git 提示 `LF will be replaced by CRLF`。提交前需确认 `.gitattributes` 设置，避免把 30 个文件变成"全文件行尾变更"的巨型 diff。

## 11. 提交记录（已执行）

| 项 | 值 |
|---|---|
| 回退点（提交前 HEAD） | `6845e800990f8352f46655036741bd79ca25eed1` |
| 提交后 HEAD | `1e32735e3b7533dd937abeb72ca875aa555733bb` |
| 提交标题 | `fix(security,reliability): 批次 A —— 7 个 P0 + S3 本机 IPC 信任边界 + D39 架构口径` |
| 变更规模 | 44 files changed, **7862 insertions(+), 139 deletions(-)**（30 修改 + 14 新增） |
| 是否推送 | **否** —— `origin/main...HEAD` = `0 1`（领先 1 个提交，未 push） |
| 台账 | `docs/整改记录/**` **未纳入**（按用户决定保留在工作区，仍未跟踪） |
| stash | **4 条原样**（名称逐一核对） |

### 提交后一致性校验（**已通过**）

- `git -c core.quotepath=false diff --stat HEAD -- .` → **空** ⇒ 工作区已跟踪文件内容与 HEAD **完全一致**。
- 用 **git 内容寻址**抽样比对 6 个关键文件：`git hash-object <file>` == `git rev-parse HEAD:<file>` → **6/6 match=True**（`edge/app/outbox.py`、`ingest/app/repository.py`、`edge/uploader_main.py`、`cli/update_manager.py`、`edge/app/storage_service.py`、`agent/app/services/edge_storage_client.py`）。
- `git show --name-only HEAD` → **44 个文件**，其中**无** `整改记录`/清单本体命中。
- ⚠️ **方法学提示**：不要用 `git show HEAD:file | Set-Content ... | Get-FileHash` 做字节比对 —— PowerShell 管道会改写换行，会得到**假不匹配**（我第一版就这么错过）。**正确做法是 git 内容寻址**（`hash-object` vs `rev-parse`），或直接看 `git diff HEAD` 是否为空。

### 回退方式

- 撤销提交但保留改动：`git reset --soft 6845e80`（或 `--mixed`）
- 完全回到提交前：`git reset --hard 6845e80`
- 反向提交（推荐用于已共享场景）：`git revert 1e32735`

> **注意**：回退**不会**救回本轮之前已被判 `dead` 的边数据（属 B5/B6 语义范畴），也不影响工作区中未跟踪的台账。
