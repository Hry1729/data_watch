import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const mockSource = await readFile(new URL("../src/lib/mock-agent.ts", import.meta.url), "utf8");

const operations = [
  "userList", "roleOptions", "createUser", "updateUser", "updateUserStatus", "resetUserPassword", "deleteUser",
  "roleList", "createRole", "updateRole", "updateRoleStatus", "deleteRole", "roleGrant", "updateRoleGrant",
  "adminMenus", "createMenu", "updateMenu", "deleteMenu", "menuPermissions", "createMenuPermission", "updateMenuPermission", "deleteMenuPermission",
];

test("backend client exposes system-management operations in real, facade, and mock implementations", () => {
  for (const operation of operations) {
    assert.ok((clientSource.match(new RegExp(`${operation}\\(`, "g")) ?? []).length >= 3, operation);
    assert.match(mockSource, new RegExp(`async ${operation}\\(`), operation);
  }
});

test("system-management client uses existing backend routes", () => {
  for (const path of ["/api/user/list", "/api/user/", "/api/role/list", "/api/role/options", "/api/role/", "/api/v3/system/menus/admin", "/api/v3/system/menu"]) {
    assert.match(clientSource, new RegExp(path.replaceAll("/", "\\/")), path);
  }
});
