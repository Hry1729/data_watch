#!/usr/bin/env python3
"""从 edge 的 junit XML 断言关键用例已经执行，而不是核对跳过总数。

pytest 默认 junit_family=xunit2，testcase 只有 classname 和 name，没有 file。
下面的 node id 会按 pytest 的规则折成 (classname, name) 再去对。

判定（读测试文件里的 skip 标记，不靠猜测）：

- ``test_outbox_b6_dead_recovery.py`` 里只有
  ``test_export_path_stays_inside_dead_exports_and_refuses_existing_files``
  会调用 ``_require_symlink_support``。CI 设置了
  ``DATAWATCH_REQUIRE_SYMLINK_TESTS=1``，这条在 Linux 和 Windows 上都必须出现且不能 skip。
- ``test_b50_secret_file_acl.py`` 第 37 行是模块级
  ``pytestmark = pytest.mark.skipif(os.name != "nt", ...)``。
  文件里没有按用例拆开的 skip，也没有未打该标记的测试函数，
  因此 8 个用例全部只要求在 Windows 上执行。Linux 上它们按设计跳过，不在本检查里。
"""

from __future__ import annotations

import argparse
import os
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

# 两边都必须出现在 junit 里，且不能 skip / failure / error。
LINUX_IDS = [
    "edge/tests/test_outbox_b6_dead_recovery.py::test_export_path_stays_inside_dead_exports_and_refuses_existing_files",
]

# Windows 在符号链接用例之外，还要真跑 B50 的全部 8 个 Windows-only 用例。
WINDOWS_IDS = [
    "edge/tests/test_outbox_b6_dead_recovery.py::test_export_path_stays_inside_dead_exports_and_refuses_existing_files",
    "edge/tests/test_b50_secret_file_acl.py::test_old_form_would_leave_explicit_broad_ace",
    "edge/tests/test_b50_secret_file_acl.py::test_restrict_token_file_acl_removes_explicit_broad_ace",
    "edge/tests/test_b50_secret_file_acl.py::test_restrict_token_file_acl_is_idempotent",
    "edge/tests/test_b50_secret_file_acl.py::test_ensure_token_file_hardens_existing_file",
    "edge/tests/test_b50_secret_file_acl.py::test_missing_roles_are_added_without_dropping_existing_ones",
    "edge/tests/test_b50_secret_file_acl.py::test_rewritten_token_file_is_hardened_afterwards",
    "edge/tests/test_b50_secret_file_acl.py::test_rewrite_invalidates_the_hardened_marker",
    "edge/tests/test_b50_secret_file_acl.py::test_ensure_token_file_creates_and_hardens_new_file",
]

EXPECTED_IDS = {
    "linux": LINUX_IDS,
    "windows": WINDOWS_IDS,
}


def platform_name() -> str:
    return "windows" if os.name == "nt" else "linux"


def _use_utf8_stdio() -> None:
    """Windows 控制台默认编码写不出中文。失败信息里有用例 id，必须能打出来。"""
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is None:
            continue
        try:
            reconfigure(encoding="utf-8", errors="backslashreplace")
        except (OSError, ValueError):
            continue


def junit_identity(nodeid: str) -> tuple[str, str]:
    """把 pytest node id 折成 junit 的 (classname, name)，与 mangle_test_address 一致。"""
    path, bracket, params = nodeid.partition("[")
    parts = path.replace("\\", "/").split("::")
    module = parts[0].replace("/", ".")
    if module.endswith(".py"):
        module = module[: -len(".py")]
    classname = ".".join([module, *parts[1:-1]])
    return classname, parts[-1] + bracket + params


def _local(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def _case_problem(case: ET.Element) -> tuple[str, str] | None:
    """返回 (问题类型, 简短原因)。通过则返回 None。失败/出错优先于跳过。"""
    failure = None
    skipped = None
    for child in list(case):
        tag = _local(child.tag)
        message = (child.attrib.get("message") or "").strip().splitlines()
        brief = message[0] if message else ""
        if tag in {"failure", "error"} and failure is None:
            failure = (tag, brief)
        elif tag == "skipped" and skipped is None:
            skipped = brief
    if failure is not None:
        tag, brief = failure
        label = "失败" if tag == "failure" else "出错"
        detail = f"{label}: {brief}" if brief else label
        return label, detail
    if skipped is not None:
        detail = f"被跳过: {skipped}" if skipped else "被跳过"
        return "被跳过", detail
    return None


def suite_failure_count(root: ET.Element) -> tuple[int, int, int]:
    """沿用原先对 testsuite 属性的汇总：tests、skipped、failures+errors。"""
    suites = [root] if _local(root.tag) == "testsuite" else [
        child for child in list(root) if _local(child.tag) == "testsuite"
    ]
    tests = sum(int(suite.attrib.get("tests") or 0) for suite in suites)
    skipped = sum(int(suite.attrib.get("skipped") or 0) for suite in suites)
    failed = sum(
        int(suite.attrib.get("failures") or 0) + int(suite.attrib.get("errors") or 0)
        for suite in suites
    )
    return tests, skipped, failed


def check_junit(path: Path, platform: str) -> list[str]:
    expected = EXPECTED_IDS[platform]
    root = ET.parse(path).getroot()
    found: dict[tuple[str, str], tuple[str, str] | None] = {}
    for case in root.iter():
        if _local(case.tag) != "testcase":
            continue
        key = (case.attrib.get("classname") or "", case.attrib.get("name") or "")
        problem = _case_problem(case)
        previous = found.get(key)
        # 同一条用例出现多次时，留下更严重的那次。
        if key not in found or (previous is None and problem is not None):
            found[key] = problem

    problems: list[str] = []
    for nodeid in expected:
        key = junit_identity(nodeid)
        if key not in found:
            problems.append(f"{platform}: 关键用例缺失: {nodeid}")
            continue
        problem = found[key]
        if problem is not None:
            _label, detail = problem
            problems.append(f"{platform}: 关键用例{detail}: {nodeid}")

    _tests, _skipped, failed = suite_failure_count(root)
    if failed != 0:
        problems.append(f"{platform}: {failed} 个用例失败")
    return problems


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="断言 junit 中的关键 edge 用例已执行且未被跳过")
    parser.add_argument("junit", type=Path)
    parser.add_argument("--platform", choices=tuple(EXPECTED_IDS), default=None)
    args = parser.parse_args(argv)
    _use_utf8_stdio()
    platform = args.platform or platform_name()
    expected = EXPECTED_IDS[platform]
    print(f"platform={platform}")
    print(f"断言 {len(expected)} 个关键用例:")
    for nodeid in expected:
        print(f"  - {nodeid}")

    if not args.junit.is_file():
        print(f"{platform}: 找不到 junit 文件: {args.junit}", file=sys.stderr)
        return 1

    root = ET.parse(args.junit).getroot()
    tests, skipped, failed = suite_failure_count(root)
    print(f"tests={tests} skipped={skipped} failures={failed}")

    problems = check_junit(args.junit, platform)
    if problems:
        for line in problems:
            print(line, file=sys.stderr)
        return 1
    print("关键用例均已执行且未跳过")
    return 0


if __name__ == "__main__":
    sys.exit(main())
