import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../src/components/ui/card.tsx", import.meta.url), "utf8");

test("shared shadcn cards use the same explicit border as console cards", () => {
  assert.match(source, /group\/card[\s\S]*border border-border/);
  assert.match(source, /ring-1 ring-foreground\/10/);
});