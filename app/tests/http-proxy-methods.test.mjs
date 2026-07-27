import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";
const proxySource = await readFile(new URL("../src-tauri/src/http_proxy.rs", import.meta.url), "utf8");
const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const appSource = await readFile(new URL("../src/App.tsx", import.meta.url), "utf8");
test("the Tauri backend proxy supports PATCH for system-management status operations", () => {
  const backendBlock = proxySource.slice(proxySource.indexOf("pub async fn backend_request"));
  assert.match(backendBlock, /"PATCH"\s*=>\s*client\.patch/);
});
test("backend client rejects non-success business response codes", () => {
  assert.match(clientSource, /json\.code\s*===\s*401/);
  assert.match(clientSource, /json\.code\s*!==\s*200/);
  assert.match(clientSource, /json\.msg/);
});
test("desktop proxy bounds Agent and backend requests with connection timeouts", () => {
  assert.match(proxySource, /connect_timeout\(Duration::from_secs\(3\)\)/);
  // Agent 支持动态 timeout_secs，默认 5s
  assert.match(proxySource, /timeout_secs\.unwrap_or\(5\)/);
  assert.match(proxySource, /build_client\(timeout\)/);
  assert.match(proxySource, /build_client\(Duration::from_secs\(10\)\)/);
});
test("startup and retry leave the loading state when the Agent never responds", () => {
  assert.match(appSource, /const STARTUP_TIMEOUT_MS = 6_000/);
  assert.match(appSource, /withStartupTimeout\(agentClient\.getBootstrapStatus\(\)\)/);
  assert.match(appSource, /withStartupTimeout\(agentClient\.getRuntimeConfig\(\)\)/);
});
