import assert from 'node:assert/strict'
import test from 'node:test'
import { passwordChangeCompletion } from '../src/utils/password-change-flow'

test('web re-authenticates after password change because tokenVersion rotates', () => {
  assert.deepEqual(passwordChangeCompletion(), { clearSession: true, redirectToLogin: true })
})
