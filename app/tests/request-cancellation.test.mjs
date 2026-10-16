import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import { webcrypto } from "node:crypto";
import test from "node:test";
import ts from "typescript";
import { deferred, hookHarness, settle } from "./helpers/hook-harness.mjs";

function transport(invoke) {
  const source = readFileSync(new URL("../src/lib/desktop-request.ts", import.meta.url), "utf8");
  const code = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText;
  const exports = {};
  runInNewContext(code, { exports, crypto: webcrypto, Error, DOMException, require: () => ({ invoke }) });
  return exports;
}

test("desktop transport preserves ordinary responses and real failures", async () => {
  const calls = [];
  const expected = [200, "response"];
  const { invokeHttp } = transport(async (command, args) => { calls.push([command, args]); return expected; });
  assert.equal(await invokeHttp("backend_request", { path: "/api/test" }), expected);
  assert.deepEqual(calls, [["backend_request", { path: "/api/test" }]]);
  const failure = new Error("network failed");
  const failing = transport(async () => { throw failure; });
  await assert.rejects(failing.invokeHttp("agent_request", {}), error => error === failure);
});

test("desktop cancellation is isolated by request id and preserves concurrent responses", async () => {
  const requests = new Map();
  const cancellations = [];
  const { invokeHttp } = transport(async (command, args) => {
    if (command === "register_http_request") { requests.set(args.requestId, deferred()); return; }
    if (command === "cancel_http_request") {
      cancellations.push(args.requestId);
      requests.get(args.requestId)?.reject("request_cancelled");
      return;
    }
    return requests.get(args.requestId).promise;
  });
  const first = new AbortController(), second = new AbortController();
  const oldRequest = invokeHttp("backend_request", {}, first.signal);
  const normalRequest = invokeHttp("agent_request", {}, second.signal);
  const cancelled = assert.rejects(oldRequest, error => error.name === "AbortError");
  await settle();
  const [firstId, secondId] = [...requests.keys()];
  assert.notEqual(firstId, secondId);
  first.abort();
  requests.get(secondId).resolve([200, "normal"]);
  await cancelled;
  assert.deepEqual(await normalRequest, [200, "normal"]);
  assert.ok(cancellations.every(id => id === firstId));
  second.abort();
  await settle();
  assert.ok(cancellations.every(id => id === firstId), "completed requests detach their abort listener");
});

test("abort during registration cannot dispatch an uncancellable HTTP request", async () => {
  const registered = deferred();
  const commands = [];
  let cancelled = false;
  const { invokeHttp } = transport(async (command) => {
    commands.push(command);
    if (command === "register_http_request") return registered.promise;
    if (command === "cancel_http_request") { cancelled = true; return; }
    assert.equal(cancelled, true);
    throw "request_cancelled";
  });
  const controller = new AbortController();
  const request = invokeHttp("backend_request", {}, controller.signal);
  controller.abort();
  registered.resolve();
  await assert.rejects(request, error => error.name === "AbortError");
  assert.equal(commands[0], "register_http_request");
  assert.ok(commands.indexOf("cancel_http_request") < commands.indexOf("backend_request"));
  const before = commands.length;
  await assert.rejects(invokeHttp("backend_request", {}, controller.signal), error => error.name === "AbortError");
  assert.equal(commands.length, before);
});

test("query unmount aborts its request and ignores a late unauthorized result", async () => {
  const pending = deferred();
  let signal, logouts = 0;
  const h = hookHarness("hooks/useBackendQuery.ts", "useBackendQuery", {
    "@/contexts/AuthContext": { useAuth: () => ({ auth: { backendToken: "test" }, logout() { logouts++; } }) },
  }, [(_token, nextSignal) => { signal = nextSignal; return pending.promise; }]);
  h.unmount();
  assert.equal(signal.aborted, true);
  pending.reject({ type: "unauthorized" });
  await settle();
  assert.equal(logouts, 0);
});

test("query replacement, manual refresh and disabling abort only the superseded load", async () => {
  const requests = [];
  const load = (_token, signal) => {
    const request = { ...deferred(), signal };
    requests.push(request);
    return request.promise;
  };
  const args = [load, { enabled: true }];
  const h = hookHarness("hooks/useBackendQuery.ts", "useBackendQuery", {}, args);
  try {
    args[0] = (...input) => load(...input);
    h.render();
    assert.equal(requests[0].signal.aborted, true);
    assert.equal(requests[1].signal.aborted, false);
    requests[1].resolve("current");
    await settle();
    assert.equal(h.value.data, "current");
    h.value.refresh();
    await settle();
    assert.equal(requests[1].signal.aborted, true);
    requests[0].reject({ type: "network_error" });
    requests[2].reject({ type: "network_error" });
    await settle();
    assert.equal(h.value.error, "无法连接到后端服务");
    assert.equal(h.value.loading, false);
    h.value.refresh();
    await settle();
    args[1] = { enabled: false };
    h.render();
    assert.equal(requests[3].signal.aborted, true);
    requests[3].reject(new DOMException("cancelled", "AbortError"));
    await settle();
    assert.equal(h.value.error, "无法连接到后端服务");
    assert.equal(h.value.loading, false);
  } finally { h.unmount(); }
});

for (const [hook, method, args] of [
  ["useModuleMetrics", "collectorMetrics", ["collector"]],
  ["useModuleRates", "moduleRates", []],
  ["useModuleDbLogs", "logList", ["writer"]],
]) {
  test(`${hook} cancels the real client signal on unmount`, async () => {
    const request = deferred();
    let signal;
    const h = hookHarness(`hooks/${hook}.ts`, hook, {
      "@/lib/backend-client": { backendClient: { [method]: (...input) => { signal = input.at(-1); return request.promise; } } },
    }, args);
    h.unmount();
    assert.equal(signal.aborted, true);
    request.reject(new DOMException("cancelled", "AbortError"));
    await settle();
  });
}

for (const [hook, method, args] of [
  ["useModuleStatus", "getProcessMetrics", ["collector"]],
  ["useConnectionHealth", "getConnectionHealth", []],
]) {
  test(`${hook} replaces manual refresh, serializes polling and cancels on unmount`, async () => {
    const requests = [];
    const logout = () => {};
    const h = hookHarness(`hooks/${hook}.ts`, hook, {
      "@/lib/agent-client": { agentClient: { [method]: (...input) => {
        const request = { ...deferred(), signal: input.at(-1) };
        requests.push(request);
        return request.promise;
      } } },
      "@/contexts/AuthContext": { useAuth: () => ({ logout }) },
      sonner: { toast: {} },
    }, args);
    try {
      h.tick(); h.tick();
      assert.equal(requests.length, 1);
      h.value.refresh();
      assert.equal(requests.length, 2);
      assert.equal(requests[0].signal.aborted, true);
      requests[0].reject(new DOMException("cancelled", "AbortError"));
      requests[1].resolve({ marker: "new" });
      await settle();
      assert.equal(h.value.data.marker, "new");
      assert.equal(h.value.error, null);
      h.tick();
      h.unmount();
      assert.equal(requests[2].signal.aborted, true);
      requests[2].reject({ type: "unauthorized" });
      await settle();
    } finally { h.unmount(); }
  });
}
