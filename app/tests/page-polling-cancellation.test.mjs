import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";
import { deferred, settle } from "./helpers/hook-harness.mjs";

function pagePolling(file, start, end, expose, context) {
  const source = readFileSync(new URL(`../src/pages/${file}.tsx`, import.meta.url), "utf8").replaceAll("\r\n", "\n");
  const block = source.slice(source.indexOf(start), source.indexOf(end));
  assert.ok(block.length > 0);
  const code = ts.transpileModule(`(() => { ${block}; globalThis.load = ${expose}; })();`, {
    compilerOptions: { target: ts.ScriptTarget.ES2022 },
  }).outputText;
  const timers = new Map();
  let cleanup;
  const addTimer = (fn, repeat) => { const id = {}; timers.set(id, { fn, repeat }); return id; };
  Object.assign(context, {
    AbortController,
    useCallback: fn => fn,
    useEffect: fn => { cleanup = fn(); },
    setTimeout: fn => addTimer(fn, false),
    clearTimeout: id => timers.delete(id),
    window: { setInterval: fn => addTimer(fn, true), clearInterval: id => timers.delete(id) },
  });
  const render = () => runInNewContext(code, context);
  render();
  return {
    context, render,
    unmount: () => cleanup(),
    tick() {
      for (const [id, timer] of [...timers]) {
        if (!timer.repeat) timers.delete(id);
        void timer.fn();
      }
    },
  };
}

function nodesHarness() {
  const requests = [], state = { nodes: null, errors: [] };
  const runtime = pagePolling("DistributedNodesPage", "  const loadNodes = useCallback", "\n\n  const totals", "loadNodes", {
    token: "first",
    nodesRequestRef: { current: null }, assignmentRequestRef: { current: null },
    backendClient: { distributedNodes: (token, signal) => {
      const request = { token, signal, ...deferred() };
      requests.push(request);
      return request.promise;
    } },
    setNodes: nodes => { state.nodes = nodes; }, setLoading() {},
    toast: { error: message => state.errors.push(message) }, errorMessage: () => "failed",
  });
  return { ...runtime, requests, state };
}

test("node polling lets requests spanning several timer ticks finish and resumes afterward", async () => {
  const h = nodesHarness();
  try {
    h.tick(); h.tick(); h.tick();
    assert.equal(h.requests.length, 1);
    assert.equal(h.requests[0].signal.aborted, false);
    h.requests[0].resolve([{ marker: "slow but complete" }]);
    await settle();
    assert.equal(h.state.nodes[0].marker, "slow but complete");
    assert.equal(h.context.nodesRequestRef.current, null);
    h.tick();
    assert.equal(h.requests.length, 2);
    h.unmount();
    assert.equal(h.requests[1].signal.aborted, true);
    h.requests[1].reject({ type: "unauthorized" });
    await settle();
    assert.deepEqual(h.state.errors, []);
  } finally { h.unmount(); }
});

test("forced node refresh replaces a request without letting its late completion release the replacement", async () => {
  const h = nodesHarness();
  try {
    const refresh = h.context.load(true);
    assert.equal(h.requests.length, 2);
    assert.equal(h.requests[0].signal.aborted, true);
    h.requests[0].reject(new DOMException("cancelled", "AbortError"));
    await settle();
    assert.equal(h.context.nodesRequestRef.current.signal, h.requests[1].signal);
    h.tick();
    assert.equal(h.requests.length, 2);
    h.requests[1].resolve([{ marker: "replacement" }]);
    await refresh;
    assert.equal(h.state.nodes[0].marker, "replacement");
    assert.equal(h.context.nodesRequestRef.current, null);
  } finally { h.unmount(); }
});

test("node dependency cleanup cancels old credentials and permits the next request immediately", async () => {
  const h = nodesHarness();
  h.unmount();
  h.context.token = "second";
  h.render();
  try {
    assert.equal(h.requests[0].signal.aborted, true);
    assert.equal(h.requests[1].token, "second");
    h.requests[0].reject(new DOMException("cancelled", "AbortError"));
    h.requests[1].resolve([]);
    await settle();
    assert.deepEqual(h.state.errors, []);
  } finally { h.unmount(); }
});

test("edge operations serializes slow snapshots and cancels the whole snapshot on departure", async () => {
  const requests = [], state = { snapshot: null, error: null };
  const agentClient = Object.fromEntries(["getLocalDevices", "getLocalCollector", "getLocalStorage", "getLocalUploader"].map(method => [method, signal => {
    const request = { signal, method, ...deferred() };
    requests.push(request);
    return request.promise;
  }]));
  const h = pagePolling("EdgeOperationsPage", "  const refresh = useCallback", "\n\n  async function rotateKey", "refresh", {
    mounted: { current: true }, requestRef: { current: null }, agentClient,
    setSnapshot: value => { state.snapshot = value; }, setError: value => { state.error = value; },
  });
  try {
    h.tick(); await settle(); h.tick(); await settle();
    assert.equal(requests.length, 4);
    assert.ok(requests.every(request => !request.signal.aborted));
    requests.forEach(request => request.resolve({ marker: request.method }));
    await settle();
    assert.equal(state.snapshot.devices.marker, "getLocalDevices");
    h.tick();
    assert.equal(requests.length, 8);
    h.unmount();
    assert.ok(requests.slice(4).every(request => request.signal.aborted));
    requests.slice(4).forEach(request => request.reject(new DOMException("cancelled", "AbortError")));
    await settle();
    assert.equal(state.error, null);
  } finally { h.unmount(); }
});

test("a failed edge snapshot cancels pending siblings and preserves the genuine error", async () => {
  const requests = [], state = { error: null };
  const agentClient = Object.fromEntries(["getLocalDevices", "getLocalCollector", "getLocalStorage", "getLocalUploader"].map(method => [method, signal => {
    const request = { signal, ...deferred() };
    requests.push(request);
    return request.promise;
  }]));
  const h = pagePolling("EdgeOperationsPage", "  const refresh = useCallback", "\n\n  async function rotateKey", "refresh", {
    mounted: { current: true }, requestRef: { current: null }, agentClient,
    setSnapshot() {}, setError: value => { state.error = value; },
  });
  try {
    requests[0].reject({ type: "agent_unreachable" });
    await settle();
    assert.equal(state.error, "本机状态读取失败");
    assert.ok(requests.every(request => request.signal.aborted));
    assert.equal(h.context.requestRef.current, null);
    requests.slice(1).forEach(request => request.reject(new DOMException("cancelled", "AbortError")));
    await settle();
  } finally { h.unmount(); }
});
