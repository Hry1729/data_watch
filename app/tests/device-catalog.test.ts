import assert from 'node:assert/strict'
import test from 'node:test'
import { DEVICE_CATALOG_MAX_ITEMS, loadAllDevicePages } from '../src/lib/device-catalog.ts'

test('app device selectors load pages beyond the former 1000-row request', async () => {
  const calls: number[] = []
  const result = await loadAllDevicePages(async (pageNo, pageSize) => {
    calls.push(pageNo)
    const total = 1_201
    const start = (pageNo - 1) * pageSize
    return { total, list: Array.from({ length: Math.min(pageSize, Math.max(0, total - start)) }, (_, index) => start + index) }
  })

  assert.deepEqual(calls, [1, 2, 3])
  assert.equal(result.list.length, 1_201)
  assert.equal(result.truncated, false)
})

test('app device selectors expose a safety cap instead of silently claiming completeness', async () => {
  const result = await loadAllDevicePages(async (_pageNo, pageSize) => ({
    total: DEVICE_CATALOG_MAX_ITEMS + 1,
    list: Array.from({ length: pageSize }, (_, index) => index),
  }))

  assert.equal(result.list.length, DEVICE_CATALOG_MAX_ITEMS)
  assert.equal(result.truncated, true)
})

test('app device pagination keeps bounded concurrency and preserves the exact cap boundary', async () => {
  let active = 0
  let peak = 0
  const result = await loadAllDevicePages(async (pageNo, pageSize) => {
    active += 1
    peak = Math.max(peak, active)
    await new Promise((resolve) => setTimeout(resolve, 2))
    active -= 1
    const total = 8
    const start = (pageNo - 1) * pageSize
    return { total, list: Array.from({ length: Math.min(pageSize, Math.max(0, total - start)) }, (_, index) => start + index) }
  }, { pageSize: 2, maxItems: 8 })

  assert.equal(peak, 3)
  assert.equal(result.total, 8)
  assert.equal(result.list.length, 8)
  assert.equal(result.truncated, false)
})

test('app device pagination rejects a failed page instead of returning a partial catalog', async () => {
  await assert.rejects(
    loadAllDevicePages(async (pageNo, pageSize) => {
      if (pageNo === 2) throw new Error('page failed')
      return { total: 3, list: Array.from({ length: Math.min(pageSize, 3 - (pageNo - 1) * pageSize) }, (_, index) => index) }
    }, { pageSize: 1 }),
    /page failed/
  )
})

test('app device pagination normalizes a zero server total when the first page contains rows', async () => {
  const result = await loadAllDevicePages(async () => ({ total: 0, list: ['device'] }), { pageSize: 10 })
  assert.equal(result.total, 1)
  assert.deepEqual(result.list, ['device'])
  assert.equal(result.truncated, false)
})
