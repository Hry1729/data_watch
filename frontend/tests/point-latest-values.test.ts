import assert from 'node:assert/strict'
import test from 'node:test'
import { mergePointLatestValues } from '../src/utils/point-latest-values'

test('latest values from an old page cannot be merged into the current page', () => {
  const currentRows = [{ id: 3, pointName: 'new page point' }]
  const merged = mergePointLatestValues(currentRows, [1, 2], {
    '1': { currentValue: 10 },
    '2': { currentValue: 20 }
  })

  assert.equal(merged, null)
  assert.deepEqual(currentRows, [{ id: 3, pointName: 'new page point' }])
})

test('latest values merge into the matching current page by point id', () => {
  const merged = mergePointLatestValues(
    [{ id: 3, pointName: 'point' }],
    [3],
    { '3': { currentValue: 10, updateTime: '2026-09-29 10:00:00' } }
  )

  assert.deepEqual(merged, [{ id: 3, pointName: 'point', currentValue: 10, updateTime: '2026-09-29 10:00:00' }])
})
