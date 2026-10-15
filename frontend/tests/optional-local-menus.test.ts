import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import vm from 'node:vm'
import ts from 'typescript'

const source = readFileSync(new URL('../src/router/core/MenuProcessor.ts', import.meta.url), 'utf8')

async function menus(local: { pages?: Record<string, object>; menus?: Record<string, object> }) {
  const executable = ts
    .transpileModule(source.replace(/^import .*$/gm, ''), {
      compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext }
    })
    .outputText.replaceAll('import.meta.glob', '__glob')
    .replace('export class MenuProcessor', 'class MenuProcessor')
  const context = {
    __glob: (pattern: string) =>
      pattern.includes('index.vue') ? local.pages || {} : local.menus || {},
    useAppMode: () => ({ isFrontendMode: { value: false } }),
    fetchGetMenuList: async () => [
      { name: 'Devices', path: '/devices', component: '/devices/index' }
    ],
    buildProjectDocsMenu: () => ({ name: 'Docs', path: '/docs', component: '/docs/index' }),
    RoutesAlias: { Layout: '/index/index' },
    console
  }
  const processor = vm.runInNewContext(executable + '\nnew MenuProcessor()', context)
  const result = await processor.getMenuList()
  return Array.from(result, (item: { name: string }) => item.name)
}

const route = (name: string) => ({ name, path: '/' + name, component: '/' + name + '/index' })

test('clean checkouts keep business and docs menus when local pages are absent', async () => {
  assert.deepEqual(await menus({}), ['Devices', 'Docs'])
})

test('local pages keep their menu order only when both the page and menu module exist', async () => {
  assert.deepEqual(
    await menus({
      pages: {
        '../../views/monitor-screen/index.vue': {},
        '../../views/monitor-light/index.vue': {},
        '../../views/landing/index.vue': {}
      },
      menus: {
        '../../views/monitor-screen/screen.menu.ts': {
          buildMonitorScreenMenu: () => route('Monitor')
        },
        '../../views/monitor-light/screen.menu.ts': { buildLightMonitorMenu: () => route('Light') },
        '../../views/landing/screen.menu.ts': { buildLandingMenu: () => route('Landing') }
      }
    }),
    ['Monitor', 'Light', 'Landing', 'Devices', 'Docs']
  )
})

test('a menu module without its page, or a page without its menu module, is skipped', async () => {
  assert.deepEqual(
    await menus({
      pages: { '../../views/landing/index.vue': {} },
      menus: {
        '../../views/monitor-screen/screen.menu.ts': {
          buildMonitorScreenMenu: () => route('Monitor')
        }
      }
    }),
    ['Devices', 'Docs']
  )
})
