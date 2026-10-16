import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";

const source = await readFile(new URL("../src/lib/monitor-client.ts", import.meta.url), "utf8");
const code = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText;

function client(agentClient, backendClient) {
  const exports = {};
  runInNewContext(code, { exports, require(name) {
    return {
      "@/config/build-role": { BUILD_ROLE: "edge" },
      "@/lib/agent-auth-token": { getAgentAuthToken: () => "local-token" },
      "@/lib/agent-client": { agentClient },
      "@/lib/backend-client": { backendClient },
    }[name];
  } });
  return exports.monitorClient;
}

test("edge selectors read lightweight assignment scope beyond 5000 and retain server filters", async () => {
  const scope = Array.from({ length: 6001 }, (_, i) => ({ id: i + 1, deviceCode: `D${i + 1}`, deviceType: "QC" }));
  const signal = new AbortController().signal;
  const monitor = client({
    async getLocalMonitorScope(actual) { assert.equal(actual, signal); return scope; },
    getLocalMonitorDevices() { throw new Error("must not load full device snapshots for scope"); },
  }, {
    async deviceList(_token, query, actual) {
      assert.equal(actual, signal);
      assert.equal(query.deviceIds.length, 1);
      assert.equal(query.deviceIds[0], 6001);
      assert.equal(query.pageSize, 1);
      return { list: [{ id: 6001 }], total: 1 };
    },
    async pointList(_token, query, actual) {
      assert.equal(actual, signal);
      assert.equal(query.deviceIds.length, 6001);
      assert.equal(query.deviceType, "QC");
      assert.equal(query.connectStatus, "online");
      return { list: [], total: 7 };
    },
  });
  const page = await monitor.deviceList("center-token", { deviceIds: [6001, 99999], pageSize: 1 }, signal);
  assert.equal(page.list[0].id, 6001);
  assert.deepEqual(Array.from(await monitor.deviceTypes("center-token", signal)), ["QC"]);
  assert.equal((await monitor.pointList("center-token", { deviceType: "QC", connectStatus: "online" }, signal)).total, 7);
});

test("empty or unassigned device ranges never widen to the whole edge scope", async () => {
  let requests = 0;
  const monitor = client({ async getLocalMonitorScope() { return [{ id: 1, deviceCode: "D1", deviceType: "QC" }]; } }, {
    deviceList() { requests += 1; }, pointList() { requests += 1; },
  });
  assert.equal((await monitor.deviceList("center-token", { deviceIds: [] })).total, 0);
  assert.equal((await monitor.deviceList("center-token", { deviceIds: [2] })).total, 0);
  assert.equal((await monitor.pointList("center-token", { deviceId: 2 })).total, 0);
  assert.equal((await monitor.pointList("center-token", { deviceIds: [] })).total, 0);
  assert.equal(requests, 0);
});
