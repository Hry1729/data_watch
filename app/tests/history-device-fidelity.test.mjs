import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const deviceSource = await readFile(new URL("../src/pages/DeviceManagementPage.tsx", import.meta.url), "utf8");
const pointSource = await readFile(new URL("../src/pages/PointManagementPage.tsx", import.meta.url), "utf8");
const historySource = await readFile(new URL("../src/pages/HistoryDataPage.tsx", import.meta.url), "utf8");
const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const chartSource = await readFile(new URL("../src/components/ui/chart.tsx", import.meta.url), "utf8");

test("device rows navigate to the selected device points and the point page consumes the query", () => {
  assert.match(deviceSource, /useNavigate/);
  assert.match(deviceSource, /\/device\/points/);
  assert.match(deviceSource, /deviceId/);
  assert.match(deviceSource, /deviceCode/);
  assert.match(deviceSource, /查看点位/);
  assert.match(pointSource, /useSearchParams/);
  assert.match(pointSource, /searchParams\.get\("deviceId"\)/);
  assert.match(pointSource, /searchParams\.get\("deviceCode"\)/);
});

test("history query and export retain the original point limits and dialogs", () => {
  assert.doesNotMatch(historySource, /slice\(0,\s*6\)/);
  assert.match(historySource, /QUERY_POINT_LIMIT\s*=\s*10/);
  assert.match(historySource, /EXPORT_POINT_LIMIT\s*=\s*20/);
  assert.match(historySource, /选择查询点位/);
  assert.match(historySource, /导出历史数据/);
  assert.match(historySource, /开始导出/);
  assert.match(historySource, /最多20个/);
});

test("history page offers the original chart modes and paged matrix controls", () => {
  for (const label of ["折线图", "面积图", "柱状图", "散点图", "历史数据矩阵", "对齐粒度", "加载更多"]) {
    assert.match(historySource, new RegExp(label), label);
  }
  assert.match(historySource, /cursorTs/);
  assert.match(clientSource, /cursorTs\?: string/);
});

test("history matrix keeps the last submitted query instead of mixing current controls", () => {
  assert.ok((historySource.match(/lastQueryRef\.current = null/g) ?? []).length >= 2);
  assert.match(historySource, /const effectiveQuery = query \?\? lastQueryRef\.current\?\.query/);
  assert.match(historySource, /historyRawMatrix\(token, \{\s*\.\.\.effectiveQuery,/);
});

test("history chart covers the submitted range and supports brush zoom", () => {
  assert.match(historySource, /parseHistoryTime/);
  assert.match(historySource, /rangeStart/);
  assert.match(historySource, /dataKey="time"[\s\S]*type="number"[\s\S]*domain=\{axisDomain\}/);
  assert.match(historySource, /<Brush[\s\S]*onChange=\{handleBrushChange\}/);
  assert.match(historySource, /submittedQuery/);
  assert.match(historySource, /重置缩放/);
});

test("history failures preserve the backend diagnostic message", () => {
  assert.match(clientSource, /status >= 400[\s\S]*message: json\.msg \|\| "后端服务异常"/);
  assert.match(historySource, /function historyErrorMessage/);
  assert.match(historySource, /historyErrorMessage\(reason, "历史数据查询失败"\)/);
  assert.match(historySource, /historyErrorMessage\(reason, "历史数据矩阵加载失败"\)/);
});

test("chart tooltip formatters receive the raw numeric x-axis label", () => {
  assert.match(chartSource, /labelFormatter\(label, payload\)/);
  assert.doesNotMatch(chartSource, /labelFormatter\(value, payload\)/);
});
