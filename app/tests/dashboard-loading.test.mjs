import assert from "node:assert/strict";
import test from "node:test";
import { deferred, hookHarness, settle } from "./helpers/hook-harness.mjs";

test("dashboard shows fast panels before a slow trend and reloads only the selected trend", async () => {
  const trend = deferred();
  const calls = {};
  const backendClient = Object.fromEntries([
    "dashboardOverview", "dashboardTrend", "dashboardDeviceTypeDistribution", "dashboardConnectionHealth", "dashboardRecentEvents",
  ].map(name => [name, () => {
    calls[name] = (calls[name] ?? 0) + 1;
    return name === "dashboardTrend" ? trend.promise : Promise.resolve({ marker: name });
  }]));
  const h = hookHarness("hooks/useDashboardData.ts", "useDashboardData", { "@/lib/backend-client": { backendClient } });
  try {
    await settle();
    assert.equal(h.value.overview?.marker, "dashboardOverview");
    assert.equal(h.value.loading, false);
    assert.equal(h.value.panelLoading.trend, true);
    h.value.setTrendRange("today");
    await settle();
    assert.equal(calls.dashboardTrend, 2);
    assert.equal(calls.dashboardOverview, 1);
    assert.equal(calls.dashboardRecentEvents ?? 0, 0);
    trend.resolve({ marker: "trend" });
    await settle();
    assert.equal(h.value.panelLoading.trend, false);
  } finally { h.unmount(); }
});

test("dashboard refresh reloads all panels and unauthorized errors still log out", async () => {
  let refreshVersion = 0, calls = 0, logouts = 0;
  const refreshCurrentPage = () => { refreshVersion++; };
  const backendClient = {
    dashboardOverview: async () => { calls++; return {}; },
    dashboardTrend: async () => { calls++; return {}; },
    dashboardDeviceTypeDistribution: async () => { calls++; return {}; },
    dashboardConnectionHealth: async () => { calls++; throw { type: "unauthorized" }; },
  };
  const h = hookHarness("hooks/useDashboardData.ts", "useDashboardData", {
    "@/lib/backend-client": { backendClient },
    "@/contexts/AuthContext": { useAuth: () => ({ auth: { backendToken: "test" }, logout() { logouts++; } }) },
    "@/contexts/PageRefreshContext": { usePageRefresh: () => ({ refreshVersion, refreshCurrentPage }) },
  });
  try {
    await settle();
    assert.equal(logouts, 1);
    assert.equal(calls, 4);
    h.value.refresh(); h.render();
    await settle();
    assert.equal(calls, 8);
    assert.equal(logouts, 2);
  } finally { h.unmount(); }
});
