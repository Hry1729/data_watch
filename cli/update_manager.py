from __future__ import annotations

import base64
import hashlib
import json
import os
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path
from typing import Any
from uuid import uuid4

from cryptography.exceptions import InvalidSignature
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey
from packaging.version import InvalidVersion, Version

from cli.client import BackendClient
from cli.errors import CliError, EXIT_API, EXIT_USAGE
from cli.update_keys import TRUSTED_RELEASE_KEYS
from cli.version import CLI_VERSION


PLATFORM = "windows-x64"
_SWAP_STATE_FILENAME = "swap-state.txt"
# 终态阶段：verified=新版本已通过自检；failed=失败恢复已完成；
# recovered=中断的 swap 已由 reconcile 恢复（但本次没有校验通过任何新版本）。
_TERMINAL_SWAP_PHASES = (":verified", ":failed", ":recovered")


class UpdateManager:
    def __init__(self, client: BackendClient, *, install_root: Path | None = None) -> None:
        self.client = client
        self.install_root = install_root or _installation_root()

    def check(self, *, channel: str = "stable") -> dict[str, Any]:
        manifest = self.client.request(
            "GET",
            "/api/cli/releases/latest",
            params={"channel": channel, "platform": PLATFORM},
            authenticated=False,
        )
        if not isinstance(manifest, dict):
            raise CliError("update_error", "更新清单格式错误", EXIT_API)
        _verify_manifest(manifest, channel=channel)
        latest = str(manifest.get("version") or "")
        try:
            update_available = Version(latest) > Version(CLI_VERSION)
        except InvalidVersion as exc:
            raise CliError("update_error", "更新版本号格式错误", EXIT_API) from exc
        return {
            "currentVersion": CLI_VERSION,
            "latestVersion": latest,
            "updateAvailable": update_available,
            "channel": channel,
            "platform": PLATFORM,
            "manifest": manifest,
        }

    def download(self, *, channel: str = "stable") -> dict[str, Any]:
        result = self.check(channel=channel)
        manifest = result["manifest"]
        archive = manifest["archive"]
        target_dir = self.install_root / "downloads"
        target_dir.mkdir(parents=True, exist_ok=True)
        target = target_dir / str(archive["fileName"])
        self.client.download_to(
            str(archive["downloadUrl"]),
            target,
            authenticated=False,
        )
        _verify_archive(target, archive)
        return {**result, "downloadPath": str(target), "downloaded": True}

    def apply(self, *, channel: str = "stable") -> dict[str, Any]:
        # 上一次 swap 被中断时 current 可能已经不存在（脚本刚把它搬成 previous），
        # 而 current 正是 _require_windows_install 的前置条件：先恢复，再校验。
        _recover_interrupted_swap(self.install_root)
        _require_windows_install(self.install_root)
        result = self.download(channel=channel)
        if not result["updateAvailable"]:
            return {**result, "updateStarted": False, "reason": "already_current"}
        version = str(result["latestVersion"])
        archive_path = Path(result["downloadPath"])
        staged_root = self.install_root / f"staging-{version}"
        if staged_root.exists():
            shutil.rmtree(staged_root)
        staged_root.mkdir(parents=True)
        _safe_extract(archive_path, staged_root)
        staged_app = staged_root / "app"
        if not (staged_app / "data-watch.exe").is_file():
            raise CliError("update_error", "更新包缺少 app/data-watch.exe", EXIT_API)
        process_id = _launch_swap(
            install_root=self.install_root,
            staged_app=staged_app,
            expected_version=version,
            rollback=False,
        )
        # updater 会在本进程退出后才真正交换目录，此刻无法知道结果：
        # 只报「已提交待验证」，是否成功必须由 swap_state() 独立确认。
        return {
            **result,
            "updateStarted": True,
            "updateVerified": False,
            "verificationRequired": True,
            "reason": "restart_required",
            "statePath": str(self.install_root / _SWAP_STATE_FILENAME),
            "updaterProcessId": process_id,
        }

    def swap_state(self) -> dict[str, Any]:
        """读取 swap 脚本留下的显式阶段状态，作为独立的验证/取证入口。"""
        path = self.install_root / _SWAP_STATE_FILENAME
        if not path.is_file():
            return {"state": None, "updateVerified": False, "statePath": str(path)}
        try:
            state = path.read_text(encoding="ascii").strip()
        except (OSError, UnicodeDecodeError):
            return {"state": "unreadable", "updateVerified": False, "statePath": str(path)}
        return {
            "state": state,
            "updateVerified": state.endswith(":verified"),
            "statePath": str(path),
        }

    def rollback(self) -> dict[str, Any]:
        # 同 apply()：先让中断的 swap 恢复到可用状态，再重新做前置校验。
        _recover_interrupted_swap(self.install_root)
        _require_windows_install(self.install_root)
        previous = self.install_root / "previous"
        if not (previous / "data-watch.exe").is_file():
            raise CliError("update_error", "没有可回滚的上一版本", EXIT_USAGE)
        process_id = _launch_swap(
            install_root=self.install_root,
            staged_app=previous,
            expected_version="",
            rollback=True,
        )
        return {"rollbackStarted": True, "updaterProcessId": process_id}


def _verify_manifest(manifest: dict[str, Any], *, channel: str) -> None:
    signature = manifest.get("signature")
    archive = manifest.get("archive")
    if not isinstance(signature, dict) or not isinstance(archive, dict):
        raise CliError("update_signature_error", "更新清单缺少签名或文件信息", EXIT_API)
    if manifest.get("schemaVersion") != "1.0":
        raise CliError("update_error", "不支持的更新清单版本", EXIT_API)
    if manifest.get("channel") != channel or manifest.get("platform") != PLATFORM:
        raise CliError("update_error", "更新清单与请求通道或平台不匹配", EXIT_API)
    if signature.get("algorithm") != "ed25519":
        raise CliError("update_signature_error", "不支持的更新签名算法", EXIT_API)
    key_id = str(signature.get("keyId") or "")
    public_key = TRUSTED_RELEASE_KEYS.get(key_id)
    if not public_key:
        raise CliError("update_signature_error", "更新签名密钥不受信任", EXIT_API)
    payload = {key: value for key, value in manifest.items() if key != "signature"}
    canonical = json.dumps(
        payload,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    try:
        Ed25519PublicKey.from_public_bytes(base64.b64decode(public_key, validate=True)).verify(
            base64.b64decode(str(signature.get("value") or ""), validate=True),
            canonical,
        )
    except (InvalidSignature, ValueError) as exc:
        raise CliError("update_signature_error", "更新清单签名无效", EXIT_API) from exc


def _verify_archive(path: Path, archive: dict[str, Any]) -> None:
    actual_size = path.stat().st_size
    expected_size = int(archive.get("size") or -1)
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    actual_hash = digest.hexdigest()
    expected_hash = str(archive.get("sha256") or "").lower()
    if actual_size != expected_size or actual_hash != expected_hash:
        path.unlink(missing_ok=True)
        raise CliError("update_integrity_error", "更新包完整性校验失败", EXIT_API)


def _safe_extract(archive: Path, destination: Path) -> None:
    root = destination.resolve()
    try:
        with zipfile.ZipFile(archive) as bundle:
            for member in bundle.infolist():
                target = (destination / member.filename).resolve()
                if root != target and root not in target.parents:
                    raise CliError("update_error", "更新包包含不安全路径", EXIT_API)
            bundle.extractall(destination)
    except zipfile.BadZipFile as exc:
        raise CliError("update_error", "更新包不是有效 ZIP 文件", EXIT_API) from exc


def _installation_root() -> Path:
    explicit = os.environ.get("DATAWATCH_INSTALL_ROOT")
    if explicit:
        return Path(explicit).expanduser().resolve()
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent.parent
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        return Path(local_app_data) / "DataWatch" / "CLI"
    return Path.home() / ".data-watch" / "cli-install"


def _require_windows_install(root: Path) -> None:
    if os.name != "nt" or not (root / "current" / "data-watch.exe").is_file():
        raise CliError(
            "update_error",
            "自动更新仅支持由 Windows 安装脚本部署的 CLI",
            EXIT_USAGE,
        )


def _pending_swap_phase(root: Path) -> str | None:
    """返回未完成的 swap 阶段值；没有状态文件或已是终态时返回 None。

    状态文件为空（写入被截断）或读不动（权限/损坏）时按「可能未完成」处理：
    宁可多跑一次不删除任何版本的 reconcile，也不要让 current 缺失的安装被
    前置校验直接拦死。
    """
    path = root / _SWAP_STATE_FILENAME
    if not path.is_file():
        return None
    try:
        state = path.read_text(encoding="ascii").strip()
    except (OSError, UnicodeDecodeError):
        return "unreadable"
    if state and state.endswith(_TERMINAL_SWAP_PHASES):
        return None
    return state


def _recover_interrupted_swap(install_root: Path) -> None:
    """在 `_require_windows_install` 之前，把上一次未完成的 swap 恢复到可用状态。

    swap 脚本顶部本来就有按 `swap-state.txt` 的 reconcile，但那只在脚本被启动后
    才生效；而崩于第一次 `Move-Item`（current -> previous）之后时 `current` 缺失，
    `_require_windows_install` 会在脚本启动之前就抛 `CliError`，使 reconcile 在
    真实 CLI 入口上不可达。这里同步跑一次 `-ReconcileOnly`：只做恢复、不交换目录、
    成功后把阶段记为 `<mode>:recovered`。恢复不了就什么都不假装，仍由
    `_require_windows_install` 如实报错。
    """
    if os.name != "nt" or _pending_swap_phase(install_root) is None:
        return
    script_path = install_root / f"recover-{uuid4().hex}.ps1"
    try:
        script_path.write_text(_SWAP_SCRIPT, encoding="utf-8")
    except OSError:
        return
    arguments = [
        "powershell.exe",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(script_path),
        "-InstallRoot",
        str(install_root),
        "-StagedApp",
        str(install_root),  # -ReconcileOnly 不读取该参数
        "-ParentProcessId",
        str(os.getpid()),  # -ReconcileOnly 不等待父进程
        "-ReconcileOnly",
    ]
    try:
        subprocess.run(
            arguments,
            stdin=subprocess.DEVNULL,
            capture_output=True,
            text=True,
            timeout=120,
            check=False,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
    except (OSError, subprocess.SubprocessError):
        # 恢复进程本身跑不起来：不伪造成功，交给前置校验如实报错。
        return
    finally:
        script_path.unlink(missing_ok=True)


def _launch_swap(
    *,
    install_root: Path,
    staged_app: Path,
    expected_version: str,
    rollback: bool,
) -> int:
    script_path = install_root / f"update-{uuid4().hex}.ps1"
    script_path.write_text(_SWAP_SCRIPT, encoding="utf-8")
    arguments = [
        "powershell.exe",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(script_path),
        "-InstallRoot",
        str(install_root),
        "-StagedApp",
        str(staged_app),
        "-ParentProcessId",
        str(os.getpid()),
        "-ExpectedVersion",
        expected_version,
    ]
    if rollback:
        arguments.append("-Rollback")
    flags = getattr(subprocess, "CREATE_NO_WINDOW", 0) | getattr(subprocess, "DETACHED_PROCESS", 0)
    process = subprocess.Popen(
        arguments,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        creationflags=flags,
        close_fds=True,
    )
    return process.pid


_SWAP_SCRIPT = r'''[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$InstallRoot,
    [Parameter(Mandatory=$true)][string]$StagedApp,
    [Parameter(Mandatory=$true)][int]$ParentProcessId,
    [string]$ExpectedVersion = "",
    [switch]$Rollback,
    [switch]$ReconcileOnly
)
$ErrorActionPreference = "Stop"
if (-not $ReconcileOnly) { Wait-Process -Id $ParentProcessId -ErrorAction SilentlyContinue }
$current = Join-Path $InstallRoot "current"
$previous = Join-Path $InstallRoot "previous"
$failed = Join-Path $InstallRoot "failed"
$statePath = Join-Path $InstallRoot "swap-state.txt"
$mode = if ($Rollback) { "rollback" } else { "update" }

function Set-SwapPhase {
    param([Parameter(Mandatory=$true)][string]$Phase)
    # 先写临时文件再原子替换：写入过程失败时状态文件保持上一个有效阶段值，
    # 不会留下「空/半截」的阶段值把下一次恢复逻辑带偏。
    $tempPath = "$statePath.tmp"
    [System.IO.File]::WriteAllText($tempPath, $Phase, [System.Text.Encoding]::ASCII)
    if (Test-Path -LiteralPath $statePath) {
        $backupPath = "$statePath.bak"
        [System.IO.File]::Replace($tempPath, $statePath, $backupPath)
        Remove-Item -LiteralPath $backupPath -Force -ErrorAction SilentlyContinue
    } else {
        [System.IO.File]::Move($tempPath, $statePath)
    }
}
function Get-SwapPhase {
    if (-not (Test-Path -LiteralPath $statePath)) { return "" }
    return (Get-Content -LiteralPath $statePath -Raw).Trim()
}
function Test-CompleteVersion {
    param([Parameter(Mandatory=$true)][string]$Path)
    return (Test-Path -LiteralPath (Join-Path $Path "data-watch.exe") -PathType Leaf)
}
# 只撤销已完成的步骤：current 不见了才搬回来，绝不先删 current。
function Restore-MissingCurrent {
    if ((-not (Test-Path -LiteralPath $current)) -and (Test-CompleteVersion $previous)) {
        Move-Item -LiteralPath $previous -Destination $current
    }
}
# 新版本已搬入但未通过校验：先把它挪到 failed（保留取证），再把完整旧版本搬回 current。
function Restore-UnverifiedSwap {
    if (-not (Test-CompleteVersion $previous)) { return }
    if (Test-Path -LiteralPath $current) {
        if (-not (Test-CompleteVersion $current)) { return }
        if (Test-Path -LiteralPath $failed) { Remove-Item -LiteralPath $failed -Recurse -Force }
        Move-Item -LiteralPath $current -Destination $failed
    }
    Move-Item -LiteralPath $previous -Destination $current
}

$crashedPhase = Get-SwapPhase
if ($crashedPhase -eq "update:backed-up" -or $crashedPhase -eq "update:swapped") {
    Restore-UnverifiedSwap
} else {
    # 这里只做「把完整旧版本搬回 current」；failed 的回收必须等确认本次交换
    # 真的能开始之后再做（见下面 try 内），否则可能在 previous 已被消耗时
    # 先删掉目录里最后一份可运行版本。
    Restore-MissingCurrent
}

# -ReconcileOnly：只做恢复，不交换目录；恢复不到可用 current 就如实非 0 退出。
if ($ReconcileOnly) {
    if (-not (Test-CompleteVersion $current)) { exit 1 }
    $priorMode = if ($crashedPhase -like "rollback:*") { "rollback" } else { "update" }
    # 只在阶段值没有被改动过时落 recovered，避免覆盖仍在运行的 updater 的进度。
    if ((Get-SwapPhase) -eq $crashedPhase) { Set-SwapPhase "$priorMode`:recovered" }
    exit 0
}

Set-SwapPhase "$mode`:prepare"
try {
    # 本次要装进去的版本必须完整，否则在动任何目录之前中止：例如 previous 已被
    # 上一次回滚消耗、以 rollback 重启时，只有中止才能保住 failed 里的被淘汰版本。
    if (-not (Test-CompleteVersion $StagedApp)) { throw "Staged version is missing or incomplete" }
    # 回收 failed 槽位必须发生在确认 current 完整之后（current 即另一个可运行版本）。
    if ((Test-CompleteVersion $current) -and (Test-Path -LiteralPath $failed)) {
        Remove-Item -LiteralPath $failed -Recurse -Force
    }
    if ($Rollback) {
        Move-Item -LiteralPath $current -Destination $failed
        Set-SwapPhase "$mode`:displaced"
        Move-Item -LiteralPath $previous -Destination $current
        Set-SwapPhase "$mode`:swapped"
    } else {
        if (Test-Path -LiteralPath $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
        if (Test-Path -LiteralPath $previous) { throw "Stale backup directory could not be removed" }
        Move-Item -LiteralPath $current -Destination $previous
        Set-SwapPhase "$mode`:backed-up"
        Move-Item -LiteralPath $StagedApp -Destination $current
        Set-SwapPhase "$mode`:swapped"
    }
    $result = & (Join-Path $current "data-watch.exe") version | ConvertFrom-Json
    if (-not $result.ok) { throw "Updated CLI version check failed" }
    if ($ExpectedVersion -and $result.data.version -ne $ExpectedVersion) {
        throw "Updated CLI version does not match the signed manifest"
    }
    if ($Rollback -and (Test-Path $failed)) { Move-Item -LiteralPath $failed -Destination $previous }
    Set-SwapPhase "$mode`:verified"
} catch {
    # 阶段状态文件本身可能写失败（磁盘满/ACL/被占用）。除了阶段值，再用目录状态
    # 交叉判定：update 模式下若 StagedApp 已被搬走而 current 又存在，说明 current
    # 是那份从未通过自检的新版本，必须换回完整旧版本，而不是原样留着它。
    $failedPhase = Get-SwapPhase
    $unverifiedInPlace = (-not $Rollback) -and (
        $failedPhase -eq "update:swapped" -or
        ($failedPhase -eq "update:backed-up" -and (Test-Path -LiteralPath $current) -and
            -not (Test-Path -LiteralPath $StagedApp))
    )
    if ($unverifiedInPlace) { Restore-UnverifiedSwap } else { Restore-MissingCurrent }
    Set-SwapPhase "$mode`:failed"
    exit 1
}
Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
'''
