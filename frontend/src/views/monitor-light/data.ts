/**
 * 浅色监控大屏的纯计算。
 * 日志缺桶保持未知（null），未确认的边缘状态不能被画成健康连接。
 */

export interface LogCountRow {
  time: string
  info: number
  warn: number
  error: number
  critical: number
  debug: number
}

export type LinkTone = 'healthy' | 'warning' | 'critical' | 'unknown'

const TONE_RANK: Record<LinkTone, number> = {
  healthy: 0,
  unknown: 1,
  warning: 2,
  critical: 3
}

/** 已确认的状态。running 等原始进程态不算健康。 */
const CONFIRMED_EDGE: Record<string, LinkTone> = {
  healthy: 'healthy',
  normal: 'healthy',
  warning: 'warning',
  backlog: 'warning',
  critical: 'critical',
  stopped: 'critical',
  crashed: 'critical',
  overflow: 'critical',
  unknown: 'unknown'
}

/** 从时间文本取出 HH:mm，供仪表盘横轴对齐。 */
export function toHourLabel(time: string): string {
  const matched = String(time ?? '')
    .replace('T', ' ')
    .match(/\d{2}:\d{2}/)
  return matched ? matched[0] : ''
}

/**
 * 把日志级别桶对齐到仪表盘 HH:mm 标签。
 * 没有该时刻的数据时返回 null，全 0 仍返回 0。
 */
export function alignLogCounts(
  labels: string[],
  rows?: LogCountRow[] | null
): Array<number | null> {
  if (!rows) return labels.map(() => null)

  const totals = new Map<string, number>()
  for (const row of rows) {
    const label = toHourLabel(row.time)
    if (!label) continue
    const count = [row.info, row.warn, row.error, row.critical, row.debug].reduce(
      (sum, value) => sum + (Number(value) || 0),
      0
    )
    totals.set(label, (totals.get(label) ?? 0) + count)
  }

  return labels.map((label) => (totals.has(label) ? (totals.get(label) as number) : null))
}

/** 边缘/模块原始状态 → 画面状态。未确认时保持 unknown。 */
export function edgeState(status?: string | null): LinkTone {
  if (!status) return 'unknown'
  return CONFIRMED_EDGE[status.trim().toLowerCase()] ?? 'unknown'
}

/** 链路取更差的一端，避免未知状态被画成健康连接。 */
export function linkState(left: LinkTone, right: LinkTone): LinkTone {
  const leftRank = TONE_RANK[left] ?? TONE_RANK.unknown
  const rightRank = TONE_RANK[right] ?? TONE_RANK.unknown
  return leftRank >= rightRank ? left : right
}

/** 比例钳制到 0–100。空基数和负数都按 0。 */
export function percent(value: number, total: number): number {
  if (!(total > 0) || !(value > 0)) return 0
  return Math.min(100, (value / total) * 100)
}
