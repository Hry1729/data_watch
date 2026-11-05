import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { buildBoolLane, lanePercent } from '../src/utils/bool-lanes'

test('bool lane splits ON runs at state changes and extends the last run to the newest sample', () => {
  const lane = buildBoolLane([
    { time: 0, value: 0 },
    { time: 10, value: 1 },
    { time: 20, value: 1 },
    { time: 30, value: 0 },
    { time: 40, value: 1 },
    { time: 50, value: 1 }
  ])
  assert.deepEqual(lane.runs, [[10, 30], [40, 50]])
  assert.equal(lane.current, true)
})

test('missing value ends an ON run and reports no current state', () => {
  const lane = buildBoolLane([
    { time: 0, value: 1 },
    { time: 10, value: null }
  ])
  assert.deepEqual(lane.runs, [[0, 10]])
  assert.equal(lane.current, null)
  assert.deepEqual(buildBoolLane([]), { runs: [], current: null })
})

test('lanePercent clamps to the lane', () => {
  assert.equal(lanePercent(50, 0, 100), 50)
  assert.equal(lanePercent(-5, 0, 100), 0)
  assert.equal(lanePercent(200, 0, 100), 100)
})

test('realtime page always shows Bool points as status lanes, no separate-chart toggle', () => {
  const page = readFileSync(new URL('../src/views/device_monitor/realtime/index.vue', import.meta.url), 'utf8')
  assert.match(page, /<BoolLanes /)
  assert.doesNotMatch(page, /Bool 型变量独立图表/)
  assert.doesNotMatch(page, /separateBoolChart = state\.separateBoolChart/)
})
