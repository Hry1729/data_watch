/**
 * 全局错误处理模块
 *
 * 提供统一的错误捕获和处理机制
 *
 * ## 主要功能
 *
 * - Vue 运行时错误捕获（组件错误、生命周期错误等）
 * - 全局脚本错误捕获（语法错误、运行时错误等）
 * - Promise 未捕获错误处理（unhandledrejection）
 * - 静态资源加载错误监控（图片、脚本、样式等）
 * - 错误日志记录和上报到后端
 * - 统一的错误处理入口
 *
 * ## 使用场景
 * - 应用启动时安装全局错误处理器
 * - 捕获和记录所有类型的错误
 * - 错误上报到监控平台
 * - 提升应用稳定性和可维护性
 * - 问题排查和调试
 *
 * ## 错误类型
 *
 * - VueError: Vue 组件相关错误
 * - ScriptError: JavaScript 脚本错误
 * - PromiseError: Promise 未捕获的 rejection
 * - ResourceError: 静态资源加载失败
 *
 * @module utils/sys/error-handle
 * @author Art Design Pro Team
 */
import type { App } from 'vue'
import { reportClientLog } from '@/api/device-monitor'

const IGNORABLE_SCRIPT_ERRORS = [
  'ResizeObserver loop completed with undelivered notifications.',
  'ResizeObserver loop limit exceeded'
]

// 上报队列，避免重复上报相同错误
const pendingReports: Map<string, number> = new Map()
const REPORT_COOLDOWN_MS = 5000 // 相同错误 5秒内不重复上报

function normalizeErrorMessage(message: Event | string): string {
  if (typeof message === 'string') {
    return message
  }

  if ('message' in message && typeof message.message === 'string') {
    return message.message
  }

  return ''
}

function isIgnorableScriptError(message: Event | string, source?: string): boolean {
  const normalizedMessage = normalizeErrorMessage(message)

  if (!normalizedMessage) {
    return false
  }

  if (IGNORABLE_SCRIPT_ERRORS.some((item) => normalizedMessage.includes(item))) {
    // 浏览器/扩展在布局抖动时常见的 ResizeObserver 噪声，不作为真实异常处理
    return true
  }

  // 浏览器扩展注入脚本偶发的跨域 Script error 也没有排查价值
  if (normalizedMessage === 'Script error.' && source === '') {
    return true
  }

  return false
}

/**
 * 判断是否应该上报（避免重复上报）
 */
function shouldReport(errorKey: string): boolean {
  const now = Date.now()
  const lastReportTime = pendingReports.get(errorKey)

  if (lastReportTime && now - lastReportTime < REPORT_COOLDOWN_MS) {
    return false
  }

  return true
}

/**
 * 安全序列化对象（处理循环引用）
 */
function safeStringify(obj: unknown, maxLength: number = 200): string {
  try {
    const seen = new WeakSet()
    const result = JSON.stringify(obj, (key, value) => {
      if (typeof value === 'object' && value !== null) {
        if (seen.has(value)) {
          return '[Circular]'
        }
        seen.add(value)
      }
      return value
    })
    return result.slice(0, maxLength)
  } catch {
    return String(obj).slice(0, maxLength)
  }
}

/**
 * 执行错误上报
 */
function doReport(errorKey: string, payload: Omit<Api.DeviceMonitor.LogClientReportParams, 'type'>) {
  pendingReports.set(errorKey, Date.now())

  reportClientLog({
    type: 'frontend',
    ...payload
  }).catch(() => {
    // 上报失败，静默处理
  })

  // 5分钟后清理队列
  setTimeout(() => {
    pendingReports.delete(errorKey)
  }, REPORT_COOLDOWN_MS)
}

/**
 * Vue 运行时错误处理
 */
export function vueErrorHandler(err: unknown, instance: any, info: string) {
  console.error('[VueError]', err, info, instance)

  const error = err instanceof Error ? err : new Error(String(err))
  const errorKey = `vue:${error.message}:${info}`

  if (!shouldReport(errorKey)) return

  doReport(errorKey, {
    level: 'ERROR',
    source: 'frontend-vue',
    content: error.message.slice(0, 200),
    detail: {
      url: window.location.href,
      componentName: instance?.$options?.name || instance?.type?.name || 'Unknown',
      lifecycleHook: info,
      errorType: error.name
    },
    exceptionStack: error.stack || ''
  })
}

/**
 * 全局脚本错误处理
 */
export function scriptErrorHandler(
  message: Event | string,
  source?: string,
  lineno?: number,
  colno?: number,
  error?: Error
): boolean {
  if (isIgnorableScriptError(message, source)) {
    // 忽略类噪声：阻止默认输出
    return true
  }

  console.error('[ScriptError]', { message, source, lineno, colno, error })

  const normalizedMessage = normalizeErrorMessage(message)
  const errorKey = `script:${normalizedMessage}:${source}:${lineno}`

  if (!shouldReport(errorKey)) return false // 已上报过，保留默认输出

  doReport(errorKey, {
    level: 'ERROR',
    source: 'frontend-runtime',
    content: normalizedMessage.slice(0, 200),
    detail: {
      url: window.location.href,
      file: source,
      line: lineno,
      column: colno,
      errorType: error?.name || 'Error'
    },
    exceptionStack: error?.stack || ''
  })

  // 真实错误：保留浏览器默认错误输出（sourcemap 链接等）
  return false
}

/**
 * Promise 未捕获错误处理
 */
export function registerPromiseErrorHandler() {
  window.addEventListener('unhandledrejection', (event) => {
    console.error('[PromiseError]', event.reason)

    const reason = event.reason
    const errorKey = `promise:${String(reason)?.slice(0, 100)}`

    if (!shouldReport(errorKey)) return

    let content = '未处理的 Promise 异常'
    let stack = ''

    if (reason instanceof Error) {
      content = reason.message.slice(0, 200)
      stack = reason.stack || ''
    } else if (typeof reason === 'string') {
      content = reason.slice(0, 200)
    } else if (reason && typeof reason === 'object') {
      content = safeStringify(reason, 200)
    }

    doReport(errorKey, {
      level: 'ERROR',
      source: 'frontend-runtime',
      content,
      detail: {
        url: window.location.href,
        errorType: reason?.constructor?.name || 'UnhandledPromiseRejection'
      },
      exceptionStack: stack
    })
  })
}

/**
 * 资源加载错误处理 (img, script, css...)
 */
export function registerResourceErrorHandler() {
  window.addEventListener(
    'error',
    (event: Event) => {
      const target = event.target as HTMLElement
      if (
        target &&
        (target.tagName === 'IMG' || target.tagName === 'SCRIPT' || target.tagName === 'LINK')
      ) {
        console.error('[ResourceError]', {
          tagName: target.tagName,
          src:
            (target as HTMLImageElement).src ||
            (target as HTMLScriptElement).src ||
            (target as HTMLLinkElement).href
        })

        const src =
          (target as HTMLImageElement).src ||
          (target as HTMLScriptElement).src ||
          (target as HTMLLinkElement).href || ''

        const errorKey = `resource:${target.tagName}:${src.slice(0, 100)}`

        if (!shouldReport(errorKey)) return

        doReport(errorKey, {
          level: 'WARN',
          source: 'frontend-resource',
          content: `资源加载失败: ${target.tagName}`,
          detail: {
            url: window.location.href,
            tagName: target.tagName,
            src
          }
        })
      }
    },
    true // 捕获阶段才能监听到资源错误
  )
}

/**
 * 安装统一错误处理
 */
export function setupErrorHandle(app: App) {
  app.config.errorHandler = vueErrorHandler
  window.onerror = scriptErrorHandler
  registerPromiseErrorHandler()
  registerResourceErrorHandler()
}
