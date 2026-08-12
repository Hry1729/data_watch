# CLI 输出契约

> 当前 CLI 版本 `0.3.0`，输出 `schemaVersion` 为 `1.0`。本契约面向 PowerShell、CI、AI Agent 和其他 subprocess 调用方。

## 1. 输出通道

| 场景 | stdout | stderr | 退出码 |
|---|---|---|---:|
| 成功 | 一个 JSON 文档 | 空 | 0 |
| 一次性命令失败 | 空 | 一个 JSON 文档 | 非 0 |
| `realtime watch` | 每行一个 JSON 文档；失败前可能已有事件 | 失败时一个 JSON 文档 | 0/非 0 |
| 文件下载 | JSON 元数据 | 空或错误 JSON | 0/非 0 |

默认输出紧凑 JSON。`--pretty` 只改变排版，不改变字段；该全局选项必须写在一级命令之前。

## 2. 成功包络

```json
{
  "schemaVersion": "1.0",
  "cliVersion": "0.3.0",
  "requestId": "3eb02fe8-5d1b-4bb7-a07e-4b28b1fe8c66",
  "ok": true,
  "command": "devices status",
  "data": {}
}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `schemaVersion` | string | CLI 输出结构版本 |
| `cliVersion` | string | 当前 CLI 程序版本 |
| `requestId` | UUID string | 单次 CLI 进程生成的本地关联 ID |
| `ok` | boolean | 成功固定为 `true` |
| `command` | string | 规范化叶子命令名 |
| `data` | any | 命令业务结果；允许对象、数组、标量或 null |

`requestId` 当前是 CLI 输出关联 ID，不等价于 Backend trace ID。需要跨层追踪写请求时额外提供 `--idempotency-key`。

## 3. 失败包络

```json
{
  "schemaVersion": "1.0",
  "cliVersion": "0.3.0",
  "requestId": "3eb02fe8-5d1b-4bb7-a07e-4b28b1fe8c66",
  "ok": false,
  "command": "auth whoami",
  "error": {
    "type": "auth_required",
    "message": "请先执行 auth login"
  }
}
```

`error.status` 和 `error.details` 是可选字段。调用方先按退出码分类，再使用 `error.type` 做细分，不解析自然语言 `message` 决定程序分支。

常见错误类型：

| `error.type` | 语义 |
|---|---|
| `usage_error` / `config_error` | 参数、Profile 或本地配置错误 |
| `auth_required` / `unauthorized` | 缺少、失效或错误的认证 |
| `permission_denied` | 账号无所需权限或角色 |
| `resource_not_found` | 设备、点位或其他资源不存在 |
| `ambiguous_resource` | 业务标识匹配到多个最佳候选 |
| `confirmation_required` | 高风险操作缺少正确确认 |
| `network_error` | 无法连接 Backend |
| `timeout` | HTTP、下载或实时等待超时 |
| `api_error` | Backend 业务错误或响应契约错误 |
| `internal_error` | 未分类 CLI 内部错误 |
| `update_signature_error` | 更新清单签名不受信任或无效 |
| `update_integrity_error` | 更新包大小或 SHA-256 不匹配 |

## 4. 退出码

| 退出码 | 常量 | 含义 |
|---:|---|---|
| `0` | 成功 | 命令完成 |
| `2` | `EXIT_USAGE` | 参数、配置或确认错误 |
| `3` | `EXIT_AUTH_REQUIRED` | 未登录、令牌失效或认证失败 |
| `4` | `EXIT_PERMISSION_DENIED` | 权限不足 |
| `5` | `EXIT_NETWORK` | 网络连接失败 |
| `6` | `EXIT_API` | Backend 或 CLI 执行错误 |
| `7` | `EXIT_PARTIAL` | 部分成功 |
| `8` | `EXIT_TIMEOUT` | 请求超时 |
| `130` | 用户取消 | `realtime watch` 被 Ctrl+C 终止 |

## 5. JSON Lines

`realtime watch` 持续输出，每一行都必须独立满足成功包络：

```jsonl
{"schemaVersion":"1.0","cliVersion":"0.3.0","requestId":"...","ok":true,"command":"realtime watch","data":{"type":"snapshot"}}
{"schemaVersion":"1.0","cliVersion":"0.3.0","requestId":"...","ok":true,"command":"realtime watch","data":{"type":"diff"}}
```

调用方不得等进程退出后再一次性解析全部 stdout，应逐行读取并设置自己的事件数、时长和输出大小上限。AI 优先使用返回单个 JSON 的 `realtime window`。

## 6. JMESPath 投影

全局 `--select` 只投影成功结果的 `data`，外层包络保持不变：

```powershell
data-watch --select "list[].deviceName" devices list
```

投影结果仍位于 `data`。表达式非法返回退出码 2 和 `usage_error`。

## 7. 自动分页

`--all-pages` 只作用于已识别出 `pageNo/pageSize` 或 `current/size` 的 GET 命令。聚合结果保留接口原有列表字段，并增加：

```json
{
  "total": 120,
  "fetched": 100,
  "allPages": true,
  "truncated": true
}
```

- `fetched`：实际返回条数；
- `truncated`：因 `--max-items` 停止时为 true；
- `total`：Backend 报告的总数；
- 默认上限 10000，最大 100000。

## 8. Dry-run

对非 GET/HEAD 请求使用 `--dry-run` 时，不发送业务请求，`data` 返回解析后的请求摘要：

```json
{
  "dryRun": true,
  "request": {
    "method": "POST",
    "path": "/api/device-monitor/device",
    "params": null,
    "jsonBody": {},
    "idempotencyKey": null
  }
}
```

字段名为 `password`、`token`、`refreshToken`、`secret`、`authorization` 的值会被替换为 `***`。dry-run 仍会执行 Profile、登录、权限和 Resolver 校验。

## 9. 文件输入输出

- JSON 对象参数使用 `--file path.json`；
- `--file -` 从 stdin 读取 JSON；
- Excel 导入使用 `.xlsx`；
- 下载先写临时文件，再原子替换目标文件；
- 下载成功输出路径、文件名和字节数，不把二进制写到 stdout。

## 10. 兼容性

- `schemaVersion` 只在破坏性输出结构变化时升级；
- 新增可选字段不要求升级主版本；
- 调用方必须忽略不认识的可选字段；
- 调用方可通过 `system compatibility` 获取 Backend 支持范围；
- 安装和升级前可执行 `doctor`；
- 命令参数变更以 `commands schema` 为机器事实源。

## 11. 敏感信息

CLI 禁止在正常输出中返回密码、Access Token 或 Refresh Token。自动化系统也不得记录：

- `DATAWATCH_TOKEN` / `DATAWATCH_REFRESH_TOKEN`；
- Windows Credential Manager 内容；
- WebSocket 认证消息中的 Token；
- 用户通过 stdin 提供的密码；
- 发布签名私钥。
