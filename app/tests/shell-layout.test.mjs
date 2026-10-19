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

test("title bar and sidebar share one continuous surface with the floating content panel", async () => {
  const [titleBarSource, themeSource] = await Promise.all([
    readFile(new URL("../src/components/TitleBar.tsx", import.meta.url), "utf8"),
    readFile(new URL("../src/index.css", import.meta.url), "utf8"),
  ]);
  assert.match(titleBarSource, /bg-sidebar/);
  assert.doesNotMatch(titleBarSource, /border-b/);
  assert.match(titleBarSource, /id=\{TITLEBAR_SLOT_ID\}/);
  assert.match(source, /titlebarSlot && createPortal\([\s\S]{0,700}折叠导航栏/);
  assert.match(source, /刷新当前页面[\s\S]{0,200}titlebarSlot,/);
  assert.doesNotMatch(source, /border-[bt] border-sidebar-border/);
  assert.doesNotMatch(source, /ChevronUp/);
  assert.match(source, /createPortal\(<ConnectionStatusBar \/>, titlebarStatusSlot\)/);
  assert.match(titleBarSource, /id=\{TITLEBAR_STATUS_SLOT_ID\}/);
  assert.doesNotMatch(titleBarSource, /Data Watch Console/);
  assert.doesNotMatch(source, /aria-label="设置"/);
  assert.match(source, /<ThemeToggle \/>/);
  assert.match(source, /flex h-full min-w-0 overflow-hidden bg-sidebar/);
  assert.ok(source.includes('sidebarCollapsed ? "w-16" : "w-60"'));
  assert.match(source, /readSidebarCollapsed/);
  assert.match(source, /<TopbarTools \/>/);
  assert.match(themeSource, /--sidebar:\s*#EFF1F4;/i);
});

test("settings lives in the user popover instead of a standalone sidebar footer link", () => {
  assert.doesNotMatch(source, /<NavLink[\s\S]{0,180}to="\/settings"/);
  assert.match(source, /const settingsPath = `\/settings\?tab=\$\{DEFAULT_SETTINGS_TAB\}`/);
  assert.match(source, /navigate\(settingsPath\)/);
  assert.match(source, /设置[\s\S]{0,500}退出登录/);
});

test("settings route drives the sidebar sections inside the application shell", () => {
  assert.match(source, /isSettingsRoute/);
  assert.match(source, /SETTINGS_TABS\.map/);
  assert.match(source, /isSettingsRoute \? "设置导航" : /);
  assert.match(source, /"设置导航" : "主导航"/);
});
test("distributed assignment locks devices owned by other nodes and filters by type", async () => {
  const [pageSource, clientSource] = await Promise.all([
    readFile(new URL("../src/pages/DistributedNodesPage.tsx", import.meta.url), "utf8"),
    readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8"),
  ]);
  assert.ok(pageSource.includes('useDevicePicker(backendClient, assignmentSearch, assignmentType'));
  assert.ok(pageSource.includes('device.deviceStatus === "enabled" || selectedIds.has(device.id)'));
  assert.ok(pageSource.includes("distributedAssignmentOccupancy(token, controller.signal)"));
  assert.ok(pageSource.includes("assignedElsewhere"));
  assert.ok(pageSource.includes("disabled={locked}"));
  assert.ok(pageSource.includes('assignedElsewhere ? "已分配"'));
  assert.ok(pageSource.includes('<TabsTrigger value="all">汇总</TabsTrigger>'));
  assert.ok(pageSource.includes("assignmentTypes.map"));
  assert.ok(pageSource.includes("同步设备启停状态"));
  assert.ok(clientSource.includes("/api/distributed/assignments/occupancy"));
  assert.ok(clientSource.includes("/api/distributed/nodes/sync-device-status"));
});
