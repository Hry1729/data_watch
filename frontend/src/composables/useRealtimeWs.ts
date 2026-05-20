import { ref, onMounted, onUnmounted } from 'vue'
import { useUserStore } from '@/store/modules/user'

interface PointValue {
  v: string
  t: number
  q: number
}

export function useRealtimeWs(deviceCode: string, groupCode: string = 'g001') {
  const values = ref<Record<string, PointValue>>({})
  const status = ref<'connecting' | 'online' | 'offline'>('connecting')
  let ws: WebSocket | null = null
  let retryDelay = 3000
  let retryTimer: ReturnType<typeof setTimeout> | null = null
  let currentGroupCode = groupCode

  function buildWsUrl(): string {
    const { VITE_API_URL } = import.meta.env
    const { accessToken } = useUserStore()
    const wsProtocol = location.protocol === 'https:' ? 'wss:' : 'ws:'
    const path = '/api/device-monitor/ws/realtime-push'

    const appendToken = (url: string) => {
      if (!accessToken) return url
      const sep = url.includes('?') ? '&' : '?'
      return `${url}${sep}token=${encodeURIComponent(accessToken)}`
    }

    if (!VITE_API_URL || VITE_API_URL === '/') {
      return appendToken(`${wsProtocol}//${location.host}${path}`)
    }

    if (/^https?:\/\//.test(VITE_API_URL)) {
      const base = VITE_API_URL.replace(/^http/, 'ws').replace(/\/$/, '')
      return appendToken(`${base}${path}`)
    }

    const normalized = VITE_API_URL.startsWith('/') ? VITE_API_URL.slice(1) : VITE_API_URL
    return appendToken(`${wsProtocol}//${location.host}/${normalized.replace(/\/$/, '')}${path}`)
  }

  function connect() {
    if (retryTimer) {
      clearTimeout(retryTimer)
      retryTimer = null
    }
    status.value = 'connecting'

    try {
      ws = new WebSocket(buildWsUrl())
    } catch {
      status.value = 'offline'
      scheduleRetry()
      return
    }

    ws.onopen = () => {
      retryDelay = 3000
      ws!.send(JSON.stringify({ action: 'subscribe', device_code: deviceCode, group_code: currentGroupCode }))
    }

    ws.onmessage = (e) => {
      try {
        const msg = JSON.parse(e.data)
        if (msg.type === 'snapshot') {
          values.value = msg.values ?? {}
          status.value = 'online'
        } else if (msg.type === 'diff') {
          Object.assign(values.value, msg.values)
          status.value = 'online'
        } else if (msg.type === 'offline') {
          status.value = 'offline'
        }
      } catch {
        // ignore parse errors
      }
    }

    ws.onerror = () => {
      status.value = 'offline'
    }

    ws.onclose = () => {
      if (status.value !== 'offline') status.value = 'offline'
      scheduleRetry()
    }
  }

  function scheduleRetry() {
    if (retryTimer) return
    retryTimer = setTimeout(() => {
      retryTimer = null
      connect()
    }, retryDelay)
    retryDelay = Math.min(retryDelay * 2, 30000)
  }

  function switchGroup(newGroupCode: string) {
    if (ws?.readyState === WebSocket.OPEN) {
      ws.send(JSON.stringify({ action: 'unsubscribe', device_code: deviceCode, group_code: currentGroupCode }))
      currentGroupCode = newGroupCode
      ws.send(JSON.stringify({ action: 'subscribe', device_code: deviceCode, group_code: currentGroupCode }))
      values.value = {}
    } else {
      currentGroupCode = newGroupCode
    }
  }

  // Page visibility: reconnect when tab becomes visible again
  let wasConnected = false
  function handleVisibility() {
    if (document.hidden) {
      wasConnected = ws?.readyState === WebSocket.OPEN
    } else if (wasConnected && ws?.readyState !== WebSocket.OPEN) {
      connect()
    }
  }

  onMounted(() => {
    connect()
    document.addEventListener('visibilitychange', handleVisibility)
  })

  onUnmounted(() => {
    document.removeEventListener('visibilitychange', handleVisibility)
    if (retryTimer) {
      clearTimeout(retryTimer)
      retryTimer = null
    }
    if (ws) {
      if (ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ action: 'unsubscribe', device_code: deviceCode, group_code: currentGroupCode }))
      }
      ws.close()
      ws = null
    }
  })

  return { values, status, switchGroup }
}
