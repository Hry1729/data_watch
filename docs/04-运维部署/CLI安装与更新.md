# DataWatch CLI 安装与更新

> 当前发布基线：CLI `0.3.0`、输出契约 `1.0`、平台 `windows-x64`。CLI 是独立工具包，不进入中心式、分布式边缘或分布式中心三角色安装包。

## 1. 交付形式

CLI 以 Windows x64 one-folder ZIP 交付，目标机器无需安装 Python。默认安装目录为：

```text
%LOCALAPPDATA%\DataWatch\CLI\current
```

配置保存在 `%APPDATA%\DataWatch\cli-config.json`，令牌按 Profile 保存在 Windows Credential Manager。程序更新不会覆盖配置和凭据。

## 2. 用户安装

解压发布 ZIP，在解压目录打开 PowerShell：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -ApiUrl http://<center-ip>:8080
data-watch doctor
data-watch auth login --username <账号>
data-watch auth capabilities
```

安装脚本会复制程序、验证版本、设置 Backend URL、把 `current` 加入当前用户 PATH，并执行健康检查。PATH 变更在新开的 PowerShell 窗口生效。生产推荐填写 frontend/nginx 的统一入口；`8000` Backend 直连只用于开发或受控排障。

安装目录采用 `current` / `previous` 双版本结构。新版目录切换失败时安装脚本立即恢复旧 `current`。自动更新（`update apply` / `update rollback`）的切换把已完成的每一步写入 `<InstallRoot>\swap-state.txt`（阶段值形如 `update:backed-up` / `update:swapped` / `update:verified`）：失败时**只撤销已完成的步骤**，**不再无条件删除仍有效的 `current`**，因此任何失败路径下都至少留有一份可运行的旧版本；若切换进程中途崩溃，下一次运行会先按该状态文件把完整旧版本搬回 `current` 再继续。配置和凭据位于安装目录之外，因此重装程序不会覆盖它们。

卸载：

```powershell
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\DataWatch\CLI\uninstall.ps1"
```

增加 `-PurgeUserData` 才会删除 CLI 配置；Windows Credential Manager 中的凭据应先通过 `data-watch auth logout` 清理。

## 3. 在线更新

Backend 托管签名后的 release 清单和 ZIP，CLI 在本机验证 Ed25519 签名、文件大小和 SHA-256：

```powershell
data-watch update check
data-watch update download
data-watch update apply --yes
data-watch update rollback --yes
```

`update check/download/apply` 不依赖登录态，安全性由内置受信任公钥和签名校验保证；网络仍应限制在内网发布服务。只有签名、文件大小和 SHA-256 全部通过才进入切换阶段。

`apply` 启动独立更新进程，等待当前 CLI 退出后切换 `current` 与 `previous`。新版本自检失败时自动恢复旧版本。因为 updater 必须等当前 CLI 退出后才真正交换目录，`update apply` 只报「已提交待验证」：返回 `updateStarted=true`，同时返回 `updateVerified=false`、`verificationRequired=true`、`reason=restart_required` 与 `statePath=<InstallRoot>\swap-state.txt`；升级是否真正成功要在重启后由该文件的 `…:verified` 阶段值（或 `UpdateManager.swap_state()`）独立确认。源码运行方式可检查和下载版本，但只有 Windows 安装脚本部署的 `current/data-watch.exe` 支持自动应用与回滚。

### 3.1 离线更新

无法访问 Backend release 接口时，将新 ZIP 送达目标机并重新运行其中的 `install.ps1`。脚本保留外部配置和 Credential Manager 凭据，并把原 `current` 保存为 `previous`。安装后执行：

```powershell
data-watch version
data-watch system compatibility
data-watch doctor
```

## 4. 发布操作

首次部署生成发布密钥：

```powershell
python packaging\release-tool.py generate-key `
  --private packaging\keys\cli-release-private.key `
  --public packaging\keys\cli-release-public.key
```

私钥目录被 Git 忽略，必须离线备份。把公钥和 key ID 写入 `cli/update_keys.py` 后重新构建所有受信任客户端。当前仓库已配置一把发布公钥，本机私钥与其匹配。

构建并发布：

```powershell
powershell -ExecutionPolicy Bypass -File packaging\build-cli.ps1
powershell -ExecutionPolicy Bypass -File packaging\publish-cli-release.ps1 -SkipBuild
```

产物位于 `artifacts/`，Backend 托管文件位于 `backend/cli-releases/`。两者都是生成物，不提交 Git。Docker Compose 已把 `backend/cli-releases` 只读挂载到 Backend。

CI 使用 `.github/workflows/cli-release.yml`，仓库 Secret `CLI_RELEASE_PRIVATE_KEY_B64` 必须保存与 `cli/update_keys.py` 公钥对应的原始私钥 Base64 文本。

## 5. Backend 配置

```dotenv
CLI_API_VERSION=1.0
CLI_MIN_VERSION=0.2.0
CLI_MAX_VERSION=0.x
CLI_RELEASE_DIR=/app/cli-releases
```

本地运行不设置 `CLI_RELEASE_DIR` 时，默认使用 `backend/cli-releases`。Docker 使用 `/app/cli-releases`。

## 6. 验收

```powershell
powershell -ExecutionPolicy Bypass -File packaging\smoke-test.ps1
data-watch system compatibility
data-watch doctor
```

至少验证普通用户、管理员、超级管理员三个账号的 `auth capabilities`，并确认越权命令同时被 CLI 预检查和 Backend 拒绝。

发布验收还应包含：

- 在不安装 Python 的 Windows x64 目标机运行 `data-watch version`；
- 使用生产 Nginx 地址完成登录、设备状态、实时窗口和历史查询；
- 验证中文用户名路径、中文设备名和 IO 地址；
- 用错误参数确认 stderr 是合法 JSON 且退出码为 2；
- 检查 `commands schema "realtime window"`；
- 检查更新包配套 SHA-256 清单；
- 验证 `update apply --yes` 后版本正确，`update rollback --yes` 可恢复上一版；
- 确认安装包和发布目录不含签名私钥。

## 7. 常见问题

| 现象 | 处理 |
|---|---|
| `data-watch` 不是命令 | 新开 PowerShell；或直接运行 `%LOCALAPPDATA%\DataWatch\CLI\current\data-watch.exe` |
| `credential_scope_mismatch` | Backend URL 已变化；确认地址后重新 `auth login` |
| `auth_required` / `unauthorized` | 执行 `auth refresh`，失败则重新登录 |
| `permission_denied` | 执行 `auth capabilities`，由管理员调整 `permission_code` |
| `network_error` / `timeout` | 执行 `doctor`，检查 Nginx/Backend、DNS、防火墙和代理超时 |
| `update_signature_error` | 停止更新，确认发布公钥、Key ID 和 release 清单来源 |
| `update_integrity_error` | 删除下载缓存并重新下载，检查发布目录文件是否完整 |
| WebSocket 无事件 | 先用 `realtime get` 验证点位，再检查 Nginx Upgrade 代理和账号监控权限 |

## 8. 相关文档

- [CLI 模块说明](../02-模块设计/cli/README.md)
- [CLI 总体设计与架构](../02-模块设计/cli/总体设计与架构.md)
- [命令与权限参考](../02-模块设计/cli/命令与权限参考.md)
- [AI 调用指南](../02-模块设计/cli/AI调用指南.md)
- [CLI 输出契约](../03-数据与契约/契约/CLI输出契约.md)
- [API 契约](../03-数据与契约/契约/API契约.md)
- [WebSocket 协议](../03-数据与契约/契约/WebSocket协议.md)
