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


def test_missing_roles_are_added_without_dropping_existing_ones(
    token_file: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """补角色只增不删：已有角色（含本进程不认识的）必须原样保留。"""
    token_file.write_text(
        '{"version":1,"tokens":{"dummy":"DUMMY","legacy":"LEGACY"}}', encoding="utf-8"
    )
    storage_service._TOKEN_ACL_HARDENED.clear()

    hardened: list[Path] = []
    monkeypatch.setattr(
        storage_service, "_restrict_token_file_acl", lambda path: hardened.append(Path(path))
    )

    storage_service.ensure_token_file(token_file)

    tokens = storage_service.read_role_tokens(token_file)
    assert tokens["dummy"] == "DUMMY", "已有角色不应被轮换"
    assert tokens["legacy"] == "LEGACY", "未知角色不应被丢弃"
    for role in storage_service.IPC_ROLE_ACTIONS:
        assert tokens[role], f"缺失角色 {role} 应被补上"


def test_rewritten_token_file_is_hardened_afterwards(
    token_file: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """补角色会重写文件，重写后的最终文件必须重新加固且只加固一次。"""
    assert _icacls(str(token_file), "/grant", "*S-1-5-32-545:RX") == 0
    assert _has_broad_grant(token_file)
    storage_service._TOKEN_ACL_HARDENED.clear()

    order: list[str] = []
    real_harden = storage_service._restrict_token_file_acl

    def _tracking(path: Path) -> None:
        order.append(Path(path).read_text(encoding="utf-8"))
        real_harden(path)

    monkeypatch.setattr(storage_service, "_restrict_token_file_acl", _tracking)

    storage_service.ensure_token_file(token_file)

    assert len(order) == 1, f"应恰好加固一次，实际 {len(order)} 次"
    # 加固时看到的文件内容必须已经包含补上的角色，否则说明加固发生在重写之前。
    for role in storage_service.IPC_ROLE_ACTIONS:
        assert role in order[0], f"加固发生在补角色之前：{role} 不在被加固的内容里"
    assert not _has_broad_grant(token_file)


def test_rewrite_invalidates_the_hardened_marker(
    token_file: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    """文件被重写过就必须重新加固，哪怕该路径此前已加固过（标记已存在）。

    这是"重写会丢掉 ACL"的核心防护：只补不清标记，重写后的文件就永久失去白名单。
    """
    assert _icacls(str(token_file), "/grant", "*S-1-5-32-545:RX") == 0
    assert _has_broad_grant(token_file)

    storage_service._TOKEN_ACL_HARDENED.clear()
    # 模拟"本进程此前已经加固过这个路径"：补角色仍会重写文件，因此必须重新加固。
    storage_service._TOKEN_ACL_HARDENED.add(str(token_file))

    calls: list[Path] = []
    monkeypatch.setattr(
        storage_service, "_restrict_token_file_acl", lambda path: calls.append(Path(path))
    )

    storage_service.ensure_token_file(token_file)

    assert calls == [token_file], "重写后必须重新加固，旧标记不能让它跳过"


def test_ensure_token_file_creates_and_hardens_new_file(tmp_path: Path) -> None:
    storage_service._TOKEN_ACL_HARDENED.clear()
    path = tmp_path / "new" / "storage-ipc-tokens.json"

    assert storage_service.ensure_token_file(path) == path

    assert path.is_file()
    assert not _has_broad_grant(path)
    principals = " | ".join(_acl_principals(path))
    assert "system" in principals and "administrators" in principals, principals

