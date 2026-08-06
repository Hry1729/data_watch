import type { LucideIcon } from "lucide-react";
import { Database, HardDrive, MonitorCog, Network, Radio, SlidersHorizontal } from "lucide-react";
import { BUILD_ROLE } from "../../config/build-role.ts";

const primaryModuleTab = BUILD_ROLE === "distributed-center"
  ? { value: "ingest", label: "Ingest", icon: Network }
  : { value: "collector", label: "Collector", icon: Radio };

const centerSettingsTabs = [
  { value: "basic", label: "基础", icon: SlidersHorizontal },
  { value: "database", label: "数据库", icon: Database },
  primaryModuleTab,
  { value: "writer", label: "Writer", icon: HardDrive },
] as const satisfies ReadonlyArray<{
  value: string;
  label: string;
  icon: LucideIcon;
}>;

const edgeSettingsTabs = [
  { value: "edge", label: "边缘节点", icon: MonitorCog },
] as const satisfies ReadonlyArray<{
  value: string;
  label: string;
  icon: LucideIcon;
}>;

export const SETTINGS_TABS = BUILD_ROLE === "edge" ? edgeSettingsTabs : centerSettingsTabs;

export type SettingsTabValue = "basic" | "database" | "collector" | "ingest" | "writer" | "edge";
export const DEFAULT_SETTINGS_TAB: SettingsTabValue = BUILD_ROLE === "edge" ? "edge" : "database";

export function normalizeSettingsTab(value: string | null): SettingsTabValue {
  return SETTINGS_TABS.some((tab) => tab.value === value)
    ? (value as SettingsTabValue)
    : DEFAULT_SETTINGS_TAB;
}
