import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { getPageMeta, placeholderPages } from "../src/config/navigation.ts";

test("system log route is implemented rather than a placeholder", () => {
  assert.equal(getPageMeta("/logs/system")?.kind, "existing");
  assert.equal(placeholderPages.some((page) => page.path === "/logs/system"), false);
});

test("system log page contains its approved operational regions", async () => {
  const source = await readFile(new URL("../src/pages/SystemLogPage.tsx", import.meta.url), "utf8");
  for (const label of ["今日日志", "日志级别趋势", "数据库日志", "高级查询", "清理日志", "DEBUG"]) {
    assert.match(source, new RegExp(label), label);
  }
  assert.match(source, /<Card/);
  assert.match(source, /<Button/);
});

test("the system log trend uses the shadcn chart container", async () => {
  const source = await readFile(new URL("../src/pages/SystemLogPage.tsx", import.meta.url), "utf8");
  assert.match(source, /<ChartContainer/);
});
