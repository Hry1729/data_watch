<!-- 浅色大屏统计图：占满格子并画出 canvas，供页面复用 -->
<template>
  <div ref="host" class="stat-chart" />
</template>

<script setup lang="ts">
  import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
  import { echarts, type EChartsOption } from '@/plugins/echarts'

  const props = defineProps<{
    option: EChartsOption
  }>()

  const host = ref<HTMLDivElement>()
  let chart: echarts.ECharts | null = null
  let observer: ResizeObserver | null = null

  const render = () => {
    chart?.setOption(props.option, true)
  }

  onMounted(async () => {
    await nextTick()
    if (!host.value) return
    chart = echarts.init(host.value)
    render()
    observer = new ResizeObserver(() => chart?.resize())
    observer.observe(host.value)
  })

  watch(() => props.option, render, { deep: true })

  onUnmounted(() => {
    observer?.disconnect()
    chart?.dispose()
    chart = null
  })
</script>

<style scoped lang="scss">
  .stat-chart {
    width: 100%;
    height: 100%;
    min-height: 0;
  }
</style>
