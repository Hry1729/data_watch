import assert from 'node:assert/strict'
import test from 'node:test'
import { isQualityAbnormal } from '../src/utils/realtime-quality'

test('GOOD quality (q=0) and missing quality are not abnormal', () => {
  assert.equal(isQualityAbnormal(0), false)
  assert.equal(isQualityAbnormal(undefined), false)
  assert.equal(isQualityAbnormal(null), false)
})

test('every non-zero platform quality code is abnormal', () => {
  for (const q of [2, 5, 6, 7, 8, 255]) {
    assert.equal(isQualityAbnormal(q), true, `q=${q}`)
  }
})
