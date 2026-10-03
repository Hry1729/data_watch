"""B41 回归：CLI 更新 swap 前半失败时不得删除仍有效的 current。

本文件只新建独立用例，不修改既有的 ``cli/tests/test_update_manager.py``。

安全边界（见 ``docs/整改记录/隔离测试环境.md``）：
- 所有实验都在 pytest ``tmp_path`` 内完成，读的是 ``cli.update_manager._SWAP_SCRIPT``
  这一段生产脚本文本 + 真实 ``powershell.exe`` 子进程；
- **不对真实安装目录、ProgramData 或任何现场服务执行更新/回滚**；
- 用 ``csc.exe`` 现场编译一个假的 ``data-watch.exe``，使 "可运行版本" 是真实可执行
  语义（脚本里 ``data-watch.exe version`` 的校验会真的跑），而不是只看文件是否存在。
"""

from __future__ import annotations

import ctypes
import os
import re
import shutil
import subprocess
import tempfile
import zipfile
from ctypes import wintypes
from pathlib import Path
from typing import Any

import pytest

import cli.update_manager as update_module
from cli.errors import CliError
from cli.update_manager import UpdateManager

POWERSHELL = shutil.which("powershell.exe") or shutil.which("powershell")
_CSC = Path(r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe")
_ANCHOR = '$ErrorActionPreference = "Stop"'
_VERSION_DIRS = ("current", "previous", "failed")
_TEMP_ROOT = Path(tempfile.gettempdir()).resolve()


def _assert_temp_root(path: Path) -> Path:
    """安全围栏：实验根必须落在系统临时目录内，绝不指向真实安装目录。

    `C:\\ProgramData\\DataWatch Edge` 是本机真实安装（见 `隔离测试环境.md` §1.1），
    本文件的任何路径都不得与之相交。
    """
    resolved = Path(path).resolve()
    if resolved != _TEMP_ROOT and _TEMP_ROOT not in resolved.parents:
        raise AssertionError(f"B41 安全围栏：实验根 {resolved} 不在临时目录 {_TEMP_ROOT} 内")
    return resolved


pytestmark = pytest.mark.skipif(
    os.name != "nt" or not POWERSHELL,
    reason="B41 需要 Windows 与 powershell.exe 才能复现 swap 脚本行为",
)


# --------------------------------------------------------------------------- #
# 假 data-watch.exe：真的可执行，版本号由环境变量控制
# --------------------------------------------------------------------------- #

_FAKE_CLI_SOURCE = r"""
public class FakeDataWatchCli
{
    public static int Main(string[] args)
    {
        string version = System.Environment.GetEnvironmentVariable("DATAWATCH_FAKE_VERSION");
        if (version == null || version.Length == 0) { version = "9.0.0"; }
        System.Console.WriteLine("{\"ok\":true,\"data\":{\"version\":\"" + version + "\"}}");
        return 0;
    }
}
"""


@pytest.fixture(scope="session")
def fake_cli_exe(tmp_path_factory: pytest.TempPathFactory) -> Path:
    """编译一个最小可执行的假 CLI；缺编译器就如实跳过而不是伪造通过。"""
    if not _CSC.is_file():
        pytest.skip(f"未找到 .NET Framework 编译器：{_CSC}")
    workdir = tmp_path_factory.mktemp("b41-fake-cli")
    _assert_temp_root(workdir)
    source = workdir / "fake_cli.cs"
    source.write_text(_FAKE_CLI_SOURCE, encoding="utf-8")
    exe = workdir / "data-watch.exe"
    completed = subprocess.run(
        [str(_CSC), "/nologo", f"/out:{exe}", str(source)],
        capture_output=True,
        text=True,
        timeout=120,
    )
    if completed.returncode != 0 or not exe.is_file():
        pytest.skip(f"csc 编译失败：{completed.stdout}{completed.stderr}")
    return exe


# --------------------------------------------------------------------------- #
# 沙盒构造与观测
# --------------------------------------------------------------------------- #


def _seed_root(root: Path, exe: Path, *, previous: bool = False) -> Path:
    """current=OLD（正在运行的完整旧版本）、staging-9.0.0/app=NEW，可选更老的 previous。"""
    _assert_temp_root(root)
    marker = {"OLD": "OLD", "NEW": "NEW", "OLDER": "OLDER"}
    for name, tag in (("current", "OLD"), ("staging-9.0.0/app", "NEW"), ("previous", "OLDER")):
        if name == "previous" and not previous:
            continue
        target = root / name
        (target / "nested").mkdir(parents=True, exist_ok=True)
        shutil.copy2(exe, target / "data-watch.exe")
        (target / "keep.txt").write_text(marker[tag], encoding="utf-8")
        (target / "nested" / "inner.txt").write_text(marker[tag], encoding="utf-8")
    return root / "staging-9.0.0" / "app"


def _complete_version(root: Path, name: str) -> str | None:
    """某个版本目录是否「完整」：可执行文件 + 全部内容都在，返回其标记。"""
    target = root / name
    if not (target / "data-watch.exe").is_file():
        return None
    try:
        marker = (target / "keep.txt").read_text(encoding="utf-8")
        inner = (target / "nested" / "inner.txt").read_text(encoding="utf-8")
    except OSError:
        return None
    return marker if marker == inner else None


def _is_runnable(target: Path) -> bool:
    exe = target / "data-watch.exe"
    if not exe.is_file():
        return False
    completed = subprocess.run([str(exe), "version"], capture_output=True, text=True, timeout=60)
    return completed.returncode == 0 and '"ok":true' in completed.stdout


def _runnable_versions(root: Path) -> dict[str, str]:
    """目录里所有完整且真的可运行的版本（按版本目录名）。"""
    found: dict[str, str] = {}
    for name in _VERSION_DIRS:
        marker = _complete_version(root, name)
        if marker and _is_runnable(root / name):
            found[name] = marker
    return found


def _run_swap(
    script_text: str,
    root: Path,
    staged: Path,
    *,
    expected_version: str = "9.0.0",
    rollback: bool = False,
    env: dict[str, str] | None = None,
    script_name: str = "swap.ps1",
) -> subprocess.CompletedProcess[str]:
    _assert_temp_root(root)
    script_path = root / script_name
    script_path.write_text(script_text, encoding="utf-8")
    arguments = [
        POWERSHELL,
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(script_path),
        "-InstallRoot",
        str(root),
        "-StagedApp",
        str(staged),
        "-ParentProcessId",
        "999999",
        "-ExpectedVersion",
        expected_version,
    ]
    if rollback:
        arguments.append("-Rollback")
    environment = dict(os.environ)
    environment.update(env or {})
    return subprocess.run(arguments, capture_output=True, text=True, timeout=180, env=environment)


def _inject(script_text: str, prelude: str) -> str:
    assert _ANCHOR in script_text, "生产 swap 脚本缺少错误处理锚点，注入方式需同步更新"
    return script_text.replace(_ANCHOR, _ANCHOR + "\n" + prelude.strip("\n"), 1)


def _move_fail(nth: int) -> str:
    return rf'''
$script:B41Move = 0
function Move-Item {{
  [CmdletBinding()]
  param([Parameter(Mandatory=$true)][string]$LiteralPath, [Parameter(Mandatory=$true)][string]$Destination)
  $script:B41Move++
  if ($script:B41Move -eq {nth}) {{ throw "INJECTED Move-Item #{nth}" }}
  Microsoft.PowerShell.Management\Move-Item -LiteralPath $LiteralPath -Destination $Destination
}}
'''


def _move_crash_after(nth: int) -> str:
    """让第 nth 次 Move-Item 真实完成后再硬崩（模拟移动完成、阶段状态未写完）。"""
    return rf'''
$script:B41Move = 0
function Move-Item {{
  [CmdletBinding()]
  param([Parameter(Mandatory=$true)][string]$LiteralPath, [Parameter(Mandatory=$true)][string]$Destination)
  $script:B41Move++
  Microsoft.PowerShell.Management\Move-Item -LiteralPath $LiteralPath -Destination $Destination
  if ($script:B41Move -eq {nth}) {{ [Environment]::Exit(97) }}
}}
'''


def _move_fail_into_current(nth: int = 1) -> str:
    """只让「搬回 current」这一步失败，用于在恢复路径本身上再注入一次失败。

    nth=1 时同时命中正向的 staged→current；nth=2 时正向成功、恢复步骤失败。
    """
    return rf'''
$script:B41Current = Join-Path $InstallRoot "current"
$script:B41IntoCurrent = 0
function Move-Item {{
  [CmdletBinding()]
  param([Parameter(Mandatory=$true)][string]$LiteralPath, [Parameter(Mandatory=$true)][string]$Destination)
  if ("$Destination" -eq "$script:B41Current") {{
    $script:B41IntoCurrent++
    if ($script:B41IntoCurrent -eq {nth}) {{ throw "INJECTED Move-Item into current" }}
  }}
  Microsoft.PowerShell.Management\Move-Item -LiteralPath $LiteralPath -Destination $Destination
}}
'''


def _deny_move() -> str:
    """以真实 Access denied 错误形态注入权限不足。"""
    return r'''
function Move-Item {
  [CmdletBinding()]
  param([Parameter(Mandatory=$true)][string]$LiteralPath, [Parameter(Mandatory=$true)][string]$Destination)
  throw "Access to the path '$LiteralPath' is denied."
}
'''


def _deny_remove(target_expr: str) -> str:
    """让针对某个目录的清理步骤以 Access denied 失败。"""
    return rf'''
$script:B41Target = {target_expr}
function Remove-Item {{
  [CmdletBinding()]
  param([Parameter(Mandatory=$true)][string]$LiteralPath, [switch]$Recurse, [switch]$Force)
  if ("$LiteralPath" -like "*$script:B41Target*") {{ throw "Access to the path '$LiteralPath' is denied." }}
  $arguments = @{{ LiteralPath = $LiteralPath }}
  if ($Recurse) {{ $arguments.Recurse = $true }}
  if ($Force) {{ $arguments.Force = $true }}
  Microsoft.PowerShell.Management\Remove-Item @arguments
}}
'''


_ANCHOR_RECONCILE = "$crashedPhase = Get-SwapPhase"


def _inject_phase(script_text: str, value: str, action: str) -> str:
    """按「阶段值」注入阶段状态写入失败 / 写入后硬崩。

    做法是把脚本自身的 ``Set-SwapPhase`` 改名后注入一个同值代理，因此与阶段状态
    的具体写入实现无关（``Set-Content`` 或 .NET 原子写都适用）。代理必须落在脚本
    自己的函数定义之后，否则会被后定义的函数覆盖。
    """
    match = re.search(r"function Set-SwapPhase \{.*?\n\}", script_text, re.DOTALL)
    assert match, "生产 swap 脚本缺少 Set-SwapPhase 定义，注入方式需同步更新"
    real = match.group(0).replace("function Set-SwapPhase", "function Set-SwapPhaseReal", 1)
    trigger = ('throw "INJECTED phase write failure"'
               if action == "fail" else "[Environment]::Exit(97)")
    call = trigger if action == "fail" else f"Set-SwapPhaseReal -Phase $Phase\n  {trigger}"
    proxy = f'''
{real}

$script:B41TargetPhase = "{value}"
function Set-SwapPhase {{
  param([Parameter(Mandatory=$true)][string]$Phase)
  if ($Phase -ne $script:B41TargetPhase) {{ Set-SwapPhaseReal -Phase $Phase; return }}
  {call}
}}
'''
    assert _ANCHOR_RECONCILE in script_text, "生产 swap 脚本缺少 reconcile 锚点，注入方式需同步更新"
    return script_text.replace(
        _ANCHOR_RECONCILE, proxy.strip("\n") + "\n" + _ANCHOR_RECONCILE, 1
    )


class _ExclusiveFile:
    """以 dwShareMode=0 独占打开文件，模拟「文件被占用」。"""

    def __init__(self, path: Path) -> None:
        kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
        create_file = kernel32.CreateFileW
        create_file.restype = wintypes.HANDLE
        create_file.argtypes = [
            wintypes.LPCWSTR,
            wintypes.DWORD,
            wintypes.DWORD,
            ctypes.c_void_p,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.HANDLE,
        ]
        handle = create_file(str(path), 0x80000000, 0, None, 3, 0x80, None)
        if handle == ctypes.c_void_p(-1).value:
            raise OSError(ctypes.get_last_error(), f"无法独占打开 {path}")
        self._handle = handle
        self._close = kernel32.CloseHandle

    def close(self) -> None:
        if self._handle:
            self._close(self._handle)
            self._handle = None

    def __enter__(self) -> "_ExclusiveFile":
        return self

    def __exit__(self, *_exc: object) -> None:
        self.close()


def _script() -> str:
    return update_module._SWAP_SCRIPT


def _state_path(root: Path) -> Path:
    return root / update_module._SWAP_STATE_FILENAME


# --------------------------------------------------------------------------- #
# 复现：catch 无条件先删 current
# --------------------------------------------------------------------------- #


def test_update_move_current_to_previous_failure_keeps_runnable_current(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """247 行 Move-Item 失败且没有 previous 时，旧代码会删掉唯一的可运行版本。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)
    completed = _run_swap(_inject(_script(), _move_fail(1)), root, staged)

    assert completed.returncode != 0, "swap 失败必须返回非 0"
    runnable = _runnable_versions(root)
    assert runnable, f"失败后必须至少留一个可运行版本；实测 {runnable}；stderr={completed.stderr}"
    assert runnable.get("current") == "OLD", f"仍有效的 current 必须原样保留；实测 {runnable}"


def test_update_move_staged_into_current_failure_keeps_runnable_version(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """248 行失败（current 已搬走）时必须把完整旧版本搬回来。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)
    completed = _run_swap(_inject(_script(), _move_fail(2)), root, staged)

    assert completed.returncode != 0
    runnable = _runnable_versions(root)
    assert runnable.get("current") == "OLD", f"应恢复完整旧版本；实测 {runnable}；stderr={completed.stderr}"


def test_update_cleanup_failure_on_previous_keeps_current(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """246 行清理 previous 失败时，catch 不得删除仍有效的 current。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe, previous=True)
    prelude = _deny_remove('(Join-Path $InstallRoot "previous")')
    completed = _run_swap(_inject(_script(), prelude), root, staged)

    assert completed.returncode != 0
    runnable = _runnable_versions(root)
    assert runnable.get("current") == "OLD", f"清理步骤失败不得动 current；实测 {runnable}"
    assert _complete_version(root, "previous") == "OLDER", "清理失败不得把 previous 删成半截"


def test_occupied_previous_keeps_running_current(tmp_path: Path, fake_cli_exe: Path) -> None:
    """文件被真实独占占用（OS 级）时，清理失败不得连带删除 current。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe, previous=True)
    with _ExclusiveFile(root / "previous" / "data-watch.exe"):
        completed = _run_swap(_script(), root, staged)

    assert completed.returncode != 0
    runnable = _runnable_versions(root)
    assert runnable.get("current") == "OLD", f"被占用时不得删除 current；实测 {runnable}"


def test_permission_denied_leaves_current_untouched(tmp_path: Path, fake_cli_exe: Path) -> None:
    """注入式权限不足：非 0 退出，且不改变 current。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)
    completed = _run_swap(_inject(_script(), _deny_move()), root, staged)

    assert completed.returncode != 0
    runnable = _runnable_versions(root)
    assert runnable.get("current") == "OLD", f"权限不足时不得改变 current；实测 {runnable}"


def test_verification_failure_keeps_old_version_and_reports_failure(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """版本校验失败：不得把未验证的新版本留在 current，且必须非 0 退出。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)
    completed = _run_swap(_script(), root, staged, env={"DATAWATCH_FAKE_VERSION": "8.0.0"})

    assert completed.returncode != 0
    runnable = _runnable_versions(root)
    assert runnable.get("current") == "OLD", f"未通过校验的版本不得留在 current；实测 {runnable}"
    assert _state_path(root).read_text(encoding="ascii").strip().endswith(":failed")


def test_second_failure_in_recovery_path_still_keeps_runnable_version(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """在恢复路径本身上再注入一次失败（二次失败），仍必须留有可运行版本。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)
    script = _inject(_script(), _move_fail_into_current(2))
    completed = _run_swap(script, root, staged, env={"DATAWATCH_FAKE_VERSION": "8.0.0"})

    assert completed.returncode != 0
    runnable = _runnable_versions(root)
    assert runnable, f"恢复路径再失败也必须留有可运行版本；实测 {runnable}；stderr={completed.stderr}"

    # 崩溃重启：阶段状态仍在，下一次运行应能把版本搬回 current。
    restarted = _run_swap(_script(), root, staged, script_name="swap-restart.ps1")
    assert restarted.returncode != 0
    assert _runnable_versions(root).get("current") == "OLD"


# --------------------------------------------------------------------------- #
# 回滚路径
# --------------------------------------------------------------------------- #


def _seed_rollback(root: Path, exe: Path) -> Path:
    """current=OLD（正在运行、将被淘汰的版本）、previous=OLDER（回滚目标）。"""
    return _seed_root(root, exe, previous=True)


def test_rollback_move_current_to_failed_failure_does_not_destroy_versions(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """回滚第一步失败时，旧代码会直接删掉 current，导致被淘汰版本不可恢复。"""
    root = tmp_path / "root"
    staged = _seed_rollback(root, fake_cli_exe)
    completed = _run_swap(_inject(_script(), _move_fail(1)), root, staged, rollback=True)

    assert completed.returncode != 0
    markers = {
        marker
        for name in _VERSION_DIRS
        if (marker := _complete_version(root, name)) is not None
    }
    assert "OLD" in markers, f"被淘汰版本不得被静默删除；实测残留标记 {markers}"
    assert _runnable_versions(root), "回滚失败后仍必须存在可运行版本"


def test_rollback_move_previous_into_current_failure_keeps_runnable_version(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    root = tmp_path / "root"
    staged = _seed_rollback(root, fake_cli_exe)
    completed = _run_swap(_inject(_script(), _move_fail(2)), root, staged, rollback=True)

    assert completed.returncode != 0
    runnable = _runnable_versions(root)
    assert runnable.get("current") == "OLDER", f"应把回滚目标恢复到 current；实测 {runnable}"


def test_rollback_verification_failure_keeps_runnable_version(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    root = tmp_path / "root"
    staged = _seed_rollback(root, fake_cli_exe)
    completed = _run_swap(
        _script(), root, staged, rollback=True, env={"DATAWATCH_FAKE_VERSION": "8.0.0"}
    )

    assert completed.returncode != 0
    assert _runnable_versions(root), "回滚校验失败后仍必须存在可运行版本"


# --------------------------------------------------------------------------- #
# 崩溃后重启
# --------------------------------------------------------------------------- #


def test_crash_after_backup_is_recovered_on_restart(tmp_path: Path, fake_cli_exe: Path) -> None:
    """第一次移动完成后崩溃：重启必须能重新完成更新，而不是报失败并卡住。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)

    crashed = _run_swap(_inject(_script(), _move_crash_after(1)), root, staged)
    assert crashed.returncode == 97, f"崩溃注入未生效：{crashed.stderr}"

    restarted = _run_swap(_script(), root, staged, script_name="swap-restart.ps1")
    assert restarted.returncode == 0, f"重启后应恢复并完成更新；stderr={restarted.stderr}"
    assert _complete_version(root, "current") == "NEW"
    assert _complete_version(root, "previous") == "OLD", "旧版本必须仍在 previous 中保留"
    assert _state_path(root).read_text(encoding="ascii").strip() == "update:verified"


def test_crash_after_swap_is_recovered_on_restart(tmp_path: Path, fake_cli_exe: Path) -> None:
    """新版本已搬入但尚未校验时崩溃：重启不得丢掉那份完整旧版本。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)

    crashed = _run_swap(_inject(_script(), _move_crash_after(2)), root, staged)
    assert crashed.returncode == 97, f"崩溃注入未生效：{crashed.stderr}"
    assert _complete_version(root, "current") == "NEW"

    restarted = _run_swap(_script(), root, staged, script_name="swap-restart.ps1")
    assert restarted.returncode != 0, "staged 已被消耗，重启不可能校验成功"
    runnable = _runnable_versions(root)
    assert runnable.get("current") == "OLD", f"重启后必须找回完整旧版本；实测 {runnable}"


# --------------------------------------------------------------------------- #
# 正向路径不能被改坏
# --------------------------------------------------------------------------- #


def test_happy_path_update_swaps_and_verifies(tmp_path: Path, fake_cli_exe: Path) -> None:
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)
    completed = _run_swap(_script(), root, staged)

    assert completed.returncode == 0, f"成功路径必须仍然工作；stderr={completed.stderr}"
    assert _complete_version(root, "current") == "NEW"
    assert _complete_version(root, "previous") == "OLD"
    assert _state_path(root).read_text(encoding="ascii").strip() == "update:verified"


def test_happy_path_rollback_swaps_and_verifies(tmp_path: Path, fake_cli_exe: Path) -> None:
    root = tmp_path / "root"
    staged = _seed_rollback(root, fake_cli_exe)
    completed = _run_swap(_script(), root, staged, rollback=True)

    assert completed.returncode == 0, f"成功回滚必须仍然工作；stderr={completed.stderr}"
    assert _complete_version(root, "current") == "OLDER"
    assert _complete_version(root, "previous") == "OLD", "被淘汰版本应回收进 previous"


# --------------------------------------------------------------------------- #
# 文本级与命令层断言
# --------------------------------------------------------------------------- #


def test_swap_script_records_explicit_phase_state() -> None:
    script = _script()
    assert update_module._SWAP_STATE_FILENAME in script, "swap 脚本必须写显式阶段状态文件"
    for phase in ("prepare", "backed-up", "displaced", "swapped", "verified"):
        assert f":{phase}" in script, f"阶段状态缺少 {phase}"
    assert "$current -Destination $failed" in script, "恢复必须先搬开未验证版本而不是删除"


def test_apply_reports_verification_pending(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    """apply() 不得在 updater 还没跑完时就宣称升级成功。"""
    root = tmp_path / "root"
    _assert_temp_root(root)
    (root / "current").mkdir(parents=True)
    (root / "current" / "data-watch.exe").write_bytes(b"MZ")
    archive = tmp_path / "release.zip"
    with zipfile.ZipFile(archive, "w") as bundle:
        bundle.writestr("app/data-watch.exe", b"MZ")

    def fake_download(self: UpdateManager, **_kwargs: Any) -> dict[str, Any]:
        return {
            "currentVersion": "1.0.0",
            "latestVersion": "9.0.0",
            "updateAvailable": True,
            "channel": "stable",
            "platform": "windows-x64",
            "manifest": {},
            "downloadPath": str(archive),
            "downloaded": True,
        }

    launched: list[int] = []

    def fake_launch(**kwargs: Any) -> int:
        launched.append(kwargs["rollback"])
        return 4242

    monkeypatch.setattr(UpdateManager, "download", fake_download)
    monkeypatch.setattr(update_module, "_launch_swap", fake_launch)

    result = UpdateManager(object(), install_root=root).apply()

    assert launched == [False]
    assert result["updateStarted"] is True
    assert result["updateVerified"] is False, "更新是否成功此时未知，不得宣称已验证"
    assert result["verificationRequired"] is True
    assert result["statePath"] == str(root / update_module._SWAP_STATE_FILENAME)
    assert result["updaterProcessId"] == 4242


def test_swap_state_reports_only_verified_as_verified(tmp_path: Path) -> None:
    """独立验证入口：只有 :verified 才算升级成功，失败/中间状态都不算。"""
    root = tmp_path / "root"
    _assert_temp_root(root)
    root.mkdir()
    manager = UpdateManager(object(), install_root=root)
    assert manager.swap_state() == {
        "state": None,
        "updateVerified": False,
        "statePath": str(root / update_module._SWAP_STATE_FILENAME),
    }

    state_path = root / update_module._SWAP_STATE_FILENAME
    state_path.write_text("update:verified", encoding="ascii")
    assert manager.swap_state()["updateVerified"] is True
    state_path.write_text("update:swapped", encoding="ascii")
    assert manager.swap_state()["updateVerified"] is False
    state_path.write_text("update:failed", encoding="ascii")
    assert manager.swap_state()["updateVerified"] is False


# --------------------------------------------------------------------------- #
# 安全围栏（`隔离测试环境.md` §1.1：本机有真实安装 C:\ProgramData\DataWatch Edge）
# --------------------------------------------------------------------------- #


def test_safety_fence_rejects_non_temp_root() -> None:
    """证明围栏真的会拦，而不是空断言（故意用不存在的非临时路径，不接触任何真实目录）。"""
    outside = Path(os.environ.get("SystemRoot", r"C:\Windows")) / "System32" / "b41-fence-probe"
    with pytest.raises(AssertionError):
        _assert_temp_root(outside)
    assert _assert_temp_root(Path(tempfile.mkdtemp(prefix="b41-fence-"))) is not None


def test_installation_root_defaults_to_local_appdata_not_program_data(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """cli/update_manager.py 的默认安装根是每用户 LOCALAPPDATA，不是 ProgramData。"""
    monkeypatch.delenv("DATAWATCH_INSTALL_ROOT", raising=False)
    monkeypatch.setattr(update_module.sys, "frozen", False, raising=False)
    monkeypatch.setenv("LOCALAPPDATA", str(tmp_path))

    root = update_module._installation_root()

    assert root == tmp_path / "DataWatch" / "CLI"
    assert "ProgramData" not in str(root)
    _assert_temp_root(root)


# --------------------------------------------------------------------------- #
# R9 / R10 / R11（独立验收反例的补齐）
# --------------------------------------------------------------------------- #


def _entry_manager(
    root: Path, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> tuple[UpdateManager, list[bool]]:
    """构造一个下载与 updater 启动都被桩替换的 UpdateManager。

    只验证「CLI 入口是否真的走到前置校验之后」，不联网、不启动真实 updater。
    """
    archive = tmp_path / "release.zip"
    with zipfile.ZipFile(archive, "w") as bundle:
        bundle.writestr("app/data-watch.exe", b"MZ")

    def fake_download(self: UpdateManager, **_kwargs: Any) -> dict[str, Any]:
        return {
            "currentVersion": "1.0.0",
            "latestVersion": "9.0.0",
            "updateAvailable": True,
            "channel": "stable",
            "platform": "windows-x64",
            "manifest": {},
            "downloadPath": str(archive),
            "downloaded": True,
        }

    launched: list[bool] = []

    def fake_launch(**kwargs: Any) -> int:
        launched.append(kwargs["rollback"])
        return 4242

    monkeypatch.setattr(UpdateManager, "download", fake_download)
    monkeypatch.setattr(update_module, "_launch_swap", fake_launch)
    return UpdateManager(object(), install_root=root), launched


def test_pending_swap_phase_only_treats_non_terminal_states_as_pending(tmp_path: Path) -> None:
    """恢复触发条件：只有非终态阶段才算「有未完成的 swap」。"""
    root = tmp_path / "root"
    root.mkdir()
    _assert_temp_root(root)
    assert update_module._pending_swap_phase(root) is None

    path = _state_path(root)
    for terminal in ("update:verified", "update:failed", "rollback:failed", "update:recovered"):
        path.write_text(terminal, encoding="ascii")
        assert update_module._pending_swap_phase(root) is None, terminal
    for pending in ("update:prepare", "update:backed-up", "update:swapped", "rollback:displaced"):
        path.write_text(pending, encoding="ascii")
        assert update_module._pending_swap_phase(root) == pending, pending
    path.write_text("", encoding="ascii")
    assert update_module._pending_swap_phase(root) == "", "空阶段文件（写入被截断）也应按未完成处理"


def test_cli_apply_recovers_interrupted_swap_before_precondition_check(
    tmp_path: Path, fake_cli_exe: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """R11：崩于第一次 Move-Item 后 current 缺失时，apply() 必须先恢复再前置校验。

    改动前：`_require_windows_install` 直接抛 CliError，脚本顶部的 reconcile 在真实
    CLI 入口上不可达，只能靠人工/直接调 PowerShell 恢复。
    """
    variants = (
        ("crash-before-phase-write", _inject(_script(), _move_crash_after(1))),
        ("crash-after-phase-write", _inject_phase(_script(), "update:backed-up", "crash")),
    )
    for label, crashed_script in variants:
        root = tmp_path / label
        staged = _seed_root(root, fake_cli_exe)

        crashed = _run_swap(crashed_script, root, staged)
        assert crashed.returncode == 97, f"[{label}] 崩溃注入未生效：{crashed.stderr}"
        assert not (root / "current" / "data-watch.exe").is_file(), f"[{label}] current 应已缺失"
        assert _runnable_versions(root).get("previous") == "OLD", f"[{label}] 旧版本仍在 previous"

        manager, launched = _entry_manager(root, tmp_path, monkeypatch)
        result = manager.apply()

        assert launched == [False], f"[{label}] 恢复后必须真的走到 _launch_swap"
        assert result["updateStarted"] is True
        assert result["updateVerified"] is False, f"[{label}] 不得宣称已验证成功"
        assert _complete_version(root, "current") == "OLD", f"[{label}] current 必须回到完整旧版本"
        assert _runnable_versions(root).get("current") == "OLD", f"[{label}] current 必须真的可运行"
        assert _complete_version(root, "previous") is None, f"[{label}] previous 已被恢复消耗"
        state = _state_path(root).read_text(encoding="ascii").strip()
        assert state == "update:recovered", f"[{label}] 恢复应落 update:recovered；实测 {state!r}"


def test_cli_rollback_recovers_interrupted_swap_then_reports_missing_target(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """R11：rollback() 也必须先恢复；恢复后剩下的报错只能是「没有可回滚的上一版本」。"""
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)
    crashed = _run_swap(_inject(_script(), _move_crash_after(1)), root, staged)
    assert crashed.returncode == 97
    assert not (root / "current" / "data-watch.exe").is_file()

    manager = UpdateManager(object(), install_root=root)
    with pytest.raises(CliError) as excinfo:
        manager.rollback()

    assert "没有可回滚的上一版本" in str(excinfo.value)
    assert _runnable_versions(root).get("current") == "OLD", "报错之前必须已经恢复出可运行 current"
    assert _state_path(root).read_text(encoding="ascii").strip() == "update:recovered"


def test_cli_apply_still_rejects_root_without_current_and_without_pending_swap(tmp_path: Path) -> None:
    """反向控制：没有未完成 swap 时，前置校验不得被放宽。"""
    root = tmp_path / "root"
    root.mkdir()
    _assert_temp_root(root)
    manager = UpdateManager(object(), install_root=root)

    with pytest.raises(CliError):
        manager.apply()
    with pytest.raises(CliError):
        manager.rollback()


def test_cli_apply_does_not_fake_success_when_recovery_cannot_restore_current(
    tmp_path: Path,
) -> None:
    """恢复不了 current 时必须如实报错，不得报升级成功、也不得删掉任何东西。"""
    root = tmp_path / "root"
    root.mkdir()
    _assert_temp_root(root)
    _state_path(root).write_text("update:backed-up", encoding="ascii")

    manager = UpdateManager(object(), install_root=root)
    with pytest.raises(CliError):
        manager.apply()

    assert not (root / "current" / "data-watch.exe").is_file()
    assert _state_path(root).read_text(encoding="ascii").strip() == "update:backed-up"


def test_phase_write_failure_does_not_leave_unverified_version_in_current(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """R9：写 `update:swapped` 阶段状态失败时，current 不得留下从未通过自检的新版本。

    改动前：阶段值停在 `update:backed-up`，catch 只认 `update:swapped`，于是
    `current` 保留那份从未跑过版本自检的新版本（`failed` 里什么都没有）。
    """
    root = tmp_path / "root"
    staged = _seed_root(root, fake_cli_exe)
    completed = _run_swap(_inject_phase(_script(), "update:swapped", "fail"), root, staged)

    assert completed.returncode != 0, "阶段写入失败必须非 0 退出"
    assert _complete_version(root, "current") == "OLD", "未经自检的新版本不得留在 current"
    assert _complete_version(root, "failed") == "NEW", "未验证的新版本应停放到 failed 取证"
    assert _runnable_versions(root), "目录里必须仍有可运行版本"
    state = _state_path(root).read_text(encoding="ascii").strip()
    assert state == "update:failed", f"阶段值必须是完整有效的终态；实测 {state!r}"


def test_rollback_crash_restart_does_not_delete_displaced_version(
    tmp_path: Path, fake_cli_exe: Path
) -> None:
    """R10：rollback 崩于第 2 次移动后重启，不得把 failed 里的被淘汰版本永久删除。

    改动前：重启时 reconcile 先 `Remove-Item $failed`（永久删掉被淘汰的 OLD），
    随后 `previous -> current` 因 previous 已被上一次运行消耗而失败，最终 `current`
    也消失，只剩 `failed/OLDER`。
    """
    root = tmp_path / "root"
    _seed_rollback(root, fake_cli_exe)
    previous = root / "previous"  # 生产 rollback() 传给脚本的 -StagedApp 就是 previous

    crashed = _run_swap(_inject(_script(), _move_crash_after(2)), root, previous, rollback=True)
    assert crashed.returncode == 97, f"崩溃注入未生效：{crashed.stderr}"
    assert _complete_version(root, "current") == "OLDER"
    assert _complete_version(root, "failed") == "OLD"

    restarted = _run_swap(_script(), root, previous, rollback=True, script_name="swap-restart.ps1")

    assert restarted.returncode != 0, "previous 已被消耗，重启不可能完成回滚，也不得报成功"
    assert _complete_version(root, "current") == "OLDER", "重启后 current 仍须是可运行的完整版本"
    markers = {m for name in _VERSION_DIRS if (m := _complete_version(root, name)) is not None}
    assert "OLD" in markers, f"被淘汰的旧版本不得被永久删除；实测 markers={markers}"
    assert _runnable_versions(root), "目录里必须仍有可运行版本"

