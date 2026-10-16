import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

for (const page of ["CollectorPage", "WriterPage"]) {
  test(`${page} fills the viewport without fixed page-level heights`, async () => {
    const source = await readFile(
      new URL(`../src/pages/${page}.tsx`, import.meta.url),
      "utf8",
    );

    assert.doesNotMatch(source, /style=\{\{ height: (595|560) \}\}/);
    assert.match(source, /h-full min-h-0/);
    assert.match(source, /min-h-0 flex-1 overflow-hidden/);
    assert.match(source, /fillHeight/);
  });
}

test("monitoring panel clipping containers preserve the shared card ring", async () => {
  for (const page of ["CollectorPage", "WriterPage"]) {
    const source = await readFile(
      new URL(`../src/pages/${page}.tsx`, import.meta.url),
      "utf8",
    );

    assert.match(source, /grid-rows-\[260px_minmax\(0,1fr\)\][^"]*overflow-hidden p-px/);
    assert.equal(source.match(/<TabsContent[^>]*className="flex min-h-0 overflow-hidden p-px"/g)?.length, 2);
  }
});

test("TrendChart supports opt-in parent-height filling", async () => {
  const source = await readFile(
    new URL("../src/components/TrendChart.tsx", import.meta.url),
    "utf8",
  );

  assert.match(source, /fillHeight\?: boolean/);
  assert.match(source, /fillHeight \? "h-full"/);
});


for (const panel of ["LogPanel", "ModuleDbLogPanel"]) {
  test(`${panel} uses the shared Card container instead of recreating its border`, async () => {
    const source = await readFile(
      new URL(`../src/components/${panel}.tsx`, import.meta.url),
      "utf8",
    );

    assert.match(source, /import \{[^}]*\bCard\b[^}]*\bCardContent\b[^}]*\bCardHeader\b[^}]*\}/);
    assert.match(source, /<Card[\s\S]*<CardHeader[\s\S]*<CardContent/);
    assert.doesNotMatch(source, /ring-1 ring-foreground\/10|style=\{\{ borderRadius: 12 \}\}/);
  });
}

test("the three large monitoring panels rely on the shared card surface", async () => {
  const trendSource = await readFile(
    new URL("../src/components/TrendChart.tsx", import.meta.url),
    "utf8",
  );
  assert.doesNotMatch(trendSource, /border border-border|style=\{\{ borderRadius: 12 \}\}/);

  for (const page of ["CollectorPage", "WriterPage"]) {
    const source = await readFile(
      new URL(`../src/pages/${page}.tsx`, import.meta.url),
      "utf8",
    );
    assert.doesNotMatch(source, /style=\{\{ borderRadius: 12 \}\}/);
  }
});
