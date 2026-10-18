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


def test_missing_key_id_fails(tmp_path: Path) -> None:
    junit = tmp_path / "junit.xml"
    # 空套件：符号链接这条关键用例不在里面。
    ElementTree(Element("testsuite", tests="0", failures="0", errors="0", skipped="0")).write(
        junit, encoding="utf-8", xml_declaration=True
    )
    env = os.environ.copy()
    env["PYTHONUTF8"] = "1"
    result = subprocess.run(
        [sys.executable, str(SCRIPT), str(junit), "--platform", "linux"],
        capture_output=True,
        text=True,
        encoding="utf-8",
        env=env,
        check=False,
    )
    assert result.returncode != 0
    assert f"linux: 关键用例缺失: {MISSING_ID}" in result.stderr
