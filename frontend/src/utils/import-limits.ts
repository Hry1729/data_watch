export const MAX_IMPORT_FILE_BYTES = 10 * 1024 * 1024
export const MAX_IMPORT_SHEET_ROWS = 10_000
export const MAX_IMPORT_SHEET_CELLS = 100_000
// Keep below the backend's 4 MiB body limit for JSON/request framing.
export const MAX_IMPORT_REQUEST_BYTES = 3.5 * 1024 * 1024

export function validateImportFileSize(file: Pick<File, 'size'>) {
  if (file.size > MAX_IMPORT_FILE_BYTES) {
    throw new Error(`导入文件不能超过 ${MAX_IMPORT_FILE_BYTES / 1024 / 1024} MiB`)
  }
}

export function validateImportSheetRange(rangeRef: string, sheetName = '数据表') {
  const match = rangeRef.match(/^([A-Z]+)(\d+)(?::([A-Z]+)(\d+))?$/i)
  if (!match) throw new Error(`${sheetName} 的工作表范围无效`)
  const columnNumber = (value: string) => [...value.toUpperCase()].reduce((sum, char) => sum * 26 + char.charCodeAt(0) - 64, 0)
  const rows = Number(match[4] ?? match[2]) - Number(match[2]) + 1
  const columns = columnNumber(match[3] ?? match[1]) - columnNumber(match[1]) + 1
  if (rows > MAX_IMPORT_SHEET_ROWS) throw new Error(`${sheetName} 行数超过 ${MAX_IMPORT_SHEET_ROWS} 行限制`)
  if (rows * columns > MAX_IMPORT_SHEET_CELLS) throw new Error(`${sheetName} 单元格超过 ${MAX_IMPORT_SHEET_CELLS} 个限制`)
}

export function assertImportRequestSize(payload: unknown) {
  const bytes = new TextEncoder().encode(JSON.stringify(payload)).byteLength
  if (bytes > MAX_IMPORT_REQUEST_BYTES) throw new Error(`导入请求不能超过 ${MAX_IMPORT_REQUEST_BYTES / 1024 / 1024} MiB`)
}
