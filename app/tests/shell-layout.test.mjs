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

test("sidebar collapse control lives inside the sidebar header", () => {
  assert.match(source, /aria-label=\{sidebarCollapsed \? "展开导航栏" : "折叠导航栏"\}/);
  assert.doesNotMatch(source, /<header[\s\S]{0,1200}aria-label=\{sidebarCollapsed/);
});

test("settings lives in the user popover instead of a standalone sidebar footer link", () => {
  assert.doesNotMatch(source, /<NavLink[\s\S]{0,180}to="\/settings"/);
  assert.match(source, /navigate\("\/settings\?tab=database"\)/);
  assert.match(source, /设置[\s\S]{0,500}退出登录/);
});

test("settings route renders back controls and centered section navigation in the top bar", () => {
  assert.match(source, /isSettingsRoute/);
  assert.match(source, /SETTINGS_TABS\.map/);
  assert.match(source, /aria-label="返回上一页"/);
  assert.match(source, /justify-self-center/);
});

