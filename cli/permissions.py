from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Iterable


@dataclass(frozen=True)
class CommandPolicy:
    command: str
    authenticated: bool = False
    permissions_any: frozenset[str] = frozenset()
    roles_any: frozenset[str] = frozenset()


DASHBOARD_VIEW = frozenset({"device_monitor:dashboard:view"})
DEVICE_VIEW = frozenset({"device_monitor:device:view"})
POINT_VIEW = frozenset({"device_monitor:point:view"})
HISTORY_VIEW = frozenset({"device_monitor:history:view"})
MONITOR_VIEW = frozenset(
    {"device_monitor:monitor:view", "device_monitor:realtime:view"}
)
NODE_ADMIN_ROLES = frozenset({"R_ADMIN", "R_SUPER"})


def _permission_policies(
    commands: Iterable[str], *permissions: str
) -> tuple[CommandPolicy, ...]:
    required = frozenset(permissions)
    return tuple(
        CommandPolicy(command, authenticated=True, permissions_any=required)
        for command in commands
    )


M1_COMMAND_POLICIES = (
    CommandPolicy("config show"),
    CommandPolicy("config set-url"),
    CommandPolicy("config profile list"),
    CommandPolicy("config profile create"),
    CommandPolicy("config profile update"),
    CommandPolicy("config profile delete"),
    CommandPolicy("config profile use"),
    CommandPolicy("auth login"),
    CommandPolicy("auth refresh", authenticated=True),
    CommandPolicy("auth whoami", authenticated=True),
    CommandPolicy("auth capabilities", authenticated=True),
    CommandPolicy("auth logout", authenticated=True),
    CommandPolicy("system health"),
    CommandPolicy("system compatibility"),
    CommandPolicy("doctor"),
    CommandPolicy("commands list"),
    CommandPolicy("commands schema"),
    CommandPolicy("update check"),
    CommandPolicy("update download"),
    CommandPolicy("update apply"),
    CommandPolicy("update rollback"),
    CommandPolicy("version"),
)

M2_A_COMMAND_POLICIES = _permission_policies(
    (
        "dashboard overview",
        "dashboard trend",
        "dashboard device-types",
        "dashboard connection-health",
        "dashboard pipeline-health",
    ),
    *DASHBOARD_VIEW,
) + (
    CommandPolicy("devices status", authenticated=True, permissions_any=MONITOR_VIEW),
    CommandPolicy("devices list", authenticated=True, permissions_any=DEVICE_VIEW),
    CommandPolicy("devices get", authenticated=True, permissions_any=DEVICE_VIEW),
    CommandPolicy("devices groups", authenticated=True, permissions_any=DEVICE_VIEW),
    CommandPolicy("points list", authenticated=True, permissions_any=POINT_VIEW),
    CommandPolicy("points get", authenticated=True, permissions_any=POINT_VIEW),
    CommandPolicy(
        "points resolve",
        authenticated=True,
        permissions_any=frozenset(
            {
                "device_monitor:point:view",
                "device_monitor:history:view",
                "device_monitor:monitor:view",
                "device_monitor:realtime:view",
            }
        ),
    ),
    CommandPolicy("points groups", authenticated=True, permissions_any=DEVICE_VIEW),
    CommandPolicy("points variables", authenticated=True, permissions_any=DEVICE_VIEW),
)

M2_B_COMMAND_POLICIES = _permission_policies(
    (
        "history devices",
        "history device-types",
        "history points",
        "history query",
        "history chart",
        "history raw",
        "history matrix",
    ),
    *HISTORY_VIEW,
) + _permission_policies(
    ("realtime tree", "realtime get", "realtime window", "realtime watch"), *MONITOR_VIEW
)

M2_C_COMMAND_POLICIES = (
    CommandPolicy(
        "system-alerts summary", authenticated=True, permissions_any=DASHBOARD_VIEW
    ),
    CommandPolicy(
        "system-alerts list",
        authenticated=True,
        permissions_any=frozenset({"device_monitor:log:view"}),
    ),
) + _permission_policies(
    (
        "modules status",
        "modules collector",
        "modules writer",
        "modules rates",
        "modules collector-config show",
    ),
    *MONITOR_VIEW,
) + _permission_policies(
    ("drivers list", "drivers options"), "device_monitor:driver:view"
) + tuple(
    CommandPolicy(command, authenticated=True, roles_any=NODE_ADMIN_ROLES)
    for command in ("nodes list", "nodes occupancy", "nodes assignments")
) + _permission_policies(
    (
        "logs list",
        "logs stats",
        "logs tdengine-files",
        "logs tdengine-tail",
        "logs policy show",
    ),
    "device_monitor:log:view",
)

M3_A_COMMAND_POLICIES = (
    _permission_policies(("devices create",), "device_monitor:device:create")
    + _permission_policies(
        ("devices update", "devices enable", "devices disable"),
        "device_monitor:device:update",
    )
    + _permission_policies(("devices import",), "device_monitor:device:import")
    + _permission_policies(("devices export",), "device_monitor:device:export")
    + _permission_policies(
        ("devices delete", "devices delete-batch"),
        "device_monitor:device:delete",
    )
    + _permission_policies(("points create",), "device_monitor:point:create")
    + _permission_policies(
        ("points update", "points enable", "points disable"),
        "device_monitor:point:update",
    )
    + _permission_policies(("points import",), "device_monitor:point:import")
    + _permission_policies(
        ("points template", "points export"), "device_monitor:point:export"
    )
    + _permission_policies(
        ("points delete", "points delete-batch"), "device_monitor:point:delete"
    )
    + _permission_policies(
        ("history export", "history export-status", "history export-download"),
        "device_monitor:history:export",
    )
    + _permission_policies(
        ("drivers register", "drivers uninstall"), "device_monitor:driver:manage"
    )
    + _permission_policies(
        ("drivers uninstall-status",), "device_monitor:driver:view"
    )
)

M3_B_COMMAND_POLICIES = tuple(
    CommandPolicy(command, authenticated=True, roles_any=NODE_ADMIN_ROLES)
    for command in (
        "nodes assign",
        "nodes unassign",
        "nodes assign-set",
        "nodes enable",
        "nodes disable",
        "nodes settings",
        "nodes registration-code",
        "nodes sync-device-status",
    )
) + (
    CommandPolicy(
        "modules collector-config set",
        authenticated=True,
        permissions_any=frozenset({"device_monitor:collector_config:update"}),
    ),
) + _permission_policies(
    ("logs policy set",), "device_monitor:log:policy"
) + _permission_policies(
    ("logs debug enable", "logs debug disable"), "device_monitor:log:debug"
) + _permission_policies(
    ("logs cleanup",), "device_monitor:log:cleanup"
) + (
    CommandPolicy("auth change-password", authenticated=True),
) + _permission_policies(
    ("users list", "users get"), "system:user:view"
) + _permission_policies(
    ("users create",), "system:user:create"
) + _permission_policies(
    ("users update", "users enable", "users disable"), "system:user:update"
) + _permission_policies(
    ("users reset-password",), "system:user:password_reset"
) + _permission_policies(
    ("users delete",), "system:user:delete"
) + (
    CommandPolicy(
        "roles list",
        authenticated=True,
        permissions_any=frozenset({"system:role:view", "system:role:grant"}),
    ),
    CommandPolicy(
        "roles options",
        authenticated=True,
        permissions_any=frozenset(
            {"system:role:view", "system:user:create", "system:user:update"}
        ),
    ),
) + _permission_policies(
    ("roles get", "roles grants get"), "system:role:view"
) + _permission_policies(
    ("roles create",), "system:role:create"
) + _permission_policies(
    ("roles update", "roles enable", "roles disable"), "system:role:update"
) + _permission_policies(
    ("roles delete",), "system:role:delete"
) + _permission_policies(
    ("roles grants set",), "system:role:grant"
) + (
    CommandPolicy("menus current", authenticated=True),
    CommandPolicy(
        "menus list",
        authenticated=True,
        permissions_any=frozenset({"system:menu:view", "system:role:grant"}),
    ),
) + _permission_policies(
    ("menus create", "menus permissions create"), "system:menu:create"
) + _permission_policies(
    ("menus update", "menus permissions update"), "system:menu:update"
) + _permission_policies(
    ("menus delete", "menus permissions delete"), "system:menu:delete"
) + _permission_policies(
    ("menus permissions list",), "system:menu:view"
)

COMMAND_POLICIES = (
    M1_COMMAND_POLICIES
    + M2_A_COMMAND_POLICIES
    + M2_B_COMMAND_POLICIES
    + M2_C_COMMAND_POLICIES
    + M3_A_COMMAND_POLICIES
    + M3_B_COMMAND_POLICIES
)
COMMAND_POLICY_MAP = {policy.command: policy for policy in COMMAND_POLICIES}

if len(COMMAND_POLICY_MAP) != len(COMMAND_POLICIES):
    raise RuntimeError("Duplicate CLI command permission policy")


def get_command_policy(command: str) -> CommandPolicy | None:
    return COMMAND_POLICY_MAP.get(command)


def is_command_allowed(policy: CommandPolicy, user_info: dict[str, Any] | None) -> bool:
    if policy.authenticated and user_info is None:
        return False
    if user_info is None:
        return not policy.permissions_any and not policy.roles_any

    permissions = _as_set(user_info.get("permissions"))
    roles = _as_set(user_info.get("roles"))
    if policy.permissions_any and not policy.permissions_any.intersection(permissions):
        return False
    if policy.roles_any and not policy.roles_any.intersection(roles):
        return False
    return True


def build_capabilities(
    user_info: dict[str, Any],
    policies: Iterable[CommandPolicy] = COMMAND_POLICIES,
) -> dict[str, Any]:
    available: list[str] = []
    denied: list[dict[str, Any]] = []
    for policy in policies:
        if is_command_allowed(policy, user_info):
            available.append(policy.command)
            continue
        denied.append(
            {
                "command": policy.command,
                "requiredPermissionsAny": sorted(policy.permissions_any),
                "requiredRolesAny": sorted(policy.roles_any),
            }
        )
    return {
        "userName": user_info.get("userName"),
        "roles": sorted(_as_set(user_info.get("roles"))),
        "permissions": sorted(_as_set(user_info.get("permissions"))),
        "availableCommands": available,
        "deniedCommands": denied,
    }


def _as_set(value: Any) -> set[str]:
    if not isinstance(value, (list, tuple, set, frozenset)):
        return set()
    return {str(item) for item in value if str(item).strip()}
