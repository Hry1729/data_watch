/**
 * Markdown 渲染（项目文档专用）
 *
 * - 基于 markdown-it，开启表格/链接识别；
 * - 给 h2/h3 注入锚点 id，并产出目录(TOC)；
 * - 代码块沿用 markdown-it 默认 `language-*` 前缀，交给全局 `v-highlight` 指令高亮，
 *   不在此处直接高亮，避免与指令重复处理。
 */
import MarkdownIt from 'markdown-it'

export interface TocItem {
  level: 2 | 3
  text: string
  id: string
}

export interface RenderResult {
  html: string
  toc: TocItem[]
}

export interface RenderOptions {
  /** 当前文档相对 docs/ 的路径，用于解析正文图片的相对路径 */
  file?: string
  /** 相对 docs/ 根的图片路径 → 打包后 URL（来自 docs.config.getDocImageUrl） */
  resolveImage?: (relToDocsRoot: string) => string | undefined
}

/** 把文档内相对路径（相对当前 .md 文件）解析为相对 docs/ 根的路径 */
function resolveDocRelative(currentFile: string, href: string): string | null {
  const clean = href.split('#')[0].split('?')[0]
  if (!clean) return null
  const stack = clean.startsWith('/') ? [] : currentFile.split('/').slice(0, -1)
  for (const part of clean.split('/')) {
    if (part === '' || part === '.') continue
    if (part === '..') stack.pop()
    else stack.push(part)
  }
  return stack.join('/')
}

const md = new MarkdownIt({
  html: true,
  linkify: true,
  breaks: false,
  langPrefix: 'language-'
})

// mermaid 代码块改输出为 <div class="mermaid">，交给阅读页用 mermaid 渲染成 SVG，
// 同时避开 v-highlight（它只处理 pre code）。其余语言走默认代码块渲染。
const defaultFence = md.renderer.rules.fence?.bind(md.renderer)
md.renderer.rules.fence = (tokens, idx, options, env, self) => {
  const token = tokens[idx]
  if (token.info.trim().toLowerCase() === 'mermaid') {
    return `<div class="mermaid">${md.utils.escapeHtml(token.content)}</div>`
  }
  return defaultFence
    ? defaultFence(tokens, idx, options, env, self)
    : self.renderToken(tokens, idx, options)
}

/** 渲染 markdown 原文 → { html, toc } */
export function renderMarkdown(src: string, opts?: RenderOptions): RenderResult {
  const toc: TocItem[] = []
  const tokens = md.parse(src ?? '', {})

  let headingIndex = 0
  for (let i = 0; i < tokens.length; i++) {
    const t = tokens[i]
    if (t.type !== 'heading_open') continue
    if (t.tag !== 'h2' && t.tag !== 'h3') continue

    const inline = tokens[i + 1]
    const text = inline?.content?.trim() || ''
    const id = `doc-h-${headingIndex++}`
    t.attrSet('id', id)
    toc.push({ level: t.tag === 'h2' ? 2 : 3, text, id })
  }

  // 把正文相对图片路径重写成 Vite 打包后的 URL，否则 SPA 路由下 <img> 会裂。
  // 跳过绝对/外链/data: 路径；解析不到打包资源时原样保留，不引入回归。
  if (opts?.file && opts.resolveImage) {
    for (const t of tokens) {
      if (t.type !== 'inline' || !t.children) continue
      for (const child of t.children) {
        if (child.type !== 'image') continue
        const rawSrc = child.attrGet('src')
        if (!rawSrc || /^(https?:|data:|\/)/i.test(rawSrc)) continue
        const rel = resolveDocRelative(opts.file, rawSrc)
        const url = rel ? opts.resolveImage(rel) : undefined
        if (url) child.attrSet('src', url)
      }
    }
  }

  const html = md.renderer.render(tokens, md.options, {})
  return { html, toc }
}
