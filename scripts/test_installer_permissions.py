"""Run the installer's data ACL commands against inherited temporary files."""
import os
from pathlib import Path
import re
import subprocess

import pytest


@pytest.mark.skipif(os.name != "nt", reason="Windows ACL validation")
def test_installer_preserves_effective_file_permissions(tmp_path):
    root = tmp_path / "data"
    settings = root / "writer" / "config" / "settings.toml"
    settings.parent.mkdir(parents=True)
    settings.write_text("synthetic = true", encoding="utf-8")
    source = (Path(__file__).resolve().parents[1] / "installer" / "DataWatchRole.iss").read_text(encoding="utf-8")
    body = source.split("procedure HardenDataPermissions", 1)[1].split("procedure RegisterRoleService", 1)[0]
    commands = re.findall(r"Exec\(\s*'icacls.exe',\s*(.*?)\s*,\s*''", body, re.S)
    assert len(commands) == 1
    helper = source.split("procedure RestoreInheritedDataPermissions", 1)[1].split("procedure HardenDataPermissions", 1)[0]
    child_commands = re.findall(r"Exec\(\s*'icacls.exe',\s*(.*?)\s*,\s*''", helper, re.S)
    assert len(child_commands) == 1
    for _ in range(2):  # upgrade/repeated execution must also preserve access
        for expression in commands:
            args = "".join(value.replace("''", "'") if not name else str(root)
                           for value, name in re.findall(r"'((?:[^']|'')*)'|\b(DataPath)\b", expression))
            result = subprocess.run(f'icacls.exe {args}', capture_output=True, text=True)
            assert result.returncode == 0, result.stdout + result.stderr
        for child in sorted(root.rglob("*"), key=lambda path: len(path.parts)):
            for expression in child_commands:
                args = "".join(value.replace("''", "'") if not name else str(child)
                               for value, name in re.findall(r"'((?:[^']|'')*)'|\b(EntryPath)\b", expression))
                result = subprocess.run(f'icacls.exe {args}', capture_output=True, text=True)
                assert result.returncode == 0, result.stdout + result.stderr
        assert settings.read_text(encoding="utf-8") == "synthetic = true"
        result = subprocess.run([
            "powershell.exe", "-NoProfile", "-Command",
            "$acl = [System.IO.File]::GetAccessControl($env:DATAWATCH_ACL_TEST_FILE); "
            "$acl.GetSecurityDescriptorSddlForm([System.Security.AccessControl.AccessControlSections]::Access)",
        ], capture_output=True, text=True, env={**os.environ, "DATAWATCH_ACL_TEST_FILE": str(settings)})
        assert result.returncode == 0, result.stderr
        assert ";;;SY)" in result.stdout
        assert ";;;BA)" in result.stdout
        assert ";;;BU)" in result.stdout
