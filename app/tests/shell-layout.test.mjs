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

test("application title and sidebar use white light-theme surfaces", async () => {
  const [titleBarSource, themeSource] = await Promise.all([
    readFile(new URL("../src/components/TitleBar.tsx", import.meta.url), "utf8"),
    readFile(new URL("../src/index.css", import.meta.url), "utf8"),
  ]);
  assert.match(titleBarSource, /bg-white[^"]*dark:bg-card/);
  assert.match(source, /bg-white[^"]*dark:bg-sidebar/);
  assert.match(source, /<header[^>]+bg-white[^>]+dark:bg-card/);
  assert.match(themeSource, /--sidebar:\s*oklch\(1 0 0\);/);
});

test("sidebar collapse control lives inside the sidebar header", () => {
  assert.match(source, /aria-label=\{sidebarCollapsed \? "展开导航栏" : "折叠导航栏"\}/);
  assert.doesNotMatch(source, /<header[\s\S]{0,1200}aria-label=\{sidebarCollapsed/);
});

test("settings lives in the user popover instead of a standalone sidebar footer link", () => {
  assert.doesNotMatch(source, /<NavLink[\s\S]{0,180}to="\/settings"/);
  assert.match(source, /const settingsPath = `\/settings\?tab=\$\{DEFAULT_SETTINGS_TAB\}`/);
  assert.match(source, /navigate\(settingsPath\)/);
  assert.match(source, /设置[\s\S]{0,500}退出登录/);
});

test("settings route renders back controls and section navigation in the application shell", () => {
  assert.match(source, /isSettingsRoute/);
  assert.match(source, /SETTINGS_TABS\.map/);
  assert.match(source, /aria-label="返回上一页"/);
  assert.match(source, /aria-label=\{isSettingsRoute \? "设置导航" : "主导航"\}/);
});
test("collapsed sidebar exposes every second-level route in a hover flyout", () => {
  assert.ok(source.includes("createPortal"));
  assert.ok(source.includes("onPointerEnter={(event)"));
  assert.ok(source.includes("collapsedFlyoutGroup.items.map"));
  assert.ok(source.includes("document.body"));
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
