import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { getPageMeta, placeholderPages } from "../src/config/navigation.ts";

const source = await readFile(new URL("../src/pages/WorkbenchPage.tsx", import.meta.url), "utf8");
const appSource = await readFile(new URL("../src/App.tsx", import.meta.url), "utf8");
const themeSource = await readFile(new URL("../src/index.css", import.meta.url), "utf8");
const pipelineSource = await readFile(new URL("../src/hooks/usePipelineHealth.ts", import.meta.url), "utf8");
const chartSource = await readFile(new URL("../src/components/ui/chart.tsx", import.meta.url), "utf8");

test("Workbench is an implemented route rather than a placeholder", () => {
  assert.equal(getPageMeta("/dashboard/workbench")?.kind, "existing");
  assert.equal(placeholderPages.some((page) => page.path === "/dashboard/workbench"), false);
  assert.match(appSource, /path="\/dashboard\/workbench" element=\{<WorkbenchPage \/>\}/);
});

test("Workbench chart bundle is loaded only when its route is opened", () => {
  assert.match(appSource, /lazy\(\(\) => import\("@\/pages\/WorkbenchPage"\)\)/);
  assert.match(appSource, /<Suspense/);
});

test("Workbench contains the approved operational dashboard regions", () => {
  for (const label of [
    "设备总数",
    "在线率",
    "采集点位",
    "近24小时系统告警",
    "采集量 / 在线设备 / 告警趋势",
    "设备通讯概览",
    "设备类型分布",
    "数据链路健康",
    "最近系统日志",
  ]) {
    assert.match(source, new RegExp(label));
  }
});

test("Workbench is composed from shadcn UI and navigates to detailed pages", () => {
  for (const component of ["Badge", "Button", "Card", "ChartContainer", "Select", "Skeleton", "Table", "Tabs"]) {
    assert.match(source, new RegExp(`<${component}`));
  }
  assert.match(source, /navigate\("\/device\/devices"\)/);
  assert.match(source, /navigate\("\/logs\/system"\)/);
});

test("Workbench removes the banner and page-local refresh action", () => {
  assert.doesNotMatch(source, /dashboard-welcome-closed/);
  assert.doesNotMatch(source, /刷新数据/);
  assert.doesNotMatch(source, /<Alert/);
});

test("Workbench uses the approved responsive panel order", () => {
  assert.match(source, /grid gap-4 sm:grid-cols-2 lg:grid-cols-4/);
  assert.match(source, /data-dashboard-row="overview-charts"[\s\S]*lg:grid-cols-\[1\.15fr_1fr_1\.15fr\]/);

  const distributionIndex = source.indexOf('data-dashboard-section="distribution"');
  const healthIndex = source.indexOf('data-dashboard-section="health"');
  const trendIndex = source.indexOf('data-dashboard-section="trend"');
  const pipelineIndex = source.indexOf('data-dashboard-section="pipeline"');
  const eventsIndex = source.indexOf('data-dashboard-section="logs"');
  assert.ok(trendIndex >= 0);
  assert.ok(distributionIndex > trendIndex);
  assert.ok(healthIndex > distributionIndex);
  assert.ok(pipelineIndex > healthIndex);
  assert.ok(eventsIndex > pipelineIndex);
});

test("Workbench uses the requested shadcn chart variants", () => {
  assert.match(source, /<ComposedChart/);
  assert.equal(source.match(/<Area\b/g)?.length, 1);
  // 概念稿：采集量=渐变面积，在线设备=虚线，告警=斜纹小柱
  assert.match(source, /<Area[\s\S]*?dataKey="collectCount"[\s\S]*?fill="url\(#wbFillCollect\)"/);
  assert.match(source, /<Line[\s\S]*?dataKey="onlineDeviceCount"[\s\S]*?strokeDasharray="5 5"/);
  assert.match(source, /<Bar[\s\S]*?dataKey="alarmCount"[\s\S]*?fill="url\(#wbAlarmHatch\)"/);
  assert.match(source, /<LegendChip/);
  assert.doesNotMatch(source, /<AreaChart/);
  assert.match(source, /<Select value=\{dashboard\.trendRange\}/);
  assert.match(source, /data-dashboard-section="distribution"[\s\S]*?grid-cols-\[72px_minmax\(0,1fr\)_64px\]/);
  assert.match(source, /data-dashboard-section="distribution"[\s\S]*?bg-brand/);
});

test("Workbench tooltip falls back to the active row for the newest bucket", () => {
  assert.match(chartSource, /item\.value \?\?[\s\S]*item\.payload\?\.\[item\.dataKey\]/);
  assert.match(chartSource, /formatter\(itemValue/);
});
test("Workbench uses the blue preset chart palette instead of neutral grays", () => {
  const rootTheme = themeSource.match(/:root\s*\{([\s\S]*?)\n\}/)?.[1] ?? "";

  for (const token of ["primary", "chart-1", "chart-2", "chart-3", "chart-4", "chart-5"]) {
    const value = rootTheme.match(new RegExp(`--${token}:\\s*oklch\\(([^)]+)\\)`))?.[1];
    assert.ok(value, `missing --${token}`);
    const [, chroma, hue] = value.trim().split(/\s+/).map(Number);
    assert.ok(chroma > 0.04, `--${token} is still neutral gray`);
    assert.ok(hue >= 220 && hue <= 290, `--${token} is not in the blue palette`);
  }
});

test("Workbench stat cards use a solid background and compact icon rows", () => {
  assert.doesNotMatch(source, /bg-gradient-to-t|from-primary\/5/);
  assert.match(source, /<Card size="sm" className="relative min-h-\[124px\] gap-0 p-4">/);
  assert.match(source, /<Icon className="size-\[15px\] shrink-0" \/>/);
  assert.match(source, /function OnlineRateGauge[\s\S]*const ticks = 24/);
  // 其余三张 KPI 卡也有同尺寸小图，全部来自真实接口字段
  assert.match(source, /<DeviceSplitRing online=\{dashboard\.overview\.onlineDeviceTotal\} offline=\{dashboard\.overview\.offlineDeviceTotal\} \/>/);
  assert.match(source, /<CollectSparkline values=\{kpiSeries\.collect\} \/>/);
  assert.match(source, /<AlarmMiniBars values=\{kpiSeries\.alarm\} \/>/);
  assert.match(source, /backendClient\.dashboardTrend\(token, "last24h", signal\)/);
  assert.match(source, /text-\[28px\] font-semibold/);
});

test("Workbench fills the content height with the log card stretching to the bottom", () => {
  assert.match(source, /<div className="flex min-h-full w-full flex-col gap-4">/);
  assert.match(source, /data-dashboard-section="logs" className=\{`@container\/card flex-1/);
});

test("Workbench cards leave the surface to the shared Card component", () => {
  assert.doesNotMatch(source, /border border-border/);
});

test("Workbench cards and controls follow the compact dashboard layout", () => {
  assert.match(source, /min-h-\[124px\]/);
  assert.match(source, /<CardAction className="flex items-center gap-4">[\s\S]*<Tabs/);
  assert.match(source, /@\[767px\]\/card:flex/);
  assert.match(source, /@\[767px\]\/card:hidden/);
  assert.match(source, /h-\[264px\] w-full aspect-auto/);
  assert.equal(source.match(/min-h-\[220px\]/g)?.length, 1);
  assert.doesNotMatch(source, /\[260px\]/);
});


test("Workbench renders six live pipeline stages with aggregated health colors", () => {
  assert.match(source, /data-dashboard-section="pipeline"/);
  for (const label of ["采集", "上行", "汇聚", "Redis", "入库", "存储"]) {
    assert.match(source, new RegExp(`label: "${label}"`));
  }
  assert.match(source, /grid min-h-\[34px\] flex-1 grid-cols-\[16px_minmax\(0,1fr\)_auto_52px\]/);
  assert.match(source, /absolute left-\[15\.5px\] w-px bg-border/);
  assert.match(source, /warning: \{ label: "需关注"[^}]*row: "bg-warning-soft\/70"/);
  assert.match(source, /critical: \{ label: "异常", color: "var\(--destructive\)"/);
  assert.match(source, /<PipelineTimeline stages=\{pipeline\.data\.stages\} \/>/);
  assert.doesNotMatch(source, /data-dashboard-section="risk"|<CardTitle>风险设备<\/CardTitle>/);
  assert.match(pipelineSource, /backendClient\.dashboardEdgePipelineHealth\(token,\s*signal\)/);
  assert.match(pipelineSource, /agentClient\.getConnectionHealth\(signal\)/);
  for (const module of ["collector", "ingest", "writer"]) {
    assert.match(pipelineSource, new RegExp(`getProcessStatus\\("${module}",\\s*signal\\)`));
  }
  assert.doesNotMatch(pipelineSource, /getProcessStatus\("uploader"(?:,|\))/);
  assert.match(pipelineSource, /BUILD_ROLE === "distributed-center"[\s\S]*dashboardEdgePipelineHealth/);
  assert.match(pipelineSource, /edgeStage\("collector", edgeHealth\)/);
  assert.match(pipelineSource, /edgeStage\("uploader", edgeHealth\)/);
  assert.match(pipelineSource, /intervalMs: 10_000/);
});

test("Workbench keeps a gray canvas and prevents smoothed trends from crossing zero", () => {
  const rootTheme = themeSource.match(/:root\s*\{([\s\S]*?)\n\}/)?.[1] ?? "";
  assert.match(rootTheme, /--background:\s*#F5F8FC;/i);
  assert.equal(source.match(/type="monotone"/g)?.length, 2);
  assert.equal(source.match(/domain=\{\[0, \(max: number\)/g)?.length, 3);
  assert.doesNotMatch(source, /type="natural"/);
});

test("Workbench overview charts use hatched capsule bars and the concentric ring", () => {
  assert.match(source, /data-dashboard-section="distribution"[\s\S]*?grid-cols-\[72px_minmax\(0,1fr\)_64px\]/);
  assert.match(source, /data-dashboard-section="distribution"[\s\S]*?repeating-linear-gradient/);
  assert.match(source, /const \[hoveredIndex, setHoveredIndex\] = useState<number \| null>\(null\)/);
  assert.match(source, /function HealthRings[\s\S]*const r = 72 - index \* 16/);
  assert.match(source, /data-dashboard-section="health"[\s\S]*<HealthRings data=\{healthData\} total=\{healthTotal\} hoveredIndex=\{hoveredIndex\} \/>/);
  assert.match(source, /onMouseEnter=\{\(\) => setHoveredIndex\(index\)\}/);
  for (const token of ["brand", "warning", "muted-foreground"]) {
    assert.match(source, new RegExp(`color: "var\\(--${token}\\)" \\}`));
  }
});

test("Workbench loads real system logs for the bottom table", () => {
  assert.match(source, /backendClient\.systemLogList/);
  assert.match(source, /useBackendQuery\(loadRecentLogs/);
  assert.match(source, /data-dashboard-section="logs"[\s\S]*<CardAction>[\s\S]*更多日志/);
  assert.doesNotMatch(source, /DashboardRecentEvent/);
});

test("Workbench system log table shows the requested five fields and pagination", () => {
  for (const heading of ["时间", "级别", "类型", "来源", "内容"]) {
    assert.match(source, new RegExp(`<TableHead[^>]*>${heading}<\\/TableHead>`));
  }
  assert.match(source, /<TableHeader className="bg-muted\/50"/);
  assert.match(source, /row\.time/);
  assert.match(source, /row\.level/);
  assert.match(source, /row\.type/);
  assert.match(source, /row\.source/);
  assert.match(source, /row\.content/);
  assert.match(source, /每页行数/);
  assert.match(source, /<ChevronsLeft/);
  assert.match(source, /<ChevronLeft/);
  assert.match(source, /<ChevronRight/);
  assert.match(source, /<ChevronsRight/);
});

test("Workbench system log content stays within the page and reveals full text on hover", () => {
  assert.match(source, /<Table className="w-full table-fixed">/);
  assert.doesNotMatch(source, /min-w-\[920px\]|min-w-80|overflow-x-auto/);
  assert.match(source, /<TooltipProvider delayDuration=\{200\}>/);
  assert.match(source, /<TooltipTrigger asChild>/);
  assert.match(source, /cursor-help truncate/);
  assert.match(source, /<TooltipContent[^>]*max-w-sm[^>]*whitespace-pre-wrap[^>]*break-words/);
});

test("Workbench system log table is compact, centered, and uses an equal-height action", () => {
  assert.match(source, /<TableRow className="h-10 hover:bg-transparent">/);
  assert.equal(source.match(/<TableHead className="[^"]*text-center[^"]*">/g)?.length, 5);
  assert.match(source, /<TableRow key=\{`\$\{row\.kind\}:\$\{row\.id\}`\} className="h-12">/);
  assert.ok((source.match(/<TableCell className="[^"]*text-center[^"]*"/g)?.length ?? 0) >= 5);
  assert.match(source, /<Button variant="outline" onClick=\{\(\) => navigate\("\/logs\/system"\)\}>/);
});
