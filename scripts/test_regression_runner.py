"""Exercise the real runner loop without running repository tests."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

import pytest


@pytest.mark.skipif(os.name != "nt", reason="PowerShell runner")
@pytest.mark.parametrize("runner_result", ["pass", "fail", "missing"])
@pytest.mark.parametrize("nested", [False, True])
def test_runner_launch_result_is_not_stale(tmp_path, runner_result, nested):
    source = (Path(__file__).with_name("regression.ps1")).read_text(encoding="utf-8-sig")
    loop = source.split("foreach ($name in $Modules) {", 1)[1].split("$summary = [ordered]@{", 1)[0]
    missing = runner_result == "missing"
    exe = "datawatch-nonexistent-test-runner" if missing else sys.executable
    # Values travel as JSON data, never interpolated into shell source.
    spec_path = tmp_path / "spec.json"
    spec_path.write_text(json.dumps({"Exe": exe, "WorkDir": str(tmp_path), "PyPath": None,
                                    "Args": ["-c", f"raise SystemExit({int(runner_result == 'fail')})"]}), encoding="utf-8")
    script = tmp_path / "runner.ps1"
    script.write_text(
        "$ErrorActionPreference='Continue'\n$LASTEXITCODE=0\n"
        "$Modules=@('probe'); $results=@(); $failed=@(); $runDir=$env:DW_RUN_DIR\n"
        "$spec=Get-Content -LiteralPath $env:DW_SPEC -Raw | ConvertFrom-Json\n"
        "$moduleSpec=@{probe=$spec}\nforeach ($name in $Modules) {" + loop +
        "$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runDir 'result.json')\n"
        "if ($failed.Count -gt 0) { exit 1 }; exit 0\n", encoding="utf-8-sig")
    entry = script
    if nested:
        entry = tmp_path / "wrapper.ps1"
        entry.write_text("& $env:DW_RUN_SCRIPT\nexit $LASTEXITCODE\n", encoding="utf-8-sig")
    result = subprocess.run([shutil.which("pwsh") or "powershell.exe", "-NoProfile", "-File", str(entry)],
                            env={**os.environ, "DW_SPEC": str(spec_path), "DW_RUN_DIR": str(tmp_path),
                                 "DW_RUN_SCRIPT": str(script)},
                            capture_output=True, text=True)
    record = json.loads((tmp_path / "result.json").read_text(encoding="utf-8-sig"))
    assert record["ok"] is (runner_result == "pass")
    assert (result.returncode == 0) is (runner_result == "pass")
    if missing:
        assert exe in (tmp_path / "probe.log").read_text(encoding="utf-8-sig")
