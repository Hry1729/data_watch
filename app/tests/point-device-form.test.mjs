// 点位/设备表单补全：驱动跟随设备、序号自动分配、真实报错、类型下拉、机架槽号
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const read = async (path) => (await readFile(new URL(path, import.meta.url), "utf8")).replaceAll("\r\n", "\n");

test("point form takes its driver from the selected device instead of the first driver", async () => {
  const source = await read("../src/pages/PointManagementPage.tsx");
  assert.doesNotMatch(source, /driverName: drivers\.data\?\.\[0\]/);
  assert.match(source, /driverName: device\?\.driverName/);
  assert.match(source, /backendClient\.nextPointSeq\(/);
  assert.match(source, /placeholder="自动分配"/);
});

test("point form surfaces backend errors and sends scan rate and storage flag", async () => {
  const source = await read("../src/pages/PointManagementPage.tsx");
  assert.match(source, /toast\.error\(errorMessage\(reason, "保存点位失败"\)\)/);
  assert.doesNotMatch(source, /catch \{ toast\.error\("保存点位失败"\)/);
  assert.match(source, /scanRate: row\.scanRate \?\? 1000/);
  assert.match(source, /isStorage: row\.isStorage \?\? true/);
  assert.match(source, /写入历史库/);
});

test("device form picks its type from the dictionary and edits rack/slot/scan cycle", async () => {
  const source = await read("../src/pages/DeviceManagementPage.tsx");
  assert.doesNotMatch(source, /deviceType: "PLC"/);
  assert.match(source, /backendClient\.deviceTypeOptions\(/);
  assert.match(source, /backendClient\.deviceDetail\(/);
  assert.match(source, /机架号/);
  assert.match(source, /槽号/);
  assert.match(source, /scanCycle: toOptionalInt/);
});

test("backend client exposes the new form endpoints", async () => {
  const source = await read("../src/lib/backend-client.ts");
  assert.match(source, /\/api\/device-monitor\/device\/type-options/);
  assert.match(source, /\/api\/device-monitor\/point\/next-seq\?/);
  assert.match(source, /async nextPointSeq\(token, deviceId, signal\)/);
});
