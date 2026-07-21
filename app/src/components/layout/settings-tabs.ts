import type { LucideIcon } from "lucide-react";
import { Database, HardDrive, Radio, SlidersHorizontal } from "lucide-react";

export const SETTINGS_TABS = [
  { value: "basic", label: "基础", icon: SlidersHorizontal },
  { value: "database", label: "数据库", icon: Database },
  { value: "collector", label: "Collector", icon: Radio },
  { value: "writer", label: "Writer", icon: HardDrive },
] as const satisfies ReadonlyArray<{
  value: string;
  label: string;
  icon: LucideIcon;
}>;

export type SettingsTabValue = (typeof SETTINGS_TABS)[number]["value"];

export function normalizeSettingsTab(value: string | null): SettingsTabValue {
  return SETTINGS_TABS.some((tab) => tab.value === value)
    ? (value as SettingsTabValue)
    : "database";
}
