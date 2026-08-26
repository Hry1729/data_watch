import assert from 'node:assert/strict'
import test from 'node:test'
import {
  migrateUserPersistence,
  pickUserPersistence,
  USER_PERSISTENCE_KEY
} from '../src/utils/storage/user-persistence'

class MemoryStorage {
  private values = new Map<string, string>()

  get length() {
    return this.values.size
  }

  key(index: number) {
    return Array.from(this.values.keys())[index] ?? null
  }

  getItem(key: string) {
    return this.values.get(key) ?? null
  }

  setItem(key: string, value: string) {
    this.values.set(key, value)
  }

  removeItem(key: string) {
    this.values.delete(key)
  }
}

test('user persistence picks no authentication or lock fields', () => {
  assert.deepEqual(
    pickUserPersistence({
      language: 'zh-cn',
      searchHistory: [{ path: '/device-monitor' }],
      accessToken: 'access-secret',
      refreshToken: 'refresh-secret',
      lockPassword: 'lock-secret',
      isLogin: true,
      info: { userId: 7 }
    }),
    { language: 'zh-cn', searchHistory: [{ path: '/device-monitor' }] }
  )
})

test('legacy snapshots are scrubbed and safe fields are migrated', () => {
  const storage = new MemoryStorage()
  storage.setItem('sys-v1.0.0-user', JSON.stringify({
    language: 'en-us',
    searchHistory: [],
    accessToken: 'old-access-secret',
    refreshToken: 'old-refresh-secret',
    lockPassword: 'old-lock-secret'
  }))

  migrateUserPersistence(storage)

  assert.deepEqual(JSON.parse(storage.getItem(USER_PERSISTENCE_KEY) ?? '{}'), {
    language: 'en-us',
    searchHistory: []
  })
  assert.equal(storage.getItem('sys-v1.0.0-user'), null)
})
