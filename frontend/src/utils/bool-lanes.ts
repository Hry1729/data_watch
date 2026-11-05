/** Bool 型点位状态泳道：把采样序列切成 ON 段，供实时监控页渲染 */
export interface BoolSample {
  time: number
  value: number | null
}

export interface BoolLane {
  /** ON 段（毫秒时间戳，[开始, 结束]） */
  runs: [number, number][]
  /** 最近一次采样是否为 ON；无采样或缺值为 null */
  current: boolean | null
}

export const isBoolOn = (value: number | null | undefined) => value != null && Number(value) === 1

/**
 * samples 需按时间升序。一段 ON 持续到下一次出现非 ON 的采样（状态切换点），
 * 最后一段持续到最新采样时刻。缺值（null）视为断开，不伪造成 OFF 以外的状态。
 */
export function buildBoolLane(samples: BoolSample[]): BoolLane {
  const runs: [number, number][] = []
  let runStart: number | null = null
  for (const sample of samples) {
    const on = isBoolOn(sample.value)
    if (on && runStart == null) runStart = sample.time
    if (!on && runStart != null) {
      runs.push([runStart, sample.time])
      runStart = null
    }
  }
  const last = samples[samples.length - 1]
  if (runStart != null && last) runs.push([runStart, last.time])
  return { runs, current: last ? (last.value == null ? null : isBoolOn(last.value)) : null }
}

/** 时间 → 泳道内百分比位置（0–100） */
export const lanePercent = (time: number, start: number, end: number) =>
  Math.min(100, Math.max(0, ((time - start) / Math.max(end - start, 1)) * 100))
