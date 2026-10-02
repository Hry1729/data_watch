import type { EChartsOption } from 'echarts'

// 保留窗口外相邻采样以绘制跨边界线段，由坐标系裁剪可见范围。
export function createHistoryDataZoom(): EChartsOption['dataZoom'] {
  return [
    { id: 'history-x-slider', type: 'slider', show: true, xAxisIndex: [0], filterMode: 'none', start: 0, end: 100, bottom: 30 },
    { id: 'history-x-inside', type: 'inside', xAxisIndex: [0], filterMode: 'none', start: 0, end: 100 },
    { id: 'history-y-slider', type: 'slider', orient: 'vertical', show: true,
      yAxisIndex: [0], filterMode: 'none', start: 0, end: 100, right: 8, top: 55, bottom: 85, width: 22 }
  ]
}

// 将Vue响应式数组转换为独立、明确的时间/数值对，避免图表推断维度。
export function historySampleData(data: Array<[string | number, number | null]>): Array<[number, number | null]> {
  return data.map(([time, value]) => [typeof time === 'number' ? time : new Date(time).getTime(), value])
}
