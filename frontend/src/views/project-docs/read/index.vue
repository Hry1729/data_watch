<!-- 项目文档 · 阅读页：渲染单篇 markdown，带目录与代码高亮 -->
<template>
  <div class="docs-reader art-full-height">
    <!-- 顶栏：返回 + 面包屑 -->
    <div class="docs-reader__bar art-card">
      <ElButton text class="docs-reader__back" @click="goBack">
        <ArtSvgIcon icon="ri:arrow-left-line" class="docs-reader__back-icon" />
        返回
      </ElButton>
      <div class="docs-reader__crumb">
        <span class="docs-reader__crumb-link" @click="goOverview">总览</span>
        <template v-if="doc">
          <ArtSvgIcon icon="ri:arrow-right-s-line" class="docs-reader__crumb-sep" />
          <span>{{ doc.categoryTitle }}</span>
          <ArtSvgIcon icon="ri:arrow-right-s-line" class="docs-reader__crumb-sep" />
          <span class="docs-reader__crumb-current">{{ doc.title }}</span>
        </template>
      </div>
    </div>

    <div v-if="doc" class="docs-reader__layout">
      <!-- 正文 -->
      <div ref="contentEl" class="docs-reader__content art-card" @click="onContentClick">
        <h1 class="docs-reader__doc-title">{{ doc.title }}</h1>
        <div v-highlight class="markdown-body" v-html="rendered.html"></div>
      </div>

      <!-- 目录 -->
      <div v-if="rendered.toc.length" class="docs-reader__toc art-card">
        <div class="docs-reader__toc-title">目录</div>
        <ul class="docs-reader__toc-list">
          <li
            v-for="item in rendered.toc"
            :key="item.id"
            :class="['docs-reader__toc-item', `is-level-${item.level}`]"
            @click="scrollTo(item.id)"
          >
            {{ item.text }}
          </li>
        </ul>
      </div>
    </div>

    <ElEmpty v-else description="未找到该文档" />
  </div>
</template>

<script setup lang="ts">
  import { computed, ref, watch, nextTick, onMounted } from 'vue'
  import { useRoute, useRouter } from 'vue-router'
  import { renderMarkdown } from '../useMarkdown'
  import { getDocBySlug, getDocContent, getSlugByFile } from '../docs.config'
  // 复用项目自带的 one-dark-pro 代码主题（仅在阅读文档时加载）
  import '@styles/custom/one-dark-pro.scss'

  defineOptions({ name: 'DocsReader' })

  const route = useRoute()
  const router = useRouter()
  const contentEl = ref<HTMLElement | null>(null)

  const slug = computed(() => String(route.params.slug || ''))
  const doc = computed(() => getDocBySlug(slug.value))
  const rendered = computed(() => {
    if (!doc.value) return { html: '', toc: [] as ReturnType<typeof renderMarkdown>['toc'] }
    return renderMarkdown(getDocContent(doc.value.file))
  })

  // mermaid 按需加载：仅当文档含图时才拉取并渲染成 SVG
  let mermaidApi: typeof import('mermaid').default | null = null
  const renderMermaid = async () => {
    const el = contentEl.value
    if (!el) return
    const nodes = Array.from(el.querySelectorAll<HTMLElement>('.mermaid')).filter(
      (n) => !n.dataset.processed
    )
    if (!nodes.length) return
    try {
      if (!mermaidApi) mermaidApi = (await import('mermaid')).default
      const isDark = document.documentElement.classList.contains('dark')
      mermaidApi.initialize({
        startOnLoad: false,
        theme: isDark ? 'dark' : 'default',
        securityLevel: 'loose'
      })
      await mermaidApi.run({ nodes, suppressErrors: true })
    } catch (e) {
      console.warn('[docs] mermaid 渲染失败', e)
    }
  }

  // 首次进入与切换文档后，等 DOM 更新再渲染图
  onMounted(async () => {
    await nextTick()
    renderMermaid()
  })
  watch(
    () => rendered.value.html,
    async () => {
      await nextTick()
      renderMermaid()
    }
  )

  const goBack = () => {
    if (window.history.length > 1) {
      router.back()
    } else {
      goOverview()
    }
  }
  const goOverview = () => router.push('/project-docs/overview')

  const scrollTo = (id: string) => {
    const el = contentEl.value?.querySelector<HTMLElement>(`#${CSS.escape(id)}`)
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }

  /** 解析文档内相对 .md 路径为相对 docs/ 的路径 */
  const resolveRelativeDoc = (currentFile: string, href: string): string | null => {
    const clean = href.split('#')[0].split('?')[0]
    if (!clean.endsWith('.md')) return null
    const stack = clean.startsWith('/') ? [] : currentFile.split('/').slice(0, -1)
    for (const part of clean.split('/')) {
      if (part === '' || part === '.') continue
      if (part === '..') stack.pop()
      else stack.push(part)
    }
    return stack.join('/')
  }

  /** 拦截正文里指向其它 .md 的链接，转为站内文档跳转 */
  const onContentClick = (e: MouseEvent) => {
    const anchor = (e.target as HTMLElement)?.closest('a')
    if (!anchor) return
    const href = anchor.getAttribute('href') || ''
    if (/^https?:\/\//i.test(href)) {
      anchor.setAttribute('target', '_blank')
      anchor.setAttribute('rel', 'noopener')
      return
    }
    if (!doc.value) return
    const resolved = resolveRelativeDoc(doc.value.file, href)
    if (!resolved) return
    const targetSlug = getSlugByFile(resolved)
    e.preventDefault()
    if (targetSlug) {
      router.push(`/project-docs/read/${targetSlug}`)
    }
  }
</script>

<style scoped lang="scss">
  .docs-reader {
    display: flex;
    flex-direction: column;
    gap: 16px;
    height: 100%;

    &__bar {
      display: flex;
      gap: 12px;
      align-items: center;
      padding: 10px 16px;
    }

    &__back-icon {
      margin-right: 2px;
      font-size: 16px;
    }

    &__crumb {
      display: flex;
      gap: 4px;
      align-items: center;
      font-size: 13px;
      color: var(--el-text-color-secondary);
    }

    &__crumb-sep {
      font-size: 14px;
    }

    &__crumb-link {
      cursor: pointer;
      transition: color 0.2s;

      &:hover {
        color: var(--el-color-primary);
      }
    }

    &__crumb-current {
      color: var(--el-text-color-primary);
    }

    &__layout {
      display: flex;
      flex: 1;
      gap: 16px;
      min-height: 0;
    }

    &__content {
      flex: 1;
      min-width: 0;
      padding: 28px 32px;
      overflow-y: auto;
    }

    &__doc-title {
      margin: 0 0 20px;
      font-size: 24px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    &__toc {
      flex-shrink: 0;
      width: 230px;
      max-height: 100%;
      padding: 18px 16px;
      overflow-y: auto;

      @media (max-width: 1100px) {
        display: none;
      }
    }

    &__toc-title {
      margin-bottom: 12px;
      font-size: 13px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    &__toc-list {
      padding: 0;
      margin: 0;
      list-style: none;
    }

    &__toc-item {
      padding: 5px 8px;
      font-size: 13px;
      line-height: 1.5;
      color: var(--el-text-color-secondary);
      cursor: pointer;
      border-left: 2px solid transparent;
      transition: all 0.2s;

      &:hover {
        color: var(--el-color-primary);
        background: var(--el-color-primary-light-9);
        border-left-color: var(--el-color-primary);
      }

      &.is-level-3 {
        padding-left: 20px;
        font-size: 12px;
      }
    }
  }

  /* ---- markdown 正文样式（v-html 内容需用 :deep） ---- */
  :deep(.markdown-body) {
    font-size: 14px;
    line-height: 1.8;
    color: var(--el-text-color-regular);
    word-wrap: break-word;

    h1,
    h2,
    h3,
    h4 {
      margin: 28px 0 14px;
      font-weight: 600;
      line-height: 1.4;
      color: var(--el-text-color-primary);
      scroll-margin-top: 12px;
    }

    h1 {
      font-size: 22px;
    }

    h2 {
      padding-bottom: 8px;
      font-size: 19px;
      border-bottom: 1px solid var(--art-card-border, var(--el-border-color-light));
    }

    h3 {
      font-size: 16px;
    }

    h4 {
      font-size: 14px;
    }

    p {
      margin: 12px 0;
    }

    a {
      color: var(--el-color-primary);
      text-decoration: none;

      &:hover {
        text-decoration: underline;
      }
    }

    ul,
    ol {
      padding-left: 24px;
      margin: 12px 0;
    }

    li {
      margin: 6px 0;
    }

    blockquote {
      padding: 8px 16px;
      margin: 14px 0;
      color: var(--el-text-color-secondary);
      border-left: 4px solid var(--el-color-primary-light-5);
      background: var(--el-fill-color-light);
      border-radius: 0 6px 6px 0;

      p {
        margin: 4px 0;
      }
    }

    img {
      max-width: 100%;
      border-radius: 6px;
    }

    hr {
      margin: 24px 0;
      border: none;
      border-top: 1px solid var(--art-card-border, var(--el-border-color-light));
    }

    /* 行内代码 */
    code {
      padding: 2px 6px;
      margin: 0 2px;
      font-family: 'JetBrains Mono', 'Fira Code', monospace;
      font-size: 0.86em;
      color: var(--el-color-primary);
      background: var(--el-color-primary-light-9);
      border-radius: 4px;
    }

    /* 表格 */
    table {
      display: block;
      width: 100%;
      margin: 16px 0;
      overflow-x: auto;
      border-collapse: collapse;
    }

    th,
    td {
      padding: 8px 12px;
      font-size: 13px;
      border: 1px solid var(--art-card-border, var(--el-border-color-light));
    }

    th {
      font-weight: 600;
      color: var(--el-text-color-primary);
      background: var(--el-fill-color-light);
    }

    tr:nth-child(even) td {
      background: var(--el-fill-color-lighter);
    }

    /* mermaid 图（由阅读页脚本渲染成 SVG） */
    .mermaid {
      display: flex;
      justify-content: center;
      margin: 18px 0;
      overflow-x: auto;
      text-align: center;

      svg {
        max-width: 100%;
        height: auto;
      }
    }

    // 渲染完成前隐藏原始源码，避免闪烁
    .mermaid:not([data-processed]) {
      opacity: 0;
    }

    /* 代码块（高亮由 v-highlight + one-dark-pro 主题提供） */
    pre {
      position: relative;
      padding: 14px 16px;
      margin: 16px 0;
      overflow-x: auto;
      background: #282c34;
      border-radius: 8px;

      code {
        padding: 0;
        margin: 0;
        font-family: 'JetBrains Mono', 'Fira Code', monospace;
        font-size: 13px;
        color: #abb2bf;
        background: transparent;
        border-radius: 0;
      }

      .line-number {
        display: inline-block;
        width: 2em;
        margin-right: 12px;
        color: #5c6370;
        text-align: right;
        user-select: none;
      }

      .copy-button {
        position: absolute;
        top: 10px;
        right: 10px;
        display: flex;
        align-items: center;
        color: #abb2bf;
        cursor: pointer;
        opacity: 0;
        transition: opacity 0.2s;
      }

      &:hover .copy-button {
        opacity: 0.7;
      }

      .copy-button:hover {
        opacity: 1;
      }
    }
  }
</style>
