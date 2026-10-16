import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
import { runInNewContext } from "node:vm";
import ts from "typescript";

const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const mockSource = await readFile(new URL("../src/lib/mock-agent.ts", import.meta.url), "utf8");

test("desktop device requests omit unused summary work without losing status filters", () => {
  const querySource = clientSource.slice(clientSource.indexOf("function toQuery("), clientSource.indexOf("// ─── realBackend"));
  const methodSource = clientSource.slice(clientSource.indexOf("  deviceList(token, query, signal) {"), clientSource.indexOf("  createDevice(token, payload) {"));
  const code = ts.transpileModule(querySource + "\nconst client = {" + methodSource + "}; globalThis.client = client;", {
    compilerOptions: { target: ts.ScriptTarget.ES2022 },
  }).outputText;
  let request;
  const context = { URLSearchParams, backendFetch: (...args) => { request = args; } };
  runInNewContext(code, context);
  const signal = new AbortController().signal;
  context.client.deviceList("test-token", { pageNo: 2, pageSize: 20, connectStatus: "online" }, signal);
  const url = new URL(request[1], "http://test");
  assert.equal(url.searchParams.get("includeSummary"), "false");
  assert.equal(url.searchParams.get("connectStatus"), "online");
  assert.equal(url.searchParams.get("pageNo"), "2");
  assert.equal(request[3], "test-token");
  assert.equal(request[4], signal);
  const deviceIds = Array.from({ length: 10000 }, (_, index) => index + 1);
  context.client.deviceList("test-token", { deviceIds, pageSize: 1 }, signal);
  assert.equal(request[0], "POST");
  assert.equal(request[1], "/api/device-monitor/device/query");
  assert.equal(request[2].deviceIds.length, 10000);
  assert.equal(request[2].includeSummary, false);
  assert.equal(request[4], signal);
});

const operations = [
  "driverList",
  "driverOptions",
  "deviceGroupTree",
  "deviceList",
  "createDevice",
  "updateDevice",
  "deleteDevice",
  "pointDeviceTree",
  "pointList",
  "createPoint",
  "updatePoint",
  "deletePoint",
  "realtimeInit",
  "historyDevices",
  "historyDeviceTypes",
  "historyPoints",
  "historyChart",
  "historyRawMatrix",
];

test("backend client exposes device-monitor operations in real, facade, and mock implementations", () => {
  for (const operation of operations) {
    assert.ok((clientSource.match(new RegExp(`${operation}\\(`, "g")) ?? []).length >= 3, operation);
    assert.match(mockSource, new RegExp(`async ${operation}\\(`), operation);
  }
});

test("device-monitor client uses existing backend routes", () => {
  for (const path of [
    "/api/device-monitor/driver/list",
    "/api/device-monitor/device/group-tree",
    "/api/device-monitor/device/list",
    "/api/device-monitor/point/device-tree",
    "/api/device-monitor/point/list",
    "/api/device-monitor/monitor/realtime-init",
    "/api/device-monitor/history/devices",
    "/api/device-monitor/history/device-types",
    "/api/device-monitor/history/points",
    "/api/device-monitor/history/chart",
    "/api/device-monitor/history/raw-matrix",
  ]) {
    assert.match(clientSource, new RegExp(path.replaceAll("/", "\\/")));
  }
});
