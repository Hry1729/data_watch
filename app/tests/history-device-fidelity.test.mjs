import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const deviceSource = await readFile(new URL("../src/pages/DeviceManagementPage.tsx", import.meta.url), "utf8");
const pointSource = await readFile(new URL("../src/pages/PointManagementPage.tsx", import.meta.url), "utf8");
const historySource = await readFile(new URL("../src/pages/HistoryDataPage.tsx", import.meta.url), "utf8");
const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");

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
