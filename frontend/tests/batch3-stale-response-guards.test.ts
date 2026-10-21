import assert from 'node:assert/strict'
import { readFile } from 'node:fs/promises'
import test from 'node:test'

// 审查批次3：Web 端乱序响应/加载状态守卫的结构回归（与 monitor-refresh-guard 同风格的源码断言）。

const read = (path: string) => readFile(new URL(`../src/${path}`, import.meta.url), 'utf8')

test('B020/B021 role grant dialog binds grant to role and blocks save until loaded', async () => {
  const source = await read('views/system/role/modules/role-permission-dialog.vue')
  // 请求代号 + 角色一致性：旧 A 响应不能写进 B
  assert.match(source, /const version = \+\+loadVersion/)
  assert.match(source, /version === loadVersion && visible\.value && props\.roleData\?\.roleId === roleId/)
  // 关闭即作废在途请求
  assert.match(source, /const handleClose = \(\) => \{\s*loadVersion\+\+/)
  // 保存按钮在加载期/加载失败时禁用，处理函数自身也拒绝
  assert.match(source, /:disabled="!canSave"/)
  assert.match(source, /if \(!canSave\.value \|\| loadedRoleId\.value === null\)/)
  // 保存使用已加载授权所属的角色 ID
  assert.match(source, /await fetchUpdateRoleGrant\(roleId, payload\)/)
  assert.doesNotMatch(source, /fetchUpdateRoleGrant\(props\.roleData\.roleId/)
})

test('B066 device monitor: the latest request always clears the visible loading', async () => {
  const source = await read('views/device_monitor/monitor/index.vue')
  assert.match(source, /if \(requestVersion === listRequestVersion\) loading\.value = false/)
  assert.doesNotMatch(source, /if \(!silent && requestVersion === listRequestVersion\) loading\.value = false/)
})

test('B067 db log tail ignores responses for an older file/lines/request', async () => {
  const source = await read('views/device_monitor/log/db-log-panel.vue')
  assert.match(source, /const requestVersion = \+\+tailRequestVersion/)
  assert.match(source, /file === currentFile\.value && lineCount === lines\.value/)
  assert.match(source, /if \(!isCurrent\(\)\) return\s*enabled\.value = data\.enabled/)
  assert.match(source, /tailRequestVersion \+= 1/)
})

test('B068 matrix reset failure clears results without orphaning the loading mask', async () => {
  const source = await read('views/device_monitor/history/index.vue')
  const catchBlock = source.slice(source.indexOf("ElMessage.error('加载矩阵数据失败')"))
  const resetBranch = catchBlock.slice(0, catchBlock.indexOf('} finally {'))
  assert.match(resetBranch, /resetMatrixResults\(\)/)
  assert.doesNotMatch(resetBranch, /clearMatrixData\(\)/)
  // 作废在途请求时同时结束 loading
  assert.match(source, /matrixQueryVersion\.value \+= 1\s*loadingMatrix\.value = false/)
})

test('B069 chart, matrix and load-more use one frozen query snapshot', async () => {
  const source = await read('views/device_monitor/history/index.vue')
  const loadMatrix = source.slice(
    source.indexOf('const loadMatrixData = async'),
    source.indexOf('const formatMatrixValue')
  )
  assert.doesNotMatch(loadMatrix, /searchForm\.value/)
  assert.match(loadMatrix, /reset \? \(query \?\? matrixQuery\.value\) : matrixQuery\.value/)
  assert.match(source, /void loadMatrixData\(true, query\)/)
  assert.match(source, /if \(chartVersion !== chartQueryVersion\) return/)
})

test('B070 point list keeps only the latest device response and gates confirm', async () => {
  const source = await read('views/device_monitor/history/index.vue')
  assert.match(source, /const requestVersion = \+\+pointListRequestVersion/)
  assert.match(source, /if \(requestVersion !== pointListRequestVersion\) return\s*pointList\.value = points/)
  assert.match(source, /pointListDeviceId\.value === tempDeviceId\.value/)
  assert.match(source, /:disabled="!canConfirmPointSelection"/)
})

test('B071 useTable: a superseded request cannot overwrite the latest state', async () => {
  const source = await read('hooks/core/useTable.ts')
  const catchBlock = source.slice(source.indexOf('} catch (err) {', source.indexOf('const fetchData')))
  const supersededIdx = catchBlock.indexOf('if (abortController !== currentController)')
  const errorIdx = catchBlock.indexOf("loadingState.value = 'error'")
  const idleIdx = catchBlock.indexOf("loadingState.value = 'idle'")
  assert.ok(supersededIdx >= 0, 'missing superseded-request guard')
  assert.ok(supersededIdx < errorIdx && supersededIdx < idleIdx, 'guard must run before any state write')
})
