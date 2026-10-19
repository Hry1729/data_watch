import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const [
  app,
  navigation,
  access,
  layout,
  login,
  modules,
  operations,
  consolePage,
  settings,
  client,
  localRouter,
  viewer,
  rateHook,
  healthHook,
  outboxStore,
  localStore,
  monitorClient,
  edgePointMonitor,
  edgeBootstrap,
] = await Promise.all([
  readFile(new URL("../src/App.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/config/navigation.ts", import.meta.url), "utf8"),
  readFile(new URL("../src/lib/navigation-access.ts", import.meta.url), "utf8"),
  readFile(new URL("../src/components/AppLayout.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/LoginPage.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/EdgeModulePages.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/EdgeOperationsPage.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/EdgeConsolePage.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/EdgeSettingsPage.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/lib/agent-client.ts", import.meta.url), "utf8"),
  readFile(new URL("../../agent/app/routers/local.py", import.meta.url), "utf8"),
  readFile(new URL("../../agent/app/routers/viewer.py", import.meta.url), "utf8"),
  readFile(new URL("../src/hooks/useEdgeRates.ts", import.meta.url), "utf8"),
  readFile(new URL("../src/hooks/useConnectionHealth.ts", import.meta.url), "utf8"),
  readFile(new URL("../../edge/app/outbox.py", import.meta.url), "utf8"),
  readFile(new URL("../../agent/app/services/edge_local_store.py", import.meta.url), "utf8"),
  readFile(new URL("../src/lib/monitor-client.ts", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/EdgePointMonitorPage.tsx", import.meta.url), "utf8"),
  readFile(new URL("../src/pages/EdgeBootstrapPage.tsx", import.meta.url), "utf8"),
]);

test("edge build exposes console, overview, collector, uplink, and settings routes", () => {
  assert.ok(navigation.includes('label: "控制台", path: "/edge/console"'));
  assert.ok(navigation.includes('label: "运行总览", path: "/edge/operations"'));
  assert.ok(navigation.includes('label: "采集监控", path: "/edge/collector"'));
  assert.ok(navigation.includes('label: "存储监控", path: "/edge/storage"'));
  assert.ok(navigation.includes('label: "上行监控", path: "/edge/uploader"'));
  assert.ok(navigation.includes('label: "数据采集"'));
  assert.ok(access.includes('["/edge/console", "/settings"]'));
  assert.ok(app.includes('path="/edge/console" element={<EdgeConsolePage />}'));
  assert.ok(app.includes('path="/edge/collector" element={<EdgeCollectorPage />}'));
  assert.ok(app.includes('path="/edge/uploader" element={<EdgeUploaderPage />}'));
  assert.ok(app.includes('import("@/pages/EdgeSettingsPage")'));
  assert.ok(login.includes("buildAllowedPaths([], agentRoles)"));
  assert.ok(login.includes("agentClient.agentLogin(username, password)"));
});

test("edge build exposes center-first device monitoring with local SQLite fallback", () => {
  assert.ok(navigation.includes('label: "点位监控", path: "/device/points"'));
  assert.ok(navigation.includes('label: "实时监控", path: "/device/realtime"'));
  assert.ok(navigation.includes('label: "历史数据", path: "/device/history"'));
  assert.ok(app.includes('path="/device/points" element={<EdgePointMonitorPage />}'));
  assert.ok(app.includes('path="/device/realtime" element={<RealtimeMonitorPage />}'));
  assert.ok(app.includes('path="/device/history" element={<HistoryDataPage />}'));
  assert.ok(monitorClient.includes("centerFirst"));
  assert.ok(monitorClient.includes('appError?.type === "network_error"'));
  assert.ok(monitorClient.includes("appError.status >= 500"));
  assert.ok(edgePointMonitor.includes("monitorClient.pointList"));
  assert.ok(edgePointMonitor.includes("本机快照"));
  assert.ok(edgePointMonitor.includes("useAdaptivePageSize"));
  assert.ok(edgePointMonitor.includes("connectStatus"));
  assert.ok(edgePointMonitor.includes("点位详情"));
  assert.ok(!edgePointMonitor.includes("编辑点位"));
  assert.ok(!edgePointMonitor.includes("删除点位"));
  assert.ok(!edgePointMonitor.includes("新增点位"));
  assert.ok(localRouter.includes('alias="deviceIds"'));
  assert.ok(client.includes("query.append(key, String(item))"));
  assert.ok(localStore.includes('d.device_pk IN'));
  assert.ok(localRouter.includes('@router.get("/monitor/points")'));
  assert.ok(localRouter.includes('@router.post("/monitor/history/chart")'));
  assert.ok(monitorClient.includes("assignedScope"));
  assert.ok(monitorClient.includes("deviceIds: requested"));
  assert.ok(edgeBootstrap.includes("centerHost(value)"));
  assert.ok(edgeBootstrap.includes("DB_TARGETS.every"));
  assert.ok(edgeBootstrap.includes("postBootstrapTestConnection"));
});
test("edge database diagnostics apply successful values and refresh shell health", () => {
  assert.ok(layout.includes("createPortal(<ConnectionStatusBar />, titlebarStatusSlot)"));
  assert.ok(!layout.includes('BUILD_ROLE !== "edge" && ('));
  assert.ok(settings.includes("agentClient.checkHealth(target, payload)"));
  assert.ok(settings.includes("配置已应用"));
  assert.ok(settings.includes("CONNECTION_HEALTH_REFRESH_EVENT"));
  assert.ok(healthHook.includes("window.addEventListener(CONNECTION_HEALTH_REFRESH_EVENT"));
});

test("edge console owns controls and exposes collector and uplink trends", () => {
  assert.ok(consolePage.includes("useProcessControl(name, status.refresh)"));
  assert.ok(consolePage.includes("useEdgeRates()"));
  assert.ok(consolePage.includes('title="本地采集入队趋势"'));
  assert.ok(consolePage.includes('title="中心确认上行趋势"'));
  assert.ok(consolePage.includes("<ModuleSummaryCard"));
  assert.ok(consolePage.includes('className="grid gap-4 lg:grid-cols-3"'));
  assert.ok(consolePage.includes('className="mx-auto w-full max-w-[1400px] space-y-4"'));
  assert.ok(!consolePage.includes("        compact"));
});

test("edge monitor pages match writer layout without module header cards", () => {
  assert.ok(!modules.includes("ModuleHeaderCard"));
  assert.ok(modules.includes('grid-cols-2 gap-3 lg:grid-cols-5'));
  assert.ok(modules.includes('title="本地采集入队趋势"'));
  assert.ok(modules.includes('title="中心确认上行趋势"'));
  assert.ok(modules.includes("useWebSocketLog(name)"));
  assert.ok(modules.includes("snapshot.data?.fastest_point_scan_rate_ms"));
  assert.ok(modules.includes("设备默认周期"));
  assert.ok(operations.includes("设备默认周期"));
  assert.ok(modules.includes("outbox?.counts.inflight"));
  assert.ok(modules.includes("grid-rows-[300px_minmax(0,1fr)]"));
  assert.ok(!modules.includes("配置修订 {snapshot.data"));
  assert.ok(!modules.includes("节拍 {outbox"));
  assert.ok(!modules.includes("useProcessControl"));
  assert.ok(viewer.includes('"collector", "writer", "ingest", "uploader"'));
});

test("edge rates use durable local counters", () => {
  assert.ok(rateHook.includes("outbox.last_seq"));
  assert.ok(rateHook.includes("outbox.sent_records_total"));
  assert.ok(outboxStore.includes("sent_records_total"));
  assert.ok(localStore.includes('metadata.get("sent_records_total", "0")'));
  assert.ok(client.includes("sent_records_total: number"));
});

test("edge settings retain local policy behavior", () => {
  assert.ok(settings.includes("getLocalSettings()"));
  assert.ok(settings.includes("updateLocalSettings({"));
  assert.ok(settings.includes('getProcessStatus("uploader")'));
  assert.ok(client.includes('agentFetch<EdgeLocalSettings>("PUT", "/local/settings", settings)'));
  assert.ok(localRouter.includes("max_offline_retention_days: int = Field(ge=1, le=365)"));
  const start = localRouter.indexOf("async def update_local_settings");
  const end = localRouter.indexOf('@router.get("/devices")', start);
  assert.ok(!localRouter.slice(start, end).includes('edge["center_url"] ='));
});

test("edge operations uses compact six-row pagination without page scrolling", () => {
  assert.ok(operations.includes("const PAGE_SIZE = 6"));
  assert.ok(operations.includes('className="flex h-full min-h-0 flex-col gap-4 overflow-hidden"'));
  assert.ok(!operations.includes("min-h-[560px]"));
  assert.ok(operations.includes('<Table className="w-full table-fixed">'));
  assert.ok(operations.includes('<TableHeader className="bg-muted/50">'));
  assert.ok(operations.includes("配置修订"));
  assert.ok(operations.includes('<Card size="sm"'));
  assert.ok(operations.includes('className="flex min-h-24 items-center gap-3 py-2"'));
  assert.ok(operations.includes('aria-label="最后一页"'));
  assert.ok(!operations.includes("每页行数"));
  assert.ok(!operations.includes("RefreshCw"));
});
