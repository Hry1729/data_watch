from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime

from sqlalchemy import delete, select

from app.core.logging import get_logger
from app.core.security import hash_password
from app.db.mysql import get_mysql_engine, get_mysql_session_factory
from app.models import (
    Base,
    SysDataPermission,
    SysMenu,
    SysPermission,
    SysRole,
    SysRoleMenu,
    SysRolePermission,
    SysUser,
    SysUserRole,
)

logger = get_logger("backend.auth.bootstrap")


@dataclass(slots=True)
class MenuSeed:
    name: str
    path: str
    component: str
    title_key: str
    icon: str = ""
    menu_type: str = "menu"
    link: str = ""
    is_iframe: bool = False
    keep_alive: bool = False
    is_hide: bool = False
    is_hide_tab: bool = False
    fixed_tab: bool = False
    active_path: str = ""
    is_full_page: bool = False
    sort: int = 1
    enabled: bool = True
    children: list["MenuSeed"] = field(default_factory=list)


@dataclass(slots=True)
class PermissionSeed:
    permission_code: str
    permission_name: str
    permission_type: str
    menu_name: str | None
    auth_mark: str
    description: str = ""
    sort: int = 1
    enabled: bool = True


ROLE_SEEDS = [
    {
        "role_code": "R_SUPER",
        "role_name": "超级管理员",
        "description": "系统内置超级管理员，拥有全部权限",
        "sort": 1,
    },
    {
        "role_code": "R_ADMIN",
        "role_name": "运维管理员",
        "description": "设备监控业务管理员，拥有设备监控和用户管理权限",
        "sort": 2,
    },
    {
        "role_code": "R_USER",
        "role_name": "监控用户",
        "description": "普通监控账号，仅保留只读查看能力",
        "sort": 3,
    },
]

USER_SEEDS = [
    {
        "user_name": "Super",
        "nick_name": "超级管理员",
        "email": "super@device-monitor.local",
        "phone": "13800000001",
        "gender": "unknown",
        "status": "1",
        "roles": ["R_SUPER"],
    },
    {
        "user_name": "Admin",
        "nick_name": "运维管理员",
        "email": "admin@device-monitor.local",
        "phone": "13800000002",
        "gender": "unknown",
        "status": "1",
        "roles": ["R_ADMIN"],
    },
    {
        "user_name": "User",
        "nick_name": "监控用户",
        "email": "user@device-monitor.local",
        "phone": "13800000003",
        "gender": "unknown",
        "status": "1",
        "roles": ["R_USER"],
    },
]

MENU_SEEDS = [
    MenuSeed(
        name="DeviceMonitor",
        path="/device-monitor",
        component="/index/index",
        title_key="menus.deviceMonitor.title",
        icon="ri:cpu-line",
        menu_type="directory",
        sort=10,
        children=[
            MenuSeed(
                name="MonitorDashboard",
                path="dashboard",
                component="/device_monitor/dashboard/index",
                title_key="menus.deviceMonitor.dashboard",
                icon="ri:dashboard-line",
                keep_alive=True,
                fixed_tab=True,
                sort=1,
            ),
            MenuSeed(
                name="DriverManage",
                path="driver",
                component="/device_monitor/driver/index",
                title_key="menus.deviceMonitor.driver",
                icon="ri:plug-line",
                keep_alive=True,
                sort=2,
            ),
            MenuSeed(
                name="DeviceManage",
                path="device",
                component="/device_monitor/device/index",
                title_key="menus.deviceMonitor.device",
                icon="ri:server-line",
                keep_alive=True,
                sort=3,
            ),
            MenuSeed(
                name="PointManage",
                path="point",
                component="/device_monitor/point/index",
                title_key="menus.deviceMonitor.point",
                icon="ri:flashlight-line",
                keep_alive=True,
                sort=4,
            ),
            MenuSeed(
                name="RealtimeMonitor",
                path="realtime",
                component="/device_monitor/realtime/index",
                title_key="menus.deviceMonitor.realtime",
                icon="ri:pulse-line",
                keep_alive=False,
                sort=5,
            ),
            MenuSeed(
                name="HistoryData",
                path="history",
                component="/device_monitor/history/index",
                title_key="menus.deviceMonitor.history",
                icon="ri:line-chart-line",
                keep_alive=False,
                sort=6,
            ),
            MenuSeed(
                name="SystemLog",
                path="log",
                component="/device_monitor/log/index",
                title_key="menus.deviceMonitor.log",
                icon="ri:file-list-3-line",
                keep_alive=True,
                sort=7,
            ),
        ],
    ),
    MenuSeed(
        name="System",
        path="/system",
        component="/index/index",
        title_key="menus.system.title",
        icon="ri:user-3-line",
        menu_type="directory",
        sort=90,
        children=[
            MenuSeed(
                name="User",
                path="user",
                component="/system/user",
                title_key="menus.system.user",
                icon="ri:user-line",
                keep_alive=True,
                sort=1,
            ),
            MenuSeed(
                name="Role",
                path="role",
                component="/system/role",
                title_key="menus.system.role",
                icon="ri:user-settings-line",
                keep_alive=True,
                sort=2,
            ),
            MenuSeed(
                name="Menus",
                path="menu",
                component="/system/menu",
                title_key="menus.system.menu",
                icon="ri:menu-line",
                keep_alive=True,
                sort=3,
            ),
        ],
    ),
]

PERMISSION_SEEDS = [
    PermissionSeed("device_monitor:dashboard:view", "工作台查看", "page", "MonitorDashboard", "device_monitor:dashboard:view", sort=10),
    PermissionSeed("device_monitor:driver:view", "驱动管理查看", "page", "DriverManage", "device_monitor:driver:view", sort=20),
    PermissionSeed("device_monitor:device:view", "设备管理查看", "page", "DeviceManage", "device_monitor:device:view", sort=30),
    PermissionSeed("device_monitor:device:create", "设备新增", "action", "DeviceManage", "device_monitor:device:create", sort=31),
    PermissionSeed("device_monitor:device:update", "设备编辑", "action", "DeviceManage", "device_monitor:device:update", sort=32),
    PermissionSeed("device_monitor:device:delete", "设备删除", "action", "DeviceManage", "device_monitor:device:delete", sort=33),
    PermissionSeed("device_monitor:device:import", "设备导入", "action", "DeviceManage", "device_monitor:device:import", sort=34),
    PermissionSeed("device_monitor:device:export", "设备导出", "action", "DeviceManage", "device_monitor:device:export", sort=35),
    PermissionSeed("device_monitor:point:view", "点位管理查看", "page", "PointManage", "device_monitor:point:view", sort=40),
    PermissionSeed("device_monitor:point:create", "点位新增", "action", "PointManage", "device_monitor:point:create", sort=41),
    PermissionSeed("device_monitor:point:update", "点位编辑", "action", "PointManage", "device_monitor:point:update", sort=42),
    PermissionSeed("device_monitor:point:delete", "点位删除", "action", "PointManage", "device_monitor:point:delete", sort=43),
    PermissionSeed("device_monitor:point:import", "点位导入", "action", "PointManage", "device_monitor:point:import", sort=44),
    PermissionSeed("device_monitor:point:export", "点位导出", "action", "PointManage", "device_monitor:point:export", sort=45),
    PermissionSeed("device_monitor:realtime:view", "实时监控查看", "page", "RealtimeMonitor", "device_monitor:realtime:view", sort=50),
    PermissionSeed("device_monitor:realtime:subscribe", "实时监控订阅", "action", "RealtimeMonitor", "device_monitor:realtime:subscribe", sort=51),
    PermissionSeed("device_monitor:realtime:refresh", "实时监控刷新", "action", "RealtimeMonitor", "device_monitor:realtime:refresh", sort=52),
    PermissionSeed("device_monitor:history:view", "历史数据查看", "page", "HistoryData", "device_monitor:history:view", sort=60),
    PermissionSeed("device_monitor:history:export", "历史数据导出", "action", "HistoryData", "device_monitor:history:export", sort=61),
    PermissionSeed("device_monitor:log:view", "系统日志查看", "page", "SystemLog", "device_monitor:log:view", sort=70),
    PermissionSeed("device_monitor:log:export", "系统日志导出", "action", "SystemLog", "device_monitor:log:export", sort=71),
    PermissionSeed("device_monitor:log:cleanup", "系统日志清理", "action", "SystemLog", "device_monitor:log:cleanup", sort=72),
    PermissionSeed("device_monitor:log:policy", "系统日志策略配置", "action", "SystemLog", "device_monitor:log:policy", sort=73),
    PermissionSeed("device_monitor:log:debug", "系统日志DEBUG开关", "action", "SystemLog", "device_monitor:log:debug", sort=74),
    PermissionSeed("system:user:view", "用户管理查看", "page", "User", "system:user:view", sort=110),
    PermissionSeed("system:user:create", "用户新增", "action", "User", "system:user:create", sort=111),
    PermissionSeed("system:user:update", "用户编辑", "action", "User", "system:user:update", sort=112),
    PermissionSeed("system:user:delete", "用户删除", "action", "User", "system:user:delete", sort=113),
    PermissionSeed("system:user:password_reset", "用户密码重置", "action", "User", "system:user:password_reset", sort=114),
    PermissionSeed("system:role:view", "角色管理查看", "page", "Role", "system:role:view", sort=120),
    PermissionSeed("system:role:create", "角色新增", "action", "Role", "system:role:create", sort=121),
    PermissionSeed("system:role:update", "角色编辑", "action", "Role", "system:role:update", sort=122),
    PermissionSeed("system:role:delete", "角色删除", "action", "Role", "system:role:delete", sort=123),
    PermissionSeed("system:role:grant", "角色授权", "action", "Role", "system:role:grant", sort=124),
    PermissionSeed("system:menu:view", "菜单管理查看", "page", "Menus", "system:menu:view", sort=130),
    PermissionSeed("system:menu:create", "菜单新增", "action", "Menus", "add", sort=131),
    PermissionSeed("system:menu:update", "菜单编辑", "action", "Menus", "edit", sort=132),
    PermissionSeed("system:menu:delete", "菜单删除", "action", "Menus", "delete", sort=133),
]

ROLE_MENU_GRANTS = {
    "R_SUPER": [
        "DeviceMonitor",
        "MonitorDashboard",
        "DriverManage",
        "DeviceManage",
        "PointManage",
        "RealtimeMonitor",
        "HistoryData",
        "SystemLog",
        "System",
        "User",
        "Role",
        "Menus",
    ],
    "R_ADMIN": [
        "DeviceMonitor",
        "MonitorDashboard",
        "DriverManage",
        "DeviceManage",
        "PointManage",
        "RealtimeMonitor",
        "HistoryData",
        "SystemLog",
        "System",
        "User",
    ],
    "R_USER": [
        "DeviceMonitor",
        "MonitorDashboard",
        "RealtimeMonitor",
        "HistoryData",
        "SystemLog",
    ],
}

ROLE_PERMISSION_GRANTS = {
    "R_SUPER": [item.permission_code for item in PERMISSION_SEEDS],
    "R_ADMIN": [
        item.permission_code
        for item in PERMISSION_SEEDS
        if (
            (
                item.permission_code.startswith("device_monitor:")
                and item.permission_code
                not in {
                    "device_monitor:log:cleanup",
                    "device_monitor:log:policy",
                    "device_monitor:log:debug",
                }
            )
            or item.permission_code.startswith("system:user:")
        )
    ],
    "R_USER": [
        "device_monitor:dashboard:view",
        "device_monitor:realtime:view",
        "device_monitor:realtime:subscribe",
        "device_monitor:realtime:refresh",
        "device_monitor:history:view",
        "device_monitor:log:view",
    ],
}

DATA_SCOPE_GRANTS = {
    "R_SUPER": [{"scope_type": "all", "scope_value": "*"}],
    "R_ADMIN": [{"scope_type": "all", "scope_value": "*"}],
    "R_USER": [{"scope_type": "all", "scope_value": "*"}],
}


async def bootstrap_auth_schema() -> None:
    engine = get_mysql_engine()
    async with engine.begin() as connection:
        await connection.run_sync(Base.metadata.create_all)

    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        await _seed_roles(session)
        await _seed_users(session)
        menu_by_name = await _seed_menus(session)
        permission_by_code = await _seed_permissions(session, menu_by_name)
        role_by_code = await _load_role_map(session)
        user_by_name = await _load_user_map(session)
        await _seed_user_roles(session, user_by_name, role_by_code)
        await _seed_role_menus(session, role_by_code, menu_by_name)
        await _seed_role_permissions(session, role_by_code, permission_by_code)
        await _seed_data_permissions(session, role_by_code)
        await session.commit()

    logger.info("Auth schema bootstrap complete")


async def _seed_roles(session) -> None:
    for seed in ROLE_SEEDS:
        role = await session.scalar(select(SysRole).where(SysRole.role_code == seed["role_code"]))
        if role is None:
            role = SysRole(role_code=seed["role_code"])
            session.add(role)
        role.role_name = seed["role_name"]
        role.description = seed["description"]
        role.enabled = True
        role.sort = seed["sort"]
    await session.flush()


async def _seed_users(session) -> None:
    default_password_hash = hash_password("123456")
    for seed in USER_SEEDS:
        user = await session.scalar(select(SysUser).where(SysUser.user_name == seed["user_name"]))
        if user is None:
            user = SysUser(user_name=seed["user_name"])
            session.add(user)
        user.password_hash = default_password_hash
        user.nick_name = seed["nick_name"]
        user.email = seed["email"]
        user.phone = seed["phone"]
        user.gender = seed["gender"]
        user.avatar = user.avatar or ""
        user.status = seed["status"]
        user.deleted = False
        user.update_by = "bootstrap"
        if not user.create_by:
            user.create_by = "bootstrap"
    await session.flush()


async def _seed_menus(session) -> dict[str, SysMenu]:
    menu_by_name: dict[str, SysMenu] = {}

    async def upsert(seed: MenuSeed, parent_id: int | None = None) -> None:
        menu = await session.scalar(select(SysMenu).where(SysMenu.name == seed.name))
        if menu is None:
            menu = SysMenu(name=seed.name)
            session.add(menu)
        menu.parent_id = parent_id
        menu.path = seed.path
        menu.component = seed.component
        menu.title_key = seed.title_key
        menu.icon = seed.icon
        menu.menu_type = seed.menu_type
        menu.link = seed.link
        menu.is_iframe = seed.is_iframe
        menu.keep_alive = seed.keep_alive
        menu.is_hide = seed.is_hide
        menu.is_hide_tab = seed.is_hide_tab
        menu.fixed_tab = seed.fixed_tab
        menu.active_path = seed.active_path
        menu.is_full_page = seed.is_full_page
        menu.sort = seed.sort
        menu.enabled = seed.enabled
        await session.flush()
        menu_by_name[seed.name] = menu
        for child in seed.children:
            await upsert(child, menu.id)

    for seed in MENU_SEEDS:
        await upsert(seed)

    return menu_by_name


async def _seed_permissions(session, menu_by_name: dict[str, SysMenu]) -> dict[str, SysPermission]:
    permission_by_code: dict[str, SysPermission] = {}
    for seed in PERMISSION_SEEDS:
        permission = await session.scalar(
            select(SysPermission).where(SysPermission.permission_code == seed.permission_code)
        )
        if permission is None:
            permission = SysPermission(permission_code=seed.permission_code)
            session.add(permission)
        permission.permission_name = seed.permission_name
        permission.permission_type = seed.permission_type
        permission.menu_id = menu_by_name[seed.menu_name].id if seed.menu_name else None
        permission.auth_mark = seed.auth_mark
        permission.description = seed.description
        permission.sort = seed.sort
        permission.enabled = seed.enabled
        await session.flush()
        permission_by_code[seed.permission_code] = permission
    return permission_by_code


async def _load_role_map(session) -> dict[str, SysRole]:
    roles = (await session.execute(select(SysRole))).scalars().all()
    return {role.role_code: role for role in roles}


async def _load_user_map(session) -> dict[str, SysUser]:
    users = (await session.execute(select(SysUser).where(SysUser.deleted.is_(False)))).scalars().all()
    return {user.user_name: user for user in users}


async def _seed_user_roles(session, user_by_name: dict[str, SysUser], role_by_code: dict[str, SysRole]) -> None:
    for seed in USER_SEEDS:
        user = user_by_name[seed["user_name"]]
        await session.execute(delete(SysUserRole).where(SysUserRole.user_id == user.id))
        for role_code in seed["roles"]:
            session.add(SysUserRole(user_id=user.id, role_id=role_by_code[role_code].id))
    await session.flush()


async def _seed_role_menus(session, role_by_code: dict[str, SysRole], menu_by_name: dict[str, SysMenu]) -> None:
    for role_code, menu_names in ROLE_MENU_GRANTS.items():
        role = role_by_code[role_code]
        await session.execute(delete(SysRoleMenu).where(SysRoleMenu.role_id == role.id))
        expanded_menu_names = _expand_menu_names(menu_names)
        for menu_name in sorted(expanded_menu_names):
            session.add(SysRoleMenu(role_id=role.id, menu_id=menu_by_name[menu_name].id))
    await session.flush()


async def _seed_role_permissions(
    session,
    role_by_code: dict[str, SysRole],
    permission_by_code: dict[str, SysPermission],
) -> None:
    for role_code, permission_codes in ROLE_PERMISSION_GRANTS.items():
        role = role_by_code[role_code]
        await session.execute(delete(SysRolePermission).where(SysRolePermission.role_id == role.id))
        for permission_code in sorted(set(permission_codes)):
            session.add(
                SysRolePermission(
                    role_id=role.id,
                    permission_id=permission_by_code[permission_code].id,
                )
            )
    await session.flush()


async def _seed_data_permissions(session, role_by_code: dict[str, SysRole]) -> None:
    for role_code, scopes in DATA_SCOPE_GRANTS.items():
        role = role_by_code[role_code]
        await session.execute(delete(SysDataPermission).where(SysDataPermission.role_id == role.id))
        for scope in scopes:
            session.add(
                SysDataPermission(
                    role_id=role.id,
                    scope_type=scope["scope_type"],
                    scope_value=scope["scope_value"],
                    enabled=True,
                )
            )
    await session.flush()


def _expand_menu_names(menu_names: list[str]) -> set[str]:
    parent_map: dict[str, str | None] = {}

    def register(seed: MenuSeed, parent_name: str | None = None) -> None:
        parent_map[seed.name] = parent_name
        for child in seed.children:
            register(child, seed.name)

    for seed in MENU_SEEDS:
        register(seed)

    expanded = set(menu_names)
    for menu_name in list(menu_names):
        current = parent_map.get(menu_name)
        while current is not None:
            expanded.add(current)
            current = parent_map.get(current)
    return expanded
