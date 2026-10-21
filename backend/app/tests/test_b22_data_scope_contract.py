"""B22 回归：数据范围当前的**真实语义**必须被显式表达。

现状（实测取证）：``sys_data_permission`` 从未参与授权判定（只有首装种子与角色授权页的
读取展示），前端也没有数据范围配置入口，内置角色一律种为 ``all/*``。因此数据范围目前
是"只有声明、没有执行"。

风险在于**静默无效**：管理员提交一个收窄范围、以为该角色已被限制，实际读取路径并不
过滤。修复后 `update_role_grant` 只接受 ``scopeType="all"``，收窄形式显式返回业务码 501，
默认策略（无范围记录 ⇒ 不受限）也在 API 契约中显式声明。
"""

from __future__ import annotations

from dataclasses import dataclass

import pytest

from app.core.exceptions import ApiError
from app.schemas.auth import RoleGrantPayload
from app.services.system_manage import SystemManageService


@dataclass
class _Role:
    id: int
    role_code: str


class _GrantSession:
    def __init__(self) -> None:
        self.added: list[object] = []
        self.commits = 0

    async def execute(self, _statement):
        return None

    def add(self, obj: object) -> None:
        self.added.append(obj)

    async def commit(self) -> None:
        self.commits += 1


def _service(session: _GrantSession) -> SystemManageService:
    service = SystemManageService(session)

    async def _role(_role_id):
        return _Role(1, "R_ADMIN")

    async def _empty(*_args, **_kwargs):
        return []

    service._get_role_or_raise = _role
    service._expand_menu_ids = _empty
    service._resolve_permission_ids = _empty
    service._resolve_page_permission_ids = _empty
    return service


def _payload(scopes):
    return RoleGrantPayload.from_dict(
        {"menuIds": [], "permissionIds": [], "dataScopes": scopes, "clearAll": True}
    )


@pytest.mark.asyncio
async def test_narrowing_scope_is_refused_instead_of_silently_ignored() -> None:
    session = _GrantSession()
    service = _service(session)
    with pytest.raises(ApiError) as excinfo:
        await service.update_role_grant(
            1,
            _payload([{"scopeType": "device", "scopeValue": "QC100"}]),
            current_user_id=1,
            current_user_roles=["R_SUPER"],
        )
    assert excinfo.value.code == 501
    assert "尚未实现" in excinfo.value.message
    # 拒绝时不得留下任何写入
    assert session.commits == 0
    assert session.added == []


@pytest.mark.asyncio
async def test_unrestricted_scope_is_accepted() -> None:
    session = _GrantSession()
    service = _service(session)
    await service.update_role_grant(
        1,
        _payload([{"scopeType": "all", "scopeValue": "*"}]),
        current_user_id=1,
        current_user_roles=["R_SUPER"],
    )
    assert session.commits == 1
    assert len(session.added) == 1
    assert getattr(session.added[0], "scope_type") == "all"


@pytest.mark.asyncio
async def test_absent_scopes_are_accepted_and_mean_unrestricted() -> None:
    """默认策略：没有范围记录 ⇒ 不受限（选择性收窄，未配置即不收窄）。"""
    session = _GrantSession()
    service = _service(session)
    await service.update_role_grant(
        1, _payload([]), current_user_id=1, current_user_roles=["R_SUPER"]
    )
    assert session.commits == 1
    assert session.added == []


def test_contract_document_states_the_current_semantics() -> None:
    """契约文档必须写明"当前不执行数据范围过滤"与默认策略，避免被当成已生效。"""
    from pathlib import Path

    doc = Path(__file__).resolve().parents[3] / "docs" / "03-数据与契约" / "契约" / "API契约.md"
    text = doc.read_text(encoding="utf-8")
    assert "不对任何读取路径" in text
    assert "501" in text
    assert "不受范围限制" in text
