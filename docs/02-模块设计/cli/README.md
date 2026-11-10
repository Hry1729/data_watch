# CLI 模块

> DataWatch CLI 是面向运维人员、自动化脚本和 AI Agent 的 Backend 命令行客户端。当前版本 `0.3.0`，输出契约 `1.0`，M0-M3 与 M5 已完成。

## 文档导航

| 文档 | 内容 |
|---|---|
| [总体设计与架构](总体设计与架构.md) | 边界、组件、调用链、权限、Resolver、可靠性和更新架构 |
| [命令与权限参考](命令与权限参考.md) | 全局选项、命令组、默认角色边界和高风险确认 |
| [AI 调用指南](AI调用指南.md) | 自然语言到命令映射、歧义处理、输出解析和安全规则 |
| [CLI 输出契约](../../03-数据与契约/契约/CLI输出契约.md) | JSON/JSONL、退出码、错误、分页、dry-run 和版本兼容 |
| [CLI 安装与更新](../../04-运维部署/CLI安装与更新.md) | Windows 离线安装、签名发布、自更新、回滚和验收 |

## 模块边界

CLI 负责：

- 命令和参数解析；
- Profile、超时和 Backend 地址管理；
- Windows 凭据安全存储；
- 账号权限预检查；
- 设备与点位业务标识解析；
- REST/WebSocket 请求编排；
- 稳定机器输出、下载和自更新。

CLI 不负责：

- 直接读写 MySQL、Redis、TDengine（例外：`plc get` 用只读 MySQL 账号解析拓扑，见下）；
- 直接操作 Collector、Writer 或 Edge Agent 进程；
- 替代 Backend 权限和业务校验；
- 实现尚不存在的设备点位告警引擎；
- 在当前阶段提供 MCP Server。

`plc get` 是唯一绕过 Backend 取数的命令：它仍走 CLI 登录与权限预检（与 `realtime get` 相同的 `device_monitor:monitor:view` 或 `device_monitor:realtime:view`），由 `direct_read/` 包按 `create → connect → read → disconnect` 短连接读一个 S7 点，结果 `source=direct_plc`。MVP 只支持 Windows + free-threaded Python 3.14t 运行时与 S7 协议；限流、超时与审计状态写在 `DATAWATCH_DATA_ROOT/direct_read/`（未设置时为 `~/.datawatch/direct_read/`）。

## 代码地图

| 路径 | 职责 |
|---|---|
| `cli/main.py` | Typer 根入口、Profile/认证/系统命令、统一输出 |
| `cli/commands/` | 各业务命令组 |
| `cli/client.py` | HTTP、Token 刷新、只读重试、流式下载 |
| `cli/session.py` | 认证会话、权限、本地 dry-run、分页聚合 |
| `cli/resolvers.py` | 设备/点位业务名称解析和会话缓存 |
| `cli/realtime.py` | WebSocket 认证、订阅、过滤和自动重连 |
| `cli/permissions.py` | 命令访问策略和 capabilities |
| `cli/schema.py` | 机器可读命令 schema |
| `cli/config.py` | Profile 和运行参数 |
| `cli/credentials.py` | Windows Credential Manager 凭据存储与 Backend 作用域校验 |
| `cli/update_manager.py` | 签名清单、下载校验、切换和回滚 |
| `cli/commands/plc.py`、`direct_read/` | `plc get` 直连只读：只读拓扑解析、运行时检查、限流、短连接读点与审计 |
| `packaging/` | PyInstaller、安装、卸载、发布和签名工具 |
| `backend/app/api/routes/cli_releases.py` | Backend CLI 发布清单和安装包下载 |

## 运行入口

安装版：

```powershell
data-watch version
data-watch doctor
```

源码开发版：

```powershell
python -m cli.main version
python -m cli.main commands list
```

控制台脚本由 `pyproject.toml` 注册为 `data-watch`。安装版目标机器不需要 Python。

## 事实源优先级

1. Backend API 与权限检查是业务事实源。
2. `cli/permissions.py` 是 CLI 命令访问策略事实源。
3. `data-watch commands list/schema` 是命令参数的机器事实源。
4. 本目录文档解释设计和使用方法，不应手工覆盖运行时 schema。

## 当前验收基线

- CLI 自动化测试：98 项通过；
- CLI 叶子命令与访问策略：130 对 130（`cli/permissions.py` 的 `COMMAND_POLICY_MAP`，2026-11-10 按 `eba47567` 核对，新增 `plc get` 与 `points resync`），无缺失、无冗余；
- Windows ZIP：隔离安装、版本、错误 JSON、命令 schema、卸载均已验证；
- 更新清单：Ed25519 签名、SHA-256 和文件大小均由 CLI 自身验证；
- Backend 本次 CLI 契约测试：Refresh 与 release 路由 5 项通过。
