export type NoResponseErrorKind = 'timeout' | 'network'

export function classifyNoResponseError(code?: string): NoResponseErrorKind {
  return code === 'ECONNABORTED' || code === 'ETIMEDOUT' ? 'timeout' : 'network'
}
