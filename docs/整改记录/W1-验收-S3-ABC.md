# W1 独立验收：S3 追加三项 A / B / C

- **验收者**：独立验收 agent（非修复者；本会话未对任何实现文件执行过 write/edit）
- **被验对象**：`a0c47502` 的 S3 追加三项（A 401/403 消息与断言、B 锁文件 append 增长、C bind 失败快速失败与分类）
- **依据的修复报告**：`docs/整改记录/S3.md` §「回环判定收尾·补充」（**仅作对照，未采信**）
- **裁决规则**：`docs/整改记录/W1-验收协议.md` §0 / §2 / §4
- **验收日期**：2026-10-03（第一阶段只读 15:11–15:13；第二阶段实测 15:13–15:20，协调者 15:12 发出 GO，全部写者已停止）
- **实现文件指纹**（我在验收期间未改动它们；sha256 供事后比对）
  | 文件 | sha256 |
  |---|---|
  | `edge/storage_main.py` | `7DF89CDB4E4B8D53F3C4897E02F0703678927C9914EC09768C2C89858122E4DC` |
  | `edge/app/uploader.py` | `2E8F57BC021DC002A28FEED9F1A138155AB5B9154A1D8EE50EC65EF2C1B125FD` |
  | `agent/app/services/edge_storage_client.py` | `293B8724D27F6ECFCDBE94EB0D478D468A7EB731201975ED00F8E93B82FC130D` |
  | `edge/tests/test_storage_ipc_auth_s3.py` | `00CB016F02BF0ACB7A9205A76809FDC36E120CAA1835C048D9ABA66664B4FFAE` |
  | `edge/tests/test_outbox_b25_lock_order.py` | `6A1C079A8819DCFFFCEBC8A2D19D62208F20C72C8076CB85F5C5921055B62968` |
  | `agent/tests/test_s3_storage_ipc_token.py` | `276E94108432632FD708CAD2989A1881CB96800CD11EDBE99700D1BCA4CCE5D5` |

> **未做回注反证**：本轮**没有**对实现文件做任何注入/还原操作（避免重犯此前 CRLF/LF 覆盖备份的事故）。所有反向检查改用**外部探针**（真实子进程 / 真实 HTTP 服务 / 独立假服务器），因此无需 pristine 备份流程。

---

## 0. 对照 14:47:39 中间态快照：最终是否**真的落地**

### 0.1 快照的取得方式（披露）

我的上下文里**没有**一份落盘的快照文件：工作区、`%TEMP%`、`docs/` 与 DSH 会话库中均不存在 14:47:39 的观察记录文件（已逐处检索）。该快照实际存档在**我上一轮会话的转录**里：`C:\Users\myrh\.dsh\sessions\--D-python-device_alarm-data_watch--\baf7f746-9a6f-42a9-bbee-19f0563adb24\session.v4.jsonl.zstd`（`parentSession` = 本父会话、`origin=subagent`）。该文件是多帧拼接 zstd（87 帧），系统无 `zstd`/`zstandard` 可用，我用捆绑 node v24.21 的 `zlib.zstdDecompressSync` 按帧魔数扫描解码（87/87 帧成功、818 319 B），从中取回原始观察记录。

⇒ 本节数字来自**当时真实的工具输出**（含 `Get-Date` 回显行 `2026年10月3日 14:47:39` 与 `Get-ChildItem` 表），不是回忆、不是推断。

### 0.2 快照 vs 最终（逐文件）

| 文件 | 快照（14:47:39 前后） | 最终（我验收时） | 落地判定 |
|---|---|---|---|
| `agent/app/services/edge_storage_client.py` | `14:46:09` / 5143 B | `14:46:09` / 5143 B | **A 已落地且早于快照**，此后一字未改 |
| `agent/tests/test_s3_storage_ipc_token.py` | `14:46:45` / 7841 B（快照内已含 `pytest.raises(StorageUnauthorized, match=r"HTTP 401")` 与 5 个用例） | `14:46:45` / 7841 B | **A 已落地且早于快照**，此后一字未改 |
| `edge/app/uploader.py` | `14:47:15` / 28328 B，且**守卫在 `if os.name == "nt":` 分支内**（快照 seq 122 实读：`640: if os.name == "nt":` → `646: self._file.seek(0, 2)`） | `14:50:53` / 28284 B，守卫已移到分支外 | **快照时是"半成品形态"**；**+3 min 14 s 后被重写为最终形态** ⇒ 落地成立，但**不是**快照时那一版 |
| `edge/tests/test_outbox_b25_lock_order.py` | `14:47:43` / 17769 B | `14:47:43` / 17769 B | B 回归用例写在快照**同一秒区**，此后未改 |
| `edge/storage_main.py` | `14:38:28` / **5056 B**（快照 seq 127 实读：只有 `_is_loopback` + `from … import StorageEngine, StorageHttpServer, default_token_file`，**无 `IpcAuthenticator`、无 `_bind_error_message`**） | `14:59:37` / **7061 B** | **快照时 C 完全没落地**；**+12 min 后被写入** |
| `edge/tests/test_storage_ipc_auth_s3.py` | `14:40:49` / **31134 B**（无任何 bind 分类用例） | `15:00:39` / **37049 B** | 同上：**快照时 C 的回归用例不存在** |
| `edge/app/storage_service.py` | `13:10:00` / 22401 B | `13:10:00` / 22401 B | A/B/C **确实未碰**它（与自述一致） |
| `docs/整改记录/S3.md` | `14:43:34` / 60524 B | `15:04:04` / 71735 B | 自述章节写于快照之后 |

### 0.3 结论（这是本轮最关键的一条）

1. **A：快照即可证明已落地**（快照时刻该文件已含新断言全文），最终字节与快照一致 ⇒ **证实**，且**不存在"自述了但没落地"**。
2. **B：快照证明当时落地的是"守卫仍在 nt 分支内"的形态**（与 collector **不同构**）；**最终形态（移出分支）是在 14:50:53 之后才落地的**。⇒ 如果只看 14:47:39 的快照，会得出"B 未按要求做完"的**正确**结论；最终确实补齐了 ⇒ **证实**（以最终字节为准，并经我实测）。
3. **C：快照证明当时完全没落地**（`storage_main.py` 仍是 5056 B 的旧版、`engine.start()` 仍在 bind 之前、无 `_bind_error_message`）⇒ **快照写下即过期**是正确的描述；最终 14:59:37/15:00:39 确实落地 ⇒ **证实**。
4. 附带证实了协调者 README §2 第 2 次误判的机理：快照里 `test_outbox_b25_lock_order.py` 的 mtime `14:47:43` **比快照自身时间戳还晚 4 秒** —— 文件当时正在被写，任何基于该快照的"未应用"结论都会立即失效。

---

## 1. (A) 401/403 消息与断言

- 验收者 / 依据的修复报告：独立验收 agent / `S3.md` §「回环判定收尾·补充」§1

**我重跑的命令与真实输出（串行）**

```text
$env:PYTHONUTF8='1'; $env:PYTHONIOENCODING='utf-8'; $env:PYTHONPATH='agent\.venv-pack\Lib\site-packages'
python -m pytest agent/tests/test_s3_storage_ipc_token.py -q -v
→ collected 5 items ... 5 passed in 4.87s

python -m pytest agent/tests -q
→ 347 passed, 1 skipped, 1 warning in 59.11s
```

我自建的独立探针（真实 `StorageHttpServer` + 真实令牌文件 + 真实 agent 客户端，夹具全在 `%TEMP%`）：

```text
[ OK ] A1 valid agent token still works
   401 msg: Edge Storage rejected the IPC credential (HTTP 401): check the 'agent' role token in storage-ipc-tokens.json
[ OK ] A2 no credential -> StorageUnauthorized/HTTP 401/no 'unavailable'
   403 msg: Edge Storage rejected the IPC credential (HTTP 403): check the 'agent' role token in storage-ipc-tokens.json
[ OK ] A3 collector token on status -> StorageUnauthorized/HTTP 403
   dead-port msg: Edge Storage unavailable: <urlopen error timed out>
[ OK ] A4 dead port -> StorageUnavailable, NOT StorageUnauthorized
   500 msg: Edge Storage unavailable: HTTP Error 500: Internal Server Error
[ OK ] A5 HTTP 500 -> NOT StorageUnauthorized (no over-classification)
   old-style msg would FAIL the 'unavailable not in' assertion; new-style passes
[ OK ] A6 'unavailable not in' has discriminating power
FAILURES []
```

**断言强度是否真的上升（结论：是，但要分维度说）**

| 维度 | 改前（HEAD `agent/tests/test_s3_storage_ipc_token.py:136,148`） | 改后（`:141-144`、`:156-159`） | 强度 |
|---|---|---|---|
| 异常类型 | `RuntimeError`（宽泛） | `StorageUnauthorized`（**严格子类**） | **上升**（真子集关系） |
| 消息 | `match="Edge Storage unavailable"`（旧文案） | `match=r"HTTP 401"`（状态码） | 换了判据，不可直接比大小 |
| 附加 | 无 | `isinstance(..., StorageUnavailable)` | **冗余**（由已声明的子类关系蕴含），不削弱 |
| 附加 | 无 | `"unavailable" not in str(...)` | **有判别力**，见下 |

- **无删除、无放宽**：两处旧断言被**替换**而非删除，且替换后的类型约束是旧的严格子集关系；同一文件另 3 条用例一字未动（`git diff` 无该文件，因其为新增未跟踪文件；我逐行实读确认）。
- **A6「判别力」验证（回答"会不会恒真"）**：该断言**不是恒真**。旧口径消息 `Edge Storage unavailable (IPC credential rejected or not permitted, HTTP 401): …` 同时满足 `match=r"HTTP 401"` 但**会被 `"unavailable" not in` 判死**；新口径消息通过。⇒ 它恰好拦住"把误导性前缀加回来"这一种回归，判别力**真实但很窄**（与 `match=r"HTTP 401"` 高度耦合，不是独立维度）。也**不恒假**（实测通过）。
- **"与 edge 侧同口径"的逐字核对**：edge 侧为 `Edge Storage rejected the IPC credential (HTTP {code}, role={role}): check the role token in the Edge Storage IPC token file`；agent 侧为 `… (HTTP {code}): check the 'agent' role token in storage-ipc-tokens.json`。**同为"不含 `unavailable` + 含 HTTP 状态码"的同一口径，但不逐字相同**（agent 侧无 `role=` 段）。自述"同口径"成立，"逐字一致"不成立。

**我构造的反例与结果**

1. **过度归类（最关键那条）**：确定无人监听的端口 → `StorageUnavailable`、`not isinstance(…, StorageUnauthorized)`、消息含 `unavailable`。⇒ **未推翻**。实测消息是 `<urlopen error timed out>`（1.0 s 超时）而非 connection refused——本机对该回环死端口的失败表现为超时，但**分类正确**。
2. **反向过度归类**：真实返回 **HTTP 500** 的服务 → `StorageUnavailable`，**不是** `StorageUnauthorized`（`A5`）。⇒ 未推翻，未把非认证类失败误标为认证失败。
3. **越权路径**：`collector` 令牌调 `status` → 服务端 `permits()` 明确拒绝并回 **403**（`storage_service.py:494-498` + `IPC_ROLE_ACTIONS["collector"]` 不含 `status`）⇒ 实测 403，未推翻。
4. **合法链路未瘫**：有效 `agent` 令牌 `status()` 正常（`A1`）⇒ 未矫枉过正。

**假修复模式排查（§2 清单逐项）**：try/except 吞异常 **否**（`HTTPError` 被改写为**更具体**的异常类型抛出，非吞掉）；放宽断言 **否**（类型收紧）；新增 skip/xfail **否**（`grep` 该文件仅 `parametrize`/`asyncio` 标记）；注释掉断言 **否**；只改 mock **否**（真实 server + 真实 token 文件）；改测试迁就代码 **这一项我单独判定**：改测试是**授权范围内**的，且其前提是代码侧真的改变了行为契约（新增 `StorageUnauthorized` + 新文案），并且新断言**严于**旧断言 —— 属"修正测试固化的不准确期望"，**不是**假修复。

- **回归数字对照（A）**：`agent` 全量 253 → **347 passed / 1 skipped**（我实测），单文件 4 → **5 passed**。
- **越界检查（A）**：仅 `agent/app/services/edge_storage_client.py`（实现）与 `agent/tests/test_s3_storage_ipc_token.py`（测试），均在授权清单内。
- **证据等级**：真实入口 + 隔离集成（真实 IPC 服务端 + 真实令牌文件 + 真实客户端）；`A5` 的 500 场景是**隔离集成**（自建假 HTTP 服务，非 edge 引擎）。
- **最终裁决：证实。**

---

## 2. (B) 锁文件 append 增长

- 依据的修复报告：`S3.md` §「回环判定收尾·补充」§2

**我重跑的命令与真实输出**

```text
python -m pytest edge/tests/test_outbox_b25_lock_order.py -q
→ 6 passed in 2.04s

python -m pytest edge/tests -q
→ 96 passed in 30.21s
```

我自建独立探针（真实现有类，无 mock）：

```text
[ OK ] B1 20 failed acquires keep size == 1 | failures=20 sizes=[1]
[ OK ] B2 content after release is exactly b'0' | b'0'
[ OK ] B3 size after release == 1 | 1
[ OK ] B4 re-acquire after release works, size still 1
[ OK ] B5 child holds the lock | LOCKED
[ OK ] B6 live cross-process holder blocks us | another uploader instance is running
[ OK ] B7 size still 1 after a cross-process failure
[ OK ] B8 lock recoverable after the holder is killed
[ OK ] B9 size still 1 after crash recovery
[ OK ] B10 content still b'0'
FAILURES []
```

**反例要点逐条**

| 要求 | 我的实测 | 结论 |
|---|---|---|
| 多次争锁失败 size 不增长 | **20 次**失败后 `sizes=[1]`（报告称 20 次；**仓内回归用例只循环 5 次**，我另做 20 次补齐） | 成立 |
| 单实例互斥（跨进程） | 子进程持锁时本进程 `acquire` 抛 `RuntimeError("another uploader instance is running")`，且 size 仍 1 | 成立 |
| 释放后可重新获取 | B4 通过 | 成立 |
| 崩溃后租约可恢复 | `kill()` 持锁子进程后立即重新 `with SingleInstanceLock(...)` 成功 | 成立 |
| 字节内容 | 释放后恒为 `b"0"`（Windows 字节锁是强制锁，持锁期间第二个句柄读锁定区间会 `PermissionError`，故只在释放后读） | 成立 |

**"既有 vs 本轮引入"更正是否属实（用 `git show HEAD:edge/app/uploader.py` 对照）**

```text
HEAD（6845e80）edge/app/uploader.py::SingleInstanceLock.__enter__
    try:
        if os.name == "nt":
            import msvcrt

            self._file.seek(0)
            self._file.write(b"0")      ← 写操作确实在 nt 分支内
            self._file.flush()
            self._file.seek(0)
            msvcrt.locking(...)
        else:
            import fcntl
            fcntl.flock(...)           ← POSIX 分支只有 flock，从不写字节
```

⇒ **更正属实**：`write(b"0")` 本来就在 `if os.name == "nt":` 内，因此"POSIX 锁文件 0 字节"是**既有行为**，**不是**本轮引入。

**同时我要精确化另一半**：快照（seq 122，14:47:15 版）**实读**显示当时守卫**确实被放进了 nt 分支内**（`640: if os.name == "nt":` → `646: self._file.seek(0, 2)`），所以观察者 (i) 关于**位置**的描述是**成立的**；不成立的是它的**因果推论**（"POSIX 下**现在不再**写那 1 字节"暗示"以前会写"）。修复者的更正命中的正是这个因果推论。⇒ 双方各对一半，**修复者的更正为真**。

**逐行同构核验（不只看"像"）**：我把 `collector/storage/collector_outbox.py::_acquire_owner` 与 `edge/app/uploader.py::SingleInstanceLock.__enter__` 的核心块抽出、去掉注释与空行、把 `self._owner_file`→`F`、`self._file`→`F` 归一化后逐行比较：

```text
collector core lines: 11
IDENTICAL after variable-name normalization: True
    F.seek(0, 2)
    if F.tell() == 0:
        F.write(b"0")
        F.flush()
    F.seek(0)
    if os.name == "nt":
        import msvcrt
        msvcrt.locking(F.fileno(), msvcrt.LK_NBLCK, 1)
    else:
        import fcntl
        fcntl.flock(F.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
uploader extra lines beyond the core: []
```

⇒ **归一化后逐字节同构**。仅存的文本差异：uploader 侧多 4 行解释性注释 + `import msvcrt` 后多一个空行 + 变量名/异常消息不同。**语义零差异**。

**与自述的差异**：报告称"实测首次 `size=1`、20 次争锁失败后仍恒为 1（改前 5 次即 1→6）"。我**独立佐证**了改前数字：`%TEMP%\dw-lock-f166fi20\uploader.lock` = **6 字节，mtime 14:37:27**（改前演示）；`%TEMP%\dw-lockfix-67h_34gn\uploader.lock` = **1 字节，mtime 14:48:04**（改后演示）。这两处临时产物与该自述**一致**。

- **回归数字对照（B）**：`edge` 43 → **96 passed / 0 failed**；B25 文件 5 → **6 passed**。
- **越界检查（B）**：仅 `edge/app/uploader.py`（`SingleInstanceLock` 一处）+ `edge/tests/test_outbox_b25_lock_order.py`（新增 1 条用例）。
- **证据等级**：单元 + 真实入口（跨进程 `subprocess` 实测互斥与崩溃恢复）。**POSIX 分支无法在本机执行**（Windows 主机）⇒ POSIX 侧行为**未覆盖**，其结论仅由"与 collector 已上线实现逐字节同构"支撑。
- **最终裁决：证实**（POSIX 平台行为标注为未覆盖）。

---

## 3. (C) bind 失败快速失败 + 分类

- 依据的修复报告：`S3.md` §「回环判定收尾·补充」§3

**我重跑的命令与真实输出**

```text
python -m pytest edge/tests/test_storage_ipc_auth_s3.py -q
→ 23 passed in 18.35s

python -m pytest edge/tests -q
→ 96 passed in 30.21s
```

**四组真实入口实测（我自建探针，`subprocess` 跑真实 `edge/storage_main.py`，timeout=60 s）**

| 场景 | rc | 耗时 | 分类文案（日志末行要点） | 建库? | 挂死? |
|---|---|---|---|---|---|
| ① 端口已被占用（`127.0.0.1:busy` 独占监听） | **2** | **0.22 s** | `… the port is already in use, or this process is not permitted to bind it [PermissionError: [WinError 10013]…]` | 否 | **否** |
| ② `--allow-non-loopback --host 192.168.1.99` | **2** | **0.23 s** | `… that address does not belong to this host [OSError: [WinError 10049]…]` | 否 | 否 |
| ②′ `--allow-non-loopback --host 192.0.2.1`（仓内用例用的地址） | **2** | **0.23 s** | 同上 | 否 | 否 |
| ③ `--host ::1` | **2** | **0.23 s** | `… this IPC endpoint is IPv4-only (ThreadingHTTPServer / AF_INET) … IPv6 (e.g. ::1) is not supported, use an IPv4 address such as 127.0.0.1 [gaierror: [Errno 11001]…]` | 否 | 否 |
| ④ `--host 127.0.0.1` | 服务中 | boot 0.52 s | `listening on http://127.0.0.1:…`，`/healthz`=**200** | 是 | — |
| ④ `--host localhost` | 服务中 | boot 0.52 s | `listening on http://localhost:…`，`/healthz`=**200** | 是 | — |
| ④ `--host localhost.` | 服务中 | boot 0.51 s | `listening on http://localhost.:…`，`/healthz`=**200** | 是 | — |
| ④ `--host 127.0.0.2` | 服务中 | boot 5.54 s | `listening on http://127.0.0.2:…`，`/healthz`=**200**（探 `127.0.0.2`）；**`127.0.0.1` 同端口不可达** | 是 | — |
| ⑤ 令牌文件不可读（路径指向目录） | **1** | **0.21 s** | `PermissionError: [Errno 13] Permission denied: '…\\ipc\\tokens.json'`（traceback 自带 `repr` 路径） | 否 | 否 |

- **不得挂死**：以上 9 次真实入口运行全部在 **0.21–0.23 s** 内自行退出（④ 为常驻服务，被 `terminate()` 收尾）；**没有任何一次触发 60 s 超时**，也**没有**出现"进程存活但无监听"。对照修复前实测（报告 §3：6 s 后 `alive=True, rc=None`）为**真实修复**。
- **非回环地址不可达（反向）**：对 ④ 的四个绑定，逐个连接本机**全部**非回环 IPv4（`172.18.112.1`、`172.22.112.1`、`192.168.1.75`、`172.19.0.1`）**全部不可达**；且绑定 `127.0.0.2` 时 `127.0.0.1` 也不可达 ⇒ 既未开给网络，也精确按请求地址绑定。

**三类错误互斥（断言层）**：仓内三条用例各自断言另两类文案**不出现**（`already in use` / `does not belong` / `IPv4-only` 两两互斥，另加 `Refusing to bind` 与 `listening` 不出现）；我的实测输出每个场景**只命中一类**。⇒ **互斥成立**。

**令牌文件不可读这条（重点验）**：`rc=1`（不是 2、不是 0）、输出**不含** `Cannot bind Edge Storage IPC`、且含 `repr(token_path)`。根因链路我实读确认：`IpcAuthenticator.from_file()` 被移到 `try` **之外**，而 `StorageHttpServer.__init__` 是**先建 authenticator、后 `super().__init__()`（bind）**（`storage_service.py:450-454`），所以若不显式传入 authenticator，该 `PermissionError` 就会被本项的 `except OSError` 误标为"端口被占用"。⇒ 自查缺陷**已真修**。

**我的反例与结果**

1. **端口占用 vs 地址族错误 vs 地址不属于本机** → 三类分别 rc=2 且文案互斥（上表）。未推翻。
2. **`_ipv4_resolution_error()` 是否真的删干净**：全仓库 `grep` 该标识符**只命中 `S3.md` 文档**，代码中**零命中** ⇒ 分类确实只留一处。未推翻。
3. **是否矫枉过正（把合法回环写法也拦掉）**：四种合法写法全部 200 ⇒ 未推翻。
4. **`--host 0.0.0.0`**：仍走安全拒绝（rc=2、`Refusing to bind … non-loopback address`），未被本次改动放开（仓内既有断言 + 我实读 `storage_main.py:114-121` 守卫在 bind 之前）。

**与修复者报告的差异**

1. **地址不同**：报告表格用 `192.168.1.99`，仓内回归用例用 `192.0.2.1`（RFC 5737 TEST-NET-1）。两者我都实测，**同一分类同一文案**；不是矛盾，但"报告里跑的场景"与"仓内可自动回归的场景"并非同一个地址。
2. **耗时不符**：报告逐项写 `1.23–1.24 s`；我实测 **0.21–0.23 s**（含解释器启动）。结论（快速失败、不挂死）一致，**具体数字我无法复现**，属测量方式/机器瞬时状态差异。
3. **越界（未声明）**：`docs/04-运维部署/README.md` 的 mtime 是 **14:57:41**，正落在 A/B/C 时间窗内，`git diff` 显示新增内容正是 C 的三类 bind 失败分类与"绑定失败一律快速失败（退出码 2）"段落。该文件**属 S3 总体授权清单**（`README.md` §「改动文件归属」），但**不在 A/B/C 自述列的"两份文档"内** ⇒ **越界未声明（轻度、内容对题）**。
4. 报告称"`engine.start()` 自身失败仍无收尾、本轮未改"——我**未覆盖**该场景（未构造 DB 初始化失败），与自述一致，列为遗留。

- **回归数字对照（C）**：`edge/tests/test_storage_ipc_auth_s3.py` 17 → **23 passed**；`edge` 整体 43（W0-A）→ 90（P2 阶段）→ **96 passed / 0 failed**；新增 C 用例 5 条（其中 `::1` 参数化 ×2 ⇒ 6 个 test item），与"23−17=6"吻合。
- **越界检查（C）**：实现只改 `edge/storage_main.py`（`_bind_error_message` 新增、bind 前移、`IpcAuthenticator.from_file()` 移出 try、删除 `_ipv4_resolution_error()`）；`edge/app/storage_service.py` **零改动**（mtime 13:10:00 与快照一致，且 sha 未变）；测试只改 `edge/tests/test_storage_ipc_auth_s3.py`。
- **证据等级**：**真实入口**（真实子进程 + 真实套接字 + 真实 HTTP 探测）+ 单元（`_is_loopback` 真值表）。
- **最终裁决：证实。**

---

## 4. 总表

| 条目 | 裁决 | 证据等级 | 遗留问题 |
|---|---|---|---|
| A 401/403 消息与断言 | **证实** | 真实入口 + 隔离集成 | `isinstance(StorageUnavailable)` 冗余；"同口径"非逐字一致 |
| B 锁文件 append 增长 | **证实** | 单元 + 真实入口（跨进程） | POSIX 分支无法本机执行 ⇒ 该平台行为**未覆盖**；仓内回归只做 5 次争锁（我另做 20 次） |
| C bind 快速失败 + 分类 | **证实** | 真实入口 + 单元 | `engine.start()` 自身失败（DB 初始化）仍可能挂死，未改未验；IPC 入口 IPv6 仍不支持（只登记）；`docs/04-运维部署/README.md` 属未声明改动 |

---

## 5. 现场栈安全基线复核（`隔离测试环境.md` §3.7）

```text
data-watch-frontend | Up 4 hours
data-watch-backend  | Up 4 hours
data-watch-redis    | Up 4 hours
data-watch-mysql    | Up 4 hours
data-watch-tdengine | Up 4 hours
dw-test-redis       | Up 4 hours (healthy)
dw-test-mysql       | Up 4 hours (healthy)
redis> info keyspace  → db0:keys=10,expires=0
redis> config get databases → 16
```

- 5 个现场容器**仍 Up 且未重启**（基线 11:55 时为 `Up 17 minutes`，现 `Up 4 hours`，时长**单调增长**、未归零）⇒ 无 agent 越界重启。
- 现场 Redis `db0` 键数仍为 **10**、`databases` 仍为 **16** ⇒ 与基线一致。
- `C:\ProgramData\DataWatch Edge`：**本会话未读写**。本次全部夹具在 `%TEMP%`（`dw-abc-*`），子进程环境里所有 `DATAWATCH_*` 变量被**显式剥离后重设**为临时目录；`agent/tests` 内自带 `programdata` 断言亦通过。
- 隔离栈 `dw-test-redis` / `dw-test-mysql` 均 `Up (healthy)`。

---

## 6. 假修复模式清单（§2）逐项

| 模式 | 排查结果 |
|---|---|
| try/except 吞异常 | **无**。C 的 `except OSError` 是"分类 + return 2"，且**故意**把 token 文件 OSError 留在 try 外（⑤ 实测 rc=1 证明未被吞）；A 把 `HTTPError` 改写为更具体异常并抛出；B 未新增 except |
| 放宽断言（`==`→`>=`、删 assert、加 tolerance） | **无**。全仓库 diff 中删除的 `assert` 行只有 2 处：`edge/tests/test_uploader_main.py` 的锁路径断言（被替换为**两条更强**断言：锁随库走）；`edge/tests/test_storage_service.py` 的 `client.apply(...)==1`（角色拆分后保留为 `agent.apply(...)==1`）。A 的类型约束是严格收紧 |
| 新增 skip / xfail / `pytest.skip()` | **无**。三个相关测试文件 `grep` 只有 `parametrize` / `asyncio` 标记；实测输出 0 skipped（edge）、agent 的 1 skipped 是既有 |
| 注释掉断言或整段测试 | **无**（逐行实读三个文件 + diff 扫描） |
| 只改 mock/fake 层 | **无**。A 用真实 `StorageHttpServer`+真实令牌；B 用真实类 + 真实跨进程；C 用真实子进程入口 |
| 改测试去迁就代码 | A 改测试**经协调者明确授权**，且代码侧行为契约真的变了（新增异常类型 + 新文案），新断言严于旧断言 ⇒ 不算假修复（详见 §1） |
| 把"永久拒绝"整体删掉 | **无**。非回环绑定守卫仍在 bind 之前（`storage_main.py:114-121`，`--host 0.0.0.0` 仍 rc=2）；鉴权仍默认拒绝 |
| 鉴权默认关闭 / 整体跳过校验 | **无**。`do_POST` 仍先 `role_for()` 再 `permits()`；我实测无凭据=401、越权=403 |
| 静默轮换密钥 | 不适用（A/B/C 未碰密钥生成） |
| 删失败路径清理 | 不适用；C 只是**新增**一条早退路径（bind 失败即退出，不再创建非守护线程） |

---

## 7. 回归数字对照（全部我亲自串行实测）

| 范围 | W0-A 基线 | 我实测 | 修复者/协调者自述 | 一致 |
|---|---|---|---|---|
| `edge/tests` | 43 passed | **96 passed / 0 failed / 30.21 s** | 96 | ✓ |
| `edge/tests/test_storage_ipc_auth_s3.py` | 17 passed | **23 passed** | 23 | ✓ |
| `edge/tests/test_outbox_b25_lock_order.py` | 5 passed | **6 passed** | 6 | ✓ |
| `agent/tests/test_s3_storage_ipc_token.py` | 4 passed | **5 passed** | 5 | ✓ |
| `agent/tests`（全量） | 253 passed / 1 skipped | **347 passed / 1 skipped / 59.11 s** | 347 | ✓ |

- **无通过数下降**，无新增失败，无新增 skip。`collector` 既有 1 failed 与 `ingest` 既有 1 failed 与 A/B/C 无交集（本轮我未重跑这两个模块，采用协调者串行全量数字并已核对其"既有失败"归属与 W0-A 基线一致）。
- 我验收期间（15:13–15:20）A/B/C 相关的 6 个实现/测试文件 mtime **保持不变**（最大为 `15:00:39`）⇒ 我测的就是最终字节。唯一在窗口外被改动的是 `docs/整改记录/README.md`（`15:10:57`，协调者本人台账），不影响代码证据。

---

## 8. 越界检查汇总

- 授权清单内：`edge/storage_main.py`、`edge/app/uploader.py`、`agent/app/services/edge_storage_client.py`、`edge/tests/test_storage_ipc_auth_s3.py`、`edge/tests/test_outbox_b25_lock_order.py`、`agent/tests/test_s3_storage_ipc_token.py`、`docs/整改记录/S3.md`。
- **清单外但已发生**：`docs/04-运维部署/README.md`（`14:57:41`，内容为 C 的行为说明，属 S3 总体授权文件，但**未在 A/B/C 自述的"两份文档"内声明**）。⇒ 记录为**轻度未声明越界**，需协调者知悉；建议要么追认，要么要求其在 `S3.md` 中补登记。
- 无顺手重构：`edge/app/storage_service.py` 一字未动（sha 与快照期一致）；`edge/app/uploader.py` 的 B5/B6 改动属其他条目，非本轮。
- `docs/整改记录/README.md`（`15:10:57`）由**协调者**写入（a0c47502 会话已于 `15:06:08` 结束），不归 A/B/C。
- 无新增第三方依赖（`storage_main.py` 只新增 `errno`/`socket`/`ipaddress` 标准库导入；未恢复原先的 `_ipv4_resolution_error()`）。

---

## 9. 我自己的方法学更正（避免把环境伪影当缺陷）

第一版 C④ 探针报出 4 条 FAIL，**全部是我探针的错误**，据实记录：

1. **`127.0.0.2` 我探错了地址**：我把 `/healthz` 探到 `127.0.0.1`（该绑定只监听 `127.0.0.2`），于是误判"未服务"。改为探绑定地址后 `healthz=200`。**并因此获得一条额外证据**：绑定 `127.0.0.2` 时 `127.0.0.1` **不可达** ⇒ 绑定地址被精确遵守。
2. **"任意非本机地址可达"是本机网络伪影**：我对 `192.0.2.1` / `203.0.113.9` / `198.51.100.7` 上一个**确定无人监听**的端口做 TCP 连接，三个**全部 connect 成功** ⇒ 本机出站路径上存在代答（透明代理/网关），因此**用"任意公网地址可达性"判断本地暴露是无效判据**。改用**本机自身的非回环 IPv4**（`172.18.112.1` / `172.22.112.1` / `192.168.1.75` / `172.19.0.1`）后，四个绑定**全部不可达** ⇒ C 的"未开给网络"结论成立。**若我沿用第一版判据，就会凭空造出一条假缺陷。**

---

## 10. 遗留问题与未覆盖项

1. **C 未覆盖**：`engine.start()` **自身**失败（如 DB 初始化失败）仍发生在非守护 writer 线程启动**之后**，与修复前同类，进程仍可能挂死。本轮未改、我未构造验证 ⇒ **未覆盖**，与自述一致。
2. **B 未覆盖**：POSIX（`fcntl.flock`）分支无法在本机执行 ⇒ POSIX 下锁文件的 0/1 字节行为**未实测**，仅由"与 collector 逐字节同构"支撑。
3. **A 未覆盖**：跨账户 ACL、目标机（真实部署）行为；`wait_ready` 对 401 仍会重试到超时（诊断信息保留、行为未改，与自述一致）。
4. **C 超范围项**：让 IPC 入口支持 IPv6（`StorageHttpServer.address_family`）按协调者要求**只登记不修**；现为"快速失败 + 明确说明 IPv4-only"。
5. **A 的窄判别力**：`"unavailable" not in` 只能拦住"前缀回退"这一种回归，与 `match=r"HTTP 401"` 高度耦合；若将来把 401/403 的分类改为按响应体判定，这条断言的证明力会下降。
6. **C② 场景不一致**：报告演示地址（`192.168.1.99`）与仓内回归地址（`192.0.2.1`）不同，建议在文档中统一，或在用例中同时覆盖两者。
7. **`docs/04-运维部署/README.md` 未声明改动**：建议在 `S3.md` 补登记，避免"改动文件归属"表与实际不符。

---

## 11. 我的操作边界自证

- **未改动任何实现文件的最终内容**：本会话对实现/测试文件**零 write/edit**；第 1 节列出的 sha256 为验收时的实际字节。
- **未做回注反证** ⇒ 无备份/还原流程、无 CRLF 风险。
- **无 git 写操作**：只执行 `git status` / `git diff` / `git show` / `git stash list` / `git log`；`stash list` 仍为 **4 条**（`docs-leftover-backup`、`synced-agent-docs-backup`、`stage1-uncommitted-before-sync`、`codex: preserve generated pycache before merging 002`），**原样保留**；HEAD 仍为 `6845e80`；**未 commit**。
- **未连接/操作现场栈**：仅 `docker ps` 只读与 `redis-cli info keyspace` / `config get` 只读；未执行任何写命令。
- **未读写 `C:\ProgramData\DataWatch Edge`**：所有夹具在 `%TEMP%\dw-abc-*`；子进程 `DATAWATCH_*` 一律显式指向临时目录。
- **测试严格串行**：第二阶段每条命令单独执行、无并发；执行时所有写者已停止（A/B/C 文件 mtime 自 `15:00:39` 起未变）。
- **唯一写入**：本文件 `docs/整改记录/W1-验收-S3-ABC.md`（任务交付物）。
