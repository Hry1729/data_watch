import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { getPageMeta, placeholderPages } from "../src/config/navigation.ts";

const pages = [
  ["UserManagementPage.tsx", "/system/users", ["新增用户", "用户名", "角色", "重置密码", "账号状态"]],
  ["RoleManagementPage.tsx", "/system/roles", ["新增角色", "角色名称", "角色编码", "菜单权限", "启用角色"]],
  ["MenuManagementPage.tsx", "/system/menus", ["添加菜单", "菜单名称", "按钮权限", "新增子菜单", "权限编码"]],
];

test("all three system-management routes are implemented rather than placeholders", () => {
  for (const [, path] of pages) {
    assert.equal(getPageMeta(path)?.kind, "existing", path);
    assert.equal(placeholderPages.some((page) => page.path === path), false, path);
  }
});

test("system-management pages contain their approved operational regions", async () => {
  for (const [file, , labels] of pages) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");
    for (const label of labels) assert.match(source, new RegExp(label), `${file}: ${label}`);
    assert.match(source, /<Card/);
    assert.match(source, /<Button/);
    assert.match(source, /<Dialog/);
  }
});

test("user management fills the content height and derives its page size from it", async () => {
  const source = await readFile(new URL("../src/pages/UserManagementPage.tsx", import.meta.url), "utf8");
  assert.match(source, /<Card className="flex min-h-0 flex-1 flex-col/);
  assert.match(source, /function fitPageSize\(/);
  assert.match(source, /new ResizeObserver\(/);
  assert.match(source, /useBackendQuery\(loadUsers, \{ enabled: sizeReady \}\)/);
});
