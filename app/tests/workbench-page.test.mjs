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
  for (const component of ["Badge", "Button", "Card", "ChartContainer", "Select", "Skeleton", "Table"]) {
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
  assert.match(source, /data-dashboard-row="overview-charts"[\s\S]*lg:grid-cols-3/);

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
  assert.match(source, /<Bar[\s\S]*dataKey="onlineDeviceCount"/);
  assert.match(source, /<Line[\s\S]*dataKey="alarmCount"/);
  assert.doesNotMatch(source, /<AreaChart/);
  assert.match(source, /<Select value=\{dashboard\.trendRange\}/);
  assert.match(source, /<BarChart[\s\S]*stackId="devices"/);
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

test("Workbench stat cards use a solid background and larger icons without changing the right inset", () => {
  assert.doesNotMatch(source, /bg-gradient-to-t|from-primary\/5/);
  assert.match(source, /className="bg-card"/);
  assert.match(source, /className="flex size-10 shrink-0 items-center justify-center/);
  assert.match(source, /<Icon className="size-6" \/>/);
});

test("Workbench cards leave the surface to the shared Card component", () => {
  assert.doesNotMatch(source, /border border-border/);
});

test("Workbench cards and controls follow the compact dashboard layout", () => {
  assert.match(source, /min-h-24 items-center justify-between/);
  assert.match(source, /<CardAction>[\s\S]*<ToggleGroup/);
  assert.match(source, /@\[767px\]\/card:flex/);
  assert.match(source, /@\[767px\]\/card:hidden/);
  assert.match(source, /h-\[220px\] w-full aspect-auto/);
  assert.equal(source.match(/min-h-\[220px\]/g)?.length, 2);
  assert.doesNotMatch(source, /\[260px\]/);
});


test("Workbench renders six live pipeline stages with aggregated health colors", () => {
  assert.match(source, /data-dashboard-section="pipeline"/);
  for (const label of ["Collector", "Uploader", "Ingest", "Redis", "Writer", "TDengine"]) {
    assert.match(source, new RegExp(`label: "${label}"`));
  }
  assert.match(source, /grid-cols-\[minmax\(0,1fr\)_18px_minmax\(0,1fr\)_18px_minmax\(0,1fr\)\]/);
  assert.match(source, /grid-rows-\[80px_18px_80px\]/);
  for (const connector of ["ArrowRight", "ArrowDown", "ArrowLeft"]) {
    assert.match(source, new RegExp("<" + connector));
  }
  assert.match(source, /animate-pulse bg-amber-400/);
  assert.match(source, /dot: "bg-red-500"/);
  assert.match(source, /<PipelineStageNode stage=\{stage\}/);
  assert.doesNotMatch(source, /data-dashboard-section="risk"|<CardTitle>风险设备<\/CardTitle>/);
  assert.match(pipelineSource, /backendClient\.dashboardEdgePipelineHealth/);
  assert.match(pipelineSource, /agentClient\.getConnectionHealth/);
  for (const module of ["collector", "ingest", "writer"]) {
    assert.match(pipelineSource, new RegExp(`getProcessStatus\\("${module}"\\)`));
  }
  assert.doesNotMatch(pipelineSource, /getProcessStatus\("uploader"\)/);
  assert.match(pipelineSource, /BUILD_ROLE === "distributed-center"[\s\S]*dashboardEdgePipelineHealth/);
  assert.match(pipelineSource, /edgeStage\("collector", edgeHealth\)/);
  assert.match(pipelineSource, /edgeStage\("uploader", edgeHealth\)/);
  assert.match(pipelineSource, /intervalMs: 10_000/);
});

test("Workbench keeps a gray canvas and prevents smoothed trends from crossing zero", () => {
  const rootTheme = themeSource.match(/:root\s*\{([\s\S]*?)\n\}/)?.[1] ?? "";
  assert.match(rootTheme, /--background:\s*#F7F9FC;/i);
  assert.equal(source.match(/type="monotone"/g)?.length, 2);
  assert.equal(source.match(/domain=\{\[0, "auto"\]\}/g)?.length, 2);
  assert.doesNotMatch(source, /type="natural"/);
});

test("Workbench overview charts use stacked bars and the interactive Bklit donut", () => {
  assert.match(source, /data-dashboard-section="distribution"[\s\S]*<BarChart[\s\S]*<ChartLegend[\s\S]*<Bar[\s\S]*<Bar/);
  assert.match(source, /from "@\/components\/charts\/pie-chart"/);
  assert.match(source, /const \[hoveredIndex, setHoveredIndex\] = useState<number \| null>\(null\)/);
  assert.match(source, /data-dashboard-section="health"[\s\S]*<PieChart[\s\S]*data=\{healthData\}[\s\S]*hoveredIndex=\{hoveredIndex\}[\s\S]*innerRadius=\{45\}[\s\S]*onHoverChange=\{setHoveredIndex\}[\s\S]*size=\{150\}/);
  assert.match(source, /healthData\.map\(\(_, index\) => <PieSlice index=\{index\} key=\{index\} \/>\)/);
  assert.match(source, /<PieCenter defaultLabel="设备" \/>/);
  assert.match(source, /<Legend[\s\S]*hoveredIndex=\{hoveredIndex\}[\s\S]*items=\{healthLegendItems\}[\s\S]*onHoverChange=\{setHoveredIndex\}/);
  assert.match(source, /<LegendItemComponent[^>]*>[\s\S]*<LegendMarker \/>[\s\S]*<LegendLabel \/>/);
  assert.match(source, /className="flex w-full max-w-lg flex-col items-center justify-center gap-5"/);
  assert.match(source, /className="w-full max-w-sm flex-row flex-wrap justify-center gap-x-4 gap-y-1"/);
  assert.doesNotMatch(source, /gap-8 sm:flex-row/);
  for (const token of ["chart-2", "chart-4", "chart-5"]) {
    assert.match(source, new RegExp(`color: "var\\(--${token}\\)"`));
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
