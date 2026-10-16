import assert from "node:assert/strict";
import test from "node:test";
import { hookHarness, settle } from "./helpers/hook-harness.mjs";

test("device picker loads one page and can search beyond the former 10,000-device cap", async () => {
  const requests = [];
  let load;
  const client = { async deviceList(token, query, signal) {
    requests.push({ token, query, signal });
    return { list: [{ id: query.keyword ? 15001 : 1 }], total: query.keyword ? 1 : 20000 };
  } };
  const args = [client, "", "QC", true];
  const h = hookHarness("hooks/useDevicePicker.ts", "useDevicePicker", {
    "@/hooks/useBackendQuery": { useBackendQuery(fn, options) {
      load = fn;
      return { data: null, loading: false, enabled: options.enabled };
    } },
  }, args);
  const signal = new AbortController().signal;
  await load("token", signal);
  assert.equal(requests.length, 1);
  assert.deepEqual(JSON.parse(JSON.stringify(requests[0].query)), { pageNo: 1, pageSize: 50, deviceTypeExact: "QC" });
  assert.equal(requests[0].signal, signal);

  h.value.setPage(201);
  await settle();
  await load("token", signal);
  assert.equal(requests.at(-1).query.pageNo, 201);
  args[1] = "  D15001  ";
  h.render();
  const result = await load("token", signal);
  assert.equal(h.value.page, 1);
  assert.equal(requests.at(-1).query.keyword, "D15001");
  assert.equal(result.list[0].id, 15001);
  h.unmount();
});

test("type changes reset picker pagination and closed dialogs disable catalog requests", async () => {
  const args = [{ deviceList: async () => ({ list: [], total: 0 }) }, "", "all", false];
  let options;
  const h = hookHarness("hooks/useDevicePicker.ts", "useDevicePicker", {
    "@/hooks/useBackendQuery": { useBackendQuery(_load, next) { options = next; return {}; } },
  }, args);
  assert.equal(options.enabled, false);
  h.value.setPage(3);
  await settle();
  assert.equal(h.value.page, 3);
  args[2] = "AGV";
  args[3] = true;
  h.render();
  assert.equal(h.value.page, 1);
  assert.equal(options.enabled, true);
  h.unmount();
});
