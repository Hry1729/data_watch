import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const mockSource = await readFile(new URL("../src/lib/mock-agent.ts", import.meta.url), "utf8");

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
