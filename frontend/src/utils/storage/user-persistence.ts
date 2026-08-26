export const USER_PERSISTENCE_KEY = 'user'

/**
 * Only these user-store fields are safe to keep between browser sessions.
 * Authentication, lock and profile state must be rebuilt after login.
 */
export const USER_PERSISTENCE_PICK = ['language', 'searchHistory'] as const

const VERSIONED_USER_KEY = /^sys-v[^-]+-user$/

export interface UserPersistenceStorage {
  getItem(key: string): string | null
  setItem(key: string, value: string): void
  removeItem(key: string): void
  key(index: number): string | null
  readonly length: number
}

export function pickUserPersistence(state: unknown): Record<string, unknown> {
  if (!state || typeof state !== 'object') return {}

  const source = state as Record<string, unknown>
  const result: Record<string, unknown> = {}
  if (typeof source.language === 'string') result.language = source.language
  if (Array.isArray(source.searchHistory)) result.searchHistory = source.searchHistory
  return result
}

function isUserPersistenceKey(key: string): boolean {
  return key === USER_PERSISTENCE_KEY || VERSIONED_USER_KEY.test(key)
}

/**
 * Scrub old Pinia snapshots before the persisted-state plugin hydrates them.
 * Safe fields from versioned snapshots are migrated to the current key; all
 * legacy snapshots are then removed so old bearer/lock secrets cannot remain.
 */
export function migrateUserPersistence(storage: UserPersistenceStorage): void {
  const keys = Array.from({ length: storage.length }, (_, index) => storage.key(index))
    .filter((key): key is string => key !== null && isUserPersistenceKey(key))

  if (!keys.length) return

  const merged: Record<string, unknown> = {}
  for (const key of keys) {
    const raw = storage.getItem(key)
    if (!raw) continue

    try {
      const safe = pickUserPersistence(JSON.parse(raw))
      for (const field of USER_PERSISTENCE_PICK) {
        if (!(field in merged) && field in safe) merged[field] = safe[field]
      }
    } catch {
      // A corrupt snapshot is not a reason to prevent the app from starting.
    }
  }

  storage.setItem(USER_PERSISTENCE_KEY, JSON.stringify(merged))
  for (const key of keys) {
    if (key !== USER_PERSISTENCE_KEY) storage.removeItem(key)
  }
}
