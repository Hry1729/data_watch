import type { LucideIcon } from "lucide-react";
import { BUILD_ROLE } from "./build-role.ts";
import {
  Activity,
  BriefcaseBusiness,
  Cable,
  Cpu,
  Database,
  FileText,
  Gauge,
  HardDrive,
  HardDriveDownload,
  History,
  LayoutDashboard,
  MapPin,
  Menu,
  MonitorCog,
  Network,
  Puzzle,
  Radio,
  Settings2,
  Shield,
  Upload,
  Users,
} from "lucide-react";

export type PageKind = "existing" | "placeholder";

export interface NavigationItem {
  label: string;
  path: string;
  icon: LucideIcon;
  kind: PageKind;
}

export interface NavigationGroup {
  label: string;
  icon: LucideIcon;
  items: readonly NavigationItem[];
}

export interface PageMeta {
  title: string;
  groupLabel: string | null;
  kind: PageKind;
}

const allNavigationGroups = [
  {
    label: "仪表盘",
    icon: LayoutDashboard,
    items: [
      { label: "工作台", path: "/dashboard/workbench", icon: BriefcaseBusiness, kind: "existing" },
      { label: "控制台", path: "/dashboard/console", icon: Gauge, kind: "existing" },
    ],
  },
  {
    label: "数据采集",
    icon: Database,
    items: [
      { label: "采集监控", path: "/collection/collector", icon: Activity, kind: "existing" },
      { label: "汇聚监控", path: "/collection/ingest", icon: Network, kind: "existing" },
      { label: "入库监控", path: "/collection/writer", icon: HardDriveDownload, kind: "existing" },
    ],
  },
  {
    label: "设备监控",
    icon: MonitorCog,
    items: [
      { label: "设备通讯", path: "/device/communication", icon: Cable, kind: "existing" },
      { label: "驱动管理", path: "/device/drivers", icon: Puzzle, kind: "existing" },
      { label: "设备管理", path: "/device/devices", icon: Cpu, kind: "existing" },
      { label: "点位管理", path: "/device/points", icon: MapPin, kind: "existing" },
      { label: "实时监控", path: "/device/realtime", icon: Radio, kind: "existing" },
      { label: "历史数据", path: "/device/history", icon: History, kind: "existing" },
    ],
  },
  {
    label: "分布式",
    icon: Network,
    items: [
      { label: "边缘节点", path: "/distributed/nodes", icon: Network, kind: "existing" },
    ],
  },
  {
    label: "系统管理",
    icon: Settings2,
    items: [
      { label: "系统日志", path: "/logs/system", icon: FileText, kind: "existing" },
      { label: "用户管理", path: "/system/users", icon: Users, kind: "existing" },
      { label: "角色管理", path: "/system/roles", icon: Shield, kind: "existing" },
      { label: "菜单管理", path: "/system/menus", icon: Menu, kind: "existing" },
    ],
  },
] as const satisfies readonly NavigationGroup[];

const edgeNavigationGroups = [
  {
    label: "边缘节点",
    icon: MonitorCog,
    items: [
      { label: "控制台", path: "/edge/console", icon: Gauge, kind: "existing" },
      { label: "运行总览", path: "/edge/operations", icon: Activity, kind: "existing" },
    ],
  },
  {
    label: "数据采集",
    icon: Database,
    items: [
      { label: "采集监控", path: "/edge/collector", icon: Radio, kind: "existing" },
      { label: "存储监控", path: "/edge/storage", icon: HardDrive, kind: "existing" },
      { label: "上行监控", path: "/edge/uploader", icon: Upload, kind: "existing" },
    ],
  },
  {
    label: "设备监控",
    icon: MonitorCog,
    items: [
      { label: "点位监控", path: "/device/points", icon: MapPin, kind: "existing" },
      { label: "实时监控", path: "/device/realtime", icon: Radio, kind: "existing" },
      { label: "历史数据", path: "/device/history", icon: History, kind: "existing" },
    ],
  },
] as const satisfies readonly NavigationGroup[];
const hiddenPathsByRole = {
  centralized: new Set<string>(["/collection/ingest", "/distributed/nodes"]),
  edge: new Set([
    "/collection/writer",
    "/system/users",
    "/system/roles",
    "/system/menus",
  ]),
  "distributed-center": new Set([
    "/collection/collector",
  ]),
} as const;

export function isRouteEnabled(path: string): boolean {
  if (BUILD_ROLE === "edge") {
    return ["/edge/console", "/edge/operations", "/edge/collector", "/edge/storage", "/edge/uploader", "/device/points", "/device/realtime", "/device/history"].includes(path);
  }
  return !hiddenPathsByRole[BUILD_ROLE].has(path);
}

const roleNavigationGroups = BUILD_ROLE === "edge"
  ? edgeNavigationGroups
  : allNavigationGroups;

export const navigationGroups: readonly NavigationGroup[] = roleNavigationGroups
  .map((group) => ({
    ...group,
    items: group.items.filter((item) => isRouteEnabled(item.path)),
  }))
  .filter((group) => group.items.length > 0);

const allLegacyRedirects = [
  { from: "/", to: "/dashboard/workbench" },
  { from: "/collector", to: "/collection/collector" },
  { from: "/writer", to: "/collection/writer" },
  { from: "/logs/monitor", to: "/logs/system" },
] as const;

const defaultPath = BUILD_ROLE === "edge" ? "/edge/operations" : "/dashboard/workbench";

export const legacyRedirects = allLegacyRedirects.map((redirect) => (
  isRouteEnabled(redirect.to)
    ? redirect
    : { ...redirect, to: defaultPath }
));

export const placeholderPages = navigationGroups.flatMap((group) =>
  group.items
    .filter((item) => (item as NavigationItem).kind === "placeholder")
    .map((item) => ({ title: item.label, path: item.path })),
);

export function getPageMeta(pathname: string): PageMeta | null {
  if (pathname === "/settings") {
    return { title: "设置", groupLabel: null, kind: "existing" };
  }

  for (const group of navigationGroups) {
    const item = group.items.find((candidate) => candidate.path === pathname);
    if (item) {
      return {
        title: item.label,
        groupLabel: group.label,
        kind: item.kind,
      };
    }
  }

  return null;
}
