import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const read = (relative) => readFile(new URL(relative, import.meta.url), "utf8");

test("edge point monitor shows the real point name and only falls back when it is missing", async () => {
  const source = await read("../src/pages/EdgePointMonitorPage.tsx");

  assert.match(source, /function displayPointName\(/);
  assert.match(source, /row\.pointName && row\.pointName !== String\(row\.varSeq\)/);
  assert.match(source, /`点位 \$\{row\.varSeq\}`/);
  // 列表和详情弹窗走同一套显示口径
  assert.match(source, /displayPointName\(row\)/);
  assert.match(source, /displayPointName\(detail\)/);
  // 不能再用「序号等于名称」这种旧判断
  assert.doesNotMatch(source, /row\.pointName === String\(row\.varSeq\)/);
});

test("point data types separate the display caliber from the creatable whitelist", async () => {
  const source = await read("../src/types/device-monitor.ts");

  // 可新建/编辑的物理类型白名单不变
  assert.match(
    source,
    /export type PointDataType = "BOOL" \| "INT" \| "DINT" \| "REAL" \| "STRING" \| "WORD" \| "DWORD"/,
  );
  // 显示口径覆盖 BYTE 等真实存在、但此前被伪装成 STRING 的类型
  assert.match(source, /export type PointDisplayDataType =/);
  for (const type of ["BYTE", "UINT", "USINT", "UDINT", "TIMER_MS", "COUNTER"]) {
    assert.match(source, new RegExp(`"${type}"`), type);
  }
  assert.match(source, /export const POINT_DISPLAY_DATA_TYPES: PointDisplayDataType\[\]/);
  assert.match(source, /dataType: PointDisplayDataType;/);
});

test("point management widens the type filter but keeps the creatable form options", async () => {
  const source = await read("../src/pages/PointManagementPage.tsx");

  assert.match(
    source,
    /const DATA_TYPES: PointDataType\[\] = \["BOOL", "INT", "DINT", "REAL", "STRING", "WORD", "DWORD"\]/,
  );
  // 筛选下拉走显示口径全集
  assert.match(source, /POINT_DISPLAY_DATA_TYPES\.map/);
});
