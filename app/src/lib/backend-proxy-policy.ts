const BACKEND_METHODS = new Set(['GET', 'POST', 'PUT', 'PATCH', 'DELETE'])

// Backend defaults to a 4 MiB HTTP body limit; keep the desktop proxy aligned.
export const MAX_BACKEND_REQUEST_BYTES = 4 * 1024 * 1024

export type BackendOriginCheck = { origin: string; error: null } | { origin: null; error: string }

/**
 * 后端 / 中心地址的唯一校验规则（引导页、登录前注入、Rust 代理共用同一口径）。
 * 只接受 http(s)://主机[:端口]；不接受用户名密码、路径、查询参数或 #，失败时给出具体原因。
 */
export function validateBackendOrigin(value: string): BackendOriginCheck {
  const raw = value.trim()
  if (!raw) return { origin: null, error: '地址不能为空' }
  let url: URL
  try {
    url = new URL(raw)
  } catch {
    return { origin: null, error: '地址格式无效，应为 http(s)://主机:端口' }
  }
  if (url.protocol !== 'http:' && url.protocol !== 'https:') {
    return { origin: null, error: '只支持 http:// 或 https:// 地址' }
  }
  if (url.username || url.password) return { origin: null, error: '地址不能包含用户名或密码' }
  if ((url.pathname !== '' && url.pathname !== '/') || url.search || url.hash) {
    return { origin: null, error: '地址只能填写到端口（例如 http://192.168.1.10:8080），不能带路径、查询参数或 #' }
  }
  return { origin: url.origin, error: null }
}

/** Normalize the configured URL to an origin; credentials and URL suffixes are never accepted. */
export function normalizeBackendOrigin(value: string): string | null {
  return validateBackendOrigin(value).origin
}

export function isAllowedBackendPath(path: string): boolean {
  if (path.length > 4096 || !path.startsWith('/api/') || path.startsWith('//')) return false
  if (path.includes('\\') || path.includes('\0')) return false
  const pathOnly = path.split('?', 1)[0]
  return !pathOnly.split('/').some((segment) => segment === '..')
}

export function isAllowedBackendMethod(method: string): boolean {
  return BACKEND_METHODS.has(method)
}

export function requestBodyByteLength(body: string | undefined): number {
  return body === undefined ? 0 : new TextEncoder().encode(body).byteLength
}
