import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const mockSource = await readFile(new URL("../src/lib/mock-agent.ts", import.meta.url), "utf8");
const typeSource = await readFile(new URL("../src/types/index.ts", import.meta.url), "utf8");

const methods = [
  "dashboardOverview",
  "dashboardTrend",
  "dashboardDeviceTypeDistribution",
  "dashboardConnectionHealth",
  "dashboardRecentEvents",
];

test("backend client exposes all Workbench dashboard operations in real, facade, and mock implementations", () => {
  for (const method of methods) {
    assert.ok((clientSource.match(new RegExp(`${method}\\(`, "g")) ?? []).length >= 3, method);
    assert.match(mockSource, new RegExp(`async ${method}\\(`), method);
  }
});

test("dashboard client uses the existing authenticated backend routes", () => {
  for (const path of [
    "/api/device-monitor/dashboard/overview",
    "/api/device-monitor/dashboard/trend",
    "/api/device-monitor/dashboard/device-type-distribution",
    "/api/device-monitor/dashboard/device-connection-health",
    "/api/device-monitor/dashboard/recent-events",
  ]) {
    assert.match(clientSource, new RegExp(path.replaceAll("/", "\\/")));
  }
});

test("dashboard response contracts are represented by explicit TypeScript interfaces", () => {
  for (const typeName of [
    "DashboardOverview",
    "DashboardTrend",
    "DashboardDeviceTypeDistribution",
    "DashboardConnectionHealth",
    "DashboardRecentEventList",
  ]) {
    assert.match(typeSource, new RegExp(`export interface ${typeName}`));
  }
});

