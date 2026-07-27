import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const accessSource = await readFile(new URL("../src/lib/navigation-access.ts", import.meta.url), "utf8");
const layoutSource = await readFile(new URL("../src/components/AppLayout.tsx", import.meta.url), "utf8");

test("settings is restricted to administrator roles", () => {
  const alwaysPaths = accessSource.match(/DESKTOP_ALWAYS_PATHS = \[([\s\S]*?)\]/)?.[1] ?? "";
  const adminPaths = accessSource.match(/ADMIN_EXTRA_PATHS = \[([\s\S]*?)\]/)?.[1] ?? "";
  assert.doesNotMatch(alwaysPaths, /"\/settings"/);
  assert.match(adminPaths, /"\/settings"/);
});

test("non-admin layout does not render settings entry points", () => {
  assert.match(layoutSource, /const canOpenSettings = auth\?\.allowedPaths\?\.includes\("\/settings"\) \?\? false/);
  assert.ok((layoutSource.match(/\{canOpenSettings && \(/g) ?? []).length >= 2);
});
