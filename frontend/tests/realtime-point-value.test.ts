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

// B022：缺值 + 异常质量的样本不能在曲线/导出里变成 0。
test('chart sample keeps missing value as null and good zero as 0', async () => {
  const { toChartSampleValue, formatExportSampleValue } = await import('../src/utils/realtime-point-value')
  assert.equal(toChartSampleValue(0), 0)
  assert.equal(toChartSampleValue('0'), 0)
  assert.equal(toChartSampleValue(false), 0)
  assert.equal(toChartSampleValue(true), 1)
  assert.equal(toChartSampleValue(12.5), 12.5)
  assert.equal(toChartSampleValue(undefined), null)
  assert.equal(toChartSampleValue(null), null)
  assert.equal(toChartSampleValue('abc'), null)
  assert.equal(toChartSampleValue(''), null)
  // 原消息循环：只有 t/q 的推送 normalize 后为 undefined
  assert.equal(toChartSampleValue(normalizeWsPointValue('REAL', undefined)), null)

  assert.equal(formatExportSampleValue(0, 0), 0)
  assert.equal(formatExportSampleValue(null, 2), `${MISSING_POINT_VALUE} 质量：异常`)
  assert.notEqual(formatExportSampleValue(null, 2), 0)
})

test('realtime view no longer coerces missing values with Number(value) || 0', async () => {
  const { readFile } = await import('node:fs/promises')
  const source = await readFile(
    new URL('../src/views/device_monitor/realtime/index.vue', import.meta.url),
    'utf8'
  )
  assert.doesNotMatch(source, /Number\(value\)\s*\|\|\s*0/)
  assert.match(source, /value: toChartSampleValue\(value\)/)
})
