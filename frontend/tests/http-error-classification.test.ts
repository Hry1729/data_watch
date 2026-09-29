import assert from 'node:assert/strict'
import test from 'node:test'
import { classifyNoResponseError } from '../src/utils/http/error-classification'

test('Axios timeout codes are classified separately from network failures', () => {
  assert.equal(classifyNoResponseError('ECONNABORTED'), 'timeout')
  assert.equal(classifyNoResponseError('ETIMEDOUT'), 'timeout')
  assert.equal(classifyNoResponseError('ERR_NETWORK'), 'network')
  assert.equal(classifyNoResponseError(undefined), 'network')
})
