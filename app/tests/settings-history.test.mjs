import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../src/components/AppLayout.tsx", import.meta.url), "utf8");

test("settings section switches replace the current history entry", () => {
  assert.match(
    source,
    /navigate\(`\/settings\?tab=\$\{tab\.value\}`, \{ replace: true \}\)/,
  );
});

test("opening settings still pushes an entry so Back returns to the previous page", () => {
  const entryCalls = source.match(/navigate\("\/settings\?tab=database"\)/g) ?? [];
  assert.equal(entryCalls.length, 2);
  assert.doesNotMatch(
    source,
    /navigate\("\/settings\?tab=database", \{ replace: true \}\)/,
  );
});
