/**
 * 打包边界回归：内部整改台账/审查材料**不得**进入产品前端产物。
 *
 * 背景（本轮取证）：`docs.config.ts` 原用 eager 的 `import.meta.glob` 把 `docs/` 下所有
 * markdown 内联进 bundle ⇒ `docs/整改记录/**`（含问题清单、复现步骤、验收状态）以及综合审查
 * 执行清单会被打进前端产物；在既有产物 `frontend/dist/assets/index-*.js` 中已能命中
 * 「整改 / 复核会话 / 综合审查」。
 *
 * 本测试锁定：
 *   1. 判定函数对内部材料为 true、对官方产品文档为 false；
 *   2. `docs.config.ts` 中**两处** glob 都写了"整改记录"负向模式（构建期真的不读入）；
 *   3. 导出的字面量清单与源码中的模式一致（防止只改一处而静默失效）。
 *
 * 注意：本测试只验证"边界判定与构建配置"，**不等于**已验证产物内容——产物级验证由构建后的
 * grep 检查完成（见整改记录）。
 */

import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import {
  INTERNAL_DOC_GLOB_NEGATIVES,
  INTERNAL_DOC_PREFIXES,
  isInternalDocPath
} from '../src/views/project-docs/docs-scope'

const CONFIG_PATH = new URL('../src/views/project-docs/docs.config.ts', import.meta.url)

test('内部整改台账与审查材料被判定为内部材料', () => {
  assert.equal(isInternalDocPath('整改记录/总状态表.md'), true)
  assert.equal(isInternalDocPath('整改记录/复核会话改动总结-20261005.md'), true)
  assert.equal(isInternalDocPath('整改记录/候选包冻结记录.md'), true)
  assert.equal(isInternalDocPath('Data_Watch_综合审查与Agent整改执行清单_6845e80.md'), true)
})

test('官方产品文档不被误判为内部材料', () => {
  const official = [
    '01-总体设计/系统架构总览.md',
    '01-总体设计/架构决策记录.md',
    '02-模块设计/writer/README.md',
    '03-数据与契约/数据库/Redis结构与字段.md',
    '04-运维部署/安全边界.md',
    'README.md'
  ]
  for (const rel of official) {
    assert.equal(isInternalDocPath(rel), false, `不应被判为内部材料: ${rel}`)
  }
})

test('docs.config.ts 的两处 glob 都排除了整改台账', () => {
  const source = readFileSync(CONFIG_PATH, 'utf8')
  const literal = "'!../../../../docs/整改记录/**'"
  const occurrences = source.split(literal).length - 1
  assert.ok(
    occurrences >= 2,
    `"整改记录" 负向模式应同时出现在 markdown 与图片两处 glob（实际 ${occurrences} 处）`
  )
})

test('导出的负向模式清单与源码字面量一致', () => {
  const source = readFileSync(CONFIG_PATH, 'utf8')
  for (const pattern of INTERNAL_DOC_GLOB_NEGATIVES) {
    assert.ok(
      source.includes(`'${pattern}'`),
      `docs.config.ts 缺少负向模式字面量（Vite 不接受变量）：${pattern}`
    )
  }
})

test('前缀清单覆盖两类内部材料', () => {
  assert.deepEqual(
    [...INTERNAL_DOC_PREFIXES],
    ['整改记录/', 'Data_Watch_综合审查与Agent整改执行清单_']
  )
})
