import assert from "node:assert/strict";
import test from "node:test";
import { decimateTimeSeries } from "../src/components/charts/decimate-time-series.ts";

test("decimation retains an isolated offline sample without exceeding the render budget", () => {
  const rows = Array.from({ length: 601 }, (_, sampleAt) => ({ sampleAt, v1: sampleAt === 1 ? null : sampleAt }));
  const result = decimateTimeSeries(rows, 600, ["v1"]);
  assert.ok(result.length <= 600);
  assert.ok(result.includes(rows[1]));
  assert.equal(rows[1].v1, null);
});

test("multiple series never bridge their different null intervals", () => {
  const rows = Array.from({ length: 3000 }, (_, sampleAt) => ({ sampleAt,
    v1: sampleAt >= 100 && sampleAt < 600 ? null : sampleAt,
    v2: sampleAt >= 900 && sampleAt < 1400 ? null : -sampleAt }));
  const result = decimateTimeSeries(rows, 600, ["v1", "v2"]);
  assert.ok(result.length <= 600);
  for (let i = 1; i < result.length; i++) {
    for (const key of ["v1", "v2"] as const) {
      if (result[i - 1][key] === null || result[i][key] === null) continue;
      assert.ok(rows.slice(result[i - 1].sampleAt, result[i].sampleAt + 1).every(row => row[key] !== null));
    }
  }
  assert.equal(result[0], rows[0]);
  assert.equal(result.at(-1), rows.at(-1));
});

test("excessive gap boundaries use a contiguous recent range", () => {
  const rows = Array.from({ length: 3000 }, (_, sampleAt) => ({ sampleAt, v1: sampleAt % 2 ? null : sampleAt }));
  assert.deepEqual(decimateTimeSeries(rows, 600, ["v1"]), rows.slice(-600));
});

// B063：候选桶从第一个内部桶开始，终点只保留一次
test("LTTB keeps a spike in the first, middle and last interior bucket without duplicating the end", () => {
  for (const spikeAt of [1, 1500, 2998]) {
    const rows = Array.from({ length: 3000 }, (_, sampleAt) => ({ sampleAt, v1: sampleAt === spikeAt ? 100 : 0 }));
    const result = decimateTimeSeries(rows, 600, ["v1"]);
    assert.equal(result.length, 600);
    assert.ok(result.includes(rows[spikeAt]), `spike at ${spikeAt} must survive`);
    assert.equal(result[0], rows[0]);
    assert.equal(result.at(-1), rows.at(-1));
    assert.notEqual(result.at(-2), result.at(-1), "终点不得重复");
    assert.equal(new Set(result).size, result.length);
  }
});
