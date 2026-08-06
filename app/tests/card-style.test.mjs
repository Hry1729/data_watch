import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../src/components/ui/card.tsx", import.meta.url), "utf8");

test("shared cards use one thin semantic border", () => {
  assert.match(source, /group\/card[\s\S]*border border-border/);
  assert.doesNotMatch(source, /ring-1 ring-foreground\/10/);
});
