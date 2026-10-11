import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'

const source = readFileSync(new URL('../src/router/core/MenuProcessor.ts', import.meta.url), 'utf8')

async function menus(localModules: Record<string, object>) {
  const executable = ts.transpileModule(source.replace(/^import .*$/gm, ''), {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext }
  }).outputText.replace('import.meta.glob', '__glob').replace('export class MenuProcessor', 'class MenuProcessor')
  const context = {
    __glob: () => localModules,
    useAppMode: () => ({ isFrontendMode: { value: false } }),
    fetchGetMenuList: async () => [{ name: 'Devices', path: '/devices', component: '/devices/index' }],
    buildProjectDocsMenu: () => ({ name: 'Docs', path: '/docs', component: '/docs/index' }),
    RoutesAlias: { Layout: '/index/index' },
    console
  }
  const processor = vm.runInNewContext(executable + '\nnew MenuProcessor()', context)
  const result = await processor.getMenuList()
  return Array.from(result, (item: { name: string }) => item.name)
}

test('clean releases retain business and documentation menus without local-only pages', async () => {
  assert.deepEqual(await menus({}), ['Devices', 'Docs'])
})

test('local pages retain their existing menu order when present', async () => {
  const route = (name: string) => ({ name, path: '/' + name, component: '/' + name + '/index' })
  assert.deepEqual(await menus({
    '../../views/monitor-screen/screen.menu.ts': { buildMonitorScreenMenu: () => route('Monitor') },
    '../../views/monitor-light/screen.menu.ts': { buildLightMonitorMenu: () => route('Light') },
    '../../views/landing/screen.menu.ts': { buildLandingMenu: () => route('Landing') }
  }), ['Monitor', 'Light', 'Landing', 'Devices', 'Docs'])
})
