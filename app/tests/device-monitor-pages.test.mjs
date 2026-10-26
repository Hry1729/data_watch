import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { getPageMeta, placeholderPages } from "../src/config/navigation.ts";

const pages = [
  ["DeviceCommunicationPage.tsx", "/device/communication", ["设备通讯", "总数", "在线", "离线", "异常", "未启用"]],
  ["DriverManagementPage.tsx", "/device/drivers", ["驱动列表", "驱动名称", "说明", "关联设备"]],
  ["DeviceManagementPage.tsx", "/device/devices", ["汇总", "新增设备", "设备编号"]],
  ["PointManagementPage.tsx", "/device/points", ["汇总", "新增点位", "点位名称", "设备号"]],
  ["RealtimeMonitorPage.tsx", "/device/realtime", ["选择设备", "选择点位", "点位组", "中心实时连接", "实时监测点", "实时趋势"]],
  ["HistoryDataPage.tsx", "/device/history", ["选择设备", "查询点位", "时间范围", "折线图", "原始数据"]],
];

test("all six device-monitor routes are implemented rather than placeholders", () => {
  for (const [, path] of pages) {
    assert.equal(getPageMeta(path)?.kind, "existing", path);
    assert.equal(placeholderPages.some((page) => page.path === path), false, path);
  }
});

test("device-monitor pages contain their approved operational regions", async () => {
  for (const [file, , labels] of pages) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");
    for (const label of labels) assert.match(source, new RegExp(label), `${file}: ${label}`);
    assert.match(source, /<Card/);
    assert.match(source, /<Button/);
  }
});

test("realtime and history value tiles share one PointValueTile component", async () => {
  const tile = await readFile(new URL("../src/components/PointValueTile.tsx", import.meta.url), "utf8");
  assert.match(tile, /rounded-\[10px\] bg-muted\/55 px-3 py-2\.5/);
  assert.match(tile, /overflow-x-auto overflow-y-hidden pb-1 \[scrollbar-width:thin\]/);
  assert.match(tile, /grid grid-flow-col auto-cols-/);
  assert.match(tile, /刚刚切换/);
  for (const file of ["RealtimeMonitorPage.tsx", "HistoryDataPage.tsx"]) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");
    assert.match(source, /import \{ PointValueTile, PointValueTileRow \} from "@\/components\/PointValueTile"/, file);
    assert.doesNotMatch(source, /bg-muted\/55 px-3/, file);
  }
});

test("realtime and history pages use shadcn chart containers", async () => {
  for (const file of ["RealtimeMonitorPage.tsx", "HistoryDataPage.tsx"]) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");
    assert.match(source, /<ChartContainer/);
  }
});

test("realtime monitor uses the requested four-section operational layout", async () => {
  const source = await readFile(new URL("../src/pages/RealtimeMonitorPage.tsx", import.meta.url), "utf8");

  assert.match(source, /realtimePointGroups/);
  assert.doesNotMatch(source, /点位分组/);
  assert.match(source, /groupCode: selectedGroupCode/);
  // Bool 点位始终走状态泳道，不再有“状态图整页替换”分支
  assert.doesNotMatch(source, /chartType === "status" \? <TrendChart/);
  assert.match(source, /deviceDialogOpen/);
  assert.match(source, /设备类型/);
  assert.match(source, /设备编号/);
  assert.match(source, /搜索设备名称或编号/);
  assert.match(source, /!flex shrink-0 items-center gap-3 border-b/);
  assert.match(source, /暂未选择监控点位/);
  assert.match(source, /max-w-44/);
  assert.doesNotMatch(source, /当前设备/);
  assert.match(source, /ml-auto flex shrink-0 items-center justify-end gap-2/);
  assert.match(source, /flex min-w-0 flex-nowrap items-center gap-2 whitespace-nowrap border-b/);
  // 点位数值卡：灰底圆角卡片，始终单行，多了横向滚动（细滚动条）
  assert.match(source, /<PointValueTileRow /);
  assert.match(source, /<PointValueTile key=\{point\.varSeq\}[^>]*previous=/);
  assert.doesNotMatch(source, /function LiveTile/);
  assert.doesNotMatch(source, /grid-cols-6/);
  assert.match(source, /flex min-h-0 min-w-0 flex-1 flex-col gap-0 overflow-hidden py-0/);
  assert.match(source, /border-b px-5 py-3\.5/);
  assert.match(source, /<Card className="shrink-0 gap-0 py-0">/);
  assert.match(source, /grid grid-cols-1 gap-x-6 gap-y-2 px-1\.5 pb-0\.5 text-xs text-muted-foreground sm:grid-cols-2 xl:grid-cols-4/);
  assert.match(source, /xl:justify-self-end/);
  assert.match(source, /selectedDevice \? selectedDevice\.deviceCode : "选择设备"/);
  assert.match(source, /最后同步时间：\$\{formatSyncTime/);
});

test("history data uses the approved four-section operational layout", async () => {
  const source = await readFile(new URL("../src/pages/HistoryDataPage.tsx", import.meta.url), "utf8");

  assert.doesNotMatch(source, /<Card>\s*<CardContent className="space-y-3 p-4">/);
  assert.match(source, /deviceDialogOpen/);
  assert.match(source, /设备类型/);
  assert.match(source, /设备编号/);
  assert.match(source, /搜索设备名称或编号/);
  assert.match(source, /function TimeRangePicker/);
  assert.doesNotMatch(source, /当前设备/);
  assert.match(source, /setRecentRange\(Number\(value\), setStartTime, setEndTime\)/);
  assert.match(source, /<Card className="shrink-0 gap-0 py-0">/);
  // 已选点位：与实时监控共用同一数值卡组件（单行，多了横向滚动），值为查询区间最新值；不再显示 最小/平均/最大
  assert.match(source, /<PointValueTileRow /);
  assert.match(source, /<PointValueTile key=\{point\.varSeq\}[^>]*value=\{latest\?\.value \?\? null\}/);
  assert.doesNotMatch(source, /"最小"/);
  // 图表卡撑满剩余高度；Bool 点位用状态泳道
  assert.match(source, /<Card className="flex min-h-0 min-w-0 flex-1 flex-col gap-0 overflow-hidden py-0">/);
  assert.match(source, /function HistoryBoolLanes/);
  assert.match(source, /isBoolSeries/);
  assert.match(source, /!flex shrink-0 items-center gap-3 border-b px-5 py-3\.5/);
  assert.match(source, /折线图/);
  assert.match(source, /aria-label="时间对齐颗粒度"/);
  // 快捷范围与起止时间收进同一个时间按钮的弹层，筛选栏保持单行
  assert.match(source, /<TimeRangePicker startTime=\{startTime\} endTime=\{endTime\}/);
  assert.match(source, /aria-label="时间范围"/);
  assert.equal((source.match(/type="datetime-local" value=\{(startTime|endTime)\}/g) ?? []).length, 2);
  assert.match(source, /flex min-w-0 flex-nowrap items-center gap-1\.5 whitespace-nowrap border-b/);
  assert.match(source, /w-\[76px\]`\} aria-label="时间对齐颗粒度"/);
  assert.match(source, /currentDevice \? currentDevice\.deviceCode \?\? currentDevice\.name : "选择设备"/);
  assert.match(source, /ml-auto flex items-center gap-2/);
  assert.match(source, /每页 200 行/);
  assert.match(source, /暂无原始数据/);
});

test("device communication drops KPI cards and shows status counts in the card legend", async () => {
  const source = await readFile(new URL("../src/pages/DeviceCommunicationPage.tsx", import.meta.url), "utf8");

  assert.doesNotMatch(source, /MonitorKpiCard|function KpiCard/);
  assert.match(source, /function LegendCount/);
  for (const [label, value] of [["总数", "total"], ["在线", "online"], ["离线", "offline"], ["异常", "unstable"], ["未启用", "disabled"]]) {
    assert.match(source, new RegExp(`<LegendCount[^>]*label="${label}" value=\\{summary\\.data \\? ${value} : null\\}`));
  }
  // 不分页：一次显示全部设备，色块墙在卡内滚动；底部无分页栏和分隔线
  assert.doesNotMatch(source, /estimatePageSize|DeviceCatalogPagination|setPage|ResizeObserver/);
  assert.doesNotMatch(source, /border-t border-border/);
  assert.match(source, /pageSize: FETCH_PAGE_SIZE/);
  assert.match(source, /\[deviceType, keyword, status\]/);
  assert.match(source, /min-h-0 flex-1 overflow-auto/);
});

test("device communication renders grouped full-width auto-fill square status tiles", async () => {
  const source = await readFile(new URL("../src/pages/DeviceCommunicationPage.tsx", import.meta.url), "utf8");

  assert.match(source, /grid-cols-\[repeat\(auto-fill,minmax\(46px,1fr\)\)\]/);
  assert.match(source, /grid w-full/);
  assert.match(source, /aspect-square/);
  assert.doesNotMatch(source, /h-8 w-14/);
  assert.doesNotMatch(source, /max-w-\[1680px\]/);
  assert.match(source, /device\.deviceCode/);
  assert.match(source, /bg-\[color-mix\(in_oklab,var\(--success\)_14%,var\(--card\)\)\]/);
  assert.match(source, /bg-warning text-white/);
  assert.match(source, /bg-destructive text-white/);
  assert.match(source, /bg-muted text-muted-foreground/);
  assert.doesNotMatch(source, /aspect-square w-full[^"]*rounded-md border/);
});
test("device communication formats equipment group titles and display-only codes", async () => {
  const source = await readFile(new URL("../src/pages/DeviceCommunicationPage.tsx", import.meta.url), "utf8");

  assert.match(source, /桥吊（QC）/);
  assert.match(source, /轨道吊（ASC）/);
  assert.match(source, /自动导引车（AGV）/);
  assert.match(source, /formatDeviceCode/);
  assert.match(source, /last === "1"/);
  assert.match(source, /last === "2"/);
  assert.match(source, /\$\{number\.slice\(0, 3\)\}L/);
  assert.match(source, /\$\{number\.slice\(0, 3\)\}S/);
  assert.match(source, /\{formatDeviceCode\(device\.deviceCode\)\}/);
  assert.doesNotMatch(source, />\s*\{device\.deviceCode\}\s*<\/button>/);
});


test("device communication header contains legend and filters without a local refresh", async () => {
  const source = await readFile(new URL("../src/pages/DeviceCommunicationPage.tsx", import.meta.url), "utf8");
  const header = source.match(/<Card className="flex min-h-0 flex-1 flex-col">[\s\S]*?<CardContent/)?.[0] ?? "";

  assert.match(header, />在线</);
  assert.match(header, />异常</);
  assert.match(header, />离线</);
  assert.match(header, />未启用</);
  assert.match(header, /placeholder="编号 \/ 名称"/);
  assert.match(header, /placeholder="全部类型"/);
  assert.match(header, /placeholder="全部状态"/);
  assert.doesNotMatch(source, /RefreshCw|query\.refresh/);
  assert.match(header, /<div className="flex flex-wrap items-center gap-x-4 gap-y-2 px-5 pt-1">/);
  assert.match(header, /ml-auto flex min-w-0 flex-1 flex-wrap items-center justify-end gap-2/);
  assert.match(header, /relative w-\[150px\] shrink-0/);
  assert.equal((header.match(/SelectTrigger className="w-28"/g) ?? []).length, 2);
  assert.match(header, /<Button variant="outline" size="sm"[\s\S]*?>重置<\/Button>/);
});
test("driver management uses a full-height paginated table with truncated description tooltips", async () => {
  const source = await readFile(new URL("../src/pages/DriverManagementPage.tsx", import.meta.url), "utf8");

  assert.doesNotMatch(source, /可用驱动/);
  assert.doesNotMatch(source, /KpiTile/);
  assert.match(source, /驱动列表/);
  assert.doesNotMatch(source, /max-w-\[1500px\]/);
  // 安装/卸载后需刷新列表（共享壳层刷新 + 本页 query.refresh）
  assert.match(source, /query\.refresh\(\)/);
  assert.match(source, /backendClient\.uninstallDriver\(auth\.backendToken/);
  assert.doesNotMatch(source, /agentClient\.uninstallDriver/);
  assert.doesNotMatch(source, /compensate:/);
  assert.match(source, /flex h-full min-h-0 w-full flex-col/);
  assert.match(source, /flex min-h-0 flex-1 flex-col/);
  assert.match(source, /table-fixed/);
  assert.match(source, /TooltipProvider/);
  assert.match(source, /TooltipTrigger asChild/);
  assert.match(source, /cursor-help truncate/);
  assert.match(source, /pageNo: page/);
  assert.match(source, /pageSize/);
  assert.match(source, /totalPages/);
  assert.match(source, /useAdaptivePageSize\(\{ rowHeight: 48 \}\)/);
  assert.match(source, /ref=\{tableViewportRef\}/);
  assert.match(source, /每页 \{pageSize\} 行/);
  assert.match(source, /共 \{query\.data\?\.total \?\? 0\} 条驱动/);
  assert.match(source, /ChevronsLeft/);
  assert.match(source, /ChevronLeft/);
  assert.match(source, /ChevronRight/);
  assert.match(source, /ChevronsRight/);
  assert.match(source, /source === "bundled"/);
  assert.match(source, /item\.aliases/);
  assert.match(source, /protected: item\.protected/);
  assert.match(source, /if \(row\.source === "bundled" \|\| row\.source === "builtin" \|\| row\.protected\) \{[\s\S]*?protected: true/);
  assert.match(source, /const official = local\.source === "official";[\s\S]*?protected: false/);
  assert.match(source, /&& !row\.protected/);
  assert.match(source, /安装包内置/);
});

test("device and point management use dynamic type tabs with paginated lists", async () => {
  const files = ["DeviceManagementPage.tsx", "PointManagementPage.tsx"];

  for (const file of files) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");

    assert.match(source, /flex h-full min-h-0 w-full flex-col/);
    assert.match(source, /flex min-h-0 flex-1 flex-col/);
    assert.match(source, /<Tabs/);
    assert.match(source, /<TabsList/);
    assert.match(source, /<TabsTrigger value="all">汇总<\/TabsTrigger>/);
    assert.match(source, /table-fixed/);
    assert.match(source, /每页 \{pageSize\} 行/);
    assert.match(source, /ChevronsLeft/);
    assert.match(source, /ChevronLeft/);
    assert.match(source, /ChevronRight/);
    assert.match(source, /ChevronsRight/);
    assert.doesNotMatch(source, /RefreshCw/);
    assert.doesNotMatch(source, /设备树/);
  }
});

test("device and point management build type tabs from current system devices", async () => {
  const deviceSource = await readFile(new URL("../src/pages/DeviceManagementPage.tsx", import.meta.url), "utf8");
  const pointSource = await readFile(new URL("../src/pages/PointManagementPage.tsx", import.meta.url), "utf8");

  assert.match(deviceSource, /deviceType: activeType === "all" \? undefined : activeType/);
  assert.match(pointSource, /backendClient\.deviceList/);
  assert.match(pointSource, /connectStatus: connectStatus === "all" \? undefined : connectStatus/);
  assert.match(pointSource, /useDevicePicker/);
  assert.match(pointSource, /activeType === "all"/);
  assert.match(deviceSource, /const loadDeviceCatalog = useCallback/);
  assert.doesNotMatch(deviceSource, /loadAllDevicePages/);
  assert.match(deviceSource, /backendClient\.deviceTypes/);
  assert.match(deviceSource, /backendClient\.deviceList/);
  assert.match(deviceSource, /const typeTabs = useMemo/);
  assert.doesNotMatch(deviceSource, /historyDeviceTypes/);
  const buttonSource = await readFile(new URL("../src/components/ui/button.tsx", import.meta.url), "utf8");
  assert.match(buttonSource, /cursor-pointer/);
});

test("device and point management keep the selected-item deletion action without a batch label", async () => {
  for (const file of ["DeviceManagementPage.tsx"]) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");
    assert.match(source, /<Button variant="destructive" size="sm" className=\{DANGER_BTN\} disabled=\{!selectedIds\.length\} onClick=\{\(\) => setBatchDeleteOpen\(true\)\}>[\s\S]*?删除[\s\S]*?<\/Button>/);
    // 已选数量以徽标显示在删除按钮内
    assert.match(source, /删除\s*\{selectedIds\.length > 0 && <SelectedCount count=\{selectedIds\.length\} \/>\}/);
  }
});
test("point management toolbar stays on one row: delete appears only with a selection, type/status live in a filter popover", async () => {
  const source = await readFile(new URL("../src/pages/PointManagementPage.tsx", import.meta.url), "utf8");
  assert.match(source, /\{selectedIds\.length > 0 && <Button variant="destructive" size="sm" className=\{DANGER_BTN\} onClick=\{\(\) => setBatchDeleteOpen\(true\)\}>[\s\S]*?删除[\s\S]*?<SelectedCount count=\{selectedIds\.length\} \/>/);
  assert.match(source, /aria-label="更多筛选"/);
  assert.match(source, /activeFilterCount/);
  assert.match(source, /flex shrink-0 flex-nowrap items-center gap-2 whitespace-nowrap px-5 pt-5/);
});
test("device list keeps filters inside the card and sizes pages from the table viewport", async () => {
  for (const file of ["DeviceManagementPage.tsx"]) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");
    assert.match(source, /useAdaptivePageSize\(\{ rowHeight: 52 \}\)/);
    assert.match(source, /ref=\{tableViewportRef\}/);
    assert.match(source, /Input className=\{`\$\{CTRL\} pl-8`\}/);
    assert.match(source, /SelectTrigger className=\{`\$\{CTRL\} w-24`\}/);
    assert.match(source, /<Table className=\{`w-full table-fixed \$\{TABLE_TEXT\}`\}/);
    assert.match(source, /<TableRow key=\{row\.id\} className=\{`h-\[52px\]/);
    assert.match(source, /<div className="flex shrink-0 flex-wrap items-center gap-2 px-5 pt-5">/);
    assert.match(source, /<Card className="flex min-h-0 flex-1 flex-col gap-0 py-0">[\s\S]*?<Tabs/);
    assert.match(source, /<Card className="flex min-h-0 flex-1 flex-col gap-0 py-0">[\s\S]*?ref=\{tableViewportRef\}/);
    assert.match(source, /overflow-x-auto overflow-y-hidden \[scrollbar-width:none\]"><TabsList/);
  }
});
test("point management pages and filters devices on the server before counting points", async () => {
  const pointSource = await readFile(new URL("../src/pages/PointManagementPage.tsx", import.meta.url), "utf8");
  const pointTypes = await readFile(new URL("../src/types/device-monitor.ts", import.meta.url), "utf8");
  const routerSource = await readFile(new URL("../../backend/app/api/routes/catalog.py", import.meta.url), "utf8");
  const serviceSource = await readFile(new URL("../../backend/app/services/catalog.py", import.meta.url), "utf8");
  assert.doesNotMatch(pointSource, /pointList\(token, \{[^}]*pageSize:\s*1000/);
  // 点位表按视口高度自动计算每页行数（行高 40px），不再固定 10 行
  assert.match(pointSource, /useAdaptivePageSize\(\{ rowHeight: 40 \}\)/);
  assert.match(pointSource, /ref=\{tableViewportRef\}/);
  assert.doesNotMatch(pointSource, /const pageSize = 10;/);
  assert.match(pointSource, /pageNo:\s*page/);
  assert.match(pointSource, /deviceType: activeType === "all" \? undefined : activeType/);
  assert.match(pointSource, /connectStatus: connectStatus === "all" \? undefined : connectStatus/);
  assert.match(pointTypes, /deviceIds\?: number\[\]/);
  assert.match(routerSource, /deviceIds: list\[str\] \| str \| None/);
  assert.match(routerSource, /"deviceIds": deviceIds/);
  assert.match(serviceSource, /params\.get\("deviceIds"\)/);
});

test("device form edits the primary and backup collection endpoints", async () => {
  const deviceSource = await readFile(new URL("../src/pages/DeviceManagementPage.tsx", import.meta.url), "utf8");
  const deviceTypes = await readFile(new URL("../src/types/device-monitor.ts", import.meta.url), "utf8");
  assert.match(deviceSource, /<Label>主地址<\/Label>/);
  assert.match(deviceSource, /<Label>备用地址<\/Label>/);
  assert.match(deviceSource, /endpoint:\s*row\.endpoint/);
  assert.match(deviceSource, /endpointBackup:\s*row\.endpointBackup/);
  assert.match(deviceTypes, /endpoint:\s*string;[\s\S]*endpointBackup\?:\s*string;/);
});
