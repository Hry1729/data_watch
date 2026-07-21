import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const mockSource = await readFile(new URL("../src/lib/mock-agent.ts", import.meta.url), "utf8");

const operations = [
  "systemLogList",
  "logStats",
  "toggleLogDebug",
  "cleanupLogs",
  "tdengineLogFiles",
  "tdengineLogTail",
];

test("backend client exposes log operations in real, facade, and mock implementations", () => {
  for (const operation of operations) {
    assert.ok((clientSource.match(new RegExp(`${operation}\\(`, "g")) ?? []).length >= 3, operation);
    assert.match(mockSource, new RegExp(`async ${operation}\\(`), operation);
  }
});

test("log client uses the existing backend routes", () => {
  for (const path of [
    "/api/device-monitor/log/list",
    "/api/device-monitor/log/stats",
    "/api/device-monitor/log/debug",
    "/api/device-monitor/log/cleanup",
    "/api/device-monitor/log/tdengine/files",
    "/api/device-monitor/log/tdengine/tail",
  ]) {
    assert.match(clientSource, new RegExp(path.replaceAll("/", "\\/")));
  }
});
