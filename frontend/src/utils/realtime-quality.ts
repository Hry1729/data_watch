// 平台质量码：0 = GOOD；非 0（UNCERTAIN/BAD/STALE/NOT_CONNECTED/解码错误/空值等）均为异常。
// 未收到质量码时不判为异常，避免把缺失信息显示成坏值。
export function isQualityAbnormal(quality: number | null | undefined): boolean {
  return typeof quality === 'number' && quality !== 0
}

/** 实时页既有文案：q=0 为 GOOD，非 0 为异常。没有质量码时不编造标签。 */
export function qualityStatusLabel(quality: number | null | undefined): string | null {
  if (typeof quality !== 'number' || !Number.isFinite(quality)) return null
  return quality === 0 ? 'GOOD' : '异常'
}
