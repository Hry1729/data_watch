import assert from 'node:assert/strict'
import test from 'node:test'
import { passwordChangeCompletion } from '../src/lib/password-change-flow.ts'

test('app re-authenticates after password change because tokenVersion rotates', () => {
  assert.deepEqual(passwordChangeCompletion(), { clearSession: true, redirectToLogin: true })
})
