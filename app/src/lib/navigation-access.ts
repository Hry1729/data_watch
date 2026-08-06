import type { MenuTreeItem } from "@/types/system-management";
import {
  navigationGroups,
  type NavigationGroup,
} from "@/config/navigation";
import { BUILD_ROLE } from "@/config/build-role";

/** 桌面专有页：任意登录用户可见（方案 A） */
export const DESKTOP_ALWAYS_PATHS = [
  "/collection/collector",
  "/collection/writer",
] as const;
export const DISTRIBUTED_CENTER_ALWAYS_PATHS = [
  "/collection/ingest",
  "/collection/writer",
] as const;
const EDGE_ALWAYS_PATHS = [
  "/edge/operations",
  "/edge/collector",
  "/edge/uploader",
  "/device/points",
  "/device/realtime",
  "/device/history",
] as const;
const activeAlwaysPaths: readonly string[] = BUILD_ROLE === "edge"
  ? EDGE_ALWAYS_PATHS
  : BUILD_ROLE === "distributed-center"
    ? DISTRIBUTED_CENTER_ALWAYS_PATHS
    : DESKTOP_ALWAYS_PATHS;

/** 管理员及以上额外可见页（控制台等） */
export const ADMIN_ROLES = ["R_SUPER", "R_ADMIN"] as const;
export const ADMIN_EXTRA_PATHS = ["/dashboard/console", "/settings"] as const;
const DISTRIBUTED_CENTER_ADMIN_EXTRA_PATHS = [
  ...ADMIN_EXTRA_PATHS,
  "/distributed/nodes",
] as const;
const activeAdminExtraPaths: readonly string[] = BUILD_ROLE === "edge"
  ? ["/edge/console", "/settings"]
  : BUILD_ROLE === "distributed-center"
    ? DISTRIBUTED_CENTER_ADMIN_EXTRA_PATHS
    : ADMIN_EXTRA_PATHS;
function isAdminRole(roles: string[] | undefined): boolean {
  if (!roles?.length) return false;
  return roles.some((role) => (ADMIN_ROLES as readonly string[]).includes(role));
}

/** Web 菜单 component → 桌面路由 path */
const COMPONENT_TO_DESKTOP_PATH: Record<string, string> = {
  "/device_monitor/dashboard/index": "/dashboard/workbench",
  "/device_monitor/monitor/index": "/device/communication",
  "/device_monitor/driver/index": "/device/drivers",
  "/device_monitor/device/index": "/device/devices",
  "/device_monitor/point/index": "/device/points",
  "/device_monitor/realtime/index": "/device/realtime",
  "/device_monitor/history/index": "/device/history",
  "/device_monitor/log/index": "/logs/system",
  "/system/user": "/system/users",
  "/system/user/index": "/system/users",
  "/system/role": "/system/roles",
  "/system/role/index": "/system/roles",
  "/system/menu": "/system/menus",
  "/system/menu/index": "/system/menus",
  // 桌面侧已落地的路由若写进菜单也可直接命中
  "/dashboard/workbench": "/dashboard/workbench",
  "/dashboard/console": "/dashboard/console",
  "/device/communication": "/device/communication",
  "/device/drivers": "/device/drivers",
  "/device/devices": "/device/devices",
  "/device/points": "/device/points",
  "/device/realtime": "/device/realtime",
  "/device/history": "/device/history",
  "/logs/system": "/logs/system",
  "/system/users": "/system/users",
  "/system/roles": "/system/roles",
  "/system/menus": "/system/menus",
};

function normalizeComponent(component: string): string {
  const trimmed = component.trim();
  if (!trimmed) return "";
  return trimmed.startsWith("/") ? trimmed : `/${trimmed}`;
}

function collectDesktopPathsFromMenus(menus: MenuTreeItem[], out: Set<string>) {
  for (const menu of menus) {
    if (menu.enabled === false || menu.meta?.isHide) {
      if (menu.children?.length) collectDesktopPathsFromMenus(menu.children, out);
      continue;
    }
    const component = normalizeComponent(menu.component ?? "");
    const mapped = component ? COMPONENT_TO_DESKTOP_PATH[component] : undefined;
    if (mapped) out.add(mapped);
    // 兼容菜单 path 直接写成桌面 path
    const rawPath = (menu.path ?? "").trim();
    if (rawPath.startsWith("/") && COMPONENT_TO_DESKTOP_PATH[rawPath]) {
      out.add(COMPONENT_TO_DESKTOP_PATH[rawPath]);
    }
    if (menu.children?.length) collectDesktopPathsFromMenus(menu.children, out);
  }
}

/** 根据后端菜单树生成桌面可访问 path（含桌面专有页；管理员另含控制台） */
export function buildAllowedPaths(menus: MenuTreeItem[], roles: string[] = []): string[] {
  const paths = new Set<string>(activeAlwaysPaths);
  collectDesktopPathsFromMenus(menus, paths);
  if (isAdminRole(roles)) {
    for (const path of activeAdminExtraPaths) paths.add(path);
  }
  return Array.from(paths);
}

export function canAccessPath(
  pathname: string,
  allowedPaths: string[] | undefined,
  roles?: string[],
): boolean {
  if (!allowedPaths) return true;
  if (activeAlwaysPaths.includes(pathname)) {
    return true;
  }
  if (
    activeAdminExtraPaths.includes(pathname)
    && isAdminRole(roles)
  ) {
    return true;
  }
  return allowedPaths.includes(pathname);
}

export function pickFallbackPath(allowedPaths: string[] | undefined): string {
  const defaultPath = BUILD_ROLE === "edge" ? "/edge/operations" : "/dashboard/workbench";
  if (!allowedPaths?.length) return defaultPath;
  if (allowedPaths.includes("/dashboard/workbench")) return "/dashboard/workbench";
  if (allowedPaths.includes("/collection/collector")) return "/collection/collector";
  if (allowedPaths.includes("/collection/ingest")) return "/collection/ingest";
  return allowedPaths[0] ?? defaultPath;
}

/** 按权限过滤侧栏分组；空分组丢弃 */
export function filterNavigationGroups(
  allowedPaths: string[] | undefined,
): NavigationGroup[] {
  if (!allowedPaths) return [...navigationGroups];
  const allowed = new Set(allowedPaths);
  return navigationGroups
    .map((group) => ({
      ...group,
      items: group.items.filter((item) => allowed.has(item.path)),
    }))
    .filter((group) => group.items.length > 0);
}
