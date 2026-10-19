import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";

const source = await readFile(new URL("../src/pages/RealtimeMonitorPage.tsx", import.meta.url), "utf8");
const functions = source.slice(source.indexOf("function normalizePointValue("), source.indexOf("function formatSyncTime("));
const context = {};
runInNewContext(ts.transpileModule(functions + "\nglobalThis.normalize = normalizePointValue; globalThis.chart = chartValue;", {
  compilerOptions: { target: ts.ScriptTarget.ES2022 },
}).outputText, context);

test("missing realtime readings remain gaps instead of fabricated zero or OFF", () => {
  for (const type of ["REAL", "BOOL", "STRING"]) {
    for (const value of [null, undefined, "", "  "]) {
      assert.equal(context.normalize({ dataType: type }, value), "--");
      assert.equal(context.chart(context.normalize({ dataType: type }, value)), null);
    }
  }
  assert.equal(context.normalize({ dataType: "BOOL" }, "invalid"), "--");
  assert.equal(context.normalize({ dataType: "REAL" }, Infinity), "--");
});

test("valid zero, false and numeric readings retain their meaning", () => {
  assert.equal(context.normalize({ dataType: "REAL" }, "0"), 0);
  assert.equal(context.normalize({ dataType: "REAL" }, "12.5"), 12.5);
  for (const value of [false, 0, "false", "OFF"]) assert.equal(context.normalize({ dataType: "BOOL" }, value), false);
  for (const value of [true, 1, "true", "ON"]) assert.equal(context.normalize({ dataType: "BOOL" }, value), true);
});
