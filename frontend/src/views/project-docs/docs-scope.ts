/**
 * 「项目文档」的**可见范围**判定（不含任何构建期 glob 依赖，可被 node 测试直接导入）。
 *
 * 内部材料（整改台账、综合审查执行清单、会话总结）必须**保留在仓库**作为版本历史与复查依据，
 * 但**不得进入产品前端产物**。这些内部文件包含问题清单、复现步骤、验收状态与现场待办，
 * 与「项目文档」的读者（产品使用者）不同。
 *
 * 隔离方式有两道，见 `docs.config.ts`：
 *   1. 构建期 `import.meta.glob` 负向模式（Vite 只接受字面量，故与
 *      `INTERNAL_DOC_GLOB_NEGATIVES` 手工保持一致，并由测试锁定）；
 *   2. 运行期 `isInternalDocPath()` 再过滤一次（双保险）。
 */

/** 内部材料在 `docs/` 下的路径前缀（相对 docs 根，统一用 `/` 分隔） */
export const INTERNAL_DOC_PREFIXES = [
  '整改记录/',
  'Data_Watch_综合审查与Agent整改执行清单_'
] as const

/**
 * 与 `docs.config.ts` 中 glob 负向模式**逐字对应**的字面量清单。
 * Vite 的 `import.meta.glob` 不接受变量拼接，因此这里只作为"契约声明 + 测试锚点"。
 */
export const INTERNAL_DOC_GLOB_NEGATIVES = [
  '!../../../../docs/整改记录/**',
  '!../../../../docs/Data_Watch_综合审查与Agent整改执行清单_*.md'
] as const

/** 该相对 docs/ 根的路径是否属于内部材料 */
export function isInternalDocPath(relToDocsRoot: string): boolean {
  const normalized = relToDocsRoot.replace(/\\/g, '/')
  return INTERNAL_DOC_PREFIXES.some((prefix) => normalized.includes(prefix))
}
