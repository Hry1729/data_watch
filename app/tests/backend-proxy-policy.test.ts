import assert from 'node:assert/strict'
import test from 'node:test'
import {
  isAllowedBackendMethod,
  isAllowedBackendPath,
  normalizeBackendOrigin,
  requestBodyByteLength,
  MAX_BACKEND_REQUEST_BYTES,
} from '../src/lib/backend-proxy-policy.ts'

test('backend proxy policy accepts only the configured origin and API paths', () => {
  assert.equal(normalizeBackendOrigin('https://backend.example:8443/'), 'https://backend.example:8443')
  assert.equal(normalizeBackendOrigin('file:///tmp/backend'), null)
  assert.equal(normalizeBackendOrigin('https://backend.example/api'), null)
  assert.equal(normalizeBackendOrigin('https://user:secret@backend.example'), null)
  assert.equal(isAllowedBackendPath('/api/auth/login'), true)
  assert.equal(isAllowedBackendPath('/admin/secrets'), false)
  assert.equal(isAllowedBackendPath('/api/../admin'), false)
  assert.equal(isAllowedBackendMethod('GET'), true)
  assert.equal(isAllowedBackendMethod('OPTIONS'), false)
})

test('backend proxy request body policy counts UTF-8 bytes', () => {
  assert.equal(requestBodyByteLength('a'), 1)
  assert.equal(requestBodyByteLength('中'), 3)
  assert.equal(requestBodyByteLength('x'.repeat(MAX_BACKEND_REQUEST_BYTES)), MAX_BACKEND_REQUEST_BYTES)
  assert.equal(requestBodyByteLength('x'.repeat(MAX_BACKEND_REQUEST_BYTES + 1)), MAX_BACKEND_REQUEST_BYTES + 1)
})
