import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../src/hooks/useDashboardData.ts", import.meta.url), "utf8");

test("Workbench data hook loads every dashboard panel and refreshes every 30 seconds", () => {
  for (const method of [
    "dashboardOverview",
    "dashboardTrend",
    "dashboardDeviceTypeDistribution",
    "dashboardConnectionHealth",
    "dashboardRecentEvents",
  ]) {
    assert.match(source, new RegExp(`backendClient\\.${method}`));
  }
  assert.match(source, /30_000/);
  assert.match(source, /clearInterval/);
});

test("Workbench data hook logs out on unauthorized dashboard responses", () => {
  assert.match(source, /type === "unauthorized"/);
  assert.match(source, /logoutRef\.current\(\)/);
});

