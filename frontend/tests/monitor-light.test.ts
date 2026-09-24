import assert from 'node:assert/strict'
import test from 'node:test'
import { alignLogCounts, edgeState, linkState, percent } from '../src/views/monitor-light/data'
import { buildLightMonitorMenu } from '../src/views/monitor-light/screen.menu'
test('today log buckets align with dashboard HH:mm labels and missing data remains unknown', () => {
  const rows = [{ time: '2026-09-10 08:00:00', info: 20, warn: 2, error: 3, critical: 1, debug: 4 }]
  assert.deepEqual(alignLogCounts(['08:00', '09:00'], rows), [30, null])
  assert.deepEqual(alignLogCounts(['08:00'], undefined), [null])
  assert.deepEqual(
    alignLogCounts(['08:00'], [{ ...rows[0], info: 0, warn: 0, error: 0, critical: 0, debug: 0 }]),
    [0]
  )
})
test('unconfirmed status cannot animate a healthy connection', () => {
  assert.equal(edgeState('running'), 'unknown')
  assert.equal(edgeState(undefined), 'unknown')
  assert.equal(edgeState('warning'), 'warning')
  assert.equal(linkState('healthy', 'unknown'), 'unknown')
  assert.equal(linkState('warning', 'healthy'), 'warning')
  assert.equal(linkState('healthy', 'critical'), 'critical')
})
test('ratios handle empty and over-capacity values', () => {
  assert.equal(percent(0, 0), 0)
  assert.equal(percent(-1, 100), 0)
  assert.equal(percent(120, 100), 100)
})
test('light screen owns a separate full-page route', () => {
  const menu = buildLightMonitorMenu()
  assert.equal(menu.path, '/monitor-light')
  assert.equal(menu.name, 'LightMonitorScreen')
  assert.equal(menu.meta.isFullPage, true)
  assert.notEqual(menu.component, '/monitor-screen/index')
})
