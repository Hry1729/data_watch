import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const appSource = await readFile(new URL("../src/App.tsx", import.meta.url), "utf8");
const layoutSource = await readFile(
  new URL("../src/components/AppLayout.tsx", import.meta.url),
  "utf8",
);

test("protected application shell provides a current-page refresh action", () => {
  assert.match(appSource, /<PageRefreshProvider>/);
  assert.match(appSource, /<PageRefreshProvider>[\s\S]*<AppLayout \/>[\s\S]*<\/PageRefreshProvider>/);
  assert.match(layoutSource, /refreshCurrentPage/);
  assert.match(layoutSource, /aria-label="刷新当前页面"/);
  assert.match(layoutSource, /<RefreshCw/);
  assert.doesNotMatch(layoutSource, /window\.location\.reload/);
});

const sharedHookPaths = [
  "useBackendQuery.ts",
  "useDashboardData.ts",
  "useConnectionHealth.ts",
  "useModuleStatus.ts",
  "useModuleMetrics.ts",
  "useModuleRates.ts",
  "useModuleDbLogs.ts",
  "useRealtimeWebSocket.ts",
  "useWebSocketLog.ts",
];

test("shared data hooks respond to the current-page refresh version", async () => {
  for (const filename of sharedHookPaths) {
    const source = await readFile(
      new URL(`../src/hooks/${filename}`, import.meta.url),
      "utf8",
    );
    assert.match(source, /usePageRefresh/, `${filename} must consume PageRefreshContext`);
    assert.match(source, filename === "useDashboardData.ts" ? /useBackendQuery/ : /refreshVersion/, `${filename} must observe refresh directly or through useBackendQuery`);
  }
});

test("custom data pages refresh without discarding their current state", async () => {
  const [historySource, settingsSource] = await Promise.all([
    readFile(new URL("../src/pages/HistoryDataPage.tsx", import.meta.url), "utf8"),
    readFile(new URL("../src/pages/SettingsPage.tsx", import.meta.url), "utf8"),
  ]);

  assert.match(historySource, /usePageRefresh/);
  assert.match(historySource, /lastQueryRef/);
  assert.match(historySource, /refreshVersion/);

  assert.match(settingsSource, /usePageRefresh/);
  assert.match(settingsSource, /formDirtyRef/);
  assert.match(settingsSource, /streamMaxlenDirtyRef/);
  assert.match(settingsSource, /refreshVersion/);
});

test("backend polling serializes requests so slow responses cannot overlap", async () => {
  const source = await readFile(new URL("../src/hooks/useBackendQuery.ts", import.meta.url), "utf8");
  assert.match(source, /let inFlight = false/);
  assert.match(source, /if \(inFlight\) return/);
  assert.match(source, /inFlight = true/);
  assert.match(source, /inFlight = false/);
});
