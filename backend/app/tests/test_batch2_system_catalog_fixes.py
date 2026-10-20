"""审查批次2 回归：系统管理 / 认证 / 点位目录 / XLSX 导入。

覆盖 B006 B007 B008 B009 B021(后端) B037 B038 B039 B050(后端) B054 B058(后端)。
全部使用内存替身，不连接真实 MySQL/Redis/TDengine。
"""

from __future__ import annotations

import asyncio
from dataclasses import dataclass
from io import BytesIO
from types import SimpleNamespace
from typing import Any
from unittest.mock import AsyncMock
from zipfile import ZIP_DEFLATED, ZipFile

import pytest

from app.core import simple_xlsx
from app.core.exceptions import ApiError
from app.core.simple_xlsx import WorkbookSheet, build_workbook_bytes, read_workbook_bytes
from app.schemas.auth import MenuPayload, RoleGrantPayload, RolePayload, UserPayload
from app.services.catalog import CatalogService, DeviceCatalogItem, PointCatalogItem
from app.services.system_manage import SystemManageService


# ---------------------------------------------------------------------------
# 通用替身
# ---------------------------------------------------------------------------


class _Rows:
    def __init__(self, rows):
        self._rows = list(rows)

    def scalars(self):
        return self

    def all(self):
        return list(self._rows)


class _MenuSession:
    def __init__(self, menus, scalar_values=()):
        self.menus = menus
        self._scalars = list(scalar_values)
        self.commits = 0

    async def execute(self, _statement):
        return _Rows(self.menus)

    async def scalar(self, _statement):
        return self._scalars.pop(0) if self._scalars else None

    async def commit(self):
        self.commits += 1


def _menu(menu_id: int, parent_id: int | None, name: str | None = None):
    return SimpleNamespace(id=menu_id, parent_id=parent_id, name=name or f"m{menu_id}")


def _menu_payload(name: str, parent_id: int | None) -> MenuPayload:
    return MenuPayload(name=name, path=f"/{name}", component="", title_key=name, parent_id=parent_id)


def _run_with_timeout(coro, seconds: float = 2.0):
    return asyncio.wait_for(coro, timeout=seconds)


# ---------------------------------------------------------------------------
# B006 菜单祖先环
# ---------------------------------------------------------------------------


def _menu_service(menus) -> tuple[SystemManageService, _MenuSession]:
    session = _MenuSession(menus)
    service = SystemManageService(session)
    by_id = {m.id: m for m in menus}

    async def _get_menu(menu_id):
        if menu_id not in by_id:
            raise ApiError(404, "菜单不存在")
        return by_id[menu_id]

    service._get_menu_or_raise = _get_menu
    return service, session


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "menus,target,new_parent",
    [
        ([_menu(1, None, "A"), _menu(2, 1, "B")], 1, 2),  # A→B→A
        ([_menu(1, None, "A"), _menu(2, 1, "B"), _menu(3, 2, "C")], 1, 3),  # A→B→C→A
    ],
)
async def test_update_menu_rejects_descendant_as_parent(menus, target, new_parent) -> None:
    service, session = _menu_service(menus)
    original_parent = next(m for m in menus if m.id == target).parent_id
    with pytest.raises(ApiError) as excinfo:
        await _run_with_timeout(
            service.update_menu(target, _menu_payload(next(m for m in menus if m.id == target).name, new_parent))
        )
    assert excinfo.value.code == 400
    assert session.commits == 0
    assert next(m for m in menus if m.id == target).parent_id == original_parent


@pytest.mark.asyncio
async def test_update_menu_allows_legit_reparent() -> None:
    menus = [_menu(1, None, "A"), _menu(2, None, "B"), _menu(3, 2, "C")]
    service, session = _menu_service(menus)
    await _run_with_timeout(service.update_menu(1, _menu_payload("A", 3)))
    assert menus[0].parent_id == 3
    assert session.commits == 1


@pytest.mark.asyncio
async def test_expand_menu_ids_terminates_on_existing_dirty_cycle() -> None:
    service = SystemManageService(_MenuSession([_menu(1, 2), _menu(2, 1), _menu(3, 1)]))
    with pytest.raises(ApiError) as excinfo:
        await _run_with_timeout(service._expand_menu_ids([3]))
    assert excinfo.value.code == 400


@pytest.mark.asyncio
async def test_expand_menu_ids_multilevel_still_works() -> None:
    service = SystemManageService(_MenuSession([_menu(1, None), _menu(2, 1), _menu(3, 2)]))
    assert sorted(await _run_with_timeout(service._expand_menu_ids([3]))) == [1, 2, 3]


# ---------------------------------------------------------------------------
# B007 通用角色编辑不能禁用 R_SUPER
# ---------------------------------------------------------------------------


@dataclass
class _Role:
    id: int
    role_code: str
    role_name: str = "role"
    description: str = ""
    enabled: bool = True


def _role_service(role: _Role) -> tuple[SystemManageService, _MenuSession]:
    session = _MenuSession([])
    service = SystemManageService(session)

    async def _get_role(_role_id):
        return role

    async def _can_manage(*_args, **_kwargs):
        return None

    service._get_role_or_raise = _get_role
    service._assert_can_manage_role = _can_manage
    return service, session


@pytest.mark.asyncio
async def test_generic_role_edit_cannot_disable_super() -> None:
    role = _Role(1, "R_SUPER")
    service, session = _role_service(role)
    with pytest.raises(ApiError) as excinfo:
        await service.update_role(
            1,
            RolePayload(role_name="超级管理员", role_code="R_SUPER", enabled=False),
            current_user_roles=["R_SUPER"],
        )
    assert excinfo.value.code == 400
    assert role.enabled is True
    assert session.commits == 0


@pytest.mark.asyncio
async def test_status_endpoint_still_cannot_disable_super() -> None:
    role = _Role(1, "R_SUPER")
    service, session = _role_service(role)
    with pytest.raises(ApiError):
        await service.update_role_status(1, enabled=False, current_user_roles=["R_SUPER"])
    assert role.enabled is True


@pytest.mark.asyncio
async def test_generic_role_edit_can_disable_normal_role() -> None:
    role = _Role(5, "R_CUSTOM")
    service, session = _role_service(role)
    await service.update_role(
        5,
        RolePayload(role_name="custom", role_code="R_CUSTOM", enabled=False),
        current_user_roles=["R_SUPER"],
    )
    assert role.enabled is False
    assert session.commits == 1


# ---------------------------------------------------------------------------
# B021 授权保存：缺字段不能被当作空列表静默清空
# ---------------------------------------------------------------------------


def test_grant_payload_rejects_missing_menu_or_permission_fields() -> None:
    for payload in ({}, {"menuIds": [1]}, {"permissionIds": [1]}, {"dataScopes": []}):
        with pytest.raises(ApiError) as excinfo:
            RoleGrantPayload.from_dict(payload)
        assert excinfo.value.code == 400


def test_grant_payload_rejects_non_array_fields() -> None:
    with pytest.raises(ApiError):
        RoleGrantPayload.from_dict({"menuIds": "", "permissionIds": []})


def test_grant_payload_explicit_empty_is_legit_clear_all() -> None:
    payload = RoleGrantPayload.from_dict({"menuIds": [], "permissionIds": [], "dataScopes": []})
    assert payload.menu_ids == [] and payload.permission_ids == [] and payload.data_scopes == []
    snake = RoleGrantPayload.from_dict({"menu_ids": [2], "permission_ids": [3]})
    assert snake.menu_ids == [2] and snake.permission_ids == [3]
    assert snake.data_scopes is None


class _GrantSession:
    def __init__(self) -> None:
        self.statements: list[Any] = []
        self.added: list[Any] = []
        self.commits = 0

    async def execute(self, statement):
        self.statements.append(statement)
        return None

    def add(self, obj):
        self.added.append(obj)

    async def commit(self):
        self.commits += 1


def _grant_service(session: _GrantSession) -> SystemManageService:
    service = SystemManageService(session)

    async def _role(_role_id):
        return _Role(1, "R_ADMIN")

    async def _ids(ids):
        return list(ids)

    async def _empty(*_args, **_kwargs):
        return []

    async def _noop(*_args, **_kwargs):
        return None

    service._get_role_or_raise = _role
    service._assert_can_manage_role = _noop
    service._expand_menu_ids = _ids
    service._resolve_permission_ids = _ids
    service._resolve_page_permission_ids = _empty
    service._assert_can_grant_permissions = _noop
    return service


def _deleted_tables(session: _GrantSession) -> list[str]:
    return [str(stmt.table.name) for stmt in session.statements if hasattr(stmt, "table")]


@pytest.mark.asyncio
async def test_grant_without_data_scopes_keeps_existing_scopes() -> None:
    session = _GrantSession()
    service = _grant_service(session)
    await service.update_role_grant(
        1,
        RoleGrantPayload.from_dict({"menuIds": [1], "permissionIds": [2]}),
        current_user_id=1,
        current_user_roles=["R_SUPER"],
    )
    tables = _deleted_tables(session)
    assert "sys_data_permission" not in tables
    assert len(tables) == 2
    assert session.commits == 1


@pytest.mark.asyncio
async def test_grant_with_explicit_empty_lists_still_clears_all() -> None:
    session = _GrantSession()
    service = _grant_service(session)
    await service.update_role_grant(
        1,
        RoleGrantPayload.from_dict({"menuIds": [], "permissionIds": [], "dataScopes": []}),
        current_user_id=1,
        current_user_roles=["R_SUPER"],
    )
    assert len(_deleted_tables(session)) == 3
    assert session.added == []
    assert session.commits == 1


# ---------------------------------------------------------------------------
# B038 软删用户名仍占唯一约束
# ---------------------------------------------------------------------------


def _user_payload(name: str) -> UserPayload:
    return UserPayload.from_dict({"userName": name, "userRoles": []})


@pytest.mark.asyncio
async def test_create_user_with_soft_deleted_name_is_business_400() -> None:
    deleted = SimpleNamespace(id=9, user_name="alice", deleted=True)
    session = _MenuSession([], scalar_values=[deleted])
    session.add = lambda _obj: (_ for _ in ()).throw(AssertionError("不应写库"))
    service = SystemManageService(session)
    with pytest.raises(ApiError) as excinfo:
        await service.create_user(_user_payload("alice"), actor="Super", current_user_roles=["R_SUPER"])
    assert excinfo.value.code == 400
    assert session.commits == 0


@pytest.mark.asyncio
async def test_rename_user_to_soft_deleted_name_is_business_400() -> None:
    user = SimpleNamespace(id=3, user_name="bob", deleted=False)
    deleted = SimpleNamespace(id=9, user_name="alice", deleted=True)
    session = _MenuSession([], scalar_values=[deleted])
    service = SystemManageService(session)

    async def _get_user(_user_id):
        return user

    service._get_user_or_raise = _get_user
    with pytest.raises(ApiError) as excinfo:
        await service.update_user(
            3, _user_payload("alice"), actor="Super", current_user_id=1, current_user_roles=["R_SUPER"]
        )
    assert excinfo.value.code == 400
    assert user.user_name == "bob"
    assert session.commits == 0


# ---------------------------------------------------------------------------
# B039 登出撤销失败不能报告成功
# ---------------------------------------------------------------------------


class _SessionCtx:
    async def __aenter__(self):
        return SimpleNamespace()

    async def __aexit__(self, *_exc):
        return False


def _install_logout_fakes(monkeypatch, logout_impl):
    from app.api.routes import auth as auth_routes

    class _FakeAuthService:
        def __init__(self, _session):
            pass

        async def logout(self, authorization):
            return await logout_impl(authorization)

    log = AsyncMock()
    monkeypatch.setattr(auth_routes, "get_mysql_session_factory", lambda: _SessionCtx)
    monkeypatch.setattr(auth_routes, "AuthService", _FakeAuthService)
    monkeypatch.setattr(auth_routes, "write_system_log", log)
    monkeypatch.setattr(auth_routes, "_resolve_client_ip", lambda _request: "127.0.0.1")
    return auth_routes, log


def _creds():
    return SimpleNamespace(scheme="Bearer", credentials="access-token")


@pytest.mark.asyncio
async def test_logout_commit_failure_is_not_success(monkeypatch) -> None:
    async def _fail(_authorization):
        raise RuntimeError("mysql commit failed")

    auth_routes, _log = _install_logout_fakes(monkeypatch, _fail)
    with pytest.raises(ApiError) as excinfo:
        await auth_routes.logout(SimpleNamespace(), _creds())
    assert excinfo.value.code == 503


@pytest.mark.asyncio
@pytest.mark.parametrize("code", [401, 403])
async def test_logout_with_invalid_token_is_idempotent_success(monkeypatch, code) -> None:
    async def _invalid(_authorization):
        raise ApiError(code, "当前登录状态已失效")

    auth_routes, log = _install_logout_fakes(monkeypatch, _invalid)
    response = await auth_routes.logout(SimpleNamespace(), _creds())
    assert response is not None
    log.assert_awaited()


@pytest.mark.asyncio
async def test_logout_other_business_error_is_propagated(monkeypatch) -> None:
    async def _busy(_authorization):
        raise ApiError(503, "busy")

    auth_routes, _log = _install_logout_fakes(monkeypatch, _busy)
    with pytest.raises(ApiError) as excinfo:
        await auth_routes.logout(SimpleNamespace(), _creds())
    assert excinfo.value.code == 503


# ---------------------------------------------------------------------------
# B008 日志清理睡醒后重新读取策略
# ---------------------------------------------------------------------------


@pytest.mark.asyncio
async def test_cleanup_rereads_policy_after_sleep(monkeypatch) -> None:
    from app.core import log_scheduler as module

    scheduler = module.LogScheduler()
    scheduler._running = True
    policies = [
        SimpleNamespace(enabled=True, cleanup_time="03:00", retention_days=7),
        # 睡眠期间被禁用
        SimpleNamespace(enabled=False, cleanup_time="03:00", retention_days=7),
    ]
    reads = {"n": 0}

    async def _get_policy():
        reads["n"] += 1
        if reads["n"] > len(policies):
            scheduler._running = False
            return policies[-1]
        return policies[reads["n"] - 1]

    executed = []

    async def _execute(policy):
        executed.append(policy)

    async def _sleep(_seconds):
        if reads["n"] >= len(policies):
            scheduler._running = False

    scheduler._get_policy = _get_policy
    scheduler._execute_cleanup = _execute
    monkeypatch.setattr(module.asyncio, "sleep", _sleep)
    await _run_with_timeout(scheduler._cleanup_scheduler())
    assert executed == []


@pytest.mark.asyncio
async def test_cleanup_uses_fresh_retention_after_sleep(monkeypatch) -> None:
    from app.core import log_scheduler as module

    scheduler = module.LogScheduler()
    scheduler._running = True
    before = SimpleNamespace(enabled=True, cleanup_time="03:00", retention_days=7)
    after = SimpleNamespace(enabled=True, cleanup_time="03:00", retention_days=30)
    queue = [before, after]
    executed = []

    async def _get_policy():
        return queue.pop(0) if queue else after

    async def _execute(policy):
        executed.append(policy)
        scheduler._running = False

    async def _sleep(_seconds):
        return None

    scheduler._get_policy = _get_policy
    scheduler._execute_cleanup = _execute
    monkeypatch.setattr(module.asyncio, "sleep", _sleep)
    await _run_with_timeout(scheduler._cleanup_scheduler())
    assert [p.retention_days for p in executed] == [30]


@pytest.mark.asyncio
async def test_capacity_check_uses_fresh_policy_after_sleep(monkeypatch) -> None:
    from app.core import log_scheduler as module

    scheduler = module.LogScheduler()
    scheduler._running = True
    before = SimpleNamespace(capacity_check_interval_minutes=1, max_total_mb=100)
    after = SimpleNamespace(capacity_check_interval_minutes=1, max_total_mb=5000)
    queue = [before, after]
    checked = []

    async def _get_policy():
        return queue.pop(0) if queue else after

    async def _check(policy):
        checked.append(policy)
        scheduler._running = False

    async def _sleep(_seconds):
        return None

    scheduler._get_policy = _get_policy
    scheduler._check_capacity = _check
    monkeypatch.setattr(module.asyncio, "sleep", _sleep)
    await _run_with_timeout(scheduler._capacity_checker())
    assert [p.max_total_mb for p in checked] == [5000]


# ---------------------------------------------------------------------------
# B009 / B037 XLSX 读取
# ---------------------------------------------------------------------------


def _rewrite_part(content: bytes, part: str, transform) -> bytes:
    source = ZipFile(BytesIO(content))
    out = BytesIO()
    with ZipFile(out, "w", compression=ZIP_DEFLATED) as target:
        for info in source.infolist():
            data = source.read(info.filename)
            if info.filename == part:
                data = transform(data)
            target.writestr(info.filename, data)
    return out.getvalue()


def _sheet_with_cells(cells_xml: str) -> bytes:
    base = build_workbook_bytes([WorkbookSheet(name="S", rows=[["a"]])])
    sheet = (
        f'<worksheet xmlns="{simple_xlsx.MAIN_NS}"><sheetData>{cells_xml}</sheetData></worksheet>'
    ).encode()
    return _rewrite_part(base, "xl/worksheets/sheet1.xml", lambda _old: sheet)


def test_out_of_range_column_is_rejected_quickly() -> None:
    content = _sheet_with_cells('<row r="1"><c r="ZZZZ1" t="inlineStr"><is><t>x</t></is></c></row>')
    with pytest.raises(ValueError):
        read_workbook_bytes(content)


def test_absurdly_long_column_letters_do_not_accumulate() -> None:
    content = _sheet_with_cells(f'<row r="1"><c r="{"Z" * 5000}1"><v>1</v></c></row>')
    with pytest.raises(ValueError):
        read_workbook_bytes(content)


def test_last_legal_column_xfd_is_accepted() -> None:
    content = _sheet_with_cells('<row r="1"><c r="XFD1"><v>7</v></c></row>')
    rows = read_workbook_bytes(content)["S"]
    assert len(rows[0]) == 16384 and rows[0][-1] == "7"


def test_out_of_range_row_is_rejected() -> None:
    content = _sheet_with_cells('<row r="1"><c r="A1048577"><v>1</v></c></row>')
    with pytest.raises(ValueError):
        read_workbook_bytes(content)


def test_sparse_expansion_budget(monkeypatch) -> None:
    monkeypatch.setattr(simple_xlsx, "MAX_EXPANDED_CELLS", 100)
    rows_xml = "".join(f'<row r="{i}"><c r="Z{i}"><v>1</v></c></row>' for i in range(1, 6))
    with pytest.raises(ValueError):
        read_workbook_bytes(_sheet_with_cells(rows_xml))


def test_catalog_import_maps_invalid_workbook_to_400() -> None:
    content = _sheet_with_cells('<row r="1"><c r="ZZZZ1"><v>1</v></c></row>')
    service = CatalogService(session=None, tdengine=None)
    with pytest.raises(ApiError) as excinfo:
        asyncio.run(service.import_points_workbook(content, actor="tester"))
    assert excinfo.value.code == 400
    assert "Excel 文件无法解析" in excinfo.value.message


@pytest.mark.parametrize(
    "target",
    ["worksheets/sheet1.xml", "/xl/worksheets/sheet1.xml", "xl/worksheets/sheet1.xml"],
)
def test_relationship_targets_resolve_to_same_part(target) -> None:
    base = build_workbook_bytes([WorkbookSheet(name="S", rows=[["h1", "h2"], ["v1", "v2"]])])
    content = _rewrite_part(
        base,
        "xl/_rels/workbook.xml.rels",
        lambda data: data.replace(b'Target="worksheets/sheet1.xml"', f'Target="{target}"'.encode()),
    )
    assert read_workbook_bytes(content)["S"] == [["h1", "h2"], ["v1", "v2"]]


@pytest.mark.parametrize("target", ["../../etc/passwd", "/../secret.xml"])
def test_relationship_target_cannot_escape_package(target) -> None:
    with pytest.raises(ValueError):
        simple_xlsx._resolve_workbook_target(target)


# ---------------------------------------------------------------------------
# B050 JSON 行 False/0 不能被抹成空
# ---------------------------------------------------------------------------


@pytest.mark.parametrize(
    "raw,expected",
    [(None, True), (False, False), (True, True), (0, False), (1, True), ("false", False), ("0", False)],
)
def test_json_rows_preserve_false_and_zero(raw, expected) -> None:
    from app.services.catalog import POINT_IMPORT_FIELD_ALIASES, POINT_IMPORT_HEADERS, POINT_REQUIRED_HEADERS

    service = CatalogService(session=None, tdengine=None)
    row = {
        "deviceCode": "D1",
        "varSeq": 1,
        "pointName": "p",
        "ioAddress": "DB1.0",
        "dataType": "BOOL",
        "driverName": "S7",
        "isReadonly": raw,
        "isStorage": raw,
    }
    records = service._normalize_import_payload_rows(
        [row],
        expected_headers=POINT_IMPORT_HEADERS,
        required_headers=POINT_REQUIRED_HEADERS,
        field_aliases=POINT_IMPORT_FIELD_ALIASES,
    )
    values = records[0]["values"]
    assert service._to_bool(values["是否只读"], field_name="是否只读", default=True) is expected
    assert service._to_bool(values["是否存储"], field_name="是否存储", default=True) is expected


# ---------------------------------------------------------------------------
# B054 已有点位按显示口径回传类型时可只改名称
# ---------------------------------------------------------------------------


class _Mapping:
    def __init__(self, row):
        self._row = row

    def mappings(self):
        return self

    def first(self):
        return self._row

    def scalar_one_or_none(self):
        return self._row


class _PointUpdateSession:
    def __init__(self, existing_var_type: str):
        self.existing_var_type = existing_var_type
        self.calls = 0
        self.update_params = None
        self.commits = 0

    async def execute(self, _statement, params=None):
        self.calls += 1
        if self.calls == 1:
            return _Mapping({"device_id": 7, "device_code": "D1", "driver_name": "S7"})
        if self.calls == 2:
            return _Mapping({"device_id": 7, "point_group_id": None, "var_type": self.existing_var_type})
        if self.calls in (3, 4):
            return _Mapping(None)
        self.update_params = params
        return None

    async def commit(self):
        self.commits += 1

    async def rollback(self):
        return None


def _point_update_service(var_type: str) -> tuple[CatalogService, _PointUpdateSession]:
    session = _PointUpdateSession(var_type)
    service = CatalogService(session=session, tdengine=None)

    async def _locator(**_kwargs):
        return {"point_id": 11}

    service._resolve_point_locator = _locator
    return service, session


def _point_update_payload(data_type: str) -> dict[str, Any]:
    return {
        "deviceId": 7,
        "deviceCode": "D1",
        "varSeq": 1,
        "pointName": "renamed",
        "ioAddress": "DB1.0",
        "dataType": data_type,
        "isReadonly": True,
        "driverName": "S7",
    }


@pytest.mark.asyncio
@pytest.mark.parametrize(
    "stored,displayed",
    [("Boolean", "BOOL"), ("FLOAT", "REAL"), ("BYTE", "BYTE"), ("INT", "INT")],
)
async def test_existing_point_can_edit_metadata_with_display_type(stored, displayed) -> None:
    service, session = _point_update_service(stored)
    await service.update_point(11, _point_update_payload(displayed), actor="tester")
    assert session.commits == 1
    # 物理类型保持库里原值，不把显示别名写回
    assert session.update_params["var_type"] == stored
    assert session.update_params["var_name"] == "renamed"


@pytest.mark.asyncio
@pytest.mark.parametrize("stored,requested", [("Boolean", "INT"), ("BYTE", "WORD"), ("FLOAT", "DINT")])
async def test_existing_point_type_change_still_rejected(stored, requested) -> None:
    service, session = _point_update_service(stored)
    with pytest.raises(ApiError) as excinfo:
        await service.update_point(11, _point_update_payload(requested), actor="tester")
    assert excinfo.value.code == 400
    assert session.commits == 0


def test_create_whitelist_unchanged() -> None:
    service = CatalogService(session=None, tdengine=None)
    with pytest.raises(ApiError):
        service._normalize_requested_data_type("BYTE")


# ---------------------------------------------------------------------------
# B058 导出复用设备类型/连接状态预筛选
# ---------------------------------------------------------------------------


def _device(device_id: int, device_type: str) -> DeviceCatalogItem:
    return DeviceCatalogItem(
        device_id=device_id,
        device_code=f"D{device_id}",
        device_name=f"dev{device_id}",
        device_type=device_type,
        device_type_code=device_type,
        group_name="",
        group_code="",
        driver_name="S7",
        enable=1,
        device_status_raw=1,
        endpoint="",
        endpoint_bkp="",
        create_time=None,
        update_time=None,
    )


def _point(point_id: int, device_id: int) -> PointCatalogItem:
    return PointCatalogItem(
        point_id=point_id,
        device_id=device_id,
        device_code=f"D{device_id}",
        device_name=f"dev{device_id}",
        point_name=f"p{point_id}",
        io_address=f"DB1.{point_id}",
        data_type="INT",
        description="",
        driver_name="S7",
        create_time=None,
        update_time=None,
        var_seq=point_id,
        scan_rate=1000,
        is_alarm=False,
        is_storage=True,
        is_readonly=True,
        group_id=None,
        td_column_name=None,
        enabled=True,
    )


def _export_service() -> CatalogService:
    service = CatalogService(session=None, tdengine=None)
    devices = {1: _device(1, "boiler"), 2: _device(2, "boiler"), 3: _device(3, "pump")}
    online = {1}
    points = [_point(10, 1), _point(20, 2), _point(30, 3)]

    async def _device_rows():
        return list(devices.values())

    async def _latest(_devices):
        return {}

    async def _point_rows(*_args, **_kwargs):
        return list(points)

    service._fetch_device_rows = _device_rows
    service._to_device_item = lambda row: row
    service._fetch_latest_collect_times = _latest
    service._serialize_device_item = lambda device, _latest: {
        "connectStatus": "online" if device.device_id in online else "offline"
    }
    service._fetch_point_rows = _point_rows
    service._to_point_item = lambda row: row
    return service


@pytest.mark.asyncio
async def test_export_applies_device_type_and_connect_status() -> None:
    service = _export_service()
    items = await service._fetch_export_points({"deviceType": "boiler", "connectStatus": "offline"})
    assert [item.point_id for item in items] == [20]
    # 与列表共用同一预筛选
    prefiltered = await service._apply_point_device_prefilter({"deviceType": "boiler", "connectStatus": "offline"})
    assert prefiltered["deviceIds"] == [2]


@pytest.mark.asyncio
async def test_export_with_no_matching_device_is_empty() -> None:
    service = _export_service()
    assert await service._fetch_export_points({"deviceType": "chiller"}) == []


@pytest.mark.asyncio
async def test_export_without_device_filters_unchanged() -> None:
    service = _export_service()
    items = await service._fetch_export_points({})
    assert [item.point_id for item in items] == [10, 20, 30]


def test_export_route_accepts_device_type_and_connect_status() -> None:
    import inspect

    from app.api.routes import catalog as catalog_routes

    params = inspect.signature(catalog_routes.export_points).parameters
    assert "deviceType" in params and "connectStatus" in params
