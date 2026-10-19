import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../src/components/AppLayout.tsx", import.meta.url), "utf8");

test("settings section switches replace the current history entry", () => {
  assert.match(
    source,
    /navigate\(`\/settings\?tab=\$\{tab.value\}`, \{ replace: true \}\)/,
  );
});

test("opening settings still pushes an entry so Back returns to the previous page", () => {
  assert.equal(source.split("navigate(settingsPath)").length - 1, 1);
  assert.ok(!source.includes("navigate(settingsPath, { replace: true })"));
});
