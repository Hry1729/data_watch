"""B15：已存在 Agent 配置可保留空 jwt_secret —— 复现与回归。

独立文件（不改动 tests/conftest.py）。全部用例只用临时目录 + 本机纯函数，
不连接现场 Redis/MySQL/TDengine，不使用任何真实凭据。
"""

import secrets
import threading
from datetime import datetime, timedelta, timezone
from pathlib import Path

import pytest
import toml
from fastapi import HTTPException
from jose import JWTError, jwt

from app.core import config
from app.core.config import load_config
from app.core.deps import get_current_admin
from app.core.security import (
    JWT_AUDIENCE,
    JWT_ISSUER,
    create_access_token,
    verify_token,
)

# 弱/空密钥哨兵值（全部为测试专用假值，不是任何真实凭据）。
# 31 字符 = 策略下限（32）减一，用于锁定「弱值不得启动认证服务」。
WEAK_SECRETS = ["", " ", "changeme", "test-secret", "0" * 31]
# 伪造闸门（签发/校验路径）只拦空值；长度下限由配置加载强制执行。
FORGERY_SECRETS = ["", " "]
STRONG_SECRET = secrets.token_hex(32)


@pytest.fixture(autouse=True)
def _isolated_data_root(tmp_path, monkeypatch):
    """安全守卫：B15 全部夹具必须落在临时目录。

    本机存在真实安装 `C:\\ProgramData\\DataWatch Edge`（含真实 config.toml 与
    agent_service_token.txt）。这里显式把 DATAWATCH_DATA_ROOT 与 CONFIG_PATH 都
    钉到 pytest tmp_path，并在用例结束后断言配置路径没有越界——因此
    `_save_config_unlocked`（只写 CONFIG_PATH 同目录）不可能写到真实安装目录。
    """
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.delenv("DATAWATCH_INSTALL_ROOT", raising=False)
    monkeypatch.setattr(config, "CONFIG_PATH", tmp_path / "config.toml")
    assert config.data_root() == tmp_path

    yield

    resolved = Path(config.CONFIG_PATH).resolve()
    assert tmp_path.resolve() in resolved.parents, f"配置路径越界: {resolved}"


def _write_config(path, **agent_fields) -> str:
    agent = {
        "host": "127.0.0.1",
        "port": 7070,
        "jwt_expire_hours": 8,
        "log_level": "INFO",
    }
    agent.update(agent_fields)
    text = toml.dumps({"agent": agent, "redis": {"host": "127.0.0.1", "port": 6379}})
    path.write_text(text, encoding="utf-8")
    return text


def _use_config_path(monkeypatch, path) -> None:
    monkeypatch.setattr(config, "CONFIG_PATH", path)


def _forge(secret: str, roles: list[str] | None = None) -> str:
    """直接用 jose 造 token：模拟攻击者掌握空密钥后的伪造能力。"""
    payload = {
        "sub": "attacker",
        "exp": datetime.now(timezone.utc) + timedelta(hours=1),
        "iss": JWT_ISSUER,
        "aud": JWT_AUDIENCE,
    }
    if roles is not None:
        payload["roles"] = roles
    return jwt.encode(payload, secret, algorithm="HS256")


# ── 验收 2：空密钥绝不用于签发/校验；存量空占位符自愈 ─────────────────────────


@pytest.mark.parametrize("placeholder", ["", " ", "\t", "   "])
def test_template_placeholder_secret_is_migrated_after_bootstrap(
    tmp_path, monkeypatch, placeholder
):
    """模板占位符空密钥 + bootstrap 已完成 → 必须自愈，不得拒绝启动。

    2026-06-02 ~ 2026-10-03 的 config.toml.example 写死
    `jwt_secret = ""  # 首次启动时自动随机生成`，而那段自动生成 2026-10-03（B15）
    才落地。该窗口内按文档流程装机的节点，配置里就是这个空占位符 +
    bootstrap_done=true；按篡改拒绝会让它们升级即砖机（现场
    `C:\\ProgramData\\DataWatch` 正是此形态，2026-10-08 实测 agent 退出码 1）。
    """
    path = tmp_path / "config.toml"
    _write_config(path, jwt_secret=placeholder, bootstrap_done=True)
    _use_config_path(monkeypatch, path)

    generated = load_config()["agent"]["jwt_secret"]
    assert isinstance(generated, str) and len(generated) == 64
    assert toml.load(path)["agent"]["jwt_secret"] == generated

    # 补写一次即稳定，不得每次加载都轮换
    assert load_config()["agent"]["jwt_secret"] == generated
    # 迁移不得动 bootstrap 状态
    assert toml.load(path)["agent"]["bootstrap_done"] is True


def test_forged_token_with_empty_secret_is_rejected_by_admin_dependency(monkeypatch):
    """服务不接受空密钥伪造的 token：依赖入口必须 401。"""
    monkeypatch.setattr(
        "app.core.deps.load_config", lambda: {"agent": {"jwt_secret": ""}}
    )
    forged = _forge("", roles=["R_SUPER"])

    with pytest.raises(HTTPException) as exc_info:
        get_current_admin(f"Bearer {forged}")
    assert exc_info.value.status_code == 401


@pytest.mark.parametrize("secret", FORGERY_SECRETS)
def test_empty_secret_is_rejected_by_token_verification(secret):
    with pytest.raises(JWTError):
        verify_token(_forge(secret), secret)


@pytest.mark.parametrize("secret", FORGERY_SECRETS)
def test_empty_secret_is_rejected_by_token_creation(secret):
    with pytest.raises(JWTError):
        create_access_token("admin", secret, 8, roles=["R_ADMIN"])


@pytest.mark.parametrize("secret", WEAK_SECRETS)
def test_weak_secret_is_rejected_by_policy_helper(secret):
    from app.core.security import validate_jwt_secret

    with pytest.raises(ValueError):
        validate_jwt_secret(secret)


def test_weak_nonempty_secret_policy_minimum_is_enforced_at_config_load(tmp_path, monkeypatch):
    """长度下限（32）在配置加载处强制：弱但非空的存量配置同样不得启动认证服务。"""
    from app.core.security import JWT_SECRET_MIN_LENGTH, validate_jwt_secret

    assert JWT_SECRET_MIN_LENGTH == 32
    assert validate_jwt_secret(STRONG_SECRET) == STRONG_SECRET

    for secret in ("changeme", "test-secret", "0" * 31):
        path = tmp_path / f"config-{len(secret)}-{secret[:2] or 'x'}.toml"
        _write_config(path, jwt_secret=secret, bootstrap_done=True)
        _use_config_path(monkeypatch, path)
        with pytest.raises(ValueError):
            load_config()


# ── 验收 1：无配置 → 自动生成并落盘，重启后不变 ───────────────────────────────


def test_missing_config_generates_secret_and_keeps_it_across_restart(tmp_path, monkeypatch):
    path = tmp_path / "config.toml"
    _use_config_path(monkeypatch, path)

    first = load_config()["agent"]["jwt_secret"]
    assert len(first) == 64
    assert path.exists()

    second = load_config()["agent"]["jwt_secret"]
    assert second == first
    assert toml.load(path)["agent"]["jwt_secret"] == first


# ── 验收：明确首装场景（bootstrap 未完成）→ 原子补写安全随机值 ────────────────


@pytest.mark.parametrize(
    "agent_fields",
    [
        {"jwt_secret": 12345, "bootstrap_done": True},  # 类型错误：数字
        {"jwt_secret": ["a" * 64], "bootstrap_done": True},  # 类型错误：数组
    ],
)
def test_unusable_secret_matrix_refuses_to_start(tmp_path, monkeypatch, agent_fields):
    """反证：键存在、非空但类型错误必须拒绝启动，且不得改写文件。

    未配置（键缺失，或值为空/纯空白占位符）不在此列——那是产品自己留下的迁移
    形态，见 test_legacy_config_without_secret_key_is_migrated 与
    test_template_placeholder_secret_is_migrated_after_bootstrap。
    """
    path = tmp_path / "config.toml"
    original = _write_config(path, **agent_fields)
    _use_config_path(monkeypatch, path)

    with pytest.raises(ValueError):
        load_config()

    assert path.read_text(encoding="utf-8") == original


# ── 旧版模板迁移：键缺失（不是篡改）→ 补写，且不因 bootstrap 已完成而拒绝 ──────


@pytest.mark.parametrize("bootstrap_done", [True, False])
def test_legacy_config_without_secret_key_is_migrated(
    tmp_path, monkeypatch, bootstrap_done
):
    """早于 2026-06-02 的配置模板从未写过 jwt_secret。

    升级到带 B15 校验的版本后，这类**键缺失**配置必须在两种 bootstrap 状态下都能
    起来：按篡改拒绝会让升级即砖机（现场 `C:\\ProgramData\\DataWatch` 正是此形态）。
    补写的是 64 位 hex 强随机值，不触碰 B15 的底线。
    """
    path = tmp_path / "config.toml"
    _write_config(path, bootstrap_done=bootstrap_done)
    _use_config_path(monkeypatch, path)
    assert "jwt_secret" not in toml.load(path)["agent"]

    generated = load_config()["agent"]["jwt_secret"]
    assert isinstance(generated, str) and len(generated) == 64
    assert toml.load(path)["agent"]["jwt_secret"] == generated

    # 补写一次即稳定，不得每次加载都轮换
    assert load_config()["agent"]["jwt_secret"] == generated

    # 迁移不得动 [agent] 之外的既有内容
    assert toml.load(path)["redis"] == {"host": "127.0.0.1", "port": 6379}
    assert toml.load(path)["agent"]["bootstrap_done"] is bootstrap_done


@pytest.mark.parametrize(
    "agent_fields",
    [
        {"jwt_secret": "", "bootstrap_done": False},
        {"jwt_secret": "changeme", "bootstrap_done": False},
        {"jwt_secret": "   ", "bootstrap_done": False},
        {"jwt_secret": 12345, "bootstrap_done": False},  # 类型错误
    ],
)
def test_first_install_unusable_secret_is_replaced_and_persisted(
    tmp_path, monkeypatch, agent_fields
):
    path = tmp_path / "config.toml"
    _write_config(path, **agent_fields)
    _use_config_path(monkeypatch, path)

    generated = load_config()["agent"]["jwt_secret"]
    assert isinstance(generated, str) and len(generated) == 64
    assert toml.load(path)["agent"]["jwt_secret"] == generated

    # 不静默频繁轮换：再次加载必须是同一个值
    assert load_config()["agent"]["jwt_secret"] == generated


# ── 验收 3：合法存量配置保持稳定 ──────────────────────────────────────────────


def test_valid_existing_config_is_not_rewritten(tmp_path, monkeypatch):
    path = tmp_path / "config.toml"
    original = _write_config(path, jwt_secret=STRONG_SECRET, bootstrap_done=True)
    _use_config_path(monkeypatch, path)

    assert load_config()["agent"]["jwt_secret"] == STRONG_SECRET
    assert load_config()["agent"]["jwt_secret"] == STRONG_SECRET
    assert path.read_text(encoding="utf-8") == original


# ── 验收 4：损坏配置不得被静默当作空密钥接受 ──────────────────────────────────


def test_corrupt_config_is_not_silently_accepted(tmp_path, monkeypatch):
    path = tmp_path / "config.toml"
    broken = "[agent\njwt_secret = "
    path.write_text(broken, encoding="utf-8")
    _use_config_path(monkeypatch, path)

    with pytest.raises(Exception):
        load_config()

    assert path.read_text(encoding="utf-8") == broken


def test_config_without_agent_section_is_rejected(tmp_path, monkeypatch):
    path = tmp_path / "config.toml"
    path.write_text("[redis]\nhost = \"127.0.0.1\"\n", encoding="utf-8")
    _use_config_path(monkeypatch, path)

    with pytest.raises(ValueError):
        load_config()


# ── 验收 5：并发首装只产生一个密钥 ────────────────────────────────────────────


def test_concurrent_first_install_produces_exactly_one_secret(tmp_path, monkeypatch):
    path = tmp_path / "config.toml"
    _use_config_path(monkeypatch, path)

    count = 8
    barrier = threading.Barrier(count)
    results: list[str] = []
    errors: list[BaseException] = []

    def worker() -> None:
        try:
            barrier.wait(timeout=10)
            results.append(load_config()["agent"]["jwt_secret"])
        except BaseException as exc:  # noqa: BLE001 - 记录后由断言暴露
            errors.append(exc)

    threads = [threading.Thread(target=worker) for _ in range(count)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join(timeout=15)

    assert errors == []
    assert len(results) == count
    assert len(set(results)) == 1
    assert toml.load(path)["agent"]["jwt_secret"] == results[0]


# ── 验收 6（补充）：WebSocket 帧内校验同样拒绝空密钥 token ────────────────────


class _AuthBypassed(AssertionError):
    """违反 B15 时才会走到日志订阅：用异常把「鉴权被绕过」变成显式失败。"""


class _FakeLogManager:
    def subscribe(self, _name):  # pragma: no cover - 只在鉴权被绕过时执行
        raise _AuthBypassed("token verification was bypassed")


class _FakeState:
    log_manager = _FakeLogManager()


class _FakeApp:
    state = _FakeState()


class _FakeWebSocket:
    def __init__(self, frame: dict) -> None:
        self._frame = frame
        self.closed: list[int] = []
        self.accepted = False
        self.app = _FakeApp()

    async def accept(self) -> None:
        self.accepted = True

    async def receive_json(self) -> dict:
        return self._frame

    async def close(self, code: int | None = None) -> None:
        self.closed.append(code)


async def test_websocket_frame_check_rejects_empty_secret_token(monkeypatch):
    from app.routers import viewer

    monkeypatch.setattr(viewer, "load_config", lambda: {"agent": {"jwt_secret": ""}})
    websocket = _FakeWebSocket({"token": _forge("", roles=["R_SUPER"])})

    await viewer.tail_logs("collector", websocket)

    assert websocket.closed == [4001]
