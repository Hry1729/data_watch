import assert from 'node:assert/strict'
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import { pathToFileURL } from 'node:url'
import ts from 'typescript'

/**
 * 加载真实的 MenuProcessor，而不是删掉 import 后在 vm 里执行源码。
 * import.meta.glob 是 Vite 构建期 API，Node 里不存在，所以只替换这一处。
 * 其余值导入改指向显式 mock；新增依赖时模块加载会直接失败，而不是静默丢掉导入。
 */
const source = readFileSync(new URL('../src/router/core/MenuProcessor.ts', import.meta.url), 'utf8')

const mockSource = [
  'export function useAppMode() {',
  '  return { isFrontendMode: { value: false } }',
  '}',
  'export async function fetchGetMenuList() {',
  "  return [{ name: 'Devices', path: '/devices', component: '/devices/index' }]",
  '}',
  'export function buildProjectDocsMenu() {',
  "  return { name: 'Docs', path: '/docs', component: '/docs/index' }",
  '}',
  "export const RoutesAlias = { Layout: '/index/index' }",
  'export function useUserStore() {',
  '  return { info: { roles: [] } }',
  '}',
  'export const asyncRoutes = []',
  'export function formatMenuTitle(title) {',
  '  return title',
  '}',
  ''
].join('\n')

const workdir = mkdtempSync(join(tmpdir(), 'menu-processor-'))
writeFileSync(join(workdir, 'menu-processor-mocks.mjs'), mockSource, 'utf8')
process.on('exit', () => {
  rmSync(workdir, { recursive: true, force: true })
})

const transpiled = ts.transpileModule(source, {
  compilerOptions: {
    target: ts.ScriptTarget.ES2022,
    module: ts.ModuleKind.ESNext
  },
  fileName: 'MenuProcessor.ts'
}).outputText

function renderModule(fixtureKey: string): string {
  let code = transpiled.replaceAll('import.meta.glob', '__testLocalGlob')
  code = code.replace(
    /(^|\n)(import[\s\S]*?\sfrom\s*)(['"])[^'"]+\3/g,
    "$1$2'./menu-processor-mocks.mjs'"
  )
  if (code.includes('import.meta.glob') || /from\s+['"](?!\.\/menu-processor-mocks\.mjs)[^'"]+['"]/.test(code)) {
    throw new Error('MenuProcessor 的 import 未能全部重定向到测试 mock')
  }
  const helper = [
    `const __fixtures = globalThis[${JSON.stringify(fixtureKey)}]`,
    'function __testLocalGlob(pattern) {',
    "  if (String(pattern).includes('index.vue')) return __fixtures.pages || {}",
    '  return __fixtures.menus || {}',
    '}'
  ].join('\n')
  const lines = code.split('\n')
  let lastImport = -1
  for (let index = 0; index < lines.length; index += 1) {
    if (lines[index].startsWith('import ')) lastImport = index
  }
  lines.splice(lastImport + 1, 0, helper)
  return lines.join('\n')
}

let caseId = 0

async function menus(local: { pages?: Record<string, object>; menus?: Record<string, object> }) {
  const fixtureKey = `__menuProcessorFixtures_${process.pid}_${(caseId += 1)}`
  const globals = globalThis as Record<string, unknown>
  globals[fixtureKey] = {
    pages: local.pages || {},
    menus: local.menus || {}
  }
  const filePath = join(workdir, `menu-processor-${caseId}.mjs`)
  writeFileSync(filePath, renderModule(fixtureKey), 'utf8')
  try {
    const loaded = await import(pathToFileURL(filePath).href)
    const processor = new loaded.MenuProcessor()
    const result = await processor.getMenuList()
    return Array.from(result, (item: { name: string }) => item.name)
  } finally {
    delete globals[fixtureKey]
  }
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
