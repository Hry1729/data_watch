import type { LucideIcon } from "lucide-react";
import {
  Activity,
  BriefcaseBusiness,
  Cable,
  Cpu,
  Database,
  FileText,
  Gauge,
  HardDriveDownload,
  History,
  LayoutDashboard,
  MapPin,
  Menu,
  MonitorCog,
  Puzzle,
  Radio,
  Settings2,
  Shield,
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

export const navigationGroups = [
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

export const legacyRedirects = [
  { from: "/", to: "/dashboard/workbench" },
  { from: "/collector", to: "/collection/collector" },
  { from: "/writer", to: "/collection/writer" },
  { from: "/logs/monitor", to: "/logs/system" },
] as const;

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
