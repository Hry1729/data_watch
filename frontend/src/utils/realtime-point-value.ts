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

/**
 * 有值但不是数值的样本（STRING/VARCHAR/NCHAR 文本、"true"/"false" 这类文本布尔）保留原文，
 * 供导出使用；曲线上它们仍是 null 断点。数值、boolean、缺值返回 undefined。
 */
export function toSampleText(value: unknown): string | undefined {
  if (isRealtimeValueAbsent(value)) return undefined
  if (typeof value === 'number' || typeof value === 'boolean') return undefined
  if (typeof value === 'string') return toChartSampleValue(value) === null ? value : undefined
  try {
    return typeof value === 'object' ? JSON.stringify(value) : String(value)
  } catch {
    return String(value)
  }
}

/**
 * 实时导出单元格：有数值导出数值；有文本导出原文；真正缺值才导出占位符并带质量文案，
 * 与真实 0 / 文本都可区分。
 */
export function formatExportSampleValue(
  value: number | null | undefined,
  quality: number | null | undefined,
  text?: string | null
): number | string {
  if (typeof value === 'number') return value
  if (typeof text === 'string') return text
  return formatMissingValueWithQuality(quality)
}
