import assert from "node:assert/strict";
import test, { afterEach } from "node:test";
import { clearBackendSession, setBackendSession, withBackendSession } from "../src/lib/backend-session.ts";

const expired = { type: "unauthorized" };
const tokens = { token: "new-access", refreshToken: "new-refresh" };
const start = (update = (_value: unknown) => {}) => setBackendSession({ token: "old-access", refreshToken: "old-refresh" }, update);
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>(yes => { resolve = yes; });
  return { promise, resolve };
}
afterEach(clearBackendSession);

test("simultaneous 401 responses share a refresh and retry with rotated credentials", async () => {
  let refreshes = 0, updates = 0;
  start(() => updates++);
  const gate = deferred<typeof tokens>();
  const request = async (token?: string) => { if (token === "old-access") throw expired; return token; };
  const refresh = async (token: string) => { assert.equal(token, "old-refresh"); refreshes++; return gate.promise; };
  const a = withBackendSession("old-access", request, refresh);
  const b = withBackendSession("Bearer old-access", request, refresh);
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(refreshes, 1);
  gate.resolve(tokens);
  assert.deepEqual(await Promise.all([a, b]), ["new-access", "new-access"]);
  assert.equal(updates, 1);
  assert.equal(await withBackendSession("old-access", request, refresh), "new-access");
  assert.equal(refreshes, 1);
});

test("refresh or retry rejection terminates rather than looping", async () => {
  start();
  let requests = 0, refreshes = 0;
  await assert.rejects(withBackendSession("old-access", async () => { requests++; throw expired; }, async () => { refreshes++; return tokens; }), expired);
  assert.equal(requests, 2);
  assert.equal(refreshes, 1);
});

test("transient refresh network failure preserves the session for a later retry", async () => {
  start();
  const network = { type: "network_error" };
  await assert.rejects(withBackendSession("old-access", async () => { throw expired; }, async () => { throw network; }), network);
  assert.equal(await withBackendSession("old-access", async token => { if (token === "old-access") throw expired; return token; }, async () => tokens), "new-access");
});

test("logout or account switch during refresh cannot restore an old session", async () => {
  let updates = 0;
  start(() => updates++);
  const gate = deferred<typeof tokens>();
  const pending = withBackendSession("old-access", async () => { throw expired; }, async () => gate.promise);
  await new Promise(resolve => setImmediate(resolve));
  setBackendSession({ token: "other-user", refreshToken: "other-refresh" }, () => updates++);
  gate.resolve(tokens);
  await assert.rejects(pending, { name: "AbortError" });
  assert.equal(updates, 0);
});

test("one cancelled page does not cancel the shared refresh needed by another page", async () => {
  start();
  const gate = deferred<typeof tokens>();
  const controller = new AbortController();
  let sharedSignal: AbortSignal | undefined;
  const refresh = async (_token: string, signal: AbortSignal) => { sharedSignal = signal; return gate.promise; };
  const request = async (token?: string) => { if (token === "old-access") throw expired; return token; };
  const a = withBackendSession("old-access", request, refresh, controller.signal);
  const b = withBackendSession("old-access", request, refresh);
  await new Promise(resolve => setImmediate(resolve));
  controller.abort();
  await assert.rejects(a, { name: "AbortError" });
  assert.equal(sharedSignal?.aborted, false);
  gate.resolve(tokens);
  assert.equal(await b, "new-access");
});

test("login failures and business 403s do not trigger renewal", async () => {
  start();
  let refreshes = 0;
  const refresh = async () => { refreshes++; return tokens; };
  await assert.rejects(withBackendSession(undefined, async () => { throw expired; }, refresh), expired);
  const forbidden = { type: "api_error", status: 403 };
  await assert.rejects(withBackendSession("old-access", async () => { throw forbidden; }, refresh), forbidden);
  assert.equal(refreshes, 0);
});
