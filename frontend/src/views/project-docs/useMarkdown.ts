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
export function renderMarkdown(src: string): RenderResult {
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

  const html = md.renderer.render(tokens, md.options, {})
  return { html, toc }
}
