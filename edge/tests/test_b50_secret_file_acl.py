"""B50/S3 回归：秘密文件（service token / IPC token）的权限必须真正形成白名单。

背景（本轮以隔离目录的真实 icacls 实验取证，而非照抄审查结论）：
- `icacls <file> /inheritance:r /grant:r <544:F> <18:F>` **只能**删除**继承**的 ACE、替换**所列
  SID** 的显式 ACE；文件上若已存在**显式** `BUILTIN\\Users:(RX)`，它**会存活**。实验证据：
    C1 显式加 Users:(RX) → C2 旧形态后仍显示 `BUILTIN\\Users:(RX)`（无 `(I)`）→ C3 加 `/remove:g`
    后被移除。
- `ensure_token_file()` 过去在"文件已存在且未轮换"时**直接返回**，不做任何加固 ⇒ 覆盖升级后旧
  ACL 永久保留。

因此本回归锁定两件事：
1. `_restrict_token_file_acl()` 必须**回收**广域 SID（Users / Everyone / Authenticated Users），
   对**显式** ACE 也生效，且可重复执行（幂等）；
2. `ensure_token_file()` 对**既有**文件也要收敛 ACL（进程内只做一次）。

注意：本测试断言的是**文件上的 ACL**（确定且可复现的代理证据），**不等于**"已用普通用户登录实测
无法读取"——后者需要独立账户，属目标机验收项。
"""

from __future__ import annotations

import os
import re
import subprocess
import sys
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
for _candidate in (REPO_ROOT, REPO_ROOT / "collector"):
    if str(_candidate) not in sys.path:
        sys.path.insert(0, str(_candidate))

from edge.app import storage_service  # noqa: E402

pytestmark = pytest.mark.skipif(os.name != "nt", reason="ACL 语义仅在 Windows 上可验证")

# 广域 SID 与其显示名：这些主体一旦能读，普通本机用户即可读取秘密文件。
_BROAD = {
    "users",
    "everyone",
    "authenticated users",
    "s-1-5-32-545",
    "s-1-1-0",
    "s-1-5-11",
}


def _acl_principals(path: Path) -> list[str]:
    """返回 icacls 输出中的主体名（`:( 权限)` 之前那段）的小写形式。

    注意不能简单按第一个冒号切分——首行形如 `C:\\...\\file PRINCIPAL:(RX)`，第一个冒号是**盘符**。
    这里用正则要求 `:(权限)` 形态，从而跳过盘符并正确抓到首行上的 ACE。
    """
    result = subprocess.run(
        ["icacls", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace"
    )
    principals: list[str] = []
    for raw in (result.stdout or "").splitlines():
        for match in re.finditer(r"(.+?):\(([A-Za-z, ]*)\)", raw.strip()):
            principals.append(match.group(1).strip().lower())
    return principals


def _has_broad_grant(path: Path) -> bool:
    """是否存在广域主体的 ACE（取主体的最后一段做精确比对，避免误判 *SandboxUsers*）。"""
    for principal in _acl_principals(path):
        leaf = principal.rsplit("\\", 1)[-1]
        if leaf in _BROAD or principal in _BROAD:
            return True
    return False


def _icacls(*arguments: str) -> int:
    return subprocess.run(
        ["icacls", *arguments], capture_output=True, text=True, encoding="utf-8", errors="replace"
    ).returncode


@pytest.fixture()
def token_file(tmp_path: Path) -> Path:
    path = tmp_path / "secret" / "storage-ipc-tokens.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text('{"version":1,"tokens":{"dummy":"DUMMY"}}', encoding="utf-8")
    return path


def test_old_form_would_leave_explicit_broad_ace(token_file: Path) -> None:
    """判别力校验：复现"显式广域 ACE 在旧形态下存活"，证明加固并非空转。"""
    assert _icacls(str(token_file), "/grant", "*S-1-5-32-545:RX") == 0
    assert _has_broad_grant(token_file), "前置条件：显式 Users 授权应已存在"

    # 旧形态（本轮修复前的实现）
    assert _icacls(
        str(token_file), "/inheritance:r", "/grant:r", "*S-1-5-32-544:F", "*S-1-5-18:F"
    ) == 0

    assert _has_broad_grant(token_file), "旧形态本应留下显式 Users 授权（判别力）"


def test_restrict_token_file_acl_removes_explicit_broad_ace(token_file: Path) -> None:
    """修复后：显式广域 ACE 必须被回收。"""
    assert _icacls(str(token_file), "/grant", "*S-1-5-32-545:RX") == 0
    assert _has_broad_grant(token_file)

    storage_service._restrict_token_file_acl(token_file)

    assert not _has_broad_grant(token_file), f"仍存在广域授权: {_acl_principals(token_file)}"
    # 合法的服务运行能力保留：SYSTEM 与 Administrators 必须在
    principals = " | ".join(_acl_principals(token_file))
    assert "system" in principals and "administrators" in principals, principals


def test_restrict_token_file_acl_is_idempotent(token_file: Path) -> None:
    storage_service._restrict_token_file_acl(token_file)
    first = sorted(_acl_principals(token_file))
    storage_service._restrict_token_file_acl(token_file)
    assert sorted(_acl_principals(token_file)) == first


def test_ensure_token_file_hardens_existing_file(
    token_file: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """既有文件路径（升级场景）也必须收敛 ACL，且每路径只加固一次。"""
    assert _icacls(str(token_file), "/grant", "*S-1-5-32-545:RX") == 0
    assert _has_broad_grant(token_file)
    storage_service._TOKEN_ACL_HARDENED.discard(str(token_file))

    calls: list[Path] = []
    real = storage_service._restrict_token_file_acl

    def _counting(path: Path) -> None:
        calls.append(Path(path))
        real(path)

    monkeypatch.setattr(storage_service, "_restrict_token_file_acl", _counting)

    returned = storage_service.ensure_token_file(token_file)  # 已存在、不轮换
    assert returned == token_file
    assert calls == [token_file], "既有文件应加固一次"
    assert not _has_broad_grant(token_file)

    storage_service.ensure_token_file(token_file)
    assert calls == [token_file], "同进程内不应重复加固"


def test_ensure_token_file_creates_and_hardens_new_file(tmp_path: Path) -> None:
    storage_service._TOKEN_ACL_HARDENED.clear()
    path = tmp_path / "new" / "storage-ipc-tokens.json"

    assert storage_service.ensure_token_file(path) == path

    assert path.is_file()
    assert not _has_broad_grant(path)
    principals = " | ".join(_acl_principals(path))
    assert "system" in principals and "administrators" in principals, principals

