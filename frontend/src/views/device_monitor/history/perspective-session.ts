const PERSPECTIVE_SESSION_STORAGE_KEY = 'device-monitor:history:perspective-session-id'
const PERSPECTIVE_WS_PATH = '/api/device-monitor/ws/perspective'

export interface HistoryPerspectiveRecord {
  timestamp: string
  deviceId: number
  deviceCode: string
  varSeq: number
  pointName: string
  ioAddress: string
  value: string | number
}

export interface PerspectiveSessionState {
  perspectiveSessionId: string
  connectionId: string
  queryVersion: number
}

export interface HistoryQueryContext {
  deviceId: number
  varSeqs: number[]
  startTime: string
  endTime: string
  queryVersion: number
  perspectiveSessionId: string
}

export interface PerspectiveSnapshotEvent {
  type: 'snapshot'
  sessionId: string
  queryVersion: number
  recordCount: number
  records: HistoryPerspectiveRecord[]
  timestamp?: string
}

export interface PerspectiveSessionRestoredEvent {
  type: 'session_restored'
  sessionId: string
}

export interface PerspectiveSessionConflictEvent {
  type: 'session_conflict'
  sessionId: string
  message?: string
}

export interface PerspectiveErrorEvent {
  type: 'error'
  message: string
}

export interface PerspectivePongEvent {
  type: 'pong'
}

export type PerspectiveWsEvent =
  | PerspectiveSnapshotEvent
  | PerspectiveSessionRestoredEvent
  | PerspectiveSessionConflictEvent
  | PerspectiveErrorEvent
  | PerspectivePongEvent

const createRandomId = () => {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }

  return `${Date.now()}-${Math.random().toString(16).slice(2, 10)}`
}

const readStoredSessionId = () => {
  if (typeof window === 'undefined') return ''
  return window.sessionStorage.getItem(PERSPECTIVE_SESSION_STORAGE_KEY)?.trim() || ''
}

const writeStoredSessionId = (sessionId: string) => {
  if (typeof window === 'undefined') return
  window.sessionStorage.setItem(PERSPECTIVE_SESSION_STORAGE_KEY, sessionId)
}

export const getOrCreatePerspectiveSessionId = (forceNew: boolean = false) => {
  if (!forceNew) {
    const existingSessionId = readStoredSessionId()
    if (existingSessionId) {
      return existingSessionId
    }
  }

  const sessionId = createRandomId()
  writeStoredSessionId(sessionId)
  return sessionId
}

export const createPerspectiveConnectionId = () => createRandomId()

export const createPerspectiveSessionState = (): PerspectiveSessionState => ({
  perspectiveSessionId: getOrCreatePerspectiveSessionId(),
  connectionId: createPerspectiveConnectionId(),
  queryVersion: 0
})

export const nextPerspectiveQueryVersion = (state: PerspectiveSessionState) => {
  state.queryVersion += 1
  return state.queryVersion
}

export const rotatePerspectiveConnectionId = (state: PerspectiveSessionState) => {
  state.connectionId = createPerspectiveConnectionId()
  return state.connectionId
}

export const resetPerspectiveSession = (state: PerspectiveSessionState) => {
  state.perspectiveSessionId = getOrCreatePerspectiveSessionId(true)
  state.connectionId = createPerspectiveConnectionId()
  state.queryVersion = 0
  return state
}

const resolveWsBaseUrl = () => {
  const { VITE_API_URL } = import.meta.env
  const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'

  if (!VITE_API_URL || VITE_API_URL === '/') {
    return `${wsProtocol}//${window.location.host}`
  }

  if (/^https?:\/\//.test(VITE_API_URL)) {
    return VITE_API_URL.replace(/^http/, 'ws').replace(/\/$/, '')
  }

  const normalizedPath = VITE_API_URL.startsWith('/') ? VITE_API_URL : `/${VITE_API_URL}`
  return `${wsProtocol}//${window.location.host}${normalizedPath.replace(/\/$/, '')}`
}

export const buildPerspectiveWebSocketUrl = (
  perspectiveSessionId: string,
  connectionId: string
) => {
  const url = new URL(`${resolveWsBaseUrl()}${PERSPECTIVE_WS_PATH}`)
  url.searchParams.set('perspectiveSessionId', perspectiveSessionId)
  url.searchParams.set('connectionId', connectionId)
  return url.toString()
}

export const parsePerspectiveWsEvent = (payload: unknown): PerspectiveWsEvent | null => {
  if (!payload || typeof payload !== 'object') {
    return null
  }

  const event = payload as Record<string, unknown>
  const type = String(event.type || '').trim()

  if (type === 'snapshot') {
    return {
      type,
      sessionId: String(event.sessionId || ''),
      queryVersion: Number(event.queryVersion || 0),
      recordCount: Number(event.recordCount || 0),
      records: Array.isArray(event.records) ? (event.records as HistoryPerspectiveRecord[]) : [],
      timestamp: typeof event.timestamp === 'string' ? event.timestamp : undefined
    }
  }

  if (type === 'session_restored') {
    return {
      type,
      sessionId: String(event.sessionId || '')
    }
  }

  if (type === 'session_conflict') {
    return {
      type,
      sessionId: String(event.sessionId || ''),
      message: typeof event.message === 'string' ? event.message : undefined
    }
  }

  if (type === 'error') {
    return {
      type,
      message: String(event.message || 'Perspective websocket error')
    }
  }

  if (type === 'pong') {
    return { type }
  }

  return null
}
