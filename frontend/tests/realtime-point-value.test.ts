import assert from 'node:assert/strict'
import test from 'node:test'
import {
  MISSING_POINT_VALUE,
  formatMissingValueWithQuality,
  formatStoredPointValue,
  normalizeWsPointValue
} from '../src/utils/realtime-point-value'

test('quality only (no v) renders the placeholder and quality text, not false', () => {
  assert.equal(normalizeWsPointValue('BOOL', undefined), undefined)
  assert.equal(normalizeWsPointValue('BOOL', null), undefined)
  assert.equal(normalizeWsPointValue('REAL', undefined), undefined)
  assert.equal(normalizeWsPointValue('STRING', null), undefined)

  for (const quality of [5, 8, 192]) {
    const text = formatMissingValueWithQuality(quality)
    assert.equal(text, `${MISSING_POINT_VALUE} 质量：异常`)
    assert.equal(text.includes('false'), false)
  }
  assert.equal(formatMissingValueWithQuality(0), `${MISSING_POINT_VALUE} 质量：GOOD`)
})

test('real BOOL false still renders false', () => {
  assert.equal(normalizeWsPointValue('BOOL', false), false)
  assert.equal(formatStoredPointValue(false), 'false')
  assert.equal(normalizeWsPointValue('BOOL', true), true)
  assert.equal(formatStoredPointValue(true), 'true')
})

test('numeric 0 still renders 0', () => {
  assert.equal(normalizeWsPointValue('INT', 0), 0)
  assert.equal(normalizeWsPointValue('REAL', 0), 0)
  const text = formatStoredPointValue(0)
  assert.equal(text, '0.00')
  assert.equal(text.includes(MISSING_POINT_VALUE), false)
})

test('empty string is a real value, not the missing placeholder', () => {
  assert.equal(normalizeWsPointValue('STRING', ''), '')
  assert.equal(formatStoredPointValue(''), '')
  assert.notEqual(formatStoredPointValue(''), MISSING_POINT_VALUE)
})
