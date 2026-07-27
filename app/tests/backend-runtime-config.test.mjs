import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const appSource = await readFile(new URL("../src/App.tsx", import.meta.url), "utf8");

test("backend runtime URL survives Vite hot updates during desktop development", () => {
  assert.match(source, /import\.meta\.hot\?\.data\.backendBaseUrl/);
  assert.match(source, /import\.meta\.hot\.dispose/);
  assert.match(source, /data\.backendBaseUrl = _backendBaseUrl/);
});

test("agent retry reloads the runtime backend URL before leaving the error page", () => {
  assert.match(appSource, /function AgentErrorPage\(\)[\s\S]*getRuntimeConfig\(\)[\s\S]*setBackendBaseUrl\(backend_base_url\)[\s\S]*navigate\("\/login"/);
});
