import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";
import { deferred, settle } from "./helpers/hook-harness.mjs";

const source = await readFile(new URL("../src/App.tsx", import.meta.url), "utf8");
const startup = source.slice(source.indexOf("function AppStartup("), source.indexOf("function AgentErrorPage("));
const effect = startup.slice(startup.indexOf("  useEffect("), startup.indexOf("// eslint-disable-line"));
const code = ts.transpileModule(effect, { compilerOptions: { target: ts.ScriptTarget.ES2022 } }).outputText;

for (const scenario of ["configured", "bootstrap", "config-failed", "agent-failed"]) {
  test(`startup requests independent configuration concurrently: ${scenario}`, async () => {
    const status = deferred(), config = deferred();
    let configRequested = false, destination = null, base = null, deciding = true;
    runInNewContext(code, {
      agentClient: { getBootstrapStatus: () => status.promise, getRuntimeConfig: () => { configRequested = true; return config.promise; } },
      withStartupTimeout: promise => promise, useEffect: fn => fn(),
      setBackendBaseUrl: value => { base = value; }, navigate: value => { destination = value; },
      setDeciding: value => { deciding = value; },
    });
    assert.equal(configRequested, true);
    if (scenario === "agent-failed") status.reject(new Error("offline"));
    else status.resolve({ in_bootstrap: scenario === "bootstrap" });
    if (scenario === "config-failed") config.reject(new Error("no config"));
    else config.resolve({ backend_base_url: "http://test" });
    await settle();
    assert.equal(deciding, false);
    assert.equal(destination, scenario === "bootstrap" ? "/bootstrap" : scenario === "agent-failed" ? "/error" : null);
    if (scenario === "configured") assert.equal(base, "http://test");
    // B056：引导期间不得固定 Agent 默认占位地址，否则引导完成后无法改用远程 Backend
    if (scenario === "bootstrap") assert.equal(base, null);
  });
}
