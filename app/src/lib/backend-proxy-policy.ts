const BACKEND_METHODS = new Set(['GET', 'POST', 'PUT', 'PATCH', 'DELETE'])

// Backend defaults to a 4 MiB HTTP body limit; keep the desktop proxy aligned.
export const MAX_BACKEND_REQUEST_BYTES = 4 * 1024 * 1024

/** Normalize the configured URL to an origin; credentials and URL suffixes are never accepted. */
export function normalizeBackendOrigin(value: string): string | null {
  try {
    const url = new URL(value.trim())
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return null
    if (url.username || url.password || (url.pathname !== '' && url.pathname !== '/')) return null
    if (url.search || url.hash) return null
    return url.origin
  } catch {
    return null
  }
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
