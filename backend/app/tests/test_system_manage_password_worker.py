"""B20：新建用户、重置密码、启动初始化不得在事件循环里同步做 PBKDF2。"""
from __future__ import annotations

import ast
import asyncio
import threading
import time
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock

import pytest

from app.core.exceptions import ApiError
from app.core.password_worker import PasswordWorkerBusy
from app.models import SysUser
from app.schemas.auth import PasswordResetPayload, UserPayload
from app.services import auth_bootstrap
from app.services.system_manage import SystemManageService

_BACKEND_APP = Path(__file__).resolve().parents[1]
_PASSWORD_CALLS = frozenset({"hash_password", "verify_password"})
_EXCLUDED_FUNCTION = "run_password_op"


class _Session:
    def __init__(self) -> None:
        self.added: list[object] = []
        self.committed = 0

    async def scalar(self, _statement):
        return None

    def add(self, value: object) -> None:
        self.added.append(value)

    async def flush(self) -> None:
        for item in self.added:
            if getattr(item, "id", None) is None:
                item.id = 11

    async def commit(self) -> None:
        self.committed += 1


class _SeedSession:
    """对齐 test_auth_bootstrap：按用户名取出已有用户。"""

    def __init__(self, users: dict[str, SysUser] | None = None) -> None:
        self.users = users or {}
        self.added: list[SysUser] = []

    async def scalar(self, statement):
        name = statement.whereclause.right.value
        return self.users.get(name)

    def add(self, value: SysUser) -> None:
        self.added.append(value)

    async def flush(self) -> None:
        return None


def _user_payload() -> UserPayload:
    return UserPayload.from_dict({"userName": "alice", "userRoles": ["R_USER"]})


def _service(session: _Session | None = None) -> tuple[SystemManageService, _Session]:
    bound = session or _Session()
    service = SystemManageService(bound)
    service._resolve_role_ids = AsyncMock(return_value=[1])
    service._role_ids_include_super = AsyncMock(return_value=False)
    return service, bound


@pytest.mark.asyncio
async def test_create_and_reset_hash_through_password_worker(monkeypatch) -> None:
    seen: list[tuple[str, tuple[object, ...]]] = []

    async def _run(func, *args, **kwargs):
        seen.append((func.__name__, args))
        return f"hashed:{args[0]}"

    monkeypatch.setattr("app.services.system_manage.run_password_op", _run)
    monkeypatch.setattr("app.services.system_manage.generate_temporary_password", lambda: "Temp-1")

    service, session = _service()
    created = await service.create_user(
        _user_payload(),
        actor="admin",
        current_user_roles=["R_SUPER"],
    )

    assert seen == [("hash_password", ("Temp-1",))]
    assert session.added[0].password_hash == "hashed:Temp-1"
    assert session.committed == 1
    assert created["temporaryPassword"] == "Temp-1"
    assert created["mustChangePassword"] is True

    user = SimpleNamespace(
        id=3,
        password_hash="old-hash",
        must_change_password=False,
        token_version=2,
        update_by=None,
    )
    service._user_has_role = AsyncMock(return_value=False)
    service._get_user_or_raise = AsyncMock(return_value=user)
    reset = await service.reset_user_password(
        3,
        PasswordResetPayload.from_dict({}),
        actor="admin",
        current_user_roles=["R_SUPER"],
    )

    assert seen[1] == ("hash_password", ("Temp-1",))
    assert user.password_hash == "hashed:Temp-1"
    assert user.must_change_password is True
    assert user.token_version == 3
    assert reset["temporaryPassword"] == "Temp-1"
    assert session.committed == 2


@pytest.mark.asyncio
async def test_create_and_reset_reject_when_password_worker_busy(monkeypatch) -> None:
    async def _busy(*_args, **_kwargs):
        raise PasswordWorkerBusy()

    monkeypatch.setattr("app.services.system_manage.run_password_op", _busy)
    monkeypatch.setattr("app.services.system_manage.generate_temporary_password", lambda: "Temp-1")

    service, session = _service()
    with pytest.raises(ApiError) as created:
        await service.create_user(
            _user_payload(),
            actor="admin",
            current_user_roles=["R_SUPER"],
        )
    assert created.value.code == 503
    assert created.value.message == "认证服务繁忙，请稍后重试"
    assert session.added == []
    assert session.committed == 0

    user = SimpleNamespace(
        id=3,
        password_hash="old-hash",
        must_change_password=False,
        token_version=2,
        update_by=None,
    )
    service._user_has_role = AsyncMock(return_value=False)
    service._get_user_or_raise = AsyncMock(return_value=user)
    with pytest.raises(ApiError) as reset:
        await service.reset_user_password(
            3,
            PasswordResetPayload.from_dict({}),
            actor="admin",
            current_user_roles=["R_SUPER"],
        )
    assert reset.value.code == 503
    assert user.password_hash == "old-hash"
    assert user.token_version == 2
    assert session.committed == 0


@pytest.mark.asyncio
async def test_hash_yields_event_loop_within_50ms(monkeypatch) -> None:
    """阻塞型哈希在工作线程里睡眠时，事件循环仍能立刻跑完 asyncio.sleep(0)。"""
    entered = threading.Event()

    def _blocking_hash(_password: str) -> str:
        entered.set()
        time.sleep(0.2)
        return "hashed"

    monkeypatch.setattr("app.services.system_manage.hash_password", _blocking_hash)
    monkeypatch.setattr("app.services.system_manage.generate_temporary_password", lambda: "Temp-1")

    service, _session = _service()
    task = asyncio.create_task(
        service.create_user(
            _user_payload(),
            actor="admin",
            current_user_roles=["R_SUPER"],
        )
    )
    for _ in range(200):
        if entered.is_set():
            break
        await asyncio.sleep(0)
    assert entered.is_set()
    started = time.perf_counter()
    await asyncio.sleep(0)
    elapsed = time.perf_counter() - started
    assert elapsed < 0.05
    assert not task.done()
    created = await task
    assert created["temporaryPassword"] == "Temp-1"


@pytest.mark.asyncio
async def test_seed_users_hashes_and_verifies_through_password_worker(monkeypatch) -> None:
    seen: list[tuple[str, tuple[object, ...]]] = []

    async def _run(func, *args, **kwargs):
        seen.append((func.__name__, args))
        if func.__name__ == "verify_password":
            return args[0] == "123456"
        return f"hashed:{args[0]}"

    monkeypatch.setattr(auth_bootstrap, "run_password_op", _run)
    monkeypatch.setattr(auth_bootstrap, "generate_temporary_password", lambda: "Temp-Dev")

    created = await auth_bootstrap._seed_users(_SeedSession(), app_env="development")
    assert [name for name, _args in seen] == ["hash_password", "hash_password", "hash_password"]
    assert created[0]["temporaryPassword"] == "Temp-Dev"

    seen.clear()
    existing = SysUser(user_name="Super", password_hash="legacy-hash", status="2", deleted=False)
    await auth_bootstrap._seed_users(_SeedSession({"Super": existing}), app_env="production")
    assert seen == [("verify_password", ("123456", "legacy-hash"))]
    assert existing.must_change_password is True


def _callee_name(node: ast.AST) -> str | None:
    if isinstance(node, ast.Name):
        return node.id
    if isinstance(node, ast.Attribute):
        return node.attr
    return None


def _iter_direct_password_calls(node: ast.AST):
    """收集直接调用，但跳过 run_password_op 函数体内部。"""
    if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)) and node.name == _EXCLUDED_FUNCTION:
        return
    if isinstance(node, ast.Call) and _callee_name(node.func) in _PASSWORD_CALLS:
        yield node
    for child in ast.iter_child_nodes(node):
        yield from _iter_direct_password_calls(child)


def _async_password_call_sites(source: str, filename: str) -> list[str]:
    tree = ast.parse(source, filename=filename)
    sites: list[str] = []
    for fn in ast.walk(tree):
        if not isinstance(fn, ast.AsyncFunctionDef) or fn.name == _EXCLUDED_FUNCTION:
            continue
        for call in _iter_direct_password_calls(fn):
            sites.append(f"{filename}:{call.lineno} {fn.name} -> {_callee_name(call.func)}")
    return sites


def test_async_functions_do_not_call_password_hash_directly() -> None:
    """扫描 backend/app 里全部 async def。测试代码不在服务事件循环上，不纳入。"""
    violations: list[str] = []
    for path in sorted(_BACKEND_APP.rglob("*.py")):
        if "tests" in path.parts or "__pycache__" in path.parts:
            continue
        source = path.read_text(encoding="utf-8-sig")
        violations.extend(_async_password_call_sites(source, str(path)))
    assert violations == []


def test_password_call_scanner_ignores_run_password_op_body() -> None:
    direct = "async def create_user():\n    hash_password('x')\n"
    delegated = (
        "async def login():\n"
        "    await run_password_op(verify_password, 'a', 'b')\n"
        "async def run_password_op(func, *args):\n"
        "    return hash_password(*args)\n"
        "    verify_password(*args)\n"
    )
    assert _async_password_call_sites(direct, "<direct>")
    assert _async_password_call_sites(delegated, "<delegated>") == []
