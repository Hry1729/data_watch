/**
 * H7 回归：开发服务器不得把仓库根（含 deploy/backend.env 一类密钥文件）暴露到局域网。
 *   1. 默认只监听本机，局域网监听需显式设置 DATAWATCH_DEV_HOST；
 *   2. fs.allow 只放行 frontend 自身与仓库根 docs/；
 *   3. fs.deny 拦截 *.env / *.env.* / 证书私钥 / toml 等文件（保留 Vite 默认项）。
 */

import assert from 'node:assert/strict'
import path from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { createServer, loadConfigFromFile } from 'vite'
import { DEV_FS_DENY, devServerHost } from '../vite.config'

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const repoRoot = path.resolve(frontendRoot, '..')

test('dev server listens on localhost unless DATAWATCH_DEV_HOST opts in', () => {
  assert.equal(devServerHost(undefined), false)
  assert.equal(devServerHost(''), false)
  assert.equal(devServerHost('true'), true)
  assert.equal(devServerHost('192.168.1.20'), '192.168.1.20')
})

test('dev server fs.allow is narrowed to frontend and docs, deny keeps Vite defaults', async () => {
  const previous = process.env.DATAWATCH_DEV_HOST
  delete process.env.DATAWATCH_DEV_HOST
  const cwd = process.cwd()
  process.chdir(frontendRoot)
  try {
    const loaded = await loadConfigFromFile(
      { command: 'serve', mode: 'development' },
      path.join(frontendRoot, 'vite.config.ts'),
      frontendRoot,
      'silent'
    )
    assert.ok(loaded)
    const config = loaded.config
    assert.equal(config.server?.host, false)
    const allow = (config.server?.fs?.allow ?? []).map((item) => path.resolve(item))
    assert.ok(!allow.includes(repoRoot), '不得放行整个仓库根')
    assert.ok(allow.includes(path.join(repoRoot, 'docs')))
    for (const pattern of ['.env', '.env.*', '*.env', '**/.git/**']) {
      assert.ok(DEV_FS_DENY.includes(pattern), pattern)
    }
    assert.deepEqual(config.server?.fs?.deny, DEV_FS_DENY)
  } finally {
    process.chdir(cwd)
    if (previous !== undefined) process.env.DATAWATCH_DEV_HOST = previous
  }
})

test('dev server refuses repo-root files and env-style secrets over /@fs/', async () => {
  const cwd = process.cwd()
  process.chdir(frontendRoot)
  const server = await createServer({
    configFile: path.join(frontendRoot, 'vite.config.ts'),
    mode: 'development',
    logLevel: 'silent',
    optimizeDeps: { noDiscovery: true, include: [] },
    server: { port: 0, strictPort: false, hmr: false, open: false }
  })
  try {
    await server.listen()
    // 默认只绑定本机回环地址
    assert.deepEqual(server.resolvedUrls?.network, [])
    const base = (server.resolvedUrls?.local[0] ?? '').replace(/\/$/, '')
    assert.ok(base)
    const fetchFs = (file: string) => fetch(`${base}/@fs/${file.split(path.sep).join('/').replace(/^\//, '')}`)

    // deploy/ 不在 allow 内：backend.env（及其模板）均不可读取
    const secret = await fetchFs(path.join(repoRoot, 'deploy', 'backend.env.example'))
    assert.equal(secret.status, 403)
    const readme = await fetchFs(path.join(repoRoot, 'README.md'))
    assert.equal(readme.status, 403)
    // frontend 自身的 .env 也命中 deny
    const ownEnv = await fetchFs(path.join(frontendRoot, '.env'))
    assert.equal(ownEnv.status, 403)
  } finally {
    await server.close()
    process.chdir(cwd)
  }
})
