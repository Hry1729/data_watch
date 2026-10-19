import assert from 'node:assert/strict'
import test from 'node:test'
import { parseImportRows } from '../src/utils/import-workbook'
import { MAX_IMPORT_FILE_BYTES } from '../src/utils/import-limits'

test('web parser maps a real XLSX workbook and ignores unknown columns', async () => {
  const XLSX = await import('xlsx')
  const workbook = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([['说明']]), '说明')
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([
    ['设备编号', '设备名称', '设备类型', '不导入'],
    ['QC-001', '岸桥一号', 'PLC', 'secret-not-a-field']
  ]), '设备数据')
  const bytes = XLSX.write(workbook, { bookType: 'xlsx', type: 'array' })
  const rows = await parseImportRows(new File([bytes], 'devices.xlsx'), {
    设备编号: 'deviceCode',
    设备名称: 'deviceName',
    设备类型: 'deviceType'
  })

  assert.deepEqual(rows, [{ deviceCode: 'QC-001', deviceName: '岸桥一号', deviceType: 'PLC' }])
})

test('web parser rejects an oversized file before reading it', async () => {
  let readCalled = false
  const file = {
    size: MAX_IMPORT_FILE_BYTES + 1,
    arrayBuffer: async () => {
      readCalled = true
      return new ArrayBuffer(0)
    }
  } as unknown as File

  await assert.rejects(parseImportRows(file, { 设备编号: 'deviceCode' }), /不能超过/)
  assert.equal(readCalled, false)
})

// H4：npm 上的 xlsx@0.18.5 有 CVE-2023-30533（原型污染，0.19.3 修复）与 CVE-2024-22363（ReDoS，0.20.2 修复）。
// 必须使用 SheetJS 官方 CDN 发布的 0.20.3 及以上版本。
test('web bundles a SheetJS release with CVE-2023-30533 / CVE-2024-22363 fixed', async () => {
  const XLSX = await import('xlsx')
  const [major, minor, patch] = String(XLSX.version).split('.').map(Number)
  assert.ok(major > 0 || minor > 20 || (minor === 20 && patch >= 3), `xlsx ${XLSX.version} < 0.20.3`)
  const { readFile } = await import('node:fs/promises')
  const pkg = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'))
  assert.match(pkg.dependencies.xlsx, /^https:\/\/cdn\.sheetjs\.com\/xlsx-0\.20\.\d+\/xlsx-0\.20\.\d+\.tgz$/)
})
