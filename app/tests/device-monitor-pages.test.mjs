import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

import { getPageMeta, placeholderPages } from "../src/config/navigation.ts";

const pages = [
  ["DeviceCommunicationPage.tsx", "/device/communication", ["设备总数", "在线比例", "离线比例", "未用比例"]],
  ["DriverManagementPage.tsx", "/device/drivers", ["驱动管理", "驱动名称", "说明", "关联设备"]],
  ["DeviceManagementPage.tsx", "/device/devices", ["设备管理", "汇总", "新增设备", "设备编号"]],
  ["PointManagementPage.tsx", "/device/points", ["点位管理", "汇总", "新增点位", "点位名称"]],
  ["RealtimeMonitorPage.tsx", "/device/realtime", ["当前设备", "选择点位", "WebSocket", "实时监测点", "点位实时趋势"]],
  ["HistoryDataPage.tsx", "/device/history", ["选择设备", "查询点位", "时间范围", "趋势图", "原始数据"]],
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
  assert.match(source, /overflow-x-auto/);
  assert.match(source, /groupCode: selectedGroupCode/);
  assert.match(source, /chartType === type/);
  assert.match(source, /deviceDialogOpen/);
  assert.match(source, /设备类型/);
  assert.match(source, /设备编号/);
  assert.match(source, /搜索设备名称或编号/);
  assert.match(source, /!flex shrink-0 items-center justify-between/);
  assert.match(source, /暂未选择监控点位/);
  assert.match(source, /max-w-56/);
  assert.match(source, /ml-auto flex flex-wrap items-center justify-end gap-2/);
  assert.match(source, /w-40 shrink-0 rounded-md border bg-card px-2\.5 py-1\.5/);
  assert.match(source, /flex min-w-max gap-2 pb-2 pr-2/);
  assert.match(source, /flex min-h-0 min-w-0 flex-1 flex-col gap-0 overflow-hidden py-0/);
  assert.match(source, /border-b px-4 py-2\.5/);
  assert.match(source, /<Card className="gap-0 py-0">/);
  assert.match(source, /grid grid-cols-1 gap-x-6 gap-y-2 px-2 py-1 text-xs text-muted-foreground sm:grid-cols-2 xl:grid-cols-4/);
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
  assert.match(source, /快捷时间范围/);
  assert.match(source, /setRecentRange\(Number\(value\), setStartTime, setEndTime\)/);
  assert.match(source, /<Card className="gap-0 py-0" title=\{pointSummary\}>/);
  assert.match(source, /flex min-w-max gap-2 pb-2 pr-2/);
  assert.match(source, /!flex items-center justify-between gap-3 border-b px-4 pb-2 pt-4/);
  assert.match(source, /历史趋势图分析/);
  assert.match(source, /aria-label="时间对齐颗粒度"/);
  assert.match(source, /className="h-7 w-\[110px\]" aria-label="快捷时间范围"/);
  assert.match(source, /className="flex items-center gap-1" aria-label="时间范围"/);
  assert.equal((source.match(/className="h-7 w-\[148px\]"/g) ?? []).length, 2);
  assert.match(source, /className="!h-7 w-20" aria-label="时间对齐颗粒度"/);
  assert.match(source, /currentDevice \? currentDevice\.deviceCode \?\? currentDevice\.name : "选择设备"/);
  assert.match(source, /ml-auto flex items-center gap-1/);
  assert.match(source, /历史数据矩阵/);
  assert.match(source, /<CardHeader className="!flex flex-row items-center justify-between space-y-0"><CardTitle className="text-base">历史数据矩阵<\/CardTitle>/);
});

test("device communication uses compact stat cards and shadcn progress", async () => {
  const source = await readFile(new URL("../src/pages/DeviceCommunicationPage.tsx", import.meta.url), "utf8");

  assert.match(source, /from "@\/components\/ui\/progress"/);
  assert.match(source, /<Progress/);
  assert.match(source, /min-h-24/);
});

test("device communication renders grouped full-width fifteen-column square status tiles", async () => {
  const source = await readFile(new URL("../src/pages/DeviceCommunicationPage.tsx", import.meta.url), "utf8");

  assert.match(source, /grid-cols-\[repeat\(15,minmax\(0,1fr\)\)\]/);
  assert.match(source, /grid w-full/);
  assert.match(source, /aspect-square/);
  assert.doesNotMatch(source, /h-8 w-14/);
  assert.doesNotMatch(source, /max-w-\[1680px\]/);
  assert.match(source, /device\.deviceCode/);
  assert.match(source, /bg-success text-white/);
  assert.match(source, /bg-warning text-white/);
  assert.match(source, /bg-destructive text-white/);
  assert.match(source, /bg-muted text-foreground/);
  assert.doesNotMatch(source, /bg-(?:success|warning|destructive)-soft/);
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
  const header = source.match(/<CardHeader[\s\S]*?<\/CardHeader>/)?.[0] ?? "";

  assert.match(header, /<CardTitle>设备通讯<\/CardTitle>/);
  assert.match(header, />在线</);
  assert.match(header, />异常</);
  assert.match(header, />离线</);
  assert.match(header, />未启用</);
  assert.match(header, /placeholder="编号 \/ 名称"/);
  assert.match(header, /placeholder="全部类型"/);
  assert.match(header, /placeholder="全部状态"/);
  assert.doesNotMatch(source, /RefreshCw|query\.refresh/);
  assert.doesNotMatch(header, /flex-wrap/);
  assert.match(header, /ml-auto flex min-w-0 flex-1 items-center justify-end/);
  assert.match(header, /<CardHeader className="flex flex-row items-center gap-3">/);
  assert.match(header, /relative w-32 shrink-0/);
  assert.equal((header.match(/SelectTrigger className="w-24"/g) ?? []).length, 2);
  assert.match(header, /<Button variant="outline" size="sm"[\s\S]*?>重置<\/Button>/);
});
test("driver management uses a full-height paginated table with truncated description tooltips", async () => {
  const source = await readFile(new URL("../src/pages/DriverManagementPage.tsx", import.meta.url), "utf8");

  assert.doesNotMatch(source, /驱动总数|可用驱动/);
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
  assert.match(source, /每页行数/);
  assert.match(source, /共 \{query\.data\?\.total \?\? 0\} 条驱动/);
  assert.match(source, /ChevronsLeft/);
  assert.match(source, /ChevronLeft/);
  assert.match(source, /ChevronRight/);
  assert.match(source, /ChevronsRight/);
  assert.match(source, /\[10, 20, 50\]/);
  assert.match(source, /source === "bundled"/);
  assert.match(source, /item\.aliases/);
  assert.match(source, /protected: bundled/);
  assert.match(source, /&& !row\.protected/);
  assert.match(source, /安装包内置/);
});

test("device and point management use dynamic type tabs with full-height paginated lists", async () => {
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
  assert.match(pointSource, /activeDeviceIds/);
  assert.match(pointSource, /filteredRows/);
  assert.match(pointSource, /activeType === "all"/);
  assert.match(deviceSource, /const loadDeviceCatalog = useCallback/);
  assert.match(deviceSource, /loadAllDevicePages/);
  assert.match(deviceSource, /backendClient\.deviceList/);
  assert.match(deviceSource, /const typeTabs = useMemo/);
  assert.doesNotMatch(deviceSource, /historyDeviceTypes/);
  const buttonSource = await readFile(new URL("../src/components/ui/button.tsx", import.meta.url), "utf8");
  assert.match(buttonSource, /cursor-pointer/);
});

test("device and point management keep the selected-item deletion action without a batch label", async () => {
  for (const file of ["DeviceManagementPage.tsx", "PointManagementPage.tsx"]) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");
    assert.match(source, /onClick=\{\(\) => setBatchDeleteOpen\(true\)\}/);
    assert.match(source, />删除<\/Button>/);
  }
});
test("device and point lists keep filters outside the card and size pages from the table viewport", async () => {
  for (const file of ["DeviceManagementPage.tsx", "PointManagementPage.tsx"]) {
    const source = await readFile(new URL(`../src/pages/${file}`, import.meta.url), "utf8");
    assert.match(source, /useAdaptivePageSize/);
    assert.match(source, /ref=\{tableViewportRef\}/);
    assert.match(source, /Input className="h-8 pl-9"/);
    assert.match(source, /SelectTrigger className="!h-8 w-24"/);
    assert.match(source, /\[&_\[data-slot=table-container\]\]:h-full/);
    assert.match(source, /<Table className="h-full w-full table-fixed"/);
    assert.match(source, /<div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">/);
    assert.ok(source.indexOf("<Tabs") < source.indexOf('<Card className="flex min-h-0 flex-1 flex-col border border-border">'));
  }
});
test("point management uses server pagination and passes the selected device ids to the catalog API", async () => {
  const pointSource = await readFile(new URL("../src/pages/PointManagementPage.tsx", import.meta.url), "utf8");
  const pointTypes = await readFile(new URL("../src/types/device-monitor.ts", import.meta.url), "utf8");
  const routerSource = await readFile(new URL("../../backend/app/api/routes/catalog.py", import.meta.url), "utf8");
  const serviceSource = await readFile(new URL("../../backend/app/services/catalog.py", import.meta.url), "utf8");
  assert.doesNotMatch(pointSource, /pointList\(token, \{[^}]*pageSize:\s*1000/);
  assert.match(pointSource, /pageNo:\s*page/);
  assert.match(pointSource, /deviceIds:\s*pointDeviceIds/);
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
