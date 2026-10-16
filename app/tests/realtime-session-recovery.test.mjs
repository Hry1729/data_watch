import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";
import { deferred, settle } from "./helpers/hook-harness.mjs";

const source = readFileSync(new URL("../src/hooks/useRealtimeWebSocket.ts", import.meta.url), "utf8").replaceAll("\r\n", "\n");
const effect = source.slice(source.indexOf("  useEffect(() => {\n    if (!enabled || !token || !deviceCode)"), source.indexOf("\n\n  return {"));
const code = ts.transpileModule(effect.replaceAll("import.meta.env", '({MODE:"production"})'), {
  compilerOptions: { target: ts.ScriptTarget.ES2022 },
}).outputText;

function harness() {
  const sockets = [], checks = [], listeners = new Map();
  let cleanup;
  class WebSocket {
    static CONNECTING = 0;
    static OPEN = 1;
    static CLOSED = 3;
    readyState = WebSocket.CONNECTING;
    constructor() { sockets.push(this); }
    send() {}
    close() { this.readyState = WebSocket.CLOSED; this.onclose?.(); }
  }
  const state = {};
  const context = {
    enabled: true, token: "expired", deviceCode: "device", groupCode: "group", refreshVersion: 0, flushIntervalMs: 1000,
    BUILD_ROLE: "centralized", AbortController, WebSocket, getAgentAuthToken: () => "local",
    backendClient: { userInfo: (token, signal) => { const request = { token, signal, ...deferred() }; checks.push(request); return request.promise; } },
    getBackendWebSocketUrl: () => "ws://localhost/realtime", agentClient: {},
    setStatus: value => { state.status = value; }, setError: value => { state.error = value; },
    setSource() {}, setDeviceOffline() {}, setTdengineConnected() {}, setValues() {}, setLastSyncTime() {},
    pendingRef: { current: {} },
    document: { hidden: false, addEventListener: (name, fn) => listeners.set(name, fn), removeEventListener: name => listeners.delete(name) },
    window: { setInterval() { return 1; }, clearInterval() {}, setTimeout() { return 2; }, clearTimeout() {} },
    useEffect: callback => { cleanup = callback(); },
  };
  runInNewContext(code, context);
  return {
    checks, sockets, state, cleanup: () => cleanup(),
    message: message => sockets.at(-1).onmessage({ data: JSON.stringify(message) }),
    visible: () => listeners.get("visibilitychange")?.(),
  };
}

test("a realtime 401 enters shared HTTP session recovery once and cancels on departure", async () => {
  const h = harness();
  h.message({ type: "error", code: 401, message: "expired" });
  h.message({ type: "error", code: 401, message: "expired" });
  assert.equal(h.checks.length, 1);
  assert.equal(h.checks[0].token, "expired");
  h.sockets[0].close();
  h.visible();
  assert.equal(h.sockets.length, 1, "visibility cannot repeatedly retry rejected credentials");
  h.cleanup();
  assert.equal(h.checks[0].signal.aborted, true);
  h.checks[0].reject(new DOMException("cancelled", "AbortError"));
  await settle();
});

test("a realtime 403 reports denied access without attempting session refresh", () => {
  const h = harness();
  try {
    h.message({ type: "error", code: 403, message: "denied" });
    assert.equal(h.checks.length, 0);
    assert.equal(h.state.error, "denied");
    assert.equal(h.state.status, "error");
  } finally { h.cleanup(); }
});
