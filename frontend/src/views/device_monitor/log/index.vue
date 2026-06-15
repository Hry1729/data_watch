<!-- 系统日志页 -->
<template>
  <div class="log-page">
    <!-- 统计卡片区 -->
    <section class="stats-grid">
      <div class="art-card relative flex flex-col justify-center h-35 px-5 bg-[var(--default-box-color)]">
        <span class="text-g-700 text-sm">今日日志</span>
        <div class="flex items-baseline mt-2">
          <ArtCountTo class="text-[26px] font-medium" :target="stats.todayTotal" :duration="1300" />
        </div>
        <div class="flex-c mt-1">
          <span class="text-xs text-g-600">较昨日</span>
          <span class="ml-1 text-xs font-semibold" :class="stats.todayTotal >= stats.yesterdayTotal ? 'text-success' : 'text-danger'">
            {{ stats.todayTotal >= stats.yesterdayTotal ? '+' : '' }}{{ stats.todayTotal - stats.yesterdayTotal }} 条
          </span>
        </div>
        <div class="absolute top-0 bottom-0 right-5 m-auto w-12 h-12 rounded-xl flex-cc bg-blue-500/10 text-blue-500">
          <ArtSvgIcon icon="ri:file-list-3-line" class="text-xl" />
        </div>
      </div>
      <div class="art-card relative flex flex-col justify-center h-35 px-5 bg-[var(--default-box-color)]">
        <span class="text-g-700 text-sm">今日异常</span>
        <div class="flex items-baseline mt-2">
          <ArtCountTo class="text-[26px] font-medium" :target="stats.todayWarnTotal + stats.todayErrorTotal + stats.todayCriticalTotal" :duration="1300" />
        </div>
        <div class="flex-c mt-1">
          <span class="text-xs text-g-600">WARN {{ stats.todayWarnTotal }} / ERROR {{ stats.todayErrorTotal }}</span>
        </div>
        <div class="absolute top-0 bottom-0 right-5 m-auto w-12 h-12 rounded-xl flex-cc bg-red-500/10 text-red-500">
          <ArtSvgIcon icon="ri:alert-line" class="text-xl" />
        </div>
      </div>
      <div class="art-card relative flex flex-col justify-center h-35 px-5 bg-[var(--default-box-color)] cursor-pointer" @click="showDebugDialog = true">
        <span class="text-g-700 text-sm">{{ stats.debugEnabled ? 'DEBUG 记录中' : 'DEBUG 已关闭' }}</span>
        <div class="flex items-baseline mt-2">
          <ArtCountTo v-if="stats.debugEnabled" class="text-[26px] font-medium" :target="stats.debugRemainingSeconds" :duration="1300" />
          <span v-else class="text-[26px] font-medium">-</span>
        </div>
        <div class="flex-c mt-1">
          <span class="text-xs text-g-600">{{ stats.debugEnabled ? '剩余秒数' : '点击开启' }}</span>
        </div>
        <div class="absolute top-0 bottom-0 right-5 m-auto w-12 h-12 rounded-xl flex-cc" :class="stats.debugEnabled ? 'bg-yellow-500/10 text-yellow-500' : 'bg-gray-400/10 text-gray-400'">
          <ArtSvgIcon icon="ri:bug-line" class="text-xl" />
        </div>
      </div>
      <div class="art-card relative flex flex-col justify-center h-35 px-5 bg-[var(--default-box-color)]">
        <span class="text-g-700 text-sm">日志容量</span>
        <div class="flex items-baseline mt-2">
          <ArtCountTo class="text-[26px] font-medium" :target="Math.round(stats.totalSizeMb)" :duration="1300" />
        </div>
        <div class="flex-c mt-1">
          <span class="text-xs text-g-600">上限 {{ stats.maxTotalMb }} MB</span>
        </div>
        <div class="absolute top-0 bottom-0 right-5 m-auto w-12 h-12 rounded-xl flex-cc bg-green-500/10 text-green-500">
          <ArtSvgIcon icon="ri:database-2-line" class="text-xl" />
        </div>
      </div>
      <div class="art-card relative flex flex-col justify-center h-35 px-5 bg-[var(--default-box-color)]">
        <span class="text-g-700 text-sm">最近清理</span>
        <div class="flex items-baseline mt-2">
          <ArtCountTo class="text-[26px] font-medium" :target="stats.lastCleanupDeletedTotal" :duration="1300" />
        </div>
        <div class="flex-c mt-1">
          <span class="text-xs text-g-600">{{ stats.lastCleanupTime || '暂无记录' }}</span>
        </div>
        <div class="absolute top-0 bottom-0 right-5 m-auto w-12 h-12 rounded-xl flex-cc bg-purple-500/10 text-purple-500">
          <ArtSvgIcon icon="ri:delete-bin-line" class="text-xl" />
        </div>
      </div>
    </section>

    <!-- 图表区 -->
    <section class="charts-grid">
      <div class="chart-card art-card flex flex-col h-[360px] p-5 bg-[var(--default-box-color)]">
        <div class="art-card-header flex justify-between items-center pr-0">
          <div class="title">
            <h4 class="text-lg font-medium text-g-900">日志级别趋势（最近24小时）</h4>
          </div>
          <ElButton text type="primary" @click="loadStats">刷新</ElButton>
        </div>
        <div class="chart-frame flex-1 min-h-0">
          <div ref="levelTrendChartRef" class="chart-surface h-full" />
        </div>
      </div>
      <div class="chart-card art-card flex flex-col h-[360px] p-5 bg-[var(--default-box-color)]">
        <div class="art-card-header pr-0">
          <div class="title">
            <h4 class="text-lg font-medium text-g-900">日志类型占比</h4>
          </div>
        </div>
        <div class="chart-frame flex-1 min-h-0 pt-4">
          <ArtRingChart
            :data="typeRatioChartData"
            :radius="['54%', '70%']"
            :colors="['#4A6CF7', '#3DBB9A', '#F4A542', '#D66B88', '#6E7FA6', '#95A3BF', '#8B5CF6', '#EC4899']"
            :showLegend="true"
            legendPosition="bottom"
          />
        </div>
      </div>
    </section>

    <!-- 日志列表区 -->
    <div class="log-list-card art-card flex flex-col p-5 bg-[var(--default-box-color)]">
      <div class="art-card-header flex justify-between items-center pr-0 gap-2 flex-wrap">
        <div class="title flex items-center gap-2 shrink-0">
          <h4 class="text-lg font-medium text-g-900">{{ logMode === 'system' ? '历史日志查询' : '数据库日志（TDengine 服务日志）' }}</h4>
          <ElButton plain @click="logMode = logMode === 'system' ? 'db' : 'system'">
            {{ logMode === 'system' ? '切换到数据库日志' : '切换到系统日志' }}
          </ElButton>
        </div>
        <div
          v-if="logMode === 'system'"
          class="log-actions flex-1 min-w-0 flex items-center justify-end gap-2 flex-wrap"
        >
          <ElDatePicker
            v-model="dateRange"
            type="datetimerange"
            range-separator="至"
            start-placeholder="开始时间"
            end-placeholder="结束时间"
            value-format="YYYY-MM-DD HH:mm:ss"
          />
          <ElSelect v-model="searchForm.type" placeholder="类型" clearable style="width: 110px">
            <ElOption v-for="opt in TYPE_OPTIONS" :key="opt.value" :label="opt.label" :value="opt.value" />
          </ElSelect>
          <ElInput
            v-model="searchForm.keyword"
            placeholder="关键字"
            clearable
            style="width: 150px"
            @keyup.enter="applySearch"
          />
          <ElButton type="primary" @click="applySearch">查询</ElButton>
          <ElButton @click="handleReset">重置</ElButton>
          <ElBadge :value="advancedCount" :hidden="advancedCount === 0" type="primary">
            <ElButton @click="showAdvancedDialog = true">高级查询</ElButton>
          </ElBadge>
          <ElButton type="primary" plain @click="handleExport">导出日志</ElButton>
          <ElButton v-roles="'R_SUPER'" type="danger" plain @click="showCleanupDialog = true">清理日志</ElButton>
          <ElButton
            v-roles="'R_SUPER'"
            :type="stats.debugEnabled ? 'warning' : 'default'"
            plain
            @click="showDebugDialog = true"
          >
            {{ stats.debugEnabled ? 'DEBUG 开启中' : 'DEBUG 开关' }}
          </ElButton>
        </div>
      </div>

      <template v-if="logMode === 'system'">
      <ArtTable
        class="mt-4"
        :loading="loading"
        :data="data"
        :columns="columns"
        :pagination="pagination"
        :row-key="(row: any) => `${row.kind || 'unknown'}:${row.id}`"
        :row-class-name="getRowClassName"
        :scroll-page-on-pagination="false"
        @pagination:size-change="handleSizeChange"
        @pagination:current-change="handleCurrentChange"
        @row-click="handleRowClick"
      >
        <template #level="{ row }">
          <ElTag :type="getLevelType(row.level)" effect="light" round size="small">
            {{ row.level }}
          </ElTag>
        </template>
        <template #type="{ row }">
          <ElTag :type="getTypeType(row.type)" effect="light" round size="small">
            {{ getTypeText(row.type) }}
          </ElTag>
        </template>
        <template #kind="{ row }">
          <ElTag effect="plain" round size="small">
            {{ row.kind === 'operation' ? '用户操作' : '系统运行' }}
          </ElTag>
        </template>
        <template #time="{ row }">
          <div class="time-cell">
            <span>{{ getTimeText(row.time) }}</span>
            <small>{{ getDateText(row.time) }}</small>
          </div>
        </template>
        <template #content="{ row }">
          <ElTooltip :content="row.content" placement="top" :disabled="row.content?.length < 50">
            <span class="content-cell">{{ row.content }}</span>
          </ElTooltip>
        </template>
        <template #operatorName="{ row }">
          <span>{{ row.operatorName || '-' }}</span>
        </template>
        <template #deviceCode="{ row }">
          <span>{{ row.deviceCode || '-' }}</span>
        </template>
        <template #resultStatus="{ row }">
          <ElTag v-if="row.resultStatus" :type="getResultStatusType(row.resultStatus)" effect="light" size="small">
            {{ getResultStatusText(row.resultStatus) }}
          </ElTag>
          <span v-else>-</span>
        </template>
        <template #durationMs="{ row }">
          <span v-if="row.durationMs">{{ row.durationMs }}ms</span>
          <span v-else>-</span>
        </template>
        <template #action="{ row }">
          <ElButton type="primary" link size="small" @click.stop="showDetail(row)">
            详情
          </ElButton>
          <ElButton v-if="row.traceId" type="primary" link size="small" @click.stop="filterByTraceId(row.traceId)">
            按链路查询
          </ElButton>
        </template>
      </ArtTable>
      </template>
      <DbLogPanel v-else embedded class="mt-4" />
    </div>

    <!-- 详情抽屉 -->
    <ElDialog
      v-model="showDetailDrawer"
      title="日志详情"
      width="600px"
      :destroy-on-close="true"
    >
      <div v-if="currentLog" class="log-detail">
        <ElDescriptions :column="1" border>
          <ElDescriptionsItem label="时间">{{ currentLog.time }}</ElDescriptionsItem>
          <ElDescriptionsItem label="级别">
            <ElTag :type="getLevelType(currentLog.level)" effect="light" round>
              {{ currentLog.level }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="类型">
            <ElTag :type="getTypeType(currentLog.type)" effect="light" round>
              {{ getTypeText(currentLog.type) }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="来源">{{ currentLog.source }}</ElDescriptionsItem>
          <ElDescriptionsItem label="服务">{{ currentLog.serviceName || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="设备">{{ currentLog.deviceCode || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="操作人">{{ currentLog.operatorName || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem v-if="currentLog.moduleName" label="模块">{{ currentLog.moduleName }}</ElDescriptionsItem>
          <ElDescriptionsItem v-if="currentLog.actionName" label="动作">{{ currentLog.actionName }}</ElDescriptionsItem>
          <ElDescriptionsItem label="结果状态">
            <template v-if="currentLog.resultStatus">
              <ElTag :type="getResultStatusType(currentLog.resultStatus)" effect="light">
                {{ getResultStatusText(currentLog.resultStatus) }}
              </ElTag>
              <span v-if="currentLog.durationMs" style="margin-left: 8px">耗时: {{ currentLog.durationMs }}ms</span>
            </template>
            <span v-else>-</span>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="Request ID">{{ currentLog.requestId || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="Trace ID">{{ currentLog.traceId || '-' }}</ElDescriptionsItem>
        </ElDescriptions>

        <div class="detail-section">
          <h4>日志内容</h4>
          <pre class="content-pre">{{ currentLog.content }}</pre>
        </div>

        <div v-if="currentLog.detail" class="detail-section">
          <h4>结构化详情</h4>
          <pre class="content-pre">{{ JSON.stringify(currentLog.detail, null, 2) }}</pre>
        </div>

        <div v-if="currentLog.exceptionStack" class="detail-section">
          <h4>异常堆栈</h4>
          <pre class="content-pre">{{ currentLog.exceptionStack }}</pre>
        </div>

        <div class="detail-actions">
          <ElButton type="primary" @click="copyLog">复制日志</ElButton>
          <ElButton v-if="currentLog.traceId" @click="filterByTraceId(currentLog.traceId)">按 TraceId 查询</ElButton>
          <ElButton v-if="currentLog.deviceCode" @click="filterByDevice(currentLog.deviceCode)">按设备查询</ElButton>
          <ElButton v-if="currentLog.operatorName" @click="filterByOperator(currentLog.operatorName)">按操作人查询</ElButton>
        </div>
      </div>
    </ElDialog>

    <!-- 清理弹窗 -->
    <ElDialog v-model="showCleanupDialog" title="清理日志" width="500px" :destroy-on-close="true">
      <ElForm :model="cleanupForm" label-width="100px">
        <ElFormItem label="清理模式">
          <ElSelect v-model="cleanupForm.mode" style="width: 100%">
            <ElOption label="按策略清理" value="policy" />
            <ElOption label="清理指定时间之前" value="beforeTime" />
            <ElOption label="清理时间范围内" value="range" />
          </ElSelect>
        </ElFormItem>
        <ElFormItem v-if="cleanupForm.mode === 'beforeTime'" label="截止时间">
          <ElDatePicker
            v-model="cleanupForm.beforeTime"
            type="datetime"
            placeholder="选择截止时间"
            value-format="YYYY-MM-DD HH:mm:ss"
            style="width: 100%"
          />
        </ElFormItem>
        <template v-if="cleanupForm.mode === 'range'">
          <ElFormItem label="开始时间">
            <ElDatePicker
              v-model="cleanupForm.startTime"
              type="datetime"
              placeholder="选择开始时间"
              value-format="YYYY-MM-DD HH:mm:ss"
              style="width: 100%"
            />
          </ElFormItem>
          <ElFormItem label="结束时间">
            <ElDatePicker
              v-model="cleanupForm.endTime"
              type="datetime"
              placeholder="选择结束时间"
              value-format="YYYY-MM-DD HH:mm:ss"
              style="width: 100%"
            />
          </ElFormItem>
        </template>
        <ElFormItem label="强制清理">
          <ElSwitch v-model="cleanupForm.force" />
          <span class="form-hint">允许清理最近24小时日志</span>
        </ElFormItem>
      </ElForm>
      <template #footer>
        <ElButton @click="showCleanupDialog = false">取消</ElButton>
        <ElButton type="danger" :loading="cleanupLoading" @click="handleCleanup">确认清理</ElButton>
      </template>
    </ElDialog>

    <!-- DEBUG 开关弹窗 -->
    <ElDialog v-model="showDebugDialog" title="DEBUG 日志开关" width="500px" :destroy-on-close="true">
      <ElAlert
        v-if="stats.debugEnabled"
        :title="`DEBUG 正在记录中，剩余 ${formatRemainingTime(stats.debugRemainingSeconds)}`"
        type="warning"
        :closable="false"
        style="margin-bottom: 16px"
      />
      <ElForm :model="debugForm" label-width="100px">
        <ElFormItem label="开启 DEBUG">
          <ElSwitch v-model="debugForm.enabled" :disabled="stats.debugEnabled && stats.debugRemainingSeconds > 0" />
        </ElFormItem>
        <template v-if="debugForm.enabled">
          <ElFormItem label="开启时长">
            <ElSelect v-model="debugForm.durationMinutes" style="width: 100%">
              <ElOption label="30 分钟" :value="30" />
              <ElOption label="1 小时" :value="60" />
              <ElOption label="4 小时" :value="240" />
              <ElOption label="8 小时" :value="480" />
            </ElSelect>
          </ElFormItem>
          <ElFormItem label="开启原因" required>
            <ElInput
              v-model="debugForm.reason"
              type="textarea"
              :rows="3"
              placeholder="请填写开启原因（必填）"
            />
          </ElFormItem>
        </template>
      </ElForm>
      <ElAlert type="info" :closable="false" style="margin-top: 16px">
        DEBUG 日志会显著增加日志量，建议只在排障期间限时开启。到期后会自动关闭。
      </ElAlert>
      <template #footer>
        <ElButton @click="showDebugDialog = false">取消</ElButton>
        <ElButton
          :type="debugForm.enabled ? 'warning' : 'default'"
          :loading="debugLoading"
          @click="handleToggleDebug"
        >
          {{ debugForm.enabled ? '开启 DEBUG' : '关闭 DEBUG' }}
        </ElButton>
      </template>
    </ElDialog>

    <!-- 高级查询弹窗 -->
    <ElDialog v-model="showAdvancedDialog" title="高级查询" width="680px" align-center :destroy-on-close="false">
      <ArtSearchBar
        v-model="searchForm"
        :items="searchItems"
        :span="12"
        :is-expand="true"
        :show-search="false"
        :show-reset="false"
      />
      <template #footer>
        <ElButton @click="handleReset">重置</ElButton>
        <ElButton type="primary" @click="handleAdvancedSearch">查询</ElButton>
      </template>
    </ElDialog>

  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import * as echarts from 'echarts'
import { ElMessage } from 'element-plus'
import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'
import ArtCountTo from '@/components/core/text-effect/art-count-to/index.vue'
import ArtRingChart from '@/components/core/charts/art-ring-chart/index.vue'
import ArtSearchBar from '@/components/core/forms/art-search-bar/index.vue'
import ArtTable from '@/components/core/tables/art-table/index.vue'
import DbLogPanel from './db-log-panel.vue'
import { useTable } from '@/hooks/core/useTable'
import { fetchLogList, fetchLogStats, toggleLogDebug, cleanupLogs } from '@/api/device-monitor'
import type { ColumnOption } from '@/types/component'

defineOptions({ name: 'SystemLog' })

type LogSearchParams = Api.DeviceMonitor.LogSearchParams
type LogListItem = Api.DeviceMonitor.LogListItem
type LogStats = Api.DeviceMonitor.LogStats

const LEVEL_CONFIG: Record<string, { type: 'success' | 'warning' | 'danger' | 'info' | '' }> = {
  DEBUG: { type: '' },
  INFO: { type: 'info' },
  WARN: { type: 'warning' },
  ERROR: { type: 'danger' },
  CRITICAL: { type: 'danger' }
}

const TYPE_CONFIG: Record<string, { type: 'success' | 'warning' | 'danger' | 'info' | ''; text: string }> = {
  system: { type: 'info', text: '系统' },
  auth: { type: 'warning', text: '认证' },
  operation: { type: '', text: '操作' },
  collect: { type: 'success', text: '采集' },
  storage: { type: 'warning', text: '存储' },
  frontend: { type: 'info', text: '前端' },
  security: { type: 'danger', text: '安全' },
  alarm: { type: 'danger', text: '告警' }
}

const TYPE_OPTIONS = Object.entries(TYPE_CONFIG).map(([value, cfg]) => ({ label: cfg.text, value }))

// 高级查询专属字段（内联快捷筛选未覆盖的），用于计算高级查询按钮角标
const ADVANCED_KEYS = ['kind', 'level', 'source', 'serviceName', 'deviceCode', 'operatorName'] as const

const stats = ref<LogStats>({
  todayTotal: 0,
  yesterdayTotal: 0,
  todayWarnTotal: 0,
  todayErrorTotal: 0,
  todayCriticalTotal: 0,
  debugEnabled: false,
  debugRemainingSeconds: 0,
  totalSizeMb: 0,
  maxTotalMb: 1024,
  lastCleanupDeletedTotal: 0,
  levelTrend: [],
  typeRatio: [],
  sourceRatio: []
})

const searchForm = ref<LogSearchParams>({
  keyword: '',
  startTime: undefined,
  endTime: undefined,
  level: undefined,
  type: undefined,
  kind: undefined,
  source: undefined,
  serviceName: undefined,
  deviceCode: undefined,
  operatorName: undefined,
  traceId: undefined,
  requestId: undefined,
  resultStatus: undefined
})

const showDetailDrawer = ref(false)
const currentLog = ref<LogListItem | null>(null)
const showCleanupDialog = ref(false)
const showDebugDialog = ref(false)
const showAdvancedDialog = ref(false)
const logMode = ref<'system' | 'db'>('system')
const cleanupLoading = ref(false)

// 内联时间范围控件 ↔ searchForm.startTime/endTime（单一来源）
const dateRange = computed<[string, string] | null>({
  get() {
    const { startTime, endTime } = searchForm.value
    return startTime && endTime ? [startTime, endTime] : null
  },
  set(val) {
    searchForm.value.startTime = val?.[0] || undefined
    searchForm.value.endTime = val?.[1] || undefined
  }
})

// 高级查询按钮角标：高级专属字段中已填的数量
const advancedCount = computed(
  () =>
    ADVANCED_KEYS.filter((key) => {
      const value = searchForm.value[key]
      return value !== undefined && value !== '' && value !== null
    }).length
)
const debugLoading = ref(false)

const cleanupForm = ref({
  mode: 'policy' as 'policy' | 'beforeTime' | 'range',
  beforeTime: '',
  startTime: '',
  endTime: '',
  force: false
})

const debugForm = ref({
  enabled: false,
  durationMinutes: 60,
  reason: ''
})

const levelTrendChartRef = ref<HTMLElement>()
let levelTrendChart: echarts.ECharts | null = null

// 类型占比图表数据
const typeRatioChartData = computed(() => {
  if (!stats.value.typeRatio?.length) return []
  return stats.value.typeRatio.map((item: any) => ({
    name: TYPE_CONFIG[item.type]?.text || item.type,
    value: item.count
  }))
})

const searchItems = computed(() => [
  {
    key: 'startTime',
    label: '开始时间',
    type: 'datetime',
    props: {
      placeholder: '请选择开始时间',
      valueFormat: 'YYYY-MM-DD HH:mm:ss'
    }
  },
  {
    key: 'endTime',
    label: '结束时间',
    type: 'datetime',
    props: {
      placeholder: '请选择结束时间',
      valueFormat: 'YYYY-MM-DD HH:mm:ss'
    }
  },
  {
    key: 'kind',
    label: '日志种类',
    type: 'select',
    props: {
      placeholder: '请选择种类',
      clearable: true,
      options: [
        { label: '全部', value: 'all' },
        { label: '系统运行日志', value: 'system' },
        { label: '用户操作日志', value: 'operation' }
      ]
    }
  },
  {
    key: 'level',
    label: '日志级别',
    type: 'select',
    props: {
      placeholder: '请选择级别',
      clearable: true,
      options: [
        { label: 'DEBUG', value: 'DEBUG' },
        { label: 'INFO', value: 'INFO' },
        { label: 'WARN', value: 'WARN' },
        { label: 'ERROR', value: 'ERROR' },
        { label: 'CRITICAL', value: 'CRITICAL' }
      ]
    }
  },
  {
    key: 'type',
    label: '日志类型',
    type: 'select',
    props: {
      placeholder: '请选择类型',
      clearable: true,
      options: TYPE_OPTIONS
    }
  },
  {
    key: 'source',
    label: '来源',
    type: 'input',
    props: {
      placeholder: '请输入来源',
      clearable: true
    }
  },
  {
    key: 'serviceName',
    label: '服务名',
    type: 'select',
    props: {
      placeholder: '请选择服务',
      clearable: true,
      options: [
        { label: 'backend', value: 'backend' },
        { label: 'collector', value: 'collector' },
        { label: 'frontend', value: 'frontend' }
      ]
    }
  },
  {
    key: 'deviceCode',
    label: '设备编号',
    type: 'input',
    props: {
      placeholder: '请输入设备编号',
      clearable: true
    }
  },
  {
    key: 'operatorName',
    label: '操作人',
    type: 'input',
    props: {
      placeholder: '请输入操作人',
      clearable: true
    }
  },
  {
    key: 'keyword',
    label: '关键字',
    type: 'input',
    props: {
      placeholder: '请输入关键字',
      clearable: true
    }
  }
])

const columnsFactory = (): ColumnOption[] => [
  { type: 'index', width: 64, label: '序号' },
  { prop: 'time', label: '时间', width: 160, useSlot: true },
  { prop: 'level', label: '级别', width: 90, useSlot: true },
  { prop: 'kind', label: '种类', width: 100, useSlot: true },
  { prop: 'type', label: '类型', width: 80, useSlot: true },
  { prop: 'source', label: '来源', width: 130, showOverflowTooltip: true },
  { prop: 'deviceCode', label: '设备', width: 100, useSlot: true },
  { prop: 'operatorName', label: '操作人', width: 90, useSlot: true },
  { prop: 'content', label: '内容', minWidth: 300, useSlot: true, showOverflowTooltip: true },
  { prop: 'resultStatus', label: '结果', width: 80, useSlot: true },
  { prop: 'durationMs', label: '耗时', width: 80, useSlot: true },
  { prop: 'action', label: '操作', width: 140, useSlot: true, fixed: 'right' }
]

const {
  loading,
  data,
  pagination,
  columns,
  replaceSearchParams,
  resetSearchParams,
  getData,
  handleSizeChange,
  handleCurrentChange
} = useTable({
  core: {
    apiFn: fetchLogList,
    apiParams: {
      pageNo: 1,
      pageSize: 8
    },
    paginationKey: {
      current: 'pageNo',
      size: 'pageSize'
    },
    columnsFactory
  }
})

const getLevelType = (level: string) => LEVEL_CONFIG[level]?.type || 'info'
const getTypeType = (type: string) => TYPE_CONFIG[type]?.type || 'info'
const getTypeText = (type: string) => TYPE_CONFIG[type]?.text || type

const getResultStatusType = (status: string) => {
  if (status === 'success') return 'success'
  if (status === 'failed') return 'danger'
  if (status === 'denied') return 'warning'
  return 'info'
}

const getResultStatusText = (status: string) => {
  if (status === 'success') return '成功'
  if (status === 'failed') return '失败'
  if (status === 'denied') return '拒绝'
  return status
}

const getRowClassName = ({ row }: { row: LogListItem }) => {
  if (row.level === 'ERROR' || row.level === 'CRITICAL') return 'row-error'
  if (row.level === 'WARN') return 'row-warn'
  return ''
}

const getTimeText = (value: string) => {
  if (!value) return '-'
  const parts = value.split(' ')
  return parts[1] || value
}

const getDateText = (value: string) => {
  if (!value) return '-'
  const parts = value.split(' ')
  return parts[0] || '-'
}

const formatRemainingTime = (seconds: number) => {
  if (seconds <= 0) return '0秒'
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  if (hours > 0) return `${hours}小时${minutes}分钟`
  if (minutes > 0) return `${minutes}分钟`
  return `${seconds}秒`
}

const loadStats = async () => {
  try {
    const result = await fetchLogStats()
    stats.value = result
  } catch (error) {
    console.error('加载统计失败:', error)
  }
}

const initLevelTrendChart = () => {
  if (!levelTrendChartRef.value || !stats.value.levelTrend?.length) return

  if (!levelTrendChart) {
    levelTrendChart = echarts.init(levelTrendChartRef.value)
  }

  const times = stats.value.levelTrend.map((item: any) => item.time)
  const series = [
    { name: 'INFO', data: stats.value.levelTrend.map((item: any) => item.info), color: '#4A6CF7' },
    { name: 'WARN', data: stats.value.levelTrend.map((item: any) => item.warn), color: '#F4A542' },
    { name: 'ERROR', data: stats.value.levelTrend.map((item: any) => item.error), color: '#D66B88' },
    { name: 'CRITICAL', data: stats.value.levelTrend.map((item: any) => item.critical), color: '#D44F63' }
  ]

  levelTrendChart.setOption({
    tooltip: { trigger: 'axis' },
    legend: {
      top: 8,
      right: 0,
      icon: 'circle',
      itemWidth: 8,
      itemHeight: 8,
      textStyle: { color: '#66758f' },
      data: ['INFO', 'WARN', 'ERROR', 'CRITICAL']
    },
    grid: {
      top: 52,
      left: 0,
      right: 0,
      bottom: 6,
      containLabel: true
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: times,
      axisLine: { lineStyle: { color: '#e5e9f2' } },
      axisLabel: { color: '#8a94a6' }
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { color: '#eef1f6', type: 'dashed' } },
      axisLabel: { color: '#8a94a6' }
    },
    series: series.map((s) => ({
      name: s.name,
      type: 'line',
      smooth: true,
      symbol: 'circle',
      symbolSize: 7,
      data: s.data,
      lineStyle: { width: 3, color: s.color },
      itemStyle: { color: s.color },
      areaStyle: s.name === 'INFO'
        ? {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(74, 108, 247, 0.22)' },
            { offset: 1, color: 'rgba(74, 108, 247, 0.02)' }
          ])
        }
        : undefined
    }))
  })
}

// 空字符串/null 视为未填，避免请求携带空筛选条件
const clean = <T,>(value: T): T | undefined => (value === '' || value === null ? undefined : value)

const applySearch = () => {
  const f = searchForm.value
  replaceSearchParams({
    startTime: clean(f.startTime),
    endTime: clean(f.endTime),
    level: clean(f.level),
    type: clean(f.type),
    kind: clean(f.kind),
    source: clean(f.source),
    serviceName: clean(f.serviceName),
    deviceCode: clean(f.deviceCode),
    operatorName: clean(f.operatorName),
    keyword: clean(f.keyword)
  })
  getData()
}

const handleAdvancedSearch = () => {
  applySearch()
  showAdvancedDialog.value = false
}

const handleReset = async () => {
  searchForm.value = {
    keyword: '',
    startTime: undefined,
    endTime: undefined,
    level: undefined,
    type: undefined,
    kind: undefined,
    source: undefined,
    serviceName: undefined,
    deviceCode: undefined,
    operatorName: undefined,
    traceId: undefined,
    requestId: undefined,
    resultStatus: undefined
  }
  await resetSearchParams()
}

const handleRowClick = (row: LogListItem) => {
  showDetail(row)
}

const showDetail = (row: LogListItem) => {
  currentLog.value = row
  showDetailDrawer.value = true
}

const copyLog = () => {
  if (!currentLog.value) return
  const text = JSON.stringify(currentLog.value, null, 2)
  navigator.clipboard.writeText(text)
  ElMessage.success('已复制到剪贴板')
}

const filterByTraceId = (traceId: string) => {
  searchForm.value.traceId = traceId
  showDetailDrawer.value = false
  applySearch()
}

const filterByDevice = (deviceCode: string) => {
  searchForm.value.deviceCode = deviceCode
  showDetailDrawer.value = false
  applySearch()
}

const filterByOperator = (operatorName: string) => {
  searchForm.value.operatorName = operatorName
  showDetailDrawer.value = false
  applySearch()
}

const handleExport = () => {
  ElMessage.info('导出功能开发中')
}

const handleCleanup = async () => {
  cleanupLoading.value = true
  try {
    const result = await cleanupLogs({
      mode: cleanupForm.value.mode,
      beforeTime: cleanupForm.value.beforeTime || undefined,
      startTime: cleanupForm.value.startTime || undefined,
      endTime: cleanupForm.value.endTime || undefined,
      force: cleanupForm.value.force
    })
    ElMessage.success(`清理完成，删除 ${result.deletedOperationLogs + result.deletedSystemLogs} 条日志`)
    showCleanupDialog.value = false
    await Promise.all([loadStats(), getData()])
  } catch (error: any) {
    ElMessage.error(error?.message || '清理失败')
  } finally {
    cleanupLoading.value = false
  }
}

const handleToggleDebug = async () => {
  debugLoading.value = true
  try {
    await toggleLogDebug({
      enabled: debugForm.value.enabled,
      durationMinutes: debugForm.value.enabled ? debugForm.value.durationMinutes : undefined,
      reason: debugForm.value.enabled ? debugForm.value.reason : undefined
    })
    ElMessage.success(debugForm.value.enabled ? 'DEBUG 已开启' : 'DEBUG 已关闭')
    showDebugDialog.value = false
    await loadStats()
  } catch (error: any) {
    ElMessage.error(error?.message || '操作失败')
  } finally {
    debugLoading.value = false
  }
}

const handleResize = () => {
  levelTrendChart?.resize()
}

watch(stats, async () => {
  await nextTick()
  initLevelTrendChart()
}, { deep: true })

watch(showDebugDialog, (val) => {
  if (val) {
    debugForm.value = {
      enabled: !stats.value.debugEnabled,
      durationMinutes: 60,
      reason: ''
    }
  }
})

let statsPollingTimer: ReturnType<typeof setInterval> | null = null
const STATS_POLLING_INTERVAL = 30000 // 30秒轮询统计

onMounted(async () => {
  await loadStats()
  await getData()
  window.addEventListener('resize', handleResize)

  // 启动统计短轮询
  statsPollingTimer = setInterval(() => {
    loadStats()
  }, STATS_POLLING_INTERVAL)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  levelTrendChart?.dispose()
  levelTrendChart = null

  // 停止统计轮询
  if (statsPollingTimer) {
    clearInterval(statsPollingTimer)
    statsPollingTimer = null
  }
})
</script>

<style scoped lang="scss">
.log-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-bottom: 24px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 16px;
}

.charts-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.55fr) minmax(360px, 1.05fr);
  gap: 16px;
}

.chart-card {
  border-radius: 8px;
}

.chart-frame {
  position: relative;
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.chart-surface {
  width: 100%;
  flex: 1;
  min-height: 0;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.header-actions {
  display: flex;
  gap: 8px;
}

// 头部右侧操作区：清掉 Element Plus 相邻按钮的额外左边距，间距统一交给 gap
.log-actions {
  :deep(.el-button + .el-button) {
    margin-left: 0;
  }

  // datetimerange 宽度由 CSS 变量控制（默认 400px），内联 width 无效，须覆盖变量；
  // range-editor 自带 flex:1 会在 flex 容器里无视 width 撑大，须 flex:none 关掉伸展
  :deep(.el-date-editor--datetimerange) {
    --el-date-editor-width: 400px;
    flex: none;
  }

}

// 次要按钮（切换日志/重置/高级查询/DEBUG 关闭态）暗色下 hover 偏弱，
// 统一 hover 填充实色 primary，与「查询」按钮一致
.title :deep(.el-button:hover),
.log-actions :deep(.el-button--default:hover) {
  color: #fff;
  background-color: var(--el-color-primary);
  border-color: var(--el-color-primary);
}

// 暗色模式下 primary/danger 的 plain 按钮边框过暗，比实色按钮显得"小"，提亮边框增强存在感
:global(html.dark .log-actions .el-button--primary.is-plain) {
  --el-button-border-color: var(--el-color-primary);
}

:global(html.dark .log-actions .el-button--danger.is-plain) {
  --el-button-border-color: var(--el-color-danger);
}

.time-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;

  span {
    font-weight: 500;
  }

  small {
    font-size: 12px;
    color: var(--el-text-color-secondary);
  }
}

.content-cell {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.log-detail {
  padding: 0 16px;
}

.detail-section {
  margin-top: 20px;

  h4 {
    margin-bottom: 12px;
    font-size: 14px;
    font-weight: 500;
  }
}

.content-pre {
  padding: 12px;
  margin: 0;
  overflow: auto;
  font-size: 12px;
  line-height: 1.6;
  background: var(--el-fill-color-light);
  border-radius: 4px;
}

.detail-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 20px;
}

.form-hint {
  margin-left: 12px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

:deep(.row-error) {
  background-color: rgba(244, 63, 94, 0.08) !important;
}

:deep(.row-warn) {
  background-color: rgba(244, 165, 66, 0.08) !important;
}

// 日志级别标签主题适配
:deep(.el-tag.el-tag--danger.el-tag--light) {
  background-color: rgba(244, 63, 94, 0.12);
  border-color: rgba(244, 63, 94, 0.2);
  color: #f43f5e;
}

:deep(.el-tag.el-tag--warning.el-tag--light) {
  background-color: rgba(244, 165, 66, 0.12);
  border-color: rgba(244, 165, 66, 0.2);
  color: #f4a542;
}

:deep(.el-tag.el-tag--info.el-tag--light) {
  background-color: rgba(74, 108, 247, 0.12);
  border-color: rgba(74, 108, 247, 0.2);
  color: #4a6cf7;
}

:deep(.el-tag.el-tag--success.el-tag--light) {
  background-color: rgba(61, 187, 154, 0.12);
  border-color: rgba(61, 187, 154, 0.2);
  color: #3dbb9a;
}

:deep(.el-tag.el-tag--plain) {
  background-color: rgba(148, 163, 184, 0.12);
  border-color: rgba(148, 163, 184, 0.2);
  color: var(--el-text-color-regular);
}

@media (max-width: 1400px) {
  .stats-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (max-width: 1000px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .charts-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .stats-grid {
    grid-template-columns: 1fr;
  }
}
</style>
