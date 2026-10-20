<!-- 数据库日志（TDengine 服务日志）只读查看面板 -->
<template>
  <section :class="['db-log-panel', embedded ? '' : 'art-card p-5 mt-4 bg-[var(--default-box-color)]']">
    <div class="art-card-header flex justify-between items-center pr-0 mb-3 flex-wrap gap-2">
      <div v-if="!embedded" class="title">
        <h4 class="text-lg font-medium text-g-900">数据库日志（TDengine 服务日志）</h4>
      </div>
      <span v-else />
      <div class="flex items-center gap-2 flex-wrap">
        <ElSelect
          v-model="currentFile"
          placeholder="选择日志文件"
          size="small"
          style="width: 260px"
          :disabled="!enabled || !files.length"
          @change="loadTail"
        >
          <ElOption v-for="f in files" :key="f.name" :label="optionLabel(f)" :value="f.name" />
        </ElSelect>
        <ElInputNumber
          v-model="lines"
          :min="1"
          :max="2000"
          :step="100"
          size="small"
          controls-position="right"
          style="width: 120px"
          :disabled="!enabled"
          @change="loadTail"
        />
        <ElSwitch v-model="autoRefresh" size="small" active-text="自动" :disabled="!enabled" />
        <ElButton size="small" :loading="loading" :disabled="!enabled" @click="loadTail">刷新</ElButton>
      </div>
    </div>

    <ElAlert
      v-if="!enabled"
      type="info"
      :closable="false"
      :title="reason || '当前部署未启用数据库日志查看（仅 compose 托管 TDengine 时可用）'"
      show-icon
    />

    <template v-else>
      <ElAlert
        v-if="tailReason"
        type="warning"
        :closable="false"
        :title="tailReason"
        show-icon
        class="mb-2"
      />
      <ElAlert
        v-else-if="truncated"
        type="warning"
        :closable="false"
        title="日志较大，仅显示最近部分（已达 5MB 扫描上限）"
        show-icon
        class="mb-2"
      />
      <pre v-if="content" ref="boxRef" class="db-log-box">{{ content }}</pre>
      <ElEmpty v-else description="暂无日志" />
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import {
  fetchTdengineLogFiles,
  fetchTdengineLogTail,
  type TdengineLogFile
} from '@/api/device-monitor'

defineProps<{ embedded?: boolean }>()

const enabled = ref(true)
const reason = ref('')
const files = ref<TdengineLogFile[]>([])
const currentFile = ref('')
const lines = ref(300)
const rows = ref<string[]>([])
const truncated = ref(false)
const tailReason = ref('')
const loading = ref(false)
const autoRefresh = ref(false)
const boxRef = ref<HTMLElement>()
let timer: ReturnType<typeof setInterval> | null = null

const content = computed(() => rows.value.join('\n'))

function optionLabel(f: TdengineLogFile): string {
  const kb = (f.size / 1024).toFixed(0)
  return `${f.name} · ${f.kind} · ${kb}KB${f.compressed ? ' [gz]' : ''}`
}

function scrollToBottom() {
  if (boxRef.value) boxRef.value.scrollTop = boxRef.value.scrollHeight
}

async function loadFiles() {
  try {
    const data = await fetchTdengineLogFiles()
    enabled.value = data.enabled
    reason.value = data.reason
    files.value = data.files || []
    if (enabled.value && files.value.length && !currentFile.value) {
      const first = files.value.find((f) => f.tailSupported) || files.value[0]
      currentFile.value = first.name
      await loadTail()
    }
  } catch {
    enabled.value = false
    reason.value = '加载日志文件列表失败'
  }
}

// 请求代号：切文件/改行数/自动刷新交错时，只接受最新请求且参数仍匹配当前选择的响应
let tailRequestVersion = 0

async function loadTail() {
  if (!enabled.value || !currentFile.value) return
  const requestVersion = ++tailRequestVersion
  const file = currentFile.value
  const lineCount = lines.value
  const isCurrent = () =>
    requestVersion === tailRequestVersion && file === currentFile.value && lineCount === lines.value
  loading.value = true
  try {
    const data = await fetchTdengineLogTail(file, lineCount)
    if (!isCurrent()) return
    enabled.value = data.enabled
    if (!data.enabled) {
      reason.value = data.reason
      return
    }
    rows.value = data.lines || []
    truncated.value = data.truncated
    tailReason.value = data.reason || ''
    await nextTick()
    scrollToBottom()
  } catch {
    if (!isCurrent()) return
    ElMessage.error('读取日志失败')
  } finally {
    if (requestVersion === tailRequestVersion) loading.value = false
  }
}

watch(autoRefresh, (on) => {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
  if (on) timer = setInterval(loadTail, 5000)
})

onMounted(loadFiles)
onUnmounted(() => {
  // 卸载后丢弃在途响应
  tailRequestVersion += 1
  if (timer) clearInterval(timer)
})
</script>

<style scoped>
.db-log-box {
  max-height: 460px;
  overflow: auto;
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: var(--el-fill-color-light, #f5f7fa);
  font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
