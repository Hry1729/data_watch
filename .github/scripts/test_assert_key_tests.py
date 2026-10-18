"""负向自检：junit 里缺关键 node id 时，断言脚本必须失败并点名该 id。"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path
from xml.etree.ElementTree import Element, ElementTree

SCRIPT = Path(__file__).with_name("assert_key_tests.py")
MISSING_ID = (
    "edge/tests/test_outbox_b6_dead_recovery.py::"
    "test_export_path_stays_inside_dead_exports_and_refuses_existing_files"
)


def _empty_junit(tmp_path: Path) -> Path:
    junit = tmp_path / "junit.xml"
    # 空套件：符号链接这条关键用例不在里面。
    ElementTree(Element("testsuite", tests="0", failures="0", errors="0", skipped="0")).write(
        junit, encoding="utf-8", xml_declaration=True
    )
    return junit


def _run(junit: Path, env: dict[str, str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        [sys.executable, str(SCRIPT), str(junit), "--platform", "linux"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=env,
        check=False,
    )


def test_missing_key_id_fails(tmp_path: Path) -> None:
    env = os.environ.copy()
    env["PYTHONUTF8"] = "1"
    result = _run(_empty_junit(tmp_path), env)
    assert result.returncode != 0
    assert f"linux: 关键用例缺失: {MISSING_ID}" in result.stderr


def test_missing_key_id_fails_when_stdio_is_legacy_codepage(tmp_path: Path) -> None:
    """Windows CI 的默认编码是 cp1252。中文失败行仍要写出来，不能 UnicodeEncodeError。"""
    env = os.environ.copy()
    env["PYTHONUTF8"] = "0"
    env["PYTHONIOENCODING"] = "cp1252"
    result = _run(_empty_junit(tmp_path), env)
    assert result.returncode != 0
    assert "UnicodeEncodeError" not in (result.stderr or "")
    assert "UnicodeEncodeError" not in (result.stdout or "")
    assert f"linux: 关键用例缺失: {MISSING_ID}" in result.stderr
