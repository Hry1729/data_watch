import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";
import { deferred, settle } from "./helpers/hook-harness.mjs";

const source = await readFile(new URL("../src/components/TitleBar.tsx", import.meta.url), "utf8");
const effect = source.slice(source.indexOf("  useEffect("), source.indexOf("\n  return (\n"));
const code = ts.transpileModule(effect, { compilerOptions: { target: ts.ScriptTarget.ES2022 } }).outputText;

test("titlebar releases a listener that registers after unmount", async () => {
  const registration = deferred(), maximized = deferred();
  let cleanup, releases = 0, updates = 0;
  runInNewContext(code, {
    appWindow: { isMaximized: () => maximized.promise, onResized: () => registration.promise },
    useEffect: fn => { cleanup = fn(); }, setMaximized: () => updates++,
  });
  cleanup();
  registration.resolve(() => releases++);
  maximized.resolve(true);
  await settle();
  assert.equal(releases, 1);
  assert.equal(updates, 0);
});

test("titlebar needs no native window in browser previews", () => {
  runInNewContext(code, {
    appWindow: null, useEffect: fn => assert.equal(fn(), undefined),
    setMaximized: () => assert.fail("no native state should be requested"),
  });
});
