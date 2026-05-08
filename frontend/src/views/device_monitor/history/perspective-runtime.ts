import type { HistoryPerspectiveRecord } from './perspective-session'

export type PerspectiveWorkerClient = {
  table: (
    data: Record<string, unknown>[] | Record<string, string>
  ) => Promise<PerspectiveTableClient>
  terminate?: () => void
}

export type PerspectiveTableClient = {
  delete: () => Promise<void>
}

export type PerspectiveViewerClient = HTMLElement & {
  load: (table: PerspectiveTableClient) => Promise<void>
  restore: (config: Record<string, unknown>) => Promise<void>
  flush: () => Promise<void>
  resize: (force?: boolean) => Promise<void>
  delete: () => Promise<void>
}

type PerspectiveModule = {
  init_server: (input: Promise<Response>) => void
  worker: () => Promise<PerspectiveWorkerClient>
}

type PerspectiveViewerModule = {
  init_client: (input: Promise<Response>) => Promise<void>
}

type PerspectiveRuntimeWindow = Window & {
  __historyPerspectiveRuntimePromise__?: Promise<PerspectiveRuntime>
  __historyPerspectiveServerReady__?: boolean
  __historyPerspectiveViewerReadyPromise__?: Promise<void>
}

type PerspectiveRuntime = {
  perspective: PerspectiveModule
  viewer: PerspectiveViewerModule
  serverWasmUrl: string
  viewerWasmUrl: string
}

const getRuntimeWindow = () => window as PerspectiveRuntimeWindow

const normalizeDefaultExport = <T>(module: { default?: T } & T) => module.default || module

const loadPerspectiveRuntime = async (): Promise<PerspectiveRuntime> => {
  const runtimeWindow = getRuntimeWindow()

  if (!runtimeWindow.__historyPerspectiveRuntimePromise__) {
    runtimeWindow.__historyPerspectiveRuntimePromise__ = (async () => {
      const [perspectiveModule, perspectiveViewerModule, serverWasmModule, viewerWasmModule] =
        await Promise.all([
          import('@finos/perspective'),
          import('@finos/perspective-viewer'),
          import('@finos/perspective/dist/wasm/perspective-server.wasm?url'),
          import('@finos/perspective-viewer/dist/wasm/perspective-viewer.wasm?url')
        ])

      await Promise.all([
        import('@finos/perspective-viewer-datagrid'),
        import('@finos/perspective-viewer-d3fc'),
        import('@finos/perspective-viewer/dist/css/themes.css')
      ])

      return {
        perspective: normalizeDefaultExport(perspectiveModule) as PerspectiveModule,
        viewer: normalizeDefaultExport(perspectiveViewerModule) as PerspectiveViewerModule,
        serverWasmUrl: serverWasmModule.default,
        viewerWasmUrl: viewerWasmModule.default
      }
    })().catch((error) => {
      runtimeWindow.__historyPerspectiveRuntimePromise__ = undefined
      throw error
    })
  }

  return runtimeWindow.__historyPerspectiveRuntimePromise__
}

export const mapPerspectiveViewerRecords = (records: HistoryPerspectiveRecord[]) =>
  records.map((record) => ({
    ...record,
    timestamp: new Date(record.timestamp.replace(' ', 'T'))
  }))

export const ensurePerspectiveViewerRuntime = async () => {
  const runtime = await loadPerspectiveRuntime()
  const runtimeWindow = getRuntimeWindow()

  if (!runtimeWindow.__historyPerspectiveServerReady__) {
    runtime.perspective.init_server(fetch(runtime.serverWasmUrl))
    runtimeWindow.__historyPerspectiveServerReady__ = true
  }

  if (customElements.get('perspective-viewer')) {
    await customElements.whenDefined('perspective-viewer')
    return runtime
  }

  if (!runtimeWindow.__historyPerspectiveViewerReadyPromise__) {
    runtimeWindow.__historyPerspectiveViewerReadyPromise__ = runtime.viewer
      .init_client(fetch(runtime.viewerWasmUrl))
      .then(async () => {
        await customElements.whenDefined('perspective-viewer')
      })
      .catch((error) => {
        runtimeWindow.__historyPerspectiveViewerReadyPromise__ = undefined
        throw error
      })
  }

  await runtimeWindow.__historyPerspectiveViewerReadyPromise__
  return runtime
}
