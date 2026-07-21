import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const mockSource = await readFile(new URL("../src/lib/mock-agent.ts", import.meta.url), "utf8");
const deviceSource = await readFile(new URL("../src/pages/DeviceManagementPage.tsx", import.meta.url), "utf8");
const pointSource = await readFile(new URL("../src/pages/PointManagementPage.tsx", import.meta.url), "utf8");
const packageSource = await readFile(new URL("../package.json", import.meta.url), "utf8");

test("catalog client exposes existing bulk, import, and export APIs", () => {
  for (const operation of [
    "batchDeleteDevices", "exportDevices", "importDevices",
    "batchDeletePoints", "exportPoints", "importPoints", "importPointsFromTemplate",
  ]) {
    assert.ok((clientSource.match(new RegExp(`${operation}\\(`, "g")) ?? []).length >= 3, operation);
    assert.match(mockSource, new RegExp(`async ${operation}\\(`), operation);
  }
  assert.match(clientSource, /\/device\/batch/);
  assert.match(clientSource, /\/device\/export/);
  assert.match(clientSource, /\/device\/import/);
  assert.match(clientSource, /\/point\/batch/);
  assert.match(clientSource, /\/point\/export/);
  assert.match(clientSource, /\/point\/import\/template/);
});

test("device management renders selection, batch delete, import, and export controls", () => {
  assert.match(deviceSource, /selectedIds/);
  assert.match(deviceSource, /backendClient\.batchDeleteDevices/);
  assert.match(deviceSource, /backendClient\.exportDevices/);
  assert.match(deviceSource, /backendClient\.importDevices/);
  for (const label of ["批量删除", "导入", "导出", "下载模板"]) assert.match(deviceSource, new RegExp(label));
});

test("point management renders all bulk operations including template import", () => {
  assert.match(pointSource, /selectedIds/);
  assert.match(pointSource, /backendClient\.batchDeletePoints/);
  assert.match(pointSource, /backendClient\.exportPoints/);
  assert.match(pointSource, /backendClient\.importPoints/);
  assert.match(pointSource, /backendClient\.importPointsFromTemplate/);
  for (const label of ["批量删除", "导入", "导出", "下载模板", "从模板导入"]) assert.match(pointSource, new RegExp(label));
});

test("desktop frontend includes the XLSX parser used by the original page", () => {
  assert.match(packageSource, /"xlsx"/);
});
