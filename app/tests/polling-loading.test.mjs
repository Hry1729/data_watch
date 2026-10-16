import assert from "node:assert/strict";
import test from "node:test";
import { deferred, hookHarness, settle } from "./helpers/hook-harness.mjs";

for (const [name, method, args] of [["useModuleMetrics", "collectorMetrics", ["collector"]], ["useModuleRates", "moduleRates", []]]) {
  test(`${name} does not overlap slow requests and ignores completion after unmount`, async () => {
    const request = deferred();
    let calls = 0, logouts = 0;
    const h = hookHarness(`hooks/${name}.ts`, name, {
      "@/lib/backend-client": { backendClient: { [method]: () => { calls++; return request.promise; } } },
      "@/contexts/AuthContext": { useAuth: () => ({ auth: { backendToken: "test" }, logout() { logouts++; } }) },
    }, args);
    h.tick(); h.tick(); h.visibility(true); h.visibility(false);
    assert.equal(calls, 1);
    h.unmount();
    request.reject({ type: "unauthorized" });
    await settle();
    assert.equal(logouts, 0);
  });
}

test("backend polling pauses while hidden and resumes without overlapping a slow request", async () => {
  const request = deferred();
  let calls = 0;
  const h = hookHarness("hooks/useBackendQuery.ts", "useBackendQuery", {}, [() => { calls++; return request.promise; }, { intervalMs: 1000 }]);
  try {
    h.visibility(true);
    request.resolve("ready");
    await settle();
    h.tick();
    assert.equal(calls, 1);
    h.visibility(false);
    h.tick();
    assert.equal(calls, 2);
    await settle();
    assert.equal(h.value.data, "ready");
  } finally { h.unmount(); }
});
