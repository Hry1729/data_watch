export interface RealtimeSample {
  v: unknown
  t: number
  q?: number
}

// 时间戳更新代表有新采样；值未变化时复用已知值，不凭本地时钟补造数据。
export function expandRealtimeSamples(
  values: Record<string, RealtimeSample>,
  freshness: Record<string, unknown>,
  previous: (pointId: string) => Omit<RealtimeSample, 't'> | undefined
): Record<string, RealtimeSample> {
  const samples = { ...values }
  for (const [pointId, timestamp] of Object.entries(freshness)) {
    if (samples[pointId] || typeof timestamp !== 'number' || !Number.isFinite(timestamp)) continue
    const cached = previous(pointId)
    if (cached) samples[pointId] = { ...cached, t: timestamp }
  }
  return samples
}
