import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../src/components/AppLayout.tsx", import.meta.url), "utf8");

test("title bar uses the packaged Tauri application icon", async () => {
  const titleBarSource = await readFile(
    new URL("../src/components/TitleBar.tsx", import.meta.url),
    "utf8",
  );
  assert.match(titleBarSource, /src-tauri\/icons\/128x128\.png/);
  assert.match(titleBarSource, /<img[^>]+src=\{appIconUrl\}/);
});

test("application title bar and shell capsules use white light-theme surfaces", async () => {
  const [titleBarSource, themeSource] = await Promise.all([
    readFile(new URL("../src/components/TitleBar.tsx", import.meta.url), "utf8"),
    readFile(new URL("../src/index.css", import.meta.url), "utf8"),
  ]);
  assert.match(titleBarSource, /bg-white[^"]*dark:bg-card/);
  assert.match(source, /bg-white[^"]*dark:bg-sidebar/);
  // 顶栏不再铺毛玻璃：胶囊外改成一圈柔光阴影，停在页顶时只留卡片阴影，内容滚上来才淡入
  assert.doesNotMatch(source, /<header[^>]*backdrop-blur/);
  assert.doesNotMatch(source, /mask-composite/);
  assert.match(source, /function glowClass\(floating: boolean\)/);
  assert.match(source, /0_0_22px_2px_color-mix\(in_oklab,var\(--foreground\)_12%/);
  assert.match(source, /<aside className="relative flex w-12 shrink-0 flex-col">/);
  // 一级菜单钉在整个工作区高度的正中（不随顶栏偏移）
  assert.match(source, /absolute left-2\.5 top-1\/2 w-12 -translate-y-1\/2 flex-col/);
  assert.match(themeSource, /--sidebar:\s*oklch\(1 0 0\);/);
});

test("settings lives in the user popover instead of a standalone sidebar footer link", () => {
  assert.doesNotMatch(source, /<NavLink[\s\S]{0,180}to="\/settings"/);
  assert.match(source, /const settingsPath = `\/settings\?tab=\$\{DEFAULT_SETTINGS_TAB\}`/);
  assert.match(source, /navigate\(settingsPath\)/);
  assert.match(source, /设置[\s\S]{0,500}退出登录/);
});

test("settings route drives the section pills inside the application shell", () => {
  assert.match(source, /isSettingsRoute/);
  assert.match(source, /SETTINGS_TABS\.map/);
  assert.match(source, /isSettingsRoute \? "设置导航" : /);
  assert.match(source, /aria-label="主导航"/);
});
test("distributed assignment locks devices owned by other nodes and filters by type", async () => {
  const [pageSource, clientSource] = await Promise.all([
    readFile(new URL("../src/pages/DistributedNodesPage.tsx", import.meta.url), "utf8"),
    readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8"),
  ]);
  assert.ok(pageSource.includes('deviceList(token, { pageNo: 1, pageSize: 500 })'));
  assert.ok(pageSource.includes('device.deviceStatus === "enabled" || assignedIds.has(device.id)'));
  assert.ok(pageSource.includes("distributedAssignmentOccupancy(token)"));
  assert.ok(pageSource.includes("assignedElsewhere"));
  assert.ok(pageSource.includes("disabled={locked}"));
  assert.ok(pageSource.includes('assignedElsewhere ? "已分配"'));
  assert.ok(pageSource.includes('<TabsTrigger value="all">汇总</TabsTrigger>'));
  assert.ok(pageSource.includes("assignmentTypes.map"));
  assert.ok(pageSource.includes("同步设备启停状态"));
  assert.ok(clientSource.includes("/api/distributed/assignments/occupancy"));
  assert.ok(clientSource.includes("/api/distributed/nodes/sync-device-status"));
});
