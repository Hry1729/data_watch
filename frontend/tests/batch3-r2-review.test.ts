import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'
import {
  MISSING_POINT_VALUE,
  formatExportSampleValue,
  toChartSampleValue,
  toSampleText
} from '../src/utils/realtime-point-value'
import { buildRoleGrantPayload, isClearAllGrant } from '../src/utils/role-grant-payload'

const read = (path: string) => readFile(new URL(`../src/${path}`, import.meta.url), 'utf8')

// ---------------------------------------------------------------- 实时导出保留文本值
test('realtime export keeps STRING / VARCHAR / NCHAR text values', () => {
  for (const raw of ['RUNNING', '急停', 'E-042 门未关', 'ON', 'true', 'false', ' padded ']) {
    const value = toChartSampleValue(raw)
    const text = toSampleText(raw)
    assert.equal(value, null, `chart keeps a gap for ${raw}`) // HURY-30：文本不画成 0
    assert.equal(text, raw)
    assert.equal(formatExportSampleValue(value, 0, text), raw)
    assert.equal(formatExportSampleValue(value, 192, text), raw)
  }
})

test('realtime export: numbers stay numbers, real 0 stays 0, missing stays placeholder', () => {
  assert.equal(toSampleText(0), undefined)
  assert.equal(formatExportSampleValue(toChartSampleValue(0), 0, toSampleText(0)), 0)
  assert.equal(formatExportSampleValue(toChartSampleValue('12.5'), 0, toSampleText('12.5')), 12.5)
  assert.equal(formatExportSampleValue(toChartSampleValue(false), 0, toSampleText(false)), 0)
  assert.equal(formatExportSampleValue(toChartSampleValue(true), 0, toSampleText(true)), 1)
  // 真正缺值（只有 t/q）
  assert.equal(toSampleText(undefined), undefined)
  assert.equal(toSampleText(null), undefined)
  assert.equal(formatExportSampleValue(toChartSampleValue(null), 192, toSampleText(null)), `${MISSING_POINT_VALUE} 质量：异常`)
  // 空字符串是真实值，不是缺值
  assert.equal(formatExportSampleValue(toChartSampleValue(''), 0, toSampleText('')), '')
})

test('realtime page records text and passes it to the export cell and snapshot', async () => {
  const source = await read('views/device_monitor/realtime/index.vue')
  assert.match(source, /text: toSampleText\(value\)/)
  assert.match(source, /formatExportSampleValue\(record\.value, record\.quality, record\.text\)/)
  assert.equal((source.match(/text: r\.text/g) || []).length, 2, 'both snapshot writers persist text')
  assert.match(source, /text: typeof item\.text === 'string' \? item\.text : toSampleText\(item\.value\)/)
  // HURY-30：曲线值仍不能把缺值/文本伪造成 0
  assert.equal(source.includes('Number(value) || 0'), false)
})

// ---------------------------------------------------------------- 角色授权 clearAll
test('role grant payload sends clearAll only when menus and permissions are both empty', () => {
  const cleared = buildRoleGrantPayload({ menuIds: [], permissionIds: [], dataScopes: [] })
  assert.deepEqual(cleared, { menuIds: [], permissionIds: [], dataScopes: [], clearAll: true })

  // 契约与 dataScopes 无关：两个列表都空就必须带 clearAll
  const scopes = [{ scopeType: 'device', scopeValue: '1' }]
  const clearedWithScopes = buildRoleGrantPayload({ menuIds: [], permissionIds: [], dataScopes: scopes })
  assert.equal(clearedWithScopes.clearAll, true)
  assert.deepEqual(clearedWithScopes.dataScopes, scopes)

  for (const [menuIds, permissionIds] of [
    [[1], []],
    [[], [7]],
    [[1, 1, 2], [7, 7]]
  ] as const) {
    const payload = buildRoleGrantPayload({ menuIds: [...menuIds], permissionIds: [...permissionIds], dataScopes: [] })
    assert.equal('clearAll' in payload, false)
    assert.deepEqual(payload.menuIds, [...new Set(menuIds)])
    assert.deepEqual(payload.permissionIds, [...new Set(permissionIds)])
  }
  assert.equal(isClearAllGrant([], []), true)
  assert.equal(isClearAllGrant([1], []), false)
})

test('role permission dialog confirms before clearing and keeps the load guard', async () => {
  const source = await read('views/system/role/modules/role-permission-dialog.vue')
  assert.match(source, /buildRoleGrantPayload\(/)
  const confirmAt = source.indexOf('ElMessageBox.confirm(')
  const sendAt = source.indexOf('await fetchUpdateRoleGrant(roleId, payload)')
  assert.ok(confirmAt > 0 && sendAt > confirmAt, 'confirm dialog runs before the grant request')
  assert.match(source, /if \(payload\.clearAll\) \{/)
  assert.match(source, /if \(!canSave\.value \|\| loadedRoleId\.value === null\)/)
  const types = await read('types/api/api.d.ts')
  assert.match(types, /interface RoleGrantPayload \{[^}]*clearAll\?: boolean/)
})
