import {
  validateImportFileSize,
  validateImportSheetRange,
  MAX_IMPORT_SHEET_ROWS
} from './import-limits'

const normalizeImportHeader = (value: unknown) =>
  String(value ?? '')
    .trim()
    .replace(/[()（）]/g, '')
    .replace(/[\s_-]+/g, '')
    .toLowerCase()

const normalizeImportCellValue = (value: unknown) => {
  if (typeof value === 'string') return value.trim()
  return value ?? ''
}

export async function parseImportRows(
  file: File,
  fieldMap: Record<string, string>
): Promise<Array<Record<string, any>>> {
  validateImportFileSize(file)
  const buffer = await file.arrayBuffer()
  const XLSX = await import('xlsx')
  const workbook = XLSX.read(buffer, { type: 'array' })
  for (const sheetName of workbook.SheetNames) {
    const sheet = workbook.Sheets[sheetName]
    const rangeRef = sheet?.['!ref']
    if (rangeRef) validateImportSheetRange(rangeRef, sheetName)
  }
  const preferredSheetName = workbook.SheetNames[1] || workbook.SheetNames[0]
  const worksheet = workbook.Sheets[preferredSheetName]
  if (!worksheet) throw new Error('工作簿中没有可导入的数据表')
  const rangeRef = worksheet['!ref']
  if (!rangeRef) throw new Error('数据表没有有效的单元格范围')

  const aliasMap: Record<string, string> = {}
  Object.entries(fieldMap).forEach(([header, field]) => {
    aliasMap[normalizeImportHeader(header)] = field
    aliasMap[normalizeImportHeader(field)] = field
  })
  const rawRows = XLSX.utils.sheet_to_json<Record<string, any>>(worksheet, {
    defval: ''
  })

  return rawRows
    .map((row) => {
      const mappedRow: Record<string, any> = {}
      Object.entries(row).forEach(([key, value]) => {
        const field = aliasMap[normalizeImportHeader(key)]
        if (field) mappedRow[field] = normalizeImportCellValue(value)
      })
      return mappedRow
    })
    .filter((row) => Object.values(row).some((value) => String(value ?? '').trim() !== ''))
}
