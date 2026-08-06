import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const overviewSource = await readFile(
  new URL("../src/pages/OverviewPage.tsx", import.meta.url),
  "utf8",
);
const cardSource = await readFile(
  new URL("../src/components/ModuleSummaryCard.tsx", import.meta.url),
  "utf8",
);

test("distributed center console controls ingest and writer", () => {
  assert.match(
    overviewSource,
    /BUILD_ROLE === "distributed-center"[\s\S]*\["ingest", "writer"\]/,
  );
  assert.match(cardSource, /ingest:[\s\S]*label: "Ingest 数据汇聚模块"/);
  assert.ok(overviewSource.includes("navigate(`/collection/${name}`"));
});

test("module errors stay inside their module grid cell", () => {
  assert.match(overviewSource, /<div className="min-w-0 space-y-1">[\s\S]*showError/);
  assert.doesNotMatch(overviewSource, /return \(\s*<>[\s\S]*<ModuleSummaryCard/);
});
