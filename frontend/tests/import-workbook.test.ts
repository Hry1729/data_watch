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
