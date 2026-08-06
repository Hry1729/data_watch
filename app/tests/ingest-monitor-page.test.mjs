import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const [pageSource, appSource, navigationSource, settingsSource, accessSource, nodesPageSource] = await Promise.all([
  readFile(new URL("../src/pages/IngestPage.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/App.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/config/navigation.ts", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/SettingsPage.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/lib/navigation-access.ts", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/DistributedNodesPage.tsx", import.meta.url), "utf8"),
]);

test("distributed center exposes a dedicated ingest monitor page", () => {
  assert.match(appSource, /const IngestPage = __DATAWATCH_ROLE__ === "distributed-center"/);
  assert.match(appSource, /path="\/collection\/ingest" element=\{<IngestPage \/>\}/);
  assert.match(navigationSource, /label: "汇聚监控", path: "\/collection\/ingest"/);
  assert.match(accessSource, /DISTRIBUTED_CENTER_ALWAYS_PATHS = \[[\s\S]*"\/collection\/ingest"/);
});

test("distributed center administrators can open edge node management", () => {
  assert.match(appSource, /path="\/distributed\/nodes" element=\{<DistributedNodesPage \/>\}/);
  assert.match(navigationSource, /label: "边缘节点", path: "\/distributed\/nodes"/);
  assert.match(accessSource, /DISTRIBUTED_CENTER_ADMIN_EXTRA_PATHS = \[[\s\S]*"\/distributed\/nodes"/);
});

test("edge node management follows the workbench table pattern", () => {
  assert.ok(nodesPageSource.includes("<TooltipProvider delayDuration={200}>"));
  assert.ok(nodesPageSource.includes('<Table className="w-full table-fixed">'));
  assert.ok(nodesPageSource.includes('<TableHeader className="bg-muted/50">'));
  assert.ok(nodesPageSource.includes("每页行数"));
  assert.ok(nodesPageSource.includes('aria-label="最后一页"'));
});

test("route render errors return through the router without reloading auth state", () => {
  assert.match(appSource, /navigate\("\/dashboard\/workbench", \{ replace: true \}\)/);
  assert.doesNotMatch(appSource, /window\.location\.assign\("\/dashboard\/workbench"\)/);
});

test("ingest monitor uses live process metrics, stream rates, and logs", () => {
  assert.match(pageSource, /useModuleStatus\("ingest"\)/);
  assert.match(pageSource, /useWebSocketLog\("ingest"\)/);
  assert.match(pageSource, /title="汇聚入队趋势"/);
  assert.match(pageSource, /<LogPanel/);
  assert.match(pageSource, /grid-cols-2 gap-3 lg:grid-cols-5/);
  assert.doesNotMatch(pageSource, /ModuleHeaderCard/);
});

test("distributed center settings load ingest without requiring collector", () => {
  assert.match(settingsSource, /const collector = cfg\.collector \?\? DEFAULT_FORM\.collector/);
  assert.match(settingsSource, /const ingest = cfg\.ingest \?\? DEFAULT_FORM\.ingest/);
  assert.ok(settingsSource.includes('BUILD_ROLE === "distributed-center" ? ["ingest", "writer"]'));
});
