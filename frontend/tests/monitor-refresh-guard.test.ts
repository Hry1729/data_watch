import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

test('device monitor rejects stale responses and prevents overlapping refresh rounds', async () => {
  const source = await readFile(
    new URL('../src/views/device_monitor/monitor/index.vue', import.meta.url),
    'utf8'
  )

  assert.match(source, /requestVersion === listRequestVersion/)
  assert.match(source, /requestVersion !== statsRequestVersion/)
  assert.match(source, /if \(refreshInFlight\) return/)
  assert.match(source, /await Promise\.all\(\[loadDeviceList\(silent\), loadStats\(\)\]\)/)
})
