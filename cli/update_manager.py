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
        return {**result, "updateStarted": True, "updaterProcessId": process_id}

    def rollback(self) -> dict[str, Any]:
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
    [switch]$Rollback
)
$ErrorActionPreference = "Stop"
Wait-Process -Id $ParentProcessId -ErrorAction SilentlyContinue
$current = Join-Path $InstallRoot "current"
$previous = Join-Path $InstallRoot "previous"
$failed = Join-Path $InstallRoot "failed"
if (Test-Path $failed) { Remove-Item -LiteralPath $failed -Recurse -Force }
try {
    if ($Rollback) {
        Move-Item -LiteralPath $current -Destination $failed
        Move-Item -LiteralPath $previous -Destination $current
    } else {
        if (Test-Path $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
        Move-Item -LiteralPath $current -Destination $previous
        Move-Item -LiteralPath $StagedApp -Destination $current
    }
    $result = & (Join-Path $current "data-watch.exe") version | ConvertFrom-Json
    if (-not $result.ok) { throw "Updated CLI version check failed" }
    if ($ExpectedVersion -and $result.data.version -ne $ExpectedVersion) {
        throw "Updated CLI version does not match the signed manifest"
    }
    if ($Rollback -and (Test-Path $failed)) { Move-Item -LiteralPath $failed -Destination $previous }
} catch {
    if (Test-Path $current) { Remove-Item -LiteralPath $current -Recurse -Force }
    if ($Rollback -and (Test-Path $failed)) {
        Move-Item -LiteralPath $failed -Destination $current
    } elseif (Test-Path $previous) {
        Move-Item -LiteralPath $previous -Destination $current
    }
    exit 1
}
Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
'''
