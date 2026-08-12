# DataWatch CLI 安装与更新

## 交付形式

CLI 以 Windows x64 one-folder ZIP 交付，目标机器无需安装 Python。默认安装目录为：

```text
%LOCALAPPDATA%\DataWatch\CLI\current
```

配置保存在 `%APPDATA%\DataWatch\cli-config.json`，令牌按 Profile 保存在 Windows Credential Manager。程序更新不会覆盖配置和凭据。

## 用户安装

解压发布 ZIP，在解压目录打开 PowerShell：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -ApiUrl http://<backend-ip>:8000
data-watch doctor
data-watch auth login --username <账号>
```

安装脚本会复制程序、验证版本、设置 Backend URL、把 `current` 加入当前用户 PATH，并执行健康检查。PATH 变更在新开的 PowerShell 窗口生效。

卸载：

```powershell
powershell -ExecutionPolicy Bypass -File "$env:LOCALAPPDATA\DataWatch\CLI\uninstall.ps1"
```

增加 `-PurgeUserData` 才会删除 CLI 配置；Windows Credential Manager 中的凭据应先通过 `data-watch auth logout` 清理。

## 在线更新

Backend 托管签名后的 release 清单和 ZIP，CLI 在本机验证 Ed25519 签名、文件大小和 SHA-256：

```powershell
data-watch update check
data-watch update download
data-watch update apply --yes
data-watch update rollback --yes
```

`apply` 启动独立更新进程，等待当前 CLI 退出后切换 `current` 与 `previous`。新版本自检失败时自动恢复旧版本。

## 发布操作

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

## Backend 配置

```dotenv
CLI_API_VERSION=1.0
CLI_MIN_VERSION=0.2.0
CLI_MAX_VERSION=0.x
CLI_RELEASE_DIR=/app/cli-releases
```

本地运行不设置 `CLI_RELEASE_DIR` 时，默认使用 `backend/cli-releases`。Docker 使用 `/app/cli-releases`。

## 验收

```powershell
powershell -ExecutionPolicy Bypass -File packaging\smoke-test.ps1
data-watch system compatibility
data-watch doctor
```

至少验证普通用户、管理员、超级管理员三个账号的 `auth capabilities`，并确认越权命令同时被 CLI 预检查和 Backend 拒绝。
