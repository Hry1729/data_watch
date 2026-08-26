import assert from 'node:assert/strict'
import test from 'node:test'
import {
  assertImportRequestSize,
  MAX_IMPORT_FILE_BYTES,
  MAX_IMPORT_REQUEST_BYTES,
  validateImportFileSize,
  validateImportSheetRange
} from '../src/utils/import-limits'

test('web import rejects oversized files and worksheet ranges before upload', () => {
  assert.throws(() => validateImportFileSize({ size: MAX_IMPORT_FILE_BYTES + 1 }), /不能超过/)
  assert.throws(() => validateImportSheetRange('A1:Z10001'), /行数超过/)
  assert.throws(() => validateImportSheetRange('A1:ZZ143'), /单元格超过/)
})

test('web import request limit counts encoded bytes', () => {
  const emptyPayloadBytes = new TextEncoder().encode(JSON.stringify({ rows: [''] })).byteLength
  const exactPayload = { rows: ['x'.repeat(MAX_IMPORT_REQUEST_BYTES - emptyPayloadBytes)] }
  assert.equal(new TextEncoder().encode(JSON.stringify(exactPayload)).byteLength, MAX_IMPORT_REQUEST_BYTES)
  assert.doesNotThrow(() => assertImportRequestSize(exactPayload))
  assert.throws(() => assertImportRequestSize({ rows: ['x'.repeat(MAX_IMPORT_REQUEST_BYTES - emptyPayloadBytes + 1)] }), /请求不能超过/)
})
