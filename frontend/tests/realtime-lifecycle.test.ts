import assert from 'node:assert/strict'
import test from 'node:test'
import { readFileSync } from 'node:fs'
import { runInNewContext } from 'node:vm'
import ts from 'typescript'
import * as vue from 'vue'

const source = readFileSync(new URL('../src/views/device_monitor/realtime/index.vue', import.meta.url), 'utf8')
const script = source.split('<script setup lang="ts">')[1].split('</script>')[0]
const executable = ts.transpileModule(script + '\nglobalThis.scope = { initializePage, activatePage, currentDevice, selectedPoints, isMonitoring, saveChartSnapshot, perspectiveWorker, perspectiveNumericViewer, updatePerspectiveTable };', {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
}).outputText

function harness(options: { deviceId?: number; runtime?: () => Promise<unknown> } = {}) {
  const hooks: Record<string, Function[]> = {}
  const storage = new Map<string, string>()
  const user = vue.reactive({ isLogin: true, info: { userId: 7 } })
  let sockets = 0
  let timers = 0
  const devices = [1, 2].map(id => ({ id, deviceCode: `D${id}`, deviceName: `Device ${id}` }))
  const route = vue.reactive({ query: { deviceId: options.deviceId } })
  const lifecycle = Object.fromEntries(['onMounted', 'onUnmounted', 'onActivated', 'onDeactivated'].map(name => [name, (callback: Function) => (hooks[name] ??= []).push(callback)]))
  const api = {
    fetchDeviceTypes: async () => [], fetchHistoryDevices: async () => devices,
    fetchRealtimeInit: async () => ({ data: [] }), getDevicePointGroups: async () => [],
    getAccessToken: () => 'token', getRealtimeWebSocketUrl: () => 'ws://test'
  }
  const modules: Record<string, unknown> = {
    vue: { ...vue, ...lifecycle }, pinia: { storeToRefs: () => ({ isDark: vue.ref(false) }) },
    'vue-router': { useRoute: () => route }, '@/store/modules/user': { useUserStore: () => user },
    '@/store/modules/setting': { useSettingStore: () => ({}) }, '@/api/device-monitor': api,
    '../history/perspective-runtime': { ensurePerspectiveViewerRuntime: options.runtime ?? (async () => ({ perspective: { worker: async () => ({ terminate() {} }) } })) },
    '@/utils/realtime-samples': { expandRealtimeSamples: () => [] }, '@/utils/realtime-quality': { isQualityAbnormal: () => false },
    'element-plus': { ElMessage: { success() {}, error() {} } }, '@element-plus/icons-vue': {},
    '@/components/core/base/art-svg-icon/index.vue': {}
  }
  const context: any = {
    exports: {}, require: (name: string) => { assert.ok(name in modules, name); return modules[name] },
    defineOptions() {}, console, setInterval: () => ++timers, clearInterval() {}, setTimeout: () => ++timers, clearTimeout() {},
    WebSocket: class { static OPEN = 1; constructor() { ++sockets } close() {} },
    localStorage: { getItem: (key: string) => storage.get(key) ?? null, setItem: (key: string, value: string) => storage.set(key, value), removeItem: (key: string) => storage.delete(key) }
  }
  runInNewContext(executable, context)
  return { scope: context.scope, storage, hooks, user, sockets: () => sockets, timers: () => timers }
}

const savedState = (deviceId: number) => JSON.stringify({ deviceId, deviceCode: `D${deviceId}`, selectedPoints: [{ pointId: 1, varSeq: 1, pointName: 'P1' }], wasMonitoring: true })

test('URL device takes precedence over cached monitoring device', async () => {
  const page = harness({ deviceId: 2 })
  page.storage.set('realtime-monitor-state', savedState(1))
  page.scope.activatePage()
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(page.scope.currentDevice.value.deviceId, 2)
  assert.equal(page.scope.selectedPoints.value.length, 0)
  assert.equal(page.sockets(), 0)
  page.hooks.onUnmounted.forEach(callback => callback())
})

test('late Perspective initialization after unmount cannot start sockets or timers', async () => {
  let resolveRuntime!: (value: unknown) => void
  const runtime = new Promise(resolve => { resolveRuntime = resolve })
  const page = harness({ runtime: () => runtime })
  page.storage.set('realtime-monitor-state', savedState(1))
  page.scope.activatePage()
  await new Promise(resolve => setImmediate(resolve))
  page.hooks.onUnmounted.forEach(callback => callback())
  resolveRuntime({ perspective: { worker: async () => ({ terminate() {} }) } })
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(page.sockets(), 0)
  assert.equal(page.timers(), 0)
})

test('logout followed by unmount cannot resurrect monitoring cache', async () => {
  const page = harness()
  page.storage.set('realtime-monitor-state', savedState(1))
  page.scope.activatePage()
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(page.sockets(), 1)
  page.user.isLogin = false
  page.storage.clear()
  await vue.nextTick()
  page.hooks.onUnmounted.forEach(callback => callback())
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(page.storage.has('realtime-monitor-state'), false)
  assert.equal(page.storage.has('realtime-monitor-chart-state'), false)
})

test('Perspective tables are bounded to the existing realtime record window', async () => {
  const page = harness()
  const options: unknown[] = []
  page.scope.perspectiveWorker.value = { table: async (_data: unknown, configuration: unknown) => {
    options.push(configuration)
    return { update: async () => {} }
  } }
  page.scope.perspectiveNumericViewer.value = { load: async () => {}, restore: async () => {}, flush: async () => {}, delete: async () => {} }
  await page.scope.updatePerspectiveTable([{ timestamp: new Date(), varSeq: 1, pointName: 'P1', value: 1 }])
  assert.equal((options[0] as { limit: number }).limit, 600)
  page.hooks.onUnmounted.forEach(callback => callback())
})
