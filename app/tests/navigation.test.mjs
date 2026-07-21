import assert from "node:assert/strict";
import test from "node:test";

import {
  getPageMeta,
  legacyRedirects,
  navigationGroups,
  placeholderPages,
} from "../src/config/navigation.ts";

test("navigation groups match the approved desktop information architecture", () => {
  assert.deepEqual(
    navigationGroups.map((group) => ({
      label: group.label,
      items: group.items.map(({ label, path }) => ({ label, path })),
    })),
    [
      {
        label: "仪表盘",
        items: [
          { label: "工作台", path: "/dashboard/workbench" },
          { label: "控制台", path: "/dashboard/console" },
        ],
      },
      {
        label: "数据采集",
        items: [
          { label: "采集监控", path: "/collection/collector" },
          { label: "入库监控", path: "/collection/writer" },
        ],
      },
      {
        label: "设备监控",
        items: [
          { label: "设备通讯", path: "/device/communication" },
          { label: "驱动管理", path: "/device/drivers" },
          { label: "设备管理", path: "/device/devices" },
          { label: "点位管理", path: "/device/points" },
          { label: "实时监控", path: "/device/realtime" },
          { label: "历史数据", path: "/device/history" },
        ],
      },
      {
        label: "系统管理",
        items: [
          { label: "系统日志", path: "/logs/system" },
          { label: "用户管理", path: "/system/users" },
          { label: "角色管理", path: "/system/roles" },
          { label: "菜单管理", path: "/system/menus" },
        ],
      },
    ],
  );
});

test("existing pages and placeholders have explicit route metadata", () => {
  assert.deepEqual(getPageMeta("/dashboard/workbench"), {
    title: "工作台",
    groupLabel: "仪表盘",
    kind: "existing",
  });
  assert.deepEqual(getPageMeta("/dashboard/console"), {
    title: "控制台",
    groupLabel: "仪表盘",
    kind: "existing",
  });
  assert.deepEqual(getPageMeta("/collection/collector"), {
    title: "采集监控",
    groupLabel: "数据采集",
    kind: "existing",
  });
  assert.deepEqual(getPageMeta("/collection/writer"), {
    title: "入库监控",
    groupLabel: "数据采集",
    kind: "existing",
  });
  assert.deepEqual(getPageMeta("/logs/system"), {
    title: "系统日志",
    groupLabel: "系统管理",
    kind: "existing",
  });
  assert.deepEqual(getPageMeta("/settings"), {
    title: "设置",
    groupLabel: null,
    kind: "existing",
  });
  assert.equal(getPageMeta("/not-a-page"), null);

  assert.deepEqual(
    placeholderPages.map(({ title, path }) => ({ title, path })),
    [
    ],
  );
});

test("legacy desktop routes redirect to their new canonical locations", () => {
  assert.deepEqual(legacyRedirects, [
    { from: "/", to: "/dashboard/workbench" },
    { from: "/collector", to: "/collection/collector" },
    { from: "/writer", to: "/collection/writer" },
    { from: "/logs/monitor", to: "/logs/system" },
  ]);
});
