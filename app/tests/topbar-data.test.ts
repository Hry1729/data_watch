import assert from "node:assert/strict";
import test from "node:test";
import { searchPages, recentNotifications } from "../src/lib/topbar-data.ts";
import type { NavigationGroup } from "../src/config/navigation.ts";
import type { SystemLogItem } from "../src/types/log.ts";

test("page search only indexes supplied authorized pages and settings visibility", () => {
  const groups = [{ label: "设备监控", items: [{ label: "实时监控", path: "/device/realtime" }] }] as NavigationGroup[];
  assert.deepEqual(searchPages(groups, "实时", false).map(item => item.path), ["/device/realtime"]);
  assert.equal(searchPages(groups, "用户管理", false).length, 0);
  assert.equal(searchPages(groups, "设置", false).length, 0);
  assert.equal(searchPages(groups, "设置", true)[0]?.path, "/settings");
  assert.equal(searchPages(groups, "REALTIME", false)[0]?.path, "/device/realtime");
});

test("notification summary combines severities, keeps source IDs separate, and caps newest records", () => {
  const row = (id: number, kind = "system", level = "WARN") => ({ id, kind, level, time: `2026-10-15 12:00:${String(id).padStart(2, "0")}` }) as SystemLogItem;
  const items = recentNotifications([{ list: Array.from({ length: 30 }, (_, id) => row(id)) }, { list: [row(29), row(29, "operation", "ERROR"), row(59, "system", "INFO")] }]);
  assert.equal(items.length, 20);
  assert.equal(items.filter(item => item.id === 29).length, 2);
  assert.equal(items[0].id, 29);
  assert.equal(items.some(item => item.level === "INFO"), false);
});
