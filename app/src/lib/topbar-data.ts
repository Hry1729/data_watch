import type { NavigationGroup } from "../config/navigation";
import type { SystemLogItem } from "../types/log";

export interface PageSearchItem { label: string; path: string; group: string }

export function searchPages(groups: readonly NavigationGroup[], keyword: string, settings: boolean): PageSearchItem[] {
  const items = groups.flatMap(group => group.items.map(item => ({ label: item.label, path: item.path, group: group.label })));
  items.push({ label: "帮助", path: "/help", group: "应用" });
  if (settings) items.push({ label: "设置", path: "/settings", group: "应用" });
  const query = keyword.trim().toLocaleLowerCase();
  return items.filter(item => `${item.label} ${item.group} ${item.path}`.toLocaleLowerCase().includes(query));
}

export function recentNotifications(pages: { list: SystemLogItem[] }[]): SystemLogItem[] {
  const items = new Map<string, SystemLogItem>();
  for (const page of pages) for (const item of page.list) {
    if (["WARN", "ERROR", "CRITICAL"].includes(item.level)) items.set(`${item.kind}:${item.id}`, item);
  }
  return [...items.values()].sort((a, b) => b.time.localeCompare(a.time)).slice(0, 20);
}
