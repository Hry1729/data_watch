<!-- 日志终端 - 实时日志流 -->
<template>
  <div class="log-terminal-page">
    <!-- 顶部工具栏 -->
    <div class="terminal-toolbar">
      <div class="toolbar-left">
        <ElSelect v-model="filter.level" placeholder="级别" clearable size="small" style="width: 100px" @change="handleFilterChange">
          <ElOption v-for="level in levelOptions" :key="level.value" :label="level.label" :value="level.value" />
        </ElSelect>
        <ElSelect v-model="filter.type" placeholder="类型" clearable size="small" style="width: 120px" @change="handleFilterChange">
          <ElOption v-for="type in typeOptions" :key="type.value" :label="type.label" :value="type.value" />
        </ElSelect>
        <ElSelect v-model="filter.serviceName" placeholder="服务" clearable size="small" style="width: 100px" @change="handleFilterChange">
          <ElOption label="backend" value="backend" />
          <ElOption label="collector" value="collector" />
          <ElOption label="frontend" value="frontend" />
        </ElSelect>
        <ElInput
          v-model="filter.keyword"
          placeholder="关键词搜索"
          clearable
          size="small"
          style="width: 150px"
          @keyup.enter="handleFilterChange"
        >
          <template #prefix>
            <ElIcon><Search /></ElIcon>
          </template>
        </ElInput>
        <ElSelect v-model="pollIntervalSeconds" placeholder="刷新频率" size="small" style="width: 90px" @change="updatePollInterval">
          <ElOption label="1秒" :value="1" />
          <ElOption label="3秒" :value="3" />
          <ElOption label="5秒" :value="5" />
          <ElOption label="10秒" :value="10" />
        </ElSelect>
      </div>
      <div class="toolbar-right">
        <ElTag v-if="debugEnabled" type="warning" effect="dark" class="debug-tag">
          DEBUG 记录中，剩余 {{ formatTime(debugRemainingSeconds) }}
        </ElTag>
        <ElTag :type="connectionStatus === 'connected' ? 'success' : connectionStatus === 'error' ? 'danger' : 'info'">
          {{ connectionStatusText }}
        </ElTag>
        <ElTag type="info">已加载 {{ logs.length }} 条</ElTag>
        <ElButton v-if="!paused" type="primary" size="small" @click="pauseLog">
          <ElIcon><VideoPause /></ElIcon> 暂停
        </ElButton>
        <ElButton v-else type="success" size="small" @click="resumeLog">
          <ElIcon><VideoPlay /></ElIcon> 继续
        </ElButton>
        <ElButton size="small" @click="clearLogs">
          <ElIcon><Delete /></ElIcon> 清空
        </ElButton>
        <ElButton size="small" @click="copyLogs">
          <ElIcon><CopyDocument /></ElIcon> 复制
        </ElButton>
      </div>
    </div>

    <!-- 暂停提示 -->
    <div v-if="paused && pendingCount > 0" class="paused-banner">
      <ElAlert type="warning" :closable="false">
        <template #title>
          日志已暂停，暂停期间新增 {{ pendingCount }} 条日志
        </template>
      </ElAlert>
    </div>

    <!-- 终端主体 -->
    <div ref="terminalRef" class="terminal-body">
      <div v-if="logs.length === 0" class="terminal-empty">
        <ElEmpty description="暂无实时日志，等待系统事件产生" :image-size="120" />
      </div>
      <div v-else class="terminal-content">
        <div v-for="(log, index) in logs"
          :key="getLogKey(log)"
          class="log-line"
          :class="getLogLineClass(log)"
          @click="showLogDetail(log)"
        >
          <span class="log-time">{{ formatLogTime(log.time) }}</span>
          <span class="log-level" :class="`level-${log.level?.toLowerCase()}`">{{ log.level }}</span>
          <span class="log-source">{{ log.source || log.serviceName }}</span>
          <span class="log-type">{{ getTypeText(log.type) }}</span>
          <span v-if="log.deviceCode" class="log-device">{{ log.deviceCode }}</span>
          <span class="log-content" v-html="highlightKeyword(log.content)"></span>
          <span v-if="log.traceId" class="log-trace">{{ log.traceId?.slice(0, 8) }}</span>
        </div>
      </div>
    </div>

    <!-- 底部状态栏 -->
    <div class="terminal-footer">
      <span>最后更新: {{ lastUpdateTime || '-' }}</span>
      <span v-if="logs.length >= maxLogs">已达到最大显示条数 ({{ maxLogs }})</span>
      <ElButton v-if="showBackToTop" size="small" type="primary" @click="scrollToTop">
        回到顶部
      </ElButton>
    </div>

    <!-- 详情弹窗 -->
    <ElDialog
      v-model="showDetailDialog"
      title="日志详情"
      width="600px"
      :destroy-on-close="true"
    >
      <div v-if="currentLogDetail" class="log-detail-dialog">
        <ElDescriptions :column="1" border size="small">
          <ElDescriptionsItem label="时间">{{ currentLogDetail.time }}</ElDescriptionsItem>
          <ElDescriptionsItem label="级别">
            <ElTag :type="getLevelType(currentLogDetail.level)" effect="light">
              {{ currentLogDetail.level }}
            </ElTag>
          </ElDescriptionsItem>
          <ElDescriptionsItem label="类型">{{ currentLogDetail.type }}</ElDescriptionsItem>
          <ElDescriptionsItem label="来源">{{ currentLogDetail.source }}</ElDescriptionsItem>
          <ElDescriptionsItem label="服务">{{ currentLogDetail.serviceName }}</ElDescriptionsItem>
          <ElDescriptionsItem label="设备">{{ currentLogDetail.deviceCode || '-' }}</ElDescriptionsItem>
          <ElDescriptionsItem label="Trace ID">{{ currentLogDetail.traceId || '-' }}</ElDescriptionsItem>
        </ElDescriptions>
        <div class="detail-section">
          <h4>内容</h4>
          <pre class="detail-content">{{ currentLogDetail.content }}</pre>
        </div>
        <div v-if="currentLogDetail.detail" class="detail-section">
          <h4>详情</h4>
          <pre class="detail-content">{{ JSON.stringify(currentLogDetail.detail, null, 2) }}</pre>
        </div>
      </div>
    </ElDialog>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { CopyDocument, Delete, Search, VideoPause, VideoPlay } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { fetchLogList, fetchLogStats } from '@/api/device-monitor'

defineOptions({ name: 'LogTerminal' })

type LogListItem = Api.DeviceMonitor.LogListItem

const levelOptions = [
  { label: 'DEBUG', value: 'DEBUG' },
  { label: 'INFO', value: 'INFO' },
  { label: 'WARN', value: 'WARN' },
  { label: 'ERROR', value: 'ERROR' },
  { label: 'CRITICAL', value: 'CRITICAL' }
]

const typeOptions = [
  { label: '系统', value: 'system' },
  { label: '认证', value: 'auth' },
  { label: '操作', value: 'operation' },
  { label: '采集', value: 'collect' },
  { label: '存储', value: 'storage' },
  { label: '前端', value: 'frontend' },
  { label: '安全', value: 'security' },
  { label: '告警', value: 'alarm' }
]

const TYPE_TEXT_MAP: Record<string, string> = {
  system: '系统',
  auth: '认证',
  operation: '操作',
  collect: '采集',
  storage: '存储',
  frontend: '前端',
  security: '安全',
  alarm: '告警'
}

const getTypeText = (type: string) => TYPE_TEXT_MAP[type] || type

const maxLogs = 1000
const pollIntervalSeconds = ref(3) // 默认3秒轮询

const logs = ref<LogListItem[]>([])
const lastLogId = ref<number>(0)
const filter = ref({
  level: '',
  type: '',
  serviceName: '',
  keyword: ''
})

const autoScroll = ref(true)
const paused = ref(false)
const pendingCount = ref(0)
const pendingLogs = new Map<string, LogListItem>() // 暂停期间累计的新日志（用于恢复后补齐）
const lastUpdateTime = ref('')
const connectionStatus = ref<'connecting' | 'connected' | 'error' | 'paused'>('connecting')
const debugEnabled = ref(false)
const debugRemainingSeconds = ref(0)
const showBackToTop = ref(false)

const terminalRef = ref<HTMLElement>()
const showDetailDialog = ref(false)
const currentLogDetail = ref<LogListItem | null>(null)

let pollTimer: ReturnType<typeof setInterval> | null = null
let statsTimer: ReturnType<typeof setInterval> | null = null

const connectionStatusText = computed(() => {
  switch (connectionStatus.value) {
    case 'connecting': return '连接中'
    case 'connected': return '已连接'
    case 'error': return '接口异常'
    case 'paused': return '已暂停'
    default: return '未知'
  }
})

const getLevelType = (level: string) => {
  const map: Record<string, 'success' | 'warning' | 'danger' | 'info' | ''> = {
    DEBUG: '',
    INFO: 'info',
    WARN: 'warning',
    ERROR: 'danger',
    CRITICAL: 'danger'
  }
  return map[level] || 'info'
}

const getLogLineClass = (log: LogListItem) => {
  if (log.level === 'ERROR' || log.level === 'CRITICAL') return 'log-line-error'
  if (log.level === 'WARN') return 'log-line-warn'
  return ''
}

const formatLogTime = (time: string) => {
  if (!time) return '-'
  return time.slice(11, 23) // HH:mm:ss.SSS
}

const formatTime = (seconds: number) => {
  if (seconds <= 0) return '0秒'
  const hours = Math.floor(seconds / 3600)
  const minutes = Math.floor((seconds % 3600) / 60)
  if (hours > 0) return `${hours}小时${minutes}分钟`
  if (minutes > 0) return `${minutes}分钟`
  return `${seconds}秒`
}

const escapeRegExp = (value: string) => value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

// HTML 转义，防止 XSS
const escapeHtml = (value: string) => {
  if (!value) return ''
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;')
}

// 先转义 HTML，再对转义后的文本做关键词高亮
const highlightKeyword = (content: string) => {
  if (!content) return ''
  const escaped = escapeHtml(content)
  if (!filter.value.keyword) return escaped
  const keyword = escapeRegExp(escapeHtml(filter.value.keyword))
  const regex = new RegExp(`(${keyword})`, 'gi')
  return escaped.replace(regex, '<mark class="highlight">$1</mark>')
}

// 生成复合 key，防止 operation_log 和 system_log id 碰撞
const getLogKey = (log: LogListItem) => `${log.kind || 'unknown'}:${log.id}`

const fetchLogs = async () => {
  try {
    connectionStatus.value = 'connecting'

    const params: Api.DeviceMonitor.LogSearchParams = {
      pageNo: 1,
      pageSize: 100
    }

    // 添加筛选条件
    if (filter.value.level) params.level = filter.value.level as any
    if (filter.value.type) params.type = filter.value.type as any
    if (filter.value.serviceName) params.serviceName = filter.value.serviceName
    if (filter.value.keyword) params.keyword = filter.value.keyword

    const result = await fetchLogList(params)

    if (result.list && result.list.length > 0) {
      if (paused.value) {
        // 暂停时累计新日志（排除已计入的），用于恢复后补齐
        const existingKeys = new Set(logs.value.map(l => getLogKey(l)))
        const newPendingLogs = result.list.filter((log) => {
          const key = getLogKey(log)
          return !existingKeys.has(key) && !pendingLogs.has(key)
        })
        newPendingLogs.forEach((log) => pendingLogs.set(getLogKey(log), log))
        pendingCount.value = pendingLogs.size
      } else {
        // 增量添加：使用复合 key 去重（kind:id）
        const existingKeys = new Set(logs.value.map(l => getLogKey(l)))
        const newLogs = result.list.filter(l => !existingKeys.has(getLogKey(l)))

        if (newLogs.length > 0) {
          // 添加到顶部（时间降序）
          logs.value = [...newLogs, ...logs.value]

          // 限制最大数量
          if (logs.value.length > maxLogs) {
            logs.value = logs.value.slice(0, maxLogs)
          }

          // 更新最新日志 ID
          const maxId = Math.max(...logs.value.map(l => Number(l.id) || 0))
          lastLogId.value = maxId

          lastUpdateTime.value = new Date().toLocaleTimeString()

          if (autoScroll.value) {
            await nextTick()
            scrollToTop()
          }
        }
      }
    }

    // 暂停时恢复状态为 paused，否则为 connected
    connectionStatus.value = paused.value ? 'paused' : 'connected'
  } catch (error) {
    connectionStatus.value = paused.value ? 'paused' : 'error'
    console.error('获取日志失败:', error)
  }
}

const fetchDebugStats = async () => {
  try {
    const stats = await fetchLogStats()
    debugEnabled.value = stats.debugEnabled
    debugRemainingSeconds.value = stats.debugRemainingSeconds
  } catch (error) {
    console.error('获取状态失败:', error)
  }
}

const scrollToTop = () => {
  if (terminalRef.value) {
    terminalRef.value.scrollTop = 0
  }
}

const scrollToBottom = () => {
  if (terminalRef.value) {
    terminalRef.value.scrollTop = terminalRef.value.scrollHeight
  }
}

const handleFilterChange = () => {
  logs.value = []
  lastLogId.value = 0
  pendingCount.value = 0
  pendingLogs.clear()
  fetchLogs()
}

const pauseLog = () => {
  paused.value = true
  connectionStatus.value = 'paused'
  pendingCount.value = 0
  pendingLogs.clear()
}

const resumeLog = () => {
  paused.value = false
  connectionStatus.value = 'connected'

  // 把暂停期间累计的日志补齐到列表头部
  if (pendingLogs.size > 0) {
    const pendingLogList = Array.from(pendingLogs.values())
    const existingKeys = new Set(logs.value.map(l => getLogKey(l)))
    const newLogs = pendingLogList.filter(l => !existingKeys.has(getLogKey(l)))

    if (newLogs.length > 0) {
      logs.value = [...newLogs, ...logs.value]

      if (logs.value.length > maxLogs) {
        logs.value = logs.value.slice(0, maxLogs)
      }

      const maxId = Math.max(...logs.value.map(l => Number(l.id) || 0))
      lastLogId.value = maxId

      lastUpdateTime.value = new Date().toLocaleTimeString()
    }
  }

  pendingCount.value = 0
  pendingLogs.clear()
  fetchLogs()
}

const clearLogs = () => {
  logs.value = []
  lastLogId.value = 0
  lastUpdateTime.value = ''
  pendingCount.value = 0
  pendingLogs.clear()
  ElMessage.success('已清空')
}

const copyLogs = () => {
  const text = logs.value.map(log =>
    `${log.time} | ${log.level} | ${log.source} | ${log.type} | ${log.content}`
  ).join('\n')
  navigator.clipboard.writeText(text)
  ElMessage.success('已复制到剪贴板')
}

const showLogDetail = (log: LogListItem) => {
  currentLogDetail.value = log
  showDetailDialog.value = true
}

const startPolling = () => {
  stopPolling()
  pollTimer = setInterval(fetchLogs, pollIntervalSeconds.value * 1000)
  statsTimer = setInterval(fetchDebugStats, 10000)
}

const stopPolling = () => {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
  if (statsTimer) {
    clearInterval(statsTimer)
    statsTimer = null
  }
}

const updatePollInterval = () => {
  // 重启轮询使用新的间隔
  if (!paused.value) {
    startPolling()
  }
}

// 监听滚动，用户手动滚动时显示"回到顶部"按钮
const handleScroll = () => {
  if (!terminalRef.value) return

  const { scrollTop, scrollHeight, clientHeight } = terminalRef.value
  const isNearTop = scrollTop < 200

  // 当用户向下滚动时显示回到顶部按钮
  showBackToTop.value = !isNearTop && logs.value.length > 10
}

onMounted(() => {
  fetchLogs()
  fetchDebugStats()
  startPolling()

  if (terminalRef.value) {
    terminalRef.value.addEventListener('scroll', handleScroll)
  }
})

onUnmounted(() => {
  stopPolling()
  if (terminalRef.value) {
    terminalRef.value.removeEventListener('scroll', handleScroll)
  }
})
</script>

<style scoped lang="scss">
.log-terminal-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 100px);
  background: #1a1a2e;
  border-radius: 8px;
  overflow: hidden;
}

.terminal-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: #16213e;
  border-bottom: 1px solid #2d3748;
}

.toolbar-left,
.toolbar-right {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.debug-tag {
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.7; }
}

.terminal-body {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;

  &::-webkit-scrollbar {
    width: 8px;
  }

  &::-webkit-scrollbar-track {
    background: #1a1a2e;
  }

  &::-webkit-scrollbar-thumb {
    background: #3d4f6f;
    border-radius: 4px;
  }
}

.terminal-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;

  :deep(.el-empty__description) {
    color: #718096;
  }
}

.terminal-content {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.log-line {
  display: flex;
  gap: 12px;
  padding: 6px 8px;
  color: #e2e8f0;
  border-radius: 4px;
  cursor: pointer;
  transition: background-color 0.15s;

  &:hover {
    background: rgba(255, 255, 255, 0.05);
  }

  &.log-line-error {
    background: rgba(245, 108, 108, 0.1);
    border-left: 3px solid #f56c6c;
  }

  &.log-line-warn {
    background: rgba(230, 162, 60, 0.1);
    border-left: 3px solid #e6a23c;
  }
}

.log-time {
  flex-shrink: 0;
  color: #718096;
}

.log-level {
  flex-shrink: 0;
  width: 70px;
  font-weight: 600;

  &.level-debug { color: #909399; }
  &.level-info { color: #409eff; }
  &.level-warn { color: #e6a23c; }
  &.level-error { color: #f56c6c; }
  &.level-critical { color: #f56c6c; font-weight: 700; }
}

.log-source {
  flex-shrink: 0;
  width: 100px;
  overflow: hidden;
  color: #a0aec0;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.log-type {
  flex-shrink: 0;
  width: 60px;
  color: #68d391;
}

.log-device {
  flex-shrink: 0;
  color: #f6ad55;
}

.log-content {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;

  :deep(.highlight) {
    padding: 0 2px;
    background: #fef08a;
    border-radius: 2px;
    color: #1a1a2e;
  }
}

.log-trace {
  flex-shrink: 0;
  color: #718096;
  font-size: 11px;
}

.terminal-footer {
  display: flex;
  gap: 24px;
  align-items: center;
  padding: 8px 16px;
  font-size: 12px;
  color: #718096;
  background: #16213e;
  border-top: 1px solid #2d3748;
}

.paused-banner {
  padding: 0 16px;

  :deep(.el-alert) {
    border-radius: 0;
  }
}

.log-detail-dialog {
  .detail-section {
    margin-top: 16px;

    h4 {
      margin-bottom: 8px;
      font-size: 14px;
      font-weight: 500;
    }
  }

  .detail-content {
    max-height: 200px;
    padding: 12px;
    margin: 0;
    overflow: auto;
    font-size: 12px;
    line-height: 1.6;
    color: var(--el-text-color-primary);
    background: var(--el-fill-color-light);
    border: 1px solid var(--el-border-color-light);
    border-radius: 4px;
  }
}

:deep(.el-dialog .log-detail-dialog) {
  color: var(--el-text-color-primary);

  .el-descriptions__body,
  .el-descriptions__cell {
    color: var(--el-text-color-primary);
    background-color: var(--el-bg-color);
  }

  .el-descriptions__label {
    color: var(--el-text-color-regular);
    background-color: var(--el-fill-color-light);
  }
}
</style>
