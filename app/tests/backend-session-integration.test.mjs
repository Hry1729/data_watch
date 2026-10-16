import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";
import { clearBackendSession, setBackendSession, withBackendSession } from "../src/lib/backend-session.ts";
import { isAllowedBackendMethod, isAllowedBackendPath, requestBodyByteLength, MAX_BACKEND_REQUEST_BYTES } from "../src/lib/backend-proxy-policy.ts";

const source = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const code = ts.transpileModule(source.slice(source.indexOf("async function backendFetch<T>("), source.indexOf("function toQuery(")) + "\nglobalThis.client = { backendFetch, backendDownload };", {
  compilerOptions: { target: ts.ScriptTarget.ES2022 },
}).outputText;

test("real HTTP wrappers refresh once for JSON and binary download, then use the new token", async () => {
  const calls = [];
  const context = {
    _backendBaseUrl: "http://backend.test", _backendOriginReady: Promise.resolve(),
    isAllowedBackendMethod, isAllowedBackendPath, requestBodyByteLength, MAX_BACKEND_REQUEST_BYTES,
    withBackendSession, isAbortError: error => error?.name === "AbortError",
    invokeHttp: async (command, args) => {
      calls.push({ command, ...args });
      if (args.path === "/api/auth/refresh") return [200, JSON.stringify({ code: 200, data: { token: "fresh", refreshToken: "rotated" } })];
      if (args.authorization === "Bearer expired") return command === "backend_download" ? [401, [], null] : [200, JSON.stringify({ code: 401 })];
      return command === "backend_download" ? [200, [1, 2, 3], 'attachment; filename="data.csv"'] : [200, JSON.stringify({ code: 200, data: { ready: true } })];
    },
  };
  runInNewContext(code, context);
  setBackendSession({ token: "expired", refreshToken: "refresh" }, () => {});
  try {
    const [json, file] = await Promise.all([
      context.client.backendFetch("GET", "/api/example", undefined, "expired"),
      context.client.backendDownload("/api/download", "expired"),
    ]);
    assert.equal(json.ready, true);
    assert.equal(file.fileName, "data.csv");
    const refreshes = calls.filter(call => call.path === "/api/auth/refresh");
    assert.equal(refreshes.length, 1);
    assert.equal(refreshes[0].method, "POST");
    assert.equal(refreshes[0].authorization, undefined);
    assert.equal(JSON.parse(refreshes[0].body).refreshToken, "refresh");
    assert.equal(calls.filter(call => call.authorization === "Bearer fresh").length, 2);
  } finally { clearBackendSession(); }
});
