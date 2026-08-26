import assert from 'node:assert/strict'
import test from 'node:test'
import {
  MAX_IMPORT_FILE_BYTES,
  MAX_IMPORT_REQUEST_BYTES,
  MAX_IMPORT_SHEET_CELLS,
  MAX_IMPORT_SHEET_ROWS,
  parseImportWorkbook,
  assertImportRequestSize,
  validateImportFileSize,
  validateImportSheetRange,
} from '../src/lib/import-workbook.ts'

test('app import rejects oversized files and worksheet ranges before row mapping', () => {
  assert.throws(() => validateImportFileSize({ size: MAX_IMPORT_FILE_BYTES + 1 }), /不能超过/)
  assert.throws(() => validateImportSheetRange(`A1:A${MAX_IMPORT_SHEET_ROWS + 1}`), /行数超过/)
  assert.throws(() => validateImportSheetRange(`A1:ZZ${Math.ceil(MAX_IMPORT_SHEET_CELLS / 702) + 1}`), /单元格超过/)
})

test('app parser maps a real XLSX workbook and ignores non-data sheets', async () => {
  const XLSX = await import('xlsx')
  const workbook = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([['说明']]), '说明')
  XLSX.utils.book_append_sheet(workbook, XLSX.utils.aoa_to_sheet([
    ['设备编号', '设备名称', '设备类型'],
    ['QC-001', '岸桥一号', 'PLC'],
  ]), '设备数据')
  const bytes = XLSX.write(workbook, { bookType: 'xlsx', type: 'array' })
  const rows = await parseImportWorkbook(new File([bytes], 'devices.xlsx'), 'device')

  assert.deepEqual(rows, [{ deviceCode: 'QC-001', deviceName: '岸桥一号', deviceType: 'PLC' }])
})

test('app parser fails before reading an oversized file', async () => {
  let readCalled = false
  const file = {
    size: MAX_IMPORT_FILE_BYTES + 1,
    arrayBuffer: async () => {
      readCalled = true
      return new ArrayBuffer(0)
    },
  } as unknown as File

  await assert.rejects(parseImportWorkbook(file, 'device'), /不能超过/)
  assert.equal(readCalled, false)
})

test('app import request limit accepts the exact byte boundary and rejects the next byte', () => {
  const emptyPayloadBytes = new TextEncoder().encode(JSON.stringify({ rows: [''] })).byteLength
  const exactPayload = { rows: ['x'.repeat(MAX_IMPORT_REQUEST_BYTES - emptyPayloadBytes)] }
  assert.equal(new TextEncoder().encode(JSON.stringify(exactPayload)).byteLength, MAX_IMPORT_REQUEST_BYTES)
  assert.doesNotThrow(() => assertImportRequestSize(exactPayload))
  assert.throws(() => assertImportRequestSize({ rows: ['x'.repeat(MAX_IMPORT_REQUEST_BYTES - emptyPayloadBytes + 1)] }), /请求不能超过/)
})
