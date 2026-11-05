<template>
  <div class="bool-lanes">
    <div class="bool-lanes-title">Bool 型点位<span class="bool-lanes-rule" /></div>
    <div class="bool-lanes-grid">
      <template v-for="lane in lanes" :key="lane.varSeq">
        <span class="bool-lane-name" :title="lane.pointName">{{ lane.pointName }}</span>
        <div class="bool-lane-track">
          <span
            v-for="[from, to] in lane.runs"
            :key="from"
            class="bool-lane-run"
            :style="{
              left: `${lanePercent(from, range[0], range[1])}%`,
              width: `${Math.max(0.6, lanePercent(to, range[0], range[1]) - lanePercent(from, range[0], range[1]))}%`,
              background: lane.color
            }"
          />
          <span
            v-if="lane.current != null"
            class="bool-lane-dot"
            :style="{ left: `${lanePercent(range[1], range[0], range[1])}%`, background: lane.current ? lane.color : 'var(--art-gray-500)' }"
          />
        </div>
        <span class="bool-lane-state" :style="{ color: lane.current ? lane.color : undefined }">
          {{ lane.current == null ? '--' : lane.current ? 'ON' : 'OFF' }}
        </span>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { computed } from 'vue'
  import { buildBoolLane, lanePercent, type BoolSample } from '@/utils/bool-lanes'

  interface LanePoint {
    varSeq: number
    pointName: string
    color: string
  }

  const props = defineProps<{
    points: LanePoint[]
    records: { timestamp: Date; varSeq: number; value: number | null }[]
  }>()

  const range = computed<[number, number]>(() => {
    let start = Infinity
    let end = -Infinity
    for (const r of props.records) {
      const t = r.timestamp.getTime()
      if (t < start) start = t
      if (t > end) end = t
    }
    return Number.isFinite(start) ? [start, end] : [0, 1]
  })

  const lanes = computed(() => {
    const bySeq = new Map<number, BoolSample[]>()
    for (const r of props.records) {
      const list = bySeq.get(r.varSeq)
      const sample = { time: r.timestamp.getTime(), value: r.value }
      if (list) list.push(sample)
      else bySeq.set(r.varSeq, [sample])
    }
    return props.points.map((p) => {
      const samples = (bySeq.get(p.varSeq) ?? []).sort((a, b) => a.time - b.time)
      return { ...p, ...buildBoolLane(samples) }
    })
  })
</script>

<style scoped lang="scss">
  .bool-lanes {
    flex-shrink: 0;
    margin-top: 14px;
  }

  .bool-lanes-title {
    display: flex;
    gap: 8px;
    align-items: center;
    margin-bottom: 8px;
    font-size: 12px;
    font-weight: 500;
    color: var(--art-gray-600);
  }

  .bool-lanes-rule {
    flex: 1;
    height: 1px;
    background: var(--art-gray-200);
  }

  .bool-lanes-grid {
    display: grid;
    grid-template-columns: 96px minmax(0, 1fr) 36px;
    row-gap: 8px;
    column-gap: 10px;
    align-items: center;
    max-height: 240px;
    padding-right: 4px;
    overflow-y: auto;
  }

  .bool-lane-name {
    overflow: hidden;
    font-size: 12px;
    color: var(--art-gray-800);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  /* OFF：斜纹底；ON：实色胶囊 */
  .bool-lane-track {
    position: relative;
    height: 18px;
    overflow: hidden;
    background: repeating-linear-gradient(
      135deg,
      color-mix(in oklab, var(--art-gray-500) 35%, transparent) 0 2px,
      transparent 2px 5px
    );
    border-radius: 999px;
  }

  .bool-lane-run {
    position: absolute;
    top: 0;
    bottom: 0;
    border-radius: 999px;
  }

  .bool-lane-dot {
    position: absolute;
    top: 50%;
    width: 8px;
    height: 8px;
    border-radius: 50%;
    transform: translate(-50%, -50%);
  }

  .bool-lane-state {
    font-size: 12px;
    font-variant-numeric: tabular-nums;
    color: var(--art-gray-500);
    text-align: right;
  }
</style>
