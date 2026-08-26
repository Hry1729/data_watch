import assert from 'node:assert/strict'
import test from 'node:test'
import { DEVICE_MAX_ITEMS, loadAllDevicePages } from '../src/utils/device-pagination'

test('device pagination loads every page instead of truncating at the first 1000 rows', async () => {
  const calls: number[] = []
  const result = await loadAllDevicePages(async (pageNo, pageSize) => {
    calls.push(pageNo)
    const total = 1_201
    const start = (pageNo - 1) * pageSize
    return {
      total,
      list: Array.from({ length: Math.min(pageSize, Math.max(0, total - start)) }, (_, index) => start + index)
    }
  }, { pageSize: 500 })

  assert.deepEqual(calls, [1, 2, 3])
  assert.equal(result.total, 1_201)
  assert.equal(result.list.length, 1_201)
  assert.equal(result.truncated, false)
})

test('device pagination reports the safety cap rather than silently claiming completeness', async () => {
  const result = await loadAllDevicePages(async (pageNo, pageSize) => ({
    total: DEVICE_MAX_ITEMS + 1,
    list: Array.from({ length: pageNo === 1 ? pageSize : pageSize }, (_, index) => index)
  }), { pageSize: 500 })

  assert.equal(result.list.length, DEVICE_MAX_ITEMS)
  assert.equal(result.total, DEVICE_MAX_ITEMS + 1)
  assert.equal(result.truncated, true)
})

test('device pagination keeps bounded concurrency and treats the exact cap as complete', async () => {
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

test('device pagination rejects a failed page instead of returning a partial catalog', async () => {
  await assert.rejects(
    loadAllDevicePages(async (pageNo, pageSize) => {
      if (pageNo === 2) throw new Error('page failed')
      return { total: 3, list: Array.from({ length: Math.min(pageSize, 3 - (pageNo - 1) * pageSize) }, (_, index) => index) }
    }, { pageSize: 1 }),
    /page failed/
  )
})

test('device pagination normalizes a zero server total when the first page contains rows', async () => {
  const result = await loadAllDevicePages(async () => ({ total: 0, list: ['device'] }), { pageSize: 10 })
  assert.equal(result.total, 1)
  assert.deepEqual(result.list, ['device'])
  assert.equal(result.truncated, false)
})
