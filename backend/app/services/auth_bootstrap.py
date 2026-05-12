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
        name="Dashboard",
        path="/dashboard",
        component="/index/index",
        title_key="menus.dashboard.title",
        icon="ri:pie-chart-line",
        menu_type="directory",
        sort=1,
        children=[
            MenuSeed("Console", "console", "/dashboard/console", "menus.dashboard.console", icon="ri:home-smile-2-line", fixed_tab=True, sort=1),
            MenuSeed("Analysis", "analysis", "/dashboard/analysis", "menus.dashboard.analysis", icon="ri:align-item-bottom-line", sort=2),
            MenuSeed("Ecommerce", "ecommerce", "/dashboard/ecommerce", "menus.dashboard.ecommerce", icon="ri:bar-chart-box-line", sort=3),
        ],
    ),
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
                name="DeviceMonitorCard",
                path="monitor",
                component="/device_monitor/monitor/index",
                title_key="menus.deviceMonitor.monitor",
                icon="ri:dashboard-3-line",
                keep_alive=True,
                sort=2,
            ),
            MenuSeed(
                name="DriverManage",
                path="driver",
                component="/device_monitor/driver/index",
                title_key="menus.deviceMonitor.driver",
                icon="ri:plug-line",
                keep_alive=True,
                sort=3,
            ),
            MenuSeed(
                name="DeviceManage",
                path="device",
                component="/device_monitor/device/index",
                title_key="menus.deviceMonitor.device",
                icon="ri:server-line",
                keep_alive=True,
                sort=4,
            ),
            MenuSeed(
                name="PointManage",
                path="point",
                component="/device_monitor/point/index",
                title_key="menus.deviceMonitor.point",
                icon="ri:flashlight-line",
                keep_alive=True,
                sort=5,
            ),
            MenuSeed(
                name="RealtimeMonitor",
                path="realtime",
                component="/device_monitor/realtime/index",
                title_key="menus.deviceMonitor.realtime",
                icon="ri:pulse-line",
                keep_alive=False,
                sort=6,
            ),
            MenuSeed(
                name="HistoryData",
                path="history",
                component="/device_monitor/history/index",
                title_key="menus.deviceMonitor.history",
                icon="ri:line-chart-line",
                keep_alive=False,
                sort=7,
            ),
            MenuSeed(
                name="SystemLog",
                path="log",
                component="/device_monitor/log/index",
                title_key="menus.deviceMonitor.log",
                icon="ri:file-list-3-line",
                keep_alive=True,
                sort=8,
            ),
            MenuSeed(
                name="LogTerminal",
                path="log-terminal",
                component="/device_monitor/log-terminal/index",
                title_key="menus.deviceMonitor.logTerminal",
                icon="ri:terminal-line",
                keep_alive=False,
                sort=9,
            ),
        ],
    ),
    MenuSeed(
        name="Template",
        path="/template",
        component="/index/index",
        title_key="menus.template.title",
        icon="ri:apps-2-line",
        menu_type="directory",
        sort=20,
        children=[
            MenuSeed("Cards", "cards", "/template/cards", "menus.template.cards", icon="ri:wallet-line", sort=1),
            MenuSeed("Banners", "banners", "/template/banners", "menus.template.banners", icon="ri:rectangle-line", sort=2),
            MenuSeed("Charts", "charts", "/template/charts", "menus.template.charts", icon="ri:bar-chart-box-line", sort=3),
            MenuSeed("Map", "map", "/template/map", "menus.template.map", icon="ri:map-pin-line", keep_alive=True, sort=4),
            MenuSeed("Chat", "chat", "/template/chat", "menus.template.chat", icon="ri:message-3-line", keep_alive=True, sort=5),
            MenuSeed("Calendar", "calendar", "/template/calendar", "menus.template.calendar", icon="ri:calendar-2-line", keep_alive=True, sort=6),
            MenuSeed("Pricing", "pricing", "/template/pricing", "menus.template.pricing", icon="ri:money-cny-box-line", keep_alive=True, is_full_page=True, sort=7),
        ],
    ),
    MenuSeed(
        name="Widgets",
        path="/widgets",
        component="/index/index",
        title_key="menus.widgets.title",
        icon="ri:apps-2-add-line",
        menu_type="directory",
        sort=30,
        children=[
            MenuSeed("Icon", "icon", "/widgets/icon", "menus.widgets.icon", icon="ri:palette-line", keep_alive=True, sort=1),
            MenuSeed("ImageCrop", "image-crop", "/widgets/image-crop", "menus.widgets.imageCrop", icon="ri:screenshot-line", keep_alive=True, sort=2),
            MenuSeed("Excel", "excel", "/widgets/excel", "menus.widgets.excel", icon="ri:download-2-line", keep_alive=True, sort=3),
            MenuSeed("Video", "video", "/widgets/video", "menus.widgets.video", icon="ri:vidicon-line", keep_alive=True, sort=4),
            MenuSeed("CountTo", "count-to", "/widgets/count-to", "menus.widgets.countTo", icon="ri:anthropic-line", sort=5),
            MenuSeed("WangEditor", "wang-editor", "/widgets/wang-editor", "menus.widgets.wangEditor", icon="ri:t-box-line", keep_alive=True, sort=6),
            MenuSeed("Watermark", "watermark", "/widgets/watermark", "menus.widgets.watermark", icon="ri:water-flash-line", keep_alive=True, sort=7),
            MenuSeed("ContextMenu", "context-menu", "/widgets/context-menu", "menus.widgets.contextMenu", icon="ri:menu-2-line", keep_alive=True, sort=8),
            MenuSeed("Qrcode", "qrcode", "/widgets/qrcode", "menus.widgets.qrcode", icon="ri:qr-code-line", keep_alive=True, sort=9),
            MenuSeed("Drag", "drag", "/widgets/drag", "menus.widgets.drag", icon="ri:drag-move-fill", keep_alive=True, sort=10),
            MenuSeed("TextScroll", "text-scroll", "/widgets/text-scroll", "menus.widgets.textScroll", icon="ri:input-method-line", keep_alive=True, sort=11),
            MenuSeed("Fireworks", "fireworks", "/widgets/fireworks", "menus.widgets.fireworks", icon="ri:magic-line", keep_alive=True, sort=12),
            MenuSeed(
                "ElementUI",
                "/outside/iframe/elementui",
                "",
                "menus.widgets.elementUI",
                icon="ri:apps-2-line",
                link="https://element-plus.org/zh-CN/component/overview.html",
                is_iframe=True,
                sort=13,
            ),
        ],
    ),
    MenuSeed(
        name="Examples",
        path="/examples",
        component="/index/index",
        title_key="menus.examples.title",
        icon="ri:sparkling-line",
        menu_type="directory",
        sort=40,
        children=[
            MenuSeed(
                name="Permission",
                path="permission",
                component="",
                title_key="menus.examples.permission.title",
                icon="ri:fingerprint-line",
                menu_type="directory",
                sort=1,
                children=[
                    MenuSeed("PermissionSwitchRole", "switch-role", "/examples/permission/switch-role", "menus.examples.permission.switchRole", icon="ri:contacts-line", keep_alive=True, sort=1),
                    MenuSeed("PermissionButtonAuth", "button-auth", "/examples/permission/button-auth", "menus.examples.permission.buttonAuth", icon="ri:mouse-line", keep_alive=True, sort=2),
                    MenuSeed("PermissionPageVisibility", "page-visibility", "/examples/permission/page-visibility", "menus.examples.permission.pageVisibility", icon="ri:user-3-line", keep_alive=True, sort=3),
                ],
            ),
            MenuSeed("Tabs", "tabs", "/examples/tabs", "menus.examples.tabs", icon="ri:price-tag-line", sort=2),
            MenuSeed("TablesBasic", "tables/basic", "/examples/tables/basic", "menus.examples.tablesBasic", icon="ri:layout-grid-line", keep_alive=True, sort=3),
            MenuSeed("Tables", "tables", "/examples/tables", "menus.examples.tables", icon="ri:table-3", keep_alive=True, sort=4),
            MenuSeed("Forms", "forms", "/examples/forms", "menus.examples.forms", icon="ri:table-view", keep_alive=True, sort=5),
            MenuSeed("SearchBar", "form/search-bar", "/examples/forms/search-bar", "menus.examples.searchBar", icon="ri:table-line", keep_alive=True, sort=6),
            MenuSeed("TablesTree", "tables/tree", "/examples/tables/tree", "menus.examples.tablesTree", icon="ri:layout-2-line", keep_alive=True, sort=7),
            MenuSeed("SocketChat", "socket-chat", "/examples/socket-chat", "menus.examples.socketChat", icon="ri:shake-hands-line", keep_alive=True, sort=8),
        ],
    ),
    MenuSeed(
        name="Article",
        path="/article",
        component="/index/index",
        title_key="menus.article.title",
        icon="ri:book-2-line",
        menu_type="directory",
        sort=50,
        children=[
            MenuSeed("ArticleList", "article-list", "/article/list", "menus.article.articleList", icon="ri:article-line", keep_alive=True, sort=1),
            MenuSeed("ArticleDetail", "detail/:id", "/article/detail", "menus.article.articleDetail", is_hide=True, keep_alive=True, active_path="/article/article-list", sort=2),
            MenuSeed("ArticleComment", "comment", "/article/comment", "menus.article.comment", icon="ri:mail-line", keep_alive=True, sort=3),
            MenuSeed("ArticlePublish", "publish", "/article/publish", "menus.article.articlePublish", icon="ri:telegram-2-line", keep_alive=True, sort=4),
        ],
    ),
    MenuSeed(
        name="Result",
        path="/result",
        component="/index/index",
        title_key="menus.result.title",
        icon="ri:checkbox-circle-line",
        menu_type="directory",
        sort=60,
        children=[
            MenuSeed("ResultSuccess", "success", "/result/success", "menus.result.success", icon="ri:checkbox-circle-line", keep_alive=True, sort=1),
            MenuSeed("ResultFail", "fail", "/result/fail", "menus.result.fail", icon="ri:close-circle-line", keep_alive=True, sort=2),
        ],
    ),
    MenuSeed(
        name="Exception",
        path="/exception",
        component="/index/index",
        title_key="menus.exception.title",
        icon="ri:error-warning-line",
        menu_type="directory",
        sort=70,
        children=[
            MenuSeed("Exception403", "403", "/exception/403", "menus.exception.forbidden", keep_alive=True, is_hide_tab=True, is_full_page=True, sort=1),
            MenuSeed("Exception404", "404", "/exception/404", "menus.exception.notFound", keep_alive=True, is_hide_tab=True, is_full_page=True, sort=2),
            MenuSeed("Exception500", "500", "/exception/500", "menus.exception.serverError", keep_alive=True, is_hide_tab=True, is_full_page=True, sort=3),
        ],
    ),
    MenuSeed(
        name="Safeguard",
        path="/safeguard",
        component="/index/index",
        title_key="menus.safeguard.title",
        icon="ri:shield-check-line",
        menu_type="directory",
        sort=80,
        children=[
            MenuSeed("SafeguardServer", "server", "/safeguard/server", "menus.safeguard.server", icon="ri:hard-drive-3-line", keep_alive=True, sort=1),
        ],
    ),
    MenuSeed(
        name="ChangeLog",
        path="/change/log",
        component="/change/log",
        title_key="menus.plan.log",
        icon="ri:gamepad-line",
        sort=85,
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


def _collect_menu_names(seeds: list[MenuSeed]) -> list[str]:
    names: list[str] = []
    for seed in seeds:
        names.append(seed.name)
        names.extend(_collect_menu_names(seed.children))
    return names


PERMISSION_SEEDS = [
    PermissionSeed("device_monitor:dashboard:view", "工作台查看", "page", "MonitorDashboard", "device_monitor:dashboard:view", sort=10),
    PermissionSeed("device_monitor:monitor:view", "监控卡片查看", "page", "DeviceMonitorCard", "device_monitor:monitor:view", sort=15),
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
    PermissionSeed("device_monitor:log_terminal:view", "日志终端查看", "page", "LogTerminal", "device_monitor:log_terminal:view", sort=80),
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
    "R_SUPER": _collect_menu_names(MENU_SEEDS),
    "R_ADMIN": [
        "DeviceMonitor",
        "MonitorDashboard",
        "DeviceMonitorCard",
        "DriverManage",
        "DeviceManage",
        "PointManage",
        "RealtimeMonitor",
        "HistoryData",
        "SystemLog",
        "LogTerminal",
    ],
    "R_USER": [
        "DeviceMonitor",
        "MonitorDashboard",
        "RealtimeMonitor",
        "HistoryData",
        "SystemLog",
        "LogTerminal",
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
        )
    ],
    "R_USER": [
        "device_monitor:dashboard:view",
        "device_monitor:realtime:view",
        "device_monitor:realtime:subscribe",
        "device_monitor:realtime:refresh",
        "device_monitor:history:view",
        "device_monitor:log:view",
        "device_monitor:log_terminal:view",
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
        await _seed_role_menu_page_permissions(session)
        await _seed_data_permissions(session, role_by_code)
        await session.commit()

    logger.info("Auth schema bootstrap complete")


async def _seed_roles(session) -> None:
    """只新增缺失角色，不覆盖已有角色的任何字段"""
    for seed in ROLE_SEEDS:
        role = await session.scalar(select(SysRole).where(SysRole.role_code == seed["role_code"]))
        if role is None:
            role = SysRole(
                role_code=seed["role_code"],
                role_name=seed["role_name"],
                description=seed["description"],
                enabled=True,
                sort=seed["sort"],
            )
            session.add(role)
    await session.flush()


async def _seed_users(session) -> None:
    """只新增缺失用户，不覆盖已有用户的密码、状态、个人信息等字段"""
    default_password_hash = hash_password("123456")
    for seed in USER_SEEDS:
        user = await session.scalar(select(SysUser).where(SysUser.user_name == seed["user_name"]))
        is_new_user = user is None
        if is_new_user:
            user = SysUser(
                user_name=seed["user_name"],
                password_hash=default_password_hash,
                nick_name=seed["nick_name"],
                email=seed["email"],
                phone=seed["phone"],
                gender=seed["gender"],
                status=seed["status"],
                deleted=False,
                create_by="bootstrap",
                update_by="bootstrap",
            )
            session.add(user)
        else:
            # 已有用户：只补齐空字段，不覆盖已有值
            if seed["user_name"] == "Super":
                user.deleted = False
                user.status = seed["status"]
                user.update_by = "bootstrap"
            if not user.nick_name:
                user.nick_name = seed["nick_name"]
            if not user.email:
                user.email = seed["email"]
            if not user.phone:
                user.phone = seed["phone"]
            if not user.gender or user.gender == "unknown":
                user.gender = seed["gender"]
    await session.flush()


async def _seed_menus(session) -> dict[str, SysMenu]:
    """只新增缺失菜单，不覆盖已有菜单的任何字段"""
    menu_by_name: dict[str, SysMenu] = {}

    async def upsert(seed: MenuSeed, parent_id: int | None = None) -> None:
        menu = await session.scalar(select(SysMenu).where(SysMenu.name == seed.name))
        if menu is None:
            menu = SysMenu(
                name=seed.name,
                parent_id=parent_id,
                path=seed.path,
                component=seed.component,
                title_key=seed.title_key,
                icon=seed.icon,
                menu_type=seed.menu_type,
                link=seed.link,
                is_iframe=seed.is_iframe,
                keep_alive=seed.keep_alive,
                is_hide=seed.is_hide,
                is_hide_tab=seed.is_hide_tab,
                fixed_tab=seed.fixed_tab,
                active_path=seed.active_path,
                is_full_page=seed.is_full_page,
                sort=seed.sort,
                enabled=seed.enabled,
            )
            session.add(menu)
            await session.flush()
        menu_by_name[seed.name] = menu
        for child in seed.children:
            await upsert(child, menu.id)

    for seed in MENU_SEEDS:
        await upsert(seed)

    return menu_by_name


async def _seed_permissions(session, menu_by_name: dict[str, SysMenu]) -> dict[str, SysPermission]:
    """只新增缺失权限，不覆盖已有权限的任何字段"""
    permission_by_code: dict[str, SysPermission] = {}
    for seed in PERMISSION_SEEDS:
        permission = await session.scalar(
            select(SysPermission).where(SysPermission.permission_code == seed.permission_code)
        )
        if permission is None:
            permission = SysPermission(
                permission_code=seed.permission_code,
                permission_name=seed.permission_name,
                permission_type=seed.permission_type,
                menu_id=menu_by_name[seed.menu_name].id if seed.menu_name else None,
                auth_mark=seed.auth_mark,
                description=seed.description,
                sort=seed.sort,
                enabled=seed.enabled,
            )
            session.add(permission)
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
    """只补齐缺失的用户角色关系，不删除已有关系"""
    for seed in USER_SEEDS:
        user = user_by_name[seed["user_name"]]
        for role_code in seed["roles"]:
            role = role_by_code[role_code]
            existing = await session.scalar(
                select(SysUserRole).where(
                    SysUserRole.user_id == user.id,
                    SysUserRole.role_id == role.id,
                )
            )
            if existing is None:
                session.add(SysUserRole(user_id=user.id, role_id=role.id))
    await session.flush()


async def _seed_role_menus(session, role_by_code: dict[str, SysRole], menu_by_name: dict[str, SysMenu]) -> None:
    """只补齐缺失的角色菜单关系，不删除已有关系"""
    for role_code, menu_names in ROLE_MENU_GRANTS.items():
        role = role_by_code.get(role_code)
        if role is None:
            continue
        expanded_menu_names = _expand_menu_names(menu_names)
        for menu_name in expanded_menu_names:
            menu = menu_by_name.get(menu_name)
            if menu is None:
                continue
            existing = await session.scalar(
                select(SysRoleMenu).where(
                    SysRoleMenu.role_id == role.id,
                    SysRoleMenu.menu_id == menu.id,
                )
            )
            if existing is None:
                session.add(SysRoleMenu(role_id=role.id, menu_id=menu.id))
    await session.flush()


async def _seed_role_permissions(
    session,
    role_by_code: dict[str, SysRole],
    permission_by_code: dict[str, SysPermission],
) -> None:
    """只补齐缺失的角色权限关系，不删除已有关系"""
    for role_code, permission_codes in ROLE_PERMISSION_GRANTS.items():
        role = role_by_code.get(role_code)
        if role is None:
            continue
        for permission_code in set(permission_codes):
            permission = permission_by_code.get(permission_code)
            if permission is None:
                continue
            existing = await session.scalar(
                select(SysRolePermission).where(
                    SysRolePermission.role_id == role.id,
                    SysRolePermission.permission_id == permission.id,
                )
            )
            if existing is None:
                session.add(
                    SysRolePermission(
                        role_id=role.id,
                        permission_id=permission.id,
                    )
                )
    await session.flush()


async def _seed_role_menu_page_permissions(session) -> None:
    """按已有角色菜单关系补齐页面权限，不删除任何已有授权"""
    rows = (
        await session.execute(
            select(SysRoleMenu.role_id, SysPermission.id)
            .join(SysPermission, SysPermission.menu_id == SysRoleMenu.menu_id)
            .where(SysPermission.permission_type == "page")
            .where(SysPermission.enabled.is_(True))
        )
    ).all()
    for role_id, permission_id in rows:
        existing = await session.scalar(
            select(SysRolePermission).where(
                SysRolePermission.role_id == role_id,
                SysRolePermission.permission_id == permission_id,
            )
        )
        if existing is None:
            session.add(SysRolePermission(role_id=role_id, permission_id=permission_id))
    await session.flush()


async def _seed_data_permissions(session, role_by_code: dict[str, SysRole]) -> None:
    """只补齐缺失的数据权限，不删除已有数据权限"""
    for role_code, scopes in DATA_SCOPE_GRANTS.items():
        role = role_by_code.get(role_code)
        if role is None:
            continue
        for scope in scopes:
            existing = await session.scalar(
                select(SysDataPermission).where(
                    SysDataPermission.role_id == role.id,
                    SysDataPermission.scope_type == scope["scope_type"],
                    SysDataPermission.scope_value == scope["scope_value"],
                )
            )
            if existing is None:
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
