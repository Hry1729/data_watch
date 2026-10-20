import { qualityStatusLabel } from './realtime-quality'

/** 没有 v 时的占位符。false / 0 / '' 是真实值，不能用这个占位。 */
export const MISSING_POINT_VALUE = '—'

const BOOL_TRUE = ['1', 'true', 'yes', 'on']
const NUMERIC_TYPES = ['INT', 'DINT', 'WORD', 'DWORD', 'REAL', 'FLOAT']

export function isRealtimeValueAbsent(value: unknown): boolean {
  return value === undefined || value === null
}

export function normalizeWsPointValue(
  dataType: string | undefined,
  value: unknown
): string | number | boolean | undefined {
  if (isRealtimeValueAbsent(value)) return undefined
  const type = (dataType || '').toUpperCase()
  if (type === 'BOOL') {
    if (typeof value === 'boolean') return value
    return BOOL_TRUE.includes(String(value).trim().toLowerCase())
  }
  if (NUMERIC_TYPES.includes(type)) {
    const num = Number(value)
    return Number.isFinite(num) ? num : String(value)
  }
  return value as string | number | boolean
}

export function formatStoredPointValue(value: unknown, unit = ''): string {
  const suffix = unit ? ` ${unit}` : ''
  if (typeof value === 'number') return `${value.toFixed(2)}${suffix}`
  return `${value}${suffix}`
}

/** 无 v：灰色占位，后面跟页面既有质量文案，不用 BOOL 的 true/false。 */
export function formatMissingValueWithQuality(quality: number | null | undefined): string {
  const label = qualityStatusLabel(quality)
  if (!label) return MISSING_POINT_VALUE
  return `${MISSING_POINT_VALUE} 质量：${label}`
}

/**
 * 实时曲线/导出的样本值：缺值（只有 t/q 的质量异常推送）和非数值字符串保持 null（曲线断点），
 * 绝不伪造成 0；真实的 0 / false 仍按数值 0 记录，true 记 1。
 */
export function toChartSampleValue(value: unknown): number | null {
  if (isRealtimeValueAbsent(value)) return null
  if (typeof value === 'boolean') return value ? 1 : 0
  if (typeof value === 'number') return Number.isFinite(value) ? value : null
  if (typeof value === 'string') {
    const text = value.trim()
    if (!text) return null
    const num = Number(text)
    return Number.isFinite(num) ? num : null
  }
  return null
}

/** 实时导出单元格：有值导出数值；缺值导出占位符并带质量文案，与真实 0 可区分。 */
export function formatExportSampleValue(
  value: number | null | undefined,
  quality: number | null | undefined
): number | string {
  if (typeof value === 'number') return value
  return formatMissingValueWithQuality(quality)
}
