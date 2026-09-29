import assert from 'node:assert/strict'
import test from 'node:test'
import { settleDashboardModules } from '../src/utils/dashboard-loads'

test('a failed overview does not block other dashboard modules', async () => {
  const started: string[] = []
  const results = await settleDashboardModules([
    async () => {
      started.push('overview')
      throw new Error('overview failed')
    },
    async () => {
      started.push('trend')
      return 'trend data'
    },
    async () => {
      started.push('logs')
      return 'log data'
    }
  ])

  assert.deepEqual(started, ['overview', 'trend', 'logs'])
  assert.equal(results[0].status, 'rejected')
  assert.deepEqual(results.slice(1), [
    { status: 'fulfilled', value: 'trend data' },
    { status: 'fulfilled', value: 'log data' }
  ])
})
